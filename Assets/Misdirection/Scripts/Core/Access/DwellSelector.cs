using System;

namespace Misdirection.Core.Access
{
    /// <summary>
    /// Choosing by dwelling: look at (or rest a finger on) one of several targets for long enough and
    /// it is chosen. Built for eyes-only and head-only use, so it reads a held state and forgives
    /// jitter: a brief slip off the target doesn't restart the dwell, a long frame can't finish one,
    /// and after a choice the target must be left before it can be chosen again. Pure logic.
    /// </summary>
    public sealed class DwellSelector
    {
        private readonly double dwellSeconds;
        private readonly double graceSeconds;
        private bool started;
        private double lastTime;
        private double held;
        private double offFor;
        private int latched = -1;

        /// <param name="dwellSeconds">How long a target must be held to choose it.</param>
        /// <param name="graceSeconds">Slips off the target shorter than this don't restart the dwell.</param>
        public DwellSelector(double dwellSeconds = 1.5, double graceSeconds = 0.25)
        {
            this.dwellSeconds = Math.Max(0.01, dwellSeconds);
            this.graceSeconds = Math.Max(0.0, graceSeconds);
            Target = -1;
        }

        /// <summary>The target being dwelt on, or -1.</summary>
        public int Target { get; private set; }

        /// <summary>0..1 fill of the dwell on <see cref="Target"/> (for the fill ring).</summary>
        public float Progress { get; private set; }

        /// <summary>
        /// Advance one frame. <paramref name="target"/> is what is being looked at or touched now (-1 for nothing).
        /// <paramref name="rate"/> speeds the dwell up (a pinch on a token counts faster than a resting finger).
        /// Returns the chosen target on the frame the dwell completes, else -1.
        /// </summary>
        public int Update(double time, int target, bool enabled, double rate = 1.0)
        {
            // A long frame counts as a short one: a hitch must never complete a dwell.
            double dt = started ? Math.Min(0.1, Math.Max(0.0, time - lastTime)) : 0.0;
            started = true;
            lastTime = time;

            if (!enabled)
            {
                Reset();
                return -1;
            }
            if (target < 0)
            {
                offFor += dt;
                if (offFor > graceSeconds)
                {
                    // Properly looked away: forget the dwell and allow the same target again.
                    Reset();
                }
                return -1;
            }
            if (target != Target)
            {
                Target = target;
                held = 0;
                if (target != latched)
                {
                    latched = -1;
                }
            }
            offFor = 0;
            if (target == latched)
            {
                // Just chosen: stay quiet until the player looks away.
                Progress = 0f;
                return -1;
            }
            held += dt * Math.Max(0.0, rate);
            Progress = (float)Math.Min(1.0, held / dwellSeconds);
            if (held >= dwellSeconds - 1e-9)
            {
                latched = target;
                held = 0;
                Progress = 0f;
                return target;
            }
            return -1;
        }

        public void Reset()
        {
            Target = -1;
            held = 0;
            offFor = 0;
            latched = -1;
            Progress = 0f;
        }
    }
}
