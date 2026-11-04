// Unit tests for Day 5 room logic: table detection from the room scan, and crowd layout around walls.
using System;
using System.Collections.Generic;
using System.Numerics;
using Misdirection.Core.Crowd;
using Misdirection.Core.Table;

public static class RoomFixtures
{
    public static readonly Vector3 Eyes = new Vector3(0f, 1.20f, 0f);

    /// <summary>Axis-aligned table top: x in [x0,x1], z in [z0,z1], at height y.</summary>
    public static TableCandidate Box(float x0, float x1, float z0, float z1, float y = 0.75f, string id = null)
    {
        return TableCandidate.FromCorners(new Vector3(x0, y, z0), new Vector3(x1, y, z0), new Vector3(x1, y, z1), new Vector3(x0, y, z1), y, id);
    }

    /// <summary>Rectangle room walls (x in [x0,x1], z in [z0,z1]).</summary>
    public static List<Segment2> Room(float x0, float x1, float z0, float z1)
    {
        return new List<Segment2>
        {
            new Segment2(x0, z0, x1, z0), new Segment2(x1, z0, x1, z1), new Segment2(x1, z1, x0, z1), new Segment2(x0, z1, x0, z0),
        };
    }
}

public static class TableDetectionTests
{
    [Test]
    public static void FrameSitsOnTheNearEdgeFacingAcross()
    {
        TableCandidate t = RoomFixtures.Box(-0.6f, 0.6f, 0.2f, 1.0f);
        TableFrame f = TableDetection.FrameFor(t, RoomFixtures.Eyes);
        Assert.Near(0.0, f.Origin.X, 1e-5, "x");
        Assert.Near(0.2, f.Origin.Z, 1e-5, "near edge");
        Assert.Near(0.75, f.Origin.Y, 1e-5, "table top");
        Assert.Near(1.0, f.Forward.Z, 1e-5, "facing across");
    }

    [Test]
    public static void OffCentrePlayerAndShortEdgesClampTheOrigin()
    {
        TableCandidate t = RoomFixtures.Box(-0.6f, 0.6f, 0.2f, 1.0f);
        TableFrame f = TableDetection.FrameFor(t, new Vector3(0.55f, 1.2f, 0f));
        Assert.Near(0.38, f.Origin.X, 1e-5, "kept 22 cm from the corner for the mat");
        TableCandidate narrow = RoomFixtures.Box(-0.15f, 0.15f, 0.2f, 1.0f);
        Assert.Near(0.0, TableDetection.FrameFor(narrow, new Vector3(0.4f, 1.2f, 0f)).Origin.X, 1e-5, "edge shorter than the mat: centre");
    }

    [Test]
    public static void SittingAtTheSideOrARotatedTable()
    {
        TableCandidate t = RoomFixtures.Box(0.3f, 1.1f, -0.6f, 0.6f);
        TableFrame f = TableDetection.FrameFor(t, RoomFixtures.Eyes);
        Assert.Near(0.3, f.Origin.X, 1e-5, "player on the -x side");
        Assert.Near(1.0, f.Forward.X, 1e-5, "facing +x");

        // A table rotated 45 degrees, corners listed clockwise.
        float r = 0.4f;
        Vector3 c = new Vector3(0f, 0.75f, 0.7f);
        TableCandidate d = TableCandidate.FromCorners(c + new Vector3(0, 0, -r), c + new Vector3(-r, 0, 0), c + new Vector3(0, 0, r), c + new Vector3(r, 0, 0), 0.75f);
        TableFrame g = TableDetection.FrameFor(d, new Vector3(-0.3f, 1.2f, 0f));
        Assert.True(g.Forward.Z > 0.6f && g.Forward.X > 0.6f, "faces into the diamond: " + g.Forward);
    }

    [Test]
    public static void ChoosesTheUsableTableNearestThePlayer()
    {
        var tables = new List<TableCandidate>
        {
            RoomFixtures.Box(-0.1f, 0.1f, 0.2f, 0.4f, 0.75f, "stool"),
            RoomFixtures.Box(-0.6f, 0.6f, 2.5f, 3.3f, 0.75f, "far table"),
            RoomFixtures.Box(-0.6f, 0.6f, 0.3f, 0.9f, 1.15f, "shelf"),
            RoomFixtures.Box(-0.5f, 0.5f, 0.6f, 1.2f, 0.72f, "desk"),
            RoomFixtures.Box(-0.6f, 0.6f, 0.25f, 0.9f, 0.70f, "dining"),
        };
        var reasons = new List<TableRejection>();
        int best = TableDetection.Choose(tables, RoomFixtures.Eyes, new TableSearchSettings(), reasons);
        Assert.Equal("dining", tables[best].Id);
        Assert.Equal(TableRejection.TooSmall, reasons[0], "stool");
        Assert.Equal(TableRejection.TooFar, reasons[1], "far table");
        Assert.Equal(TableRejection.WrongHeight, reasons[2], "shelf at eye level");
        Assert.Equal(TableRejection.None, reasons[3], "desk is fine, just further");
        Assert.Equal(-1, TableDetection.Choose(new List<TableCandidate>(), RoomFixtures.Eyes, null), "no tables");
    }

    [Test]
    public static void EdgeDistanceIsZeroOverTheTable()
    {
        TableCandidate t = RoomFixtures.Box(-0.6f, 0.6f, -0.2f, 1.0f);
        Assert.Near(0.0, TableDetection.EdgeDistance(t, RoomFixtures.Eyes), 1e-6, "leaning over it");
        Assert.Near(0.5, TableDetection.EdgeDistance(RoomFixtures.Box(-0.6f, 0.6f, 0.5f, 1.0f), RoomFixtures.Eyes), 1e-5);
    }
}

public static class CrowdLayoutTests
{
    static readonly Vector2 Eyes = Vector2.Zero;
    static readonly Vector2 Ahead = new Vector2(0f, 1f);

    [Test]
    public static void OpenRoomUsesTheFullArc()
    {
        LayoutResult result;
        float arc;
        var slots = CrowdLayout.Place(Eyes, Ahead, 3, new List<Segment2>(), null, out result, out arc);
        Assert.Equal(LayoutResult.Open, result);
        Assert.Equal(3, slots.Count);
        Assert.Near(30.0, arc, 1e-3, "arc");
        Assert.Near(-0.775, slots[0].Position.X, 1e-3, "left spectator 30 degrees left");
        Assert.Near(1.55, slots[1].Position.Y, 1e-4, "centre spectator straight ahead");
        Assert.True(slots[2].Position.X > 0.7f, "right is +x");
    }

    [Test]
    public static void WallAheadPullsTheCrowdCloser()
    {
        LayoutResult result;
        float arc;
        var walls = RoomFixtures.Room(-3f, 3f, -1f, 1.5f);
        var slots = CrowdLayout.Place(Eyes, Ahead, 3, walls, null, out result, out arc);
        Assert.Equal(LayoutResult.Tightened, result);
        Assert.Equal(3, slots.Count);
        foreach (CrowdSlot s in slots)
        {
            Assert.True(s.Position.Y <= 1.5f - 0.35f + 1e-3f, "keeps 35 cm off the wall: " + s.Position);
            Assert.True(s.Distance >= 1.0f, "never closer than 1 m");
        }
        Assert.True(slots[1].Constrained, "centre pulled in");
    }

    [Test]
    public static void NarrowRoomTightensTheArc()
    {
        LayoutResult result;
        float arc;
        var walls = RoomFixtures.Room(-0.75f, 0.75f, -1f, 4f);
        var slots = CrowdLayout.Place(Eyes, Ahead, 3, walls, null, out result, out arc);
        Assert.Equal(3, slots.Count, "everyone still fits");
        Assert.True(arc < 30f && arc >= 8f, "arc tightened to " + arc);
        foreach (CrowdSlot s in slots)
        {
            Assert.True(Math.Abs(s.Position.X) <= 0.75f - 0.35f + 1e-3f, "inside the side walls: " + s.Position);
        }
    }

    [Test]
    public static void NobodyStandsInTheSofa()
    {
        LayoutResult result;
        float arc;
        var sofa = new List<Footprint> { Footprint.Box(0.3f, 1.4f, 0.9f, 1.8f) };
        var slots = CrowdLayout.Place(Eyes, Ahead, 3, new List<Segment2>(), sofa, null, out result, out arc);
        Assert.True(slots.Count >= 2, "at least two spectators fit, got " + slots.Count);
        foreach (CrowdSlot s in slots)
        {
            Assert.True(sofa[0].Distance(s.Position) >= 0.35f - 1e-3f, "clear of the sofa: " + s.Position);
        }
    }

    [Test]
    public static void CoffeeTableInBetweenDoesNotHideTheFloorBeyond()
    {
        LayoutResult result;
        float arc;
        var coffee = new List<Footprint> { Footprint.Box(-0.8f, 0.8f, 0.5f, 0.9f) };
        var slots = CrowdLayout.Place(Eyes, Ahead, 3, new List<Segment2>(), coffee, null, out result, out arc);
        Assert.Equal(LayoutResult.Open, result, "spectators stand beyond it at the usual distance");
        Assert.Equal(3, slots.Count);
        var sofaAhead = new List<Footprint> { Footprint.Box(-0.3f, 0.3f, 1.3f, 2.0f) };
        Assert.True(CrowdLayout.FreeDistance(Eyes, Ahead, null, sofaAhead, null) <= 0.96f,
            "a sofa right where they would stand pushes them in front of it");
    }

    [Test]
    public static void TableAgainstAWallLeavesNoSpace()
    {
        LayoutResult result;
        float arc;
        var walls = RoomFixtures.Room(-3f, 3f, -1f, 0.9f);
        var slots = CrowdLayout.Place(Eyes, Ahead, 3, walls, null, out result, out arc);
        Assert.Equal(LayoutResult.NoSpace, result);
        Assert.Equal(0, slots.Count);
    }

    [Test]
    public static void SpacingAndRotation()
    {
        Vector2 right = CrowdLayout.Rotate(Ahead, 90f);
        Assert.Near(1.0, right.X, 1e-5, "+90 turns right");
        LayoutResult result;
        float arc;
        // Five people can't stand 55 cm apart inside ±30° at 1.55 m (neighbours would be ~40 cm apart):
        // one row holds three. A second row for crowds of 4–5 is future work.
        var slots = CrowdLayout.Place(Eyes, new Vector2(1f, 0f), 5, new List<Segment2>(), null, out result, out arc);
        Assert.Equal(LayoutResult.Reduced, result);
        Assert.Equal(3, slots.Count, "one row within the tracking arc");
        for (int i = 1; i < slots.Count; i++)
        {
            Assert.True(Vector2.Distance(slots[i].Position, slots[i - 1].Position) >= 0.55f - 1e-3f, "spacing");
        }
        Assert.True(slots[2].Position.X > 1.5f, "facing +x");
    }
}
