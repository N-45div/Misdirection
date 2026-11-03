// Unit tests for Misdirection.Core.Crowd (attention model) and the show-hand flourish detector.
using System;
using System.Numerics;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;

public static class Stage
{
    public static readonly Vector3 Eyes = new Vector3(0f, 1.20f, 0f);
    public static readonly Vector3 Hands = new Vector3(0f, 0.80f, 0.35f);
    public static readonly Vector3 Mat = new Vector3(0f, 0.75f, 0.45f);
    public const float Table = 0.75f;
    public const float Dt = 1f / 72f;

    public static readonly Vector3 TouristEyes = new Vector3(-0.6f, 1.55f, 1.6f);
    public static readonly Vector3 KidEyes = new Vector3(0f, 1.15f, 1.7f);
    public static readonly Vector3 SkepticEyes = new Vector3(0.6f, 1.55f, 1.6f);

    /// <summary>Tourist (0), Kid (1), Skeptic (2) across the table.</summary>
    public static CrowdModel ThreeSpectators()
    {
        var crowd = new CrowdModel();
        crowd.Add(SpectatorProfile.For(Archetype.Tourist), TouristEyes);
        crowd.Add(SpectatorProfile.For(Archetype.Kid), KidEyes);
        crowd.Add(SpectatorProfile.For(Archetype.Skeptic), SkepticEyes);
        return crowd;
    }

    public static double Run(CrowdModel crowd, double t, double seconds, Vector3 lookAt)
    {
        Vector3 dir = Vector3.Normalize(lookAt - Eyes);
        int frames = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
        {
            t += Dt;
            crowd.Update(t, Dt, Eyes, dir, Hands, Mat, Table);
        }
        return t;
    }

    public static float Dist(Vector3 a, Vector3 b)
    {
        return Vector3.Distance(a, b);
    }
}

public static class GazeTests
{
    [Test]
    public static void GazePointLandsOnTheTableOrAhead()
    {
        Vector3 down = Vector3.Normalize(Stage.Mat - Stage.Eyes);
        Vector3 p = CrowdModel.GazePointOf(Stage.Eyes, down, Stage.Table, 1.5f, 3f);
        Assert.Near(0.75, p.Y, 1e-4, "on the table");
        Assert.Near(0.45, p.Z, 1e-3, "at the mat");
        Vector3 ahead = CrowdModel.GazePointOf(Stage.Eyes, Vector3.UnitZ, Stage.Table, 1.5f, 3f);
        Assert.Near(1.5, ahead.Z, 1e-5, "1.5 m ahead");
    }

    [Test]
    public static void LookingAtAFaceSelectsThatSpectatorWithHysteresis()
    {
        var crowd = Stage.ThreeSpectators();
        double t = Stage.Run(crowd, 0, 0.1, Stage.SkepticEyes);
        Assert.Equal(2, crowd.LookedAt, "looking at the skeptic");
        // Aim about 8 degrees off the skeptic's face: still held (release at 9).
        Vector3 off = Vector3.Normalize(Stage.SkepticEyes - Stage.Eyes);
        Vector3 aim = Vector3.Normalize(off + new Vector3(0f, 0.14f, 0f));
        float angle = CrowdModel.AngleDegrees(off, aim);
        Assert.True(angle > 7f && angle < 9f, "test angle " + angle);
        crowd.Update(t + Stage.Dt, Stage.Dt, Stage.Eyes, aim, Stage.Hands, Stage.Mat, Stage.Table);
        Assert.Equal(2, crowd.LookedAt, "hysteresis keeps the face");
        var fresh = Stage.ThreeSpectators();
        fresh.Update(Stage.Dt, Stage.Dt, Stage.Eyes, aim, Stage.Hands, Stage.Mat, Stage.Table);
        Assert.Equal(-1, fresh.LookedAt, "8 degrees off doesn't acquire a face");
        Stage.Run(crowd, t, 0.1, Stage.Mat);
        Assert.Equal(-1, crowd.LookedAt, "looking at the mat");
    }
}

public static class AttentionTests
{
    [Test]
    public static void WhereYouLookTheyLook()
    {
        var crowd = Stage.ThreeSpectators();
        Stage.Run(crowd, 0, 2.0, Stage.KidEyes);
        SpectatorMind tourist = crowd.Spectators[0];
        Assert.True(Stage.Dist(tourist.AttentionPoint, Stage.KidEyes) < 0.15f, "tourist looks where the magician looks: " + tourist.AttentionPoint);
        Assert.Equal(AttentionFocus.Gaze, tourist.Focus);
    }

    [Test]
    public static void EyeContactLocksThenReleasesAfterAMoment()
    {
        var crowd = Stage.ThreeSpectators();
        SpectatorMind tourist = crowd.Spectators[0];
        double t = Stage.Run(crowd, 0, 0.2, Stage.TouristEyes);
        Assert.False(tourist.EyeContact, "needs a 0.3 s dwell");
        t = Stage.Run(crowd, t, 0.6, Stage.TouristEyes);
        Assert.True(tourist.EyeContact, "locked");
        t = Stage.Run(crowd, t, 1.0, Stage.Mat);
        Assert.True(tourist.EyeContact, "lock holds after looking away");
        Assert.True(Stage.Dist(tourist.AttentionPoint, Stage.Eyes) < 0.3f, "still watching the magician's face");
        t = Stage.Run(crowd, t, 1.0, Stage.Mat);
        Assert.False(tourist.EyeContact, "released after ~1.5 s");
    }

    [Test]
    public static void StaringTooLongMakesThemLookAwayAtTheHands()
    {
        var crowd = Stage.ThreeSpectators();
        SpectatorMind kid = crowd.Spectators[1];
        double t = Stage.Run(crowd, 0, 2.8, Stage.KidEyes);
        Assert.True(kid.LookingAway, "kid tolerates 2.5 s of staring");
        Assert.False(kid.EyeContact);
        t = Stage.Run(crowd, t, 1.0, Stage.KidEyes);
        Assert.True(Stage.Dist(kid.AttentionPoint, Stage.Hands) < 0.25f, "looks at the hands: " + kid.AttentionPoint);
        Assert.Equal(AttentionFocus.LookingAway, kid.Focus);
    }

    [Test]
    public static void SkepticWatchesHandsUnlessWonWithEyeContact()
    {
        var crowd = Stage.ThreeSpectators();
        SpectatorMind skeptic = crowd.Spectators[2];
        Stage.Run(crowd, 0, 2.0, Stage.TouristEyes);
        Assert.True(Stage.Dist(skeptic.AttentionPoint, Stage.Hands) < 0.12f, "skeptic ignores the gaze: " + skeptic.AttentionPoint);
        WitnessReport seen = crowd.Witness(Stage.Hands, 1f);
        Assert.Equal(2, seen.SeenBy, "skeptic catches the move");
        Assert.True(skeptic.Suspicion > 0.9f, "suspicion jumps, got " + skeptic.Suspicion);
    }

    [Test]
    public static void SecretMoveDuringEyeContactWithTheSkepticGoesUnseen()
    {
        var crowd = Stage.ThreeSpectators();
        Stage.Run(crowd, 0, 1.5, Stage.SkepticEyes);
        Assert.True(crowd.Spectators[2].EyeContact, "won the skeptic");
        WitnessReport report = crowd.Witness(Stage.Hands, 1f);
        Assert.True(report.Unseen, "nobody sees the hands, best coverage " + report.MaxCoverage + " by " + report.SeenBy);
        Assert.Near(0.0, crowd.MaxSuspicion, 1e-6, "no suspicion");
    }

    [Test]
    public static void KidFollowsAFlourishThenDriftsBack()
    {
        var crowd = Stage.ThreeSpectators();
        SpectatorMind kid = crowd.Spectators[1];
        Vector3 flourish = new Vector3(-0.45f, 1.05f, 0.35f);
        double t = Stage.Run(crowd, 0, 1.0, Stage.Mat);
        crowd.Flourish(t, flourish);
        t = Stage.Run(crowd, t, 0.6, Stage.Mat);
        Assert.Equal(AttentionFocus.Flourish, kid.Focus);
        Assert.True(Stage.Dist(kid.AttentionPoint, flourish) < 0.25f, "kid looks at the flourish: " + kid.AttentionPoint);
        t = Stage.Run(crowd, t, 5.0, Stage.Mat);
        Assert.True(Stage.Dist(kid.AttentionPoint, flourish) > 0.3f, "drifts back once it fades");
    }

    [Test]
    public static void SoundPullsAttention()
    {
        var crowd = Stage.ThreeSpectators();
        Vector3 clink = new Vector3(0.3f, 0.75f, 0.2f);
        double t = Stage.Run(crowd, 0, 1.0, Stage.KidEyes + new Vector3(0, 0.5f, 0));
        crowd.Sound(t, clink);
        Stage.Run(crowd, t, 0.5, Stage.KidEyes + new Vector3(0, 0.5f, 0));
        Assert.Equal(AttentionFocus.Sound, crowd.Spectators[1].Focus, "the kid turns to the clink");
    }

    [Test]
    public static void FilmerPhoneAlwaysCoversTheMat()
    {
        var crowd = new CrowdModel();
        crowd.Add(SpectatorProfile.For(Archetype.Filmer), new Vector3(0.9f, 1.5f, 1.2f));
        Stage.Run(crowd, 0, 1.0, Stage.TouristEyes);
        Assert.Near(1.0, crowd.Spectators[0].Coverage(Stage.Mat), 1e-6, "phone on the mat");
        Assert.False(crowd.Witness(Stage.Mat + new Vector3(0.1f, 0f, 0f), 0.5f).Unseen, "move on the mat is filmed");
    }
}

public static class SuspicionTests
{
    [Test]
    public static void ConeCoverageHasASoftEdge()
    {
        var crowd = new CrowdModel();
        crowd.Add(SpectatorProfile.For(Archetype.Tourist), Vector3.Zero);
        SpectatorMind m = crowd.Spectators[0];
        // Make the tourist look straight down +Z.
        crowd.Update(0, Stage.Dt, new Vector3(0, 0, 5), Vector3.UnitZ, new Vector3(0, 0, 5), new Vector3(0, 0, 5), -10f);
        Assert.Near(1.0, m.Coverage(new Vector3(0, 0, 1)), 1e-6, "on axis");
        float at15 = m.Coverage(new Vector3((float)Math.Tan(15 * Math.PI / 180), 0, 1));
        Assert.Near(0.5, at15, 0.02, "halfway through the soft edge (12 + 3 of 6)");
        Assert.Near(0.0, m.Coverage(new Vector3((float)Math.Tan(20 * Math.PI / 180), 0, 1)), 1e-6, "outside");
    }

    [Test]
    public static void LevelsDecayAndExposure()
    {
        var crowd = Stage.ThreeSpectators();
        SpectatorMind tourist = crowd.Spectators[0];
        tourist.AddSuspicion(0.75f);
        Assert.Equal(SuspicionLevel.Suspicious, tourist.Level);
        Stage.Run(crowd, 0, 10.0, Stage.Mat);
        Assert.Equal(SuspicionLevel.Curious, tourist.Level, "decays slowly");
        tourist.AddSuspicion(1.0f);
        Assert.Equal(SuspicionLevel.WalkingOff, tourist.Level);
        tourist.ResetSuspicion();
        Assert.Equal(SuspicionLevel.Calm, tourist.Level);

        var watched = Stage.ThreeSpectators();
        Stage.Run(watched, 0, 2.0, Stage.Mat);
        for (int i = 0; i < 72; i++) watched.Expose(Stage.Hands, Stage.Dt);
        float s = watched.Spectators[2].Suspicion;
        Assert.True(s > 0.15f && s < 0.3f, "a second of exposure in the skeptic's cone, got " + s);
    }
}

public static class FlourishTests
{
    static readonly Vector3 Eyes = new Vector3(0f, 1.2f, 0f);
    const float Table = 0.75f;
    static readonly Vector3 Outward = Vector3.UnitZ;

    /// <summary>Sweep the hand from <paramref name="from"/> to <paramref name="to"/> in 0.3 s, then hold.</summary>
    static int Gesture(FlourishDetector d, ref double t, Vector3 from, Vector3 to, Vector3 normal, float openness, double hold, bool reliable = true)
    {
        int fired = 0;
        const double dt = 1.0 / 72.0;
        for (int i = 0; i <= 21; i++)
        {
            t += dt;
            if (d.Update(t, reliable, Vector3.Lerp(from, to, i / 21f), normal, openness, Eyes, Table)) fired++;
        }
        for (double h = 0; h < hold; h += dt)
        {
            t += dt;
            if (d.Update(t, reliable, to, normal, openness, Eyes, Table)) fired++;
        }
        return fired;
    }

    static readonly Vector3 Start = new Vector3(0.05f, 0.85f, 0.35f);
    static readonly Vector3 End = new Vector3(0.35f, 1.00f, 0.45f);

    [Test]
    public static void SweepThenOpenPalmHeldFiresOnce()
    {
        var d = new FlourishDetector();
        double t = 0;
        Assert.Equal(1, Gesture(d, ref t, Start, End, Outward, 0.9f, 0.6), "one flourish");
        Assert.Near(End.X, d.LastPoint.X, 1e-5, "at the palm");
    }

    [Test]
    public static void PalmUpPresentingCounts()
    {
        var d = new FlourishDetector();
        double t = 0;
        Assert.Equal(1, Gesture(d, ref t, Start, End, Vector3.UnitY, 0.9f, 0.5));
    }

    [Test]
    public static void NoSweepNoFlourish()
    {
        var d = new FlourishDetector();
        double t = 0;
        Assert.Equal(0, Gesture(d, ref t, End, End, Outward, 0.9f, 1.0));
    }

    [Test]
    public static void PalmFacingThePlayerNeverCounts()
    {
        var d = new FlourishDetector();
        double t = 0;
        Vector3 toFace = Vector3.Normalize(Eyes - End);
        Assert.Equal(0, Gesture(d, ref t, Start, End, toFace, 1f, 1.0), "system-gesture pose");
        Assert.Equal(0, Gesture(d, ref t, Start, End, -Vector3.UnitY, 1f, 1.0), "palm down resting");
        Assert.Equal(0, Gesture(d, ref t, Start, End, Outward, 0.4f, 1.0), "fist");
        Assert.Equal(0, Gesture(d, ref t, Start, End, Outward, 0.9f, 1.0, false), "unreliable tracking");
        var low = new FlourishDetector();
        double t2 = 0;
        Assert.Equal(0, Gesture(low, ref t2, new Vector3(0, 0.76f, 0.3f), new Vector3(0.3f, 0.78f, 0.3f), Outward, 0.9f, 1.0), "on the table");
    }

    [Test]
    public static void NeedsANewGestureAndCooldown()
    {
        var d = new FlourishDetector();
        double t = 0;
        Assert.Equal(1, Gesture(d, ref t, Start, End, Outward, 0.9f, 2.0), "holding longer doesn't repeat");
        Assert.Equal(0, Gesture(d, ref t, End, Start, Outward, 0.9f, 0.5), "still within the pose: not re-armed");
        // Break the pose (close the hand), then sweep again after the cooldown.
        Gesture(d, ref t, Start, Start, Outward, 0.2f, 1.2);
        Assert.Equal(1, Gesture(d, ref t, Start, End, Outward, 0.9f, 0.5), "a new gesture fires");
    }
}

public static class HandGeometryTests
{
    [Test]
    public static void PalmNormalPointsDownForBothHandsResting()
    {
        // Right hand palm down: thumb side (index) on the left.
        Vector3 r = HandGeometry.PalmNormal(Vector3.Zero, new Vector3(-0.02f, 0, 0.09f), new Vector3(0.02f, 0, 0.085f), true);
        Assert.Near(-1.0, r.Y, 1e-5, "right palm down");
        Vector3 l = HandGeometry.PalmNormal(Vector3.Zero, new Vector3(0.02f, 0, 0.09f), new Vector3(-0.02f, 0, 0.085f), false);
        Assert.Near(-1.0, l.Y, 1e-5, "left palm down");
        Assert.Equal(Vector3.Zero, HandGeometry.PalmNormal(Vector3.Zero, Vector3.UnitZ, Vector3.UnitZ * 2, true), "degenerate");
    }
}
