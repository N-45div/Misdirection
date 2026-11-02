using System;

namespace Misdirection.Core.Hands
{
    public enum HandPresenceState
    {
        /// <summary>Tracked with high confidence. Live pose, decisions allowed once settled.</summary>
        Tracked,
        /// <summary>Tracked but low confidence. Live pose, hand tinted, no grab/release decisions.</summary>
        Uncertain,
        /// <summary>Not tracked for a while. Pose frozen at the last tracked pose and faded.</summary>
        Lost,
        /// <summary>Back after being lost. Blending from the frozen pose to the live pose.</summary>
        Recovering,
    }

    [Serializable]
    public sealed class HandPresenceSettings
    {
        /// <summary>Untracked gaps shorter than this count as uncertain, not lost.</summary>
        public double LostAfterSeconds = 0.10;
        /// <summary>Low confidence for this long gives full warning tint.</summary>
        public double TintRampSeconds = 0.25;
        /// <summary>Time to fade a lost hand down to <see cref="LostAlpha"/>.</summary>
        public double FadeOutSeconds = 0.30;
        public float LostAlpha = 0.25f;
        /// <summary>Time to blend from the frozen pose back to the live pose.</summary>
        public double BlendBackSeconds = 0.25;
        /// <summary>A hand must be steadily tracked this long before it may grab or release.</summary>
        public double SettleSeconds = 0.15;
    }

    /// <summary>
    /// Turns raw tracking flags into the game's tracking-loss behaviour (docs/DESIGN.md):
    /// tint as confidence drops, freeze and fade when lost, blend back on return,
    /// and never make grab/release decisions on unreliable data. Pure logic: feed it every frame.
    /// </summary>
    public sealed class HandPresence
    {
        private readonly HandPresenceSettings settings;

        private bool started;
        private double lastTime;
        private double untrackedSince = -1;
        private double lowSince = -1;
        private double goodSince = -1;
        private double recoverStart = -1;

        public HandPresence(HandPresenceSettings settings = null)
        {
            this.settings = settings ?? new HandPresenceSettings();
            State = HandPresenceState.Lost;
            Alpha = this.settings.LostAlpha;
            Tint = 1f;
        }

        public HandPresenceState State { get; private set; }

        /// <summary>0 = normal colour, 1 = full warning tint.</summary>
        public float Tint { get; private set; }

        /// <summary>Visual opacity for the hand and anything it holds.</summary>
        public float Alpha { get; private set; }

        /// <summary>0 = show the frozen pose, 1 = show the live pose.</summary>
        public float LiveWeight { get; private set; }

        /// <summary>True when grab and release decisions may use this hand's data.</summary>
        public bool ReliableForDecisions { get; private set; }

        /// <summary>True once the hand has been tracked at least once (there is a pose to freeze).</summary>
        public bool HasPose { get; private set; }

        /// <summary>How long the hand has been continuously untracked (0 while tracked).</summary>
        public double UntrackedSeconds { get; private set; }

        public void Update(double time, bool tracked, bool highConfidence)
        {
            double dt = started ? Math.Max(0.0, time - lastTime) : 0.0;
            started = true;
            lastTime = time;

            if (!tracked)
            {
                UpdateUntracked(time, dt);
                return;
            }

            untrackedSince = -1;
            UntrackedSeconds = 0;
            HasPose = true;

            if (State == HandPresenceState.Lost)
            {
                State = HandPresenceState.Recovering;
                recoverStart = time;
                goodSince = -1;
            }

            bool good = highConfidence;
            if (good)
            {
                lowSince = -1;
                if (goodSince < 0)
                {
                    goodSince = time;
                }
                Tint = Approach(Tint, 0f, dt, settings.TintRampSeconds);
            }
            else
            {
                goodSince = -1;
                if (lowSince < 0)
                {
                    lowSince = time;
                }
                Tint = Math.Max(Tint, Ramp(time - lowSince, settings.TintRampSeconds));
            }

            if (State == HandPresenceState.Recovering)
            {
                LiveWeight = Ramp(time - recoverStart, settings.BlendBackSeconds);
                if (LiveWeight >= 1f)
                {
                    State = good ? HandPresenceState.Tracked : HandPresenceState.Uncertain;
                }
            }
            else
            {
                LiveWeight = 1f;
                State = good ? HandPresenceState.Tracked : HandPresenceState.Uncertain;
            }

            Alpha = Approach(Alpha, 1f, dt, settings.BlendBackSeconds);
            ReliableForDecisions = State == HandPresenceState.Tracked
                && goodSince >= 0
                && time - goodSince >= settings.SettleSeconds - 1e-9;
        }

        private void UpdateUntracked(double time, double dt)
        {
            ReliableForDecisions = false;
            goodSince = -1;
            lowSince = -1;
            if (untrackedSince < 0)
            {
                untrackedSince = time;
            }
            UntrackedSeconds = time - untrackedSince;

            if (State == HandPresenceState.Lost || UntrackedSeconds >= settings.LostAfterSeconds - 1e-9)
            {
                State = HandPresenceState.Lost;
                LiveWeight = 0f;
                Tint = 1f;
                Alpha = Approach(Alpha, settings.LostAlpha, dt, settings.FadeOutSeconds);
            }
            else
            {
                // A brief gap: hold the last pose (the adapter has no live data) and warn.
                State = HandPresenceState.Uncertain;
                Tint = Math.Max(Tint, Ramp(UntrackedSeconds, settings.TintRampSeconds));
            }
        }

        private static float Ramp(double elapsed, double duration)
        {
            if (duration <= 0)
            {
                return 1f;
            }
            return (float)Math.Min(1.0, Math.Max(0.0, elapsed / duration));
        }

        /// <summary>Moves <paramref name="value"/> toward <paramref name="target"/> covering the full 0..1 range in <paramref name="duration"/>.</summary>
        private static float Approach(float value, float target, double dt, double duration)
        {
            if (duration <= 0)
            {
                return target;
            }
            float step = (float)(dt / duration);
            if (value < target)
            {
                return Math.Min(target, value + step);
            }
            return Math.Max(target, value - step);
        }
    }

    /// <summary>
    /// "The crowd waits": pauses the show when both hands have been gone for a while, and resumes
    /// once both are steadily back. Doubles as the clean pause/resume Meta asks for.
    /// </summary>
    public sealed class BothHandsPause
    {
        private readonly double pauseAfterSeconds;
        private readonly double resumeAfterSeconds;
        private double bothGoneSince = -1;
        private double bothBackSince = -1;

        public BothHandsPause(double pauseAfterSeconds = 2.0, double resumeAfterSeconds = 0.5)
        {
            this.pauseAfterSeconds = pauseAfterSeconds;
            this.resumeAfterSeconds = resumeAfterSeconds;
        }

        public bool Paused { get; private set; }

        /// <summary>Returns true on the frame the paused state changes.</summary>
        public bool Update(double time, bool leftTracked, bool rightTracked)
        {
            bool before = Paused;
            if (!leftTracked && !rightTracked)
            {
                bothBackSince = -1;
                if (bothGoneSince < 0)
                {
                    bothGoneSince = time;
                }
                if (time - bothGoneSince >= pauseAfterSeconds - 1e-9)
                {
                    Paused = true;
                }
            }
            else
            {
                bothGoneSince = -1;
                if (Paused)
                {
                    if (leftTracked && rightTracked)
                    {
                        if (bothBackSince < 0)
                        {
                            bothBackSince = time;
                        }
                        if (time - bothBackSince >= resumeAfterSeconds - 1e-9)
                        {
                            Paused = false;
                            bothBackSince = -1;
                        }
                    }
                    else
                    {
                        bothBackSince = -1;
                    }
                }
            }
            return before != Paused;
        }
    }
}
