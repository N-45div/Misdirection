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

Close-up magicians don't hide the move. They move your attention. A glance at someone's face, a flourish of the empty hand, and the coin in the other hand goes where nobody is looking.

A headset knows where you're looking. So we made that the core verb: in Misdirection, **where you look, they look**. You sit at your own table in passthrough, a small street crowd gathers across it, and their eyes follow yours. Look a spectator in the eye and they lock onto your face; gesture with your show hand and heads turn; meanwhile your secret hand palms, ditches and produces a coin, slowly and low, at your real table edge.

### What it is

- **Your table is the stage.** The Quest's room scan finds your table, and a spatial anchor remembers it next time. Rest your palms on it if there's no scan. Spectators stand on open floor, clear of your walls and furniture.
- **A crowd that pays attention.** Each spectator blends where you look, your flourishes and any sounds. Tourists follow your gaze, the Kid chases movement, the Skeptic watches your hands, the Filmer holds a phone on the table. A method seen inside someone's attention cone makes them suspicious: "Hm!".
- **Two tricks, three acts.** Vanish a coin and pluck it from a spectator's ear by looking at them and pinching; then the coin under the cup. The crowd grows each act; tips and stars at the bow, or a walk-off after three busts.
- **A first five minutes with no wall of text.** Ghost hands show each move. One spectator, the Kid, teaches the rule: look her in the eyes, drop the coin behind the edge, show her your empty hand. She gasps and claps.
- **A reason to come back.** A new crowd and a daily challenge every day, a trick book with mastery stars, a streak, and a returning Regular who remembers how you fooled him yesterday and won't fall for it twice.
- **Accessible by default.** One-handed play switches on by itself (one-palm table setup, one-hand pause and resume). Relaxed timing, captions for the crowd's sounds, high contrast, and an Access Tray you can toggle just by looking at it for 1.5 seconds. An optional "Look!" or clap works as a voice alternative to the flourish gesture.

### How we built it

Unity 6 with Meta XR SDK v207: hand tracking, passthrough, scene understanding (MRUK) and spatial anchors. Head gaze drives the crowd on Quest 3 and 3S, eye gaze where the device has it. We wrote our own hand-tracking rules for magic: a sticky pinch with the coin parented to the wrist, no release decisions while tracking confidence is low, and hands that freeze, fade and blend back when tracking drops. The show pauses when both hands leave view.

The game rules (attention, suspicion, tricks, the show, the tutorial, progress and accessibility) are engine-free C#, with **197 unit tests**. We built it without a headset: Meta XR Simulator 207 with webcam hand tracking and synthetic rooms, plus an automated simulator smoke test on every change. The release build passed Meta's upload validation and security checks.

**TODO (Day 12):** performance and FoV-aware layout, and the Interaction SDK gaze outcome.

### What's next

On-device tuning on Quest 3 and 3S with outside playtesters; more tricks (the French drop, a card force); and **Night Train Con**, a campaign that moves the act onto a night train: coin magic for the passengers who are awake, pickpocketing in time with the sleepers' breathing and gaze, and a briefcase to crack.

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
