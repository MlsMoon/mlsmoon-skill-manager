"""Build and drive the real WPF window in the background. Invoke only; never activate."""

from __future__ import annotations

import json
import os
import shutil
import subprocess
import sys
import time
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
WINDOW_ID = "MlsmoonUiTest"
OPEN_FOLDER_ID = "OpenInstallFolder-open-psd-kit"
SW_SHOWNOACTIVATE = 4
CREATE_NO_WINDOW = 0x08000000


def fail(message: str) -> None:
    print("ui_flow failed:", message, file=sys.stderr)
    raise SystemExit(1)


def ensure_uiautomation() -> None:
    try:
        import uiautomation  # noqa: F401
    except ImportError:
        pip = subprocess.run(
            [sys.executable, "-m", "pip", "install", "--quiet", "uiautomation"],
            check=False,
        )
        if pip.returncode != 0:
            fail("pip install uiautomation failed")
        import uiautomation  # noqa: F401


def ensure_exe() -> Path:
    env_exe = os.environ.get("MLSMOON_EXE")
    if env_exe and Path(env_exe).is_file() and os.environ.get("MLSMOON_UI_REUSE") == "1":
        return Path(env_exe)

    out = Path(os.environ.get("TEMP", ".")) / "mlsmoon-ui-build"
    project = ROOT / "src" / "MlsmoonSkillManager.App" / "MlsmoonSkillManager.App.csproj"
    built = subprocess.run(
        ["dotnet", "build", str(project), "--nologo", "-o", str(out)],
        cwd=str(ROOT),
        check=False,
    )
    if built.returncode != 0:
        fail("dotnet build failed")
    exe = out / "MlsmoonSkillManager.exe"
    if not exe.is_file():
        fail(f"build produced no exe: {exe}")
    return exe


def window_control(timeout: float):
    import uiautomation as auto

    window = auto.WindowControl(searchDepth=3, AutomationId=WINDOW_ID)
    if window.Exists(timeout, 0.4):
        return window
    named = auto.WindowControl(searchDepth=3, Name="Moon Game Dev Tool Manager — UI TEST")
    if named.Exists(0.6, 0.2):
        return named
    fail(f"window not found: {WINDOW_ID}")
    return window


def button_width(button) -> int:
    rect = button.BoundingRectangle
    width = getattr(rect, "width", None)
    if callable(width):
        return int(width())
    if isinstance(width, int):
        return width
    return int(rect.right - rect.left)


def find_button(window, automation_id: str):
    for index in range(1, 40):
        button = window.ButtonControl(AutomationId=automation_id, foundIndex=index)
        if not button.Exists(0.12, 0.05):
            break
        if button.IsEnabled and button_width(button) > 0:
            return button
    return None


def invoke_button(automation_id: str, timeout: float) -> None:
    deadline = time.time() + timeout
    last = "missing"
    while time.time() < deadline:
        window = window_control(min(12, timeout))
        button = find_button(window, automation_id)
        if button is None:
            last = "no enabled button"
            time.sleep(0.3)
            continue
        try:
            button.GetInvokePattern().Invoke()
        except Exception as ex:
            fail(f"invoke {automation_id}: {ex}")
        return
    fail(f"button {automation_id}: {last}")


def wait_summary(prefix: str, timeout: float) -> None:
    deadline = time.time() + timeout
    last = ""
    while time.time() < deadline:
        window = window_control(8)
        text = window.TextControl(AutomationId="CatalogFilterSummary")
        if text.Exists(0.3, 0.1):
            last = text.Name
            if last.startswith(prefix):
                return
        time.sleep(0.2)
    fail(f"summary missing {prefix}: {last!r}")


def wait_named(name: str, timeout: float) -> None:
    window = window_control(8)
    if window.TextControl(Name=name).Exists(timeout, 0.3):
        return
    if window.ButtonControl(Name=name).Exists(0.4, 0.1):
        return
    fail(f"missing {name}")


def wait_probe(probe: Path, *needles: str, timeout: float = 8) -> str:
    deadline = time.time() + timeout
    text = ""
    while time.time() < deadline:
        if probe.is_file():
            text = probe.read_text(encoding="utf-8")
            if all(item in text for item in needles):
                return text
        time.sleep(0.2)
    fail(f"probe missing {needles}: {text!r}")
    return text


def write_settings(config: Path, workspace: Path) -> None:
    config.mkdir(parents=True, exist_ok=True)
    skill = workspace / ".agents" / "skills" / "open-psd-kit"
    skill.mkdir(parents=True, exist_ok=True)
    (skill / "SKILL.md").write_text("---\nname: open-psd-kit\n---\nprobe\n", encoding="utf-8")
    settings = {
        "lastWorkspace": str(workspace),
        "selectedRoots": [".agents"],
        "workspaces": [{"path": str(workspace), "selectedRoots": [".agents"]}],
        "theme": "Dark",
        "autoCheckUpdates": False,
        "showRepoLinks": False,
        "nasUser": "probe",
    }
    (config / "settings.json").write_text(json.dumps(settings, indent=2), encoding="utf-8")


def launch(exe: Path, config: Path, cache: Path) -> subprocess.Popen:
    env = os.environ.copy()
    env["MLSMOON_CONFIG_DIR"] = str(config)
    env["MLSMOON_CACHE_DIR"] = str(cache)
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = SW_SHOWNOACTIVATE
    return subprocess.Popen(
        [str(exe), "--dev", "--ui-test"],
        cwd=str(ROOT),
        env=env,
        stdout=subprocess.DEVNULL,
        stderr=subprocess.DEVNULL,
        startupinfo=startup,
        creationflags=CREATE_NO_WINDOW,
    )


def stop(proc: subprocess.Popen) -> None:
    if proc.poll() is not None:
        return
    proc.terminate()
    try:
        proc.wait(timeout=5)
    except subprocess.TimeoutExpired:
        proc.kill()


def main() -> None:
    ensure_uiautomation()
    exe = ensure_exe()
    stamp = time.strftime("%Y%m%d%H%M%S")
    temp = Path(os.environ.get("TEMP", ".")) / f"mlsmoon-ui-flow-{stamp}"
    config = temp / "config"
    workspace = temp / "workspace"
    write_settings(config, workspace)
    proc = launch(exe, config, temp / "cache")
    probe = config / "ui-probe.log"
    ready = config / "ui-ready.log"
    try:
        deadline = time.time() + 40
        while time.time() < deadline and not ready.is_file():
            if proc.poll() is not None:
                fail(f"process exited {proc.returncode} before window")
            time.sleep(0.2)
        if not ready.is_file():
            fail("ui-ready.log missing")
        window_control(20)
        invoke_button("OpenSettings", 20)
        wait_named("外观", 8)
        invoke_button("SettingsTab-connection", 8)
        wait_named("重新检查连接", 8)
        invoke_button("SettingsTab-folders", 8)
        invoke_button("OpenWorkspaceFolder", 8)
        wait_probe(probe, "open-folder", str(workspace))
        invoke_button("DialogPrimary", 8)
        invoke_button(OPEN_FOLDER_ID, 20)
        wait_probe(probe, "open-folder", "open-psd-kit")
        invoke_button("Filter-install-installed", 8)
        wait_summary("显示 1", 8)
        invoke_button("Filter-kind-skill", 8)
        wait_summary("显示 1", 8)
        invoke_button("Filter-kind-plugin", 8)
        wait_summary("显示 0", 8)
        print("ui_flow ok: settings + folders + open-folder + filter")
    finally:
        stop(proc)
        shutil.rmtree(temp, ignore_errors=True)


if __name__ == "__main__":
    main()
