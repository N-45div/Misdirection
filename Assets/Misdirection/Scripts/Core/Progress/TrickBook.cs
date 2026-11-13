using System;
using Misdirection.Core.Crowd;

namespace Misdirection.Core.Progress
{
    /// <summary>
    /// How well the player knows one trick: three feats, one star each. Stars are kept for good.
    /// </summary>
    [Serializable]
    public sealed class TrickMastery
    {
        /// <summary>Star: a reveal that wasn't heckled (applause or a gasp).</summary>
        public bool Clean;
        /// <summary>Star: one whole performance with applause on every reveal.</summary>
        public bool Ovation;
        /// <summary>Star: one whole performance with no heckles, in front of the Skeptic and the Filmer together.</summary>
        public bool ToughCrowd;
        /// <summary>Whole performances so far (finished, bust-free or not).</summary>
        public int Performances;

        /// <summary>0 to 3.</summary>
        public int Stars
        {
            get { return (Clean ? 1 : 0) + (Ovation ? 1 : 0) + (ToughCrowd ? 1 : 0); }
        }
    }

    /// <summary>
    /// The trick book: mastery stars for each trick, earned from what happened in a show.
    ///  1. Clean: any reveal of the trick that isn't heckled.
    ///  2. Ovation: a whole performance (the act finished with no bust and at least two reveals,
    ///     so the vanish and the reappearance) where every reveal got applause.
    ///  3. Tough crowd: a whole performance with no heckle, watched by both the Skeptic and the
    ///     Filmer. Whether they are both there depends on the day's crowd: a reason to come back.
    /// </summary>
    public static class TrickBook
    {
        /// <summary>Tricks in the book: 0 = vanish and produce, 1 = coin under the cup.</summary>
        public const int TrickCount = 2;
        public const int MaxStars = 3;
        /// <summary>A whole performance shows the vanish and the reappearance.</summary>
        public const int RevealsInAWholePerformance = 2;

        public static string NameOf(int trick)
        {
            switch (trick)
            {
                case 0: return "Vanish";
                case 1: return "Cup";
                default: return "Trick " + (trick + 1);
            }
        }

        /// <summary>What the next star for this trick takes, or "" when it has all three.</summary>
        public static string NextStarHint(TrickMastery mastery)
        {
            if (mastery == null || !mastery.Clean)
            {
                return "a reveal with no heckle";
            }
            if (!mastery.Ovation)
            {
                return "applause on every reveal";
            }
            if (!mastery.ToughCrowd)
            {
                return "no heckles, Skeptic and Filmer watching";
            }
            return "";
        }

        /// <summary>
        /// Updates the book from one show. <paramref name="actsCompleted"/> acts were finished (a
        /// walk-off leaves the last one unfinished). Returns the stars gained, per trick.
        /// </summary>
        public static int[] Learn(TrickMastery[] book, ShowRecord record, int actsCompleted)
        {
            int[] gained = new int[book != null ? book.Length : 0];
            if (book == null || record == null)
            {
                return gained;
            }
            int[] before = new int[book.Length];
            for (int i = 0; i < book.Length; i++)
            {
                before[i] = book[i] != null ? book[i].Stars : 0;
            }

            for (int act = 0; act < record.ActCount; act++)
            {
                int trick = record.TrickOf(act);
                if (trick < 0 || trick >= book.Length || book[trick] == null)
                {
                    continue;
                }
                TrickMastery m = book[trick];
                int reveals = record.RevealsIn(act);
                int heckles = record.HecklesIn(act);
                if (reveals > heckles)
                {
                    m.Clean = true;
                }
                if (act >= actsCompleted)
                {
                    continue;
                }
                m.Performances++;
                bool whole = record.BustsIn(act) == 0 && reveals >= RevealsInAWholePerformance;
                if (whole && record.ApplauseIn(act) == reveals)
                {
                    m.Ovation = true;
                }
                if (whole && heckles == 0 && record.CrowdHas(act, Archetype.Skeptic) && record.CrowdHas(act, Archetype.Filmer))
                {
                    m.ToughCrowd = true;
                }
            }

            for (int i = 0; i < book.Length; i++)
            {
                gained[i] = (book[i] != null ? book[i].Stars : 0) - before[i];
            }
            return gained;
        }
    }
}
