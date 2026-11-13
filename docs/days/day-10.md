# Day 10 · Wed Nov 11 · Retention: a reason to come back

**Exit criterion:** day two visibly differs from day one.
**Cut rule applied:** local only. No Platform SDK leaderboards, achievements or entitlement calls: judges install from a release-channel invite, and a Platform call that fails there would cost more than a leaderboard earns. Everything below is saved on the device.

Written on Nov 13 (planned for Nov 11). 54 new unit tests (148 core tests in total, all passing). Compiles against Unity 6000.0.66f2 + Meta v207 for the Editor and the Android player.

**Not yet run in the simulator.** The rules are unit-tested; the banner, the trick book on the table, the Regular's line and the save file on disk have only been compiled. The check below is the first run.

## What changed

**What the player sees**

| When | Day one | Day two |
|---|---|---|
| Start of act 1 (4 s banner above "Act 1 of 3") | "Day 1 / Today's crowd: … / Challenge: …" | "Day 2", a different crowd with **+ Regular**, a different challenge |
| Act 3 | Five people, no Regular | The Regular is back. Produce the coin from the same ear as yesterday and he points: **"Same as yesterday!"** A different ear: "That's new!" |
| Bow or walk-off (trick book on the table, left of the mat) | Star coins for each trick, "Challenge: OK / not yet", "Day 1: come back tomorrow", "Tomorrow: the Regular comes to watch" | More stars (the new ones turn), "Day 2 in a row", best tips, "Tomorrow: a new crowd, a new challenge" |

**The daily show.** Every calendar date has its own show, the same for everyone on that date (seeded from yyyymmdd; the device's local date).

- Still three acts of 3, 4 and 5 people; still vanish, cup, vanish. Two of a kind is allowed, three is not.
- Act 1 is learnable: the Kid, someone easy (Tourist or a second Kid), and at most one hard watcher (Skeptic or Filmer).
- The Skeptic has arrived by act 2.
- Nobody leaves: each act keeps the people already there and one more joins, so act 3 is the hardest crowd of the day. Where the newcomer stands in the arc changes too.
- **The Regular only comes back once you have finished a show on an earlier day.** Then he joins for act 3. Acts 1 and 2 of a date are the same for everybody.

The next few days, so you know what to expect:

| Date | First-day player | Returning player | Challenge |
|---|---|---|---|
| Nov 13 | Tourist, Skeptic, Kid + Kid + Filmer | … + Kid + Regular | Finish with 60 tips or more |
| Nov 14 | Tourist, Skeptic, Kid + Filmer + Skeptic | … + Filmer + Regular | Applause for every reveal in one act |
| Nov 15 | Kid, Tourist, Skeptic + Filmer + Skeptic | … + Filmer + Regular | No busts all show |
| Nov 16 | Tourist, Tourist, Kid + Skeptic + Skeptic | … + Skeptic + Regular | Finish with 60 tips or more |

**The daily challenge.** One a day. The five come round in a shuffled order every five days, never the same one two days running. Once met, it stays met for the day.

| Challenge | Met when |
|---|---|
| No busts all show | The show reaches the bow with no bust |
| Find the coin behind the Skeptic's ear | A production from the Skeptic that isn't heckled |
| Applause for every reveal in one act | One finished act, no bust in it, every reveal applauded |
| Finish with N tips or more | The show reaches the bow with at least N tips (N is 60, 70, 80 or 90) |
| No heckles from the Filmer | The show reaches the bow and she never says "I saw that!" (the Skeptic on days with no Filmer) |

**The trick book.** Each trick (Vanish, Cup) has three stars, one per feat, kept for good. The book shows what the next star takes.

1. A reveal with no heckle (applause or a gasp).
2. A whole performance (the act finished, no bust, both reveals) with applause on every reveal.
3. A whole performance with no heckle, in front of the Skeptic and the Filmer together. Whether both are there depends on the day's crowd (about half the days), so this one can mean coming back tomorrow.

**Days and streak.** "Day N" counts the days you have played. The streak counts days in a row with a show that reached its end. A walk-off counts: turning up keeps the streak. Missing a day starts it again at 1; the longest streak is kept. A second show on the same day changes nothing, and a clock set backwards never takes days away.

**The returning Regular.** He remembers your last show's signature: whose ear the coin came from, and which trick the show ended on.

- The same ear again: he points, says "Same as yesterday!" ("Same as last time!" if it wasn't yesterday) and his suspicion jumps.
- Once you have repeated a signature, he walks in Curious (his "?" never goes away) and seen methods raise his suspicion faster: +35% per repeat, up to three repeats. Only for the trick he keeps seeing.
- A different ear: "That's new!", and he is an ordinary spectator again.
- A show with no production (an early walk-off) leaves his memory alone.

**The save.** `progress.json` in the app's persistent data folder (in the Editor: `%USERPROFILE%\AppData\LocalLow\<company>\<product>\`). Written to a temporary file and swapped in, keeping the previous save as `.bak`. Loading never stops the game: no file starts fresh, an unreadable file is set aside as `.corrupt` and the backup is used, and anything loaded is repaired (format version, missing fields, impossible values) before use. Two weeks of challenge results are kept.

**Code**

- `Core/Progress/DailyShow.cs`: the daily seed and line-ups · `DailyChallenge.cs`: the five challenges
- `Core/Progress/ShowRecord.cs`: what happened in one show, spectators named by archetype
- `Core/Progress/TrickBook.cs`: the stars · `RegularMemory.cs`: the Regular's rules
- `Core/Progress/ProgressData.cs`: the save and its repair and merge · `ProgressRules.cs`: what a finished show adds · `ProgressText.cs`: the short lines · `CalendarDay.cs`: dates
- `Runtime/Progress/ProgressStore.cs`: the file · `ProgressDirector.cs`: wires it all to the show · `TrickBookView.cs`: the book on the table
- `Runtime/Show/ShowDirector.cs`: four events (show started, reveal, bust, show ended) and `UseActs`
- `Core/Crowd/SpectatorMind.cs`: `SetWariness` (suspicion gain scale and floor), used only for the Regular
- Tests: `tools/tests/csharp/ProgressTests.cs`

**Setup:** the bootstrap adds the Progress Director to Show.unity. By hand: open Show.unity → **Misdirection → Day 10 → Add Progress Director (daily show, trick book) to Open Scene** → save. A scene that hasn't been re-saved still works: the Progress Director adds itself at runtime wherever there is a Show Director (to switch progression off, leave a disabled one in the scene).

## Check in the simulator · ~30 min

Run **Misdirection → Setup → Run Project Bootstrap** once, then start clean: **Misdirection → Day 10 → Reset Progress**.

1. **Day one.** Play Show.unity and set the table. When act 1 starts, a banner above "Act 1 of 3" reads "Day 1", today's crowd and the challenge for about 4 s. The Console has the same in one line: `[Misdirection] Progress: 2026… is day 1 …`.
2. **Play to the bow** (or bust three times for a walk-off). Left of the mat stands the trick book: Vanish and Cup with gold or grey coins, the next star for each, "Challenge: OK" or "not yet", "Day 1: come back tomorrow", "Tomorrow: the Regular comes to watch". The Console logs `Progress: saved. …`.
3. **Day two.** Still in Play mode: **Misdirection → Day 10 → Simulate Next Day**, then pick up the coin. The banner now reads "Day 2", the crowd line ends in "+ Regular" and the challenge is a different one. (Or stop, Simulate Next Day, and press Play.)
4. **The Regular.** In act 3 of day two, produce the coin from the same kind of spectator as on day one: he points and "Same as yesterday!" floats above him. Bow: "Day 2 in a row". On day three (Simulate Next Day again) he arrives with a "?" above his head. Produce from somebody else: "That's new!".
5. **A missed day.** Simulate Next Day twice, play a show: the book says "Day 1: come back tomorrow" again, and **Log Saved Progress** shows the longest streak kept.
6. **Second launch.** Stop and play again without touching the date: same day number, same crowd, same challenge; the stars are still there.
7. **A broken save.** Stop. Open `progress.json` (**Log Saved Progress** prints its path), delete half of it, save, and play: a warning in the Console, progress recovered from the backup, no error. (The backup is the save before the last one, so this needs two saved shows; with one, the game starts fresh, again with a warning and no error.)
8. Automated: `tools/sim_smoke.sh`. The report now includes `progress_day`, `daily_challenge` and `streak` (reported, not part of pass/fail).

**Without hands** (the webcam won't hold a whole show): **Misdirection → Day 10 → Book a Practice Show Today (testing)** books one clean show through the real rules, as if it had just been played. Then Simulate Next Day and Play: "Day 2", the Regular in the crowd line. **Preview Trick Book (Play mode)** stands the book on the table for 20 s once the table is set.

When done: **Back to Today (clear the simulated date)**, and Reset Progress if the save holds days "from the future".

## Tuning knobs

On the **Progress Director** (child of Misdirection Show):

- *Daily Crowd*: untick for the fixed Day 9 crowd (for filming); the challenge, book and streak still work.
- *Day Banner Seconds* (4) and *Day Banner Local* (0, 0.47, 0.55): how long and where the banner floats, in table space (x right, y up, z away from you, metres).
- *Book Local* (−0.25, 0.135, 0.40): where the trick book stands. Move it toward x = 0 if it falls outside a narrow field of view.
- *Regular*: gain per repeat (0.35), max repeats (3), wary floor (0.4, just above Curious), repeat bump (0.3).

In code (`Core/Progress`): the crowd weights and the two-of-a-kind cap in `DailyShow.cs`; the tip targets in `DailyChallenge.cs`; what counts as a whole performance in `TrickBook.cs`; the 14 days of history in `ProgressData.cs`.

## Known limits

- **Not run in the simulator yet.** Text sizes, the book's position and the banner sitting clear of "Act 1 of 3" are estimates.
- The save uses Unity's JsonUtility, which the unit tests can't run: the first Editor run is the real test of saving and loading (step 6 and 7).
- The date is the device's local date. Changing the clock forward gives a new day's show; nothing stops that, and nothing is lost by it.
- Simulate Next Day works in the Editor and in development builds only (a PlayerPrefs offset); release builds always use the real date.
- A show counts for the day it started on. A new day that begins while the app is open arrives with the next show.
- The Regular remembers the last production he heard of, even on the day he wasn't there yet. He never appears in act 1 or 2.
- No microgesture scrolling for the trick book (the Day 10 INT item): two tricks fit on one card, so there is nothing to scroll.
- The show's own star coins and "Bravo!" banner are unchanged; the trick book adds to them.
