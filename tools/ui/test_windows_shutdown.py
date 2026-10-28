"""Exercise native Windows shutdown after UI Automation touches the Player window.

The QA Player must support disposable storage (FONT10_QA_PLAYER). No personal
database or identification preference is used. A native crash is always a failure.
"""
import argparse
import ctypes
from ctypes import wintypes
import hashlib
import json
import os
from pathlib import Path
import re
import sqlite3
import subprocess
import time
import uuid


def write(path, value):
    path.write_text(json.dumps(value, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")


def window_for(pid):
    user32 = ctypes.WinDLL("user32", use_last_error=True)
    found = []
    callback_type = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    user32.EnumWindows.argtypes = [callback_type, wintypes.LPARAM]
    user32.GetWindowThreadProcessId.argtypes = [wintypes.HWND, ctypes.POINTER(wintypes.DWORD)]
    user32.GetWindowTextLengthW.argtypes = [wintypes.HWND]

    @callback_type
    def visit(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value == pid and user32.GetWindowTextLengthW(hwnd) > 0:
            found.append(hwnd)
        return True

    user32.EnumWindows(visit, 0)
    return found[0] if found else None


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--exe", type=Path, required=True)
    parser.add_argument("--out", type=Path, required=True)
    parser.add_argument("--mode", choices=["window-close", "application-quit", "save", "discard"], default="window-close")
    parser.add_argument("--legacy", action="store_true", help="Baseline Player without the focused QA probe")
    parser.add_argument("--no-ui-automation", action="store_true", help="Control run without accessing the UI Automation provider")
    args = parser.parse_args()
    if args.legacy and args.mode != "window-close":
        parser.error("Legacy Players support only the window-close baseline")
    exe, out = args.exe.resolve(), args.out.resolve()
    out.mkdir(parents=True, exist_ok=False)
    env = os.environ.copy()
    env["COGNA_TEST_DATABASE_PATH"] = str(out / "qa.db")
    env["COGNA_TEST_IDENTIFICATION_KEY"] = "Cogna.Shutdown.QA." + str(uuid.uuid4())
    env["COGNA_SHUTDOWN_QA_OUTPUT"] = str(out)
    command = [str(exe), "--windows-shutdown-qa", args.mode, "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0", "-logFile", str(out / "Player.log")]
    startup = subprocess.STARTUPINFO()
    startup.dwFlags |= subprocess.STARTF_USESHOWWINDOW
    startup.wShowWindow = subprocess.SW_HIDE
    child = subprocess.Popen(command, cwd=exe.parent, env=env, startupinfo=startup)
    payload = [exe, exe.parent / "UnityPlayer.dll", exe.parent / (exe.stem + "_Data") / "Managed" / "Assembly-CSharp.dll"]
    result = {"pid": child.pid, "mode": args.mode, "exe": str(exe), "exe_sha256": hashlib.sha256(exe.read_bytes()).hexdigest(), "payload_sha256": {str(path.relative_to(exe.parent)): hashlib.sha256(path.read_bytes()).hexdigest() for path in payload}, "database": env["COGNA_TEST_DATABASE_PATH"], "identification_key": env["COGNA_TEST_IDENTIFICATION_KEY"], "legacy": args.legacy}
    write(out / "launch.json", result)
    try:
        deadline = time.monotonic() + 120
        while child.poll() is None:
            log = (out / "Player.log").read_text(encoding="utf-8", errors="replace") if (out / "Player.log").exists() else ""
            ready = "Estado atual:" in log if args.legacy else (out / "ready.json").exists()
            hwnd = window_for(child.pid)
            if ready and hwnd:
                break
            if time.monotonic() > deadline:
                raise TimeoutError("Player failed to become ready")
            time.sleep(0.25)
        if child.poll() is not None:
            raise RuntimeError("Player exited before UI Automation interaction")
        automation_script = out / "touch-ui-automation.ps1"
        automation_script.write_text("""param([long]$WindowHandle)
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes
$element = [System.Windows.Automation.AutomationElement]::FromHandle([IntPtr]::new($WindowHandle))
$name = $element.Current.Name
$children = $element.FindAll([System.Windows.Automation.TreeScope]::Children, [System.Windows.Automation.Condition]::TrueCondition)
@{name=$name; childCount=$children.Count; handle=$WindowHandle; interacted=$true} | ConvertTo-Json
""", encoding="utf-8")
        result["ui_automation"] = {"interacted": False}
        if not args.no_ui_automation:
            interaction = subprocess.run(["powershell.exe", "-NoProfile", "-NonInteractive", "-ExecutionPolicy", "Bypass", "-File", str(automation_script), str(hwnd)], capture_output=True, text=True, timeout=30, startupinfo=startup)
            if interaction.returncode:
                raise RuntimeError(interaction.stderr)
            result["ui_automation"] = json.loads(interaction.stdout)
        write(out / "ui-automation.json", result["ui_automation"])
        if args.mode == "window-close":
            user32 = ctypes.WinDLL("user32", use_last_error=True)
            user32.PostMessageW.argtypes = [wintypes.HWND, wintypes.UINT, wintypes.WPARAM, wintypes.LPARAM]
            if not user32.PostMessageW(hwnd, 0x0010, 0, 0):  # WM_CLOSE, same path as the window X / Alt+F4.
                raise ctypes.WinError(ctypes.get_last_error())
        else:
            (out / "continue.txt").write_text("quit", encoding="utf-8")
        result["exit_code"] = child.wait(timeout=60)
        result["exit_hex"] = hex(result["exit_code"] & 0xffffffff)
        result["clean_exit"] = result["exit_code"] == 0
        log = (out / "Player.log").read_text(encoding="utf-8", errors="replace")
        result["managed_exception"] = bool(re.search(r"(?m)^[\w.]*Exception:", log)) or "Unhandled Exception:" in log
        if not args.legacy:
            ready = json.loads((out / "ready.json").read_text(encoding="utf-8"))
            with sqlite3.connect(out / "qa.db") as db:
                saved = db.execute("SELECT currentCash FROM GameSessionEntity WHERE sessionId=?", (ready["sessionId"],)).fetchone()
                assert db.execute("PRAGMA integrity_check").fetchone()[0] == "ok"
            expected = ready["initialCash"] if args.mode == "discard" else ready["pendingCash"]
            result["persistence"] = {"cash": saved[0], "expected": expected, "passed": saved[0] == expected, "integrity": "ok"}
            result["passed"] = result["clean_exit"] and not result["managed_exception"] and result["persistence"]["passed"] and (out / "quit-callback.json").exists()
        else:
            result["passed"] = result["clean_exit"]
    except Exception as error:
        result["error"] = str(error)
        result["passed"] = False
        if child.poll() is None:
            child.terminate()
            child.wait(timeout=30)
            result["terminated_own_child"] = True
    finally:
        write(out / "result.json", result)
    print(json.dumps(result, ensure_ascii=False), flush=True)
    return 0 if result["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
