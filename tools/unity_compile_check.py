#!/usr/bin/env python3
"""Compile the Unity project's C# the way the Unity Editor would, without running Unity.

Why: the Unity Editor needs a licence to run, even headless. Compiling only needs the editor's
files, so this script lets anyone (or a coding agent) check every script against the real
Unity 6 and Meta XR SDK v207 assemblies before opening Unity.

What it does:
  1. Resolves Packages/manifest.json the way Unity's Package Manager does (built-in packages,
     packages bundled with the editor, packages.unity.com, and scoped registries like Meta's).
  2. Finds every assembly definition (.asmdef) in those packages and in Assets/.
  3. Works out each assembly's defines (Unity version, platform, versionDefines) and references.
  4. Compiles them in dependency order with the editor's own Roslyn compiler, in two contexts:
     "editor" (what you see in the Unity Editor) and "player" (the Android build).

Usage:
  python3 tools/unity_compile_check.py --unity /path/to/Unity/Editor
  python3 tools/unity_compile_check.py --unity ... --context editor --verbose

Get the editor (Linux, ~4.5 GB) without Unity Hub:
  curl -L https://download.unity3d.com/download_unity/b20bc5da3050/LinuxEditorInstaller/Unity-6000.0.66f2.tar.xz | tar -xJ

Exit code is 0 only if every assembly under Assets/ compiles in every requested context.
Third-party failures are reported but don't fail the run (they usually mean this script
emulates Unity imperfectly, not that the package is broken).
"""

from __future__ import annotations

import argparse
import concurrent.futures
import io
import json
import os
import re
import shutil
import struct
import subprocess
import sys
import tarfile
import tempfile
import urllib.request
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Sequence, Set, Tuple

REPO = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
DEFAULT_CACHE = os.path.join(os.path.expanduser("~"), ".cache", "misdirection-unity-check")
UNITY_REGISTRY = "https://packages.unity.com"

# --------------------------------------------------------------------------- versions


def parse_version(text: str) -> Tuple:
    """Comparable key for semver and Unity versions (6000.0.66f2, 1.17.0-pre.2)."""
    text = text.strip()
    main, _, pre = text.partition("-")
    m = re.match(r"^(\d+)(?:\.(\d+))?(?:\.(\d+))?([abfp])?(\d+)?", main)
    if not m:
        return (0, 0, 0, 0, 0)
    major, minor, patch = (int(m.group(i) or 0) for i in (1, 2, 3))
    stage = {"a": 0, "b": 1, "f": 2, "p": 3}.get(m.group(4) or "f", 2)
    stage_n = int(m.group(5) or 0)
    # A prerelease (1.17.0-pre.2) sorts before the release (1.17.0).
    pre_key = (0, pre) if pre else (1, "")
    return (major, minor, patch, pre_key, stage, stage_n)


def version_matches(version: str, expression: str) -> bool:
    """Unity versionDefines expressions: '' (any), '1.2.3' (>=), '[a,b]' / '(a,b)' / '[a,)' ranges."""
    expression = (expression or "").strip()
    if not expression:
        return True
    v = parse_version(version)
    if expression[0] in "[(" and expression[-1] in "])":
        low, _, high = expression[1:-1].partition(",")
        low, high = low.strip(), high.strip()
        if "," not in expression:  # [1.0] exact
            return v == parse_version(low)
        if low:
            lv = parse_version(low)
            if v < lv or (expression[0] == "(" and v == lv):
                return False
        if high:
            hv = parse_version(high)
            if v > hv or (expression[-1] == ")" and v == hv):
                return False
        return True
    return v >= parse_version(expression)


# --------------------------------------------------------------------------- packages


@dataclass
class Package:
    name: str
    version: str
    path: str
    dependencies: Dict[str, str]


class PackageResolver:
    def __init__(self, editor_data: str, cache: str, registries: List[dict], verbose: bool = False):
        self.pm = os.path.join(editor_data, "Resources", "PackageManager")
        self.cache = cache
        self.registries = registries
        self.verbose = verbose
        self.editor_manifest = self._load_editor_manifest()
        self._metadata: Dict[str, dict] = {}

    def _load_editor_manifest(self) -> Dict[str, dict]:
        path = os.path.join(self.pm, "Editor", "manifest.json")
        if not os.path.exists(path):
            return {}
        with open(path, encoding="utf-8") as handle:
            data = json.load(handle)
        return data.get("packages", data)

    def _registry_for(self, name: str) -> str:
        for reg in self.registries:
            for scope in reg.get("scopes", []):
                if name == scope or name.startswith(scope + "."):
                    return reg["url"].rstrip("/")
        return UNITY_REGISTRY

    def _meta(self, name: str) -> dict:
        if name not in self._metadata:
            url = f"{self._registry_for(name)}/{name}"
            with urllib.request.urlopen(url, timeout=60) as response:
                self._metadata[name] = json.load(response)
        return self._metadata[name]

    def minimum_version(self, name: str) -> Optional[str]:
        info = self.editor_manifest.get(name)
        if isinstance(info, dict):
            return info.get("minimumVersion")
        return None

    def fetch(self, name: str, version: str) -> Package:
        builtin = os.path.join(self.pm, "BuiltInPackages", name)
        if os.path.isdir(builtin):
            return self._load(builtin)

        target = os.path.join(self.cache, "packages", f"{name}@{version}")
        if not os.path.isdir(target):
            bundled = os.path.join(self.pm, "Editor", f"{name}-{version}.tgz")
            if os.path.exists(bundled):
                with open(bundled, "rb") as handle:
                    data = handle.read()
                source = "editor bundle"
            else:
                meta = self._meta(name)
                if version not in meta.get("versions", {}):
                    raise RuntimeError(f"{name}@{version} not found in {self._registry_for(name)}")
                url = meta["versions"][version]["dist"]["tarball"]
                with urllib.request.urlopen(url, timeout=600) as response:
                    data = response.read()
                source = url
            if self.verbose:
                print(f"  fetch {name}@{version} <- {source}")
            tmp = target + ".tmp"
            shutil.rmtree(tmp, ignore_errors=True)
            os.makedirs(tmp)
            with tarfile.open(fileobj=io.BytesIO(data), mode="r:gz") as tar:
                for member in tar.getmembers():
                    parts = member.name.split("/", 1)
                    if len(parts) < 2 or not parts[1] or member.issym() or member.islnk() or ".." in member.name:
                        continue
                    member.name = parts[1]
                    tar.extract(member, tmp)
            os.replace(tmp, target)
        return self._load(target)

    @staticmethod
    def _load(path: str) -> Package:
        with open(os.path.join(path, "package.json"), encoding="utf-8") as handle:
            pj = json.load(handle)
        return Package(pj["name"], pj.get("version", "0.0.0"), path, pj.get("dependencies", {}) or {})

    def resolve(self, direct: Dict[str, str]) -> Dict[str, Package]:
        """UPM-style: manifest versions win; otherwise the highest requested version, at least the editor minimum."""
        chosen: Dict[str, str] = dict(direct)
        resolved: Dict[str, Package] = {}
        changed = True
        while changed:
            changed = False
            for name, version in list(chosen.items()):
                minimum = self.minimum_version(name)
                if minimum and parse_version(minimum) > parse_version(version) and name not in direct:
                    chosen[name] = version = minimum
                if name in resolved and resolved[name].version == version:
                    continue
                pkg = self.fetch(name, version)
                resolved[name] = pkg
                for dep, dep_version in pkg.dependencies.items():
                    if dep in direct:
                        continue
                    if dep not in chosen or parse_version(dep_version) > parse_version(chosen[dep]):
                        chosen[dep] = dep_version
                        changed = True
        return resolved


# --------------------------------------------------------------------------- assemblies


@dataclass
class Assembly:
    name: str
    path: str  # folder holding the asmdef
    data: dict
    owner: str  # package name or "Assets"
    guid: Optional[str] = None
    sources: List[str] = field(default_factory=list)
    rsp_defines: List[str] = field(default_factory=list)

    @property
    def is_project(self) -> bool:
        return self.owner == "Assets"

    def editor_only(self) -> bool:
        return self.data.get("includePlatforms") == ["Editor"]

    def compatible(self, context: str) -> bool:
        include = self.data.get("includePlatforms") or []
        exclude = self.data.get("excludePlatforms") or []
        platform = "Editor" if context == "editor" else "Android"
        if include:
            return platform in include
        return platform not in exclude


def _hidden(part: str) -> bool:
    return part.startswith(".") or part.endswith("~") or part in ("cvs", "CVS")


def _read_guid(meta_path: str) -> Optional[str]:
    try:
        with open(meta_path, encoding="utf-8") as handle:
            for line in handle:
                if line.startswith("guid:"):
                    return line.split(":", 1)[1].strip()
    except OSError:
        pass
    return None


def discover(roots: Sequence[Tuple[str, str]]) -> Tuple[List[Assembly], List[Tuple[str, str]], List[str]]:
    """Returns assemblies, precompiled DLLs [(path, owner)], and loose scripts in Assets with no asmdef."""
    assemblies: List[Assembly] = []
    dlls: List[Tuple[str, str]] = []
    loose: List[str] = []
    asmrefs: List[Tuple[str, str]] = []  # (folder, reference)
    by_folder: Dict[str, Assembly] = {}

    for root, owner in roots:
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames[:] = sorted(d for d in dirnames if not _hidden(d))
            for f in filenames:
                full = os.path.join(dirpath, f)
                if f.endswith(".asmdef"):
                    with open(full, encoding="utf-8-sig") as handle:
                        data = json.load(handle)
                    asm = Assembly(data["name"], dirpath, data, owner, _read_guid(full + ".meta"))
                    assemblies.append(asm)
                    by_folder[dirpath] = asm
                elif f.endswith(".asmref"):
                    with open(full, encoding="utf-8-sig") as handle:
                        asmrefs.append((dirpath, json.load(handle).get("reference", "")))
                elif f.lower().endswith(".dll"):
                    dlls.append((full, owner))

    by_name = {a.name: a for a in assemblies}
    by_guid = {a.guid: a for a in assemblies if a.guid}
    ref_folders: Dict[str, Assembly] = {}
    for folder, ref in asmrefs:
        target = by_guid.get(ref[5:]) if ref.startswith("GUID:") else by_name.get(ref)
        if target:
            ref_folders[folder] = target

    for root, owner in roots:
        for dirpath, dirnames, filenames in os.walk(root):
            dirnames[:] = sorted(d for d in dirnames if not _hidden(d))
            for f in filenames:
                if not f.endswith(".cs"):
                    continue
                full = os.path.join(dirpath, f)
                d = dirpath
                target = None
                while True:
                    if d in by_folder:
                        target = by_folder[d]
                        break
                    if d in ref_folders:
                        target = ref_folders[d]
                        break
                    if os.path.normpath(d) == os.path.normpath(root):
                        break
                    d = os.path.dirname(d)
                if target:
                    target.sources.append(full)
                elif owner == "Assets":
                    loose.append(full)

    for asm in assemblies:
        rsp = os.path.join(asm.path, "csc.rsp")
        if os.path.exists(rsp):
            with open(rsp, encoding="utf-8") as handle:
                for line in handle:
                    m = re.match(r"^\s*[-/]d(?:efine)?:(\S+)", line)
                    if m:
                        asm.rsp_defines.extend(x for x in re.split(r"[;,]", m.group(1)) if x)
    return assemblies, dlls, loose


def is_managed_dll(path: str) -> bool:
    try:
        with open(path, "rb") as handle:
            head = handle.read(4096)
        if head[:2] != b"MZ":
            return False
        pe = struct.unpack_from("<I", head, 0x3C)[0]
        if head[pe:pe + 4] != b"PE\0\0":
            return False
        magic = struct.unpack_from("<H", head, pe + 24)[0]
        dirs = pe + 24 + (96 if magic == 0x10B else 112)
        clr_rva, clr_size = struct.unpack_from("<II", head, dirs + 14 * 8)
        return clr_rva != 0 and clr_size != 0
    except (OSError, struct.error):
        return False


def dll_flags(path: str) -> Tuple[bool, bool, bool]:
    """(explicitly_referenced, editor_ok, android_ok) from the DLL's .meta PluginImporter settings."""
    explicit, editor_ok, android_ok = False, True, True
    meta = path + ".meta"
    if not os.path.exists(meta):
        return explicit, editor_ok, android_ok
    with open(meta, encoding="utf-8", errors="replace") as handle:
        text = handle.read()
    explicit = "isExplicitlyReferenced: 1" in text
    any_platform = re.search(r"Any:\s*\n\s*second:\s*\n\s*enabled: (\d)", text)
    if any_platform and any_platform.group(1) == "1":
        return explicit, True, True

    def enabled(platform: str) -> Optional[bool]:
        m = re.search(r"first:\s*\n\s*" + platform + r": " + platform + r"\s*\n\s*second:\s*\n\s*enabled: (\d)", text)
        return None if m is None else m.group(1) == "1"

    e = enabled("Editor")
    a = enabled("Android")
    if e is not None:
        editor_ok = e
    if a is not None:
        android_ok = a
    if "PluginImporter" in text and e is None and a is None and any_platform:
        editor_ok = android_ok = False
    return explicit, editor_ok, android_ok


# --------------------------------------------------------------------------- compile


def unity_defines(unity_version: str, context: str, extra: Sequence[str]) -> List[str]:
    major, minor, patch = parse_version(unity_version)[:3]
    defines = [f"UNITY_{major}", f"UNITY_{major}_{minor}", f"UNITY_{major}_{minor}_{patch}"]
    for y, ms in ((5, range(3, 7)), (2017, range(1, 5)), (2018, range(1, 5)), (2019, range(1, 5)),
                  (2020, range(1, 4)), (2021, range(1, 4)), (2022, range(1, 4)), (2023, range(1, 4))):
        defines += [f"UNITY_{y}_{m}_OR_NEWER" for m in ms]
    defines += [f"UNITY_6000_{m}_OR_NEWER" for m in range(0, minor + 1)]
    defines += ["UNITY_ANDROID", "PLATFORM_ANDROID", "UNITY_ANDROID_API", "UNITY_ASSERTIONS", "ENABLE_INPUT_SYSTEM",
                "NET_STANDARD_2_0", "NET_STANDARD_2_1", "NET_STANDARD", "NETSTANDARD2_1", "NETSTANDARD",
                "CSHARP_7_3_OR_NEWER", "CSHARP_7_OR_LATER", "ENABLE_VR", "ENABLE_AR", "ENABLE_PROFILER",
                "ENABLE_PHYSICS", "ENABLE_UNITYWEBREQUEST", "ENABLE_CLOTH", "UNITY_UGP_API", "PLATFORM_SUPPORTS_MONO"]
    if context == "editor":
        defines += ["UNITY_EDITOR", "UNITY_EDITOR_64", "UNITY_EDITOR_LINUX", "ENABLE_MONO",
                    "ENABLE_UNITY_COLLECTIONS_CHECKS", "DEVELOPMENT_BUILD"]
    else:
        defines += ["ENABLE_IL2CPP", "UNITY_IL2CPP"]
    return defines + list(extra)


@dataclass
class Result:
    name: str
    owner: str
    status: str  # ok, failed, skipped
    detail: str = ""
    errors: List[str] = field(default_factory=list)


class Compiler:
    def __init__(self, editor_data: str, out_dir: str, unity_version: str, extra_defines: Sequence[str], verbose: bool):
        self.data = editor_data
        self.dotnet = os.path.join(editor_data, "NetCoreRuntime", "dotnet")
        self.csc = os.path.join(editor_data, "DotNetSdkRoslyn", "csc.dll")
        self.out_dir = out_dir
        self.unity_version = unity_version
        self.extra_defines = list(extra_defines)
        self.verbose = verbose
        managed = os.path.join(editor_data, "Managed", "UnityEngine")
        self.engine_refs = sorted(os.path.join(managed, f) for f in os.listdir(managed)
                                  if f.endswith(".dll") and f.startswith("UnityEngine"))
        # Real Android player builds compile against the IL2CPP player assemblies when the
        # Android Build Support module is installed; fall back to the editor's engine DLLs.
        player = os.path.join(editor_data, "PlaybackEngines", "AndroidPlayer", "Variations", "il2cpp", "Managed")
        self.player_refs = sorted(os.path.join(player, f) for f in os.listdir(player)
                                  if f.endswith(".dll") and f.startswith("UnityEngine")) if os.path.isdir(player) else []
        self.editor_refs = sorted(os.path.join(managed, f) for f in os.listdir(managed)
                                  if f.endswith(".dll") and f.startswith("UnityEditor"))
        netstd = os.path.join(editor_data, "NetStandard")
        self.bcl_refs = [os.path.join(netstd, "ref", "2.1.0", "netstandard.dll")]
        for sub in ("compat/2.1.0/shims/netstandard", "compat/2.1.0/shims/netfx", "Extensions/2.0.0"):
            folder = os.path.join(netstd, sub)
            if os.path.isdir(folder):
                self.bcl_refs += sorted(os.path.join(folder, f) for f in os.listdir(folder) if f.endswith(".dll"))
        # Editor-only assemblies compile against Unity's .NET Framework 4.8 profile, not .NET Standard 2.1.
        api48 = os.path.join(editor_data, "UnityReferenceAssemblies", "unity-4.8-api")
        core48 = ["mscorlib", "System", "System.Core", "System.Data", "System.Drawing", "System.IO.Compression",
                  "System.IO.Compression.FileSystem", "System.Net.Http", "System.Numerics", "System.Runtime.Serialization",
                  "System.Transactions", "System.Xml", "System.Xml.Linq", "Microsoft.CSharp", "System.ComponentModel.Composition",
                  "System.Configuration", "System.Web", "System.Security", "System.ServiceModel.Internals"]
        self.bcl48_refs = [os.path.join(api48, n + ".dll") for n in core48 if os.path.exists(os.path.join(api48, n + ".dll"))]
        facades = os.path.join(api48, "Facades")
        if os.path.isdir(facades):
            self.bcl48_refs += sorted(os.path.join(facades, f) for f in os.listdir(facades) if f.endswith(".dll"))
        gen = os.path.join(editor_data, "Tools", "Unity.SourceGenerators")
        self.analyzers = sorted(os.path.join(gen, f) for f in os.listdir(gen) if f.endswith(".dll")) if os.path.isdir(gen) else []

    def compile_all(self, assemblies: List[Assembly], dlls: List[Tuple[str, str]], packages: Dict[str, Package], context: str) -> List[Result]:
        out = os.path.join(self.out_dir, context)
        os.makedirs(out, exist_ok=True)
        versions = {name: pkg.version for name, pkg in packages.items()}
        versions["Unity"] = self.unity_version
        base = unity_defines(self.unity_version, context, self.extra_defines)

        precompiled: Dict[str, Tuple[str, bool]] = {}  # file name -> (path, explicit)
        for path, _owner in dlls:
            if not is_managed_dll(path):
                continue
            explicit, editor_ok, android_ok = dll_flags(path)
            if (context == "editor" and not editor_ok) or (context == "player" and not android_ok):
                continue
            precompiled.setdefault(os.path.basename(path), (path, explicit))

        by_name = {a.name: a for a in assemblies}
        by_guid = {a.guid: a for a in assemblies if a.guid}
        results: Dict[str, Result] = {}
        built: Dict[str, str] = {}

        # Unity treats the built-in UnityEngine.UI / UnityEditor.UI assemblies like engine modules:
        # every assembly with engine references gets them implicitly (TextMeshPro relies on this).
        implicit_names = [a.name for a in assemblies
                          if a.owner in BUILTIN_PACKAGES and re.match(r"^Unity(Engine|Editor)\.UI$", a.name)]

        def deps(asm: Assembly) -> List[Assembly]:
            found = []
            if not asm.data.get("noEngineReferences") and asm.name not in implicit_names:
                for name in implicit_names:
                    if name.startswith("UnityEditor") and context != "editor":
                        continue
                    if by_name[name].name != asm.name and asm.name not in _ui_deps(by_name, name):
                        found.append(by_name[name])
            for ref in asm.data.get("references", []) or []:
                target = by_guid.get(ref[5:]) if ref.startswith("GUID:") else by_name.get(ref)
                if target:
                    found.append(target)
            return found

        # Decide what takes part in this context.
        active: Dict[str, Tuple[Assembly, List[str]]] = {}
        for asm in assemblies:
            if not asm.compatible(context):
                results[asm.name] = Result(asm.name, asm.owner, "skipped", f"not built for {context}")
                continue
            defines = list(base) + asm.rsp_defines
            for vd in asm.data.get("versionDefines", []) or []:
                have = versions.get(vd.get("name", ""))
                if have is not None and version_matches(have, vd.get("expression", "")):
                    defines.append(vd["define"])
            constraints = asm.data.get("defineConstraints", []) or []
            unmet = [c for c in constraints if not _constraint_met(c, defines)]
            if unmet:
                results[asm.name] = Result(asm.name, asm.owner, "skipped", "define constraints not met: " + ", ".join(unmet))
                continue
            if not asm.sources:
                results[asm.name] = Result(asm.name, asm.owner, "skipped", "no sources")
                continue
            active[asm.name] = (asm, defines)

        # Compile in waves: an assembly is ready once its active dependencies are done.
        pending = dict(active)
        with concurrent.futures.ThreadPoolExecutor(max_workers=max(1, (os.cpu_count() or 2))) as pool:
            while pending:
                ready = [n for n, (a, _) in pending.items()
                         if all(d.name not in pending for d in deps(a) if d.name in active)]
                if not ready:  # cycle: compile the rest anyway
                    ready = list(pending)
                futures = {}
                for name in ready:
                    asm, defines = pending.pop(name)
                    refs = [built[d.name] for d in deps(asm) if d.name in built]
                    futures[pool.submit(self._compile_one, asm, defines, refs, precompiled, context, out)] = name
                for fut in concurrent.futures.as_completed(futures):
                    name = futures[fut]
                    result, dll = fut.result()
                    results[name] = result
                    if dll:
                        built[name] = dll
        return [results[a.name] for a in assemblies if a.name in results]

    def _compile_one(self, asm: Assembly, defines: List[str], refs: List[str], precompiled: Dict[str, Tuple[str, bool]],
                     context: str, out: str) -> Tuple[Result, Optional[str]]:
        data = asm.data
        use48 = context == "editor" and asm.editor_only() and self.bcl48_refs
        references: List[str] = list(self.bcl48_refs if use48 else self.bcl_refs)
        if not data.get("noEngineReferences"):
            references += self.player_refs if (context == "player" and self.player_refs) else self.engine_refs
            if context == "editor":
                references += self.editor_refs
        references += refs
        override = data.get("overrideReferences", False)
        listed = set(data.get("precompiledReferences", []) or [])
        for fname, (path, explicit) in precompiled.items():
            if fname in listed or (not override and not explicit):
                references.append(path)

        dll = os.path.join(out, asm.name + ".dll")
        rsp_lines = ["-nologo", "-target:library", "-langversion:9.0", "-nostdlib", "-noconfig", "-deterministic",
                     "-debug-", "-warn:0", "-nowarn:1701,1702,0618,0649,0169,0414,8632,8669",
                     f"-out:{dll}", "-define:" + ";".join(sorted(set(defines)))]
        if data.get("allowUnsafeCode"):
            rsp_lines.append("-unsafe")
        rsp_lines += [f"-reference:{r}" for r in dict.fromkeys(references)]
        rsp_lines += [f"-analyzer:{a}" for a in self.analyzers]
        rsp_lines += sorted(asm.sources)
        with tempfile.NamedTemporaryFile("w", suffix=".rsp", delete=False, encoding="utf-8") as handle:
            handle.write("\n".join(f'"{line}"' if " " in line and not line.startswith("-define") else line
                                   for line in rsp_lines if line))
            rsp = handle.name
        try:
            proc = subprocess.run([self.dotnet, self.csc, "@" + rsp], capture_output=True, text=True)
        finally:
            os.unlink(rsp)
        output = (proc.stdout + proc.stderr).splitlines()
        errors = [line for line in output if ": error " in line]
        if proc.returncode == 0 and os.path.exists(dll):
            return Result(asm.name, asm.owner, "ok", f"{len(asm.sources)} files"), dll
        if not errors:
            errors = output[-10:]
        return Result(asm.name, asm.owner, "failed", f"{len(errors)} error(s)", errors), None


BUILTIN_PACKAGES = {"com.unity.ugui"}


def _ui_deps(by_name: Dict[str, "Assembly"], name: str) -> Set[str]:
    """Names UnityEngine.UI / UnityEditor.UI themselves depend on (avoid implicit-reference cycles)."""
    seen: Set[str] = set()
    stack = [name]
    while stack:
        current = by_name.get(stack.pop())
        if current is None:
            continue
        for ref in current.data.get("references", []) or []:
            if ref not in seen:
                seen.add(ref)
                stack.append(ref)
    return seen


def _constraint_met(constraint: str, defines: Sequence[str]) -> bool:
    # Constraints can be "A", "!A" or "A || B".
    for option in constraint.split("||"):
        option = option.strip()
        if option.startswith("!"):
            if option[1:] not in defines:
                return True
        elif option in defines:
            return True
    return False


# --------------------------------------------------------------------------- main


def main(argv: Optional[Sequence[str]] = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--unity", required=True, help="Unity 'Editor' folder (contains Data/)")
    parser.add_argument("--project", default=REPO)
    parser.add_argument("--cache", default=DEFAULT_CACHE)
    parser.add_argument("--context", choices=["editor", "player", "both"], default="both")
    parser.add_argument("--define", action="append", default=[], help="extra scripting define symbol")
    parser.add_argument("--show-third-party-errors", action="store_true")
    parser.add_argument("--verbose", action="store_true")
    args = parser.parse_args(argv)

    data = os.path.join(args.unity, "Data")
    if not os.path.isdir(data):
        print(f"error: {data} not found", file=sys.stderr)
        return 2
    with open(os.path.join(args.project, "ProjectSettings", "ProjectVersion.txt"), encoding="utf-8") as handle:
        unity_version = re.search(r"m_EditorVersion:\s*(\S+)", handle.read()).group(1)
    with open(os.path.join(args.project, "Packages", "manifest.json"), encoding="utf-8") as handle:
        manifest = json.load(handle)

    print(f"Resolving packages for Unity {unity_version}...")
    resolver = PackageResolver(data, args.cache, manifest.get("scopedRegistries", []), args.verbose)
    packages = resolver.resolve(manifest.get("dependencies", {}))
    print(f"  {len(packages)} packages")

    roots = [(pkg.path, name) for name, pkg in sorted(packages.items())]
    roots.append((os.path.join(args.project, "Assets"), "Assets"))
    assemblies, dlls, loose = discover(roots)
    if loose:
        print(f"warning: {len(loose)} script(s) in Assets/ are outside any asmdef and were not checked:")
        for path in loose[:10]:
            print("   ", os.path.relpath(path, args.project))

    contexts = ["editor", "player"] if args.context == "both" else [args.context]
    compiler = Compiler(data, os.path.join(args.cache, "build"), unity_version, args.define, args.verbose)
    failed_project = False
    for context in contexts:
        print(f"\n== {context} context ==")
        results = compiler.compile_all(assemblies, dlls, packages, context)
        ok = [r for r in results if r.status == "ok"]
        third_failed = [r for r in results if r.status == "failed" and r.owner != "Assets"]
        print(f"  third-party: {sum(1 for r in ok if r.owner != 'Assets')} compiled, {len(third_failed)} failed")
        for r in third_failed:
            print(f"    ~ {r.name} ({r.owner}): {r.detail}")
            if args.show_third_party_errors or args.verbose:
                for e in r.errors[:5]:
                    print("        " + e)
        for r in results:
            if r.owner != "Assets":
                continue
            mark = {"ok": "OK  ", "failed": "FAIL", "skipped": "SKIP"}[r.status]
            print(f"  {mark} {r.name}: {r.detail}")
            for e in r.errors[:30]:
                print("        " + e.replace(args.project + os.sep, ""))
            if r.status == "failed" or (r.status == "skipped" and "constraints" in r.detail):
                failed_project = True
    return 1 if failed_project else 0


if __name__ == "__main__":
    sys.exit(main())
