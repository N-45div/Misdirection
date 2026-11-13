// Unit tests for relaxed timing ("longer windows", Day 11) in Misdirection.Core.Access.
using System;
using System.Numerics;
using Misdirection.Core.Access;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Show;
using Misdirection.Core.Table;
using Misdirection.Core.Tricks;

public static class RelaxedTimingTests
{
    [Test]
    public static void OffChangesNothing()
    {
        Assert.Near(0.6, RelaxedTiming.Hold(0.6, false));
        Assert.Near(8.0, RelaxedTiming.Window(8.0, false));
        Assert.Near(4.5, RelaxedTiming.TurnRate(4.5f, false));
        Assert.Near(1.0, RelaxedTiming.SuspicionScale(false));
    }

    [Test]
    public static void HoldsGetShorterButNeverBecomeAnInstant()
    {
        Assert.Near(0.36, RelaxedTiming.Hold(0.6, true), 1e-6, "0.6 s hold");
        Assert.Near(0.24, RelaxedTiming.Hold(0.4, true), 1e-6, "0.4 s hold");
        Assert.Near(0.2, RelaxedTiming.Hold(0.25, true), 1e-6, "floored at 0.2 s");
        Assert.Near(0.1, RelaxedTiming.Hold(0.1, true), 1e-6, "an already short hold is never lengthened");
    }

    [Test]
    public static void WindowsGetLongerTheCrowdSlowerAndLessSuspicious()
    {
        Assert.Near(12.8, RelaxedTiming.Window(8.0, true), 1e-5);
        Assert.Near(2.7, RelaxedTiming.TurnRate(4.5f, true), 1e-5);
        Assert.Near(0.6, RelaxedTiming.SuspicionScale(true), 1e-6);
        var odd = new RelaxedTimingSettings { WindowScale = 0.5f, SuspicionScale = 3f, TurnRateScale = -1f };
        Assert.Near(8.0, RelaxedTiming.Window(8.0, true, odd), 1e-6, "a window never shrinks");
        Assert.Near(1.0, RelaxedTiming.SuspicionScale(true, odd), 1e-6, "suspicion never grows");
        Assert.Near(0.0, RelaxedTiming.TurnRate(4f, true, odd), 1e-6, "clamped");
    }

    [Test]
    public static void AppliesToTheLiveSettingsAndBackWithoutDrift()
    {
        var vanish = new VanishTrickSettings();
        var cup = new CupTrickSettings();
        var tutorial = new TutorialSettings();
        var attention = new AttentionSettings();
        RelaxedTimingBaseline baseline = RelaxedTimingBaseline.Capture(vanish, cup, tutorial, attention);
        for (int i = 0; i < 3; i++)
        {
            baseline.Apply(vanish, cup, tutorial, attention, true);
            Assert.Near(0.36, vanish.ShowSeconds, 1e-6, "vanish show");
            Assert.Near(0.24, vanish.RevealHoldSeconds, 1e-6, "vanish reveal");
            Assert.Near(0.24, vanish.ProduceDwellSeconds, 1e-6, "produce dwell");
            Assert.Near(0.36, cup.ShowSeconds, 1e-6, "cup show");
            Assert.Near(0.24, cup.RevealHoldSeconds, 1e-6, "cup reveal");
            Assert.Near(0.24, tutorial.LookHoldSeconds, 1e-6, "look hold");
            Assert.Near(12.8, tutorial.LookTimeoutSeconds, 1e-5, "look timeout");
            Assert.Near(2.4, attention.LockHoldSeconds, 1e-5, "eye-contact window");
            baseline.Apply(vanish, cup, tutorial, attention, false);
            Assert.Near(0.6, vanish.ShowSeconds, 1e-9, "back to the design");
            Assert.Near(0.4, cup.RevealHoldSeconds, 1e-9);
            Assert.Near(8.0, tutorial.LookTimeoutSeconds, 1e-9);
            Assert.Near(1.5, attention.LockHoldSeconds, 1e-9);
        }
        Assert.Near(3.0, vanish.FinaleSeconds, 1e-9, "untouched: the bow");
        Assert.Near(0.15, vanish.ShowHeight, 1e-6, "untouched: distances");
    }

    [Test]
    public static void MissingPartsAreLeftAlone()
    {
        var vanish = new VanishTrickSettings();
        RelaxedTimingBaseline baseline = RelaxedTimingBaseline.Capture(vanish, null, null, null);
        var cup = new CupTrickSettings();
        baseline.Apply(vanish, cup, null, null, true);
        Assert.Near(0.36, vanish.ShowSeconds, 1e-6);
        Assert.Near(0.6, cup.ShowSeconds, 1e-9, "no baseline for the cup: not touched");
    }

    [Test]
    public static void TheRelaxedTrickTakesAShorterHold()
    {
        var settings = new VanishTrickSettings();
        RelaxedTimingBaseline.Capture(settings, null, null, null).Apply(settings, null, null, null, true);
        var trick = new VanishTrick(settings);
        double t = 0;
        trick.PickedUp(t, HandId.Right);
        TrickSim.Run(trick, ref t, 0.3, TrickSim.Input(height: 0.25f));
        Assert.Equal(TrickPhase.Showing, trick.Phase, "0.3 s is not yet shown");
        TrickSim.Run(trick, ref t, 0.1, TrickSim.Input(height: 0.25f));
        Assert.Equal(TrickPhase.Misdirect, trick.Phase, "0.4 s is (0.6 s by default)");
    }

    [Test]
    public static void ASlowerCrowdTakesLongerToLookBack()
    {
        // The same spectator, attention on the gaze point, then the gaze snaps to the hands.
        float normal = SecondsToReach(RelaxedTiming.TurnRate(4f, false));
        float relaxed = SecondsToReach(RelaxedTiming.TurnRate(4f, true));
        Assert.True(relaxed > normal * 1.5f, "normal " + normal + " s, relaxed " + relaxed + " s");
    }

    private static float SecondsToReach(float turnRate)
    {
        var profile = new SpectatorProfile { GazeWeight = 1f, HandsWeight = 0f, FlourishWeight = 0f, SoundWeight = 0f, TurnRate = turnRate };
        var mind = new SpectatorMind(0, profile, new Vector3(0f, 1.5f, 1.5f));
        var s = new Stimuli { LookedAt = -1, GazePoint = new Vector3(1f, 0.8f, 0f), HandsCenter = new Vector3(0f, 0.8f, 0f) };
        double t = 0;
        mind.Update(t, 0f, s);
        s.GazePoint = new Vector3(0f, 0.8f, 0f);
        for (int i = 0; i < 2000; i++)
        {
            t += TrickSim.Dt;
            mind.Update(t, (float)TrickSim.Dt, s);
            if (mind.AttentionPoint.X < 0.1f)
            {
                return (float)t;
            }
        }
        return float.MaxValue;
    }
}
