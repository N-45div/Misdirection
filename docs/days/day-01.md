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

> **Updated Nov 1 (evening).** The Unity project now exists in the repo (built from Unity's own Universal 3D template, Meta XR SDK **v207.0.1**). All our scripts compile against the real Unity 6000.0.66f2 + Meta v207 code (`tools/unity_compile_check.py`). Two changes from the original plan: the **MR Tabletop sample does not exist** in the v207.0.1 packages, so Main is built from Building Blocks instead; and the ISDK Gaze block falls back to **hand rays**, not head direction, on Quest 3 (open item #14), which is why we use our own `LookSource`. No Unity licence was available in the cloud, so the bootstrap below has compiled but not yet run. See [BUILD_WITHOUT_A_HEADSET.md](../BUILD_WITHOUT_A_HEADSET.md).

---

## A. Open the project · INT · ~30 min (mostly package import)

1. Install Unity **6000.0.66f2** exactly (Unity Hub → Installs → Install Editor → Archive, or `unityhub://6000.0.66f2/b20bc5da3050`) with **Android Build Support** (OpenJDK, Android SDK & NDK).
2. Unity Hub → **Add → Add project from disk** → the repo root → open. The first import downloads Meta v207.0.1 and the Unity packages listed in `Packages/manifest.json` (5–15 minutes).
3. The **Misdirection** menu should show *Setup*, *Build* and *Day 1*. The Console must not show `[Misdirection] Meta XR Core SDK ... is not installed`.

## B. Bootstrap: settings + scenes in one click · INT · ~10 min

1. **Misdirection → Setup → Run Project Bootstrap.** Watch the Console for `[Misdirection] Bootstrap 1/7 … 7/7` and then `Bootstrap done`. It:
   - switches to Android
   - sets Meta config: **Hands Only**, passthrough, anchors, scene, eye tracking
   - imports TMP essentials
   - creates `Assets/Misdirection/Scenes/Spike.unity` (Passthrough + Hand Tracking blocks + tracking spike)
   - creates `Main.unity` (the same blocks + MR Utility Kit)
   - sets the build list
   - runs Meta's **Project Setup Tool → Fix All** twice
2. Open **Meta → Tools → Project Setup Tool**. Anything still red, fix it by hand and note it here so the bootstrap can learn it.
3. Open `Spike.unity` → **Misdirection → Day 1 → Check Scene and Project**. Every line should be `OK`.
4. **Commit everything the bootstrap created**: scenes, `Assets/XR`, `Assets/TextMesh Pro`, `Assets/Resources/OculusProjectConfig.asset`, `ProjectSettings`, `Packages/packages-lock.json`.
5. Optional Editor check (Windows/macOS): in the Meta XR Simulator with simulated hands, pinch both hands for 1 s (or right-click *Tracking Spike* → **Start protocol now**).

If the bootstrap fails, do the same steps by hand: Building Blocks (Passthrough, Hand Tracking), then *Day 1 → Add Tracking Spike to Open Scene*, then the Project Setup Tool. Send the Console error so the bootstrap can be fixed.

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

**Open item #14 (SYS).** Answered from the v207 source: the ISDK *Gaze Interaction* block falls back to **hand rays** without eye tracking. On device, just confirm the CSV `look` column says `head` on Quest 3.

## G. Gate 1 checklist

- [x] Unity project, Packages and ProjectSettings committed (done in the cloud on Nov 1)
- [ ] Bootstrap run; Spike and Main scenes + generated settings committed
- [ ] Signed release APK built from the repo (version code: ______)
- [ ] Installed and played hands-only with controllers off
- [ ] Live on **Competition**; installed via Invite URL on a second account
- [ ] Spike: ≥2 testers × 2 runs on Quest 3 (+ 3S if available)
- [ ] Widest usable crowd arc recorded: ±____°
- [ ] Open items #4, #6, #7, #8, #9, #11, #14 answered or escalated
- [ ] Trailer storyboard in TRAILER.md (ART)

## Troubleshooting

- **Misdirection → Day 1 menu missing, or no spike component.** Our runtime and setup code only compile when `com.meta.xr.sdk.core` **≥ 207.0.0** is installed (assembly version define). Check Package Manager. Anyone can reproduce a compile error without Unity: `python3 tools/unity_compile_check.py --unity <Editor folder>`.
- **Text is invisible.** Import TMP Essential Resources (A.7).
- **"Could not find both OVRHand components".** Select *Misdirection Tracking Spike* and drag the left and right OVRHand objects into the fields.
- **Analysis warns "secret-hand position never moves".** The rig's hand anchors aren't following tracked hands. Confidence data is still valid; tell SYS.
- **`adb pull` permission denied.** Use Meta Quest Developer Hub's file browser on `Android/data/com.n45div.misdirection/files/spike`.
- **Upload rejected for version code.** Every upload needs a higher version code. The build script uses minutes since Sep 24, so rebuild rather than re-uploading an old APK.
