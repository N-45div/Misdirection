# Day 12 · Fri Nov 13 · Glasses readiness + performance

**Exit criterion:** at least **72 fps on Quest 3S** (the rules ask for 60), and the essential UI **inside the simulated glasses field of view**.
With no Quest ([NO_HEADSET.md](../NO_HEADSET.md)) the frame rate can't be measured, so today's work makes the build cheap to draw *by construction*, adds numbers we can compare on the PC, and leaves a frame-time log ready for the first hour on a real headset.

Written on Nov 13. 28 new unit tests (122 core tests in total, all passing). Compiles against Unity 6000.0.66f2 + Meta v207 for the Editor and the Android player.

**Not run yet:** the Editor, the simulator and the smoke test. Nothing below has been seen on a screen. The first job after merging is the setup step and the checks in this file.

## What changed

### 1. Drawing cost

**A real bug found on the way.** Spectators, coin, cup, mat and stars are built from Unity primitives. In the Editor a primitive gets URP's default material, so everything looked right. In a **player build** URP has no default material, and a primitive falls back to the old built-in Standard material, which URP does not draw: **magenta in a development build, invisible in a release build**. Nobody saw it because nobody has run an APK. Every part now gets a material from one cache whose shaders are guaranteed to be in the build.

| | Before | Now |
|---|---|---|
| Materials for a five-person crowd | one per part (`renderer.material.color`), about 100 | one per colour, about 25, shared (`SharedMaterials`) |
| Opaque parts | default material (missing on device) | `Universal Render Pipeline/Lit`, SRP Batcher: one batch whatever the colours |
| Transparent parts (cones, outline, ghost hands, ring) | a new `Sprites/Default` material each | one shared `Sprites/Default` material; cones tint it per renderer |
| Trick prompt text | rebuilt every frame (several strings) | rebuilt only when a prompt, tip, focus or whole percent changes, and not while hidden |
| Per-frame garbage in crowd / ghost hands | temporary arrays and enumerators | none of ours |

**Why these shaders ship:**

- `Universal Render Pipeline/Lit`: the bootstrap saves a template material at `Assets/Misdirection/Resources/Misdirection/SharedLit.mat` (a copy of URP's default Lit material, so the look is unchanged). Unity puts everything under a `Resources` folder into every build, with its shader. A build pre-process step does the same check, so a build made without the bootstrap is safe too. If the template is ever missing on device, the code falls back to `Shader.Find`, then to an unlit colour, and logs a warning.
- `Sprites/Default`: built in, and on the project's Always Included Shaders list (the bootstrap checks). It keeps the cones' soft edge (vertex colours), which URP's unlit shader can't do.

**Project settings**, set by the bootstrap and again before every Android build (`PerformanceSetup.EnsureQuestRenderSettings`). Each value is logged as `already X` or `A -> B`:

| Setting (Android URP asset, `Mobile_RPAsset`) | Value | Was |
|---|---|---|
| MSAA | 4x | 4x (Meta's Fix All sets it) |
| HDR | off | off |
| Render scale | **1.0** | 0.8 (blurry text; dynamic resolution still adapts on device) |
| Main-light shadow maps | **off** | on (the light casts none, spectators opt out) |
| Additional lights | **off** | per pixel |
| Additional-light shadows | off | off |
| SRP Batcher | on | on |
| OpenXR Android render mode | single pass instanced (multiview) | already |

Meta's Fix All keeps MSAA, HDR, depth/opaque textures and dynamic resolution right; this step agrees with it and fills in the rest.

**On the headset** (`QuestPerformance`, one object per scene, added by the bootstrap; added at runtime if a scene has none): at start it asks for **foveated rendering High, dynamic**, **CPU and GPU level SustainedHigh**, and a **72 Hz** display. These are runtime requests through `OVRManager`, not saved settings. In the Editor it only logs what it would ask for.

**Frame-time log** (`PerfLogger`, development builds and the Editor only): every 5 s, one line:

```
[Misdirection] Perf: 72.0 fps, frame 13.9 ms avg, 15.2 ms worst, over 5.0 s (360 frames), app CPU time ..., app GPU time ..., GPU load ..., 0 dropped, display 72 Hz
```

The part after the frame count comes from the headset (OVRPlugin perf metrics) and is missing in the Editor. On a headset, `adb logcat -s Unity | grep "Perf:"` shows it.

### 2. FoV-aware layout

Meta VR Glasses see about **70° × 66°**; Quest 3 about 110°. Meta's own simulation in the v207 SDK masks to **35° left and right, 25° up, 41° down**, so the view is not centred: it reaches further down than up. We use those numbers.

- The layout was already narrow by design: crowd within ±20°, prompt and banner straight ahead. Looking at the table or the crowd, everything essential is inside the glasses' view.
- New: essential text **follows when you really look away**. Each element has a home. While the home is inside a comfortable zone around the head direction (**50° wide, 20° up, 33° down**), or within 4° of it, nothing moves. After **0.6 s** further out than that, the element eases (about half a second) to the nearest point inside the zone, then stays fixed in the room until the view moves on by more than 4°. When its home is comfortable again it eases back. It never jumps, and a still or trembling head means still text.
- What follows: the trick prompt, the show banner and star coins (as one set, so they keep their arrangement), the floating reaction above a spectator, and the ghost hands' caption (its zone is 4° larger so it settles clear of the others). Following text never sinks below 6 cm above the table.
- What doesn't: suspicion markers above heads (they belong to the person), ghost hands, the cones.
- With these numbers the prompt stays home through a whole trick: looking at the mat, level, or into the eyes of a tall spectator at the edge of the arc. It moves when you look above the crowd or turn well away.

Rules and tests: `Core/View/ViewCone.cs`, `Core/View/ComfortFollow.cs`, `tools/tests/csharp/ViewTests.cs`. Scene side: `Runtime/Util/ComfortAnchor.cs`.

### 3. Gaze through Meta's Interaction SDK (ISDK v207)

Until today "which spectator am I looking at" was our own angle test. It now goes through ISDK's gaze interaction:

| Piece | What it is |
|---|---|
| Spectator face | an ISDK `GazeInteractable` (a 5 mm target at the midpoint of the eyes, on a `ColliderSurface`) |
| Interactor | one ISDK `GazeInteractor`, hit-testing with ISDK's `GazeConecaster` (6° cone, so a face is a 12° target; Meta's minimum is 3°) |
| Gaze source | `IsdkSpectatorGaze` implements ISDK's `IGaze` from `LookSource`: **head direction on Quest 3 / 3S**, eyes on devices that track them |
| Result | the hovered spectator goes to `CrowdModel.ReportHover`; eye contact, misdirection and the pluck work from it as before |
| Look + pinch | when a coin is plucked from behind an ear, the trick tells ISDK, which selects and releases that face |

- **Not** the Eye Gaze building block: on Quest 3 it falls back to hand rays ([OPEN_ITEMS](../OPEN_ITEMS.md) #14), which would break "where you look, they look".
- **Same feel:** ISDK's own dwell is off (the game has its 0.3 s eye-contact dwell and the pluck's); the crowd model keeps a face until the look drifts 9° away. A unit test sweeps the look across the crowd for 4,000 frames and checks that "6° cone-cast + hold" picks the same spectator as the old test on every frame.
- **Fallback:** if ISDK throws, or *Use Isdk Gaze* is unticked on the Crowd, the old test decides. The log and the smoke report say which is in use (`gaze_source`).
- **Why the pinch isn't an ISDK selector all the time:** the coin is held by a pinch for most of a trick, and a selecting ISDK interactor stops looking for new targets. That would freeze who you are looking at.
- ISDK marks its gaze classes `[Experimental]` in v207. We are locked to v207, so that is fine for the competition.

### Code

- `Runtime/Util/SharedMaterials.cs`: the material cache (use it for any new runtime-built visuals)
- `Runtime/Perf/QuestPerformance.cs`, `Runtime/Perf/PerfLogger.cs`
- `Runtime/Util/ComfortAnchor.cs` (+ `UiAnchors`: where the prompt, banner and mat sit)
- `Runtime/Audience/IsdkSpectatorGaze.cs`
- `Core/View/ViewCone.cs`, `Core/View/ComfortFollow.cs`; `CrowdModel.ReportHover`
- `Editor/Setup/PerformanceSetup.cs`, `Editor/Setup/GazeSetup.cs`; the smoke test's new report fields
- Tests: `tools/tests/csharp/ViewTests.cs`, and `ReportedHoverTests` in `CrowdTests.cs`

## Setup (once, after merging)

1. Open the project and run **Misdirection → Setup → Run Project Bootstrap**. New today, each logged as `[Misdirection] Performance: ...` or `[Misdirection] Gaze: ...`:
   - saves `Assets/Misdirection/Resources/Misdirection/SharedLit.mat`
   - sets the URP values in the table above
   - adds a `Misdirection Performance` object to Show, Main and Spike
   - adds the ISDK gaze bridge to the crowd in Show and Main
2. **Commit what it changed**: the new `.mat` and its folder metas, `Assets/Settings/Mobile_RPAsset.asset`, the three scenes.
3. By hand instead, in the open scene: **Misdirection → Day 12 →** *Check Shaders That Ship*, *Apply Quest 3 Render Settings*, *Add Headset Performance Settings and Perf Log*, *Add ISDK Gaze (spectator faces)*.

The game also works before step 1: materials find their shader by name in the Editor, and the performance object and the gaze bridge are added at runtime when a scene has none.

## Check in the simulator · ~30 min

1. **Smoke test** (`tools/sim_smoke.sh`). `Builds/Smoke/report.json` has new fields. Write the numbers here after the first run:

   | Field | Meaning | Nov __ |
   |---|---|---|
   | `batches`, `draw_calls`, `set_pass_calls` | the Game view's Stats panel, averaged over the last 5 s | |
   | `triangles`, `vertices` | same | |
   | `renderers`, `materials_in_use` | active renderers, and how many different materials they use | |
   | `shared_materials` | e.g. `21 opaque (Resources/Misdirection/SharedLit), 2 transparent (Sprites/Default)` | |
   | `perf_log` | the last `PerfLogger` window | |
   | `gaze_source` | should read `ISDK GazeInteractor (6 degree cone, Head)` | |
   | `fov_check` | for a 70×66 view from the simulated head: prompt, banner, mat centre and each face, inside or not, with yaw and pitch | |

   To see the difference today's work made, run the smoke test on the commit before Day 12 (`662a65f`, which has none of the new fields: use the Stats panel) and compare. The smoke run plays the tutorial with one spectator; for a full crowd, read the Stats panel during act 3.
2. **Looks the same?** Play Show.unity. Spectators, coin, cup, mat, stars, cones and ghost hands should look as they did on Day 9. If spectators look flat (no shading), the Lit shader was not found: the Console says so.
3. **Glasses field of view, two ways:**
   - In the Meta XR Simulator: **Inputs → Device info → Device → Meta VR Glasses**. The view narrows to the glasses.
   - Or on the Camera Rig's **OVRManager**: tick **Simulate Device Field of View** (v207). It masks Play mode, Link and the simulator to the glasses' view. It is a local preference and does not touch builds. Leave the *Android* one under *Device Simulation* **off**: Meta's SDK says such a build can't be submitted to the Store (the build logs a warning if it is on).
   - Then: look at the table: prompt, mat and crowd all visible. Look at each face: the prompt is still visible below. Look up above the crowd for a second: the prompt rises into view, and goes back when you look down. Turn 60° to the side: it follows to the edge of your view. Hold still: it holds still.
4. **Gaze:** the Console should show `[Misdirection] Gaze: spectator faces are ISDK gaze interactables...` once the crowd is up. Look at the Kid: eye contact after about 0.3 s, as before. Untick *Use Isdk Gaze* on the Crowd and play again: it must feel the same.
5. **Look and pinch** (the simulator's Look and Pinch input, or webcam hands): steal the coin back, look at a spectator and pinch: the coin comes from behind their ear, as before.
6. **Unity Profiler** (Window → Analysis → Profiler, Play mode): in *CPU Usage → GC Alloc*, our scripts (`Crowd`, `TrickDirector`, `Spectator`, `MentorHands`) should show 0 B on most frames. ISDK's cone-cast allocates a little each frame; that is Meta's code.

## On a headset (when one turns up)

Install a development build (`tools/build_apk.sh --dev`, `tools/install_quest.sh`), play a full show, and pull the log: every 5 s a `[Misdirection] Perf:` line. The exit criterion is 72 fps with 0 dropped frames through act 3 on a Quest 3S. Also check the first line `[Misdirection] Perf: headset settings: foveation High (dynamic), CPU SustainedHigh, GPU SustainedHigh, 72 Hz`, and that spectators are not magenta.

## Tuning knobs

| Where | Knob | Default |
|---|---|---|
| `Misdirection Performance` → Quest Performance | foveation level, dynamic, CPU / GPU level, display Hz, apply in Editor | High, on, SustainedHigh, SustainedHigh, 72, off |
| same → Perf Logger | window seconds, also in release | 5, off |
| `ComfortAnchor.Settings` (code) | zone width / up / down, delay, slack, follow rate | 50° / 20° / 33°, 0.6 s, 4°, 5 per s |
| `ComfortAnchor.Enabled` (code) | off = all text stays at its home, as on Day 9 | on |
| Crowd → *Use Isdk Gaze* | off = the crowd model's own gaze test | on |
| Crowd's `IsdkSpectatorGaze` | face target radius, cone length | 5 mm, 10 m |
| Crowd → Crowd Settings | face angle (also the ISDK cone), release angle | 6°, 9° |
| `PerformanceSetup` (code) | MSAA, render scale | 4, 1.0 |

If a headset shows we are short of 72 fps, in this order: render scale 0.9, then the cones off in the show (*Show Cones In Show* on the Show Director), then `SharedMaterials.LitShaderName` to `Universal Render Pipeline/Simple Lit` (cheaper lighting, slightly flatter look; re-run the bootstrap).

## Known limits

- **No device numbers.** Everything here is by construction or from the PC. The Devpost text can't quote a measured Quest frame rate until someone runs the perf log on a headset.
- **Not run in the Editor yet.** The scene changes arrive when the bootstrap is run; the ISDK path and the headset requests have only been compiled and reasoned through against the v207 sources. Both step aside with a warning if they fail.
- Spectators are still about 17 primitives each (roughly 8,000 triangles a person): fine for five people, not for fifty. Art pass territory.
- The transparent shader (`Sprites/Default`) is not SRP Batcher compatible. There are few transparent objects (cones, outline, ghost hands), so it doesn't matter yet.
- Looking steeply down while a banner shows, the banner (which sits above the prompt) can end up above the comfortable zone, though still inside the glasses' view.
- The ISDK face target is 5 mm, so the cone-cast accepts a face up to about 0.2° wider than the old 6° test at 1.5 m.
- The glasses' real field of view is taken from Meta's simulation defaults in the SDK, not from a device.
