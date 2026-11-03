using System;
using System.Collections.Generic;
using System.Numerics;

namespace Misdirection.Core.Hands
{
    /// <summary>Hand geometry helpers in Unity's world convention (x right, y up, z forward).</summary>
    public static class HandGeometry
    {
        /// <summary>
        /// Unit normal pointing out of the palm, from the wrist and the index and ring knuckles.
        /// Zero when the points are degenerate.
        /// </summary>
        public static Vector3 PalmNormal(Vector3 wrist, Vector3 indexKnuckle, Vector3 ringKnuckle, bool rightHand)
        {
            Vector3 a = indexKnuckle - wrist;
            Vector3 b = ringKnuckle - wrist;
            Vector3 n = rightHand ? Vector3.Cross(b, a) : Vector3.Cross(a, b);
            return n.LengthSquared() > 1e-12f ? Vector3.Normalize(n) : Vector3.Zero;
        }
    }

    [Serializable]
    public sealed class FlourishSettings
    {
        public float MinOpenness = 0.75f;
        /// <summary>Palm must face away from the player at least this much (cosine), unless it faces up.</summary>
        public float MinOutward = 0.3f;
        /// <summary>A palm facing up this much (normal.y) also counts: the "presenting" gesture.</summary>
        public float MinUpward = 0.5f;
        /// <summary>
        /// Above this (cosine toward the player's eyes) the palm faces the player: never a flourish, since that is
        /// Meta's system-gesture pose. 0.5 = within 60° of straight at the face. A palm-up "presenting" hand below
        /// eye level sits around 0.3 and is allowed.
        /// </summary>
        public float MaxTowardFace = 0.5f;
        /// <summary>The hand must be raised at least this far above the table (metres).</summary>
        public float MinRaise = 0.08f;
        /// <summary>The hand must have swept at least this far just before the hold (metres).</summary>
        public float SweepDistance = 0.12f;
        public double SweepWindowSeconds = 1.0;
        /// <summary>Held still in the pose this long ("held for a beat").</summary>
        public double HoldSeconds = 0.3;
        public float MaxHoldSpeed = 0.3f;
        public double CooldownSeconds = 1.0;
    }

    /// <summary>
    /// The show hand's flourish: a sweep that ends in an open palm, raised and turned away from
    /// the player (or up), held for a beat. Fires once per gesture. Never fires for a palm facing
    /// the player, which is the system-menu pose.
    /// </summary>
    public sealed class FlourishDetector
    {
        private struct Sample
        {
            public double Time;
            public Vector3 Position;
        }

        private readonly FlourishSettings settings;
        private readonly List<Sample> history = new List<Sample>();
        private double holdSince = -1;
        private double lastFire = double.NegativeInfinity;
        private bool armed = true;

        public FlourishDetector(FlourishSettings settings = null)
        {
            this.settings = settings ?? new FlourishSettings();
        }

        public Vector3 LastPoint { get; private set; }
        public Vector3 LastDirection { get; private set; }

        /// <summary>True while the hand is in the flourish pose (for visual feedback).</summary>
        public bool Posed { get; private set; }

        /// <summary>Feed every frame. Returns true on the frame a flourish completes.</summary>
        public bool Update(double time, bool reliable, Vector3 palm, Vector3 palmNormal, float openness, Vector3 eyes, float tableHeight)
        {
            float speed = 0f;
            if (history.Count > 0)
            {
                Sample last = history[history.Count - 1];
                double dt = time - last.Time;
                if (dt > 1e-6)
                {
                    speed = (float)(Vector3.Distance(palm, last.Position) / dt);
                }
            }
            history.Add(new Sample { Time = time, Position = palm });
            double keep = settings.SweepWindowSeconds + settings.HoldSeconds + 0.5;
            while (history.Count > 0 && time - history[0].Time > keep)
            {
                history.RemoveAt(0);
            }

            Posed = reliable && InPose(palm, palmNormal, openness, eyes, tableHeight);
            if (!Posed || speed > settings.MaxHoldSpeed)
            {
                holdSince = -1;
                if (!Posed)
                {
                    armed = true;
                }
                return false;
            }
            if (holdSince < 0)
            {
                holdSince = time;
            }
            if (!armed || time - holdSince < settings.HoldSeconds - 1e-9 || time - lastFire < settings.CooldownSeconds)
            {
                return false;
            }
            if (!SweptBefore(holdSince))
            {
                return false;
            }
            armed = false;
            lastFire = time;
            LastPoint = palm;
            LastDirection = palmNormal;
            return true;
        }

        private bool InPose(Vector3 palm, Vector3 normal, float openness, Vector3 eyes, float tableHeight)
        {
            if (float.IsNaN(openness) || openness < settings.MinOpenness || normal.LengthSquared() < 0.5f)
            {
                return false;
            }
            if (palm.Y - tableHeight < settings.MinRaise)
            {
                return false;
            }
            Vector3 toEyes = eyes - palm;
            if (toEyes.LengthSquared() > 1e-8f && Vector3.Dot(normal, Vector3.Normalize(toEyes)) > settings.MaxTowardFace)
            {
                return false;
            }
            Vector3 away = palm - eyes;
            away.Y = 0f;
            float outward = away.LengthSquared() > 1e-8f ? Vector3.Dot(normal, Vector3.Normalize(away)) : 0f;
            return outward >= settings.MinOutward || normal.Y >= settings.MinUpward;
        }

        private bool SweptBefore(double holdStart)
        {
            Vector3 end = Vector3.Zero;
            bool found = false;
            for (int i = history.Count - 1; i >= 0; i--)
            {
                if (history[i].Time <= holdStart + 1e-9)
                {
                    end = history[i].Position;
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                return false;
            }
            foreach (Sample s in history)
            {
                if (s.Time < holdStart - settings.SweepWindowSeconds || s.Time > holdStart)
                {
                    continue;
                }
                if (Vector3.Distance(s.Position, end) >= settings.SweepDistance)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
