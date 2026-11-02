# Day 2 · Tue Nov 3 · Secret-hand verbs

**Exit criterion:** the ditch succeeds about **19 times in 20** for **two testers**.

The code was written early (Nov 2) while Day 1 waited on a headset and a Unity licence. It compiles against the real Unity 6000.0.66f2 + Meta XR SDK v207.0.1, and its rules have 21 unit tests. **It has not run on a headset yet**, so today is about feel and tuning, not writing it from scratch.

## What's in the build

| Piece | Where | What it does |
|---|---|---|
| Hand presence | `Scripts/Core/Hands/HandPresence.cs` | Tracked → uncertain (tint) → lost (freeze + fade) → recovering (blend back over 0.25 s). No grab or release until a hand has been steadily tracked for 0.15 s |
| Sticky grip | `Scripts/Core/Hands/PinchGrip.cs` | Grab: pinch ≥ 0.8 held 60 ms on the coin. Release: fingers apart (past the grab-time gap + 2 cm, or pinch < 0.35) **and** hand open (≥ 0.65), held 80 ms. A fist (palmed coin) never releases. Low confidence never releases |
| Table + servante | `Scripts/Core/Table/TableFrame.cs` | Table frame from both palms flat at the near edge. Servante = box along the near edge (60 cm wide, 14 cm toward you to 5 cm onto the table), with 2 cm hysteresis |
| Coin + trials | `Scripts/Core/Tricks/Coin.cs` | Resting → held → ditched / exposed. Every attempt logged; last-20 score shown live |
| Unity side | `Scripts/Runtime/Hands/TrackedHand.cs`, `Table/TableCalibrator.cs`, `Tricks/CoinActor.cs`, `Tricks/DitchPractice.cs` | Reads OVRHand/OVRSkeleton (both bone layouts), stable wrist/palm pose, mat and servante outline, HUD, placeholder sounds, CSV log |

The bootstrap puts all of this in **Main.unity**. In an existing scene, use **Misdirection → Day 2 → Add Ditch Practice to Open Scene**.

## Build

The ditch practice lives in Main, so build Main first today:

```bash
MISDIRECTION_SCENES=Assets/Misdirection/Scenes/Main.unity tools/build_apk.sh --dev
```

```bash
tools/install_quest.sh
```

Or run the **build-apk** GitHub Action (once the Unity licence secret exists) and install the artifact.

## Test protocol · INT + 2 testers · ~30 min each

Seated at a real table, controllers off, normal light.

1. **Calibrate.** Rest both palms flat on the table, heels of the hands at the near edge, and hold still. A progress bar fills in about 1 s. The dark mat appears with the coin on it; the purple outline is the servante.
2. **Ditch.** Pinch the coin and pick it up. Close the hand around it (palm it) or keep pinching. Carry it to the near edge and open the hand inside the outline. Ditch = low thud. Exposed = bright clink, and the coin lies on the table.
3. **Repeat 25+ times per tester.** Mix it up:
   - 10 with a pinch carry, 10 with a palmed (fist) carry
   - 5+ with your head turned about 30° toward where the crowd will stand (no looking at your hand)
4. The HUD shows the last-20 score and both hands' state, pinch and openness. The wrist dots show tracking: green tracked, amber uncertain, red lost, blue recovering.
5. Recalibrate any time the coin is on the mat: hold palms flat for 3 s.

Pull and analyse per tester (headset connected):

```bash
tools/pull_logs.sh ditch alice
```

Paste the report into `docs/results/day-02-ditch.md` and commit it.

## Tuning knobs (Inspector, no code changes)

| Symptom | Knob |
|---|---|
| Coin drops when closing the hand | *DitchPractice → Grip → Open Threshold* up (0.65 → 0.75), or *Release Margin* up |
| Opening the hand doesn't release | *Open Threshold* down, or *Release Hold Seconds* down |
| Hard to pick the coin up | *Grab Radius* up (4.5 → 6 cm), *Grab Strength* down (0.8 → 0.7) |
| Releases just outside the zone | *TableCalibrator → Servante → Near Z / Far Z / Half Width* |
| Coin jitters in the hand | *TrackedHand → Held Smoothing Rate* down |
| Table calibration won't trigger | *TableCalibrator → Min Openness* down, *Max Rest Speed* up |

Write every change and the reason below, so the defaults in code can be updated.

## Known limits (by design, for later days)

- The SDK hand mesh still hides itself on low confidence (Meta's default). Our own freeze-and-fade hand visual is Day 8 polish. The **coin** already freezes and blends correctly.
- Table calibration is the palms gesture only. MRUK table detection and spatial anchors arrive on Day 5, and the gesture stays as the no-table fallback.
- Sounds are generated placeholders until the art/audio pass.

## Also today

- **SYS:** open item #15 (Meta XR Operator with OVRPlugin microgestures), device test.
- **ART:** coin model and the four sounds (pickup, palm, servante drop, reveal), plus a hand-tint material.
- **PRO:** recruit 3+ outside playtesters for Thursday's fun gate. Open item #10.

## Exit checklist

- [ ] Two testers at ≥ 19/20 in `tools/pull_logs.sh ditch` reports
- [ ] Tuning changes recorded here and copied into code defaults
- [ ] `docs/results/day-02-ditch.md` committed

## Tuning log

| Date | Tester | Change | Why | Result |
|---|---|---|---|---|
| | | | | |
