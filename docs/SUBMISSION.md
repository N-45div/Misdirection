# Devpost submission (draft)

Copy each field into the [Devpost form](https://start-developer-competition-26.devpost.com/). Lines marked **TODO** need a decision or a link first. Mark the project **Submitted**, not Draft, and check every link in a logged-out window. Edits are allowed until Nov 18, 12:00 PT (Nov 19, 01:30 IST); after that, change nothing until winners are announced.

## Fields

| Field | Value |
|---|---|
| Submission name | Misdirection (**TODO:** confirm after the name check, open item #17) |
| Tagline (≤140 characters) | Street magic at your real table: where you look, they look. Steer a crowd with your gaze while your other hand does the trick. |
| Track | Gaming |
| Division | New Experience |
| Release channel link | **TODO:** Invite URL of the "Competition" channel |
| Video | **TODO:** public YouTube link (under 3:00) |
| Target launch date | **TODO:** team decision (suggested: Meta Horizon Store, Q2 2027) |
| Team | **TODO:** names, emails and roles of each member |

## Description

### Inspiration

Close-up magicians don't hide the move. They move your attention: a glance at someone's face, a flourish of the empty hand, and the coin goes where nobody is looking. A headset knows where you look, so we made that the core verb. In Misdirection, **where you look, they look**.

### What it is

You sit at your own table in passthrough while a small street crowd gathers across it, and their eyes follow yours. Look a spectator in the eye and they lock onto your face; flourish your show hand and heads turn; meanwhile your secret hand palms, ditches and produces a coin, low and slow, at your real table edge. A move seen inside someone's attention cone makes them suspicious: "Hm!"

- **Your table is the stage.** The room scan finds it, a spatial anchor remembers it, and palms on the table work without a scan. Spectators stand clear of your walls and furniture.
- **Two tricks, three acts.** Vanish a coin and pluck it from a spectator's ear by looking at them and pinching; then the coin under the cup. The crowd grows each act: tips and stars at the bow, or a walk-off after three busts.
- **A first five minutes with no wall of text.** Ghost hands show each move, and the Kid teaches the rule until she gasps and claps.
- **A reason to come back.** A new crowd and challenge every day, a trick book with mastery stars, a streak, and a Regular who remembers how you fooled him yesterday.
- **Accessible by default.** One-handed play switches on by itself. Relaxed timing, captions, high contrast, an Access Tray you can toggle just by looking at it, and an optional "Look!" or clap instead of the flourish.

### How we built it

Unity 6 and Meta XR SDK v207: hand tracking, passthrough, scene understanding (MRUK), spatial anchors, and **Interaction SDK gaze**: every face is a `GazeInteractable`, hovered by a `GazeInteractor` that follows your head on Quest 3 and 3S. Our own hand-tracking rules make magic dependable: a sticky pinch, no release while tracking is unsure, hands that freeze and blend back, and a pause when both hands leave view.

Shared materials keep a full scene at 46 draw calls, with dynamic foveation at 72 Hz, and essential UI eases back into a 70° × 66° view when you look away. The rules are engine-free C# with **225 unit tests**. We had no headset, so we built and tested in Meta XR Simulator 207 with webcam hands and synthetic rooms, with an automated smoke test after every change. Release builds pass Meta's upload checks.

### What's next

Tuning on Quest 3 and 3S with outside playtesters, more tricks, and **Night Train Con**: coin magic for a night train's passengers, pickpocketing in time with the sleepers' breathing, and a briefcase to crack.

## Hand interactions (optional field)

Everything is hands only: no controller is ever needed. Pinch to pick up the coin; hold it up to show it; open your hand just past the table edge to ditch it; show your empty hand palm-out to reveal; look at a spectator and pinch near their ear to produce the coin; pinch the cup's top to lift it. Resting your palms on the table sets the table; resting one palm works for one-handed players. Every state has a sound and a visual, because hands have no haptics. We never use a palm-facing pinch (the system gesture) as input.

**The future of VR (optional):** headsets that know where you look let a game read intent, not just input. Misdirection treats your attention as the controller: a mechanic that couldn't exist on a flat screen, and one that works with one hand, or almost none.

## Before pressing Submit

- [ ] Release channel link opens and installs on a second Meta account (ask someone outside the team)
- [ ] The whole show is playable with controllers off
- [ ] Video is public, under 3:00, and plays logged out
- [ ] Track: Gaming. Division: New Experience
- [ ] The submitter is a Start member with Developer Access enabled
- [ ] Status shows **Submitted**
