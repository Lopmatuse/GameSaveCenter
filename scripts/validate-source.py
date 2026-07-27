#!/usr/bin/env python3
"""Cross-platform structural checks that do not replace a real Windows build."""
from __future__ import annotations

import json
import re
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
    if 'ItemsSource="{Binding GamesView}"' not in dashboard or 'GameSearchText' not in dashboard:
        fail("Dashboard large-library search/filter view is missing")
    if 'ProgressBar Width="120" Height="4" IsIndeterminate="{Binding IsBusy}"' in dashboard:
        fail("Dashboard still contains the always-visible idle progress frame")
    if 'TextOptions.TextRenderingMode="ClearType"' not in dashboard:
        fail("Dashboard ClearType rendering guard is missing")

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

def main() -> int:
    check_structured_files()
    check_csharp_delimiters()
    check_xaml_semantics()
    check_solution()
    check_ipc_constants()
    check_delivery_guards()
    check_dashboard_regressions()
    check_windows_launchers()
    if ERRORS:
        print("Source validation failed:")
        for item in ERRORS:
            print(f" - {item}")
        return 1
    print("Source validation passed: JSON/XML/YAML, XAML semantics, C# delimiters, solution, IPC constants, delivery guards and Windows launchers.")
    print("Note: this does not replace dotnet build/test on Windows with Playnite installed.")
    return 0


if __name__ == "__main__":
    sys.exit(main())
