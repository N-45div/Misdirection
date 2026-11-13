using System;

namespace Misdirection.Core.Access
{
    [Serializable]
    public sealed class LoudSoundSettings
    {
        /// <summary>The envelope rises to a louder level this fast (seconds).</summary>
        public double AttackSeconds = 0.01;
        /// <summary>...and falls back this fast.</summary>
        public double ReleaseSeconds = 0.05;
        /// <summary>The noise floor creeps up toward the room's level this slowly, so a steady noise stops counting as loud.</summary>
        public double FloorRiseSeconds = 1.5;
        /// <summary>...and drops this fast when the room goes quiet.</summary>
        public double FloorFallSeconds = 0.4;
        /// <summary>Loud means at least this many times the noise floor (4 = about 12 dB above it).</summary>
        public float LoudRatio = 4f;
        /// <summary>...and at least this level whatever the floor (0..1 of full scale).</summary>
        public float MinLevel = 0.04f;
        /// <summary>The sound ends when the envelope falls below this fraction of the loud threshold.</summary>
        public float EndRatio = 0.5f;
        /// <summary>Shorter than this is a click or a glitch, not a call.</summary>
        public double MinLoudSeconds = 0.03;
        /// <summary>Longer than this is talking, music or a fan, not a call.</summary>
        public double MaxLoudSeconds = 0.9;
        /// <summary>After a call, nothing counts for this long.</summary>
        public double RefractorySeconds = 1.2;
        /// <summary>Listen this long before the first call can count (lets the noise floor settle).</summary>
        public double WarmUpSeconds = 0.5;
    }

    /// <summary>
    /// The voice alternative to the show-hand flourish: hears a short, loud sound ("Look!", a clap)
    /// standing well clear of the room's noise. No speech recognition: an envelope follower, an
    /// adaptive noise floor, a threshold and a refractory period. It fires when the sound ends, so a
    /// long noise never counts, and it prefers missing a call to inventing one. Pure logic: feed it
    /// the level (RMS, 0..1) of each short block of microphone samples.
    /// </summary>
    public sealed class LoudSoundDetector
    {
        private readonly LoudSoundSettings settings;
        private bool started;
        private double startTime;
        private double lastTime;
        private double loudSince = -1;
        private double loudFor;
        private bool tooLong;
        private double quietUntil = double.NegativeInfinity;

        public LoudSoundDetector(LoudSoundSettings settings = null)
        {
            this.settings = settings ?? new LoudSoundSettings();
        }

        /// <summary>The smoothed level.</summary>
        public float Envelope { get; private set; }

        /// <summary>The room's steady noise level.</summary>
        public float NoiseFloor { get; private set; }

        /// <summary>The level a sound must reach to count as loud right now.</summary>
        public float Threshold
        {
            get { return Math.Max(settings.MinLevel, NoiseFloor * settings.LoudRatio); }
        }

        /// <summary>True while a loud sound is in progress.</summary>
        public bool Loud { get { return loudSince >= 0; } }

        /// <summary>Calls heard since the start (for debugging).</summary>
        public int Count { get; private set; }

        /// <summary>
        /// Ignore everything until <paramref name="time"/>: the game's own applause and gasps come out of
        /// the headset's speakers and into its microphone.
        /// </summary>
        public void Suppress(double time)
        {
            quietUntil = Math.Max(quietUntil, time);
        }

        /// <summary>Feed one block's level. Returns true when a short loud sound has just ended.</summary>
        public bool Update(double time, float level)
        {
            if (float.IsNaN(level) || level < 0f)
            {
                level = 0f;
            }
            if (!started)
            {
                started = true;
                startTime = time;
                lastTime = time;
                Envelope = level;
                NoiseFloor = level;
                return false;
            }
            double dt = Math.Min(0.1, Math.Max(0.0, time - lastTime));
            lastTime = time;

            Envelope = Follow(Envelope, level, dt, level > Envelope ? settings.AttackSeconds : settings.ReleaseSeconds);

            bool loudNow = loudSince >= 0 ? Envelope >= Threshold * settings.EndRatio : Envelope >= Threshold;
            if (loudNow && loudSince < 0)
            {
                loudSince = time;
                loudFor = 0;
                tooLong = false;
            }
            if (loudSince >= 0 && level >= Threshold * settings.EndRatio)
            {
                // Time the sound itself was loud, not the envelope's tail: a click stays a click.
                loudFor += dt;
            }
            if (loudSince >= 0 && time - loudSince > settings.MaxLoudSeconds)
            {
                tooLong = true;
            }
            // The floor holds still during a short loud sound; once it has gone on too long, it is the room.
            if (loudSince < 0 || tooLong)
            {
                NoiseFloor = Follow(NoiseFloor, Envelope, dt, Envelope > NoiseFloor ? settings.FloorRiseSeconds : settings.FloorFallSeconds);
            }

            if (loudNow || loudSince < 0)
            {
                return false;
            }
            // The loud sound has just ended.
            loudSince = -1;
            bool counts = !tooLong
                && loudFor >= settings.MinLoudSeconds - 1e-9
                && time - startTime >= settings.WarmUpSeconds
                && time >= quietUntil;
            tooLong = false;
            if (!counts)
            {
                return false;
            }
            quietUntil = time + settings.RefractorySeconds;
            Count++;
            return true;
        }

        public void Reset()
        {
            started = false;
            loudSince = -1;
            loudFor = 0;
            tooLong = false;
            quietUntil = double.NegativeInfinity;
            Envelope = 0f;
            NoiseFloor = 0f;
        }

        /// <summary>One-pole follower: moves <paramref name="value"/> toward <paramref name="target"/> with time constant <paramref name="seconds"/>.</summary>
        private static float Follow(float value, float target, double dt, double seconds)
        {
            if (seconds <= 0)
            {
                return target;
            }
            double k = 1.0 - Math.Exp(-dt / seconds);
            return (float)(value + (target - value) * k);
        }
    }
}
