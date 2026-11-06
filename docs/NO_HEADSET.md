# Building without a Quest (from Nov 6)

The team has no Quest. Development and testing move to the **Meta XR Simulator 207.1** on the Windows PC. Its v207 features cover most of what we need:

- **Webcam hands:** your real hands, seen by the webcam, drive the simulated hands, including per-finger pinch strength.
- **Look and Pinch:** gaze-and-pinch input.
- **Synthetic rooms:** rooms with walls and furniture that the room scanner (MRUK) can read.

The rules allow simulator footage for the video ("a Meta VR device, via XR Simulator, or another equivalent emulator").

## How to play it on the PC

1. Unity Hub → **Installs → Locate** → `C:\Unity\6000.0.66f2\Editor\Unity.exe`, then **Projects → Add** → this folder.
2. Open the project. Menu **Window → Meta → Meta XR Simulator → Activate**.
3. Open `Assets/Misdirection/Scenes/Show.unity` and press **Play**. In the simulator window, pick a synthetic room with a table and switch the hands to webcam tracking.

## What each gate becomes

| Plan item | With a Quest | Without (what we do now) | Risk we accept |
|---|---|---|---|
| Day 1 tracking spike | Measure how far you can turn before the secret hand drops out | **Can't measure.** Assume the conservative layout: crowd within **±20°**, secret moves low and centred | The real arc may be narrower or wider |
| Day 2 ditch 19/20 | Real hand tracking | Webcam hands in the simulator: tests our grip rules and timing, not Quest cameras | Feel on device unknown |
| Day 4 fun gate | Outsiders in the headset | Outsiders play the simulator on the PC monitor (mouse-look head, webcam hands). Same questions: do they find "where I look, they look", and ask to go again? | Weaker signal than in VR |
| Day 5 three tables | Three real tables | Three synthetic rooms with tables | Real scans are messier |
| Day 12 performance (72 fps on 3S) | Measure on device | Keep greybox light; check draw calls and CPU in the Unity profiler | No device frame-rate number for the Devpost text |
| Day 13 unfamiliar rooms | 3+ homes | Every synthetic room the simulator offers, plus "no room scan" | Same as Day 5 |
| Day 15 judge install | Install via Invite URL on a headset | Upload to the Competition channel and check the Dashboard accepts it; can't play it | **Highest risk:** a device-only crash would fail Stage One |
| Video | Headset capture | Simulator capture + webcam of the real hands for the split screen | None (rules allow it) |

## Worth trying before Nov 16

Even one hour on any Quest 3 or 3S (a friend, a college lab, a VR café) before **Day 15** would let us run the judge install path and catch device-only crashes. Install the APK with `tools/install_quest.sh`.

## Automated checks we can run here

- `tools/unity_compile_check.py`: every script against the real Unity + Meta packages.
- `python -m unittest discover -s tools/tests -t .`: all the game rules (C#) and analysis tools.
- `tools/unity_batch.sh bootstrap | build`: project setup and a debug APK, headless.
- Simulator smoke test (in progress): plays Show.unity in the simulator and checks that the table, crowd and trick come up without errors.
