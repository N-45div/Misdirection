using System;
using System.Numerics;

namespace Misdirection.Core.Crowd
{
    public enum SuspicionLevel { Calm, Curious, Suspicious, WalkingOff }

    /// <summary>What is currently steering a spectator's attention (for debugging and the tutorial).</summary>
    public enum AttentionFocus { Hands, Gaze, Flourish, Sound, EyeContact, LookingAway }

    /// <summary>Everything a spectator can notice this frame. Built by <see cref="CrowdModel"/>.</summary>
    public struct Stimuli
    {
        public Vector3 MagicianEyes;
        /// <summary>Where the magician's look lands: a spectator's face, the table, or a point ahead.</summary>
        public Vector3 GazePoint;
        /// <summary>Index of the spectator whose face the magician is looking at, or -1.</summary>
        public int LookedAt;
        /// <summary>The magician's hands (the mat when no hand is tracked).</summary>
        public Vector3 HandsCenter;
        /// <summary>Centre of the close-up mat.</summary>
        public Vector3 Stage;
        public Vector3 FlourishPoint;
        /// <summary>0..1, decays after a flourish.</summary>
        public float FlourishSalience;
        public Vector3 SoundPoint;
        /// <summary>0..1, decays after a sound.</summary>
        public float SoundSalience;
    }

    [Serializable]
    public sealed class AttentionSettings
    {
        /// <summary>Eye contact must be held this long before it locks a spectator.</summary>
        public double ContactDwellSeconds = 0.3;
        /// <summary>A locked spectator keeps watching the magician's face this long after the magician looks away.</summary>
        public double LockHoldSeconds = 1.5;
        /// <summary>After staring too long, the spectator looks away (often at the hands) for this long.</summary>
        public double LookAwaySeconds = 2.5;
        /// <summary>Soft edge outside the cone where coverage falls from 1 to 0, as a fraction of the half-angle.</summary>
        public float ConeSoftEdge = 0.5f;
        /// <summary>Suspicion lost per second when nothing is seen.</summary>
        public float SuspicionDecayPerSecond = 0.015f;
        public float CuriousAt = 0.35f;
        public float SuspiciousAt = 0.7f;
        public float WalkOffAt = 1.0f;
        public float MaxSuspicion = 1.5f;
    }

    /// <summary>
    /// One spectator's attention: a point they look at that drifts toward a weighted blend of
    /// what the magician looks at, flourishes, sounds and the hands. Eye contact locks them onto
    /// the magician's face for a moment; staring too long makes them look away. Anything inside
    /// their cone is seen, and a seen method raises suspicion. Pure logic.
    /// </summary>
    public sealed class SpectatorMind
    {
        private readonly AttentionSettings settings;
        private double contactSeconds;
        private double lockUntil = double.NegativeInfinity;
        private double uncomfortableUntil = double.NegativeInfinity;
        private bool started;

        public SpectatorMind(int index, SpectatorProfile profile, Vector3 eyes, AttentionSettings settings = null)
        {
            Index = index;
            Profile = profile ?? new SpectatorProfile();
            Eyes = eyes;
            this.settings = settings ?? new AttentionSettings();
        }

        public int Index { get; private set; }
        public SpectatorProfile Profile { get; private set; }

        /// <summary>Eye position (also the face the magician can look at). Set by the scene.</summary>
        public Vector3 Eyes { get; set; }

        public Vector3 AttentionPoint { get; private set; }

        /// <summary>Where the attention cone points (the stage for a Filmer's phone).</summary>
        public Vector3 ConeTarget { get; private set; }

        public AttentionFocus Focus { get; private set; }
        public bool EyeContact { get; private set; }
        public bool LookingAway { get; private set; }
        public float Suspicion { get; private set; }

        public SuspicionLevel Level
        {
            get
            {
                if (Suspicion >= settings.WalkOffAt) return SuspicionLevel.WalkingOff;
                if (Suspicion >= settings.SuspiciousAt) return SuspicionLevel.Suspicious;
                if (Suspicion >= settings.CuriousAt) return SuspicionLevel.Curious;
                return SuspicionLevel.Calm;
            }
        }

        public void Update(double time, float dt, Stimuli s)
        {
            bool lookedAt = s.LookedAt == Index;
            LookingAway = time < uncomfortableUntil;
            if (lookedAt && !LookingAway)
            {
                contactSeconds += dt;
                if (contactSeconds >= settings.ContactDwellSeconds - 1e-9)
                {
                    lockUntil = time + settings.LockHoldSeconds;
                }
                if (contactSeconds >= Profile.StareTolerance)
                {
                    // Stared at too long: uncomfortable, looks away (often at the hands).
                    uncomfortableUntil = time + settings.LookAwaySeconds;
                    lockUntil = double.NegativeInfinity;
                    contactSeconds = 0;
                    LookingAway = true;
                }
            }
            else if (!lookedAt)
            {
                contactSeconds = 0;
            }
            EyeContact = !LookingAway && time < lockUntil;

            Vector3 target;
            if (LookingAway)
            {
                target = s.HandsCenter;
                Focus = AttentionFocus.LookingAway;
            }
            else if (EyeContact)
            {
                target = s.MagicianEyes;
                Focus = AttentionFocus.EyeContact;
            }
            else
            {
                target = Blend(s, lookedAt);
            }

            if (!started)
            {
                AttentionPoint = target;
                started = true;
            }
            else
            {
                float k = 1f - (float)Math.Exp(-Profile.TurnRate * Math.Max(0f, dt));
                AttentionPoint = Vector3.Lerp(AttentionPoint, target, k);
            }
            ConeTarget = Profile.FixedConeOnStage ? s.Stage : AttentionPoint;
            Suspicion = Math.Max(0f, Suspicion - settings.SuspicionDecayPerSecond * Math.Max(0f, dt));
        }

        /// <summary>Weighted blend of stimuli, sharpened (squared weights) so the strongest pull dominates.</summary>
        private Vector3 Blend(Stimuli s, bool lookedAt)
        {
            Vector3 sum = Vector3.Zero;
            float total = 0f;
            float best = -1f;
            Focus = AttentionFocus.Hands;

            Add(ref sum, ref total, ref best, AttentionFocus.Gaze, lookedAt ? s.MagicianEyes : s.GazePoint, Profile.GazeWeight);
            Add(ref sum, ref total, ref best, AttentionFocus.Hands, s.HandsCenter, Profile.HandsWeight);
            Add(ref sum, ref total, ref best, AttentionFocus.Flourish, s.FlourishPoint, Profile.FlourishWeight * s.FlourishSalience);
            Add(ref sum, ref total, ref best, AttentionFocus.Sound, s.SoundPoint, Profile.SoundWeight * s.SoundSalience);
            return total > 1e-6f ? sum / total : s.HandsCenter;
        }

        private void Add(ref Vector3 sum, ref float total, ref float best, AttentionFocus focus, Vector3 point, float weight)
        {
            if (weight <= 0f)
            {
                return;
            }
            float w = weight * weight;
            sum += point * w;
            total += w;
            if (w > best)
            {
                best = w;
                Focus = focus;
            }
        }

        /// <summary>How much this spectator sees a point: 1 inside the cone, fading to 0 across the soft edge.</summary>
        public float Coverage(Vector3 point)
        {
            Vector3 axis = ConeTarget - Eyes;
            Vector3 to = point - Eyes;
            if (axis.LengthSquared() < 1e-8f || to.LengthSquared() < 1e-8f)
            {
                return 0f;
            }
            float cos = Vector3.Dot(Vector3.Normalize(axis), Vector3.Normalize(to));
            float angle = (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, cos))) * 180.0 / Math.PI);
            float half = Profile.ConeHalfAngle;
            float soft = Math.Max(0.01f, half * settings.ConeSoftEdge);
            if (angle <= half)
            {
                return 1f;
            }
            if (angle >= half + soft)
            {
                return 0f;
            }
            return 1f - (angle - half) / soft;
        }

        /// <summary>A secret move happened at <paramref name="point"/>. Returns how well it was seen (0..1).</summary>
        public float Witness(Vector3 point, float severity)
        {
            float seen = Coverage(point);
            AddSuspicion(seen * severity * Profile.SuspicionGain);
            return seen;
        }

        /// <summary>The secret hand is carrying something at <paramref name="point"/> for <paramref name="dt"/> seconds.</summary>
        public void Expose(Vector3 point, float dt, float ratePerSecond)
        {
            AddSuspicion(Coverage(point) * ratePerSecond * dt * Profile.SuspicionGain);
        }

        public void AddSuspicion(float amount)
        {
            Suspicion = Math.Max(0f, Math.Min(settings.MaxSuspicion, Suspicion + amount));
        }

        public void ResetSuspicion()
        {
            Suspicion = 0f;
        }
    }
}
