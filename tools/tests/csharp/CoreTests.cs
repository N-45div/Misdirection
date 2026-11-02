// Unit tests for Assets/Misdirection/Scripts/Core (engine-free game logic).
using System;
using System.Numerics;
using Misdirection.Core.Hands;
using Misdirection.Core.Table;
using Misdirection.Core.Tricks;

public static class Sim
{
    public const double Dt = 1.0 / 72.0;

    /// <summary>Feeds the same flags for <paramref name="seconds"/>, returning the end time.</summary>
    public static double Run(HandPresence p, double start, double seconds, bool tracked, bool high)
    {
        double t = start;
        int frames = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
        {
            t += Dt;
            p.Update(t, tracked, high);
        }
        return t;
    }

    public static double Steady(HandPresence p)
    {
        return Run(p, 0, 1.0, true, true);
    }

    public static GripEvent RunGrip(PinchGrip g, ref double t, double seconds, bool reliable, HandShape shape, bool canGrab)
    {
        GripEvent last = GripEvent.None;
        int frames = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
        {
            t += Dt;
            GripEvent e = g.Update(t, reliable, shape, canGrab);
            if (e != GripEvent.None)
            {
                last = e;
            }
        }
        return last;
    }
}

public static class HandPresenceTests
{
    [Test]
    public static void StartsLostThenBlendsInAndSettlesBeforeDecisions()
    {
        var p = new HandPresence();
        Assert.Equal(HandPresenceState.Lost, p.State);
        p.Update(0.0, true, true);
        Assert.Equal(HandPresenceState.Recovering, p.State, "first tracked frame");
        Assert.False(p.ReliableForDecisions, "no decisions while blending in");
        double t = Sim.Run(p, 0.0, 0.10, true, true);
        Assert.True(p.LiveWeight > 0.3f && p.LiveWeight < 0.6f, "blending, weight " + p.LiveWeight);
        t = Sim.Run(p, t, 0.20, true, true);
        Assert.Equal(HandPresenceState.Tracked, p.State);
        Assert.True(p.ReliableForDecisions, "reliable once blended and settled");
        Assert.Near(1.0, p.LiveWeight);
        Assert.Near(0.0, p.Tint, 1e-3, "tint");
    }

    [Test]
    public static void BriefGapIsUncertainNotLost()
    {
        var p = new HandPresence();
        double t = Sim.Steady(p);
        t = Sim.Run(p, t, 0.05, false, false);
        Assert.Equal(HandPresenceState.Uncertain, p.State);
        Assert.Near(1.0, p.LiveWeight, 1e-6, "pose not frozen for a blink");
        Assert.False(p.ReliableForDecisions);
        t = Sim.Run(p, t, 0.10, true, true);
        Assert.Equal(HandPresenceState.Tracked, p.State);
        Assert.False(p.ReliableForDecisions, "must settle again after a gap");
        t = Sim.Run(p, t, 0.10, true, true);
        Assert.True(p.ReliableForDecisions);
    }

    [Test]
    public static void LongGapFreezesAndFades()
    {
        var p = new HandPresence();
        double t = Sim.Steady(p);
        t = Sim.Run(p, t, 0.15, false, false);
        Assert.Equal(HandPresenceState.Lost, p.State);
        Assert.Near(0.0, p.LiveWeight, 1e-6, "frozen pose");
        Assert.True(p.UntrackedSeconds > 0.12, "untracked seconds " + p.UntrackedSeconds);
        t = Sim.Run(p, t, 0.5, false, false);
        Assert.Near(0.25, p.Alpha, 1e-3, "faded to lost alpha");
        Assert.Near(1.0, p.Tint, 1e-6, "full warning tint");
    }

    [Test]
    public static void LowConfidenceTintsAndBlocksDecisions()
    {
        var p = new HandPresence();
        double t = Sim.Steady(p);
        t = Sim.Run(p, t, 0.125, true, false);
        Assert.Equal(HandPresenceState.Uncertain, p.State);
        Assert.True(p.Tint > 0.4f && p.Tint < 0.6f, "half tint, got " + p.Tint);
        Assert.False(p.ReliableForDecisions);
        t = Sim.Run(p, t, 0.25, true, false);
        Assert.Near(1.0, p.Tint, 1e-3, "full tint");
        t = Sim.Run(p, t, 0.5, true, true);
        Assert.Near(0.0, p.Tint, 1e-3, "tint clears");
        Assert.True(p.ReliableForDecisions);
    }

    [Test]
    public static void RecoveringWithLowConfidenceEndsUncertain()
    {
        var p = new HandPresence();
        double t = Sim.Steady(p);
        t = Sim.Run(p, t, 0.5, false, false);
        t = Sim.Run(p, t, 0.3, true, false);
        Assert.Equal(HandPresenceState.Uncertain, p.State);
        Assert.Near(1.0, p.LiveWeight);
        Assert.False(p.ReliableForDecisions);
    }
}

public static class BothHandsPauseTests
{
    [Test]
    public static void PausesAfterTwoSecondsAndResumesWhenBothBack()
    {
        var pause = new BothHandsPause();
        double t = 0;
        for (; t < 1.9; t += Sim.Dt) pause.Update(t, false, false);
        Assert.False(pause.Paused, "not yet");
        bool changed = false;
        for (; t < 2.2; t += Sim.Dt) changed |= pause.Update(t, false, false);
        Assert.True(pause.Paused && changed, "paused after 2 s");
        for (double end = t + 1.0; t < end; t += Sim.Dt) pause.Update(t, true, false);
        Assert.True(pause.Paused, "one hand back is not enough");
        for (double end = t + 0.4; t < end; t += Sim.Dt) pause.Update(t, true, true);
        Assert.True(pause.Paused, "both back but not settled");
        for (double end = t + 0.2; t < end; t += Sim.Dt) pause.Update(t, true, true);
        Assert.False(pause.Paused, "resumed");
    }

    [Test]
    public static void OneTrackedHandNeverPauses()
    {
        var pause = new BothHandsPause();
        for (double t = 0; t < 5; t += Sim.Dt) pause.Update(t, false, true);
        Assert.False(pause.Paused);
    }
}

public static class PinchGripTests
{
    static readonly HandShape Pinched = HandShape.Pinch(0.95f, 0.010f, 0.3f);

    [Test]
    public static void GrabNeedsAHeldPinchOnSomethingGrabbable()
    {
        var g = new PinchGrip();
        double t = 0;
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.5, true, Pinched, false), "nothing to grab");
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.5, false, Pinched, true), "unreliable tracking");
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.04, true, Pinched, true), "too short");
        Assert.Equal(GripState.Open, g.State);
        Assert.Equal(GripEvent.Grabbed, Sim.RunGrip(g, ref t, 0.05, true, Pinched, true), "held pinch grabs");
        Assert.Equal(GripState.Holding, g.State);
        Assert.Near(0.010, g.GripDistance, 1e-6, "grip distance");
        Assert.Near(0.030, g.ReleaseDistance, 1e-6, "release distance = grip + 2 cm");
    }

    static PinchGrip Holding(ref double t)
    {
        var g = new PinchGrip();
        Sim.RunGrip(g, ref t, 0.2, true, Pinched, true);
        Assert.Equal(GripState.Holding, g.State, "setup");
        return g;
    }

    [Test]
    public static void ReleaseUsesTheMarginSetAtGrabTime()
    {
        double t = 0;
        var g = Holding(ref t);
        var slightlyApart = HandShape.Pinch(0.5f, 0.025f, 0.9f);
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.5, true, slightlyApart, false), "within margin keeps holding");
        var apart = HandShape.Pinch(0.5f, 0.035f, 0.9f);
        Assert.Equal(GripEvent.Released, Sim.RunGrip(g, ref t, 0.2, true, apart, false), "beyond margin releases");
        Assert.Equal(GripState.Open, g.State);
    }

    [Test]
    public static void FistKeepsHoldingTheCoin()
    {
        double t = 0;
        var g = Holding(ref t);
        var fist = HandShape.Pinch(0.1f, 0.05f, 0.15f);
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 2.0, true, fist, false), "palmed coin stays put");
        Assert.Equal(GripState.Holding, g.State);
    }

    [Test]
    public static void UnreliableTrackingNeverReleases()
    {
        double t = 0;
        var g = Holding(ref t);
        var open = HandShape.Pinch(0.0f, 0.08f, 1.0f);
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 3.0, false, open, false));
        Assert.Equal(GripState.Holding, g.State);
        // A pending release resets when tracking drops mid-way.
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.05, true, open, false));
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.05, false, open, false));
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.05, true, open, false), "timer restarted");
        Assert.Equal(GripEvent.Released, Sim.RunGrip(g, ref t, 0.05, true, open, false));
    }

    [Test]
    public static void ReleaseMustBeHeldNotFlashed()
    {
        double t = 0;
        var g = Holding(ref t);
        var open = HandShape.Pinch(0.0f, 0.08f, 1.0f);
        Sim.RunGrip(g, ref t, 0.05, true, open, false);
        Sim.RunGrip(g, ref t, 0.2, true, Pinched, false);
        Assert.Equal(GripState.Holding, g.State, "a 50 ms flicker doesn't drop the coin");
    }

    [Test]
    public static void WithoutGeometryFallsBackToPinchStrength()
    {
        double t = 0;
        var g = new PinchGrip();
        Sim.RunGrip(g, ref t, 0.2, true, HandShape.Pinch(0.9f), true);
        Assert.Equal(GripState.Holding, g.State);
        Assert.True(float.IsNaN(g.ReleaseDistance), "no distance without geometry");
        Assert.Equal(GripEvent.None, Sim.RunGrip(g, ref t, 0.3, true, HandShape.Pinch(0.5f), false));
        Assert.Equal(GripEvent.Released, Sim.RunGrip(g, ref t, 0.3, true, HandShape.Pinch(0.2f), false));
    }
}

public static class HandOpennessTests
{
    [Test]
    public static void ExtendedCurledAndInvalid()
    {
        Assert.Near(1.0, HandOpenness.Finger(0.165f, 0.09f), 1e-6, "straight finger");
        Assert.Near(0.0, HandOpenness.Finger(0.07f, 0.09f), 1e-6, "curled finger");
        Assert.True(float.IsNaN(HandOpenness.Finger(0.1f, 0f)), "zero knuckle distance");
        Assert.Near(0.5, HandOpenness.Combine(0f, 1f, float.NaN), 1e-6, "mean of valid");
        Assert.True(float.IsNaN(HandOpenness.Combine(float.NaN, float.NaN, float.NaN)), "none valid");
    }
}

public static class TableFrameTests
{
    static readonly Vector3 Head = new Vector3(0f, 1.20f, -0.30f);

    [Test]
    public static void PalmsDefineTheNearEdge()
    {
        var frame = TableFrame.FromPalms(new Vector3(-0.2f, 0.78f, 0.1f), new Vector3(0.2f, 0.77f, 0.1f), Head);
        Assert.Near(0.0, frame.Origin.X, 1e-5, "origin x");
        Assert.Near(0.745, frame.Origin.Y, 1e-5, "surface under the lower wrist");
        Assert.Near(1.0, frame.Forward.Z, 1e-5, "forward away from the player");
        Assert.Near(1.0, frame.Right.X, 1e-5, "right");
        Vector3 local = frame.ToLocal(new Vector3(0.1f, 0.8f, 0.3f));
        Assert.Near(0.1, local.X, 1e-5, "local x");
        Assert.Near(0.2, local.Z, 1e-5, "local z");
        Vector3 back = frame.ToWorld(local);
        Assert.Near(0.3, back.Z, 1e-5, "round trip");
    }

    [Test]
    public static void CrossedHandsStillFaceAway()
    {
        var frame = TableFrame.FromPalms(new Vector3(0.2f, 0.78f, 0.1f), new Vector3(-0.2f, 0.78f, 0.1f), Head);
        Assert.True(frame.Forward.Z > 0.99f, "forward " + frame.Forward);
    }

    [Test]
    public static void RotatedTable()
    {
        // Player sits facing +X.
        var frame = TableFrame.FromPalms(new Vector3(0.4f, 0.75f, 0.2f), new Vector3(0.4f, 0.75f, -0.2f), new Vector3(0f, 1.2f, 0f));
        Assert.Near(1.0, frame.Forward.X, 1e-5, "forward +X");
        Vector3 local = frame.ToLocal(new Vector3(0.5f, 0.725f, 0f));
        Assert.Near(0.1, local.Z, 1e-5, "10 cm onto the table");
    }
}

public static class ServanteZoneTests
{
    [Test]
    public static void InsideOutsideAndHysteresis()
    {
        var zone = new ServanteZone();
        Assert.True(zone.Update(new Vector3(0f, -0.05f, -0.05f)), "just past the near edge");
        Assert.True(zone.Update(new Vector3(0f, -0.05f, 0.06f)), "1 cm out: still inside (hysteresis)");
        Assert.False(zone.Update(new Vector3(0f, -0.05f, 0.08f)), "3 cm out: outside");
        Assert.False(zone.Update(new Vector3(0f, -0.05f, 0.06f)), "re-entering needs the real boundary");
        Assert.Near(0.0, zone.Distance(new Vector3(0.2f, 0f, 0f)), 1e-6, "inside distance");
        Assert.Near(0.25, zone.Distance(new Vector3(0f, 0f, 0.30f)), 1e-5, "far onto the table");
    }
}

public static class CoinTests
{
    [Test]
    public static void GrabDitchExposeAndReset()
    {
        var coin = new CoinModel();
        Assert.True(coin.TryGrab(HandId.Right));
        Assert.False(coin.TryGrab(HandId.Left), "already held");
        Assert.Equal(CoinState.Held, coin.Release(HandId.Left, true), "other hand can't release it");
        Assert.Equal(CoinState.Ditched, coin.Release(HandId.Right, true));
        Assert.False(coin.Grabbable, "ditched coin is gone");
        coin.Reset();
        coin.TryGrab(HandId.Left);
        Assert.Equal(CoinState.Exposed, coin.Release(HandId.Left, false));
        Assert.True(coin.TryGrab(HandId.Left), "an exposed coin can be picked up again");
    }
}

public static class DitchTrialTests
{
    static void Add(DitchTrialRecorder r, TrialOutcome outcome, double start = 0)
    {
        r.Begin(start, HandId.Right);
        r.End(start + 2, outcome, outcome == TrialOutcome.Ditched ? 0f : 0.3f);
    }

    [Test]
    public static void TracksLostAndUncertainTimeWhileHolding()
    {
        var r = new DitchTrialRecorder();
        r.Begin(10.0, HandId.Left);
        r.Tick(10.5, false, false);
        r.Tick(10.7, true, false);
        r.Tick(11.0, false, true);
        DitchTrial trial = r.End(11.2, TrialOutcome.Exposed, 0.2f);
        Assert.Near(0.2, trial.LostSeconds, 1e-9, "lost");
        Assert.Near(0.3, trial.UncertainSeconds, 1e-9, "uncertain");
        Assert.Near(1.2, trial.Duration, 1e-9, "duration");
        Assert.True(trial.LikelyAccidental(), "20 cm from the servante");
    }

    [Test]
    public static void ExitCriterionUsesTheLastTwentyAttempts()
    {
        var r = new DitchTrialRecorder();
        for (int i = 0; i < 5; i++) Add(r, TrialOutcome.Exposed);
        for (int i = 0; i < 19; i++) Add(r, TrialOutcome.Ditched);
        Add(r, TrialOutcome.Abandoned);
        Add(r, TrialOutcome.Exposed);
        int s, a;
        r.Recent(out s, out a);
        Assert.Equal(20, a, "attempts in window (abandoned ignored)");
        Assert.Equal(19, s, "successes in window");
        Assert.True(r.MeetsExitCriterion());
        Add(r, TrialOutcome.Exposed);
        Assert.False(r.MeetsExitCriterion(), "18/20 fails");
        Assert.Equal(19, r.Successes, "total successes");
    }
}
