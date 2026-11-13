using System;

namespace Misdirection.Core.Progress
{
    /// <summary>
    /// Calendar dates as plain yyyymmdd integers (20261111 = 11 Nov 2026): easy to save, compare and
    /// read in a log. The core never reads the clock; the scene passes today's date in.
    /// </summary>
    public static class CalendarDay
    {
        /// <summary>"No date" (never played, nothing remembered).</summary>
        public const int None = 0;

        public static int From(int year, int month, int day)
        {
            return year * 10000 + month * 100 + day;
        }

        public static int From(DateTime date)
        {
            return From(date.Year, date.Month, date.Day);
        }

        /// <summary>A real calendar date (so 20260230 and 0 are not).</summary>
        public static bool IsValid(int date)
        {
            int year = date / 10000;
            int month = date / 100 % 100;
            int day = date % 100;
            if (date <= 0 || year < 1 || year > 9999 || month < 1 || month > 12 || day < 1)
            {
                return false;
            }
            return day <= DateTime.DaysInMonth(year, month);
        }

        /// <summary>Days since 1 Jan 0001, so consecutive days differ by exactly one. -1 for an invalid date.</summary>
        public static int Ordinal(int date)
        {
            if (!IsValid(date))
            {
                return -1;
            }
            DateTime d = new DateTime(date / 10000, date / 100 % 100, date % 100);
            return (int)(d.Ticks / TimeSpan.TicksPerDay);
        }

        /// <summary>The date a number of days after (or, negative, before) <paramref name="date"/>. None if it isn't a date.</summary>
        public static int AddDays(int date, int days)
        {
            int ordinal = Ordinal(date);
            if (ordinal < 0)
            {
                return None;
            }
            long shifted = (long)ordinal + days;
            if (shifted < 0 || shifted > DateTime.MaxValue.Ticks / TimeSpan.TicksPerDay)
            {
                return None;
            }
            return From(new DateTime(shifted * TimeSpan.TicksPerDay));
        }

        /// <summary>Whole days from one date to another (positive when <paramref name="to"/> is later). 0 if either isn't a date.</summary>
        public static int DaysBetween(int from, int to)
        {
            int a = Ordinal(from);
            int b = Ordinal(to);
            return a < 0 || b < 0 ? 0 : b - a;
        }
    }
}
