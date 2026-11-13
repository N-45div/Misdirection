# Day 11 · Thu Nov 12 · Accessibility

**Exit criterion:** the full show is completable **one-handed**. With no Quest, that is checked in the Meta XR Simulator with one hand in the webcam's view ([NO_HEADSET.md](../NO_HEADSET.md)), and by unit tests that walk both tricks and a whole show through with a single hand.

Written on Nov 13. 49 new unit tests on the core rules (143 in total, all passing) and 3 on the Android manifest. Compiles against Unity 6000.0.66f2 + Meta v207 for the Editor and the Android player. **Not yet run in the simulator**: the checks below are the first hands-on run.

This is the entry's case for Meta's *Best Accessibility Forward Experience* award: voice alternatives for hand interactions, eyes-only navigation, high contrast, inclusive by default.

## What changed

| Feature | What the player gets | Default |
|---|---|---|
| **One-handed play** | Switches itself on after 5–8 s with only one hand in view. One palm sets the table, one hand resumes from a pause, one ghost hand (left or right, matching yours) demonstrates | **Auto** |
| **Relaxed timing** ("longer windows") | Shorter holds, a longer eye-contact window, a crowd that turns more slowly and suspects less | Off |
| **Captions for sound** | `[applause]`, `[gasp]`, `Hm!`, `[murmur]`, `[clink]`, `[chime]`... each shown where the sound comes from | **On** |
| **Access Tray** | Five tokens along the mat's left edge. Look at one for 1.5 s (a ring fills), or poke or pinch it | Always there |
| **High contrast** | Bolder Magician's Eye cones, bold yellow servante outline, a white-and-black rim on the coin, larger `?` `?!` `!!` markers and text | Off |
| **Voice "Look!"** | A short loud sound (a word, a clap, a tap on the table) pulls the crowd's eyes to where you are looking | Off |

Choices are saved on the device and survive restarts.

### One-handed play

- **Switches on by itself.** One hand in view for 5 s while the other has never been seen (8 s if it has), and the game plays one-handed. Both hands steadily in view for 1.5 s switches it off again. The *One hand* token overrides: **auto → on → off**.
- **Table:** rest **one palm** flat with the heel of the hand at the table's edge and hold still for 1 s. The palm gives the height and the distance of the edge; the mat is centred in front of your head, facing the way your head faces, whichever hand you used. Moving a table that is already set takes 5 s **while looking at the palm**, so a hand resting on the table during the show can't move it. (The room scan and the saved anchor still set the table with no hands at all.)
- **Pause:** your hand out of view for 2 s pauses ("the crowd waits"); back for 0.5 s resumes. Before Day 11 a one-handed player could pause the show and never resume it.
- **Ghost hands:** one hand, on your side, for every step. Captions change to "Rest one palm flat on the table" and "Show your hand to carry on".
- **Tricks:** both already ran on one hand, and now a test proves it. Trick 1: pick up, show, ditch, show the same hand empty, steal back, look and pinch to produce. Trick 2: pick up, show, open the hand at the cup's base, show it empty, pinch the cup's top and lift. The hand holding the coin can't also take the cup, so nothing fires by accident.
- **Misdirection with one hand:** the show-hand flourish needs a free hand, so a one-handed player misdirects with **eye contact** (look a spectator in the eyes for 0.3 s and they lock onto you for 1.5 s, or 2.4 s with relaxed timing) or with **voice** (below).

### Relaxed timing

One switch, applied the moment it's toggled:

| What | Normal | Relaxed |
|---|---|---|
| Hold the coin up to show it | 0.6 s | 0.36 s |
| Hold the empty hand up (reveal) | 0.4 s | 0.24 s |
| Look at a spectator before the pluck | 0.4 s | 0.24 s |
| Tutorial: the Kid's eyes off the coin | 0.4 s | 0.24 s |
| Tutorial: "look at her" moves on anyway after | 8 s | 12.8 s |
| Eye contact holds a spectator after you look away | 1.5 s | 2.4 s |
| How fast each spectator's attention turns | ×1 | ×0.6 |
| Suspicion gained from what they see | ×1 | ×0.6 |

Holds never go under 0.2 s: they stay settled states, never instants. Scoring is untouched: how well a method was *seen* is the same, only how fast suspicion builds.

### Captions for sound

Every sound that tells the player something is captioned where it happens: each spectator's `[applause]`, `[gasp]` or `Hm!` in front of their chest (clear of the suspicion marker above their head), `[murmur]` → `[louder murmur]` → `[grumbling]` as their suspicion rises, `[groan]` on a bust, `[clink]` where a coin drops, `[the crowd gathers]`, `[fanfare]`, `[the crowd walks off]` and the mentor's `[chime]` over the table. White text with a black edge; sized to look the same near or far. The *Captions* token also switches the mentor's text hints.

### The Access Tray (eyes-only navigation)

Five tokens in a row just outside the mat's left edge, 7 cm apart, clear of the coin, the cup and the servante. Shape tells them apart; raised, large and bright means on; low, small and grey means off; a two-line label says which and what state.

| Token | Shape | Does |
|---|---|---|
| One hand | Upright pill | auto → on → off |
| Relaxed | Ball | Relaxed timing |
| Captions | Flat bar | Sound captions and mentor text |
| High contrast | Disc with a black centre | Bolder cones, outline, coin, markers, text |
| Voice | Cube on its corner | "Look!" (asks for the microphone) |

- **Look** at a token for 1.5 s: a ring fills clockwise, then it toggles. Uses the same look source as the game (eyes on devices with eye tracking, head otherwise). Brief slips are forgiven; it won't toggle again until you look away. Looking only counts **while the coin rests on the mat or the show is paused waiting for your hands**, never mid-move, and never while the system menu has focus.
- **Poke** it (a pointing finger held on it for 0.5 s) or **pinch** it (about 0.13 s), with either hand, any time that hand isn't carrying the coin. A flat palm resting there does nothing, and a palm-toward-face pinch is ignored (that's the system gesture).
- Before the table is set, the tray waits in front of you where the ghost hands are, so the options can be changed with the eyes alone.

### Voice "Look!"

No speech recognition and nothing is recorded or sent anywhere: the game reads the microphone's loudness only. A sound counts when it is short (30 ms to 0.9 s) and at least four times the room's noise level; it fires when the sound ends, at most once every 1.2 s. The crowd hears a sound at the point you are looking at, the same thing a coin's clink does, and a `Look!` caption appears there. While the game itself is making noise the microphone is ignored, so the headset's speakers can't trigger it.

**Permissions.** The app never asks for the microphone at startup. The system's permission dialog appears once, at the moment the *Voice* token is switched on. Say no (or have no microphone) and the token switches itself back off with a caption saying why. Details in [Permissions at startup](#permissions-at-startup) below.

## Code

- `Core/Access/AccessOptions.cs`: the five options, saved as one string
- `Core/Access/OneHandedDetector.cs`: when one-handed play applies and which hand is playing
- `Core/Access/RelaxedTiming.cs`: the scaling rules and the designed timings they start from
- `Core/Access/DwellSelector.cs`: choosing by looking (or resting a finger)
- `Core/Access/LoudSoundDetector.cs`: hearing a short loud sound above the room's noise
- `Core/Hands/HandPresence.cs`, `Core/Show/ShowPause.cs`: the pause resumes on one hand when one-handed
- `Core/Table/TableFrame.cs`: `FromPalm` (one palm + the head's heading)
- `Runtime/Access/AccessRuntime.cs`: loads, saves and applies the options; publishes them to `AccessSettings`
- `Runtime/Access/AccessSettings.cs`: the shared state the rest of the game reads through one-line hooks
- `Runtime/Access/AccessTray.cs`, `SoundCaptions.cs`, `VoiceLook.cs`
- `Editor/Setup/AccessSetup.cs`: scene setup, menu items, the manifest guard
- Tests: `tools/tests/csharp/{AccessTests, OneHandedTests, RelaxedTimingTests, DwellSelectorTests, VoiceTests}.cs`, `tools/tests/test_android_manifest.py`

**Setup:** the bootstrap adds a **Misdirection Access** object to Show.unity. By hand: open Show.unity → **Misdirection → Day 11 → Add Accessibility (tray, one-handed, captions, voice) to Open Scene** → save. If a scene was saved before Day 11 and nobody has done that, the game adds the layer by itself when the show starts (the log says so), so accessibility never depends on a setup step.

Other menu items: **Misdirection → Day 11 → Reset Access Settings** (back to the defaults) and **Check: No Permission Prompt at Startup**.

## Permissions at startup

- Unity's Android player asks for every "dangerous" permission in the manifest when the app launches, unless the manifest carries `unityplayer.SkipPermissionsDialog = true`. Meta's manifest template sets it to `false`. Voice needs `RECORD_AUDIO` in the manifest, so with the template's value **the judges would have met a microphone prompt on first launch**.
- `Assets/Plugins/Android/AndroidManifest.xml` now has `SkipPermissionsDialog = true`, `RECORD_AUDIO`, and the microphone as an optional feature. Nothing else changed: hand tracking, anchors, scene and eye tracking are all still declared.
- The setting is kept right three ways: the bootstrap fixes it after Meta's Fix All, every Android build fixes it before building (`AccessManifestGuard`), and a unit test fails if the committed manifest loses it.
- **Still asked at startup, as before:** the room scan's spatial-data permission (`USE_SCENE`). `OVRManager` asks for it (*Request Scene Permission On Startup* is ticked in Show.unity) and so does MRUK; that didn't change.
- **No longer asked by anyone:** eye tracking. It was only ever asked for by Unity's automatic dialog. On a headset with eye tracking (Quest Pro) the game now stays on head direction until something asks for `EYE_TRACKING`. Quest 3 and 3S have no eye tracking, so nothing changes there. See the open risks in the hand-over notes.

## Check in the simulator · ~40 min

Run the bootstrap once first (or use the menu item above), then play Show.unity with **Inputs → Camera tracking → Enable camera** (webcam hands).

1. **Tray:** five tokens sit along the left edge of the mat, each a different shape, each with a label. *One hand* says "auto", *Captions* says "on" and is raised and yellow, the others are low and grey.
2. **Eyes-only:** hands out of view (the show pauses after 2 s; the tray works while paused). Turn the simulated head (mouse look) until the *High contrast* token is in the middle of the view; once the crowd is there, the blue gaze dot shows where the look lands. A ring fills in 1.5 s and it toggles: cones get bolder, the servante outline turns thick yellow, the coin gets a white-and-black rim, markers and labels grow. Keep looking: it does not toggle back. Look away and back: it does.
3. **One hand, auto:** start with **one hand only** in the webcam's view. After about 5 s the log says `Access: one-handed play on (Right hand)`, the *One hand* label reads "auto (on)", and the ghost hands become one hand on that side.
4. **One-palm table:** the ghost palm only shows while no table is set, so stop, untick *Table Anchor Store → Restore On Start* (so last run's table isn't restored), pick a synthetic room *without* a table (so the room scan finds none), and play. With one hand in view, wait for the single ghost palm, rest your palm flat and still: the mat appears centred in front of the head. Log: `Table calibrated by one palm (right)`.
5. **One-handed show:** play the tutorial and all three acts with that one hand. Misdirect by looking the spectators in the eyes. The show must reach "Bravo!". This is the exit criterion.
6. **Pause:** take the hand out of view for 3 s. Everything freezes and **one** ghost hand rises with "Show your hand to carry on". Hand back for half a second: it carries on.
7. **Both hands back:** show both hands for 2 s: the log says one-handed play is off and the label goes back to "auto".
8. **Poke and pinch:** with a pointing finger, rest the fingertip on the *Relaxed* token for half a second: it toggles. Pinch it: it toggles faster. Lay a flat palm over the tokens: nothing happens.
9. **Dwell guard:** pick the coin up and stare at a token: no ring. Put the coin down (or let the trick finish): the ring works again.
10. **Relaxed:** switch *Relaxed* on. The coin counts as shown after about a third of a second held up; spectators' heads swing round more slowly.
11. **Captions:** finish a clean vanish: `[applause]` appears in front of each spectator. Drop the coin on the mat: `[clink]` at the coin and `[groan]` from the crowd. Switch *Captions* off: no captions, and no mentor text either.
12. **Voice:** switch *Voice* on (in the Editor it uses the PC's microphone, with no prompt). Look at a spectator and clap or say "Look!": a `Look!` caption appears at the gaze dot and the Kid's eyes go there. Talk continuously: nothing fires. Unplug or disable the microphone and switch it on: the token drops back to off with "No microphone found: voice is off".
13. **Saved:** stop and play again: the choices are still there. **Misdirection → Day 11 → Reset Access Settings** puts them back.
14. Automated: `tools/sim_smoke.sh`. The report now has `one_handed` (false with no hands in view) and `access_settings` (`one=auto;relaxed=0;captions=1;contrast=0;voice=0` on a clean run; the smoke test resets the options first).

## One-hand test (PRO)

Someone who has not played, using one hand only (the other in a pocket), webcam hands, simulator running. Say only "it's a magic game, use your hand". Note in `docs/results/day-11-one-handed.md`:

- Time until the game switched to one-handed by itself, and whether they noticed
- Whether the single ghost palm was understood (one-palm table), or whether the room scan had already set the table
- Did they finish the show? Where did one hand feel worse than two?
- How did they misdirect: eye contact, voice, or neither?

## Tuning knobs

| Where | Knob | Now |
|---|---|---|
| Misdirection Access → Access Runtime → One Handed | Auto After / Never Seen After / Both Back / Switch Hand (s) | 8 / 5 / 1.5 / 0.5 |
| ... → Relaxed | Hold Scale, Min Hold, Window Scale, Turn Rate Scale, Suspicion Scale | 0.6, 0.2 s, 1.6, 0.6, 0.6 |
| ... → Access Tray | Edge X, First Z, Spacing (m) | −0.255, 0.11, 0.07 |
| ... → Access Tray | Dwell Seconds, Gaze Degrees | 1.5 s, 3.5° |
| ... → Access Tray | Poke Seconds, Touch Radius, Poke Max Openness | 0.5 s, 3.5 cm, 0.6 |
| ... → Voice Look → Settings | Loud Ratio, Min Level, Max Loud, Refractory | ×4, 0.04, 0.9 s, 1.2 s |
| ... → Voice Look | Game Sound Level (microphone ignored above this) | 0.06 |
| Table Calibrator | One Palm Recalibrate Hold / Look Degrees | 5 s / 20° |
| `AccessSettings.cs` | Cone width ×1.25 and alpha ×3, marker ×1.7, text ×1.3, outline 8 mm yellow | high contrast |

## Known limits

- **Nothing here has been seen running yet.** Token sizes, label sizes, caption positions and the dwell angle are first guesses to tune in the simulator.
- Auto one-handed can switch on for a two-handed player who keeps one hand out of view for 8 s. Nothing breaks (two hands still work for everything), and it switches off when both hands are back. Set *One hand* to off to stop it.
- With one-handed play on, a hand the cameras lost a while ago still counts toward where the crowd thinks "the hands" are (its frozen last pose). The crowd's default gaze can sit a little off the playing hand. Same as two-handed play with a hand in the lap.
- Voice hears loudness, not words: a cough, a door or someone else talking nearby can trigger it. That is why it's off by default. Sounds the game doesn't make loudly enough to notice (very quiet ones) can't suppress the microphone, but they're also too quiet to trigger it.
- Captions cover the crowd, the coin's clink and the show's cues. The pickup tick, the ditch thud and the production sparkle have no caption: each already has a visible partner (the coin moves, vanishes, flies).
- The tray's label and state are English text. The shapes, size and height carry the meaning without it.
- High contrast doesn't restyle the spectators, the mat or the ghost hands.
- Left-handed *two-handed* players still see right-handed ghost hands (they demonstrate a single hand's moves; either hand can do them).
