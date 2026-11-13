# Trailer (under 3:00)

The rules allow footage "as viewed on a Meta Quest device or via XR Simulator or another equivalent emulator" (Rules §4). Judging: "show real gameplay that honestly represents the experience. Lead with the story and what makes your project distinct. Don't lean on AI-generated video." Judges don't have to watch past 3:00, so the core rule must land in the first 30 seconds. Upload to YouTube as **Public** (not Unlisted) and check the link logged out.

**The split screen is the product spec:** the game view of the crowd's eyes next to a camera on the player's real hands.

## Shot list

Every claim on screen must be true of the build we submit. No frame-rate numbers we haven't measured on a Quest.

| Time | Shot | What it proves |
|---|---|---|
| 0:00–0:08 | Cold open: the player looks a spectator in the eye, the coin vanishes, the crowd gasps. Title: "Where you look, they look" | Hook, innovation |
| 0:08–0:30 | Split screen: game view + phone camera on the real hands, Magician's Eye cones following the player's look | The core rule in 20 seconds |
| 0:30–1:05 | First launch: ghost hands rest on the table, the mat appears under your palms, the Kid wanders up, "look her in the eyes", first applause. Captions show it needs no reading | Best First Five Minutes |
| 1:05–1:45 | A show beat: produce the coin from a spectator's ear by look + pinch; then a careless ditch and the Skeptic points "Hm!" | Core loop and stakes |
| 1:45–2:05 | Robustness: both hands leave view, the show pauses, hands back and it resumes; a second synthetic room; the table found by the room scan | Technical Implementation |
| 2:05–2:25 | Day two: a different crowd, the returning Regular, the trick book's stars; one-handed play and the access tray | Reason to Come Back, Accessibility |
| 2:25–2:45 | Tech card over a simulator clip in the Meta VR Glasses profile: "Hands only · Unity 6 + Meta XR SDK v207 · scene understanding + spatial anchors · FoV-aware · built and tested without a headset" | Rubric checklist |
| 2:45–2:55 | End card: title, "Meta VR Start Developer Competition 2026" | |

Rows 1:45 and 2:05 depend on the Day 10–12 features landing; drop a row rather than show something the build can't do.

## How to film it (no headset)

1. **Game view.** Play Show.unity in the Meta XR Simulator (Quest 3S device profile, a synthetic room with a table, webcam hand tracking on). Record the simulator window with **Windows Game Bar** (Win + Alt + R starts and stops; clips go to `Videos\Captures`). The simulator 207 also has its own MP4 recording; either is fine. Record each row as its own short clip.
2. **Real hands.** The webcam is busy tracking your hands, so film them with a **phone** on a stand, above and behind your shoulder, so the table and both hands are in frame. Clap once at the start of each take: the spike lines up the two clips when editing.
3. **Glasses profile clip.** For the tech card, switch the simulator's **Device** to **Meta VR Glasses** (Inputs → Device info) and record 10 seconds of a show.
4. **Edit** in **Clipchamp** (free, built into Windows 11): 1920×1080, the two clips side by side for the split-screen rows, burned-in captions (Clipchamp can auto-caption, then correct them), music from Clipchamp's royalty-free library (keep a note of the track names in `docs/trailer/music.md`). Export 1080p.
5. **Upload** to YouTube as Public, title "Misdirection: where you look, they look (Meta VR Start Developer Competition 2026)". Open the link in a private window to check it plays logged out. Paste it into the Devpost form.

## Checklist

- [ ] Under 3:00 (aim for 2:45)
- [ ] Core rule clear by 0:30
- [ ] Only real gameplay from the submitted build; no AI-generated video
- [ ] Captions on every spoken or sound-led moment
- [ ] Music licence noted in `docs/trailer/music.md`
- [ ] Public on YouTube, link works logged out
