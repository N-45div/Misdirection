# Day 9 · Tue Nov 10 · First five minutes + show flow · Gate 3 (feature freeze)

**Exit criterion:** a new player reaches the first applause in **under 2 minutes with no help**. With no Quest, they play in the Meta XR Simulator on the PC ([NO_HEADSET.md](../NO_HEADSET.md)).
**Feature freeze tonight.** After today, only the planned retention (Day 10) and accessibility (Day 11) work, then fixes.

Written on Nov 10. Day 8's show flow had slipped, so it lands today too. 20 new unit tests (94 core tests in total, all passing). Compiles against Unity 6000.0.66f2 + Meta v207 for the Editor and the Android player.

**Simulator smoke test, Nov 10: PASS** (Meta XR Simulator 207.1, `tools/sim_smoke.sh`). Table found in the room scan, show in `Tutorial` with just the Kid, Trick 1 live, tutorial at `PickUp`, 71 fps in the Editor, no errors. Nobody's hands were in view, so after 2 s the show paused (`HandsGone`, time scale 0, ghost hands up), as designed. The ghost hands still need a look by eye: that's the hands-on check below.

## What changed

**The show has a start and an end** (it used to loop Trick 1 ↔ Trick 2 forever):

| Stage | What happens | Ends when |
|---|---|---|
| Setup | Ghost hands rest palms-down on the table | The table is known (palms, saved anchor or room scan) |
| Tutorial (first launch only) | Just the Kid. Ghost hands teach one vanish, step by step | She claps or gasps, then 3 s of applause |
| Act 1 | Trick 1 (vanish + produce) for Tourist, Kid, Skeptic | Trick 1's bow |
| Act 2 | Trick 2 (coin under the cup); the Filmer joins | Trick 2's bow |
| Act 3 | Trick 1 again; the Regular joins (5 people) | Trick 1's bow → **Bow** |
| Bow | Everyone claps, "Bravo! N tips", 0–3 gold coins for stars | Pick up the coin (after 4 s) → new show |
| Walk-off | 3 busts in one show: frowns, then the crowd leaves | Pick up the coin (after 4 s) → new show |

- **Stars:** 3 = no busts and at least two thirds of reveals got applause; 2 = at most one bust; 1 = finished; 0 = walked off.
- **Pause ("the crowd waits"):** both hands out of view for 2 s, or the system taking input focus, freezes the show (game time and sound). Ghost hands held up ask for your hands back; both hands steadily back for 0.5 s resumes. Clean pause/resume is on Meta's design guidelines.
- **Ghost-hand hint:** if the coin sits untouched for 15 s mid-show, a ghost hand reaches for it.
- **Table safety:** resting palms can only re-place the table while the coin is untouched.
- **Cones:** the Magician's Eye cones always show in the tutorial; in the show they stay on unless *Show Cones In Show* is unticked on the Show Director.

**Tutorial, step by step.** Each step is taught by translucent ghost hands, plus a 2–5 word caption that can be turned off (`Mentor Ghost Hands → Captions`):

| Step | Ghost hands show | Moves on when | If it goes wrong |
|---|---|---|---|
| Pick up | A hand reaches for the coin and pinches | Coin picked up | — |
| Show | The pinched coin rises | Held up 0.6 s | Dropped → back to pick up |
| Look at her | A pulsing ring around the Kid's face; her cone is visible | Eye contact, or her cone off the coin, for 0.4 s (or 8 s pass) | — |
| Ditch | The hand drifts back past the near edge and opens | Coin in the servante | Dropped in view → bust → back to pick up |
| Show empty | An open hand turns palm-out to her | She claps or gasps: **first applause** | She saw it → coin back on the mat, try again |

Later launches skip the tutorial (saved on the device). To see it again: **Misdirection → Day 9 → Replay the Tutorial Next Launch**, or tick *Always Tutorial* on the Show Director.

**Code**

- `Core/Show/ShowFlow.cs`: stages, acts, tips, busts, stars, restart (pure logic, tested)
- `Core/Show/Tutorial.cs`: the tutorial state machine (pure logic, tested)
- `Core/Show/ShowPause.cs`: hands-gone and focus-lost pause (pure logic, tested)
- `Runtime/Show/ShowDirector.cs`: wires tricks, crowd, table, hands and ghost hands
- `Runtime/Show/MentorHands.cs`: the ghost hands (primitives, unscaled time, optional captions)
- Tricks now report reveals and busts, can be reset, and can hide their text prompt; the crowd can change its line-up.
- Tests: `tools/tests/csharp/ShowTests.cs`

**Setup:** the bootstrap adds the Show Director to Show.unity and removes the old `ShowSequence`. By hand: open Show.unity → **Misdirection → Day 9 → Add Show Director (tutorial, acts, pause) to Open Scene** → save.

## On this PC (one time)

Everything installs on **D:**.

1. Unity 6000.0.66f2 is at `D:\Unity\6000.0.66f2` (installed by `tools/install_unity.py`).
2. **Licence:** install Unity Hub (choose a folder on D:), sign in, and add a free Personal licence. Then **Installs → Locate** → `D:\Unity\6000.0.66f2\Editor\Unity.exe`, and **Projects → Add** → this folder.
3. **Meta XR Simulator 207.1:** download the Windows installer from Meta's [simulator download page](https://developers.meta.com/vr/downloads/package/meta-xr-simulator-windows/) and install it under `D:\MetaXR`. No Unity package is needed: in the Editor, **Window → Meta → Meta XR Simulator → Activate**.
4. Open the project, run **Misdirection → Setup → Run Project Bootstrap** once (it adds today's Show Director to Show.unity).

## Check in the simulator · ~30 min

1. Play Show.unity. In the simulator, pick a synthetic room with a table, set **Device** to **Meta Quest 3S**, and turn on **Inputs → Camera tracking → Enable camera** (webcam hands; pinch and grab work in 207.1).
2. **Setup:** before the table is set, ghost hands lower onto the table in front of you. Rest both palms flat: the mat appears.
3. **Tutorial:** only the Kid is there, with her cone showing. Follow the ghost hands: pinch, hold up, look at her (ring around her face), drop past the edge, open your hand. She claps. Then Tourist and Skeptic walk in, with "Act 1 of 3".
4. **Show:** play all three acts. The Filmer joins at act 2, the Regular at act 3. At the end: "Bravo!", the tip total, the star coins. Pick up the coin: a new show starts.
5. **Walk-off:** bust three times (drop the coin in view). The crowd frowns, then leaves. Pick up the coin to start again.
6. **Pause:** move both hands out of the webcam's view for 3 s. Everything freezes and ghost hands rise. Hands back: it carries on.
7. **Second launch:** stop and play again. No tutorial, straight to act 1.
8. Automated: `tools/sim_smoke.sh` (needs the licence). The report now includes `show_stage`, `tutorial_step`, `paused` and `mentor_demo`; on a first launch with the table set, `show_stage` must be `Tutorial`.

## Fresh-eyes test (Gate 3) · PRO · this evening

One or two people who have never seen the game. Before starting: **Misdirection → Day 9 → Replay the Tutorial Next Launch**. Sit them at the PC, webcam hands on, simulator running. Say only "it's a magic game, use your hands". Don't help. Start a stopwatch when Play starts.

Write it up in [results/day-09-first-five-minutes.md](../results/day-09-first-five-minutes.md):

- Time to first applause (the log line `[Misdirection] Show: first applause N s into the tutorial` gives it exactly)
- Retries, and where they got stuck
- Did they read the captions or follow the hands? Try one run with captions off.
- Did they understand "where I look, they look" by the end of act 1?

**Gate 3 passes** if both reach the first applause in under 2 minutes without help.

## Known limits

- Ghost hands are primitives and right-handed only. One-handed and left-handed play is Day 11.
- The tricks' text prompts still show during the show (after the tutorial). Ghost hands carry the tutorial alone; set *Hide Trick Hud In Show* on the Show Director to try a text-free show.
- Not yet run in the simulator: tuning (ghost-hand positions, the 15 s hint, the 8 s look timeout) happens after the first runs.
