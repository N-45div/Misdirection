# Day 4 · Thu Nov 5 · Trick 1 + Gate 2 (fun gate)

**Gate 2:** outsiders grasp "where I look, they look" **unprompted** and **ask to go again**. Otherwise, or if the Day 1 spike lost more than about 1 move in 10, the team pivots to Kolam Dawn on Day 5 ([TIMELINE.md](../TIMELINE.md#pivot-plan-kolam-dawn-only-if-gate-2-fails)).

Trick 1 was written on Nov 4. It compiles against the real Unity 6000.0.66f2 + Meta XR SDK v207.0.1, and its rules have 9 unit tests (48 core tests in total). **Not yet run on a headset**, so do a 15-minute team run-through before the first outsider sits down.

## Trick 1: vanish, then produce

| Beat | Player does | Game checks | Crowd |
|---|---|---|---|
| Engage | Pinch the coin on the mat, hold it up | Coin hand ≥ 15 cm above the table for 0.6 s | Hands-watchers look at the coin |
| Misdirect | Look a spectator in the eye, flourish, or look elsewhere | Carrying inside any cone slowly builds suspicion | Eyes follow your look; eye contact locks |
| Execute | Carry the coin to the near edge and open the hand | Ditch in the servante = method, scored by who saw it | A method inside a cone = suspicion, heckle if ≥ 60% seen |
| Reveal 1 | Show the empty hand: open, raised, palm out or up, for 0.4 s | Amazement = (1 − coverage) × (1 − ½ × average suspicion) | Applause ≥ 75%, gasp ≥ 45%, else heckle. Tips = amazement × 5 × spectators |
| Encore | Pinch inside the servante to steal the coin back (palmed) | Steal = method (severity 0.8) | Same scoring |
| Reveal 2 | Look at a spectator for 0.4 s, raise the palming hand and pinch | Produce from behind their ear: coin flies from the ear to your fingers | +20% production bonus |
| Bust | Coin released outside the servante | "The coin fell in plain view." Resets after 2.5 s | Clink draws eyes |

The bootstrap builds this as **Show.unity** and makes it the first scene in the APK. In an existing scene, use **Misdirection → Day 4 → Add Trick 1 (Vanish + Produce) to Open Scene** (it refuses scenes that already have the ditch practice). Cones are on by default, which suits playtests. The servante outline is off in the show.

Each event goes to `show/show_<headset>_<time>.csv` on the headset (`tools/pull_logs.sh` handles `spike` and `ditch`; pull `show` with `adb pull /sdcard/Android/data/com.n45div.misdirection/files/show`).

## Team run-through first · INT + SYS · 15 min

- [ ] Calibrate. The crowd appears and the HUD says "Pick up the coin."
- [ ] Full clean run: show, eye contact with the Skeptic, ditch → "Nobody saw it", show empty hand → applause, steal, look at the Kid and pinch → coin flies from his ear.
- [ ] Sloppy run: ditch while looking at your hands → the Skeptic's "I saw that!"
- [ ] Bust: drop the coin on the mat → reset.
- [ ] Note anything confusing in the tuning log below **before** outsiders arrive.

## Outsider playtest · PRO runs it · 3+ people, ~10 min each

Script (read it word for word, then stay quiet):

> "You're a street magician. These people are watching you. Make the coin vanish without them seeing how. I can't give you any other tips."

Rules for the observer: **don't explain "where I look, they look."** Note the moment the player discovers it, if they do. Record the session (headset cast + phone on the hands) with consent.

| # | Tester (initials) | Found "they look where I look" unprompted? (time) | First applause at | Busts | "Can I go again?" unprompted | Quote | Notes |
|---|---|---|---|---|---|---|---|
| 1 | | | | | | | |
| 2 | | | | | | | |
| 3 | | | | | | | |

## Go / pivot decision (write today)

Copy the table above into [`docs/results/day-04-fun-gate.md`](../results/day-04-fun-gate.md), then answer:

1. How many of 3+ outsiders discovered the gaze rule unprompted?
2. How many asked to go again without being prompted?
3. Day 1 spike: did any usable arc keep lost moves at or under 1 in 10?
4. **Decision:** GO (continue Misdirection on Day 5) / PIVOT (Kolam Dawn on Day 5). Who decided, and why.

## Tuning knobs (Inspector on *Misdirection Trick 1*)

| Symptom | Knob |
|---|---|
| "Hold it up" never completes | *Trick Settings → Show Height / Show Seconds* down |
| Empty-hand reveal doesn't register | *Reveal Openness* down, *Reveal Height* down |
| Production fires too easily or never | *Trick Settings → Produce Dwell Seconds*, *Produce Pinch Strength*, *Produce Height* |
| Can't steal from the edge | *Steal Margin* up (3 → 5 cm) |
| Too many heckles | *Trick Settings → Heckle Coverage* up; *Crowd → Exposure Rate* down |
| Tips feel stingy | *Trick Settings → Tips Per Spectator* |

## Tuning log

| Date | Tester | Change | Why | Result |
|---|---|---|---|---|
| | | | | |
