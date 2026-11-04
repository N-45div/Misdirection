using System;
using System.Collections.Generic;
using System.Numerics;

namespace Misdirection.Core.Table
{
    /// <summary>A table the room scan found: its top surface as a rectangle (4 corners in order, world space).</summary>
    public struct TableCandidate
    {
        public Vector3 C0;
        public Vector3 C1;
        public Vector3 C2;
        public Vector3 C3;
        /// <summary>Height of the top surface.</summary>
        public float TopY;
        /// <summary>Anything that identifies the source (MRUK anchor name), for logs.</summary>
        public string Id;

        public Vector3 Corner(int i)
        {
            switch (i & 3)
            {
                case 0: return C0;
                case 1: return C1;
                case 2: return C2;
                default: return C3;
            }
        }

        public Vector3 Center { get { return (C0 + C1 + C2 + C3) * 0.25f; } }

        /// <summary>Lengths of the two sides (C0→C1 and C1→C2), horizontal only.</summary>
        public float SideA { get { return Flat(C1 - C0).Length(); } }
        public float SideB { get { return Flat(C2 - C1).Length(); } }

        public static TableCandidate FromCorners(Vector3 c0, Vector3 c1, Vector3 c2, Vector3 c3, float topY, string id = null)
        {
            TableCandidate t = new TableCandidate();
            t.C0 = c0;
            t.C1 = c1;
            t.C2 = c2;
            t.C3 = c3;
            t.TopY = topY;
            t.Id = id;
            return t;
        }

        internal static Vector3 Flat(Vector3 v)
        {
            return new Vector3(v.X, 0f, v.Z);
        }
    }

    [Serializable]
    public sealed class TableSearchSettings
    {
        /// <summary>Both sides of the table top must be at least this long (metres).</summary>
        public float MinSide = 0.30f;
        /// <summary>The player must be within this horizontal distance of the table's nearest edge (metres).</summary>
        public float MaxEdgeDistance = 1.0f;
        /// <summary>A seated player's eyes sit 0.15–0.95 m above a usable table top.</summary>
        public float MinTopBelowEyes = 0.15f;
        public float MaxTopBelowEyes = 0.95f;
        /// <summary>Keep the frame origin this far from the edge's ends, so the mat fits (metres).</summary>
        public float MatHalfWidth = 0.22f;
    }

    public enum TableRejection { None, TooSmall, TooFar, WrongHeight }

    /// <summary>
    /// Turns room-scan tables into the game's <see cref="TableFrame"/>: picks the table the player
    /// is sitting at, then puts the frame on the edge closest to them (the near edge), facing across.
    /// Pure geometry, unit-tested; the Unity side feeds it MRUK TABLE anchors.
    /// </summary>
    public static class TableDetection
    {
        /// <summary>Index of the best table for a player whose eyes are at <paramref name="eyes"/>, or -1.</summary>
        public static int Choose(IList<TableCandidate> tables, Vector3 eyes, TableSearchSettings settings, List<TableRejection> reasons = null)
        {
            settings = settings ?? new TableSearchSettings();
            if (reasons != null)
            {
                reasons.Clear();
            }
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < tables.Count; i++)
            {
                TableCandidate t = tables[i];
                TableRejection why = TableRejection.None;
                float below = eyes.Y - t.TopY;
                float distance = EdgeDistance(t, eyes);
                if (Math.Min(t.SideA, t.SideB) < settings.MinSide)
                {
                    why = TableRejection.TooSmall;
                }
                else if (below < settings.MinTopBelowEyes || below > settings.MaxTopBelowEyes)
                {
                    why = TableRejection.WrongHeight;
                }
                else if (distance > settings.MaxEdgeDistance)
                {
                    why = TableRejection.TooFar;
                }
                if (reasons != null)
                {
                    reasons.Add(why);
                }
                if (why == TableRejection.None && distance < bestDistance)
                {
                    best = i;
                    bestDistance = distance;
                }
            }
            return best;
        }

        /// <summary>
        /// Frame on the edge nearest the player: origin on that edge (as close to the player as the mat allows),
        /// at the table-top height, forward pointing across the table.
        /// </summary>
        public static TableFrame FrameFor(TableCandidate t, Vector3 eyes, float matHalfWidth = 0.22f)
        {
            Vector3 player = TableCandidate.Flat(eyes);
            Vector3 center = TableCandidate.Flat(t.Center);
            int bestEdge = 0;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                float d = DistanceToSegment(player, TableCandidate.Flat(t.Corner(i)), TableCandidate.Flat(t.Corner(i + 1)));
                if (d < bestDistance - 1e-5f)
                {
                    bestDistance = d;
                    bestEdge = i;
                }
            }

            Vector3 a = TableCandidate.Flat(t.Corner(bestEdge));
            Vector3 b = TableCandidate.Flat(t.Corner(bestEdge + 1));
            Vector3 along = b - a;
            float length = along.Length();
            Vector3 dir = length > 1e-5f ? along / length : Vector3.UnitX;
            float s = Vector3.Dot(player - a, dir);
            float margin = Math.Min(matHalfWidth, length * 0.5f);
            s = Math.Max(margin, Math.Min(length - margin, s));
            Vector3 origin = a + dir * s;

            // Inward normal: perpendicular to the edge, pointing at the table's centre.
            Vector3 normal = new Vector3(-dir.Z, 0f, dir.X);
            if (Vector3.Dot(normal, center - origin) < 0f)
            {
                normal = -normal;
            }
            return new TableFrame(new Vector3(origin.X, t.TopY, origin.Z), normal);
        }

        /// <summary>Horizontal distance from the player to the table top (0 when above it).</summary>
        public static float EdgeDistance(TableCandidate t, Vector3 eyes)
        {
            Vector3 p = TableCandidate.Flat(eyes);
            if (Inside(t, p))
            {
                return 0f;
            }
            float best = float.MaxValue;
            for (int i = 0; i < 4; i++)
            {
                best = Math.Min(best, DistanceToSegment(p, TableCandidate.Flat(t.Corner(i)), TableCandidate.Flat(t.Corner(i + 1))));
            }
            return best;
        }

        private static bool Inside(TableCandidate t, Vector3 p)
        {
            // Convex quad: inside when p is on the same side of all four edges.
            int sign = 0;
            for (int i = 0; i < 4; i++)
            {
                Vector3 a = TableCandidate.Flat(t.Corner(i));
                Vector3 b = TableCandidate.Flat(t.Corner(i + 1));
                float cross = (b.X - a.X) * (p.Z - a.Z) - (b.Z - a.Z) * (p.X - a.X);
                int s = cross > 1e-6f ? 1 : (cross < -1e-6f ? -1 : 0);
                if (s == 0)
                {
                    continue;
                }
                if (sign == 0)
                {
                    sign = s;
                }
                else if (s != sign)
                {
                    return false;
                }
            }
            return true;
        }

        private static float DistanceToSegment(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float len2 = ab.LengthSquared();
            float t = len2 > 1e-10f ? Math.Max(0f, Math.Min(1f, Vector3.Dot(p - a, ab) / len2)) : 0f;
            return Vector3.Distance(p, a + ab * t);
        }
    }
}
