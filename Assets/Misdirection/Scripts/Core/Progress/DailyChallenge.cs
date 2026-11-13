using System;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;

namespace Misdirection.Core.Progress
{
    public enum ChallengeKind
    {
        /// <summary>Finish the show without a single bust.</summary>
        NoBusts,
        /// <summary>Pluck the coin from behind the Skeptic's ear without being heckled for it.</summary>
        SkepticEar,
        /// <summary>One whole act in which every reveal gets applause.</summary>
        FullApplauseAct,
        /// <summary>Finish the show with at least <see cref="DailyChallenge.Target"/> tips.</summary>
        TipTarget,
        /// <summary>Finish the show without "I saw that!" from one watchful spectator (the Filmer when present, else the Skeptic).</summary>
        QuietWatcher,
    }

    /// <summary>Today's extra goal. One per calendar day, the same for everybody on that date.</summary>
    [Serializable]
    public sealed class DailyChallenge
    {
        public ChallengeKind Kind;
        /// <summary>TipTarget: the tips to reach. QuietWatcher: the archetype (as a number) to keep quiet. Otherwise unused.</summary>
        public int Target;

        public DailyChallenge() { }

        public DailyChallenge(ChallengeKind kind, int target)
        {
            Kind = kind;
            Target = target;
        }

        /// <summary>A short line for the banner and the trick book (plain ASCII: the default font has it all).</summary>
        public string Text
        {
            get
            {
                switch (Kind)
                {
                    case ChallengeKind.NoBusts: return "No busts all show";
                    case ChallengeKind.SkepticEar: return "Find the coin behind the Skeptic's ear";
                    case ChallengeKind.FullApplauseAct: return "Applause for every reveal in one act";
                    case ChallengeKind.TipTarget: return "Finish with " + Target + " tips or more";
                    default: return "No heckles from the " + (Archetype)Target;
                }
            }
        }
    }

    /// <summary>
    /// Picks the day's challenge and decides whether a show met it. The five challenges come round in
    /// a shuffled order every five days, and never the same one two days running, so tomorrow always
    /// asks for something else.
    /// </summary>
    public static class DailyChallenges
    {
        public const int KindCount = 5;

        /// <summary>Tip targets step up from this in tens (about half to two thirds of a clean show's tips).</summary>
        public const int LowestTipTarget = 60;
        public const int TipTargetSteps = 4;

        /// <summary>The challenge for a date, fitted to that day's acts (who can be asked to stay quiet).</summary>
        public static DailyChallenge For(int date, ShowAct[] acts)
        {
            ChallengeKind kind = KindFor(date);
            int target = 0;
            if (kind == ChallengeKind.TipTarget)
            {
                SeededRandom rng = new SeededRandom(DailyShow.SeedFor(date) ^ 0x51ED270Bu);
                target = LowestTipTarget + 10 * rng.Next(TipTargetSteps);
            }
            else if (kind == ChallengeKind.QuietWatcher)
            {
                target = (int)(Appears(acts, Archetype.Filmer) ? Archetype.Filmer : Archetype.Skeptic);
            }
            return new DailyChallenge(kind, target);
        }

        /// <summary>Which of the five challenges a date gets.</summary>
        public static ChallengeKind KindFor(int date)
        {
            int ordinal = CalendarDay.Ordinal(date);
            if (ordinal < 0)
            {
                // Not a real date: still deterministic.
                ordinal = Math.Abs(date % 1000000);
            }
            int block = ordinal / KindCount;
            return (ChallengeKind)Order(block)[ordinal % KindCount];
        }

        /// <summary>Did this show meet the challenge?</summary>
        public static bool Passed(DailyChallenge challenge, ShowRecord record, ShowSummary summary)
        {
            if (challenge == null || record == null)
            {
                return false;
            }
            switch (challenge.Kind)
            {
                case ChallengeKind.NoBusts:
                    return !summary.WalkedOff && summary.Busts == 0 && summary.ActsCompleted > 0;

                case ChallengeKind.SkepticEar:
                    foreach (ShowEvent e in record.Events)
                    {
                        if (e.Kind == ShowEventKind.Reveal && e.Reveal == ShowRecord.Produce
                            && e.ProducedFrom == (int)Archetype.Skeptic && e.Reaction != Reaction.Heckle)
                        {
                            return true;
                        }
                    }
                    return false;

                case ChallengeKind.FullApplauseAct:
                    for (int act = 0; act < summary.ActsCompleted && act < record.ActCount; act++)
                    {
                        int reveals = record.RevealsIn(act);
                        if (reveals > 0 && record.ApplauseIn(act) == reveals && record.BustsIn(act) == 0)
                        {
                            return true;
                        }
                    }
                    return false;

                case ChallengeKind.TipTarget:
                    return !summary.WalkedOff && summary.Tips >= challenge.Target;

                case ChallengeKind.QuietWatcher:
                    if (summary.WalkedOff || summary.ActsCompleted <= 0)
                    {
                        return false;
                    }
                    foreach (ShowEvent e in record.Events)
                    {
                        if (e.Kind == ShowEventKind.Reveal && e.Reaction == Reaction.Heckle && e.SeenBy == challenge.Target)
                        {
                            return false;
                        }
                    }
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The order the five challenges come in for one five-day block: a seeded shuffle, nudged so
        /// a block never opens with the challenge the block before closed on.
        /// </summary>
        private static int[] Order(int block)
        {
            int[] order = Shuffled(block);
            int[] before = Shuffled(block - 1);
            // The nudge only ever swaps the first two, so the block before still ends where its plain shuffle ends.
            if (order[0] == before[KindCount - 1])
            {
                int swap = order[0];
                order[0] = order[1];
                order[1] = swap;
            }
            return order;
        }

        private static int[] Shuffled(int block)
        {
            int[] order = new int[KindCount];
            for (int i = 0; i < KindCount; i++)
            {
                order[i] = i;
            }
            SeededRandom rng = new SeededRandom(DailyShow.Mix(unchecked((uint)block) ^ 0xC0FFEE11u));
            for (int i = KindCount - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int swap = order[i];
                order[i] = order[j];
                order[j] = swap;
            }
            return order;
        }

        private static bool Appears(ShowAct[] acts, Archetype archetype)
        {
            if (acts == null)
            {
                return false;
            }
            foreach (ShowAct act in acts)
            {
                if (act != null && act.Crowd != null && Array.IndexOf(act.Crowd, archetype) >= 0)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
