using System;
using System.Collections.Generic;
using Misdirection.Core.Crowd;

namespace Misdirection.Core.Progress
{
    /// <summary>How one day's challenge went.</summary>
    [Serializable]
    public sealed class ChallengeResult
    {
        /// <summary>The day (yyyymmdd).</summary>
        public int Date;
        /// <summary>A <see cref="ChallengeKind"/> as a number (so a newer build's kinds survive an older one).</summary>
        public int Kind;
        public int Target;
        /// <summary>Met in at least one show that day.</summary>
        public bool Passed;
    }

    /// <summary>
    /// Everything remembered between sessions, saved on the device as JSON (public fields only, so
    /// Unity's JsonUtility can write it). Every field has a safe default: a missing or half-written
    /// file still loads. Run a loaded save through <see cref="ProgressSave.Upgrade"/> before using it.
    /// </summary>
    [Serializable]
    public sealed class ProgressData
    {
        /// <summary>Format version written by this build. Bump it when a field changes meaning, and teach <see cref="ProgressSave.Upgrade"/> the step.</summary>
        public const int CurrentVersion = 1;
        /// <summary>Daily challenge results are kept for this many days.</summary>
        public const int HistoryDays = 14;

        public int Version = CurrentVersion;

        /// <summary>Shows that reached an end (a bow or a walk-off).</summary>
        public int ShowsPlayed;
        /// <summary>Shows that reached the bow.</summary>
        public int ShowsFinished;
        public int BestTips;
        /// <summary>Best show rating so far, 0 to 3.</summary>
        public int BestStars;
        public int TotalTips;

        /// <summary>Calendar days with at least one show.</summary>
        public int DaysPlayed;
        /// <summary>yyyymmdd; 0 = never played.</summary>
        public int FirstPlayedDate;
        public int LastPlayedDate;
        /// <summary>Days in a row up to <see cref="LastPlayedDate"/>.</summary>
        public int CurrentStreak;
        public int LongestStreak;

        /// <summary>The trick book: index 0 = vanish and produce, 1 = coin under the cup.</summary>
        public TrickMastery[] Tricks = { new TrickMastery(), new TrickMastery() };
        /// <summary>Daily challenge results, oldest first, at most <see cref="HistoryDays"/> days.</summary>
        public ChallengeResult[] Challenges = new ChallengeResult[0];
        public RegularMemory Regular = new RegularMemory();
    }

    /// <summary>
    /// Keeps saved progress trustworthy: <see cref="Upgrade"/> turns whatever was loaded (old, newer,
    /// hand-edited, half-empty) into a valid save in the current format, and <see cref="Merge"/> combines
    /// two copies of the same save (the file and its backup) without losing anything earned.
    /// </summary>
    public static class ProgressSave
    {
        /// <summary>The save was written by a newer build than this one: fields this build doesn't know are lost on the next save.</summary>
        public static bool IsFromNewerBuild(ProgressData data)
        {
            return data != null && data.Version > ProgressData.CurrentVersion;
        }

        /// <summary>
        /// Returns a save that is safe to use: never null, current version, no nulls inside, counts and
        /// dates in range, history trimmed. Fixes <paramref name="data"/> in place when it can.
        /// </summary>
        public static ProgressData Upgrade(ProgressData data)
        {
            if (data == null)
            {
                return new ProgressData();
            }
            // Version steps go here, oldest first (if (data.Version < 2) { ... }). Version 0 or less is a
            // save from before the field existed: same layout as version 1.
            data.Version = ProgressData.CurrentVersion;

            data.ShowsPlayed = Math.Max(0, data.ShowsPlayed);
            data.BestTips = Math.Max(0, data.BestTips);
            data.TotalTips = Math.Max(data.BestTips, data.TotalTips);
            data.BestStars = Math.Max(0, Math.Min(TrickBook.MaxStars, data.BestStars));

            if (!CalendarDay.IsValid(data.LastPlayedDate))
            {
                data.LastPlayedDate = CalendarDay.None;
            }
            if (!CalendarDay.IsValid(data.FirstPlayedDate))
            {
                data.FirstPlayedDate = CalendarDay.None;
            }
            if (data.LastPlayedDate == CalendarDay.None)
            {
                // Never played (or the date was unreadable): no days, no streak.
                data.FirstPlayedDate = CalendarDay.None;
                data.DaysPlayed = 0;
                data.CurrentStreak = 0;
            }
            else
            {
                if (data.FirstPlayedDate == CalendarDay.None || data.FirstPlayedDate > data.LastPlayedDate)
                {
                    data.FirstPlayedDate = data.LastPlayedDate;
                }
                data.DaysPlayed = Math.Max(1, data.DaysPlayed);
                data.CurrentStreak = Math.Max(1, Math.Min(data.CurrentStreak, data.DaysPlayed));
                data.ShowsPlayed = Math.Max(data.ShowsPlayed, data.DaysPlayed);
            }
            data.LongestStreak = Math.Max(data.CurrentStreak, Math.Min(data.LongestStreak, data.DaysPlayed));
            data.ShowsFinished = Math.Max(0, Math.Min(data.ShowsFinished, data.ShowsPlayed));

            data.Tricks = FixTricks(data.Tricks);
            data.Challenges = FixHistory(data.Challenges);
            data.Regular = FixRegular(data.Regular);
            return data;
        }

        /// <summary>
        /// Combines two copies of one player's save (say the file and its backup after a crash): counts
        /// and bests take the higher value, stars and passed challenges are never lost, and the streak
        /// and the Regular's memory come from whichever copy was played most recently. Returns a new save.
        /// </summary>
        public static ProgressData Merge(ProgressData a, ProgressData b)
        {
            a = Upgrade(a);
            b = Upgrade(b);
            bool aLater = a.LastPlayedDate > b.LastPlayedDate
                || (a.LastPlayedDate == b.LastPlayedDate && a.ShowsPlayed >= b.ShowsPlayed);
            ProgressData later = aLater ? a : b;
            ProgressData earlier = aLater ? b : a;

            ProgressData m = new ProgressData();
            m.ShowsPlayed = Math.Max(a.ShowsPlayed, b.ShowsPlayed);
            m.ShowsFinished = Math.Max(a.ShowsFinished, b.ShowsFinished);
            m.BestTips = Math.Max(a.BestTips, b.BestTips);
            m.BestStars = Math.Max(a.BestStars, b.BestStars);
            m.TotalTips = Math.Max(a.TotalTips, b.TotalTips);
            m.DaysPlayed = Math.Max(a.DaysPlayed, b.DaysPlayed);
            m.FirstPlayedDate = earlier.FirstPlayedDate != CalendarDay.None
                && (later.FirstPlayedDate == CalendarDay.None || earlier.FirstPlayedDate < later.FirstPlayedDate)
                    ? earlier.FirstPlayedDate : later.FirstPlayedDate;
            m.LastPlayedDate = later.LastPlayedDate;
            m.CurrentStreak = later.LastPlayedDate == earlier.LastPlayedDate
                ? Math.Max(later.CurrentStreak, earlier.CurrentStreak) : later.CurrentStreak;
            m.LongestStreak = Math.Max(a.LongestStreak, b.LongestStreak);

            m.Tricks = new TrickMastery[Math.Max(a.Tricks.Length, b.Tricks.Length)];
            for (int i = 0; i < m.Tricks.Length; i++)
            {
                TrickMastery x = i < a.Tricks.Length ? a.Tricks[i] : new TrickMastery();
                TrickMastery y = i < b.Tricks.Length ? b.Tricks[i] : new TrickMastery();
                TrickMastery t = new TrickMastery();
                t.Clean = x.Clean || y.Clean;
                t.Ovation = x.Ovation || y.Ovation;
                t.ToughCrowd = x.ToughCrowd || y.ToughCrowd;
                t.Performances = Math.Max(x.Performances, y.Performances);
                m.Tricks[i] = t;
            }

            // The later copy's entries go last, so they win where both have the same day.
            List<ChallengeResult> history = new List<ChallengeResult>(earlier.Challenges);
            history.AddRange(later.Challenges);
            m.Challenges = FixHistory(history.ToArray());

            bool laterMemory = later.Regular.Date > earlier.Regular.Date
                || (later.Regular.Date == earlier.Regular.Date && later.Regular.Repeats >= earlier.Regular.Repeats);
            RegularMemory memory = laterMemory ? later.Regular : earlier.Regular;
            m.Regular = new RegularMemory();
            m.Regular.Date = memory.Date;
            m.Regular.ProducedFrom = memory.ProducedFrom;
            m.Regular.EndTrick = memory.EndTrick;
            m.Regular.Repeats = memory.Repeats;
            return Upgrade(m);
        }

        /// <summary>At least one entry per trick this build knows, none of them null. Extra entries (a newer build's tricks) are kept.</summary>
        private static TrickMastery[] FixTricks(TrickMastery[] tricks)
        {
            int length = Math.Max(TrickBook.TrickCount, tricks != null ? tricks.Length : 0);
            TrickMastery[] result = new TrickMastery[length];
            for (int i = 0; i < length; i++)
            {
                TrickMastery t = tricks != null && i < tricks.Length ? tricks[i] : null;
                result[i] = t ?? new TrickMastery();
                result[i].Performances = Math.Max(0, result[i].Performances);
            }
            return result;
        }

        /// <summary>One entry per real date, oldest first, the newest <see cref="ProgressData.HistoryDays"/> only. A day passed in any copy stays passed.</summary>
        private static ChallengeResult[] FixHistory(ChallengeResult[] history)
        {
            List<ChallengeResult> clean = new List<ChallengeResult>();
            if (history != null)
            {
                foreach (ChallengeResult original in history)
                {
                    if (original == null || !CalendarDay.IsValid(original.Date))
                    {
                        continue;
                    }
                    // A copy, so fixing one save never changes another that shares the entry.
                    ChallengeResult entry = new ChallengeResult();
                    entry.Date = original.Date;
                    entry.Kind = original.Kind;
                    entry.Target = original.Target;
                    entry.Passed = original.Passed;
                    int at = clean.FindIndex(delegate(ChallengeResult c) { return c.Date == entry.Date; });
                    if (at < 0)
                    {
                        clean.Add(entry);
                    }
                    else
                    {
                        // The same day twice: the later entry's challenge, passed if either was.
                        entry.Passed = entry.Passed || clean[at].Passed;
                        clean[at] = entry;
                    }
                }
            }
            clean.Sort(delegate(ChallengeResult x, ChallengeResult y) { return x.Date.CompareTo(y.Date); });
            if (clean.Count > ProgressData.HistoryDays)
            {
                clean.RemoveRange(0, clean.Count - ProgressData.HistoryDays);
            }
            return clean.ToArray();
        }

        private static RegularMemory FixRegular(RegularMemory memory)
        {
            if (memory == null)
            {
                return new RegularMemory();
            }
            int archetypes = Enum.GetValues(typeof(Archetype)).Length;
            if (!CalendarDay.IsValid(memory.Date) || memory.ProducedFrom < 0 || memory.ProducedFrom >= archetypes)
            {
                // Nothing usable remembered.
                return new RegularMemory();
            }
            memory.EndTrick = Math.Max(ShowRecord.Nobody, memory.EndTrick);
            memory.Repeats = Math.Max(0, memory.Repeats);
            return memory;
        }
    }
}
