#!/usr/bin/env python3
"""Install a Unity editor with Android Build Support on Linux, without Unity Hub.

Reads the module list from Unity's release API, so it installs exactly what Hub would:
the editor, Android Build Support, OpenJDK, Android SDK tools/platforms/build-tools and NDK.

    python3 tools/install_unity_linux.py --dest ~/unity            # version from ProjectVersion.txt
    python3 tools/install_unity_linux.py --dest ~/unity --no-android-sdk

Result: <dest>/Editor/Unity. Needs ~14 GB free, curl, bsdtar (libarchive-tools), cpio, unzip.
Running Unity still needs a licence; compiling with tools/unity_compile_check.py does not.
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
import urllib.request

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
API = "https://services.api.unity.com/unity/editor/release/v1/releases?version={v}&platform=LINUX&architecture=X86_64&limit=1"


def project_version() -> str:
    with open(os.path.join(REPO, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as handle:
        return re.search(r"m_EditorVersion:\s*(\S+)", handle.read()).group(1)


def release(version: str) -> dict:
    with urllib.request.urlopen(API.format(v=version), timeout=60) as response:
        results = json.load(response).get("results", [])
    if not results:
        raise SystemExit(f"Unity {version} not found in the release API")
    for download in results[0]["downloads"]:
        if download["platform"] == "LINUX":
            return download
    raise SystemExit(f"No Linux download for Unity {version}")


def run(cmd: str) -> None:
    subprocess.run(cmd, shell=True, check=True)


def fetch(url: str, path: str) -> None:
    print(f"  downloading {url}")
    run(f'curl -fsSL --retry 4 -o "{path}" "{url}"')


def install_editor(download: dict, dest: str) -> None:
    if os.path.exists(os.path.join(dest, "Editor", "Unity")):
        print("editor: already installed")
        return
    os.makedirs(dest, exist_ok=True)
    print("editor: downloading and extracting (~4.5 GB)")
    run(f'curl -fsSL --retry 4 "{download["url"]}" | tar -xJ -C "{dest}"')


def install_module(module: dict, dest: str, tmp: str) -> None:
    target = module["destination"].replace("{UNITY_PATH}", dest)
    rename = module.get("extractedPathRename")
    final = rename["to"].replace("{UNITY_PATH}", dest) if rename else target
    marker = os.path.join(final, ".installed-" + module["id"])
    if os.path.exists(marker):
        print(f"{module['id']}: already installed")
        return
    print(f"{module['id']}:")
    url = module["url"]
    archive = os.path.join(tmp, os.path.basename(url.split("?")[0]))
    fetch(url, archive)
    os.makedirs(target, exist_ok=True)
    if archive.endswith(".pkg"):
        unpack = os.path.join(tmp, "pkg")
        shutil.rmtree(unpack, ignore_errors=True)
        os.makedirs(unpack)
        run(f'bsdtar -xf "{archive}" -C "{unpack}"')
        payload = next(os.path.join(root, f) for root, _, files in os.walk(unpack) for f in files if f == "Payload")
        run(f'cd "{target}" && (gunzip -c "{payload}" 2>/dev/null || cat "{payload}") | cpio -idm --quiet')
        shutil.rmtree(unpack, ignore_errors=True)
    elif archive.endswith(".zip"):
        run(f'unzip -q -o "{archive}" -d "{target}"')
    else:
        run(f'tar -xf "{archive}" -C "{target}"')
    os.remove(archive)
    if rename:
        src = rename["from"].replace("{UNITY_PATH}", dest)
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
    os.makedirs(final, exist_ok=True)
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
    install_editor(download, dest)

    modules = list(walk(download.get("modules"), lambda i: i == "android"))
    if args.no_android_sdk:
        modules = modules[:1]
    with tempfile.TemporaryDirectory() as tmp:
        for module in modules:
            install_module(module, dest, tmp)

    jdk = os.path.join(dest, "Editor", "Data", "PlaybackEngines", "AndroidPlayer", "OpenJDK", "bin")
    for folder in (jdk,):
        if os.path.isdir(folder):
            for name in os.listdir(folder):
                path = os.path.join(folder, name)
                os.chmod(path, os.stat(path).st_mode | 0o111)
    print(f"\nUnity {version} ready: {os.path.join(dest, 'Editor', 'Unity')}")
    return 0


if __name__ == "__main__":
    sys.exit(main())
