# Day 6 · Sat Nov 7 · Trick 2 + scoring

**Exit criterion:** a two-trick show is playable.
**Cut rule:** Trick 2 is the first cut candidate. If it isn't playable by Saturday evening, cut it; the show becomes vanish, produce and a variation.

Written on Nov 6 (6 unit tests, 66 core tests in total). Not yet run on a headset.

## Trick 2: the coin under the cup

| Beat | Player does | Game checks | Crowd |
|---|---|---|---|
| Engage | Pinch the coin, hold it up | ≥ 15 cm above the table for 0.6 s | Hands-watchers look at the coin |
| Misdirect | Eye contact, a flourish, or look away | Carrying in a cone builds suspicion | Eyes follow your look |
| Load | Open the coin hand right at the cup's base: within 7 cm of it, no higher than 9 cm | The load is the method, scored by who saw it | Seen ≥ 60% → heckle later |
| Reveal 1 | Show the coin hand empty: open, raised, palm out or up, 0.4 s | Amazement from the load | Applause / gasp / heckle |
| Reveal 2 | Pinch the cup's top and lift it ≥ 8 cm | The coin is under it | +20% bonus if you did reveal 1 first |
| Bust | Coin dropped in view, ditched in the servante, or hidden before it was shown | Resets after 2.5 s | Clink draws eyes |

- **Shared scoring:** both tricks use the same rules (`Core/Tricks/VanishTrick.cs` → `RevealScoring`). Amazement = how cleanly the method stayed out of every cone × the crowd's remaining trust, plus a production bonus.
- **The show alternates:** in **Show.unity**, `ShowSequence` runs Trick 1, then Trick 2, then repeats. Only the current trick is active; both share the coin, hands, table and crowd, and the crowd's suspicion resets after each bow.
- **Code:**
  - `Core/Tricks/CupTrick.cs` (rules)
  - `Runtime/Tricks/CupActor.cs` (greybox cup you can pinch and lift)
  - `Runtime/Tricks/CupTrickDirector.cs` (wiring)
  - `Runtime/Tricks/TrickFeedback.cs` (HUD, reactions, sounds, CSV log, shared by future tricks)
  - `Runtime/Tricks/ShowSequence.cs`
- **Setup:** the bootstrap adds Trick 2 and the sequence to an existing Show.unity. Or use **Misdirection → Day 6 → Add Trick 2 (Cup) and Show Sequence to Open Scene**.

## Test protocol · INT + SYS · ~30 min

1. Do Trick 1 to the bow. The show should switch: the cup appears beside the coin, and the HUD says "Pick up the coin."
2. Clean run: show the coin, look the Skeptic in the eye, open your hand at the cup's base, show the hand empty (applause), then lift the cup. The coin is there, with applause and tips.
3. Sloppy run: load while looking at your hands. Expect "I saw that!" on both reveals.
4. Lift-first run: load, then lift without showing the hand empty. You get a reveal without the bonus.
5. Busts: drop the coin on the mat; ditch it in the servante instead of under the cup.
6. Note anything hard to do with hand tracking: lifting the cup, the load position.

## Tuning knobs (Inspector on *Misdirection Trick 1 → Cup Trick Director*)

| Feel | Knob |
|---|---|
| Load doesn't register | *Load Radius* up (7 → 9 cm), *Load Height* up |
| Cup hard to pick up | *Cup Grab Radius* up (6 → 8 cm) |
| Lift reveal too easy or hard | *Trick Settings → Lift Height* (8 cm) |
| Cup in the way of the coin | *Coin Rest Local*, *Cup Rest Local* (table coordinates, metres) |

## Known limits

- The load is abstracted: no rim tilt, just "open your hand at the base". Classic cups-and-balls moves would hide fingers behind the palm, which hand tracking handles worst.
- One cup. Three-cup routines are out of scope for the sprint.
