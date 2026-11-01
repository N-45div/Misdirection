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
| `Assets/Misdirection/` | Our code and content. Everything we write lives here |
| `tools/` | Build, install, upload and analysis scripts |

## Status

| Day | Date | Status |
|---|---|---|
| 0 | Sun Nov 1 | Admin and setup. Repo started |
| 1 | Mon Nov 2 | Code ready: tracking spike, look source, signed build pipeline. **Needs a person with a headset** to create the Unity project, build, upload and run the spike ([runbook](docs/days/day-01.md)) |

## Getting started

1. Install Unity **6000.0.66f2 or newer** (Unity 6) with Android Build Support (OpenJDK, Android SDK & NDK).
2. Follow [docs/days/day-01.md](docs/days/day-01.md), step A, to create the Unity project in this folder and add Meta XR SDK v207.
3. Our scripts compile only once `com.meta.xr.sdk.core` 207.x is installed. Until then they stay switched off, which avoids Safe Mode on the first open.

## Tools

```bash
tools/build_apk.sh                 # signed release APK -> Builds/Android/ (Editor must be closed)
tools/build_apk.sh --dev           # development build
tools/install_quest.sh             # install + launch the latest APK on the connected headset
tools/upload_competition.sh "msg"  # upload the latest release APK to the "Competition" channel
tools/pull_spike_logs.sh alice     # pull tracking-spike CSVs from the headset and analyse them
python3 -m unittest discover -s tools/tests -t .   # analysis tests
```

The Unity Editor also has a **Misdirection** menu with the same builds plus Day 1 scene helpers.

## House rules

- Hands only. Every build must be playable start to finish with the controllers switched off.
- Never commit keystores, app secrets or `secrets/`. Back up the keystore: losing it means you can never update the app.
- Keep Meta XR SDK at v207. Do not upgrade mid-sprint.
- Keep a dated commit history. It is our evidence that this is a New Experience built inside the competition window.
