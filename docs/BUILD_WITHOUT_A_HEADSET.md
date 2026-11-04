# Building and checking without Unity Hub or a headset

What a cloud session, a CI runner or a coding agent can do for this project, and what still needs a person.

| Job | Needs | How |
|---|---|---|
| Compile every script against the real Unity 6000.0.66f2 + Meta XR SDK v207 | Nothing (no licence) | `tools/unity_compile_check.py`, or the `unity-compile` CI job |
| Spike scoring and analysis tests (C# under Mono + Python) | Nothing | `python3 -m unittest discover -s tools/tests -t .` |
| Configure the project, create the scenes, build the APK | A **Unity licence** | `tools/unity_batch.sh all`, or the `build-apk` CI workflow |
| Upload the APK to the Competition channel | Meta app secret | `tools/upload_competition.sh`, or the Developer Dashboard |
| Run the tracking spike, play the game | **A Quest** | No Quest emulator exists. Meta XR Simulator is Windows/macOS only and runs inside the Editor |

## 1. Compile check (no licence)

Unity's editor runs only with a licence, but compiling doesn't need one. `unity_compile_check.py` resolves `Packages/manifest.json` like Unity's Package Manager, finds every assembly definition, and compiles everything in dependency order with the editor's own Roslyn compiler. It runs twice: once as the Editor sees it, once as the Android IL2CPP player does.

```bash
python3 tools/install_unity.py --dest ~/unity --no-android-sdk
```

```bash
python3 tools/unity_compile_check.py --unity ~/unity/Editor
```

It exits non-zero if anything under `Assets/` fails. A handful of third-party test or codegen assemblies fail because this script imitates Unity imperfectly; those are reported but never fail the run. CI runs the same check inside Unity's official Docker image on every push (`.github/workflows/tools-tests.yml`, job `unity-compile`).

Result on Nov 1: all Meta assemblies (Core, Core Editor, Interaction, MRUK, Platform) and all three of ours compile in both contexts.

## 2. Bootstrap and build (needs a licence)

`MisdirectionBootstrap` (`Assets/Misdirection/Scripts/Editor/Setup/`) does the Day 1 Editor clicking in code:

1. Switches to Android
2. Sets Meta project config: Hands Only, passthrough, anchors, scene, eye tracking
3. Imports TextMesh Pro essentials
4. Builds `Spike.unity` from the Passthrough + Hand Tracking building blocks, plus the tracking spike
5. Builds `Main.unity` from the same blocks plus the MR Utility Kit block
6. Builds `Show.unity` (Trick 1 + crowd) and sets the build list: Show, Main, Spike
7. Runs Meta's Project Setup Tool **Fix All** twice

Then `MisdirectionBuild` makes the APK. The bootstrap compiles against the real SDK but has **not been run yet**, because no licence was available on Nov 1. Expect one or two fixes on its first real run.

### Get a licence file (Unity Personal is free)

1. Install Unity Hub on your own computer, sign in, and activate a Personal licence.
2. Find the licence file:
   - Windows: `C:\ProgramData\Unity\Unity_lic.ulf`
   - macOS: `/Library/Application Support/Unity/Unity_lic.ulf`
   - Linux: `~/.local/share/unity3d/Unity/Unity_lic.ulf`
3. Store it as a secret. **Never paste it into chat or commit it.**

### Option 0: a Windows PC (fastest, used from Nov 4)

No admin rights needed. In PowerShell, from the repo:

```powershell
winget install --id Unity.UnityHub --exact     # per-user app; open it once and sign in (renews the licence)
python tools/install_unity.py --dest C:\Unity\6000.0.66f2
```

Then, in Git Bash:

```bash
tools/unity_batch.sh all    # bootstrap (scenes + settings) and a debug-signed APK in Builds/Android
```

`install_unity.py` runs Unity's own installers "as invoker", so there's no UAC prompt as long as the folder is yours (keep spaces out of it). `unity_batch.sh` finds `C:\Unity\<version>` and the licence that Unity Hub keeps in `C:\ProgramData\Unity` by itself. To open the project in the Editor, Unity Hub → Installs → Locate → `C:\Unity\6000.0.66f2\Editor\Unity.exe`.

### Option A: GitHub Actions (recommended for the team)

Repository → Settings → Secrets and variables → Actions → New repository secret:

| Secret | Value |
|---|---|
| `UNITY_LICENSE` | Full contents of `Unity_lic.ulf` |
| `UNITY_EMAIL`, `UNITY_PASSWORD` | Optional. Unity account, if the licence file alone is rejected |
| `ANDROID_KEYSTORE_BASE64` | Optional. `base64 -w0 misdirection.keystore` |
| `ANDROID_KEYSTORE_PASS`, `ANDROID_KEY_ALIAS`, `ANDROID_KEY_PASS` | Optional, with the keystore |

Then go to Actions → **build-apk** → Run workflow. The artifact contains:

- The APK (debug-signed if there's no keystore; it still installs on a developer-mode Quest)
- Unity logs
- `generated-project-files.tgz`, the scenes and settings the bootstrap created. Commit these once so everyone opens the same project.

### Option B: this cloud environment

In the cloud environment's settings (environment menu → Edit), add an environment variable **`UNITY_LICENSE_B64`** containing `base64 -w0 Unity_lic.ulf`. A new session picks it up. Then an agent can run:

```bash
python3 tools/install_unity.py --dest ~/unity
```

```bash
UNITY_HOME=~/unity tools/unity_batch.sh all
```

Personal licences expire (the Feb 2026 one on the dev PC had lapsed by November) and are renewed by signing in to Unity Hub; re-upload the file after a renewal. They are also tied to a machine. If Unity rejects yours on a CI runner or cloud machine, use `UNITY_EMAIL` + `UNITY_PASSWORD` (plus `UNITY_SERIAL` for Plus/Pro) instead. Unity's own Docker images and GameCI document the current activation rules.

## 3. What still needs a person with a Quest

- Running the tracking spike (Gate 1's crowd-arc measurement).
- Every hands-on check: hand feel, fun gate, unfamiliar rooms, judge install with controllers off.
- Installing a CI-built APK: download the artifact, then `tools/install_quest.sh path/to/Misdirection.apk`.
