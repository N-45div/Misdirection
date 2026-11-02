using System;

namespace Misdirection.Core.Hands
{
    public enum GripState { Open, Holding }

    public enum GripEvent { None, Grabbed, Released }

    /// <summary>One frame of hand shape, measured by the Unity adapter.</summary>
    public struct HandShape
    {
        /// <summary>Index pinch strength from the SDK, 0..1.</summary>
        public float PinchStrength;
        /// <summary>Thumb tip to index tip, metres. NaN when unknown.</summary>
        public float ThumbIndexDistance;
        /// <summary>0 = fist, 1 = fingers fully extended. NaN when unknown.</summary>
        public float Openness;

        public static HandShape Pinch(float strength, float thumbIndexDistance = float.NaN, float openness = float.NaN)
        {
            HandShape s = new HandShape();
            s.PinchStrength = strength;
            s.ThumbIndexDistance = thumbIndexDistance;
            s.Openness = openness;
            return s;
        }
    }

    [Serializable]
    public sealed class PinchGripSettings
    {
        /// <summary>Pinch strength needed to grab.</summary>
        public float GrabStrength = 0.8f;
        /// <summary>The pinch must hold this long before it counts (reads a settled state, not an instant).</summary>
        public double GrabHoldSeconds = 0.06;
        /// <summary>Below this pinch strength the fingers count as apart.</summary>
        public float ReleaseStrength = 0.35f;
        /// <summary>Fingers count as apart when thumb–index distance exceeds the grab-time distance by this much (metres).</summary>
        public float ReleaseMargin = 0.02f;
        /// <summary>The hand counts as open (not a fist) at or above this openness.</summary>
        public float OpenThreshold = 0.65f;
        /// <summary>The release must hold this long before it counts.</summary>
        public double ReleaseHoldSeconds = 0.08;
    }

    /// <summary>
    /// Sticky pinch-to-hold for the secret hand, following what shipped hand-tracking games do
    /// (Cubism, Hand Physics Lab): the release margin is set from the thumb–index distance at grab
    /// time, no release is ever decided on unreliable tracking, and a closed fist (palming) keeps
    /// holding. Releasing needs fingers apart AND an open hand, held for a moment.
    /// Bias: it is better to miss a release than to drop the coin by accident.
    /// </summary>
    public sealed class PinchGrip
    {
        private readonly PinchGripSettings settings;
        private double grabCandidateSince = -1;
        private double releaseCandidateSince = -1;

        public PinchGrip(PinchGripSettings settings = null)
        {
            this.settings = settings ?? new PinchGripSettings();
            ForceRelease();
        }

        public GripState State { get; private set; }

        /// <summary>Thumb–index distance captured at grab (NaN if unknown).</summary>
        public float GripDistance { get; private set; }

        /// <summary>Distance beyond which the fingers count as apart for this grip (NaN if unknown).</summary>
        public float ReleaseDistance { get; private set; }

        /// <summary>
        /// Advance one frame. <paramref name="reliable"/> comes from <see cref="HandPresence.ReliableForDecisions"/>;
        /// <paramref name="canGrab"/> means something grabbable is at the pinch point.
        /// </summary>
        public GripEvent Update(double time, bool reliable, HandShape shape, bool canGrab)
        {
            if (State == GripState.Open)
            {
                releaseCandidateSince = -1;
                bool pinching = reliable && canGrab && shape.PinchStrength >= settings.GrabStrength;
                if (!pinching)
                {
                    grabCandidateSince = -1;
                    return GripEvent.None;
                }
                if (grabCandidateSince < 0)
                {
                    grabCandidateSince = time;
                }
                if (time - grabCandidateSince < settings.GrabHoldSeconds - 1e-9)
                {
                    return GripEvent.None;
                }
                State = GripState.Holding;
                grabCandidateSince = -1;
                GripDistance = shape.ThumbIndexDistance;
                ReleaseDistance = float.IsNaN(GripDistance) ? float.NaN : GripDistance + settings.ReleaseMargin;
                return GripEvent.Grabbed;
            }

            // Holding: sticky. Unreliable data never releases and resets any pending release.
            if (!reliable || !LooksReleased(shape))
            {
                releaseCandidateSince = -1;
                return GripEvent.None;
            }
            if (releaseCandidateSince < 0)
            {
                releaseCandidateSince = time;
            }
            if (time - releaseCandidateSince < settings.ReleaseHoldSeconds - 1e-9)
            {
                return GripEvent.None;
            }
            ForceRelease();
            return GripEvent.Released;
        }

        /// <summary>Drop whatever is held without an event (e.g. the trick resets).</summary>
        public void ForceRelease()
        {
            State = GripState.Open;
            releaseCandidateSince = -1;
            grabCandidateSince = -1;
            GripDistance = float.NaN;
            ReleaseDistance = float.NaN;
        }

        private bool LooksReleased(HandShape shape)
        {
            bool fingersApart = shape.PinchStrength < settings.ReleaseStrength
                || (!float.IsNaN(ReleaseDistance) && !float.IsNaN(shape.ThumbIndexDistance) && shape.ThumbIndexDistance > ReleaseDistance);
            if (!fingersApart)
            {
                return false;
            }
            // A fist (palming) has the fingers apart too; only an open hand releases.
            return float.IsNaN(shape.Openness) || shape.Openness >= settings.OpenThreshold;
        }
    }

    /// <summary>Hand openness from finger geometry: how far each fingertip reaches compared with its knuckle.</summary>
    public static class HandOpenness
    {
        /// <summary>Tip-to-wrist / knuckle-to-wrist ratio for a curled finger.</summary>
        public const float CurledRatio = 1.0f;
        /// <summary>Ratio for a straight finger (wrist to knuckle about 9 cm, knuckle to tip about 8 cm).</summary>
        public const float ExtendedRatio = 1.75f;

        /// <summary>Openness 0..1 for one finger from its two distances to the wrist. NaN for bad input.</summary>
        public static float Finger(float tipToWrist, float knuckleToWrist)
        {
            if (!(knuckleToWrist > 1e-4f) || float.IsNaN(tipToWrist))
            {
                return float.NaN;
            }
            float ratio = tipToWrist / knuckleToWrist;
            float t = (ratio - CurledRatio) / (ExtendedRatio - CurledRatio);
            return Math.Min(1f, Math.Max(0f, t));
        }

        /// <summary>Mean of the valid finger values, NaN when none are valid.</summary>
        public static float Combine(float a, float b, float c)
        {
            float sum = 0f;
            int n = 0;
            if (!float.IsNaN(a)) { sum += a; n++; }
            if (!float.IsNaN(b)) { sum += b; n++; }
            if (!float.IsNaN(c)) { sum += c; n++; }
            return n == 0 ? float.NaN : sum / n;
        }
    }
}
