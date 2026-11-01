using System;
using System.Collections.Generic;

namespace Misdirection.Spike
{
    public enum HandSide { Left, Right }

    public enum TurnKind { Center, Away, Toward }

    /// <summary>One measured condition: which hand is secret and where the head is turned.</summary>
    [Serializable]
    public struct SpikeCondition
    {
        public int Index;
        public int Repeat;
        public HandSide SecretHand;
        public TurnKind Turn;
        public float AngleDeg;

        /// <summary>Target yaw relative to the table's forward direction. Positive is to the right.</summary>
        public float SignedTargetDeg
        {
            get
            {
                if (Turn == TurnKind.Center)
                {
                    return 0f;
                }
                // Turning "away" from the left hand means turning right, and vice versa.
                bool turnRight = (SecretHand == HandSide.Left) == (Turn == TurnKind.Away);
                return turnRight ? AngleDeg : -AngleDeg;
            }
        }

        public string SecretCode { get { return SecretHand == HandSide.Left ? "L" : "R"; } }

        public string TurnCode
        {
            get
            {
                switch (Turn)
                {
                    case TurnKind.Away: return "away";
                    case TurnKind.Toward: return "toward";
                    default: return "center";
                }
            }
        }
    }

    /// <summary>
    /// Splits a record phase into fixed "move windows" (roughly one secret move each).
    /// A window fails when the secret hand stays below high confidence for at least
    /// <c>dropoutSeconds</c> inside it. Mirrors tools/analyze_tracking_spike.py; keep them in sync.
    /// </summary>
    public sealed class MoveWindowScorer
    {
        private readonly double windowSeconds;
        private readonly double dropoutSeconds;

        private double start = double.NaN;
        private int windowIndex = -1;
        private double windowFirst;
        private double windowLast;
        private bool windowFailed;
        private double lowRunStart = -1.0;

        public int Frames { get; private set; }
        public int TrackedFrames { get; private set; }
        public int HighFrames { get; private set; }
        public int Windows { get; private set; }
        public int FailedWindows { get; private set; }
        public double LongestLowRun { get; private set; }

        public MoveWindowScorer(double windowSeconds, double dropoutSeconds)
        {
            this.windowSeconds = Math.Max(0.1, windowSeconds);
            this.dropoutSeconds = Math.Max(0.0, dropoutSeconds);
        }

        public void Add(double time, bool tracked, bool highConfidence)
        {
            if (double.IsNaN(start))
            {
                start = time;
            }

            int index = (int)Math.Floor((time - start) / windowSeconds + 1e-9);
            if (index != windowIndex)
            {
                CloseWindow();
                windowIndex = index;
                windowFirst = time;
                windowFailed = false;
            }
            windowLast = time;

            Frames++;
            if (tracked)
            {
                TrackedFrames++;
            }

            bool high = tracked && highConfidence;
            if (high)
            {
                HighFrames++;
                lowRunStart = -1.0;
                return;
            }

            if (lowRunStart < 0.0)
            {
                lowRunStart = time;
            }
            double run = time - lowRunStart;
            if (run > LongestLowRun)
            {
                LongestLowRun = run;
            }
            if (run >= dropoutSeconds - 1e-9)
            {
                windowFailed = true;
            }
        }

        public void Finish()
        {
            CloseWindow();
            windowIndex = -1;
        }

        private void CloseWindow()
        {
            if (windowIndex < 0)
            {
                return;
            }
            // Ignore stub windows (e.g. a single frame landing exactly on the boundary).
            if (windowLast - windowFirst >= 0.5 * windowSeconds - 0.05)
            {
                Windows++;
                if (windowFailed)
                {
                    FailedWindows++;
                }
            }
            windowIndex = -1;
        }
    }

    /// <summary>Aggregated result for one (secret hand, turn, angle) cell across repeats.</summary>
    public sealed class SpikeCell
    {
        public int Frames;
        public int TrackedFrames;
        public int HighFrames;
        public int Windows;
        public int FailedWindows;
        public double LongestLowRun;

        public float FailRate { get { return Windows == 0 ? 1f : (float)FailedWindows / Windows; } }
        public float HighRate { get { return Frames == 0 ? 0f : (float)HighFrames / Frames; } }

        public void Add(MoveWindowScorer s)
        {
            Frames += s.Frames;
            TrackedFrames += s.TrackedFrames;
            HighFrames += s.HighFrames;
            Windows += s.Windows;
            FailedWindows += s.FailedWindows;
            LongestLowRun = Math.Max(LongestLowRun, s.LongestLowRun);
        }
    }

    public static class SpikeScoring
    {
        public static string Key(HandSide hand, TurnKind turn, float angle)
        {
            return (hand == HandSide.Left ? "L" : "R") + "|" + turn + "|" + Math.Round(angle);
        }

        /// <summary>
        /// Largest angle A such that every measured angle up to A passes for both hands
        /// (fail rate at or below <paramref name="maxFailRate"/>). Angle 0 uses the centre condition.
        /// Returns -1 when even the centre condition fails or is missing.
        /// </summary>
        public static float UsableAngle(IDictionary<string, SpikeCell> cells, IList<float> angles, TurnKind turn, float maxFailRate)
        {
            List<float> sorted = new List<float>(angles);
            sorted.Sort();
            float usable = -1f;
            foreach (float angle in sorted)
            {
                TurnKind kind = angle < 0.5f ? TurnKind.Center : turn;
                bool pass = true;
                foreach (HandSide hand in new[] { HandSide.Left, HandSide.Right })
                {
                    SpikeCell cell;
                    if (!cells.TryGetValue(Key(hand, kind, angle), out cell) || cell.Windows == 0 || cell.FailRate > maxFailRate + 1e-6f)
                    {
                        pass = false;
                        break;
                    }
                }
                if (!pass)
                {
                    break;
                }
                usable = angle;
            }
            return usable;
        }
    }
}
