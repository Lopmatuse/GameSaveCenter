#!/usr/bin/env python3
"""Cross-platform structural checks that do not replace a real Windows build."""
from __future__ import annotations

import json
import re
import sqlite3
import sys
from pathlib import Path
import xml.etree.ElementTree as ET

try:
    import yaml
except ImportError:  # pragma: no cover
    yaml = None

ROOT = Path(__file__).resolve().parents[1]
ERRORS: list[str] = []


def fail(message: str) -> None:
    ERRORS.append(message)


def read_state_store() -> str:
    """Read every partial of the SQLite store so guards survive domain splitting."""
    persistence = ROOT / "src/GameSaveCenter.Worker/Persistence"
    return "\n".join(
        path.read_text(encoding="utf-8")
        for path in sorted(persistence.glob("SqliteStateStore*.cs"))
    )


def read_dashboard_view_model() -> str:
    """Read every dashboard partial so feature guards follow extracted workspaces."""
    view_models = ROOT / "src/GameSaveCenter.Playnite/ViewModels"
    return "\n".join(
        path.read_text(encoding="utf-8")
        for path in sorted(view_models.glob("DashboardViewModel*.cs"))
    )


def check_structured_files() -> None:
    for path in ROOT.rglob("*.json"):
        if any(part in {"bin", "obj", ".git", "artifacts"} for part in path.parts):
            continue
        try:
            json.loads(path.read_text(encoding="utf-8"))
        except Exception as exc:
            fail(f"JSON invalid: {path.relative_to(ROOT)}: {exc}")

    for pattern in ("*.xaml", "*.csproj", "*.props"):
        for path in ROOT.rglob(pattern):
            if any(part in {"bin", "obj", ".git", "artifacts"} for part in path.parts):
                continue
            try:
                ET.parse(path)
            except Exception as exc:
                fail(f"XML invalid: {path.relative_to(ROOT)}: {exc}")

    manifest = ROOT / "src/GameSaveCenter.Playnite/extension.yaml"
    if yaml is not None:
        try:
            data = yaml.safe_load(manifest.read_text(encoding="utf-8"))
            for key in ("Id", "Name", "Version", "Module", "Type"):
                if not data.get(key):
                    fail(f"extension.yaml missing {key}")
        except Exception as exc:
            fail(f"YAML invalid: {manifest.relative_to(ROOT)}: {exc}")


def strip_csharp(text: str) -> str:
    """Remove comments and strings before delimiter checks; current source uses no raw strings."""
    result: list[str] = []
    i = 0
    state = "code"
    while i < len(text):
        ch = text[i]
        nxt = text[i + 1] if i + 1 < len(text) else ""
        if state == "code":
            if ch == "/" and nxt == "/":
                state = "line_comment"; result.extend("  "); i += 2; continue
            if ch == "/" and nxt == "*":
                state = "block_comment"; result.extend("  "); i += 2; continue
            if ch == '@' and nxt == '"':
                state = "verbatim"; result.extend("  "); i += 2; continue
            if ch == '"':
                state = "string"; result.append(" "); i += 1; continue
            if ch == "'":
                state = "char"; result.append(" "); i += 1; continue
            result.append(ch); i += 1; continue
        if state == "line_comment":
            if ch == "\n": state = "code"; result.append("\n")
            else: result.append(" ")
            i += 1; continue
        if state == "block_comment":
            if ch == "*" and nxt == "/": state = "code"; result.extend("  "); i += 2
            else: result.append("\n" if ch == "\n" else " "); i += 1
            continue
        if state == "verbatim":
            if ch == '"' and nxt == '"': result.extend("  "); i += 2
            elif ch == '"': state = "code"; result.append(" "); i += 1
            else: result.append("\n" if ch == "\n" else " "); i += 1
            continue
        if state in {"string", "char"}:
            quote = '"' if state == "string" else "'"
            if ch == "\\": result.extend("  "); i += 2
            elif ch == quote: state = "code"; result.append(" "); i += 1
            else: result.append("\n" if ch == "\n" else " "); i += 1
            continue
    return "".join(result)


def check_csharp_delimiters() -> None:
    pairs = {')': '(', ']': '[', '}': '{'}
    for path in list((ROOT / "src").rglob("*.cs")) + list((ROOT / "tests").rglob("*.cs")):
        clean = strip_csharp(path.read_text(encoding="utf-8"))
        stack: list[tuple[str, int]] = []
        for index, ch in enumerate(clean):
            if ch in "([{":
                stack.append((ch, index))
            elif ch in ")]}":
                if not stack or stack[-1][0] != pairs[ch]:
                    fail(f"Delimiter mismatch: {path.relative_to(ROOT)} at offset {index}")
                    break
                stack.pop()
        if stack:
            fail(f"Unclosed delimiter: {path.relative_to(ROOT)} ({stack[-1][0]})")



def local_name(value: str) -> str:
    return value.rsplit("}", 1)[-1]


def check_xaml_semantics() -> None:
    """Catch common WPF compile failures before the Windows build is available."""
    for path in (ROOT / "src/GameSaveCenter.Playnite").rglob("*.xaml"):
        try:
            tree = ET.parse(path)
        except Exception:
            continue
        root = tree.getroot()

        expected_parents = {
            "DataTemplate.Triggers": "DataTemplate",
            "ControlTemplate.Triggers": "ControlTemplate",
            "Style.Triggers": "Style",
        }
        # ElementTree has no parent pointer. Build a parent map for the same check.
        parent_map = {child: parent for parent in root.iter() for child in parent}

        for resources in [n for n in root.iter() if local_name(n.tag).endswith(".Resources")]:
            for child in resources:
                if local_name(child.tag) == "ResourceDictionary.MergedDictionaries":
                    fail(
                        f"XAML merged dictionaries require an explicit ResourceDictionary: "
                        f"{path.relative_to(ROOT)}"
                    )

        for node in root.iter():
            node_name = local_name(node.tag)
            expected = expected_parents.get(node_name)
            if expected:
                parent = parent_map.get(node)
                actual = local_name(parent.tag) if parent is not None else "<none>"
                if actual != expected:
                    fail(f"XAML trigger parent invalid: {path.relative_to(ROOT)}: {node_name} is under {actual}, expected {expected}")

        for template in [n for n in root.iter() if local_name(n.tag) in {"ControlTemplate", "DataTemplate"}]:
            names: dict[str, str] = {}
            for child in template.iter():
                for attr_name, attr_value in child.attrib.items():
                    if local_name(attr_name) == "Name" and attr_value:
                        names[attr_value] = local_name(child.tag)
            for child in template.iter():
                for attr_name, attr_value in child.attrib.items():
                    if local_name(attr_name).endswith("TargetName"):
                        if attr_value not in names:
                            fail(f"XAML TargetName missing: {path.relative_to(ROOT)}: {attr_value}")
                        elif names[attr_value].endswith("Transform"):
                            fail(f"XAML trigger targets transform directly: {path.relative_to(ROOT)}: {attr_value}")

        for style in [n for n in root.iter() if local_name(n.tag) == "Style"]:
            for setter in [n for n in style if local_name(n.tag) == "Setter" and n.attrib.get("Property") == "RenderTransform"]:
                for node in setter.iter():
                    if node is not setter and local_name(node.tag).endswith("Transform"):
                        fail(
                            f"XAML style contains animatable frozen transform: {path.relative_to(ROOT)}: "
                            f"{local_name(node.tag)}; create a per-element mutable transform in code instead"
                        )

        code_behind = path.with_suffix(path.suffix + ".cs")
        if code_behind.exists():
            code = code_behind.read_text(encoding="utf-8")
            handlers: set[str] = set()
            for node in root.iter():
                for value in node.attrib.values():
                    if re.fullmatch(r"On[A-Za-z0-9_]+", value):
                        handlers.add(value)
            for handler in handlers:
                if not re.search(rf"\b{re.escape(handler)}\s*\(", code):
                    fail(f"XAML event handler missing: {path.relative_to(ROOT)} -> {handler}")

def check_gsc_resource_references() -> None:
    """Ensure plugin-owned XAML resource names resolve within the view or shared theme dictionaries."""
    plugin_root = ROOT / "src/GameSaveCenter.Playnite"
    theme_keys: set[str] = set()
    for path in (plugin_root / "Themes").rglob("*.xaml"):
        theme_text = path.read_text(encoding="utf-8")
        theme_keys.update(re.findall(r'x:Key\s*=\s*"(Gsc[A-Za-z0-9_]+)"', theme_text))

    resource_pattern = re.compile(r'\{(?:Static|Dynamic)Resource\s+(Gsc[A-Za-z0-9_]+)')
    for path in plugin_root.rglob("*.xaml"):
        xaml = path.read_text(encoding="utf-8")
        local_keys = set(re.findall(r'x:Key\s*=\s*"(Gsc[A-Za-z0-9_]+)"', xaml))
        missing = sorted(set(resource_pattern.findall(xaml)) - local_keys - theme_keys)
        for key in missing:
            fail(f"XAML GameSaveCenter resource missing: {path.relative_to(ROOT)} -> {key}")


def check_solution() -> None:
    solution = (ROOT / "GameSaveCenter.sln").read_text(encoding="utf-8")
    project_lines = re.findall(r'^Project\([^\n]+?\) = "([^"]+)", "([^"]+)"', solution, re.M)
    names = [name for name, _ in project_lines]
    if len(names) != len(set(names)):
        fail("Solution contains duplicate projects")
    expected = {
        "GameSaveCenter.Contracts", "GameSaveCenter.Core", "GameSaveCenter.Worker",
        "GameSaveCenter.Playnite", "GameSaveCenter.Core.Tests", "GameSaveCenter.Worker.Tests",
        "GameSaveCenter.Playnite.Tests"
    }
    if set(names) != expected:
        fail(f"Solution project set mismatch: {set(names)!r}")
    for _, rel in project_lines:
        path = ROOT / rel.replace("\\", "/")
        if not path.exists():
            fail(f"Solution project missing: {rel}")
    if len(re.findall(r"^Global$", solution, re.M)) != 1 or len(re.findall(r"^EndGlobal$", solution, re.M)) != 1:
        fail("Solution Global structure is invalid")


def check_ipc_constants() -> None:
    constants_text = (ROOT / "src/GameSaveCenter.Contracts/MessageTypes.cs").read_text(encoding="utf-8")
    declared = set(re.findall(r'public const string (\w+)\s*=', constants_text))
    for path in list((ROOT / "src").rglob("*.cs")):
        for name in re.findall(r'MessageTypes\.(\w+)', path.read_text(encoding="utf-8")):
            if name not in declared:
                fail(f"Unknown MessageTypes.{name} in {path.relative_to(ROOT)}")


def check_version_consistency() -> None:
    manifest = (ROOT / "src/GameSaveCenter.Playnite/extension.yaml").read_text(encoding="utf-8")
    props = (ROOT / "Directory.Build.props").read_text(encoding="utf-8")
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")

    manifest_match = re.search(r"^Version:\s*([0-9]+\.[0-9]+\.[0-9]+)\s*$", manifest, re.M)
    prefix_match = re.search(r"<VersionPrefix>([^<]+)</VersionPrefix>", props)
    assembly_match = re.search(r"<AssemblyVersion>([^<]+)</AssemblyVersion>", props)
    sidebar_match = re.search(r'x:Name="SidebarVersionText"\s+Text="v([^"]+)"', dashboard)
    if not all((manifest_match, prefix_match, assembly_match, sidebar_match)):
        fail("Version metadata could not be parsed")
        return

    manifest_version = manifest_match.group(1)
    prefix_version = prefix_match.group(1)
    assembly_version = assembly_match.group(1)
    sidebar_version = sidebar_match.group(1)
    if not (manifest_version == prefix_version == sidebar_version):
        fail(
            "Version mismatch: "
            f"manifest={manifest_version}, VersionPrefix={prefix_version}, sidebar={sidebar_version}"
        )
    if assembly_version != f"{prefix_version}.0":
        fail(f"AssemblyVersion mismatch: expected {prefix_version}.0, got {assembly_version}")

    installer_path = ROOT / "manifests/InstallerManifest.yaml"
    addon_path = ROOT / "manifests/PlayniteAddonDatabase.yaml"
    if not installer_path.exists() or not addon_path.exists():
        fail("Playnite release manifests are missing")
        return
    if yaml is None:
        return
    try:
        installer = yaml.safe_load(installer_path.read_text(encoding="utf-8"))
        addon = yaml.safe_load(addon_path.read_text(encoding="utf-8"))
        extension_id = re.search(r"^Id:\s*(\S+)\s*$", manifest, re.M)
        package = (installer.get("Packages") or [None])[0]
        if not extension_id or installer.get("AddonId") != extension_id.group(1) or addon.get("AddonId") != extension_id.group(1):
            fail("Playnite release manifest AddonId mismatch")
        if not package or str(package.get("Version")) != manifest_version:
            fail("Playnite installer manifest version mismatch")
        expected_asset = f"/v{manifest_version}/GameSaveCenter-{manifest_version}.pext"
        if expected_asset not in str(package.get("PackageUrl", "")):
            fail("Playnite installer manifest package URL/version mismatch")
        if addon.get("Type") != "Generic" or not addon.get("InstallerManifestUrl") or not addon.get("SourceUrl"):
            fail("Playnite add-on database manifest is incomplete")
    except Exception as exc:
        fail(f"Playnite release manifest validation failed: {exc}")


def check_delivery_guards() -> None:
    forbidden = ["rclone.conf", "secrets.json", "appsettings.local.json"]
    for name in forbidden:
        for path in ROOT.rglob(name):
            if ".git" not in path.parts:
                fail(f"Secret-bearing file must not be committed: {path.relative_to(ROOT)}")
    if not (ROOT / "docs/DEVELOPMENT_PROGRESS.md").exists():
        fail("Missing development progress document")
    if not (ROOT / "docs/PROJECT_MEMORY.md").exists():
        fail("Missing project memory document")





def check_dashboard_regressions() -> None:
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    if 'SelectedTask.DurationDisplay, Mode=OneWay' not in dashboard:
        fail("DurationDisplay must use OneWay binding because it is read-only")
    run_binding_count = 0
    for path in ROOT.joinpath("src/GameSaveCenter.Playnite").rglob("*.xaml"):
        if any(part in {"bin", "obj"} for part in path.parts):
            continue
        text = path.read_text(encoding="utf-8")
        for binding in re.findall(r'<Run\b[^>]*\bText="\{Binding ([^}]*)\}"', text):
            run_binding_count += 1
            if "Mode=OneWay" not in binding:
                fail(f"Run.Text binding must explicitly use Mode=OneWay: {path.relative_to(ROOT)}: {binding}")
    if run_binding_count == 0:
        fail("Run.Text binding guard matched no XAML; check the validator regex")
    if 'ItemsSource="{Binding GamesView}"' not in dashboard or 'GameSearchText' not in dashboard:
        fail("Dashboard large-library search/filter view is missing")
    if 'ProgressBar Width="120" Height="4" IsIndeterminate="{Binding IsBusy}"' in dashboard:
        fail("Dashboard still contains the always-visible idle progress frame")
    for token in (
        'x:Key="GscFocusVisual"',
        'TextElement.Foreground="{DynamicResource GscPrimaryTextBrush}"',
        'ItemsSource="{Binding TasksView}"',
        'ItemsSource="{Binding TaskStatusFilterOptions}"',
        'TaskTypeDisplay, Mode=OneWay',
        'ItemsSource="{Binding OverviewTasks}"',
        'Header="查看完整诊断信息"',
    ):
        if token not in dashboard:
            fail(f"Dashboard design-system guard is missing: {token}")
    responsive = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml.cs").read_text(encoding="utf-8")
    for boundary in ("width >= 1280", "width >= 980", "width >= 880", "height >= 760"):
        if boundary not in responsive:
            fail(f"Unified responsive breakpoint is missing: {boundary}")
    tokens = (ROOT / "src/GameSaveCenter.Playnite/Themes/DesignTokens.xaml").read_text(encoding="utf-8")
    for token in ('x:Key="GscSharedFocusVisual"', 'x:Key="GscCheckBox"', 'x:Key="GscScrollThumb"'):
        if token not in tokens:
            fail(f"Shared control template guard is missing: {token}")
    vertical_thumb = tokens.split('x:Key="GscVerticalScrollThumbTemplate"', 1)[1].split('</ControlTemplate>', 1)[0]
    horizontal_thumb = tokens.split('x:Key="GscHorizontalScrollThumbTemplate"', 1)[1].split('</ControlTemplate>', 1)[0]
    for name, thumb in (("vertical", vertical_thumb), ("horizontal", horizontal_thumb)):
        if '<Ellipse' in thumb:
            fail(f"{name} scrollbar Thumb must not use overlapping ellipse caps")
        if thumb.count('<Rectangle') != 1 or 'RadiusX="4"' not in thumb or 'RadiusY="4"' not in thumb:
            fail(f"{name} scrollbar Thumb must use one rounded Rectangle")
    for token in (
        'xmlns:sys="clr-namespace:System;assembly=mscorlib"',
        'x:Key="{x:Static SystemParameters.VerticalScrollBarButtonHeightKey}">72',
        'x:Key="{x:Static SystemParameters.HorizontalScrollBarButtonWidthKey}">72',
    ):
        if token not in tokens:
            fail(f"Scrollbar minimum Thumb resource guard is missing: {token}")
    coordinator = (ROOT / "src/GameSaveCenter.Worker/Services/GameSessionCoordinator.cs").read_text(encoding="utf-8")
    plugin = (ROOT / "src/GameSaveCenter.Playnite/GameSaveCenterPlugin.cs").read_text(encoding="utf-8")
    if "Math.Max(1, policy.DuringPlayIntervalMinutes)" not in coordinator:
        fail("During-play backup must honor the documented one-minute minimum")
    if "TimeSpan.FromSeconds(5)" not in coordinator:
        fail("During-play backup scheduler must check frequently enough for one-minute policies")
    if "NextBackupUtc.AddMinutes(intervalMinutes)" not in coordinator:
        fail("During-play backup cadence must remain anchored instead of accumulating polling drift")
    if "BackupPending" not in coordinator or "Interlocked.CompareExchange" not in coordinator:
        fail("During-play backup scheduler must prevent overlapping backup requests")
    if "TimedAutomationEnabled" not in coordinator:
        fail("During-play automation scheduler must re-anchor when its policy is enabled during a session")
    if "taskNotificationTimer" not in plugin or "MessageTypes.GetTasks" not in plugin:
        fail("Application-lifetime task notification monitor is missing")
    if "notifiedTaskIds.TryAdd(task.TaskId" not in plugin:
        fail("Task notifications must be de-duplicated by task ID")
    if "LimitNotificationText(task.DetailMessage)" not in plugin:
        fail("Successful task notifications must preserve exact worker result details")
    if 'TextOptions.TextRenderingMode="ClearType"' not in dashboard:
        fail("Dashboard ClearType rendering guard is missing")


def check_media_inbox_guards() -> None:
    messages = (ROOT / "src/GameSaveCenter.Contracts/MessageTypes.cs").read_text(encoding="utf-8")
    operations = (ROOT / "src/GameSaveCenter.Contracts/OperationDtos.cs").read_text(encoding="utf-8")
    store = read_state_store()
    service = (ROOT / "src/GameSaveCenter.Worker/Services/MediaSyncService.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")

    for token in ("ListUnassignedMedia", "IgnoreMedia"):
        if token not in messages:
            fail(f"Media inbox IPC constant missing: {token}")
    if "SharedOnly" not in operations or "IgnoreMediaRequestDto" not in operations:
        fail("Media inbox operation DTOs are incomplete")
    ensure_pos = store.find('EnsureColumnAsync(connection, "media", "classification_state"')
    index_pos = store.find('CREATE INDEX IF NOT EXISTS ix_media_classification')
    schema_match = re.search(r'private const string Schema = @"(.*?)";\s*\}', store, re.S)
    if ensure_pos < 0 or index_pos < 0 or index_pos < ensure_pos:
        fail("Media classification index must be created after the legacy column migration")
    if not schema_match:
        fail("Could not locate SQLite base schema")
    elif "ix_media_classification" in schema_match.group(1):
        fail("Media classification index is still embedded in the base schema and can break legacy upgrades")
    for token in ("GetUnassignedMediaAsync", "AssignMediaAsync", "IgnoreMediaAsync"):
        if token not in store:
            fail(f"Media inbox persistence method missing: {token}")
    for token in ("_Inbox", "RelocateArchivedCopyAsync", "EnsureArchivedCopyAsync", "SharedMediaResolution"):
        if token not in service:
            fail(f"Media inbox service guard missing: {token}")
    if "File.Delete(item.OriginalPath)" in service or "File.Move(item.OriginalPath" in service:
        fail("Media inbox must never delete or move the original capture")
    for token in ("UnassignedMedia", "AssignInboxMediaCommand", "IgnoreInboxMediaCommand"):
        if token not in view_model or token not in dashboard:
            fail(f"Media inbox UI binding missing: {token}")
    for header in ('Header="待归类"', 'Header="当前游戏媒体"', 'Header="来源与规则"'):
        if dashboard.count(header) != 1:
            fail(f"Media workspace sub-page is duplicated or missing: {header}")
    if 'KindDisplay, Mode=OneWay' not in dashboard or 'SourceDisplay, Mode=OneWay' not in dashboard:
        fail("Media workspace must show localized kind/source names instead of enum values")


def check_media_sql_migration() -> None:
    """Execute the legacy media-table upgrade order against an in-memory SQLite database."""
    store = read_state_store()
    schema_match = re.search(r'private const string Schema = @"(.*?)";\s*\}', store, re.S)
    if not schema_match:
        return
    connection = sqlite3.connect(":memory:")
    try:
        connection.executescript(
            "CREATE TABLE media("
            "media_id TEXT PRIMARY KEY,playnite_id TEXT,kind INTEGER NOT NULL,source INTEGER NOT NULL,"
            "archive_path TEXT NOT NULL,original_path TEXT NOT NULL,captured_utc TEXT NOT NULL,"
            "size_bytes INTEGER NOT NULL,sha256 TEXT NOT NULL UNIQUE,is_favorite INTEGER NOT NULL DEFAULT 0,"
            "comment TEXT,cloud_state TEXT NOT NULL DEFAULT 'Pending');"
        )
        connection.execute(
            "INSERT INTO media(media_id,playnite_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256) "
            "VALUES('assigned','game-a',0,0,'a','a','2026-07-28T00:00:00Z',1,'hash-a')"
        )
        connection.execute(
            "INSERT INTO media(media_id,playnite_id,kind,source,archive_path,original_path,captured_utc,size_bytes,sha256) "
            "VALUES('unassigned','',0,0,'b','b','2026-07-28T00:00:01Z',1,'hash-b')"
        )
        connection.executescript(schema_match.group(1))
        columns = {row[1] for row in connection.execute("PRAGMA table_info(media)")}
        if "classification_state" not in columns:
            connection.execute("ALTER TABLE media ADD COLUMN classification_state TEXT NOT NULL DEFAULT 'Assigned'")
        if "classification_reason" not in columns:
            connection.execute("ALTER TABLE media ADD COLUMN classification_reason TEXT")
        connection.execute(
            "UPDATE media SET classification_state=CASE WHEN COALESCE(playnite_id,'')='' THEN 'Inbox' ELSE 'Assigned' END "
            "WHERE COALESCE(classification_state,'')='' OR classification_state='Assigned'"
        )
        connection.execute("CREATE INDEX IF NOT EXISTS ix_media_classification ON media(classification_state,captured_utc DESC)")
        states = dict(connection.execute("SELECT media_id,classification_state FROM media"))
        indexes = {row[1] for row in connection.execute("PRAGMA index_list(media)")}
        if states != {"assigned": "Assigned", "unassigned": "Inbox"}:
            fail(f"Legacy media classification migration produced unexpected states: {states!r}")
        if "ix_media_classification" not in indexes:
            fail("Legacy media classification migration did not create its index")
    except Exception as exc:
        fail(f"Legacy media classification migration failed: {exc}")
    finally:
        connection.close()


def check_game_tools_guards() -> None:
    """Protect the trainer schema, IPC surface and navigation separation."""
    store = read_state_store()
    service = (ROOT / "src/GameSaveCenter.Worker/Services/GameToolService.cs").read_text(encoding="utf-8")
    source = (ROOT / "src/GameSaveCenter.Worker/Services/FlingTrainerCatalogSource.cs").read_text(encoding="utf-8")
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    code_behind = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml.cs").read_text(encoding="utf-8")
    schema_match = re.search(r'private const string Schema = @"(.*?)";\s*\}', store, re.S)
    if not schema_match:
        return
    connection = sqlite3.connect(":memory:")
    try:
        connection.executescript(
            "CREATE TABLE games(playnite_id TEXT PRIMARY KEY,name TEXT NOT NULL,platform INTEGER NOT NULL,"
            "descriptor_json TEXT NOT NULL,updated_utc TEXT NOT NULL);"
        )
        connection.executescript(schema_match.group(1))
        tables = {row[0] for row in connection.execute("SELECT name FROM sqlite_master WHERE type='table'")}
        expected = {"game_tools", "game_tool_versions", "trainer_catalog", "trainer_releases"}
        if not expected.issubset(tables):
            fail(f"Game tool migration tables missing: {sorted(expected - tables)}")
        connection.executescript(schema_match.group(1))
    except Exception as exc:
        fail(f"Game tool schema is not idempotent: {exc}")
    finally:
        connection.close()
    for token in ("ArchivePathGuard.ResolveEntryPath", "AutoStart", "CloseOnGameExit", "HasAntiCheat"):
        if token not in service:
            fail(f"Game tool safety guard missing: {token}")
    for token in ("flingtrainer.com", "EnsureFlingUri", "FLING_CATALOG_PARSE_FAILED"):
        if token not in source:
            fail(f"FLiNG source boundary missing: {token}")
    for token in ("修改器中心", "ImportTrainerCommand", "DownloadTrainerCommand", "TrainerCatalogResults"):
        if token not in dashboard:
            fail(f"Trainer center UI binding missing: {token}")
    if "SyncNavigationFromTab" in code_behind:
        fail("Primary workspace navigation must not be synchronized back from internal tabs")

def check_windows_launchers() -> None:
    """Keep the double-click bootstrap safe for legacy cmd.exe and Windows PowerShell 5.1."""
    launchers = [
        ROOT / "GameSaveCenter-Run.cmd",
        ROOT / "GameSaveCenter-一键构建安装运行.cmd",
    ]
    for path in launchers:
        if not path.exists():
            fail(f"Missing Windows launcher: {path.relative_to(ROOT)}")
            continue
        data = path.read_bytes()
        if any(byte >= 0x80 for byte in data):
            fail(f"Windows launcher must be ASCII-only: {path.relative_to(ROOT)}")
        if b"\n" in data.replace(b"\r\n", b""):
            fail(f"Windows launcher must use CRLF line endings: {path.relative_to(ROOT)}")

    scripts = sorted((ROOT / "scripts").glob("*.ps1"))
    if not scripts:
        fail("Missing PowerShell automation scripts")
    for path in scripts:
        if not path.read_bytes().startswith(b"\xef\xbb\xbf"):
            fail(f"PowerShell script must include a UTF-8 BOM for Windows PowerShell 5.1: {path.relative_to(ROOT)}")

def check_large_library_performance_guards() -> None:
    plugin = (ROOT / "src/GameSaveCenter.Playnite/GameSaveCenterPlugin.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    catalog = (ROOT / "src/GameSaveCenter.Worker/Services/GameCatalogService.cs").read_text(encoding="utf-8")
    dashboard = (ROOT / "src/GameSaveCenter.Worker/Services/DashboardService.cs").read_text(encoding="utf-8")
    dispatcher = (ROOT / "src/GameSaveCenter.Worker/Ipc/IpcRequestDispatcher.cs").read_text(encoding="utf-8")
    store = read_state_store()

    for token in ("lastSynchronizedLibraryFingerprint", "CreateLibraryFingerprint", "TimeSpan.FromMinutes(5)"):
        if token not in plugin:
            fail(f"Library synchronization de-duplication guard missing: {token}")
    for token in ("GetGameMatchCacheAsync", "GameMatchInput.CreateHash", "retryBefore"):
        if token not in catalog:
            fail(f"Incremental Ludusavi matching guard missing: {token}")
    if "_store.GetBackupVersionsAsync" in dashboard or "_store.GetMediaAsync" in dashboard or "_store.GetPolicyAsync" in dashboard:
        fail("DashboardService must use aggregate records instead of per-game N+1 queries")
    if "GetDashboardGameRecordsAsync" not in dashboard or "GROUP BY playnite_id" not in store:
        fail("Dashboard aggregate query guard is missing")
    if "RefreshCoreAsync(false)" not in view_model or "IsGameScopedWorkspace" not in view_model:
        fail("Dashboard must render cached state first and lazy-load the active workspace")
    if "(query.ForceRefresh || cached.Count == 0)" not in dispatcher:
        fail("Backup history must remain cache-first unless explicitly refreshed")


def check_061_reliability_guards() -> None:
    """Keep the actionable attention, cloud restore lock and bounded trainer download safeguards intact."""
    messages = (ROOT / "src/GameSaveCenter.Contracts/MessageTypes.cs").read_text(encoding="utf-8")
    operations = (ROOT / "src/GameSaveCenter.Contracts/OperationDtos.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    restore = (ROOT / "src/GameSaveCenter.Worker/Services/RestoreOrchestrator.cs").read_text(encoding="utf-8")
    cloud = (ROOT / "src/GameSaveCenter.Worker/Services/CloudTransferCoordinator.cs").read_text(encoding="utf-8")
    tools = (ROOT / "src/GameSaveCenter.Worker/Services/GameToolService.cs").read_text(encoding="utf-8")
    fling = (ROOT / "src/GameSaveCenter.Worker/Services/FlingTrainerCatalogSource.cs").read_text(encoding="utf-8")

    for token in ("OpenAttentionCenterCommand", "SelectedFinding", "AttentionCenterRequested"):
        if token not in view_model:
            fail(f"Actionable attention center guard missing from view model: {token}")
    for token in ("OpenAttentionCenterCommand", "FindingsGrid", "SuggestedAction", "GameName"):
        if token not in dashboard:
            fail(f"Actionable attention center UI guard missing: {token}")
    if "GetTaskChanges" not in messages or "TaskChangeFeedDto" not in operations:
        fail("Incremental task-feed contract guard missing")
    for token in ("EnsureGameClosedAsync", "PauseForRestoreAsync", "RESTORE_GAME_RUNNING"):
        if token not in restore:
            fail(f"Restore safety guard missing: {token}")
    for token in ("RunUploadAsync", "PauseForRestoreAsync"):
        if token not in cloud:
            fail(f"Cloud transfer gate guard missing: {token}")
    for token in ("MaxArchiveEntryCount", "MaxArchiveExpandedBytes", "installedSuccessfully"):
        if token not in tools:
            fail(f"Trainer archive safety guard missing: {token}")
    if "MaxDownloadBytes" not in fling:
        fail("FLiNG download size guard missing")

def check_device_state_guards() -> None:
    """Device comparison must remain content-free and read-only."""
    service = (ROOT / "src/GameSaveCenter.Worker/Services/DeviceStateService.cs").read_text(encoding="utf-8")
    rclone = (ROOT / "src/GameSaveCenter.Worker/Infrastructure/RcloneClient.cs").read_text(encoding="utf-8")
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    for token in ("DeviceStateSidecarDto", "DeviceConflictDetector", "GetLatestBackupSummariesAsync", "ReadRemoteTextAsync"):
        if token not in service:
            fail(f"Device-state service guard missing: {token}")
    for forbidden in ('"sync"', '"delete"', '"purge"', '"move"'):
        if forbidden in service.lower():
            fail(f"Device-state service must not invoke destructive cloud operation: {forbidden}")
    for token in ('"lsf"', '"cat"'):
        if token not in rclone:
            fail(f"Read-only rclone sidecar guard missing: {token}")
    if "SyncDeviceStatesCommand" not in ui or "设备状态" not in ui:
        fail("Device-state maintenance UI guard missing")

def check_065_completion_guards() -> None:
    """Protect the signalled task feed, cloud-only retry and explicit trainer entry selection."""
    messages = (ROOT / "src/GameSaveCenter.Contracts/MessageTypes.cs").read_text(encoding="utf-8")
    coordinator = (ROOT / "src/GameSaveCenter.Worker/Services/TaskCoordinator.cs").read_text(encoding="utf-8")
    backup = (ROOT / "src/GameSaveCenter.Worker/Services/BackupOrchestrator.cs").read_text(encoding="utf-8")
    plugin = (ROOT / "src/GameSaveCenter.Playnite/GameSaveCenterPlugin.cs").read_text(encoding="utf-8")
    tools = (ROOT / "src/GameSaveCenter.Worker/Services/GameToolService.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    for token in ("WaitForTaskChanges", "RetryCloudUpload", "InspectGameToolImport"):
        if token not in messages:
            fail(f"0.6.5 IPC completion guard missing: {token}")
    for token in ("WaitForChangesAsync", "RunContinuationsAsynchronously", "CancelAfter"):
        if token not in coordinator:
            fail(f"Signalled task-feed guard missing: {token}")
    for token in ("RetryCloudUploadAsync", '"CloudUpload"', '"Pending"', '"Uploaded"', '"Failed"'):
        if token not in backup:
            fail(f"Cloud-only retry guard missing: {token}")
    if "MessageTypes.WaitForTaskChanges" not in plugin or "WaitSeconds = 20" not in plugin:
        fail("Playnite task notification monitor must use the bounded signalled feed")
    for token in ("InspectImportAsync", "ValidateArchiveShape", "GameToolEntryCandidateDto"):
        if token not in tools:
            fail(f"Explicit trainer entry inspection guard missing: {token}")
    for token in ("HasPendingGameToolEntrySelection", "ConfirmGameToolImportCommand", "SelectedGameToolVersion"):
        if token not in view_model or token not in ui:
            fail(f"Trainer selection/version UI guard missing: {token}")

def check_066_portability_media_guards() -> None:
    """Protect portable settings validation and non-destructive media metadata/storage features."""
    settings = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettings.cs").read_text(encoding="utf-8")
    settings_ui = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml").read_text(encoding="utf-8")
    messages = (ROOT / "src/GameSaveCenter.Contracts/MessageTypes.cs").read_text(encoding="utf-8")
    store = read_state_store()
    dispatcher = (ROOT / "src/GameSaveCenter.Worker/Ipc/IpcRequestDispatcher.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    for token in ("ExportPortableJson", "ImportPortableJson", "SchemaVersion = 1", "ValidateValueRanges", "MissingPaths"):
        if token not in settings:
            fail(f"Portable settings guard missing: {token}")
    settings_test = ROOT / "tests/GameSaveCenter.Playnite.Tests/PortableSettingsTests.cs"
    if not settings_test.exists():
        fail("Portable settings migration tests are missing")
    else:
        test_text = settings_test.read_text(encoding="utf-8")
        for token in ("ExportImport_RoundTripsNonSecretSettings", "Import_LegacyPackageUsesDefaultsForNewFields",
                      "Import_InvalidValuesDoesNotMutateCurrentSettings", "Import_ReportsMissingProgramsAndDirectoriesWithoutCreatingThem"):
            if token not in test_text:
                fail(f"Portable settings test guard missing: {token}")
    for token in ("OnExportSettingsClick", "OnImportSettingsClick"):
        if token not in settings_ui:
            fail(f"Settings migration UI guard missing: {token}")
    for token in ("GetMediaSummary", "UpdateMediaMetadata"):
        if token not in messages:
            fail(f"Media metadata IPC guard missing: {token}")
    for token in ("GetMediaSummaryAsync", "SUM(size_bytes)", "UpdateMediaMetadataAsync"):
        if token not in store:
            fail(f"Media aggregate/metadata store guard missing: {token}")
    for token in ("MessageTypes.GetMediaSummary", "MessageTypes.UpdateMediaMetadata", "1000"):
        if token not in dispatcher:
            fail(f"Media metadata dispatcher guard missing: {token}")
    for token in ("MediaSummary", "UpdateMediaMetadataCommand", "OpenSelectedMediaCommand", "RevealSelectedMediaCommand"):
        if token not in view_model or token not in ui:
            fail(f"Media management UI guard missing: {token}")
    for forbidden in ("File.Delete(", "Directory.Delete("):
        if forbidden in view_model:
            fail(f"Media UI must remain non-destructive: {forbidden}")

def check_067_media_browsing_guards() -> None:
    """Keep media filtering local and selected-image preview bounded."""
    view_model = read_dashboard_view_model()
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    converter = (ROOT / "src/GameSaveCenter.Playnite/Converters/MediaThumbnailConverter.cs").read_text(encoding="utf-8")
    for token in ("MediaView", "FilterMedia", "MediaFilterOptions", "MediaSearchText"):
        if token not in view_model:
            fail(f"Media browsing guard missing: {token}")
    for token in ("MediaView", "MediaFilterOptions", "MediaSearchText"):
        if token not in ui:
            fail(f"Media browsing UI guard missing: {token}")
    for token in ("DefaultPreviewWidth = 480", "CacheLimit = 96", "DecodePixelWidth=width",
                  "BitmapCacheOption.OnLoad", "image.Freeze()", "FileShare.ReadWrite|FileShare.Delete"):
        if token not in converter:
            fail(f"Bounded selected-media preview guard missing: {token}")
    for token in ('ItemsSource="{Binding MediaView}"', "MediaThumbnailConverter", "MediaVideoSourceConverter",
                  'ConverterParameter=96', 'VirtualizingPanel.VirtualizationMode="Recycling"', "<MediaElement"):
        if token not in ui:
            fail(f"Media virtualized preview guard missing: {token}")
    if "LoadedBehavior=\"Play\"" not in ui or "IsMuted=\"True\"" not in ui:
        fail("Embedded video preview must remain selected-only and muted")
    if "MediaVideoSourceConverter" not in converter:
        fail("Media filtered view/preview binding guard missing")
    thumbnail_test = ROOT / "tests/GameSaveCenter.Playnite.Tests/MediaThumbnailConverterTests.cs"
    if not thumbnail_test.exists() or "Convert_UsesBoundedFrozenCacheAndReleasesFiles" not in thumbnail_test.read_text(encoding="utf-8"):
        fail("Bounded thumbnail cache test is missing")

def check_068_media_batch_guards() -> None:
    """Keep batch media edits transactional, bounded and non-destructive."""
    messages = (ROOT / "src/GameSaveCenter.Contracts/MessageTypes.cs").read_text(encoding="utf-8")
    operations = (ROOT / "src/GameSaveCenter.Contracts/OperationDtos.cs").read_text(encoding="utf-8")
    store = read_state_store()
    dispatcher = (ROOT / "src/GameSaveCenter.Worker/Ipc/IpcRequestDispatcher.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    worker_test = ROOT / "tests/GameSaveCenter.Worker.Tests/SqliteMediaMetadataTests.cs"
    for token in ("UpdateMediaMetadataBatch", "MediaMetadataBatchUpdateDto"):
        if token not in messages + operations + dispatcher:
            fail(f"Batch media IPC guard missing: {token}")
    for token in ("BeginTransactionAsync", "updated!=update.MediaIds.Count", "CommitAsync"):
        if token not in store:
            fail(f"Batch media transaction guard missing: {token}")
    for token in ("FavoriteSelectedMediaCommand", "UnfavoriteSelectedMediaCommand", "CommentSelectedMediaCommand"):
        if token not in view_model or token not in ui:
            fail(f"Batch media UI guard missing: {token}")
    if not worker_test.exists() or "BatchMetadataUpdate_IsAtomicAndPreservesUnchangedFields" not in worker_test.read_text(encoding="utf-8"):
        fail("Batch media SQLite integration test is missing")

def check_069_device_decision_guards() -> None:
    """Keep device conflict decisions auditable and non-executing."""
    contracts = (ROOT / "src/GameSaveCenter.Contracts/DeviceStateDtos.cs").read_text(encoding="utf-8")
    store = read_state_store()
    dispatcher = (ROOT / "src/GameSaveCenter.Worker/Ipc/IpcRequestDispatcher.cs").read_text(encoding="utf-8")
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    for token in ("DeviceConflictDecisionDto", "device_conflict_decisions", "SaveDeviceConflictDecisionAsync", "SaveDeviceDecisionCommand"):
        if token not in contracts + store + dispatcher + ui:
            fail(f"Device decision guard missing: {token}")
    if "已记录人工决策；未下载、恢复、删除或覆盖任何存档" not in read_dashboard_view_model():
        fail("Device decision UI must state its non-executing boundary")

def check_0613_remote_restore_guards() -> None:
    """Keep remote restore isolated, verified, explicit and rollback-protected."""
    staging = (ROOT / "src/GameSaveCenter.Worker/Services/RemoteBackupStagingService.cs").read_text(encoding="utf-8")
    rclone = (ROOT / "src/GameSaveCenter.Worker/Infrastructure/RcloneClient.cs").read_text(encoding="utf-8")
    restore = (ROOT / "src/GameSaveCenter.Worker/Services/RestoreOrchestrator.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    test = ROOT / "tests/GameSaveCenter.Worker.Tests/RemoteBackupStagingSafetyTests.cs"
    for token in ("ResolveStagingRoot", "IsSafeDeviceName", "ChecksumCheckAsync", "ListBackupsFromPathAsync", "RevalidateAsync",
                  "ExpiresUtc", "TryDeleteStaging"):
        if token not in staging + rclone:
            fail(f"Remote staging safety guard missing: {token}")
    for token in ("ExecuteRemoteAsync", "PreRestore", "RestoreFromPathAsync", "PauseForRestoreAsync"):
        if token not in restore:
            fail(f"Remote restore state-machine guard missing: {token}")
    for token in ("StageRemoteBackupCommand", "RestoreStagedRemoteBackupCommand", "下载并校验", "创建快照并恢复"):
        if token not in view_model + ui:
            fail(f"Two-step remote restore UI guard missing: {token}")
    if not test.exists() or "DeviceName_RejectsTraversalAndSeparators" not in test.read_text(encoding="utf-8"):
        fail("Remote staging traversal tests are missing")

def check_0618_task_event_guards() -> None:
    """The optional push channel must stay isolated from durable task synchronization."""
    protocol = (ROOT / "src/GameSaveCenter.Contracts/ProtocolConstants.cs").read_text(encoding="utf-8")
    coordinator = (ROOT / "src/GameSaveCenter.Worker/Services/TaskCoordinator.cs").read_text(encoding="utf-8")
    broadcaster = (ROOT / "src/GameSaveCenter.Worker/Ipc/TaskEventBroadcaster.cs").read_text(encoding="utf-8")
    event_server = (ROOT / "src/GameSaveCenter.Worker/Ipc/TaskEventPipeServerService.cs").read_text(encoding="utf-8")
    program = (ROOT / "src/GameSaveCenter.Worker/Program.cs").read_text(encoding="utf-8")
    ipc_client = (ROOT / "src/GameSaveCenter.Playnite/Ipc/WorkerIpcClient.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    view = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml.cs").read_text(encoding="utf-8")
    test = ROOT / "tests/GameSaveCenter.Worker.Tests/TaskEventBroadcasterTests.cs"
    for token in ("EventPipeName", "GameSaveCenter.Worker.Events.v1"):
        if token not in protocol:
            fail(f"Task event pipe protocol guard missing: {token}")
    for token in ("TaskEventBroadcaster", "_events.Publish(change)"):
        if token not in coordinator:
            fail(f"Task event publish guard missing: {token}")
    for token in ("BoundedChannelFullMode.DropOldest", "PerSubscriberCapacity", "TaskEventSubscription"):
        if token not in broadcaster:
            fail(f"Task event bounded fan-out guard missing: {token}")
    for token in ("ProtocolConstants.EventPipeName", "PipeOptions.CurrentUserOnly", "MessageTypes.TaskEvent"):
        if token not in event_server:
            fail(f"Task event server isolation guard missing: {token}")
    if "AddHostedService<TaskEventPipeServerService>" not in program:
        fail("Task event pipe server must be hosted by the Worker")
    for token in ("ListenForTaskEventsAsync", "retryDelay", "MessageTypes.TaskEvent"):
        if token not in ipc_client:
            fail(f"Task event client reconnect guard missing: {token}")
    for token in ("StartTaskEventSubscription", "StopTaskEventSubscription", "ApplyTaskEventAsync"):
        if token not in view_model:
            fail(f"Dashboard task event view-model guard missing: {token}")
    for token in ("StartTaskEventSubscription", "StopTaskEventSubscription"):
        if token not in view:
            fail(f"Dashboard task event view lifecycle guard missing: {token}")
    if not test.exists() or "Publish_FansOutIndependentTaskSnapshots" not in test.read_text(encoding="utf-8"):
        fail("Task event broadcaster regression tests are missing")

def check_0620_wpf_thread_guards() -> None:
    """PropertyChanged listeners must never read WPF controls from Worker callbacks."""
    view = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml.cs").read_text(encoding="utf-8")
    view_model = read_dashboard_view_model()
    handler = re.search(
        r"private void OnViewModelPropertyChanged\(.*?\n        \}", view, re.DOTALL)
    if handler is None:
        fail("Dashboard PropertyChanged handler is missing")
    else:
        body = handler.group(0)
        dispatcher_index = body.find("Dispatcher.CheckAccess()")
        loaded_index = body.find("IsLoaded")
        if dispatcher_index < 0 or loaded_index < 0 or dispatcher_index > loaded_index:
            fail("Dashboard must check Dispatcher access before reading IsLoaded")
        if "OnViewModelPropertyChanged(sender, e)" not in body or "Dispatcher.BeginInvoke" not in body:
            fail("Dashboard background PropertyChanged must be reposted to its Dispatcher")
    for forbidden in ("async void RequestBackgroundRefresh", "async void RefreshAfterSynchronization"):
        if forbidden in view_model:
            fail(f"Dashboard background operation must return Task: {forbidden}")
    for token in ("RequestBackgroundRefreshAsync", "RefreshAfterSynchronizationAsync", "ApplyOnUi(() => IsBackgroundRefreshing = false)"):
        if token not in view_model:
            fail(f"Dashboard UI-thread refresh guard missing: {token}")
    if "await viewModel.RequestBackgroundRefreshAsync()" not in view:
        fail("Dashboard refresh timer must await the controlled background refresh Task")


def check_0621_cloud_retry_and_numeric_ui_guards() -> None:
    """Keep durable cloud recovery and complete-value numeric editing from regressing."""
    store = read_state_store()
    policy = (ROOT / "src/GameSaveCenter.Worker/Services/CloudRetryPolicy.cs").read_text(encoding="utf-8")
    retry_service = (ROOT / "src/GameSaveCenter.Worker/Services/CloudRetryService.cs").read_text(encoding="utf-8")
    backup = (ROOT / "src/GameSaveCenter.Worker/Services/BackupOrchestrator.cs").read_text(encoding="utf-8")
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    settings = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml").read_text(encoding="utf-8")
    tokens = (ROOT / "src/GameSaveCenter.Playnite/Themes/DesignTokens.xaml").read_text(encoding="utf-8")
    agents = ROOT / "AGENTS.md"

    for token in ("cloud_retry_queue", "attempt_count", "next_attempt_utc", "ix_cloud_retry_due"):
        if token not in store:
            fail(f"Cloud retry durable storage guard missing: {token}")
    for token in ("MaximumAutomaticRetries", "TimeSpan.FromMinutes(1)", "TimeSpan.FromMinutes(5)",
                  "TimeSpan.FromMinutes(15)", "TimeSpan.FromHours(1)", "TimeSpan.FromHours(4)", "TimeSpan.FromHours(12)"):
        if token not in policy:
            fail(f"Cloud retry backoff guard missing: {token}")
    for token in ("EnableCloudUpload && _rclone.IsConfigured", "Directory.Exists(_options.LudusaviBackupDirectory)",
                  "DeferCloudRetryAsync", "CLOUD_GAME_NOT_FOUND"):
        if token not in retry_service:
            fail(f"Cloud retry storm-prevention guard missing: {token}")
    for token in ("ScheduleCloudRetryAsync", "RemoveCloudRetryAsync", "RetryScheduled", "MaximumAutomaticRetries"):
        if token not in backup:
            fail(f"Cloud retry orchestration guard missing: {token}")
    if "Width=\"58\"" in dashboard or "DuringPlayIntervalMinutes, UpdateSourceTrigger=PropertyChanged" in dashboard:
        fail("Backup policy interval must not use the narrow per-keystroke numeric editor")
    for text, file_name, field in ((dashboard, "DashboardView.xaml", "DuringPlayIntervalMinutes"),
                                   (settings, "GameSaveCenterSettingsView.xaml", "DefaultBackupIntervalMinutes"),
                                   (settings, "GameSaveCenterSettingsView.xaml", "ProcessPollingSeconds"),
                                   (settings, "GameSaveCenterSettingsView.xaml", "DashboardRefreshSeconds"),
                                   (settings, "GameSaveCenterSettingsView.xaml", "FullBackupLimit"),
                                   (settings, "GameSaveCenterSettingsView.xaml", "DifferentialBackupLimit"),
                                   (settings, "GameSaveCenterSettingsView.xaml", "CompressionLevel")):
        marker = rf'Path="[^"]*{re.escape(field)}"[^>]*UpdateSourceTrigger="LostFocus"'
        if not re.search(marker, text):
            fail(f"Numeric input must commit complete values on LostFocus: {file_name} {field}")
    for token in ("GscNumericTextBox", "Validation.ErrorTemplate", "Validation.HasError"):
        if token not in tokens:
            fail(f"Shared numeric UI guard missing: {token}")
    for text, file_name in ((dashboard, "DashboardView.xaml"), (settings, "GameSaveCenterSettingsView.xaml")):
        if re.search(r"#[0-9A-Fa-f]{3,8}", text):
            fail(f"Theme colors must be declared in DesignTokens.xaml, not {file_name}")
    if not agents.exists() or "wpf-apple-desktop-ui" not in agents.read_text(encoding="utf-8"):
        fail("Repository AGENTS.md must require the WPF Apple desktop UI skill")


def check_wpf_ui_probe_guards() -> None:
    """Keep the experimental WPF-UI integration local to GameSaveCenter's visual tree."""
    packages = (ROOT / "Directory.Packages.props").read_text(encoding="utf-8")
    project = (ROOT / "src/GameSaveCenter.Playnite/GameSaveCenter.Playnite.csproj").read_text(encoding="utf-8")
    base = (ROOT / "src/GameSaveCenter.Playnite/Themes/WpfUiBase.xaml").read_text(encoding="utf-8")
    probe = (ROOT / "src/GameSaveCenter.Playnite/Views/Development/UiFrameworkProbeView.xaml").read_text(encoding="utf-8")
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    package = (ROOT / "scripts/package.ps1").read_text(encoding="utf-8")

    for description, token, text in (("WPF-UI central version", "WPF-UI", packages),
                                     ("WPF-UI plugin reference", "WPF-UI", project),
                                     ("WPF-UI resource dictionary", "ui:ThemesDictionary", base),
                                     ("WPF-UI probe", "UiFrameworkProbeView", probe)):
        if token not in text:
            fail(f"{description} guard missing")
    for token in ("Version=\"4.3.0\"", "<PackageReference Include=\"WPF-UI\" />"):
        if token not in packages and token not in project:
            fail(f"WPF-UI 4.3.0 package guard missing: {token}")
    for token in ("ResourceDictionary.MergedDictionaries", "ui:ThemesDictionary", "ui:ControlsDictionary"):
        if token not in base:
            fail(f"WPF-UI local resource guard missing: {token}")
    for token in ("UserControl.Resources", "WpfUiBase.xaml", "SnackbarPresenter", "UiFrameworkProbeView"):
        if token not in probe and token not in dashboard:
            fail(f"WPF-UI probe surface guard missing: {token}")
    for source, label in ((probe, "WPF-UI probe"), (dashboard, "Dashboard")):
        if "<ui:ContentDialogHost" in source:
            fail(f"{label} must not register WPF-UI ContentDialogHost inside Playnite's shared Window")
    if "<development:UiFrameworkProbeView" in dashboard:
        fail("WPF-UI probe must not be constructed while Dashboard XAML is parsed")
    for token in ("UiFrameworkProbeHost", "UiFrameworkProbeRecoveryPanel", "OnLoadUiFrameworkProbeClick"):
        if token not in dashboard:
            fail(f"WPF-UI lazy probe boundary missing: {token}")
    loader = (ROOT / "src/GameSaveCenter.Playnite/Infrastructure/UiFrameworkProbeLoader.cs").read_text(encoding="utf-8")
    for token in ("TryCreate", "维护中心仍可继续使用", "Trace.TraceError"):
        if token not in loader:
            fail(f"WPF-UI lazy probe recovery guard missing: {token}")
    executable_source = (ROOT / "src/GameSaveCenter.Playnite/GameSaveCenterPlugin.cs").read_text(encoding="utf-8")
    if "Application.Current.Resources" in executable_source:
        fail("WPF-UI resources must not be injected into Playnite Application.Current.Resources")
    for token in ("Wpf.Ui.dll", "Wpf.Ui.Abstractions.dll", "System.Memory.dll", "System.ValueTuple.dll"):
        if token not in package:
            fail(f"WPF-UI package dependency guard missing: {token}")

def check_shared_wpf_control_guards() -> None:
    """Keep native high-density primitives and WPF-UI production adapters centralized."""
    tokens = (ROOT / "src/GameSaveCenter.Playnite/Themes/DesignTokens.xaml").read_text(encoding="utf-8")
    production = (ROOT / "src/GameSaveCenter.Playnite/Themes/WpfUiProduction.xaml").read_text(encoding="utf-8")
    settings = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml").read_text(encoding="utf-8")
    for token in ("GscSurface", "GscButtonBase", "GscPrimaryButton", "GscTextBox", "GscNumericTextBox",
                  "GscComboBox", "GscCheckBox", "GscSlider", "GscScrollThumb", "TargetType=\"ToolTip\"",
                  "TargetType=\"ProgressBar\"", "IsIndeterminate"):
        if token not in tokens:
            fail(f"Shared native WPF control guard missing: {token}")
    for token in ("GscWpfUiCard", "GscWpfUiButton", "GscWpfUiSecondaryButton",
                  "GscWpfUiPrimaryButton", "GscWpfUiCompactButton", "GscWpfUiActionButton",
                  "GscWpfUiPrimaryActionButton", "GscWpfUiToolbarButton",
                  "GscWpfUiToolbarPrimaryButton", "GscWpfUiContextButton", "GscWpfUiToggleSwitch",
                  "GscWpfUiTextBox", "GscWpfUiComboBox",
                  "BasedOn=\"{StaticResource {x:Type ui:Card}}\"",
                  "<Setter Property=\"Border.CornerRadius\" Value=\"18\"/>",
                  "<Trigger Property=\"IsEnabled\" Value=\"False\">",
                  "<Setter Property=\"Visibility\" Value=\"Collapsed\"/>"):
        if token not in production:
            fail(f"Shared WPF-UI production adapter guard missing: {token}")
    if 'ResourceDictionary Source="/GameSaveCenter.Playnite;component/Themes/WpfUiBase.xaml"' not in production:
        fail("WPF-UI production adapters must merge WpfUiBase in their own parse scope")
    for token in ('{StaticResource GscSoftShadowColor}', '{StaticResource GscSharedFocusVisual}'):
        if token in production:
            fail("WPF-UI production adapters must resolve GameSaveCenter theme tokens dynamically from their UserControl scope")
    for token in ('{DynamicResource GscSoftShadowColor}', '{DynamicResource GscSharedFocusVisual}'):
        if token not in production:
            fail(f"WPF-UI production adapter dynamic theme-token guard missing: {token}")
    if '<Setter Property="CornerRadius"' in production:
        fail("WPF-UI Card must use Border.CornerRadius; Card has no CornerRadius dependency property")
    if production.count('<Setter Property="Margin" Value="0,0,8,8"/>') < 3:
        fail("WPF-UI action/context adapters must preserve the established WrapPanel spacing")
    for token in ("Themes/WpfUiProduction.xaml",
                  "TargetType=\"{x:Type ui:Card}\" BasedOn=\"{StaticResource GscWpfUiCard}\""):
        if token not in settings:
            fail(f"Settings production card guard missing: {token}")

def check_responsive_ui_layout_guards() -> None:
    """Keep compact Playnite hosts scrollable without hiding navigation or settings fields."""
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    dashboard_code = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml.cs").read_text(encoding="utf-8")
    settings = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml").read_text(encoding="utf-8")
    settings_code = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml.cs").read_text(encoding="utf-8")
    for token in ("VerticalScrollBarVisibility=\"Auto\"", "KeyboardNavigation.TabNavigation=\"Local\"",
                  "<Ellipse.RenderTransform><TranslateTransform X=\"-95\" Y=\"-130\"/>",
                  "<Ellipse.RenderTransform><TranslateTransform X=\"105\" Y=\"0\"/>",
                  "AutomationProperties.Name=\"刷新全部状态\"", "x:Name=\"TopRefreshLabel\""):
        if token not in dashboard:
            fail(f"Dashboard responsive layout guard missing: {token}")
    for token in ("x:Name=\"SettingsScroller\"", "HorizontalScrollBarVisibility=\"Auto\"",
                  "x:Name=\"SettingsHeaderSubtitle\"", "AutomationProperties.Name=\"毛玻璃强度\"",
                  "<Ellipse.RenderTransform><TranslateTransform X=\"-95\" Y=\"-120\"/>"):
        if token not in settings:
            fail(f"Settings responsive layout guard missing: {token}")
    for token in ("SizeChanged += OnSizeChanged", "ApplyResponsiveLayout(ActualWidth, ActualHeight)",
                  "SettingsHeaderSubtitle.Visibility", "width < 720"):
        if token not in settings_code:
            fail(f"Settings responsive behavior guard missing: {token}")
    for token in ("SetToolbarLabelsVisible(mode == LayoutMode.Expanded)", "TopRefreshLabel.Visibility"):
        if token not in dashboard_code:
            fail(f"Dashboard responsive behavior guard missing: {token}")

def check_wpf_ui_production_scope_guards() -> None:
    """Keep production WPF-UI resources view-local, recoverable and virtualization-safe."""
    dashboard = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    dashboard_code = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml.cs").read_text(encoding="utf-8")
    settings = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml").read_text(encoding="utf-8")
    settings_code = (ROOT / "src/GameSaveCenter.Playnite/Settings/GameSaveCenterSettingsView.xaml.cs").read_text(encoding="utf-8")
    theme_scope = (ROOT / "src/GameSaveCenter.Playnite/Infrastructure/WpfUiThemeScope.cs").read_text(encoding="utf-8")
    production = (ROOT / "src/GameSaveCenter.Playnite/Themes/WpfUiProduction.xaml").read_text(encoding="utf-8")
    for source, label, required in (
        (dashboard, "Dashboard WPF-UI production scope",
         ("xmlns:ui=\"http://schemas.lepo.co/wpfui/2022/xaml\"", "Themes/WpfUiProduction.xaml",
          "<ui:Card", "<ui:Button", "<ui:ToggleSwitch",
          "x:Name=\"SnackbarHost\"")),
        (settings, "Settings WPF-UI production scope",
         ("xmlns:ui=\"http://schemas.lepo.co/wpfui/2022/xaml\"", "Themes/WpfUiProduction.xaml",
          "<ui:Card", "<ui:ToggleSwitch", "<ui:Button",
          "x:Name=\"SettingsSnackbarHost\"")),
        (dashboard_code, "Dashboard production feedback",
         ("new Snackbar(SnackbarHost)", "ShowFallbackConfirmation", "ShowToast",
          "if (confirmationOpen)", "confirmationOpen = false",
          "return Task.CompletedTask")),
        (settings_code, "Settings production feedback",
         ("new Snackbar(SettingsSnackbarHost)",
          "Task.Run", "MessageBox.Show")),
        (theme_scope, "WPF-UI theme scope", ("ThemesDictionary", "ApplicationTheme.HighContrast", "ApplyMergedDictionaries")),
    ):
        for token in required:
            if token not in source:
                fail(f"{label} guard missing: {token}")
    for source, label in ((dashboard_code, "Dashboard"), (settings_code, "Settings")):
        if "WpfUiThemeScope.Apply(Resources, palette.IsDark)" not in source:
            fail(f"{label} must apply WPF-UI theme only through its local resource scope")
        if "using Wpf.Ui.Controls;" in source:
            fail(f"{label} must alias individual WPF-UI feedback controls to avoid native WPF type ambiguity")
    for token in ("using Snackbar = Wpf.Ui.Controls.Snackbar;",):
        if token not in dashboard_code:
            fail(f"Dashboard WPF-UI alias guard missing: {token}")
    for token in ("using Snackbar = Wpf.Ui.Controls.Snackbar;",):
        if token not in settings_code:
            fail(f"Settings WPF-UI alias guard missing: {token}")
    for source, label in ((dashboard, "Dashboard"), (settings, "Settings")):
        if "<ui:ContentDialogHost" in source:
            fail(f"{label} must use GameSaveCenter's embedded dialog fallback instead of a WPF-UI Window host")
    for source, label in ((dashboard_code, "Dashboard feedback"), (settings_code, "Settings feedback")):
        if "new ContentDialog(" in source:
            fail(f"{label} must use GameSaveCenter's embedded dialog fallback instead of a WPF-UI Window host")
    if "Application.Current.Resources" in theme_scope or "Application.Current.Resources" in production:
        fail("WPF-UI production theme scope must never mutate Playnite application resources")
    for token in ("EnableRowVirtualization=\"True\"", "EnableColumnVirtualization=\"True\"",
                  "VirtualizingPanel.IsVirtualizing=\"True\"", "VirtualizingPanel.VirtualizationMode=\"Recycling\""):
        if token not in dashboard:
            fail(f"WPF-UI migration must preserve large-library virtualization: {token}")
    if "async void" in settings_code:
        fail("Settings WPF-UI event boundary must not introduce async void handlers")

def main() -> int:
    check_structured_files()
    check_csharp_delimiters()
    check_xaml_semantics()
    check_gsc_resource_references()
    check_solution()
    check_ipc_constants()
    check_version_consistency()
    check_delivery_guards()
    check_dashboard_regressions()
    check_media_inbox_guards()
    check_media_sql_migration()
    check_game_tools_guards()
    check_windows_launchers()
    check_large_library_performance_guards()
    check_061_reliability_guards()
    check_device_state_guards()
    check_065_completion_guards()
    check_066_portability_media_guards()
    check_067_media_browsing_guards()
    check_068_media_batch_guards()
    check_069_device_decision_guards()
    check_0613_remote_restore_guards()
    check_0618_task_event_guards()
    check_0620_wpf_thread_guards()
    check_0621_cloud_retry_and_numeric_ui_guards()
    check_shared_wpf_control_guards()
    check_responsive_ui_layout_guards()
    check_wpf_ui_production_scope_guards()
    check_wpf_ui_probe_guards()
    if ERRORS:
        print("Source validation failed:")
        for item in ERRORS:
            print(f" - {item}")
        return 1
    print("Source validation passed: JSON/XML/YAML, XAML semantics/resources, C# delimiters, solution, IPC constants, version consistency, delivery guards, media/game-tool SQLite guards, large-library performance guards and Windows launchers.")
    print("Note: this does not replace dotnet build/test on Windows with Playnite installed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
