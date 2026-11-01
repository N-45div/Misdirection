# Misdirection: design summary

Condensed from the team's research brief. The brief holds the evidence and sources; this page holds the decisions.

## Pitch

Seated, hands-only, mixed-reality street magic. You sit at your own real table in passthrough. Three to five stylised spectators gather on the far side. Your gaze and your show hand steer their attention while your secret hand palms, ditches and produces coins.

**"Where you look, they look."** Gaze stops being a menu pointer and becomes the core verb. That makes the one capability the 2026 rubric names with an SDK version (ISDK v207 gaze) the heart of the game, and fits the Boldest Original Concept definition: a loop that "depends on presence, spatial depth, real-world scale, or eyes and hands".

## Core loop: one trick, 60–90 seconds

| Beat | Player does | Crowd does |
|---|---|---|
| Engage | Shows a coin | Eyes go to the coin |
| Misdirect | Looks a spectator in the face (locks them about 2 s), sweeps an open show hand toward something and holds it a beat, or says "Look!" | Each spectator's attention point drifts toward a weighted blend of your gaze, the latest flourish and the latest sound |
| Execute | Secret hand does the method while no spectator's attention cone covers it: palm, ditch into the servante (hidden zone along the near table edge), or load under a cup | A method caught inside a cone raises that spectator's suspicion |
| Reveal | Opens the empty show hand, or looks at a spectator and pinches to produce the coin from behind their ear | Amazement, paid as tips, scales with how cleanly the method stayed out of every cone |

Three tricks make a 6–8 minute show (Meta: "satisfying in ≤10 minutes"). Too much suspicion means heckles, then a walk-off. Staring at one spectator too long makes them uncomfortable, and they look away, often at your hands. **The player creates each window rather than reacting to one**, so there's no reflex timing.

## Spectator archetypes

| Archetype | Behaviour |
|---|---|
| Tourist | Follows your gaze faithfully |
| Kid | Follows sounds and flourishes |
| Skeptic | Watches your hands whatever you do. Win her with eye contact |
| Filmer | Holds a phone with a wide, fixed attention cone |
| Regular | Returns on later days and remembers the methods you used, which forces variety |

## Hand-tracking rules (non-negotiable)

These come from Meta's guidance and from shipped hand-tracking games.

- Read **held or settled** states, never instants. Tolerate about 2 cm of slop.
- Secret moves are **slow, low and centred**, in front of the chest. No fast motion, no throw timing.
- Never use a **palm-facing pinch** as input (it's the system menu gesture).
- Held coins are parented to the **wrist/knuckles**, not fingertips. The release margin is set from thumb–index distance at grab time.
- **No release decisions during low confidence.** A held coin stays held.
- On tracking loss the hand **freezes, fades, then blends back** when tracking returns. Nothing unexpected ever happens.
- Hands **tint** as confidence drops or near the edge of the tracking volume.
- Bias detection toward **missing an input rather than firing the wrong one**.
- No interactions in the 0.5–0.8 m middle distance. Direct-touch UI sits at 42–46 cm; gaze UI at 0.8–3 m. Gaze targets are at least 3° (spectator faces are larger).
- Every state has paired **visual and audio** feedback, because hands have no haptics.
- If both hands vanish for more than 2 s, the show pauses ("the crowd waits"). This is also our clean pause/resume.

## Feature map to judging criteria

Each criterion is worth 25%: Innovation, Experience Design, Technical Implementation, Polish & Presentation.

| Feature | Implementation | Serves |
|---|---|---|
| Gaze misdirection | One `LookSource` layer: eye gaze on VR Glasses / Quest Pro, head direction on Quest 3/3S | Innovation, Technical (gaze), Boldest Original |
| Produce from a spectator | ISDK v207 gaze interactor + pinch on spectators 1–1.5 m away, face hit areas > 3° | Technical (gaze), First Five Minutes |
| Palm and sticky hold | ISDK hand grab, coin parented to wrist, release margin set at grab, no release in low confidence | Technical (hands) |
| Servante ditch | Trigger volume along the near table edge, about 35–45 cm from the eyes | Experience Design, airplane-seat test |
| Show-hand flourish | Open palm aimed at a target, held about 0.3 s | Experience Design |
| Real table as stage | MRUK table detection (MR Utility Kit block), table-surface placement, persistent spatial anchor, palms-flat calibration | Technical (passthrough, MRUK, anchors), purposeful passthrough |
| Crowd placement | MRUK room geometry; spectators fill open floor; the arc tightens near walls | Technical (untested rooms) |
| FoV-aware layout | Crowd arc set from the Day 1 spike (target about ±30°); cues live on spectators and the mat; OVRManager glasses-FoV simulation | Experience Design, Technical (FoV) |
| Microgesture scrolling | Trick book | Technical, Accessibility |
| Daily crowd and progression | Daily-seeded archetypes, trick book with mastery stars, Platform SDK leaderboard + achievements | Reason to Come Back |
| Crowd as spatial agents | Perception, attention, memory, gossip | Agentic Interaction (moderate) |
| Accessibility layer | One-handed mode, voice "Look!", gaze-dwell menus, longer windows, captions, shape-coded suspicion icons | Accessibility Forward |

An LLM heckler is a stretch goal after feature freeze at most. It stays off the critical path.

## First five minutes (no text)

Launch straight into passthrough → mentor ghost hands show palms resting on the table → the mat unfurls under your palms (calibrates table height, checks lighting) → mentor pinches a coin and you copy → the Kid wanders up → "look at her": the Kid's eyes follow your gaze, drawn as a visible **Magician's Eye** cone → "drop it behind the edge" → "show her your hand" → the Kid gasps and applauds. **First applause within 2 minutes.**

## Never cut

Hands end to end · onboarding · tracking-loss handling · pause/resume · 60 fps minimum (target 72 on Quest 3S) · table placement with fallback · the gaze-misdirection core.

## Cut order (fixed in advance)

1. Day 6 slips → drop cups and balls. The show becomes vanish, produce and a variation.
2. Day 10 slips → a local daily best and the trick book replace Platform SDK leaderboards and achievements.
3. Day 11 slips → drop voice and gaze-dwell menus. Keep one-handed mode, longer windows and captions.
4. Day 13 placement trouble → the crowd locks to a shallow arc behind the table that tightens when MRUK finds a wall.

## Pivot rule

If Gate 2 (Day 4 fun gate) fails, **or** even a narrowed arc loses the secret hand on more than about 1 move in 10, switch to **Kolam Dawn** on Day 5. Kolam Dawn is pinch-trickled rice-flour dot-loop puzzles on your real table. It reuses the table placement, pinch handling, onboarding pattern and daily seed already built. Kolam's mathematical rules need checking with a practitioner before they go in the pitch.

## Future plans (Devpost only)

**Night Train Con**: a campaign mode that adds coin magic for awake passengers, pickpocketing in time with sleepers' breathing and gaze, and briefcase safecracking. It's out of scope for this sprint.
