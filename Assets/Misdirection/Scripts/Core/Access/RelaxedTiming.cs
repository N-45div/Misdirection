using System;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;

namespace Misdirection.Core.Access
{
    [Serializable]
    public sealed class RelaxedTimingSettings
    {
        /// <summary>Poses the player must hold (coin held up, empty hand shown, the look before a pluck) get this much shorter.</summary>
        public float HoldScale = 0.6f;
        /// <summary>...but never shorter than this: a hold stays a settled state, never an instant.</summary>
        public double MinHoldSeconds = 0.2;
        /// <summary>Windows the player is given (how long eye contact holds a spectator, the tutorial's look timeout) get this much longer.</summary>
        public float WindowScale = 1.6f;
        /// <summary>The crowd's attention turns this much more slowly.</summary>
        public float TurnRateScale = 0.6f;
        /// <summary>The crowd gains this much less suspicion from what it sees.</summary>
        public float SuspicionScale = 0.6f;
    }

    /// <summary>
    /// "Longer windows" (Day 11): one switch that makes every timing kinder. The holds the player has
    /// to keep get shorter, the windows the player earns get longer, the crowd turns more slowly and
    /// is slower to suspect. Pure scaling: nothing here remembers state.
    /// </summary>
    public static class RelaxedTiming
    {
        private static readonly RelaxedTimingSettings Defaults = new RelaxedTimingSettings();

        /// <summary>A pose the player must hold for <paramref name="seconds"/>.</summary>
        public static double Hold(double seconds, bool relaxed, RelaxedTimingSettings settings = null)
        {
            if (!relaxed)
            {
                return seconds;
            }
            RelaxedTimingSettings s = settings ?? Defaults;
            // Already shorter than the floor: leave it alone rather than lengthen it.
            return Math.Min(seconds, Math.Max(s.MinHoldSeconds, seconds * s.HoldScale));
        }

        /// <summary>A window of <paramref name="seconds"/> the player is given.</summary>
        public static double Window(double seconds, bool relaxed, RelaxedTimingSettings settings = null)
        {
            if (!relaxed)
            {
                return seconds;
            }
            return seconds * Math.Max(1f, (settings ?? Defaults).WindowScale);
        }

        /// <summary>How fast a spectator's attention moves (1/s).</summary>
        public static float TurnRate(float rate, bool relaxed, RelaxedTimingSettings settings = null)
        {
            return relaxed ? rate * Clamp01((settings ?? Defaults).TurnRateScale) : rate;
        }

        /// <summary>Multiplier on suspicion gained from seen methods and exposure (1 when not relaxed).</summary>
        public static float SuspicionScale(bool relaxed, RelaxedTimingSettings settings = null)
        {
            return relaxed ? Clamp01((settings ?? Defaults).SuspicionScale) : 1f;
        }

        private static float Clamp01(float v)
        {
            return Math.Max(0f, Math.Min(1f, v));
        }
    }

    /// <summary>
    /// The timings as designed, captured once from the live settings objects, so relaxed timing can be
    /// switched on and off any number of times without drifting. The tricks, tutorial and crowd keep
    /// reading their own settings objects; <see cref="Apply"/> writes the scaled numbers into them.
    /// </summary>
    public sealed class RelaxedTimingBaseline
    {
        private double vanishShow, vanishReveal, vanishProduce;
        private double cupShow, cupReveal;
        private double lookHold, lookTimeout;
        private double lockHold;
        private bool hasVanish, hasCup, hasTutorial, hasAttention;

        /// <summary>Remember the designed timings. Any argument may be null (that part is then left alone).</summary>
        public static RelaxedTimingBaseline Capture(VanishTrickSettings vanish, CupTrickSettings cup, TutorialSettings tutorial,
                                                    AttentionSettings attention)
        {
            RelaxedTimingBaseline b = new RelaxedTimingBaseline();
            if (vanish != null)
            {
                b.hasVanish = true;
                b.vanishShow = vanish.ShowSeconds;
                b.vanishReveal = vanish.RevealHoldSeconds;
                b.vanishProduce = vanish.ProduceDwellSeconds;
            }
            if (cup != null)
            {
                b.hasCup = true;
                b.cupShow = cup.ShowSeconds;
                b.cupReveal = cup.RevealHoldSeconds;
            }
            if (tutorial != null)
            {
                b.hasTutorial = true;
                b.lookHold = tutorial.LookHoldSeconds;
                b.lookTimeout = tutorial.LookTimeoutSeconds;
            }
            if (attention != null)
            {
                b.hasAttention = true;
                b.lockHold = attention.LockHoldSeconds;
            }
            return b;
        }

        /// <summary>Write the designed timings (or their relaxed versions) into the live settings objects.</summary>
        public void Apply(VanishTrickSettings vanish, CupTrickSettings cup, TutorialSettings tutorial, AttentionSettings attention,
                          bool relaxed, RelaxedTimingSettings settings = null)
        {
            if (vanish != null && hasVanish)
            {
                vanish.ShowSeconds = RelaxedTiming.Hold(vanishShow, relaxed, settings);
                vanish.RevealHoldSeconds = RelaxedTiming.Hold(vanishReveal, relaxed, settings);
                vanish.ProduceDwellSeconds = RelaxedTiming.Hold(vanishProduce, relaxed, settings);
            }
            if (cup != null && hasCup)
            {
                cup.ShowSeconds = RelaxedTiming.Hold(cupShow, relaxed, settings);
                cup.RevealHoldSeconds = RelaxedTiming.Hold(cupReveal, relaxed, settings);
            }
            if (tutorial != null && hasTutorial)
            {
                tutorial.LookHoldSeconds = RelaxedTiming.Hold(lookHold, relaxed, settings);
                tutorial.LookTimeoutSeconds = RelaxedTiming.Window(lookTimeout, relaxed, settings);
            }
            if (attention != null && hasAttention)
            {
                // The misdirection window itself: how long eye contact keeps a spectator's eyes off the hands.
                attention.LockHoldSeconds = RelaxedTiming.Window(lockHold, relaxed, settings);
            }
        }
    }
}
