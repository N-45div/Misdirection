using System;
using Misdirection.Core.Show;

namespace Misdirection.Core.Progress
{
    /// <summary>What one finished show changed, for the trick book shown at the bow.</summary>
    public sealed class ShowOutcome
    {
        /// <summary>The day (yyyymmdd) the show counted for.</summary>
        public int Date;
        /// <summary>Days played so far, counting today ("Day 2").</summary>
        public int DayNumber;
        /// <summary>Days in a row, counting today.</summary>
        public int Streak;
        /// <summary>This was the first show of a new day (so the day count and the streak moved).</summary>
        public bool NewDay;
        /// <summary>This show met today's challenge.</summary>
        public bool ChallengePassedNow;
        /// <summary>Today's challenge has been met, by this show or an earlier one today.</summary>
        public bool ChallengePassedToday;
        /// <summary>Trick book stars gained by this show, per trick.</summary>
        public int[] StarsGained = new int[0];
        /// <summary>Beat an earlier best (never set on the very first show).</summary>
        public bool NewBestTips;
        public bool NewBestStars;
    }

    /// <summary>
    /// The progression rules: what today's show is, and what a finished show adds to the save (days,
    /// streak, bests, trick book stars, the daily challenge, the Regular's memory). Pure logic: the
    /// scene passes in today's date and what happened.
    /// </summary>
    public static class ProgressRules
    {
        /// <summary>The player has finished a show on some day before <paramref name="today"/>: the Regular comes back.</summary>
        public static bool IsReturning(ProgressData data, int today)
        {
            if (data == null || data.LastPlayedDate == CalendarDay.None)
            {
                return false;
            }
            return data.DaysPlayed >= 2 || data.LastPlayedDate != today;
        }

        /// <summary>Which day of playing today is: 1 on the first day, 2 on the second day played, and so on.</summary>
        public static int DayNumber(ProgressData data, int today)
        {
            if (data == null || data.LastPlayedDate == CalendarDay.None)
            {
                return 1;
            }
            return Math.Max(1, data.DaysPlayed + (IsNewDay(data, today) ? 1 : 0));
        }

        /// <summary>
        /// The streak as it stands on <paramref name="today"/>: still alive if the last show was today
        /// or yesterday, otherwise broken (0).
        /// </summary>
        public static int StreakOn(ProgressData data, int today)
        {
            if (data == null || data.LastPlayedDate == CalendarDay.None)
            {
                return 0;
            }
            return CalendarDay.DaysBetween(data.LastPlayedDate, today) <= 1 ? data.CurrentStreak : 0;
        }

        /// <summary>Today's show for this player.</summary>
        public static DailyPlan PlanFor(ProgressData data, int today)
        {
            return DailyShow.Plan(today, IsReturning(data, today));
        }

        /// <summary>Whether the challenge of <paramref name="date"/> has been met.</summary>
        public static bool ChallengePassedOn(ProgressData data, int date)
        {
            if (data != null && data.Challenges != null)
            {
                foreach (ChallengeResult c in data.Challenges)
                {
                    if (c != null && c.Date == date)
                    {
                        return c.Passed;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// A show ended (bow or walk-off) on <paramref name="today"/>. Updates the save and says what
        /// changed. A walk-off still counts as playing today: turning up keeps the streak.
        /// </summary>
        public static ShowOutcome FinishShow(ProgressData data, int today, DailyChallenge challenge, ShowRecord record, ShowSummary summary)
        {
            ShowOutcome outcome = new ShowOutcome();
            outcome.Date = today;
            if (data == null)
            {
                return outcome;
            }
            ProgressSave.Upgrade(data);

            // The day and the streak.
            outcome.NewDay = MarkDayPlayed(data, today);
            outcome.DayNumber = Math.Max(1, data.DaysPlayed);
            outcome.Streak = Math.Max(1, data.CurrentStreak);

            // Counts and bests.
            bool first = data.ShowsPlayed == 0;
            data.ShowsPlayed++;
            if (!summary.WalkedOff)
            {
                data.ShowsFinished++;
            }
            int tips = Math.Max(0, summary.Tips);
            int stars = Math.Max(0, Math.Min(TrickBook.MaxStars, summary.Stars));
            data.TotalTips += tips;
            outcome.NewBestTips = !first && tips > data.BestTips;
            outcome.NewBestStars = !first && stars > data.BestStars;
            data.BestTips = Math.Max(data.BestTips, tips);
            data.BestStars = Math.Max(data.BestStars, stars);

            if (record != null)
            {
                outcome.StarsGained = TrickBook.Learn(data.Tricks, record, summary.ActsCompleted);
                RegularRule.Remember(data.Regular, record, today);
            }
            else
            {
                outcome.StarsGained = new int[data.Tricks.Length];
            }

            // Today's challenge: once met, it stays met for the day.
            outcome.ChallengePassedNow = DailyChallenges.Passed(challenge, record, summary);
            outcome.ChallengePassedToday = outcome.ChallengePassedNow;
            if (challenge != null && CalendarDay.IsValid(today))
            {
                ChallengeResult entry = null;
                foreach (ChallengeResult c in data.Challenges)
                {
                    if (c.Date == today)
                    {
                        entry = c;
                    }
                }
                if (entry == null)
                {
                    entry = new ChallengeResult();
                    entry.Date = today;
                    ChallengeResult[] longer = new ChallengeResult[data.Challenges.Length + 1];
                    data.Challenges.CopyTo(longer, 0);
                    longer[longer.Length - 1] = entry;
                    data.Challenges = longer;
                }
                entry.Kind = (int)challenge.Kind;
                entry.Target = challenge.Target;
                entry.Passed = entry.Passed || outcome.ChallengePassedNow;
                outcome.ChallengePassedToday = entry.Passed;
            }

            // Sorts and trims the history, and keeps every count consistent.
            ProgressSave.Upgrade(data);
            return outcome;
        }

        /// <summary>
        /// Counts <paramref name="today"/> as a day played. Returns true if it is a new day: the day
        /// count goes up and the streak grows (yesterday was played) or starts again at 1 (it was not).
        /// The same day again, or a clock that went backwards, changes nothing.
        /// </summary>
        public static bool MarkDayPlayed(ProgressData data, int today)
        {
            if (data == null || !CalendarDay.IsValid(today))
            {
                return false;
            }
            if (data.LastPlayedDate == CalendarDay.None)
            {
                data.FirstPlayedDate = today;
                data.LastPlayedDate = today;
                data.DaysPlayed = 1;
                data.CurrentStreak = 1;
            }
            else
            {
                if (!IsNewDay(data, today))
                {
                    return false;
                }
                bool yesterday = CalendarDay.DaysBetween(data.LastPlayedDate, today) == 1;
                data.DaysPlayed++;
                data.CurrentStreak = yesterday ? data.CurrentStreak + 1 : 1;
                data.LastPlayedDate = today;
            }
            data.LongestStreak = Math.Max(data.LongestStreak, data.CurrentStreak);
            return true;
        }

        private static bool IsNewDay(ProgressData data, int today)
        {
            return CalendarDay.DaysBetween(data.LastPlayedDate, today) > 0;
        }
    }
}
