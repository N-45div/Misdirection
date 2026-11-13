using System;
using Misdirection.Core.Crowd;

namespace Misdirection.Core.Progress
{
    /// <summary>
    /// What the Regular remembers of your last show: its signature (whose ear the coin came from and
    /// which trick the show ended on) and how many shows running you have repeated it.
    /// </summary>
    [Serializable]
    public sealed class RegularMemory
    {
        /// <summary>Date (yyyymmdd) of the remembered show; 0 = nothing remembered yet.</summary>
        public int Date;
        /// <summary>Archetype (as a number) the coin was last produced from, or -1.</summary>
        public int ProducedFrom = ShowRecord.Nobody;
        /// <summary>The trick that show ended on (0 = vanish and produce, 1 = cup), or -1.</summary>
        public int EndTrick = ShowRecord.Nobody;
        /// <summary>Shows in a row that repeated this signature (0 = he has seen it once).</summary>
        public int Repeats;

        /// <summary>There is a production to remember.</summary>
        public bool HasSignature
        {
            get { return Date > 0 && ProducedFrom != ShowRecord.Nobody; }
        }
    }

    /// <summary>How the Regular walks in today.</summary>
    public struct RegularAttitude
    {
        /// <summary>He has seen this method more than once: Curious from the start and quicker to suspect.</summary>
        public bool Wary;
        /// <summary>For <see cref="SpectatorMind.SetWariness"/>: 1 = normal.</summary>
        public float SuspicionGainScale;
        /// <summary>For <see cref="SpectatorMind.SetWariness"/>: 0 = normal.</summary>
        public float SuspicionFloor;
    }

    /// <summary>What the Regular makes of a production.</summary>
    public enum RegularReaction
    {
        /// <summary>Nothing to compare it with.</summary>
        None,
        /// <summary>The same ear as last time: "Same as yesterday!"</summary>
        SameAgain,
        /// <summary>A different ear: "That's new!", and he relaxes.</summary>
        SomethingNew,
    }

    [Serializable]
    public sealed class RegularSettings
    {
        /// <summary>Each repeat makes seen methods raise his suspicion this much faster (0.35 = +35%).</summary>
        public float GainPerRepeat = 0.35f;
        /// <summary>Repeats beyond this many don't make him any warier.</summary>
        public int MaxRepeats = 3;
        /// <summary>A wary Regular's suspicion never falls below this (just above the Curious level, 0.35).</summary>
        public float WaryFloor = 0.4f;
        /// <summary>Suspicion he gains on the spot when the coin comes from the same ear again.</summary>
        public float RepeatBump = 0.3f;
    }

    /// <summary>
    /// The returning Regular: he remembers how your last show went. Repeat its signature and he says
    /// "Same as yesterday!"; from then on he arrives Curious and suspects that method faster, a little
    /// more with every repeat. Produce the coin from somebody else and he relaxes. Pure rules; the
    /// scene applies <see cref="AttitudeFor"/> to his <see cref="SpectatorMind"/> with <see cref="Apply"/>.
    /// </summary>
    public static class RegularRule
    {
        /// <summary>
        /// His attitude at the start of an act performing <paramref name="trick"/>. He is only wary of
        /// the method he keeps seeing: the trick his remembered show ended on.
        /// </summary>
        public static RegularAttitude AttitudeFor(RegularMemory memory, int trick, RegularSettings settings = null)
        {
            settings = settings ?? new RegularSettings();
            RegularAttitude a = new RegularAttitude();
            a.SuspicionGainScale = 1f;
            a.SuspicionFloor = 0f;
            if (memory == null || !memory.HasSignature || memory.Repeats <= 0 || memory.EndTrick != trick)
            {
                return a;
            }
            int repeats = Math.Min(memory.Repeats, Math.Max(1, settings.MaxRepeats));
            a.Wary = true;
            a.SuspicionGainScale = 1f + Math.Max(0f, settings.GainPerRepeat) * repeats;
            a.SuspicionFloor = Math.Max(0f, settings.WaryFloor);
            return a;
        }

        /// <summary>The hook into the crowd model: makes one spectator (the Regular) as wary as the attitude says.</summary>
        public static void Apply(SpectatorMind regular, RegularAttitude attitude)
        {
            if (regular != null)
            {
                regular.SetWariness(attitude.SuspicionGainScale, attitude.SuspicionFloor);
            }
        }

        /// <summary>The coin was just produced from <paramref name="from"/>'s ear while the Regular watched.</summary>
        public static RegularReaction OnProduce(RegularMemory memory, Archetype from)
        {
            if (memory == null || !memory.HasSignature)
            {
                return RegularReaction.None;
            }
            return memory.ProducedFrom == (int)from ? RegularReaction.SameAgain : RegularReaction.SomethingNew;
        }

        /// <summary>
        /// Applies his reaction on the spot: the same ear again adds suspicion; something new clears
        /// his wariness and his suspicion.
        /// </summary>
        public static void React(SpectatorMind regular, RegularReaction reaction, RegularSettings settings = null)
        {
            if (regular == null)
            {
                return;
            }
            settings = settings ?? new RegularSettings();
            if (reaction == RegularReaction.SameAgain)
            {
                regular.AddSuspicion(Math.Max(0f, settings.RepeatBump));
            }
            else if (reaction == RegularReaction.SomethingNew)
            {
                regular.SetWariness(1f, 0f);
                regular.ResetSuspicion();
            }
        }

        /// <summary>What he says. "Yesterday" only when the remembered show really was the day before.</summary>
        public static string Line(RegularReaction reaction, RegularMemory memory, int today)
        {
            switch (reaction)
            {
                case RegularReaction.SameAgain:
                    return memory != null && CalendarDay.DaysBetween(memory.Date, today) == 1 ? "Same as yesterday!" : "Same as last time!";
                case RegularReaction.SomethingNew:
                    return "That's new!";
                default:
                    return "";
            }
        }

        /// <summary>
        /// A show ended on <paramref name="date"/>: remember its signature. A show with no production
        /// leaves the memory as it was. The same signature again counts as a repeat; a new one starts over.
        /// </summary>
        public static void Remember(RegularMemory memory, ShowRecord record, int date)
        {
            if (memory == null || record == null)
            {
                return;
            }
            int from = record.LastProducedFrom;
            if (from == ShowRecord.Nobody)
            {
                return;
            }
            int endTrick = record.EndTrick;
            if (memory.HasSignature && memory.ProducedFrom == from && memory.EndTrick == endTrick)
            {
                memory.Repeats++;
            }
            else
            {
                memory.Repeats = 0;
            }
            memory.ProducedFrom = from;
            memory.EndTrick = endTrick;
            memory.Date = date;
        }
    }
}
