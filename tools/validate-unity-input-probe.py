"""Exercise the AVD console API -> Kotlin -> Unity path and save observed results.

This only runs against a confirmed Android emulator. It restores zero controls and
closes its own console session on exit. No real vehicle control API is called.
"""
import argparse
from datetime import datetime, timezone
import json
import os
from pathlib import Path
import shutil
import subprocess
import threading
import time
import urllib.parse
import urllib.error
import urllib.request


PACKAGE = "com.example.pleosdemo.unityprobe"
REPOSITORY = Path(__file__).resolve().parent.parent


def resolve_adb_path(explicit=None):
    if explicit:
        resolved = shutil.which(explicit) or explicit
        if Path(resolved).is_file():
            return str(resolved)
        raise FileNotFoundError("ADB does not exist: " + explicit)
    candidates = []
    for variable in ("ANDROID_HOME", "ANDROID_SDK_ROOT"):
        if os.environ.get(variable):
            candidates.append(Path(os.environ[variable]) / "platform-tools/adb.exe")
    properties = REPOSITORY / "local.properties"
    if properties.is_file():
        for line in properties.read_text(encoding="utf-8-sig").splitlines():
            if line.strip().startswith("sdk.dir="):
                sdk = line.split("=", 1)[1].strip().replace("\\:", ":").replace("\\\\", "\\")
                candidates.append(Path(sdk) / "platform-tools/adb.exe")
    on_path = shutil.which("adb")
    if on_path:
        candidates.append(Path(on_path))
    if os.environ.get("LOCALAPPDATA"):
        candidates.append(Path(os.environ["LOCALAPPDATA"]) / "Android/Sdk/platform-tools/adb.exe")
    for candidate in candidates:
        if candidate.is_file():
            return str(candidate)
    raise FileNotFoundError("Set --adb, ANDROID_HOME, or local.properties sdk.dir to locate ADB")


def parse_probe_line(line):
    if "PLEOS_PROBE {" not in line:
        return None
    try:
        payload = line.split("PLEOS_PROBE ", 1)[1]
        snapshot, end = json.JSONDecoder().raw_decode(payload)
        ready_fields = [part.split("=", 1)[1].lower() for part in payload[end:].split() if part.startswith("inputReady=")]
        ready = None
        if len(ready_fields) == 1 and ready_fields[0] in ("true", "false"):
            ready = ready_fields[0] == "true"
        return {"snapshot": snapshot, "inputReady": ready, "line": line}
    except (ValueError, IndexError):
        return None


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--adb", help="ADB executable; otherwise use SDK environment, local.properties, or PATH")
    parser.add_argument("--serial", default="emulator-5554")
    parser.add_argument("--apk", default=str(REPOSITORY / "build/unity-probe/parkside-input-probe.apk"))
    parser.add_argument("--no-install", action="store_true")
    options = parser.parse_args()
    output = REPOSITORY / "build/unity-probe"
    output.mkdir(parents=True, exist_ok=True)
    adb_command = [resolve_adb_path(options.adb), "-s", options.serial]
    flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0

    def adb(*arguments, timeout=30):
        return subprocess.run(adb_command + list(arguments), check=True, timeout=timeout,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, creationflags=flags).stdout.decode("utf-8", "replace").strip()

    adb("wait-for-device", timeout=30)
    qemu = adb("shell", "getprop", "ro.kernel.qemu") or adb("shell", "getprop", "ro.boot.qemu")
    if "1" not in qemu.splitlines():
        raise RuntimeError("This validation must only run on the Pleos AVD, never a real vehicle/device")
    adb("forward", "tcp:8088", "tcp:8088")
    base = "http://127.0.0.1:8088"
    client = "parkside-unity-probe-" + str(os.getpid())

    def request(path, method="GET"):
        req = urllib.request.Request(base + path, data=b"" if method != "GET" else None, method=method)
        with urllib.request.urlopen(req, timeout=4) as response:
            body = response.read()
            return json.loads(body) if body else {}

    # The console only publishes DDS state while a console session is active.
    try:
        deadline = time.monotonic() + 10
        while True:
            request("/browser/session?client=" + urllib.parse.quote(client), "POST")
            initial_state = request("/vehicle/state")
            initial_gear = (initial_state.get("gear", {}).get("actual") or {}).get("value")
            if initial_gear is not None or time.monotonic() >= deadline:
                break
            time.sleep(0.5)
    finally:
        request("/browser/session?client=" + urllib.parse.quote(client) + "&closing=1", "POST")
    # P/0 are preconditions; the test never selects D/R or moves the real AVD vehicle.
    if initial_gear not in ("Park", "P", 4):
        raise RuntimeError("Set the AVD to P before starting this validation: " + repr(initial_gear))
    if not options.no_install:
        if not Path(options.apk).is_file():
            raise RuntimeError("Build the probe APK first: " + options.apk)
        print(adb("install", "-r", "-g", options.apk, timeout=90), flush=True)
    adb("shell", "am", "force-stop", PACKAGE)

    observations = []
    raw_lines = []
    lock = threading.Lock()
    reader_stop = threading.Event()
    heartbeat_stop = threading.Event()
    # Follow fresh output; an earlier successful run must never satisfy this run.
    logcat = subprocess.Popen(adb_command + ["logcat", "-T", "1", "-v", "raw", "Unity:I", "PleosUnityBridge:I", "*:S"],
                              stdout=subprocess.PIPE, stderr=subprocess.STDOUT, creationflags=flags)

    def read_logs():
        while not reader_stop.is_set():
            data = logcat.stdout.readline()
            if not data:
                return
            line = data.decode("utf-8", "replace").rstrip()
            with lock:
                raw_lines.append(line)
                parsed = parse_probe_line(line)
                if parsed is not None:
                    observations.append({"receivedAt": time.monotonic(), **parsed})

    def heartbeat():
        while not heartbeat_stop.is_set():
            try:
                request("/browser/session?client=" + urllib.parse.quote(client), "POST")
            except Exception:
                pass
            heartbeat_stop.wait(0.5)

    threading.Thread(target=read_logs, daemon=True).start()
    heartbeat_thread = threading.Thread(target=heartbeat, daemon=True)
    report = {"package": PACKAGE, "serial": options.serial, "startedAtUtc": datetime.now(timezone.utc).isoformat(), "initialState": initial_state,
              "cases": [], "passed": False, "scope": "AVD console API -> existing Kotlin adapters -> Unity C#"}

    def controls(steering=0, accelerator=0, brake=0):
        query = urllib.parse.urlencode({"speed": 0, "accelerator": accelerator, "brake": brake, "steering": steering})
        deadline = time.monotonic() + 5
        while True:
            try:
                return request("/vehicle/controls?" + query, "PUT")
            except urllib.error.HTTPError as error:
                # The browser heartbeat can precede DDS publisher readiness after an install.
                if error.code != 503 or time.monotonic() >= deadline:
                    raise
                time.sleep(0.2)

    def check_case(name, predicate, after, timeout=20, expected_ready=True):
        deadline = time.monotonic() + timeout
        while time.monotonic() < deadline:
            with lock:
                candidates = [value for value in observations if value["receivedAt"] >= after + 0.3]
            for value in reversed(candidates):
                if predicate(value["snapshot"]) and value["inputReady"] is expected_ready:
                    report["cases"].append({"name": name, "passed": True, "observed": value["snapshot"], "inputReady": value["inputReady"], "unityLog": value["line"]})
                    print("PASS " + name + ": " + json.dumps(value["snapshot"], ensure_ascii=False), flush=True)
                    return value["snapshot"]
            time.sleep(0.2)
        with lock:
            latest = observations[-1]["snapshot"] if observations else None
            latest_ready = observations[-1]["inputReady"] if observations else None
        report["cases"].append({"name": name, "passed": False, "latest": latest, "inputReady": latest_ready})
        hint = "; inputReady=" + repr(latest_ready)
        if observations and latest_ready is None:
            hint += "; rebuild the APK to include current inputReady telemetry"
        raise RuntimeError("Timed out: " + name + hint + "; latest=" + json.dumps(latest, ensure_ascii=False))

    def screenshot(name):
        image = subprocess.run(adb_command + ["exec-out", "screencap", "-p"], check=True,
                               stdout=subprocess.PIPE, timeout=20, creationflags=flags).stdout
        (output / name).write_bytes(image)

    try:
        heartbeat_thread.start()
        deadline = time.monotonic() + 15
        while True:
            state = request("/vehicle/state")
            if (state.get("browser", {}).get("active") and
                    state.get("controls", {}).get("emulator_active") and
                    state.get("gear", {}).get("bridge_ready")):
                break
            if time.monotonic() >= deadline:
                raise RuntimeError("AVD console DDS bridge did not become ready")
            time.sleep(0.2)
        controls()
        print(adb("shell", "am", "start", "-n", PACKAGE + "/com.unity3d.player.UnityPlayerActivity"), flush=True)
        check_case("connected-neutral", lambda s: s["allowed"] and s["realGear"] == 4 and abs(s["acceleratorPercent"]) < 0.1,
                   time.monotonic(), timeout=45)
        changed = time.monotonic()
        controls(accelerator=37)
        check_case("accelerator-37-percent", lambda s: s["allowed"] and abs(s["acceleratorPercent"] - 37) < 0.2 and abs(s["throttle"] - 0.37) < 0.01, changed)
        changed = time.monotonic()
        controls(steering=25, accelerator=37)
        check_case("console-steering-plus-25-unity-plus-25", lambda s: s["allowed"] and abs(s["steeringDegrees"] - 25) < 0.5, changed)
        screenshot("connected-steering-accelerator.png")
        changed = time.monotonic()
        controls(steering=25, brake=50)
        check_case("brake-50-percent", lambda s: s["allowed"] and abs(s["brakePercent"] - 50) < 0.2 and abs(s["brake"] - 0.5) < 0.01, changed)
        screenshot("connected-brake.png")
        changed = time.monotonic()
        controls()
        check_case("controls-released", lambda s: s["allowed"] and abs(s["steeringDegrees"]) < 0.5 and s["throttle"] == 0 and s["brakePercent"] == 0, changed)
        (output / "android-meminfo.txt").write_text(adb("shell", "dumpsys", "meminfo", PACKAGE), encoding="utf-8")

        heartbeat_stop.set()
        heartbeat_thread.join(timeout=5)
        request("/browser/session?client=" + urllib.parse.quote(client) + "&closing=1", "POST")
        changed = time.monotonic()
        state = request("/vehicle/state")
        if state.get("browser", {}).get("active"):
            report["cases"].append({"name": "console-disconnect", "skipped": "Another console session is active; it was left unchanged"})
        else:
            check_case("console-disconnect-fails-closed", lambda s: not s["allowed"] and s["throttle"] == 0 and s["brake"] == 1, changed, expected_ready=False)
        report["passed"] = all(value.get("passed", True) for value in report["cases"])
        report["finalState"] = request("/vehicle/state")
    except Exception as error:
        report["error"] = str(error)
        raise
    finally:
        heartbeat_stop.set()
        if heartbeat_thread.is_alive(): heartbeat_thread.join(timeout=5)
        for cleanup in (lambda: controls(), lambda: request("/browser/session?client=" + urllib.parse.quote(client) + "&closing=1", "POST")):
            try: cleanup()
            except Exception: pass
        reader_stop.set()
        logcat.terminate()
        try: logcat.wait(timeout=5)
        except subprocess.TimeoutExpired: logcat.kill()
        with lock:
            report["snapshotCount"] = len(observations)
            error_markers = ("NullReferenceException", "AndroidJavaException", "MissingMethodException",
                             "Can't add component", "Shader error", "shader is not supported")
            report["runtimeErrors"] = [line for line in raw_lines if any(marker in line for marker in error_markers)]
            if report["runtimeErrors"]:
                report["passed"] = False
            (output / "unity-device.log").write_text("\n".join(raw_lines), encoding="utf-8")
        (output / "validation.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
        print("Report: " + str(output / "validation.json"), flush=True)
    if not report["passed"]:
        raise RuntimeError("Runtime errors were observed; inspect unity-device.log and screenshots")


if __name__ == "__main__":
    main()
