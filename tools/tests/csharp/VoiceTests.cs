// Unit tests for the voice alternative to the flourish (Day 11): hearing a short loud sound.
using System;
using System.Numerics;
using Misdirection.Core.Access;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Show;
using Misdirection.Core.Table;
using Misdirection.Core.Tricks;

public static class VoiceSim
{
    public const double Dt = 1.0 / 72.0;

    /// <summary>Feeds a steady microphone level in 10 ms blocks; returns how many calls were heard.</summary>
    public static int Hear(LoudSoundDetector d, ref double t, double seconds, float level)
    {
        int calls = 0;
        int blocks = (int)Math.Round(seconds / 0.01);
        for (int i = 0; i < blocks; i++)
        {
            t += 0.01;
            if (d.Update(t, level)) calls++;
        }
        return calls;
    }
}

public static class LoudSoundDetectorTests
{
    const float Room = 0.01f;
    const float Call = 0.3f;

    [Test]
    public static void AShortLoudSoundIsHeardOnceWhenItEnds()
    {
        var d = new LoudSoundDetector();
        double t = 0;
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 2.0, Room), "a quiet room");
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 0.3, Call), "not while it is still sounding");
        Assert.True(d.Loud, "loud");
        Assert.Equal(1, VoiceSim.Hear(d, ref t, 0.5, Room), "heard when it ends");
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 2.0, Room), "once");
    }

    [Test]
    public static void AClapCounts()
    {
        var d = new LoudSoundDetector();
        double t = 0;
        VoiceSim.Hear(d, ref t, 1.0, Room);
        VoiceSim.Hear(d, ref t, 0.05, 0.6f);
        Assert.Equal(1, VoiceSim.Hear(d, ref t, 0.5, Room));
    }

    [Test]
    public static void ALongNoiseIsNotACallAndBecomesTheRoom()
    {
        var d = new LoudSoundDetector();
        double t = 0;
        VoiceSim.Hear(d, ref t, 1.0, Room);
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 8.0, 0.2f), "a vacuum cleaner starts");
        Assert.True(d.NoiseFloor > 0.1f, "the floor rose to the noise: " + d.NoiseFloor);
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 0.3, 0.3f), "a little louder than the noise is not a call");
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 0.5, 0.2f), "and nothing is heard when it ends");
        VoiceSim.Hear(d, ref t, 0.3, 0.95f);
        Assert.Equal(1, VoiceSim.Hear(d, ref t, 0.5, 0.2f), "well above the noise is");
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 3.0, Room), "the noise stopping is not a call");
    }

    [Test]
    public static void QuietSoundsAndClicksDoNotCount()
    {
        var d = new LoudSoundDetector();
        double t = 0;
        VoiceSim.Hear(d, ref t, 1.0, 0.001f);
        VoiceSim.Hear(d, ref t, 0.3, 0.03f);
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 0.5, 0.001f), "30x the floor but under the minimum level");
        VoiceSim.Hear(d, ref t, 0.01, 0.9f);
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 0.5, 0.001f), "a single 10 ms click is too short");
    }

    [Test]
    public static void RefractoryPeriodSpacesCallsOut()
    {
        var d = new LoudSoundDetector();
        double t = 0;
        VoiceSim.Hear(d, ref t, 1.0, Room);
        int calls = 0;
        for (int i = 0; i < 4; i++)
        {
            // "Look! Look! Look! Look!", one every half second.
            calls += VoiceSim.Hear(d, ref t, 0.2, Call);
            calls += VoiceSim.Hear(d, ref t, 0.3, Room);
        }
        Assert.Equal(2, calls, "the first, and the next one 1.2 s or more later");
    }

    [Test]
    public static void NothingCountsDuringWarmUpOrWhileSuppressed()
    {
        var d = new LoudSoundDetector();
        double t = 0;
        VoiceSim.Hear(d, ref t, 0.05, Room);
        VoiceSim.Hear(d, ref t, 0.1, Call);
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 0.3, Room), "still warming up");
        Assert.False(d.Loud, "that sound is over");

        VoiceSim.Hear(d, ref t, 2.0, Room);
        d.Suppress(t + 2.5);
        VoiceSim.Hear(d, ref t, 0.4, Call);
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 0.5, Room), "the game's own applause is ignored");
        VoiceSim.Hear(d, ref t, 2.0, Room);
        VoiceSim.Hear(d, ref t, 0.3, Call);
        Assert.Equal(1, VoiceSim.Hear(d, ref t, 0.5, Room), "listening again afterwards");
        Assert.Equal(1, d.Count);
    }

    [Test]
    public static void BadLevelsAreTreatedAsSilence()
    {
        var d = new LoudSoundDetector();
        double t = 0;
        VoiceSim.Hear(d, ref t, 1.0, float.NaN);
        Assert.Equal(0, VoiceSim.Hear(d, ref t, 1.0, -1f));
        Assert.Near(0.0, d.Envelope, 1e-6);
    }
}
