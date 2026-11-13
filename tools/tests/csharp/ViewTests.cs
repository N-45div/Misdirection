// Unit tests for Misdirection.Core.View: field-of-view geometry and the comfort follow (Day 12).
using System;
using System.Numerics;
using Misdirection.Core.View;

public static class ViewConeTests
{
    private static readonly Vector3 Eye = new Vector3(0f, 1.2f, 0f);

    /// <summary>A point <paramref name="distance"/> away, <paramref name="yaw"/> degrees right and <paramref name="pitch"/> up of straight ahead (+z).</summary>
    public static Vector3 At(float yaw, float pitch, float distance)
    {
        double y = yaw * Math.PI / 180.0;
        double p = pitch * Math.PI / 180.0;
        Vector3 dir = new Vector3((float)(Math.Sin(y) * Math.Cos(p)), (float)Math.Sin(p), (float)(Math.Cos(y) * Math.Cos(p)));
        return Eye + dir * distance;
    }

    [Test]
    public static void PointsAheadAreInsideAndPointsAsideAreNot()
    {
        FieldOfView comfort = FieldOfView.Comfort;
        Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, comfort, At(0f, 0f, 1f)), "straight ahead");
        Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, comfort, At(24f, 0f, 1f)), "24 degrees right is inside 50 wide");
        Assert.False(ViewCone.Contains(Eye, Vector3.UnitZ, comfort, At(26f, 0f, 1f)), "26 degrees right is outside 50 wide");
        Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, comfort, At(0f, -19f, 1f)), "19 degrees down is inside 40 tall");
        Assert.False(ViewCone.Contains(Eye, Vector3.UnitZ, comfort, At(0f, 21f, 1f)), "21 degrees up is outside 40 tall");
        Assert.False(ViewCone.Contains(Eye, Vector3.UnitZ, comfort, Eye - Vector3.UnitZ), "behind the viewer");
        Assert.False(ViewCone.Contains(Eye, Vector3.Zero, comfort, At(0f, 0f, 1f)), "no view direction");
    }

    [Test]
    public static void GlassesSeeMoreThanTheComfortZoneAndLessThanQuest()
    {
        Vector3 p = At(30f, 0f, 1f);
        Assert.False(ViewCone.Contains(Eye, Vector3.UnitZ, FieldOfView.Comfort, p), "outside comfort");
        Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, FieldOfView.Glasses, p), "inside the glasses' 70 degrees");
        Vector3 wide = At(45f, 0f, 1f);
        Assert.False(ViewCone.Contains(Eye, Vector3.UnitZ, FieldOfView.Glasses, wide), "outside the glasses");
        Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, new FieldOfView(110f, 96f), wide), "inside Quest 3");
    }

    [Test]
    public static void ItFollowsTheViewDirection()
    {
        // Looking 40 degrees to the right, a point 40 degrees right is dead centre.
        Vector3 forward = Vector3.Normalize(At(40f, 0f, 1f) - Eye);
        Assert.True(ViewCone.Contains(Eye, forward, FieldOfView.Comfort, At(40f, 0f, 2f)), "in the centre of the view");
        Assert.False(ViewCone.Contains(Eye, forward, FieldOfView.Comfort, At(0f, 0f, 2f)), "straight ahead is now 40 degrees off");
    }

    [Test]
    public static void AnglesReadYawRightAndPitchUp()
    {
        float yaw, pitch;
        ViewCone.Angles(Eye, Vector3.UnitZ, Vector3.UnitY, At(20f, 0f, 1.5f), out yaw, out pitch);
        Assert.Near(20, yaw, 1e-3, "yaw");
        Assert.Near(0, pitch, 1e-3, "pitch");
        ViewCone.Angles(Eye, Vector3.UnitZ, Vector3.UnitY, At(0f, -17.5f, 1f), out yaw, out pitch);
        Assert.Near(0, yaw, 1e-3, "yaw");
        Assert.Near(-17.5, pitch, 1e-3, "pitch");
        ViewCone.Angles(Eye, Vector3.UnitZ, Vector3.UnitY, At(-30f, 0f, 1f), out yaw, out pitch);
        Assert.Near(-30, yaw, 1e-3, "left is negative");
    }

    [Test]
    public static void ATiltedHeadTiltsTheField()
    {
        // 66 tall and 70 wide: a point 34 degrees up is outside, unless the head is rolled a quarter turn.
        Vector3 p = At(0f, 34f, 1f);
        Assert.False(ViewCone.Contains(Eye, Vector3.UnitZ, Vector3.UnitY, FieldOfView.Glasses, p), "upright: outside");
        Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, Vector3.UnitX, FieldOfView.Glasses, p), "rolled 90 degrees: inside");
    }

    [Test]
    public static void NearestLeavesInsidePointsAlone()
    {
        Vector3 p = At(10f, -8f, 0.9f);
        Vector3 n = ViewCone.Nearest(Eye, Vector3.UnitZ, FieldOfView.Comfort, p);
        Assert.Near(0, Vector3.Distance(p, n), 1e-6, "unchanged");
    }

    [Test]
    public static void NearestSlidesAnOutsidePointToTheEdgeAtTheSameDistance()
    {
        Vector3 p = At(60f, 0f, 0.9f);
        Vector3 n = ViewCone.Nearest(Eye, Vector3.UnitZ, FieldOfView.Comfort, p);
        Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, FieldOfView.Comfort, n), "inside");
        Assert.Near(0.9, Vector3.Distance(Eye, n), 1e-4, "same distance from the eye");
        float yaw, pitch;
        ViewCone.Angles(Eye, Vector3.UnitZ, Vector3.UnitY, n, out yaw, out pitch);
        Assert.Near(25, yaw, 1e-2, "on the right edge");
        Assert.Near(0, pitch, 1e-2, "same height");

        // Below and to the left: lands in the bottom-left corner.
        Vector3 q = At(-50f, -45f, 1.2f);
        Vector3 m = ViewCone.Nearest(Eye, Vector3.UnitZ, FieldOfView.Comfort, q);
        ViewCone.Angles(Eye, Vector3.UnitZ, Vector3.UnitY, m, out yaw, out pitch);
        Assert.Near(-25, yaw, 1e-2, "left edge");
        Assert.Near(-20, pitch, 1e-2, "bottom edge");
        Assert.Near(1.2, Vector3.Distance(Eye, m), 1e-4, "same distance");
    }

    [Test]
    public static void NearestCopesWithPointsBesideAndBehind()
    {
        Vector3[] awkward =
        {
            Eye + new Vector3(1f, 0f, 0f),       // exactly to the right
            Eye + new Vector3(-0.5f, 0.1f, -1f), // behind, left
            Eye - Vector3.UnitZ,                 // exactly behind
            Eye + new Vector3(0f, 1f, 0f),       // straight up
        };
        foreach (Vector3 p in awkward)
        {
            Vector3 n = ViewCone.Nearest(Eye, Vector3.UnitZ, FieldOfView.Comfort, p);
            Assert.False(float.IsNaN(n.X) || float.IsNaN(n.Y) || float.IsNaN(n.Z), "a real point");
            Assert.True(ViewCone.Contains(Eye, Vector3.UnitZ, FieldOfView.Comfort, n), "inside for " + p);
            Assert.Near(Vector3.Distance(Eye, p), Vector3.Distance(Eye, n), 1e-4, "same distance");
        }
        // A point to the right stays on the right.
        Vector3 right = ViewCone.Nearest(Eye, Vector3.UnitZ, FieldOfView.Comfort, Eye + new Vector3(1f, 0f, 0f));
        Assert.True(right.X > Eye.X, "right edge");
        // Looking straight down still works (no usable "up").
        Vector3 down = ViewCone.Nearest(Eye, -Vector3.UnitY, FieldOfView.Comfort, At(0f, 0f, 1f));
        Assert.True(ViewCone.Contains(Eye, -Vector3.UnitY, FieldOfView.Comfort, down), "inside when looking down");
    }
}

public static class ComfortFollowTests
{
    private const float Dt = 1f / 72f;
    private static readonly Vector3 Eye = new Vector3(0f, 1.2f, 0f);

    /// <summary>The trick HUD seen from a seated player: about 0.96 m away, 17.5 degrees below eye level.</summary>
    private static readonly Vector3 Hud = ViewConeTests.At(0f, -17.5f, 0.96f);

    private static Vector3 Look(float yaw, float pitch)
    {
        return Vector3.Normalize(ViewConeTests.At(yaw, pitch, 1f) - Eye);
    }

    private static Vector3 Run(ComfortFollow follow, Vector3 forward, Vector3 home, double seconds)
    {
        int frames = (int)Math.Round(seconds / Dt);
        Vector3 p = follow.Position;
        for (int i = 0; i < frames; i++)
        {
            p = follow.Update(Eye, forward, home, Dt);
        }
        return p;
    }

    [Test]
    public static void ItStaysHomeWhileHomeIsComfortable()
    {
        ComfortFollow follow = new ComfortFollow();
        // Looking at the mat (about 40 degrees down), then at the HUD, then level: the HUD is within 20 degrees each time.
        Vector3 p = Run(follow, Look(0f, -35f), Hud, 2.0);
        Assert.Near(0, Vector3.Distance(p, Hud), 1e-6, "home while looking at the mat");
        p = Run(follow, Look(10f, 0f), Hud, 2.0);
        Assert.Near(0, Vector3.Distance(p, Hud), 1e-6, "home while looking level");
        Assert.False(follow.Displaced, "not displaced");
    }

    [Test]
    public static void AGlanceAwayDoesNotMoveIt()
    {
        ComfortFollow follow = new ComfortFollow();
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        // Eye contact with a tall spectator (13 degrees up) for 0.4 s: shorter than the 0.6 s delay.
        Vector3 p = Run(follow, Look(-15f, 13f), Hud, 0.4);
        Assert.Near(0, Vector3.Distance(p, Hud), 1e-6, "still home");
        p = Run(follow, Look(0f, -17.5f), Hud, 1.0);
        Assert.Near(0, Vector3.Distance(p, Hud), 1e-6, "home after looking back");
        Assert.False(follow.Displaced, "never displaced");
    }

    [Test]
    public static void LookingAwayBringsItIntoTheComfortZone()
    {
        ComfortFollow follow = new ComfortFollow();
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        Vector3 away = Look(0f, 13f); // the HUD is now 30.5 degrees below the view centre
        Vector3 p = Run(follow, away, Hud, 3.0);
        Assert.True(follow.Displaced, "displaced");
        Assert.True(ViewCone.Contains(Eye, away, FieldOfView.Comfort.Grown(0.5f), p), "settled inside the comfort zone");
        Assert.True(ViewCone.Contains(Eye, away, FieldOfView.Glasses, p), "so also inside the glasses' field of view");
        Assert.Near(0.96, Vector3.Distance(Eye, p), 0.02, "about as far away as before");
        Assert.True(p.Y > Hud.Y, "it came up toward the view");
    }

    [Test]
    public static void ItEasesAndNeverJumps()
    {
        ComfortFollow follow = new ComfortFollow();
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        Vector3 away = Look(70f, 10f);
        Vector3 last = follow.Position;
        float biggestStep = 0f;
        for (int i = 0; i < 72 * 3; i++)
        {
            Vector3 p = follow.Update(Eye, away, Hud, Dt);
            biggestStep = Math.Max(biggestStep, Vector3.Distance(p, last));
            last = p;
        }
        // The whole move is about 0.9 m; eased at 5/s, no frame moves it more than a few centimetres.
        Assert.True(biggestStep < 0.08f, "largest step " + biggestStep);
        Assert.True(biggestStep > 0f, "it did move");
    }

    [Test]
    public static void AStillOrTremblingHeadMeansAStillElement()
    {
        ComfortFollow follow = new ComfortFollow();
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        Run(follow, Look(0f, 13f), Hud, 4.0);
        Vector3 settled = follow.Position;
        // Head tremor of half a degree either way, for three seconds.
        float worst = 0f;
        for (int i = 0; i < 72 * 3; i++)
        {
            float wobble = 0.5f * (float)Math.Sin(i * 0.9);
            Vector3 p = follow.Update(Eye, Look(wobble, 13f + 0.5f * (float)Math.Cos(i * 1.3)), Hud, Dt);
            worst = Math.Max(worst, Vector3.Distance(p, settled));
        }
        Assert.True(worst < 1e-3f, "moved " + worst + " m under tremor");
    }

    [Test]
    public static void ItGoesHomeWhenHomeIsComfortableAgain()
    {
        ComfortFollow follow = new ComfortFollow();
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        Run(follow, Look(50f, 13f), Hud, 3.0);
        Assert.True(follow.Displaced, "displaced while looking away");
        Vector3 p = Run(follow, Look(0f, -10f), Hud, 3.0);
        Assert.False(follow.Displaced, "home again");
        Assert.Near(0, Vector3.Distance(p, Hud), 1e-3, "back at home");
    }

    [Test]
    public static void ItKeepsUpWhenTheViewKeepsTurning()
    {
        ComfortFollow follow = new ComfortFollow();
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        // Turn steadily to the right, 30 degrees a second, for three seconds.
        float yaw = 0f;
        Vector3 forward = Look(0f, 0f);
        for (int i = 0; i < 72 * 3; i++)
        {
            yaw += 30f * Dt;
            forward = Look(yaw, 0f);
            follow.Update(Eye, forward, Hud, Dt);
        }
        Vector3 p = Run(follow, forward, Hud, 2.0);
        Assert.True(ViewCone.Contains(Eye, forward, FieldOfView.Comfort.Grown(4.5f), p), "within the zone plus its slack after the turn");
        Assert.True(ViewCone.Contains(Eye, forward, FieldOfView.Glasses, p), "inside the glasses' field of view");
    }

    [Test]
    public static void AfterAResetItAppearsInPlace()
    {
        ComfortFollow follow = new ComfortFollow();
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        follow.Reset();
        // Shown again while the player looks far away: it appears inside the zone at once.
        Vector3 away = Look(-80f, 0f);
        Vector3 p = follow.Update(Eye, away, Hud, Dt);
        Assert.True(ViewCone.Contains(Eye, away, FieldOfView.Comfort, p), "inside on the first frame");
        Assert.True(follow.Displaced, "displaced");
    }

    [Test]
    public static void AtHomeItIsFixedToItsHomeEvenWhenHomeMoves()
    {
        ComfortFollow follow = new ComfortFollow();
        Vector3 forward = Look(0f, -10f);
        Vector3 home = Hud;
        follow.Update(Eye, forward, home, Dt);
        // A reaction drifting up 4 cm a second: no lag at all.
        for (int i = 0; i < 72; i++)
        {
            home += new Vector3(0f, 0.04f * Dt, 0f);
            Vector3 p = follow.Update(Eye, forward, home, Dt);
            Assert.Near(0, Vector3.Distance(p, home), 1e-6, "exactly at home");
        }
        // Also during the delay after looking away: still exactly at home.
        Vector3 q = follow.Update(Eye, Look(0f, 40f), home, Dt);
        Assert.Near(0, Vector3.Distance(q, home), 1e-6, "at home during the delay");
    }

    [Test]
    public static void AHomeThatJumpsIsANewPlaceNotASlide()
    {
        ComfortFollow follow = new ComfortFollow();
        Vector3 forward = Look(0f, 13f);
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        Run(follow, forward, Hud, 3.0);
        Assert.True(follow.Displaced, "displaced first");
        // The caption moves to a spot 30 cm away that is comfortable: it is simply there.
        Vector3 newHome = ViewConeTests.At(5f, 10f, 1.4f);
        Vector3 p = follow.Update(Eye, forward, newHome, Dt);
        Assert.Near(0, Vector3.Distance(p, newHome), 1e-6, "appears at the new home");
        Assert.False(follow.Displaced, "not displaced");
        // ...and a jump to an uncomfortable spot appears inside the zone at once.
        Vector3 farHome = ViewConeTests.At(-70f, 0f, 1.4f);
        Vector3 r = follow.Update(Eye, forward, farHome, Dt);
        Assert.True(ViewCone.Contains(Eye, forward, FieldOfView.Comfort, r), "inside the zone on the first frame");
    }

    [Test]
    public static void SettingsChangeTheZone()
    {
        ComfortSettings wide = new ComfortSettings();
        wide.HorizontalDegrees = 100f;
        wide.VerticalDegrees = 90f;
        ComfortFollow follow = new ComfortFollow(wide);
        Run(follow, Look(0f, -17.5f), Hud, 0.5);
        Vector3 p = Run(follow, Look(0f, 13f), Hud, 3.0);
        Assert.Near(0, Vector3.Distance(p, Hud), 1e-6, "30 degrees off is fine in a 90 degree tall zone");
    }
}
