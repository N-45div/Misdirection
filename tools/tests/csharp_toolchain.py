"""Compile and run small C# test programs outside Unity, on Linux/macOS or Windows.

- With Mono installed (CI, Linux): mcs + mono.
- On Windows without Mono: the Roslyn compiler that ships inside the Unity editor, against Unity's
  .NET Framework 4.8 reference assemblies; the .exe then runs on Windows' built-in .NET Framework.
  The editor is found via UNITY_EDITOR_DATA, else C:\\Unity\\<version>\\Editor\\Data or the Unity
  Hub default folder for the version in ProjectSettings/ProjectVersion.txt.
"""

from __future__ import annotations

import os
import re
import shutil
import subprocess
from typing import List, Optional, Sequence

REPO = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))


def _editor_version() -> Optional[str]:
    try:
        with open(os.path.join(REPO, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as handle:
            return re.search(r"m_EditorVersion:\s*(\S+)", handle.read()).group(1)
    except (OSError, AttributeError):
        return None


def _unity_data() -> Optional[str]:
    candidates = [os.environ.get("UNITY_EDITOR_DATA", "")]
    version = _editor_version()
    if version:
        candidates += [rf"C:\Unity\{version}\Editor\Data", rf"C:\Program Files\Unity\Hub\Editor\{version}\Editor\Data"]
    for path in candidates:
        if path and os.path.exists(os.path.join(path, "DotNetSdkRoslyn", "csc.dll")):
            return path
    return None


def mono_available() -> bool:
    return bool(shutil.which("mcs") and shutil.which("mono"))


def available() -> bool:
    return mono_available() or (os.name == "nt" and _unity_data() is not None)


def describe() -> str:
    if mono_available():
        return "mono"
    data = _unity_data() if os.name == "nt" else None
    return f"unity-roslyn ({data})" if data else "none"


def compile_exe(sources: Sequence[str], out_exe: str) -> None:
    """Compile <sources> into a console .exe that can use System.Numerics vectors and LINQ."""
    if mono_available():
        subprocess.run(["mcs", "-nologo", "-out:" + out_exe, "-r:System.Numerics.Vectors", "-r:System.Numerics", *sources],
                       check=True)
        return
    data = _unity_data()
    if os.name != "nt" or data is None:
        raise RuntimeError("No C# toolchain: install Mono, or (Windows) a Unity editor with tools/install_unity.py")
    api = os.path.join(data, "UnityReferenceAssemblies", "unity-4.8-api")
    refs = [os.path.join(api, n) for n in ("mscorlib.dll", "System.dll", "System.Core.dll", "System.Numerics.dll")]
    refs.append(os.path.join(api, "Facades", "System.Numerics.Vectors.dll"))
    rsp_lines: List[str] = ["-nologo", "-noconfig", "-nostdlib", "-target:exe", "-langversion:9.0", "-warn:0",
                            f'"-out:{out_exe}"']
    rsp_lines += [f'"-reference:{r}"' for r in refs if os.path.exists(r)]
    rsp_lines += [f'"{s}"' for s in sources]
    rsp = out_exe + ".rsp"
    with open(rsp, "w", encoding="utf-8") as handle:
        handle.write("\n".join(rsp_lines))
    dotnet = os.path.join(data, "NetCoreRuntime", "dotnet.exe")
    proc = subprocess.run([dotnet, os.path.join(data, "DotNetSdkRoslyn", "csc.dll"), "@" + rsp],
                          capture_output=True, text=True)
    if proc.returncode != 0:
        raise RuntimeError("C# compile failed:\n" + proc.stdout + proc.stderr)


def run_exe(exe: str, args: Sequence[str] = ()) -> subprocess.CompletedProcess:
    cmd = [exe, *args] if not mono_available() else ["mono", exe, *args]
    return subprocess.run(cmd, capture_output=True, text=True)
