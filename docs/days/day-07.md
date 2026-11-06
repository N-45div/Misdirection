# Day 7 · Sun Nov 8 · A crowd that reads clearly

**Exit criterion:** the crowd reads clearly in a recording. With no Quest, the recording comes from the Meta XR Simulator ([NO_HEADSET.md](../NO_HEADSET.md)).

Written on Nov 6. The expression rules have 6 unit tests (74 core tests in total), and the new crowd runs in the simulator smoke test.

## What changed

Spectators are still built from primitives in code (no art assets yet), but every state now shows on the body:

| State | Face | Body | Sound (3-D, from that spectator) |
|---|---|---|---|
| Calm | Neutral | Upright, slight lean in | Quiet murmur |
| Curious (?) | Brows up | Leans in | Murmur rises |
| Suspicious (?!) | Brows furrowed, frown | Leans back | Louder murmur |
| About to walk off (!!) | Hard frown | Leans well back | Loudest murmur |
| Eye contact (◉) | Small smile | — | Murmur drops |
| Applause | Big smile | Claps (each slightly out of time) | Claps + "ooh" |
| Gasp | Jaw drops, brows high | Hands up, leans back | Gasp |
| Heckle (saw your method) | Furrowed, frowning | Leans in, **points at your hands** | "Hm!" |
| Heckle (bystander) | Slight frown | — | — |

Archetypes are easy to tell apart at a glance:

| Archetype | Colour | Look and resting pose |
|---|---|---|
| Tourist | Blue | Sun hat |
| Kid | Green | Red cap, smaller, higher voice |
| Skeptic | Purple | Glasses; resting pose is arms folded |
| Filmer | Grey | Phone held up in one hand |
| Regular | Brown | Green scarf |

When a crowd is present, the old flat global applause is replaced by these spatial sounds. A bust makes everyone frown.

- `Core/Crowd/Expression.cs`: state → expression (pure logic, tested)
- `Runtime/Audience/SpectatorFigure.cs`: brows, mouth, arms, lean, breathing, accessories
- `Runtime/Audience/SpectatorVoice.cs`: generated murmur, claps, gasp, "hm!", all 3-D
- `Crowd.React(reaction, seenBy)`: called by both tricks on reveals and busts

## Check in the simulator · ~20 min

1. Play Show.unity with the simulator active (see NO_HEADSET.md). Calibrate or let the room scan find the table.
2. **Gaze:** look at each spectator. Their heads and eyes turn; ◉ and a small smile on eye contact.
3. **Suspicion:** carry the coin around in the Skeptic's cone. Watch her brows furrow and her body lean back as ? → ?! → !!.
4. **Reveals:** do a clean Trick 1 (everyone claps), then a sloppy one (the Skeptic points at your hands and says "hm!").
5. **Record a 20-second clip** of a clean reveal and a heckle, then show it to someone who hasn't seen the game. Can they say who was suspicious and who was impressed **without sound**? That's the exit check.

## Tuning

- Expression strengths live in `ExpressionModel` (code).
- Reaction length: `ExpressionModel.ReactionSeconds` (2.5 s).
- Murmur loudness: `SpectatorVoice.Tick` targets.
- Arm poses: angles in `SpectatorFigure.Pose`.

## Known limits

- Still primitives. Proper stylised meshes would be an art pass if time allows after feature freeze (Day 9).
- Placeholder audio is generated in code. Licensed or recorded crowd sounds go in during the Day 14 polish.
