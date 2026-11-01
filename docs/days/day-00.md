# Day 0 · Sun Nov 1 · Admin + setup

**Exit criterion:** every account item submitted or escalated. Nothing on this list can be done by code; it needs people with accounts.

## PRO: accounts and rules (do first, these can block the whole entry)

- [ ] Apply to the **Meta Horizon Start** program for every team member. If "Apply today" shows *This page isn't available*: post in the forum thread, open a Meta support ticket, and keep dated screenshots as evidence. (Open item #1)
- [ ] Enable **Developer Access** on each member's Meta account. (#2)
- [ ] Register on **Devpost** and form the team. Note who the Team Representative is.
- [ ] Read the Devpost **rules page in a browser** and fill in [OPEN_ITEMS.md](../OPEN_ITEMS.md) #3 (India eligibility) and #5 (age). Skim #4 and #6–#11 now; they're due tomorrow.
- [ ] Confirm hardware in hand: at least one Quest 3, ideally a Quest 3S too. (#13)
- [ ] In the **Meta Developer Dashboard**: create the organisation, create a new **Quest** app named "Misdirection", and add a release channel named exactly **Competition**.
- [ ] Pick the Android package name **once**: `com.n45div.misdirection` is the default in `MisdirectionBuild.cs`. The Dashboard locks it at the first upload.

## SYS / INT: machines

- [ ] Unity Hub → install **Unity 6000.0.66f2 or newer (6000.0 LTS line)** with *Android Build Support → OpenJDK + Android SDK & NDK Tools*.
- [ ] Headset in **developer mode** (Meta Horizon phone app → Devices → Developer Mode). `adb devices` lists it after you accept the USB prompt.
- [ ] Install the **metavr** CLI. In Claude Code:
  ```
  /plugin marketplace add meta-quest/agentic-tools
  /plugin install meta-vr@meta-vr
  ```
- [ ] Download **ovr-platform-util** (Meta Platform Command Line Utility) for uploads.
- [ ] Clone this repo. Git history starts today; keep committing daily as evidence of a New Experience.

## ART

- [ ] Spectator moodboard: stylised, big readable heads and eyes. Attention must read from 1.5 m in passthrough.
- [ ] Collect 5–10 street-magic reference clips (crowd reactions, eye-line misdirection).
- [ ] First sketch of the trailer split-screen (headset view + external camera on real hands). See [TRAILER.md](../TRAILER.md).
