# Timeline: Nov 1 → Nov 18, 2026 (IST)

Four gates. Every day has one exit criterion. If a day's exit isn't met, apply the cut order in [DESIGN.md](DESIGN.md#cut-order-fixed-in-advance) rather than borrowing from later days.

**Roles**

- **INT** (Interaction lead): hand feel, secret-hand state machine
- **SYS** (Systems lead): crowd attention, game flow, Platform SDK, build pipeline
- **ART** (Art, audio, video): spectators, sound, trailer
- **PRO** (Producer): admin, playtests, submission

A two-person team merges INT + SYS ("Dev") and ART + PRO ("Producer").

**Agents** (Claude Code with the `meta-vr` plugin / metavr MCP) handle deploys, logs, perf traces, and the `hz-unity-device-readiness` and `hz-unity-code-review` audits. Humans own architecture and review every AI-written change. Meta XR Operator stays in development builds only.

| Day | Date | Focus | Exit criterion | Gate |
|---|---|---|---|---|
| 0 | Sun Nov 1 | Admin + setup | Every account item submitted or escalated | |
| 1 | Mon Nov 2 | Pipeline + tracking spike | Signed APK on "Competition"; widest usable crowd arc measured | **Gate 1** |
| 2 | Tue Nov 3 | Secret-hand verbs | Ditch succeeds ~19/20 for two testers | |
| 3 | Wed Nov 4 | Attention model v1 | Cones visibly follow the look source on device | |
| 4 | Thu Nov 5 | Trick 1 end to end + outsider playtest | Outsiders get "where I look, they look" unprompted and ask to go again | **Gate 2 (fun)** |
| 5 | Fri Nov 6 | Robust table placement | Works on three different tables | |
| 6 | Sat Nov 7 | Trick 2 (cups and balls) + scoring | Two-trick show playable | |
| 7 | Sun Nov 8 | Spectator art + crowd audio | Crowd reads clearly in a headset recording | |
| 8 | Mon Nov 9 | Show flow | Full show in 6–8 minutes | |
| 9 | Tue Nov 10 | First Five Minutes | New player reaches first applause in < 2 min with no text | **Gate 3 (feature freeze)** |
| 10 | Wed Nov 11 | Retention | Day two visibly differs from day one | |
| 11 | Thu Nov 12 | Accessibility | Full show completable one-handed | |
| 12 | Fri Nov 13 | Glasses readiness + performance | ≥72 fps on Quest 3S; UI inside simulated glasses FoV | |
| 13 | Sat Nov 14 | Unfamiliar rooms (3+ homes) | No blocking failure in any room | |
| 14 | Sun Nov 15 | Polish + bug bash | Zero known crashes or soft-locks | |
| 15 | Mon Nov 16 | Code freeze + judge install path | Fresh-device install via Invite URL works with controllers off | **Gate 4** |
| 16 | Tue Nov 17 | Video + Devpost draft | Draft complete; all links work logged out | |
| 17 | Wed Nov 18 | Submit | Submitted by 18:00 IST | |

---

## Day 0 · Sun Nov 1 · Admin + setup

Runbook: [days/day-00.md](days/day-00.md)

- **PRO**: Apply to the Start program for every member. If the page is broken, post in the forum thread, open a Meta support ticket, and keep dated screenshots. Enable Developer Access on each Meta account. Register on Devpost and form the team. Read the rules **in a browser** and answer [open items](OPEN_ITEMS.md) #3 and #5. Confirm hardware (#13).
- **PRO**: Create the org and app in the Meta Developer Dashboard. Create a release channel named exactly **Competition**.
- **SYS/INT**: Install Unity 6000.0.66f2+ with Android Build Support, Meta Quest Developer Hub or adb, the metavr CLI, and the agentic-tools plugin. Put the headset in developer mode. Clone this repo.
- **ART**: Spectator moodboard (stylised, readable heads and eyes). Street-magic reference clips. First pass at the trailer's split-screen storyboard.

## Day 1 · Mon Nov 2 · Gate 1: pipeline end to end

Runbook: [days/day-01.md](days/day-01.md). Code is ready in `Assets/Misdirection/Scripts` and `tools/`, and compiles against the real Unity 6000.0.66f2 + Meta v207 code. APKs can also be built in CI once a Unity licence secret is added ([BUILD_WITHOUT_A_HEADSET.md](BUILD_WITHOUT_A_HEADSET.md)).

- **INT**: Open the project (already in the repo) and run **Misdirection → Setup → Run Project Bootstrap** to create the Spike and Main scenes. Run the tracking spike with 2+ testers on Quest 3 (and 3S if available). Analyse the results and set the crowd arc.
- **SYS**: Team keystore and signing, `tools/build_apk.sh`, install via metavr or adb. Answer open item #14 (what the Eye Gaze block falls back to on Quest 3). Verify `LookSource` reports `head` on Quest 3.
- **PRO**: Upload the signed APK to "Competition", create the Invite URL, install through the Store on a second account or headset. Answer open items #4, #6, #7, #8, #9, #11.
- **ART**: Finish the storyboard in [TRAILER.md](TRAILER.md). Do a 5-minute passthrough capture test (early look at #16).

## Day 2 · Tue Nov 3 · Secret-hand verbs

Runbook: [days/day-02.md](days/day-02.md). Code written early on Nov 2 and unit-tested; today is on-device tuning.

- **INT**: Pinch-palm via ISDK hand grab. Coin parented to the wrist. Release margin set from thumb–index distance at grab time. No release while confidence is low. Servante trigger volume along the near table edge (35–45 cm from the eyes). Hand states: tracked → low-confidence tint → frozen/faded → blend-back.
- **SYS**: Per-attempt logging (success/fail + reason) so "19 of 20" is measured, not guessed. Coexistence test for Operator and microgestures (#15). An agent-driven deploy loop (`metavr`).
- **ART**: Coin model. Sounds for pickup, palm, servante drop and reveal. Hand-tint material.
- **PRO**: Recruit 3+ outside playtesters for Day 4 and schedule them. Open item #10.
- Exit: the ditch succeeds about 19/20 for two testers.

## Day 3 · Wed Nov 4 · Attention model v1

Runbook: [days/day-03.md](days/day-03.md). Code written and unit-tested on Nov 4; on-device check pending.

- **SYS**: Each spectator's attention point = weighted blend of the look source, the latest flourish and the latest sound. Attention cones. Suspicion builds when a method happens inside a cone. Stare discomfort (a spectator looks away after being stared at too long, often at your hands).
- **INT**: Show-hand flourish: open palm aimed at a target, held about 0.3 s, never a palm-facing pinch.
- **ART**: Greybox spectators with code-driven head and eye look-at. Debug "Magician's Eye" cone visual.
- **PRO**: Open item #12 (which device judges use). Day 4 playtest script and consent notes.
- Exit: cones visibly follow the look source on device.

## Day 4 · Thu Nov 5 · Gate 2: fun gate

Runbook: [days/day-04.md](days/day-04.md). Trick 1 written and unit-tested on Nov 4; Show.unity is the APK's first scene.

- **INT**: Produce-from-spectator: ISDK v207 gaze interactor + pinch on spectator faces (hit area > 3°).
- **SYS**: Trick flow (engage → misdirect → execute → reveal). Amazement score stub.
- **ART**: Placeholder gasp and applause audio. Reveal VFX.
- **PRO**: Run 3+ outsider playtests and record them (with consent). Write the go / pivot decision in `docs/results/day-04-fun-gate.md`.
- **Gate 2**: outsiders grasp "where I look, they look" unprompted and ask to go again → continue. Otherwise, or if the Day 1 arc lost more than 1 move in 10, **pivot to Kolam Dawn on Day 5**.

## Day 5 · Fri Nov 6 · Robust table placement

- **SYS**: MRUK table detection, spatial-anchor persistence, no-table fallback (height from palms on the table), crowd placed on open floor, arc tightens near walls.
- **INT**: Palms-flat calibration gesture (also the onboarding moment).
- **ART**: Close-up mat art. Passthrough capture check (#16).
- **PRO**: Line up three different tables or rooms and test them.
- Exit: works on three different tables.

## Day 6 · Sat Nov 7 · Trick 2 + scoring

- **INT**: Cups-and-balls load (**first cut candidate**).
- **SYS**: Amazement and suspicion scoring, tips, heckle thresholds.
- **ART**: Cups, ball, table props.
- **PRO**: Watch the cut rule. If the trick isn't playable by evening, cut it.
- Exit: a two-trick show is playable.

## Day 7 · Sun Nov 8 · Spectator art + crowd audio

- **ART**: Stylised spectator models. Code-driven head and eye look-at. Reaction states. Crowd audio (murmurs, gasps, applause), spatialised.
- **SYS**: The five archetypes (Tourist, Kid, Skeptic, Filmer, Regular) as attention-weight presets.
- **INT**: Hand-feel tuning from Day 4 notes.
- **PRO**: Title and trademark check (#17). Start a Devpost text skeleton.
- Exit: the crowd reads clearly in a headset recording.

## Day 8 · Mon Nov 9 · Show flow

- **SYS**: Three-trick set, tips, caught → heckle → walk-off path, pause/resume (both hands gone > 2 s), cold start to first interaction in about 10 s.
- **INT**: Tracking-loss polish (freeze, fade, blend back) across all verbs.
- **ART**: Show intro and outro stings, tip VFX.
- **PRO**: Mid-sprint outsider playtest (2+ people).
- Exit: a full show runs in 6–8 minutes.

## Day 9 · Tue Nov 10 · Gate 3: First Five Minutes, feature freeze

- **INT**: Mentor ghost hands. Palms-on-table calibration. Tutorial cones. Launch lighting and hands-in-view tip.
- **SYS**: Tutorial state machine. Skip the tutorial on later launches.
- **ART**: Mentor hand visuals. Tutorial audio lines.
- **PRO**: Fresh-eyes test with someone who has never seen the game.
- **Gate 3**: a new player reaches first applause in under 2 minutes with no text. **No new features after today.**

## Day 10 · Wed Nov 11 · Retention

- **SYS**: Daily-seeded crowd, trick book with mastery stars, local save, Platform SDK leaderboard and achievements. Cut rule: if this slips, a local daily best + trick book replace the Platform SDK work.
- **INT**: Microgesture scrolling for the trick book.
- **ART**: Trick book UI.
- **PRO**: Confirm the Platform SDK entitlement check doesn't block judges installing via the Invite URL.
- Exit: day two visibly differs from day one.

## Day 11 · Thu Nov 12 · Accessibility

- **INT**: One-handed mode (gaze misdirects, the single hand executes). Longer windows option.
- **SYS**: Voice "Look!". Gaze-dwell menus. Cut rule: if this slips, drop voice and dwell, keep the rest.
- **ART**: Captions for every sound. Shape-coded suspicion icons (colour-blind safe).
- **PRO**: Test with someone using only one hand.
- Exit: the full show is completable one-handed.

## Day 12 · Fri Nov 13 · Glasses readiness + performance

- **SYS**: OVRManager glasses-FoV simulation. Device Readiness Check. Simulator Look and Pinch. Runtime Optimizer pass.
- **INT**: Eye-gaze path through `LookSource` in the Simulator.
- **ART**: Texture and draw-call budget cleanup.
- **PRO**: Record perf numbers for the Devpost text.
- Exit: ≥72 fps on Quest 3S (60 is the rules' minimum); UI inside the simulated glasses FoV.

## Day 13 · Sat Nov 14 · Unfamiliar rooms

- **ALL**: Test in 3+ homes: dim light, cramped room, table against a wall, no table. Log every failure.
- Cut rule: placement trouble → the crowd locks to a shallow arc behind the table that tightens near walls.
- Exit: no blocking failure in any room.

## Day 14 · Sun Nov 15 · Polish + bug bash

- **INT/SYS**: Fix list. Operator-scripted pinch and gaze regression runs in the Simulator.
- **ART**: Audio mix, VFX polish.
- **PRO**: 5+ outside testers. Triage.
- Exit: zero known crashes or soft-locks.

## Day 15 · Mon Nov 16 · Gate 4: code freeze

- **SYS**: Release candidate to "Competition".
- **PRO**: Fresh-device install via the Invite URL **with controllers powered off**. Complete the whole experience hands-only.
- **ART**: Capture all footage: headset view, external camera on real hands, Simulator in the VR Glasses profile.
- **Gate 4**: the judge install path is verified.

## Day 16 · Tue Nov 17 · Video + Devpost

- **ART**: Edit the video to under 3:00 per [TRAILER.md](TRAILER.md). Real device or Simulator footage only, no AI video. Burned-in captions, licensed music. Publish publicly on YouTube.
- **PRO**: Devpost text: one-line pitch, each capability by SDK name, hands-only test result, measured fps, rooms tested, accessibility options, Night Train Con under future plans, launch target. Screenshots. Save the draft. Check every link while logged out.

## Day 17 · Wed Nov 18 · Submit

- **PRO**: Final check, then **submit by 18:00 IST** (7.5 h before the 01:30 IST Nov 19 close).
- **DEV**: Critical hotfixes only, each re-verified on device. Keep the release channel live until winners are announced (around Dec 11).

---

## Pivot plan: Kolam Dawn (only if Gate 2 fails)

Day 5 starts Kolam Dawn with 13 days left. It keeps table placement, pinch handling, the onboarding pattern and the daily seed. Hold a pinch to trickle rice flour around a dot grid on your real table; lines snap to legal curves around each dot; closing the loop brings the kolam to life. The team re-plans Days 5–17 the same morning.
