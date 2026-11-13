// Unit tests for one-handed play (Day 11): the detector, the one-handed pause, one-palm table
// calibration, and both tricks walked through with a single hand.
using System;
using System.Numerics;
using Misdirection.Core.Access;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Show;
using Misdirection.Core.Table;
using Misdirection.Core.Tricks;

public static class OneHandSim
{
    public const double Dt = 1.0 / 72.0;

    /// <summary>Feeds the same hands for <paramref name="seconds"/>; returns true if Active changed along the way.</summary>
    public static bool Run(OneHandedDetector d, ref double t, double seconds, bool left, bool right)
    {
        bool changed = false;
        int frames = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
        {
            t += Dt;
            changed |= d.Update(t, left, right);
        }
        return changed;
    }
}

public static class OneHandedDetectorTests
{
    [Test]
    public static void OneHandNeverJoinedByTheOtherSwitchesOnByItself()
    {
        var d = new OneHandedDetector();
        double t = 0;
        Assert.False(OneHandSim.Run(d, ref t, 4.5, false, true), "not yet");
        Assert.False(d.Active);
        Assert.True(d.AutoProgress > 0.8f, "progress " + d.AutoProgress);
        Assert.True(OneHandSim.Run(d, ref t, 1.0, false, true), "on after 5 s when the other hand was never seen");
        Assert.True(d.Active);
        Assert.Equal(HandId.Right, d.ActiveHand);
    }

    [Test]
    public static void AHandThatWasSeenBeforeTakesLongerToCountAsGone()
    {
        var d = new OneHandedDetector();
        double t = 0;
        OneHandSim.Run(d, ref t, 1.0, true, true);
        OneHandSim.Run(d, ref t, 7.0, true, false);
        Assert.False(d.Active, "7 s is not enough once both hands have been seen");
        OneHandSim.Run(d, ref t, 1.5, true, false);
        Assert.True(d.Active, "8 s is");
        Assert.Equal(HandId.Left, d.ActiveHand);
    }

    [Test]
    public static void TwoHandedPlayNeverSwitchesItOn()
    {
        var d = new OneHandedDetector();
        double t = 0;
        for (int i = 0; i < 10; i++)
        {
            // The secret hand keeps dipping out of view for a few seconds at a time.
            OneHandSim.Run(d, ref t, 3.0, true, true);
            OneHandSim.Run(d, ref t, 4.0, false, true);
        }
        Assert.False(d.Active);
    }

    [Test]
    public static void BothHandsSteadilyBackSwitchesItOff()
    {
        var d = new OneHandedDetector();
        double t = 0;
        OneHandSim.Run(d, ref t, 6.0, false, true);
        Assert.True(d.Active);
        OneHandSim.Run(d, ref t, 1.0, true, true);
        Assert.True(d.Active, "a glimpse of a second hand changes nothing");
        OneHandSim.Run(d, ref t, 3.0, false, true);
        Assert.True(d.Active, "still one-handed");
        Assert.True(OneHandSim.Run(d, ref t, 2.0, true, true), "two hands for 1.5 s: off");
        Assert.False(d.Active);
    }

    [Test]
    public static void HandsOutOfViewKeepTheCountAndTheMode()
    {
        var d = new OneHandedDetector();
        double t = 0;
        OneHandSim.Run(d, ref t, 3.0, false, true);
        OneHandSim.Run(d, ref t, 10.0, false, false);
        Assert.False(d.Active, "nothing in view is not one hand in view");
        OneHandSim.Run(d, ref t, 2.5, false, true);
        Assert.True(d.Active, "3 s + 2.5 s of one hand");
        OneHandSim.Run(d, ref t, 30.0, false, false);
        Assert.True(d.Active, "stays on while the hand is away (the pause needs it to resume)");
        Assert.Equal(HandId.Right, d.ActiveHand, "remembers which hand");
    }

    [Test]
    public static void ManualOnAndOffOverrideAuto()
    {
        var d = new OneHandedDetector();
        d.Mode = OneHandMode.On;
        Assert.True(d.Update(0, true, true), "on at once, even with two hands");
        Assert.True(d.Active);
        d.Mode = OneHandMode.Off;
        double t = 0;
        OneHandSim.Run(d, ref t, 20.0, false, true);
        Assert.False(d.Active, "off stays off");
        d.Mode = OneHandMode.Auto;
        d.Update(t + OneHandSim.Dt, false, true);
        Assert.True(d.Active, "auto picks up what it saw meanwhile");
    }

    [Test]
    public static void ThePlayingHandFollowsTheOnlyHandInView()
    {
        var d = new OneHandedDetector();
        d.Mode = OneHandMode.On;
        double t = 0;
        Assert.Equal(HandId.None, d.ActiveHand, "nothing seen yet");
        OneHandSim.Run(d, ref t, 0.1, true, false);
        Assert.Equal(HandId.Left, d.ActiveHand, "first hand seen");
        OneHandSim.Run(d, ref t, 0.3, false, true);
        Assert.Equal(HandId.Left, d.ActiveHand, "a flicker of the other hand doesn't swap");
        OneHandSim.Run(d, ref t, 0.4, false, true);
        Assert.Equal(HandId.Right, d.ActiveHand, "swapped after half a second");
        OneHandSim.Run(d, ref t, 5.0, true, true);
        Assert.Equal(HandId.Right, d.ActiveHand, "both in view: keep the choice");
    }

    [Test]
    public static void ALongFrameCannotSwitchItOn()
    {
        var d = new OneHandedDetector();
        d.Update(0, false, true);
        d.Update(60, false, true);
        Assert.False(d.Active, "one 60 s frame is not 60 s of evidence");
    }
}

public static class OneHandedPauseTests
{
    [Test]
    public static void OneHandBackResumesAOneHandedPlayer()
    {
        var pause = new BothHandsPause();
        pause.OneHanded = true;
        double t = 0;
        for (; t < 1.0; t += OneHandSim.Dt) pause.Update(t, false, true);
        Assert.False(pause.Paused, "playing with one hand");
        for (double end = t + 1.9; t < end; t += OneHandSim.Dt) pause.Update(t, false, false);
        Assert.False(pause.Paused, "hand gone, not yet 2 s");
        for (double end = t + 0.3; t < end; t += OneHandSim.Dt) pause.Update(t, false, false);
        Assert.True(pause.Paused, "paused after 2 s without the hand");
        for (double end = t + 0.4; t < end; t += OneHandSim.Dt) pause.Update(t, false, true);
        Assert.True(pause.Paused, "back but not settled");
        for (double end = t + 0.2; t < end; t += OneHandSim.Dt) pause.Update(t, false, true);
        Assert.False(pause.Paused, "resumed with the one hand back for 0.5 s");
    }

    [Test]
    public static void EitherHandResumesAndAFlickerDoesNot()
    {
        var pause = new BothHandsPause();
        pause.OneHanded = true;
        double t = 0;
        for (; t < 2.5; t += OneHandSim.Dt) pause.Update(t, false, false);
        Assert.True(pause.Paused);
        for (double end = t + 0.3; t < end; t += OneHandSim.Dt) pause.Update(t, true, false);
        for (double end = t + 0.1; t < end; t += OneHandSim.Dt) pause.Update(t, false, false);
        for (double end = t + 0.3; t < end; t += OneHandSim.Dt) pause.Update(t, true, false);
        Assert.True(pause.Paused, "0.3 s, a gap, 0.3 s: not settled");
        for (double end = t + 0.3; t < end; t += OneHandSim.Dt) pause.Update(t, true, false);
        Assert.False(pause.Paused, "the left hand alone resumes too");
    }

    [Test]
    public static void SwitchingOneHandedOnWhilePausedLetsTheWaitingHandResume()
    {
        // A one-handed player paused before the game noticed: stuck until one-handed play switches on.
        var pause = new ShowPause();
        Assert.False(pause.Update(0, false, true, true), "playing");
        pause.Update(0.5, false, false, true);
        Assert.True(pause.Update(2.6, false, false, true), "paused");
        pause.Update(3.0, false, true, true);
        Assert.False(pause.Update(9.0, false, true, true), "two-handed rules: one hand never resumes");
        Assert.True(pause.Paused);
        pause.OneHanded = true;
        pause.Update(9.1, false, true, true);
        Assert.True(pause.Update(9.7, false, true, true), "resumed 0.5 s after one-handed play came on");
        Assert.Equal(PauseReason.None, pause.Reason);
    }

    [Test]
    public static void FocusLossStillPausesAOneHandedPlayer()
    {
        var pause = new ShowPause();
        pause.OneHanded = true;
        Assert.True(pause.Update(0, false, true, false), "focus lost");
        Assert.Equal(PauseReason.FocusLost, pause.Reason);
        Assert.True(pause.Update(1, false, true, true), "focus back");
        Assert.False(pause.Paused);
    }
}

public static class OnePalmTableTests
{
    static readonly Vector3 Head = new Vector3(0f, 1.20f, -0.30f);

    [Test]
    public static void OnePalmGivesHeightAndDepthTheHeadGivesTheRest()
    {
        // Right palm resting 18 cm to the right of the body's middle, 40 cm in front of the head.
        var frame = TableFrame.FromPalm(new Vector3(0.18f, 0.775f, 0.10f), Head, new Vector3(0f, -0.5f, 1f));
        Assert.True(frame.IsValid);
        Assert.Near(0.75, frame.Origin.Y, 1e-5, "surface just under the wrist");
        Assert.Near(0.0, frame.Origin.X, 1e-5, "centred in front of the head, not on the palm");
        Assert.Near(0.10, frame.Origin.Z, 1e-5, "near edge through the heel of the hand");
        Assert.Near(1.0, frame.Forward.Z, 1e-5, "faces the way the head faces");
        Assert.Near(1.0, frame.Right.X, 1e-5, "right");
    }

    [Test]
    public static void LeftAndRightPalmsPlaceTheSameTable()
    {
        Vector3 fwd = new Vector3(0f, -0.4f, 1f);
        var right = TableFrame.FromPalm(new Vector3(0.2f, 0.775f, 0.10f), Head, fwd);
        var left = TableFrame.FromPalm(new Vector3(-0.2f, 0.775f, 0.10f), Head, fwd);
        Assert.Near(right.Origin.X, left.Origin.X, 1e-5, "x");
        Assert.Near(right.Origin.Z, left.Origin.Z, 1e-5, "z");
        Assert.Near(right.Forward.Z, left.Forward.Z, 1e-5, "forward");
    }

    [Test]
    public static void RotatedPlayerGetsARotatedTable()
    {
        // Sitting at the origin facing +X; the palm is 40 cm ahead and 15 cm to the left (+Z is left when facing +X).
        var frame = TableFrame.FromPalm(new Vector3(0.40f, 0.75f, 0.15f), new Vector3(0f, 1.2f, 0f), new Vector3(1f, -0.3f, 0f));
        Assert.Near(1.0, frame.Forward.X, 1e-5, "forward +X");
        Assert.Near(0.40, frame.Origin.X, 1e-5, "40 cm ahead");
        Assert.Near(0.0, frame.Origin.Z, 1e-5, "centred on the head");
        Vector3 palmLocal = frame.ToLocal(new Vector3(0.40f, 0.75f, 0.15f));
        Assert.Near(-0.15, palmLocal.X, 1e-5, "the palm sits left of centre in table terms");
        Assert.Near(0.0, palmLocal.Z, 1e-5, "on the near edge");
    }

    [Test]
    public static void LookingStraightDownFallsBackToTheDirectionOfThePalm()
    {
        var frame = TableFrame.FromPalm(new Vector3(0f, 0.775f, 0.10f), Head, new Vector3(0.01f, -1f, -0.01f));
        Assert.True(frame.Forward.Z > 0.99f, "forward " + frame.Forward);
        Assert.Near(0.10, frame.Origin.Z, 1e-5, "edge at the palm");
    }

    [Test]
    public static void ThePalmCannotPutTheEdgeBehindThePlayer()
    {
        // Palm beside the hip, level with the head front-to-back: the edge still lands in front.
        var frame = TableFrame.FromPalm(new Vector3(0.3f, 0.775f, -0.32f), Head, new Vector3(0f, 0f, 1f));
        Assert.Near(-0.15, frame.Origin.Z, 1e-5, "15 cm in front of the head at least");
        Assert.Near(0.0, frame.Origin.X, 1e-5, "still centred");
    }

    [Test]
    public static void TheServanteStillSitsAlongTheNearEdge()
    {
        var frame = TableFrame.FromPalm(new Vector3(0.18f, 0.775f, 0.10f), Head, new Vector3(0f, -0.5f, 1f));
        var zone = new ServanteZone();
        Assert.True(zone.Update(frame.ToLocal(new Vector3(0.05f, 0.72f, 0.04f))), "just past the edge, below the surface");
        zone.Reset();
        Assert.False(zone.Update(frame.ToLocal(new Vector3(0.0f, 0.78f, 0.34f))), "the middle of the mat is not the servante");
    }
}

/// <summary>
/// One hand, the real rules: a single pinch grip, the coin model, the servante and each trick,
/// driven the way the trick directors drive them. The other hand never appears.
/// </summary>
public static class OneHandedTrickTests
{
    static readonly HandShape Pinched = HandShape.Pinch(0.95f, 0.010f, 0.3f);
    static readonly HandShape Open = HandShape.Pinch(0.05f, 0.09f, 0.95f);

    /// <summary>Holds a hand shape for a while and reports the grip event that happened (if any).</summary>
    private static GripEvent Hold(PinchGrip grip, ref double t, double seconds, HandShape shape, bool canGrab)
    {
        GripEvent seen = GripEvent.None;
        int frames = (int)Math.Round(seconds / OneHandSim.Dt);
        for (int i = 0; i < frames; i++)
        {
            t += OneHandSim.Dt;
            GripEvent e = grip.Update(t, true, shape, canGrab);
            if (e != GripEvent.None) seen = e;
        }
        return seen;
    }

    private static void VanishAndProduceWith(HandId hand)
    {
        var trick = new VanishTrick();
        var coin = new CoinModel();
        var grip = new PinchGrip();
        var zone = new ServanteZone();
        double t = 0;

        // Pick up: pinch at the coin.
        Assert.Equal(GripEvent.Grabbed, Hold(grip, ref t, 0.2, Pinched, true), "pinched the coin");
        Assert.True(coin.TryGrab(hand));
        trick.PickedUp(t, hand);

        // Show: hold it up.
        TrickSim.Run(trick, ref t, 0.8, TrickSim.Input(height: 0.25f));
        Assert.Equal(TrickPhase.Misdirect, trick.Phase, "shown");

        // Misdirect with the eyes (the flourish needs a free hand), then open the hand in the servante.
        bool inside = zone.Update(new Vector3(0.05f, -0.04f, -0.05f));
        Assert.True(inside, "hand past the near edge");
        Assert.Equal(GripEvent.Released, Hold(grip, ref t, 0.3, Open, false), "opened");
        Assert.Equal(CoinState.Ditched, coin.Release(hand, inside));
        trick.Released(t, hand, inside, TrickSim.Unseen);
        Assert.Equal(TrickPhase.Vanished, trick.Phase, "ditched");

        // Show empty: the same hand, the one that last held the coin.
        Assert.Equal(hand, trick.LastHolder);
        bool emptyShown = coin.Holder != hand;
        RevealResult? vanish = TrickSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: emptyShown));
        Assert.True(vanish.HasValue && vanish.Value.Reaction == Reaction.Applause, "vanish applauded");
        Assert.True(trick.CanSteal);

        // Steal it back: pinch in the servante.
        Assert.Equal(GripEvent.Grabbed, Hold(grip, ref t, 0.2, Pinched, true), "pinched in the servante");
        Assert.True(coin.TrySteal(hand));
        Assert.True(trick.Stolen(t, hand, TrickSim.Unseen));
        Assert.Equal(TrickPhase.Loaded, trick.Phase);

        // Produce: look at a spectator and raise the pinch.
        RevealResult? produce = TrickSim.Run(trick, ref t, 0.2, TrickSim.Input(lookedAt: 1, dwell: 0.6, pinch: true));
        Assert.True(produce.HasValue, "produced");
        Assert.Equal("produce", produce.Value.Kind);
        Assert.Equal(1, produce.Value.Spectator);
        Assert.Equal(TrickPhase.Finale, trick.Phase);
        TrickSim.Run(trick, ref t, 3.2, TrickSim.Input());
        Assert.Equal(TrickPhase.Idle, trick.Phase, "ready for the next act");
    }

    private static void CupWith(HandId hand)
    {
        var trick = new CupTrick();
        var coin = new CoinModel();
        var coinGrip = new PinchGrip();
        var cupGrip = new PinchGrip();
        HandId cupHolder = HandId.None;
        double t = 0;

        // Pick up the coin (the cup is not in this hand).
        Assert.True(cupHolder != hand);
        Assert.Equal(GripEvent.Grabbed, Hold(coinGrip, ref t, 0.2, Pinched, true));
        Assert.True(coin.TryGrab(hand));
        trick.PickedUp(t, hand);

        // While the hand holds the coin it cannot also take the cup (CupTrickDirector.StepCup's guard).
        bool canGrabCup = cupHolder == HandId.None && coin.Holder != hand;
        Assert.False(canGrabCup, "one hand, one thing at a time");
        Assert.Equal(GripEvent.None, Hold(cupGrip, ref t, 0.2, Pinched, canGrabCup));

        CupSim.Run(trick, ref t, 0.8, TrickSim.Input(height: 0.25f));
        Assert.Equal(CupPhase.Misdirect, trick.Phase, "shown");

        // Load: open the hand at the cup's base.
        Assert.Equal(GripEvent.Released, Hold(coinGrip, ref t, 0.3, Open, false));
        coin.Release(hand, true);
        trick.Released(t, hand, ReleaseZone.UnderCup, TrickSim.Unseen);
        Assert.Equal(CupPhase.Loaded, trick.Phase);
        Hold(cupGrip, ref t, 0.3, Open, false);

        // Show the same hand empty.
        Assert.Equal(hand, trick.LastHolder);
        RevealResult? vanish = CupSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: coin.Holder != hand));
        Assert.True(vanish.HasValue, "vanish revealed");
        Assert.Equal(CupPhase.Vanished, trick.Phase);

        // Now the hand is free: pinch the cup's top and lift.
        canGrabCup = cupHolder == HandId.None && coin.Holder != hand;
        Assert.True(canGrabCup, "the hand that loaded may lift");
        Assert.Equal(GripEvent.Grabbed, Hold(cupGrip, ref t, 0.2, Pinched, canGrabCup));
        cupHolder = hand;
        RevealResult? cup = CupSim.Run(trick, ref t, 0.2, TrickSim.Input(), 0.12f);
        Assert.True(cup.HasValue, "lifted");
        Assert.Equal("cup", cup.Value.Kind);
        Assert.Equal(Reaction.Applause, cup.Value.Reaction);
        Assert.Equal(CupPhase.Finale, trick.Phase);
        CupSim.Run(trick, ref t, 3.2, TrickSim.Input());
        Assert.Equal(CupPhase.Idle, trick.Phase, "ready for the next act");
    }

    [Test]
    public static void Trick1CompletesWithTheRightHandOnly()
    {
        VanishAndProduceWith(HandId.Right);
    }

    [Test]
    public static void Trick1CompletesWithTheLeftHandOnly()
    {
        VanishAndProduceWith(HandId.Left);
    }

    [Test]
    public static void Trick2CompletesWithTheRightHandOnly()
    {
        CupWith(HandId.Right);
    }

    [Test]
    public static void Trick2CompletesWithTheLeftHandOnly()
    {
        CupWith(HandId.Left);
    }

    [Test]
    public static void AWholeShowIsCompletableWithOneHand()
    {
        // Three acts (vanish, cup, vanish), every reveal and bow driven by one hand's events.
        var show = new ShowFlow(null, true);
        show.TableReady(0);
        double t = 0;
        for (int act = 0; act < 3; act++)
        {
            if (show.ActiveTrick == 0)
            {
                VanishAndProduceWith(HandId.Left);
            }
            else
            {
                CupWith(HandId.Left);
            }
            show.Revealed(new RevealResult { Kind = "vanish", Tips = 15, Reaction = Reaction.Applause, SeenBy = -1, Spectator = -1 });
            show.Revealed(new RevealResult { Kind = "produce", Tips = 18, Reaction = Reaction.Applause, SeenBy = -1, Spectator = -1 });
            t += 60;
            show.TrickCompleted(t);
        }
        Assert.Equal(ShowStage.Bow, show.Stage);
        Assert.Equal(3, show.LastSummary.Stars, "a clean one-handed show earns all three stars");
    }

    [Test]
    public static void EyeContactAloneClearsTheWayForTheDitch()
    {
        // One-handed misdirection: no flourish, no sound. Look a spectator in the eyes, then ditch.
        var crowd = new CrowdModel();
        Vector3 spectatorEyes = new Vector3(0f, 1.5f, 1.5f);
        SpectatorMind mind = crowd.Add(SpectatorProfile.For(Archetype.Tourist), spectatorEyes);
        Vector3 magician = new Vector3(0f, 1.2f, -0.3f);
        Vector3 hand = new Vector3(0.1f, 0.78f, 0.15f);
        Vector3 stage = new Vector3(0f, 0.75f, 0.24f);
        Vector3 servante = new Vector3(0.1f, 0.72f, -0.05f);
        double t = 0;

        // Looking down at the hand: she watches it.
        Vector3 down = Vector3.Normalize(hand - magician);
        for (int i = 0; i < 144; i++)
        {
            t += OneHandSim.Dt;
            crowd.Update(t, (float)OneHandSim.Dt, magician, down, hand, stage, 0.75f);
        }
        Assert.True(mind.Coverage(hand) > 0.9f, "she sees the hand: " + mind.Coverage(hand));

        // Look her in the eyes for a second.
        Vector3 atHer = Vector3.Normalize(spectatorEyes - magician);
        for (int i = 0; i < 72; i++)
        {
            t += OneHandSim.Dt;
            crowd.Update(t, (float)OneHandSim.Dt, magician, atHer, hand, stage, 0.75f);
        }
        Assert.True(mind.EyeContact, "eye contact locks her");
        WitnessReport w = crowd.Witness(servante, 1f);
        Assert.True(w.MaxCoverage < 0.25f, "the ditch is at most a flicker at the edge of her cone: " + w.MaxCoverage);
        RevealResult r = new VanishTrick().Score("vanish", w, TrickSim.Input(), 0f, -1);
        Assert.Equal(Reaction.Applause, r.Reaction, "and the vanish still gets applause");
    }
}
