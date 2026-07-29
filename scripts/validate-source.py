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
        "GameSaveCenter.Playnite", "GameSaveCenter.Core.Tests"
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
    for path in ROOT.joinpath("src/GameSaveCenter.Playnite").rglob("*.xaml"):
        if any(part in {"bin", "obj"} for part in path.parts):
            continue
        text = path.read_text(encoding="utf-8")
        for binding in re.findall(r'<Run\\b[^>]*\\bText="\\{Binding ([^}]*)\\}"', text):
            if "Mode=OneWay" not in binding:
                fail(f"Run.Text binding must explicitly use Mode=OneWay: {path.relative_to(ROOT)}: {binding}")
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
    if "TimedBackupEnabled" not in coordinator:
        fail("During-play backup scheduler must re-anchor when the policy is enabled during a session")
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
    store = (ROOT / "src/GameSaveCenter.Worker/Persistence/SqliteStateStore.cs").read_text(encoding="utf-8")
    service = (ROOT / "src/GameSaveCenter.Worker/Services/MediaSyncService.cs").read_text(encoding="utf-8")
    view_model = (ROOT / "src/GameSaveCenter.Playnite/ViewModels/DashboardViewModel.cs").read_text(encoding="utf-8")
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
    store = (ROOT / "src/GameSaveCenter.Worker/Persistence/SqliteStateStore.cs").read_text(encoding="utf-8")
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
    store = (ROOT / "src/GameSaveCenter.Worker/Persistence/SqliteStateStore.cs").read_text(encoding="utf-8")
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

    runner = ROOT / "scripts/dev-install-run.ps1"
    if not runner.exists():
        fail("Missing scripts/dev-install-run.ps1")
    elif not runner.read_bytes().startswith(b"\xef\xbb\xbf"):
        fail("scripts/dev-install-run.ps1 must include a UTF-8 BOM for Windows PowerShell 5.1")

def check_large_library_performance_guards() -> None:
    plugin = (ROOT / "src/GameSaveCenter.Playnite/GameSaveCenterPlugin.cs").read_text(encoding="utf-8")
    view_model = (ROOT / "src/GameSaveCenter.Playnite/ViewModels/DashboardViewModel.cs").read_text(encoding="utf-8")
    catalog = (ROOT / "src/GameSaveCenter.Worker/Services/GameCatalogService.cs").read_text(encoding="utf-8")
    dashboard = (ROOT / "src/GameSaveCenter.Worker/Services/DashboardService.cs").read_text(encoding="utf-8")
    dispatcher = (ROOT / "src/GameSaveCenter.Worker/Ipc/IpcRequestDispatcher.cs").read_text(encoding="utf-8")
    store = (ROOT / "src/GameSaveCenter.Worker/Persistence/SqliteStateStore.cs").read_text(encoding="utf-8")

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
    view_model = (ROOT / "src/GameSaveCenter.Playnite/ViewModels/DashboardViewModel.cs").read_text(encoding="utf-8")
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
    view_model = (ROOT / "src/GameSaveCenter.Playnite/ViewModels/DashboardViewModel.cs").read_text(encoding="utf-8")
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
    store = (ROOT / "src/GameSaveCenter.Worker/Persistence/SqliteStateStore.cs").read_text(encoding="utf-8")
    dispatcher = (ROOT / "src/GameSaveCenter.Worker/Ipc/IpcRequestDispatcher.cs").read_text(encoding="utf-8")
    view_model = (ROOT / "src/GameSaveCenter.Playnite/ViewModels/DashboardViewModel.cs").read_text(encoding="utf-8")
    ui = (ROOT / "src/GameSaveCenter.Playnite/Views/DashboardView.xaml").read_text(encoding="utf-8")
    for token in ("ExportPortableJson", "ImportPortableJson", "SchemaVersion = 1", "ValidateValueRanges", "MissingPaths"):
        if token not in settings:
            fail(f"Portable settings guard missing: {token}")
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
