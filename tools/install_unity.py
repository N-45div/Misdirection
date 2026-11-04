#!/usr/bin/env python3
"""Install a Unity editor with Android Build Support, without Unity Hub (Linux and Windows).

Reads the module list from Unity's release API, so it installs exactly what Hub would:
the editor, Android Build Support, OpenJDK, Android SDK tools/platforms/build-tools and NDK.

    python3 tools/install_unity.py --dest ~/unity                 # Linux, version from ProjectVersion.txt
    python  tools/install_unity.py --dest C:\\Unity\\6000.0.66f2    # Windows
    python3 tools/install_unity.py --dest ~/unity --no-android-sdk

Result: <dest>/Editor/Unity (Linux) or <dest>\\Editor\\Unity.exe (Windows). Needs ~14 GB free.
Linux needs curl, bsdtar (libarchive-tools) and cpio. Windows needs nothing extra and no admin
rights: the installers run "as invoker", so pick a folder you can write to (avoid spaces).
Running Unity still needs a licence; compiling with tools/unity_compile_check.py does not.
Safe to re-run: finished parts are skipped.
"""

from __future__ import annotations

import argparse
import json
import os
import re
import shutil
import subprocess
import sys
import tempfile
import time
import urllib.request
import zipfile

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
API = "https://services.api.unity.com/unity/editor/release/v1/releases?version={v}&platform={p}&architecture=X86_64&limit=1"
WINDOWS = os.name == "nt"
PLATFORM = "WINDOWS" if WINDOWS else "LINUX"
EDITOR_EXE = os.path.join("Editor", "Unity.exe" if WINDOWS else "Unity")


def project_version() -> str:
    with open(os.path.join(REPO, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as handle:
        return re.search(r"m_EditorVersion:\s*(\S+)", handle.read()).group(1)


def release(version: str) -> dict:
    with urllib.request.urlopen(API.format(v=version, p=PLATFORM), timeout=60) as response:
        results = json.load(response).get("results", [])
    if not results:
        raise SystemExit(f"Unity {version} not found in the release API")
    for download in results[0]["downloads"]:
        if download["platform"] == PLATFORM:
            return download
    raise SystemExit(f"No {PLATFORM} download for Unity {version}")


def run(cmd: str) -> None:
    subprocess.run(cmd, shell=True, check=True)


def fetch(url: str, path: str) -> None:
    print(f"  downloading {url}", flush=True)
    # curl ships with Windows 10+ as curl.exe.
    curl = "curl.exe" if WINDOWS else "curl"
    run(f'{curl} -fsSL --retry 4 --retry-delay 5 -o "{path}" "{url}"')


def run_windows_installer(exe: str, install_root: str) -> None:
    """Silent NSIS install into install_root without a UAC prompt.

    Unity's installers ask for "highestAvailable", which prompts on admin accounts.
    RunAsInvoker keeps them at the user's level; that works for a user-writable folder.
    NSIS wants /D= last and unquoted, so the command line is passed as one string.
    """
    if " " in install_root:
        raise SystemExit(f"Install folder must not contain spaces (NSIS /D= limitation): {install_root}")
    env = dict(os.environ, __COMPAT_LAYER="RunAsInvoker")
    proc = subprocess.Popen(f'"{exe}" /S /D={install_root}', env=env)
    # Without admin, Unity's installers end with a Yes/No box ("...until some dependencies are installed.
    # View the list now?") even in silent mode. The only missing item is a firewall rule for the remote
    # profiler; answer No so the install doesn't hang. The list is saved to <root>\MissingDependencies.
    while proc.poll() is None:
        if _answer_no_to_dependency_prompt(proc.pid):
            print("  (answered No to the missing-dependencies prompt; see MissingDependencies.txt)", flush=True)
        time.sleep(1.0)
    if proc.returncode != 0:
        raise SystemExit(f"{os.path.basename(exe)} exited with code {proc.returncode}")


def _answer_no_to_dependency_prompt(pid: int) -> bool:
    import ctypes
    from ctypes import wintypes

    user32 = ctypes.windll.user32
    enum_proc = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)
    clicked = []

    def text_of(hwnd) -> str:
        buf = ctypes.create_unicode_buffer(1024)
        user32.SendMessageW(hwnd, 0x000D, 1024, buf)  # WM_GETTEXT
        return buf.value

    def on_top(hwnd, _):
        owner = wintypes.DWORD()
        user32.GetWindowThreadProcessId(hwnd, ctypes.byref(owner))
        if owner.value != pid or not user32.IsWindowVisible(hwnd):
            return True
        children = []
        user32.EnumChildWindows(hwnd, enum_proc(lambda h, _l: children.append(h) or True), 0)
        texts = {h: text_of(h) for h in children}
        if any("dependencies" in t for t in texts.values()):
            for h, t in texts.items():
                if t.replace("&", "") == "No":
                    user32.SendMessageW(h, 0x00F5, 0, 0)  # BM_CLICK
                    clicked.append(h)
                    return False
        return True

    user32.EnumWindows(enum_proc(on_top), 0)
    return bool(clicked)


def install_editor(download: dict, dest: str, tmp: str) -> None:
    if os.path.exists(os.path.join(dest, EDITOR_EXE)):
        print("editor: already installed", flush=True)
        return
    os.makedirs(dest, exist_ok=True)
    size = download.get("downloadSize", {}).get("value", 0) / 1e9
    print(f"editor: downloading and installing (~{size:.1f} GB)", flush=True)
    if WINDOWS:
        exe = os.path.join(tmp, os.path.basename(download["url"]))
        fetch(download["url"], exe)
        run_windows_installer(exe, dest)
        os.remove(exe)
        if not os.path.exists(os.path.join(dest, EDITOR_EXE)):
            raise SystemExit("The editor installer finished but Editor\\Unity.exe is missing.")
    else:
        run(f'curl -fsSL --retry 4 "{download["url"]}" | tar -xJ -C "{dest}"')


def extract_zip(archive: str, target: str) -> None:
    # The \\?\ prefix lifts Windows' 260-character path limit (the NDK has deep paths).
    root = "\\\\?\\" + os.path.abspath(target) if WINDOWS else target
    with zipfile.ZipFile(archive) as z:
        z.extractall(root)
    if not WINDOWS:
        # zipfile drops Unix permissions; restore the executable bits.
        with zipfile.ZipFile(archive) as z:
            for info in z.infolist():
                mode = (info.external_attr >> 16) & 0o777
                if mode & 0o111:
                    path = os.path.join(target, info.filename)
                    if os.path.isfile(path):
                        os.chmod(path, mode)


def install_module(module: dict, dest: str, tmp: str) -> None:
    target = os.path.normpath(module["destination"].replace("{UNITY_PATH}", dest))
    rename = module.get("extractedPathRename")
    final = os.path.normpath(rename["to"].replace("{UNITY_PATH}", dest)) if rename else target
    marker_dir = final if not (WINDOWS and module["url"].endswith(".exe")) else os.path.join(dest, "Editor")
    marker = os.path.join(marker_dir, ".installed-" + module["id"])
    if os.path.exists(marker):
        print(f"{module['id']}: already installed", flush=True)
        return
    print(f"{module['id']}:", flush=True)
    url = module["url"]
    archive = os.path.join(tmp, os.path.basename(url.split("?")[0]))
    fetch(url, archive)
    os.makedirs(target, exist_ok=True)
    if archive.endswith(".exe"):
        run_windows_installer(archive, target)
    elif archive.endswith(".pkg"):
        unpack = os.path.join(tmp, "pkg")
        shutil.rmtree(unpack, ignore_errors=True)
        os.makedirs(unpack)
        run(f'bsdtar -xf "{archive}" -C "{unpack}"')
        payload = next(os.path.join(root, f) for root, _, files in os.walk(unpack) for f in files if f == "Payload")
        run(f'cd "{target}" && (gunzip -c "{payload}" 2>/dev/null || cat "{payload}") | cpio -idm --quiet')
        shutil.rmtree(unpack, ignore_errors=True)
    elif archive.endswith(".zip"):
        extract_zip(archive, target)
    else:
        run(f'tar -xf "{archive}" -C "{target}"')
    os.remove(archive)
    if rename:
        src = os.path.normpath(rename["from"].replace("{UNITY_PATH}", dest))
        if os.path.isdir(src) and os.path.abspath(src) != os.path.abspath(final):
            if os.path.isdir(final) and os.path.abspath(final) != os.path.abspath(target):
                shutil.rmtree(final)
            if os.path.abspath(final) == os.path.abspath(target):
                # Move the contents of src up into target.
                for name in os.listdir(src):
                    shutil.move(os.path.join(src, name), os.path.join(target, name))
                os.rmdir(src)
            else:
                os.makedirs(os.path.dirname(final), exist_ok=True)
                shutil.move(src, final)
    os.makedirs(marker_dir, exist_ok=True)
    open(marker, "w").close()


def walk(modules, wanted):
    for module in modules or []:
        if wanted(module["id"]):
            yield module
            yield from walk(module.get("subModules"), lambda _id: True)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--dest", required=True)
    parser.add_argument("--version", default=None)
    parser.add_argument("--no-android-sdk", action="store_true", help="install Android Build Support only, no JDK/SDK/NDK")
    args = parser.parse_args()

    version = args.version or project_version()
    dest = os.path.abspath(os.path.expanduser(args.dest))
    download = release(version)

    modules = list(walk(download.get("modules"), lambda i: i == "android"))
    if args.no_android_sdk:
        modules = modules[:1]
    with tempfile.TemporaryDirectory() as tmp:
        install_editor(download, dest, tmp)
        for module in modules:
            install_module(module, dest, tmp)

    if not WINDOWS:
        jdk = os.path.join(dest, "Editor", "Data", "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin")
        if os.path.isdir(jdk):
            for name in os.listdir(jdk):
                path = os.path.join(jdk, name)
                os.chmod(path, os.stat(path).st_mode | 0o111)
    print(f"\nUnity {version} ready: {os.path.join(dest, EDITOR_EXE)}", flush=True)
    return 0


if __name__ == "__main__":
    sys.exit(main())
