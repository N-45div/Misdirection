using System;
using System.Collections.Generic;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;

namespace Misdirection.Core.Progress
{
    /// <summary>
    /// A small seeded random number generator (xorshift32). Ours rather than System.Random so the
    /// same seed gives the same show on every device and every .NET runtime.
    /// </summary>
    public sealed class SeededRandom
    {
        private uint state;

        public SeededRandom(uint seed)
        {
            state = seed != 0 ? seed : 0x9E3779B9u;
        }

        public uint NextUInt()
        {
            unchecked
            {
                uint x = state;
                x ^= x << 13;
                x ^= x >> 17;
                x ^= x << 5;
                state = x;
                return x;
            }
        }

        /// <summary>A whole number from 0 up to (not including) <paramref name="max"/>.</summary>
        public int Next(int max)
        {
            if (max <= 1)
            {
                return 0;
            }
            return (int)((NextUInt() >> 8) % (uint)max);
        }
    }

    /// <summary>Today's show: who is in each act and the day's challenge. The same date always gives the same plan.</summary>
    public sealed class DailyPlan
    {
        /// <summary>The calendar date (yyyymmdd) this plan is for.</summary>
        public int Date;
        public uint Seed;
        /// <summary>The player has played on an earlier day, so the Regular is back for the last act.</summary>
        public bool RegularReturns;
        /// <summary>Three acts of 3, 4 and 5 people; tricks vanish, cup, vanish.</summary>
        public ShowAct[] Acts;
        public DailyChallenge Challenge;
    }

    /// <summary>
    /// The daily-seeded crowd. Each calendar date has its own line-up, built so the show stays fair:
    ///  - Act 1 (3 people) is learnable: the Kid, someone easy, and at most one hard watcher.
    ///  - The Skeptic has arrived by act 2 (4 people).
    ///  - Nobody leaves: each act keeps the people already there and one more joins, so act 3
    ///    (5 people) is the hardest crowd of the day.
    ///  - The Regular only turns up once you have played on an earlier day; then he joins for act 3.
    /// At most two of any kind of spectator. Where each newcomer stands in the arc is seeded too.
    /// </summary>
    public static class DailyShow
    {
        /// <summary>Tricks per act: vanish and produce, coin under the cup, vanish and produce.</summary>
        public static readonly int[] ActTricks = { 0, 1, 0 };

        /// <summary>Never more than this many of one archetype in a day's crowd.</summary>
        public const int MaxOfAKind = 2;

        // Who can fill each open place, and how likely (relative weights).
        private static readonly Archetype[] EasyPool = { Archetype.Tourist, Archetype.Kid };
        private static readonly int[] EasyWeights = { 3, 1 };
        private static readonly Archetype[] ThirdPool = { Archetype.Tourist, Archetype.Skeptic, Archetype.Filmer };
        private static readonly int[] ThirdWeights = { 2, 2, 2 };
        private static readonly Archetype[] JoinPool = { Archetype.Filmer, Archetype.Tourist, Archetype.Skeptic, Archetype.Kid };
        private static readonly int[] Act2Weights = { 3, 2, 1, 1 };
        private static readonly int[] Act3Weights = { 3, 2, 2, 1 };

        /// <summary>A well-mixed seed for a calendar date (yyyymmdd), so neighbouring days share nothing.</summary>
        public static uint SeedFor(int date)
        {
            return Mix(unchecked((uint)date) ^ 0x4D495344u);
        }

        /// <summary>A 32-bit integer hash: every input bit changes about half the output bits.</summary>
        public static uint Mix(uint x)
        {
            unchecked
            {
                x ^= x >> 16;
                x *= 0x7FEB352Du;
                x ^= x >> 15;
                x *= 0x846CA68Bu;
                x ^= x >> 16;
                return x;
            }
        }

        /// <summary>
        /// The show for a date. <paramref name="regularReturns"/>: the player has played on an earlier
        /// day (see <see cref="ProgressRules.IsReturning"/>).
        /// </summary>
        public static DailyPlan Plan(int date, bool regularReturns)
        {
            uint seed = SeedFor(date);
            SeededRandom rng = new SeededRandom(seed);
            int[] counts = new int[Enum.GetValues(typeof(Archetype)).Length];

            // Act 1: the Kid, someone easy, and one more who may be the day's first hard watcher.
            counts[(int)Archetype.Kid]++;
            Archetype easy = Draw(rng, EasyPool, EasyWeights, counts);
            Archetype third = Draw(rng, ThirdPool, ThirdWeights, counts);
            List<Archetype> crowd = new List<Archetype>();
            crowd.Add(Archetype.Kid);
            crowd.Add(easy);
            crowd.Add(third);
            Shuffle(rng, crowd);
            Archetype[] act1 = crowd.ToArray();

            // Act 2: the Skeptic if she isn't here yet, else another draw.
            Archetype second = Draw(rng, JoinPool, Act2Weights, counts, crowd.Contains(Archetype.Skeptic) ? -1 : (int)Archetype.Skeptic);
            crowd.Insert(rng.Next(crowd.Count + 1), second);
            Archetype[] act2 = crowd.ToArray();

            // Act 3: the returning Regular, else one more draw. Always draw, so acts 1 and 2 of a date
            // are the same whether or not he comes.
            Archetype last = Draw(rng, JoinPool, Act3Weights, counts);
            int place = rng.Next(crowd.Count + 1);
            crowd.Insert(place, regularReturns ? Archetype.Regular : last);
            Archetype[] act3 = crowd.ToArray();

            DailyPlan plan = new DailyPlan();
            plan.Date = date;
            plan.Seed = seed;
            plan.RegularReturns = regularReturns;
            plan.Acts = new[]
            {
                new ShowAct(ActTricks[0], act1),
                new ShowAct(ActTricks[1], act2),
                new ShowAct(ActTricks[2], act3),
            };
            plan.Challenge = DailyChallenges.For(date, plan.Acts);
            return plan;
        }

        /// <summary>Show settings for a plan: the plan's acts, everything else from <paramref name="template"/> (or the defaults).</summary>
        public static ShowSettings ToSettings(DailyPlan plan, ShowSettings template = null)
        {
            ShowSettings settings = new ShowSettings();
            if (template != null)
            {
                settings.BustsToWalkOff = template.BustsToWalkOff;
                settings.OverSeconds = template.OverSeconds;
                settings.TutorialCrowd = template.TutorialCrowd;
            }
            if (plan != null && plan.Acts != null && plan.Acts.Length > 0)
            {
                settings.Acts = plan.Acts;
            }
            return settings;
        }

        /// <summary>How hard one kind of spectator is to fool, 1 (easy) to 3 (hard).</summary>
        public static int Difficulty(Archetype archetype)
        {
            switch (archetype)
            {
                case Archetype.Skeptic:
                case Archetype.Filmer: return 3;
                case Archetype.Regular: return 2;
                default: return 1;
            }
        }

        /// <summary>How hard a whole crowd is: the sum over its people.</summary>
        public static int Difficulty(Archetype[] crowd)
        {
            int sum = 0;
            if (crowd != null)
            {
                foreach (Archetype a in crowd)
                {
                    sum += Difficulty(a);
                }
            }
            return sum;
        }

        /// <summary>A weighted draw that respects <see cref="MaxOfAKind"/>; <paramref name="forced"/> (an archetype number) skips the draw.</summary>
        private static Archetype Draw(SeededRandom rng, Archetype[] pool, int[] weights, int[] counts, int forced = -1)
        {
            // Draw even when forced, so the random stream stays in step for the rest of the day's plan.
            int total = 0;
            for (int i = 0; i < pool.Length; i++)
            {
                total += counts[(int)pool[i]] < MaxOfAKind ? weights[i] : 0;
            }
            Archetype picked = pool[0];
            int roll = rng.Next(Math.Max(1, total));
            for (int i = 0; i < pool.Length; i++)
            {
                int weight = counts[(int)pool[i]] < MaxOfAKind ? weights[i] : 0;
                if (roll < weight)
                {
                    picked = pool[i];
                    break;
                }
                roll -= weight;
            }
            if (forced >= 0)
            {
                picked = (Archetype)forced;
            }
            counts[(int)picked]++;
            return picked;
        }

        private static void Shuffle(SeededRandom rng, List<Archetype> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                Archetype swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
