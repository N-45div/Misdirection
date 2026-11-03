# Day 3 · Wed Nov 4 · Attention model v1

**Exit criterion:** cones visibly follow the look source on device.

Written on Nov 4 alongside Days 1–2, which are still waiting on a headset. It compiles against the real Unity 6000.0.66f2 + Meta XR SDK v207.0.1, and the attention rules have 18 unit tests (39 core tests in total). **Not yet run on a headset.**

## The rules in code

| Rule (docs/DESIGN.md) | Where | How it works |
|---|---|---|
| Where you look, they look | `Core/Crowd/CrowdModel.cs` | Your look ray lands on a spectator's face (within 6°, held until 9°), on the table, or 1.5 m ahead. Every spectator's attention drifts toward a weighted blend of that point, your hands, the latest flourish and the latest sound (squared weights, so the strongest pull wins) |
| Eye contact locks a spectator | `Core/Crowd/SpectatorMind.cs` | Look at a face for 0.3 s: they watch your face, and keep watching for 1.5 s after you look away |
| Staring too long backfires | same | Past their tolerance (Kid 2.5 s, Skeptic 3 s, Tourist 4 s) they get uncomfortable and look at your hands for 2.5 s |
| Seen methods raise suspicion | same | Each spectator has a cone (Tourist 12°, Kid 14°, Skeptic 10°, Filmer 28°) with a soft edge. A secret move inside it adds suspicion, and carrying the coin inside it adds a little per second. Levels: curious ≥ 0.35 (?), suspicious ≥ 0.7 (?!), walking off ≥ 1.0 (!!). Slow decay |
| Archetypes | `Core/Crowd/SpectatorProfile.cs` | Tourist follows your look. Kid follows flourishes and sounds. Skeptic watches your hands (only eye contact wins her). Filmer's phone covers the mat whatever she looks at. Regular is like the Tourist, with memory to come on Day 10 |
| Show-hand flourish | `Core/Hands/FlourishDetector.cs` | Sweep at least 12 cm, then hold an open palm (raised, facing away from you or up) still for 0.3 s. Never fires on a palm facing you (Meta's system-menu pose), palms down, a fist, or unreliable tracking |

The headline test, `SecretMoveDuringEyeContactWithTheSkepticGoesUnseen`, checks the whole idea: with the Skeptic locked by eye contact, the Tourist and Kid follow your look to her, so a move at your hands is seen by nobody. The same move without eye contact is caught by the Skeptic.

## In the headset

The bootstrap adds the crowd to **Main.unity** next to the ditch practice. In an existing scene, use **Misdirection → Day 3 → Add Crowd to Open Scene**.

1. Calibrate the table (palms flat). A Tourist, a Kid and a Skeptic appear across the table, ±30° from straight ahead.
2. Each spectator shows a faint **Magician's Eye** cone from their eyes along their attention: white normally, blue during eye contact, red when your secret hand (carrying the coin) is inside it. A small blue dot marks where your look lands.
3. Above each head: ◉ = eye contact, ? = curious, ?! = suspicious, !! = about to walk off.
4. The ditch practice HUD now reports **"Nobody saw it"** or **"The Skeptic saw it (83%)"** for each ditch, plus a line with each spectator's focus and suspicion.

## Test protocol · INT + SYS · ~30 min

1. **Gaze follow:** look at the Kid. The Tourist's cone and head should turn to the Kid within about half a second. Look at the mat: they follow.
2. **Eye contact:** look the Skeptic in the face for a second (◉ appears), then look away. Her cone stays on your face for about 1.5 s.
3. **Stare:** keep looking at the Kid for 3 s. He looks away at your hands.
4. **Flourish:** sweep your show hand up and out and hold an open palm for a beat. The Kid's cone jumps to it.
5. **Clean ditch:** pick up the coin, look the Skeptic in the eye, ditch while she's locked. The HUD should say "Nobody saw it". Then ditch while looking at your hands: the Skeptic catches it.
6. Note anything that feels wrong (too slow, too twitchy, cones too wide) in the tuning log.

## Tuning knobs (Inspector on *Misdirection Crowd*)

| Feel | Knob |
|---|---|
| Spectators too far left or right | *Arc Half Degrees* (set from the Day 1 spike's usable arc) |
| Hard to "hit" a face with your look | *Crowd Settings → Face Angle Degrees* up (6 → 8) |
| Crowd reacts too slowly or too twitchily | Per-archetype *Turn Rate* in `SpectatorProfile.For` (code) |
| Eye contact too sticky or too short | *Attention Settings → Lock Hold Seconds* |
| Suspicion too punishing | *Exposure Rate* down; *Attention Settings → Suspicion Decay Per Second* up |
| Flourish too hard to trigger | *Flourish Settings → Sweep Distance* down, *Hold Seconds* down |
| Cones distracting | Untick *Show Cones* (the tutorial will toggle them) |

## Known limits

- Greybox spectators (capsules and spheres). Stylised models and crowd audio are Day 7.
- Spectators stand at a fixed distance from your eyes. MRUK room geometry (open floor, walls) comes on Day 5.
- No voice "Look!" yet (Day 11). `Crowd.MakeSound(...)` is ready for it, and a coin clink already uses it.

## Tuning log

| Date | Tester | Change | Why | Result |
|---|---|---|---|---|
| | | | | |
