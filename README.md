# Misdirection (working title)

A seated, hands-only, mixed-reality street-magic game for Meta Quest. Your gaze and your "show hand" steer a crowd's attention while your "secret hand" palms, ditches and produces coins at your own real table.

**Core rule: where you look, they look.**

Entry for the Meta Horizon Start Developer Competition 2026, Gaming track (Best New Gaming Experience). Built in Unity 6 with Meta XR SDK v207, locked at v207 for the whole build.

- Submission target: **18:00 IST, Wed Nov 18, 2026** (Devpost closes 01:30 IST Nov 19)
- Fun gate: **Thu Nov 5 (Day 4)**. If outsiders don't get "where I look, they look" unprompted, the team pivots to Kolam Dawn on Day 5.

## Where things are

| Path | What it is |
|---|---|
| [docs/TIMELINE.md](docs/TIMELINE.md) | Day-by-day plan from Nov 1 to Nov 18, split by role, with gates, cut order and pivot rule |
| [docs/days/](docs/days/) | Detailed runbook for each day as it starts. **Today's: [day-01.md](docs/days/day-01.md)** |
| [docs/DESIGN.md](docs/DESIGN.md) | Game design and how each feature maps to the judging criteria |
| [docs/OPEN_ITEMS.md](docs/OPEN_ITEMS.md) | Rules, eligibility and device questions to verify, with owners and due dates |
| [docs/TRAILER.md](docs/TRAILER.md) | Video shot list (under 3:00) |
| [docs/BUILD_WITHOUT_A_HEADSET.md](docs/BUILD_WITHOUT_A_HEADSET.md) | Compile checks with no Unity licence; APK builds in CI or the cloud with one |
| `Assets/Misdirection/` | Our code and content. Everything we write lives here |
| `tools/` | Build, install, upload and analysis scripts |

## Status

| Day | Date | Status |
|---|---|---|
| 0 | Sun Nov 1 | Admin and setup. Repo started |
| 1 | Mon Nov 2 | Unity project in the repo; code compiles against the real Unity 6000.0.66f2 + Meta v207.0.1. One-click bootstrap for settings and scenes. **Still needs:** a Unity licence to build the APK (Editor or CI), and a person with a Quest to run the spike ([runbook](docs/days/day-01.md)) |
| 2 | Tue Nov 3 | **Code written early (Nov 2):** hand presence (tint/freeze/blend), sticky pinch grip, table calibration, servante, coin, ditch practice with a live 19/20 score and logs. Compiles against the real SDK; 21 unit tests on the core rules. Needs on-device tuning ([runbook](docs/days/day-02.md)) |

## Getting started

1. Install Unity **6000.0.66f2** (Unity 6) with Android Build Support (OpenJDK, Android SDK & NDK).
2. Unity Hub → Add project from disk → this folder. Packages (Meta XR SDK v207.0.1) import on first open.
3. **Misdirection → Setup → Run Project Bootstrap** configures the project and creates the scenes. Then follow [docs/days/day-01.md](docs/days/day-01.md).

## Tools

```bash
tools/build_apk.sh                 # signed release APK -> Builds/Android/ (Editor must be closed)
tools/build_apk.sh --dev           # development build
tools/install_quest.sh             # install + launch the latest APK on the connected headset
tools/upload_competition.sh "msg"  # upload the latest release APK to the "Competition" channel
tools/pull_logs.sh spike alice     # pull tracking-spike CSVs from the headset and analyse them
tools/pull_logs.sh ditch alice     # same for Day 2 ditch-practice logs
python3 -m unittest discover -s tools/tests -t .   # all tests: analysis + the game's core C# rules under Mono

# No Unity Hub needed (Linux / CI / cloud sessions):
python3 tools/install_unity_linux.py --dest ~/unity          # editor + Android toolchain, as Hub would
python3 tools/unity_compile_check.py --unity ~/unity/Editor  # compile everything, no licence needed
UNITY_HOME=~/unity tools/unity_batch.sh all                  # bootstrap + APK (needs a Unity licence)
```

The Unity Editor also has a **Misdirection** menu with the same builds plus Day 1 scene helpers.

## House rules

- Hands only. Every build must be playable start to finish with the controllers switched off.
- Never commit keystores, app secrets or `secrets/`. Back up the keystore: losing it means you can never update the app.
- Keep Meta XR SDK at v207. Do not upgrade mid-sprint.
- Keep a dated commit history. It is our evidence that this is a New Experience built inside the competition window.
