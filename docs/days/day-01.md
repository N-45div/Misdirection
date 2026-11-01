# Day 1 · Mon Nov 2 · Gate 1: pipeline end to end

**Exit criterion:** a signed APK built from this repo is live on the **Competition** channel, and the **widest usable crowd arc** is measured on a real headset.

Already in the repo for today:

| What | Where |
|---|---|
| Look source (eyes when available, head otherwise) | `Assets/Misdirection/Scripts/Runtime/Look/LookSource.cs` |
| Tracking spike: guided in-headset protocol, CSV logger, on-device summary | `Assets/Misdirection/Scripts/Runtime/Spike/` |
| Signed APK builds (menu + command line), SDK v207 lock check | `Assets/Misdirection/Scripts/Editor/Build/MisdirectionBuild.cs` |
| One-click spike setup + scene checker | `Assets/Misdirection/Scripts/Editor/Setup/SpikeSceneSetup.cs` |
| Build / install / upload / pull-and-analyse scripts | `tools/*.sh`, `tools/analyze_tracking_spike.py` |

> Meta XR SDK v207 shipped after these scripts' API references were last checked against docs. The code only uses long-standing Core SDK APIs (`OVRHand`, `OVRCameraRig`, `OVREyeGaze`, `OVRPlugin`), but if anything fails to compile, see **Troubleshooting** at the bottom.

---

## A. Create the Unity project in this repo · INT · ~45 min

1. Unity Hub → **New project** → Unity **6000.0.66f2+** → template **Universal 3D** → create it in a scratch folder **outside** the repo (e.g. `~/tmp/MisdirectionTemp`).
2. Close Unity. Copy the temp project's `Assets/`, `Packages/` and `ProjectSettings/` into the repo root, merging `Assets/` (our `Assets/Misdirection` stays). Delete the template's `Assets/TutorialInfo` and `Assets/Readme.asset`. Don't copy `Library/`.
3. Add the Meta packages **before** opening. Merge [docs/setup/manifest-snippet.json](../setup/manifest-snippet.json) into `Packages/manifest.json` (scoped registry + `com.meta.xr.sdk.all` 207). If `207.0.0` doesn't resolve, use the newest **207.x** that Package Manager lists. Alternative: Asset Store → *Meta XR All-in-One SDK* → Add to My Assets → Package Manager → My Assets → install 207.x.
4. Unity Hub → **Add → Add project from disk** → the repo root. Open it.
5. **File → Build Profiles → Android → Switch Platform.**
6. **Meta → Tools → Project Setup Tool** → Android tab → **Fix All**, then **Apply All**. Repeat until clean.
7. **Window → TextMeshPro → Import TMP Essential Resources** (the spike's text is invisible without it).
8. Find **OVRManager** on the camera rig (after step B) → *Quest Features*:
   - Hand Tracking Support: **Hands Only**
   - Hand tracking frequency: leave the default. Don't turn on high frequency / Fast Motion Mode
   - Passthrough Support: **Required**
   - Anchor Support and Scene Support: **Enabled** (MRUK needs them)
   - Eye Tracking Support: **Supported** (eyes on Quest Pro / VR Glasses; harmless on Quest 3)
9. The Console should **not** show `[Misdirection] Meta XR Core SDK ... is not installed`. The **Misdirection** menu should show *Build* and *Day 1*.

## B. Scenes · INT · ~45 min

1. **Main scene (from the MR Tabletop sample).** Package Manager → *Meta XR Interaction SDK* → **Samples** → import the **MR Tabletop** sample (the name may differ slightly in v207). Open its scene → **File → Save As** `Assets/Misdirection/Scenes/Main.unity`. Add the **MR Utility Kit** building block if the sample doesn't already have MRUK. Commit the imported sample. (Open item #8: write in the Devpost text that we started from Meta's sample.)
2. **Spike scene.** File → New Scene (Basic) → Save As `Assets/Misdirection/Scenes/Spike.unity`. **Meta → Tools → Building Blocks** → add **Camera Rig**, **Passthrough**, **Hand Tracking**, **Eye Gaze** (the v207 Eye Gaze Interaction block). Keep exactly **one** OVRCameraRig.
3. **Misdirection → Day 1 → Add Tracking Spike to Open Scene.**
4. **Misdirection → Day 1 → Check Scene and Project.** Fix every `FIX` line.
5. Build Profiles scene list: **Spike** first, **Main** second. Save both scenes.
6. Optional Editor check: in the Meta XR Simulator with simulated hands, pinch both hands for 1 s (or right-click the *Tracking Spike* component → **Start protocol now**). The protocol should step through its conditions.

## C. Signing key · SYS · ~20 min

Create the key once and keep it outside the repo:

```bash
mkdir -p ~/misdirection-keys
```

```bash
keytool -genkeypair -v -keystore ~/misdirection-keys/misdirection.keystore -alias misdirection -keyalg RSA -keysize 2048 -validity 10000
```

Copy [docs/setup/android-signing.properties.example](../setup/android-signing.properties.example) to `secrets/android-signing.properties` and fill it in (`secrets/` is git-ignored). **Store the keystore and both passwords in the team password manager now.** Losing the key means you can never update the app.

## D. Build and install · SYS · ~30 min

Close the Editor, then:

```bash
tools/build_apk.sh
```

```bash
tools/install_quest.sh
```

Or, in the Editor: **Misdirection → Build → Signed Release APK + Install on Quest**. With the metavr MCP you can ask the agent to install and launch `Builds/Android/latest.txt`'s APK and tail the logs.

Smoke test with the **controllers switched off and out of reach**: passthrough shows, both hands render, the intro panel follows your head, and a two-hand pinch starts the protocol.

## E. Upload to "Competition" · PRO · ~30 min

1. Copy [docs/setup/meta.env.example](../setup/meta.env.example) to `secrets/meta.env` and add the App ID and App Secret (Dashboard → your app → API).
2. Upload:
   ```bash
   tools/upload_competition.sh "Day 1 pipeline proof"
   ```
   Or upload the APK from `Builds/Android/` through the Dashboard web page into the **Competition** channel.
3. Dashboard → Release Channels → Competition → add a second account and create the **Invite URL**. Install from the Store on that account or headset, then launch it hands-only.
4. Write the Invite URL and anything that blocked you (data-use forms, age rating, package name) into [OPEN_ITEMS.md](../OPEN_ITEMS.md) #11.

## F. Tracking spike · INT + 2 testers · ~45 min

The question: *how far can the player turn their head toward a spectator before the secret hand, held low at the near table edge, drops out of reliable tracking?*

**Setup.** Seated at a real table, normal room light, sleeves up, no rings, controllers off.

**Each run takes about 4 minutes.**

1. Face the far side of the table (where the crowd would stand). Pinch both hands, palms down, for 1 s.
2. *Secret hand LEFT*: hold a pinch with the left hand low at the near table edge, a little left of centre. Turn your **head** to face the ball each time it moves (0°, then 20/35/50° away from the secret hand, then 20/35/50° toward it). Keep the secret hand still. Each position settles for 2 s and records for 6 s, with beeps.
3. *Secret hand RIGHT*: same again.
4. The headset shows a summary: lost move windows per angle, and the **widest usable crowd arc**.

**Runs to collect:** at least **2 testers × 2 runs** on Quest 3, plus Quest 3S if available (the 3S result decides, since it's the weaker device). If time allows, add one dim-light run.

**Pull and analyse** (per tester, headset connected):

```bash
tools/pull_spike_logs.sh alice
```

Then copy the numbers into [docs/results/day-01-tracking-spike.md](../results/day-01-tracking-spike.md) and commit it. Raw CSVs stay in the git-ignored `spike_logs/`.

**Decision rule** (a move window is 1.5 s; it is "lost" if the secret hand is below high confidence for 150 ms or more inside it):

| Widest usable arc (weakest device) | Crowd layout |
|---|---|
| ±35° or more | Standard arc, spectators within ±30° |
| ±20° | Shallow arc, spectators within ±20°; secret moves low and centred |
| 0° only | **At risk.** More than 1 move in 10 lost at any turn. Flag for the Day 4 pivot decision |
| none | Setup problem. Fix lighting and hand height and re-run before concluding |

**Open item #14 (SYS).** On Quest 3, check the CSV `look` column (expect `head`). Also watch what the Eye Gaze building block's interactor does with no eye tracking: does it fall back to head direction, or to hand rays? Record the answer in OPEN_ITEMS.md.

## G. Gate 1 checklist

- [ ] Unity project, Packages and ProjectSettings committed (with `.meta` files; no `Library/`)
- [ ] Main (from MR Tabletop) and Spike scenes committed
- [ ] Signed release APK built from the repo (version code: ______)
- [ ] Installed and played hands-only with controllers off
- [ ] Live on **Competition**; installed via Invite URL on a second account
- [ ] Spike: ≥2 testers × 2 runs on Quest 3 (+ 3S if available)
- [ ] Widest usable crowd arc recorded: ±____°
- [ ] Open items #4, #6, #7, #8, #9, #11, #14 answered or escalated
- [ ] Trailer storyboard in TRAILER.md (ART)

## Troubleshooting

- **Misdirection → Day 1 menu missing, or no spike component.** Our runtime and setup code only compile when `com.meta.xr.sdk.core` **≥ 207.0.0** is installed (assembly version define). Check Package Manager. If the SDK is installed but you see "type or namespace OVRHand could not be found", open `Assets/Misdirection/Scripts/Runtime/Misdirection.Runtime.asmdef` and re-add the references to the Meta XR Core assembly (`Oculus.VR`) and `Unity.TextMeshPro`.
- **Text is invisible.** Import TMP Essential Resources (A.7).
- **"Could not find both OVRHand components".** Select *Misdirection Tracking Spike* and drag the left and right OVRHand objects into the fields.
- **Analysis warns "secret-hand position never moves".** The rig's hand anchors aren't following tracked hands. Confidence data is still valid; tell SYS.
- **`adb pull` permission denied.** Use Meta Quest Developer Hub's file browser on `Android/data/com.n45div.misdirection/files/spike`.
- **Upload rejected for version code.** Every upload needs a higher version code. The build script uses minutes since Sep 24, so rebuild rather than re-uploading an old APK.
