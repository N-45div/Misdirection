// Unit tests for dwell selection (the Access Tray's eyes-only toggles, Day 11).
using System;
using System.Numerics;
using Misdirection.Core.Access;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Show;
using Misdirection.Core.Table;
using Misdirection.Core.Tricks;

public static class DwellSim
{
    public const double Dt = 1.0 / 72.0;

    /// <summary>Dwells on <paramref name="target"/> for <paramref name="seconds"/>; returns what was chosen (-1 for nothing).</summary>
    public static int Dwell(DwellSelector s, ref double t, double seconds, int target, bool enabled = true, double rate = 1.0)
    {
        int chosen = -1;
        int frames = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
        {
            t += Dt;
            int c = s.Update(t, target, enabled, rate);
            if (c >= 0) chosen = c;
        }
        return chosen;
    }
}

public static class DwellSelectorTests
{
    [Test]
    public static void ChoosesAfterTheDwellAndNotBefore()
    {
        var s = new DwellSelector(1.5);
        double t = 0;
        Assert.Equal(-1, DwellSim.Dwell(s, ref t, 1.4, 2), "not yet");
        Assert.Equal(2, s.Target);
        Assert.True(s.Progress > 0.9f, "ring nearly full: " + s.Progress);
        Assert.Equal(2, DwellSim.Dwell(s, ref t, 0.2, 2), "chosen");
    }

    [Test]
    public static void DoesNotRepeatUntilLookedAway()
    {
        var s = new DwellSelector(1.5);
        double t = 0;
        Assert.Equal(1, DwellSim.Dwell(s, ref t, 1.6, 1));
        Assert.Equal(-1, DwellSim.Dwell(s, ref t, 5.0, 1), "staring on does not toggle it back");
        Assert.Near(0.0, s.Progress, 1e-6, "no ring while latched");
        DwellSim.Dwell(s, ref t, 0.5, -1);
        Assert.Equal(1, DwellSim.Dwell(s, ref t, 1.6, 1), "looked away and back: again");
    }

    [Test]
    public static void ABriefSlipKeepsTheDwellALongOneRestartsIt()
    {
        var s = new DwellSelector(1.5, 0.25);
        double t = 0;
        DwellSim.Dwell(s, ref t, 1.0, 0);
        DwellSim.Dwell(s, ref t, 0.15, -1);
        Assert.Equal(0, DwellSim.Dwell(s, ref t, 0.6, 0), "a 0.15 s slip is forgiven");

        s.Reset();
        DwellSim.Dwell(s, ref t, 1.0, 0);
        DwellSim.Dwell(s, ref t, 0.4, -1);
        Assert.Equal(-1, DwellSim.Dwell(s, ref t, 1.0, 0), "a 0.4 s look away starts over");
        Assert.Equal(0, DwellSim.Dwell(s, ref t, 0.6, 0), "and then completes");
    }

    [Test]
    public static void MovingToAnotherTargetStartsOver()
    {
        var s = new DwellSelector(1.5);
        double t = 0;
        DwellSim.Dwell(s, ref t, 1.2, 0);
        Assert.Equal(-1, DwellSim.Dwell(s, ref t, 1.2, 3), "the time on token 0 doesn't count for token 3");
        Assert.Equal(3, DwellSim.Dwell(s, ref t, 0.4, 3));
    }

    [Test]
    public static void DisabledIgnoresTheLookAndForgetsTheDwell()
    {
        var s = new DwellSelector(1.5);
        double t = 0;
        DwellSim.Dwell(s, ref t, 1.2, 4);
        Assert.Equal(-1, DwellSim.Dwell(s, ref t, 3.0, 4, false), "coin in hand: no dwell");
        Assert.Equal(-1, s.Target);
        Assert.Equal(-1, DwellSim.Dwell(s, ref t, 1.2, 4), "starts from nothing afterwards");
        Assert.Equal(4, DwellSim.Dwell(s, ref t, 0.4, 4));
    }

    [Test]
    public static void ALongFrameCannotCompleteADwell()
    {
        var s = new DwellSelector(1.5);
        s.Update(0, 1, true);
        Assert.Equal(-1, s.Update(10, 1, true), "one 10 s frame counts as 0.1 s");
        Assert.True(s.Progress < 0.1f);
    }

    [Test]
    public static void APinchCountsFasterThanARestingFinger()
    {
        var s = new DwellSelector(0.5);
        double t = 0;
        Assert.Equal(-1, DwellSim.Dwell(s, ref t, 0.2, 0), "resting 0.2 s: not yet");
        s.Reset();
        Assert.Equal(0, DwellSim.Dwell(s, ref t, 0.2, 0, true, 4.0), "pinched 0.2 s: chosen");
    }
}
