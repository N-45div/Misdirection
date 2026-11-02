using System;
using System.Numerics;

namespace Misdirection.Core.Table
{
    /// <summary>
    /// The player's real table as a coordinate frame. Origin: middle of the near edge, on the
    /// table surface. +Z points away from the player across the table, +Y up, +X to the player's right.
    /// Day 2 calibrates it from two palms on the table; Day 5 replaces that with MRUK table detection.
    /// </summary>
    public struct TableFrame
    {
        public static readonly Vector3 Up = Vector3.UnitY;

        public Vector3 Origin;
        public Vector3 Forward;

        public TableFrame(Vector3 origin, Vector3 forward)
        {
            Origin = origin;
            Forward = Horizontal(forward, Vector3.UnitZ);
        }

        public Vector3 Right { get { return Vector3.Cross(Up, Forward); } }

        public bool IsValid { get { return Forward.LengthSquared() > 0.5f; } }

        public Vector3 ToLocal(Vector3 world)
        {
            Vector3 d = world - Origin;
            return new Vector3(Vector3.Dot(d, Right), d.Y, Vector3.Dot(d, Forward));
        }

        public Vector3 ToWorld(Vector3 local)
        {
            return Origin + Right * local.X + Up * local.Y + Forward * local.Z;
        }

        /// <summary>
        /// Calibrate from both palms resting flat on the table with the heels of the hands at the near edge.
        /// Wrist joints sit a little above the surface, so the surface is <paramref name="wristHeight"/> below them.
        /// </summary>
        public static TableFrame FromPalms(Vector3 leftWrist, Vector3 rightWrist, Vector3 head, float wristHeight = 0.025f)
        {
            Vector3 mid = (leftWrist + rightWrist) * 0.5f;
            Vector3 across = Flatten(rightWrist - leftWrist);
            Vector3 forward = across.LengthSquared() > 1e-6f ? Vector3.Cross(across, Up) : Flatten(mid - head);
            forward = Horizontal(forward, Flatten(mid - head));
            // Point away from the player even if the hands were crossed.
            if (Vector3.Dot(forward, Flatten(mid - head)) < 0f)
            {
                forward = -forward;
            }
            float surface = Math.Min(leftWrist.Y, rightWrist.Y) - wristHeight;
            return new TableFrame(new Vector3(mid.X, surface, mid.Z), forward);
        }

        private static Vector3 Flatten(Vector3 v)
        {
            return new Vector3(v.X, 0f, v.Z);
        }

        private static Vector3 Horizontal(Vector3 v, Vector3 fallback)
        {
            Vector3 flat = Flatten(v);
            if (flat.LengthSquared() < 1e-8f)
            {
                flat = Flatten(fallback);
            }
            if (flat.LengthSquared() < 1e-8f)
            {
                flat = Vector3.UnitZ;
            }
            return Vector3.Normalize(flat);
        }
    }

    [Serializable]
    public sealed class ServanteSettings
    {
        /// <summary>Half the width of the zone along the near edge (metres).</summary>
        public float HalfWidth = 0.30f;
        /// <summary>How far toward the player, past the edge, the zone reaches (negative Z).</summary>
        public float NearZ = -0.14f;
        /// <summary>How far onto the table the zone reaches (positive Z).</summary>
        public float FarZ = 0.05f;
        public float MinY = -0.20f;
        public float MaxY = 0.08f;
        /// <summary>Once inside, the hand must leave by this much to count as outside (tolerates ~2 cm of tracking slop).</summary>
        public float ExitMargin = 0.02f;
    }

    /// <summary>
    /// The servante: a hidden drop zone along the near table edge, borrowed from stage magic.
    /// A coin released inside it is ditched. Box in table-local coordinates with hysteresis.
    /// </summary>
    public sealed class ServanteZone
    {
        public ServanteZone(ServanteSettings settings = null)
        {
            Settings = settings ?? new ServanteSettings();
        }

        public ServanteSettings Settings { get; private set; }

        public bool Inside { get; private set; }

        /// <summary>Update with the hand's position in table-local coordinates. Returns <see cref="Inside"/>.</summary>
        public bool Update(Vector3 local)
        {
            Inside = Inside ? Distance(local) <= Settings.ExitMargin : Distance(local) <= 0f;
            return Inside;
        }

        public void Reset()
        {
            Inside = false;
        }

        /// <summary>Distance from the box (0 inside), metres.</summary>
        public float Distance(Vector3 local)
        {
            float dx = Outside(local.X, -Settings.HalfWidth, Settings.HalfWidth);
            float dy = Outside(local.Y, Settings.MinY, Settings.MaxY);
            float dz = Outside(local.Z, Settings.NearZ, Settings.FarZ);
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        /// <summary>Centre of the box in table-local coordinates (for visuals).</summary>
        public Vector3 Center
        {
            get { return new Vector3(0f, (Settings.MinY + Settings.MaxY) * 0.5f, (Settings.NearZ + Settings.FarZ) * 0.5f); }
        }

        public Vector3 Size
        {
            get { return new Vector3(Settings.HalfWidth * 2f, Settings.MaxY - Settings.MinY, Settings.FarZ - Settings.NearZ); }
        }

        private static float Outside(float v, float min, float max)
        {
            if (v < min)
            {
                return min - v;
            }
            if (v > max)
            {
                return v - max;
            }
            return 0f;
        }
    }
}
