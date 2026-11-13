using System.Collections.Generic;
using System.Text;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;

namespace Misdirection.Core.Progress
{
    /// <summary>
    /// The short lines the player reads: the day banner at the start of the first act and the trick
    /// book at the bow. Plain ASCII only (the default TextMesh Pro font has no ticks or stars).
    /// </summary>
    public static class ProgressText
    {
        /// <summary>"Day 2".</summary>
        public static string DayLine(int dayNumber)
        {
            return "Day " + (dayNumber < 1 ? 1 : dayNumber);
        }

        /// <summary>
        /// Who is in today's crowd: act 1's people, then who joins each later act.
        /// "Kid, Tourist, Skeptic + Filmer + Regular".
        /// </summary>
        public static string CrowdLine(ShowAct[] acts)
        {
            StringBuilder sb = new StringBuilder();
            List<Archetype> here = new List<Archetype>();
            if (acts == null)
            {
                return "";
            }
            for (int i = 0; i < acts.Length; i++)
            {
                Archetype[] crowd = acts[i] != null && acts[i].Crowd != null ? acts[i].Crowd : new Archetype[0];
                List<Archetype> before = new List<Archetype>(here);
                foreach (Archetype a in crowd)
                {
                    if (before.Remove(a))
                    {
                        continue; // already standing there
                    }
                    if (sb.Length > 0)
                    {
                        sb.Append(i > 0 ? " + " : ", ");
                    }
                    sb.Append(a);
                }
                here = new List<Archetype>(crowd);
            }
            return sb.ToString();
        }

        /// <summary>"Day 3 in a row", or on the first day of a streak "Day 1: come back tomorrow".</summary>
        public static string StreakLine(int streak)
        {
            return streak >= 2 ? "Day " + streak + " in a row" : "Day 1: come back tomorrow";
        }

        /// <summary>"Challenge: No busts all show - OK" (or "- not yet").</summary>
        public static string ChallengeLine(DailyChallenge challenge, bool passed)
        {
            return "Challenge: " + (challenge != null ? challenge.Text : "none today") + (passed ? " - OK" : " - not yet");
        }

        /// <summary>"Challenge: OK" or "Challenge: not yet" (words, not a tick: it reads without colour too).</summary>
        public static string ChallengeVerdict(bool passed)
        {
            return passed ? "Challenge: OK" : "Challenge: not yet";
        }

        /// <summary>"Next: applause on every reveal", or "Mastered" when the trick has all its stars.</summary>
        public static string NextStarLine(TrickMastery mastery)
        {
            string hint = TrickBook.NextStarHint(mastery);
            return hint.Length > 0 ? "Next: " + hint : "Mastered";
        }

        /// <summary>"Best: 96 tips", with "(new!)" when this show set it.</summary>
        public static string BestLine(int bestTips, bool isNew)
        {
            return "Best: " + bestTips + " tips" + (isNew ? " (new!)" : "");
        }

        /// <summary>What tomorrow brings: after the first day played, the Regular's first visit; later, a new crowd.</summary>
        public static string TomorrowLine(bool regularAlreadyComes)
        {
            return regularAlreadyComes ? "Tomorrow: a new crowd, a new challenge" : "Tomorrow: the Regular comes to watch";
        }
    }
}
