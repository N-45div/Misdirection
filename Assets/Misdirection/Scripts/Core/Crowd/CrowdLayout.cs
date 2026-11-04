using System;
using System.Collections.Generic;
using System.Numerics;

namespace Misdirection.Core.Crowd
{
    /// <summary>A wall or furniture edge on the floor plan (x, z in metres).</summary>
    public struct Segment2
    {
        public Vector2 A;
        public Vector2 B;

        public Segment2(Vector2 a, Vector2 b)
        {
            A = a;
            B = b;
        }

        public Segment2(float ax, float az, float bx, float bz)
        {
            A = new Vector2(ax, az);
            B = new Vector2(bx, bz);
        }
    }

    /// <summary>A piece of furniture's footprint on the floor plan (a convex quad, corners in order).</summary>
    public struct Footprint
    {
        public Vector2 C0;
        public Vector2 C1;
        public Vector2 C2;
        public Vector2 C3;

        public Footprint(Vector2 c0, Vector2 c1, Vector2 c2, Vector2 c3)
        {
            C0 = c0;
            C1 = c1;
            C2 = c2;
            C3 = c3;
        }

        public static Footprint Box(float x0, float x1, float z0, float z1)
        {
            return new Footprint(new Vector2(x0, z0), new Vector2(x1, z0), new Vector2(x1, z1), new Vector2(x0, z1));
        }

        public Vector2 Corner(int i)
        {
            switch (i & 3)
            {
                case 0: return C0;
                case 1: return C1;
                case 2: return C2;
                default: return C3;
            }
        }

        public bool Contains(Vector2 p)
        {
            int sign = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = Corner(i);
                Vector2 b = Corner(i + 1);
                float cross = (b.X - a.X) * (p.Y - a.Y) - (b.Y - a.Y) * (p.X - a.X);
                int side = cross > 1e-6f ? 1 : (cross < -1e-6f ? -1 : 0);
                if (side == 0)
                {
                    continue;
                }
                if (sign == 0)
                {
                    sign = side;
                }
                else if (side != sign)
                {
                    return false;
                }
            }
            return true;
        }

        public float Distance(Vector2 p)
        {
            if (Contains(p))
            {
                return 0f;
            }
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                best = Math.Min(best, CrowdLayout.SegmentDistance(p, Corner(i), Corner(i + 1)));
            }
            return best;
        }
    }

    [Serializable]
    public sealed class CrowdLayoutSettings
    {
        /// <summary>Ideal distance from the player's eyes to a spectator (metres, horizontal).</summary>
        public float PreferredDistance = 1.55f;
        /// <summary>Closer than this, a spectator would crowd the table.</summary>
        public float MinDistance = 1.0f;
        /// <summary>Widest arc either side of straight ahead. Set it from the Day 1 tracking spike.</summary>
        public float MaxArcHalfDegrees = 30f;
        /// <summary>Narrowest arc worth trying before giving up on the even spread.</summary>
        public float MinArcHalfDegrees = 8f;
        public float ArcStepDegrees = 2f;
        /// <summary>Gap kept between a spectator's centre and any wall or furniture (metres).</summary>
        public float WallClearance = 0.35f;
        /// <summary>Spectators stand at least this far apart (metres).</summary>
        public float MinSpacing = 0.55f;
    }

    public struct CrowdSlot
    {
        public Vector2 Position;
        public float AngleDegrees;
        public float Distance;
        /// <summary>True when a wall pulled this spectator closer than the preferred distance.</summary>
        public bool Constrained;
    }

    public enum LayoutResult
    {
        /// <summary>Everyone fits at the preferred arc and distance.</summary>
        Open,
        /// <summary>Everyone fits, but walls pulled some closer or narrowed the arc.</summary>
        Tightened,
        /// <summary>Not everyone fits; fewer spectators placed.</summary>
        Reduced,
        /// <summary>No space at all (e.g. the table is against a wall). The caller decides the fallback.</summary>
        NoSpace,
    }

    /// <summary>
    /// Places the audience on the open floor across the table (docs/DESIGN.md: "spectators fill open
    /// floor across the table, and the arc tightens near walls"). Works on a 2-D floor plan from the
    /// room scan: walls end a spectator's line (nobody stands behind a wall), furniture only marks
    /// spots nobody can stand on (a coffee table in between doesn't hide the floor beyond it).
    /// Pure geometry, unit-tested.
    /// </summary>
    public static class CrowdLayout
    {
        public static List<CrowdSlot> Place(Vector2 eyes, Vector2 forward, int count, IList<Segment2> walls,
                                            CrowdLayoutSettings settings, out LayoutResult result, out float arcHalfDegrees)
        {
            return Place(eyes, forward, count, walls, null, settings, out result, out arcHalfDegrees);
        }

        public static List<CrowdSlot> Place(Vector2 eyes, Vector2 forward, int count, IList<Segment2> walls, IList<Footprint> furniture,
                                            CrowdLayoutSettings settings, out LayoutResult result, out float arcHalfDegrees)
        {
            Room room = new Room(walls, furniture);
            settings = settings ?? new CrowdLayoutSettings();
            forward = forward.LengthSquared() > 1e-8f ? Vector2.Normalize(forward) : new Vector2(0f, 1f);
            List<CrowdSlot> slots = new List<CrowdSlot>();
            arcHalfDegrees = 0f;
            result = LayoutResult.NoSpace;
            if (count <= 0)
            {
                result = LayoutResult.Open;
                return slots;
            }

            // 1. Even spread, narrowing the arc until everyone fits.
            for (float arc = settings.MaxArcHalfDegrees; arc >= settings.MinArcHalfDegrees - 1e-3f; arc -= settings.ArcStepDegrees)
            {
                if (TryEven(eyes, forward, count, arc, room, settings, slots))
                {
                    arcHalfDegrees = count == 1 ? 0f : arc;
                    bool tight = arc < settings.MaxArcHalfDegrees - 1e-3f;
                    foreach (CrowdSlot s in slots)
                    {
                        tight |= s.Constrained;
                    }
                    result = tight ? LayoutResult.Tightened : LayoutResult.Open;
                    return slots;
                }
                if (count == 1)
                {
                    break;
                }
            }

            // 2. Greedy: take the free directions closest to the even targets, as many as fit.
            slots.Clear();
            List<float> free = new List<float>();
            for (float a = -settings.MaxArcHalfDegrees; a <= settings.MaxArcHalfDegrees + 1e-3f; a += settings.ArcStepDegrees * 0.5f)
            {
                if (FreeDistance(eyes, Rotate(forward, a), room, settings) >= settings.MinDistance)
                {
                    free.Add(a);
                }
            }
            for (int i = 0; i < count; i++)
            {
                float target = count == 1 ? 0f : -settings.MaxArcHalfDegrees + 2f * settings.MaxArcHalfDegrees * i / (count - 1);
                CrowdSlot? best = null;
                float bestError = float.MaxValue;
                foreach (float a in free)
                {
                    CrowdSlot candidate = SlotAt(eyes, forward, a, room, settings);
                    if (!FarEnough(candidate, slots, settings.MinSpacing))
                    {
                        continue;
                    }
                    float error = Math.Abs(a - target);
                    if (error < bestError)
                    {
                        bestError = error;
                        best = candidate;
                    }
                }
                if (best.HasValue)
                {
                    slots.Add(best.Value);
                }
            }
            slots.Sort((x, y) => x.AngleDegrees.CompareTo(y.AngleDegrees));
            foreach (CrowdSlot s in slots)
            {
                arcHalfDegrees = Math.Max(arcHalfDegrees, Math.Abs(s.AngleDegrees));
            }
            result = slots.Count == 0 ? LayoutResult.NoSpace : LayoutResult.Reduced;
            return slots;
        }

        private struct Room
        {
            public readonly IList<Segment2> Walls;
            public readonly IList<Footprint> Furniture;

            public Room(IList<Segment2> walls, IList<Footprint> furniture)
            {
                Walls = walls;
                Furniture = furniture;
            }

            /// <summary>Distance from p to the nearest wall or furniture (0 inside furniture).</summary>
            public float Clearance(Vector2 p)
            {
                float best = float.MaxValue;
                if (Walls != null)
                {
                    foreach (Segment2 s in Walls)
                    {
                        best = Math.Min(best, SegmentDistance(p, s.A, s.B));
                    }
                }
                if (Furniture != null)
                {
                    foreach (Footprint f in Furniture)
                    {
                        best = Math.Min(best, f.Distance(p));
                    }
                }
                return best;
            }
        }

        /// <summary>How far a spectator can stand along <paramref name="dir"/> (capped at the preferred distance): before the first wall, and clear of walls and furniture.</summary>
        public static float FreeDistance(Vector2 eyes, Vector2 dir, IList<Segment2> walls, IList<Footprint> furniture, CrowdLayoutSettings settings)
        {
            return FreeDistance(eyes, dir, new Room(walls, furniture), settings ?? new CrowdLayoutSettings());
        }

        private static float FreeDistance(Vector2 eyes, Vector2 dir, Room room, CrowdLayoutSettings settings)
        {
            float nearest = float.MaxValue;
            if (room.Walls != null)
            {
                foreach (Segment2 s in room.Walls)
                {
                    float t = RaySegment(eyes, dir, s.A, s.B);
                    if (t >= 0f && t < nearest)
                    {
                        nearest = t;
                    }
                }
            }
            float free = nearest == float.MaxValue ? float.MaxValue : nearest - settings.WallClearance;
            float d = Math.Min(settings.PreferredDistance, free);
            // A spectator also needs clearance all round: step toward the player until the spot is clear.
            while (d > 0f && room.Clearance(eyes + dir * d) < settings.WallClearance - 1e-4f)
            {
                d -= 0.05f;
            }
            return d;
        }

        private static bool TryEven(Vector2 eyes, Vector2 forward, int count, float arc, Room room,
                                    CrowdLayoutSettings settings, List<CrowdSlot> slots)
        {
            slots.Clear();
            for (int i = 0; i < count; i++)
            {
                float a = count == 1 ? 0f : -arc + 2f * arc * i / (count - 1);
                CrowdSlot slot = SlotAt(eyes, forward, a, room, settings);
                if (slot.Distance < settings.MinDistance - 1e-4f || !FarEnough(slot, slots, settings.MinSpacing))
                {
                    slots.Clear();
                    return false;
                }
                slots.Add(slot);
            }
            return true;
        }

        private static CrowdSlot SlotAt(Vector2 eyes, Vector2 forward, float angle, Room room, CrowdLayoutSettings settings)
        {
            Vector2 dir = Rotate(forward, angle);
            float d = FreeDistance(eyes, dir, room, settings);
            CrowdSlot slot = new CrowdSlot();
            slot.AngleDegrees = angle;
            slot.Distance = d;
            slot.Position = eyes + dir * d;
            slot.Constrained = d < settings.PreferredDistance - 1e-4f;
            return slot;
        }

        private static bool FarEnough(CrowdSlot candidate, List<CrowdSlot> placed, float spacing)
        {
            foreach (CrowdSlot s in placed)
            {
                if (Vector2.Distance(s.Position, candidate.Position) < spacing - 1e-4f)
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>Positive angles turn right (clockwise seen from above, x right and z forward).</summary>
        public static Vector2 Rotate(Vector2 v, float degrees)
        {
            double r = -degrees * Math.PI / 180.0;
            float c = (float)Math.Cos(r);
            float s = (float)Math.Sin(r);
            return new Vector2(v.X * c - v.Y * s, v.X * s + v.Y * c);
        }

        /// <summary>Distance along the ray to the segment, or -1 when it misses.</summary>
        private static float RaySegment(Vector2 origin, Vector2 dir, Vector2 a, Vector2 b)
        {
            Vector2 e = b - a;
            float denom = Cross(dir, e);
            if (Math.Abs(denom) < 1e-8f)
            {
                return -1f;
            }
            Vector2 diff = a - origin;
            float t = Cross(diff, e) / denom;
            float u = Cross(diff, dir) / denom;
            return t >= 0f && u >= -1e-5f && u <= 1f + 1e-5f ? t : -1f;
        }

        public static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float len2 = ab.LengthSquared();
            float t = len2 > 1e-10f ? Math.Max(0f, Math.Min(1f, Vector2.Dot(p - a, ab) / len2)) : 0f;
            return Vector2.Distance(p, a + ab * t);
        }

        private static float Cross(Vector2 a, Vector2 b)
        {
            return a.X * b.Y - a.Y * b.X;
        }
    }
}
