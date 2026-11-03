using System;
using System.Collections.Generic;

namespace Misdirection.Core.Tricks
{
    public enum CoinState
    {
        /// <summary>Lying on the close-up mat, ready to be picked up.</summary>
        Resting,
        /// <summary>Held in a hand (pinched or palmed).</summary>
        Held,
        /// <summary>Dropped into the servante: gone from the crowd's view. The method worked.</summary>
        Ditched,
        /// <summary>Released outside the servante and lying on the table in plain view.</summary>
        Exposed,
    }

    public enum HandId { None, Left, Right }

    /// <summary>The coin's life cycle for the Day 2 secret-hand verbs. Pure state; the Unity side moves the visuals.</summary>
    public sealed class CoinModel
    {
        public CoinState State { get; private set; }
        public HandId Holder { get; private set; }

        /// <summary>True when a hand may pick the coin up now.</summary>
        public bool Grabbable { get { return State == CoinState.Resting || State == CoinState.Exposed; } }

        public bool TryGrab(HandId hand)
        {
            if (!Grabbable || hand == HandId.None)
            {
                return false;
            }
            State = CoinState.Held;
            Holder = hand;
            return true;
        }

        /// <summary>Secretly take the coin back out of the servante (a steal). Returns false unless it was ditched.</summary>
        public bool TrySteal(HandId hand)
        {
            if (State != CoinState.Ditched || hand == HandId.None)
            {
                return false;
            }
            State = CoinState.Held;
            Holder = hand;
            return true;
        }

        /// <summary>Release from <paramref name="hand"/>. Returns the new state (unchanged if that hand wasn't holding).</summary>
        public CoinState Release(HandId hand, bool insideServante)
        {
            if (State != CoinState.Held || hand != Holder)
            {
                return State;
            }
            Holder = HandId.None;
            State = insideServante ? CoinState.Ditched : CoinState.Exposed;
            return State;
        }

        public void Reset()
        {
            State = CoinState.Resting;
            Holder = HandId.None;
        }
    }

    public enum TrialOutcome { Ditched, Exposed, Abandoned }

    /// <summary>One attempt: pick the coin up, carry it to the servante, open the hand.</summary>
    [Serializable]
    public struct DitchTrial
    {
        public int Number;
        public HandId Hand;
        public double GrabTime;
        public double EndTime;
        public TrialOutcome Outcome;
        /// <summary>Distance from the hand to the servante at release (0 = inside), metres.</summary>
        public float ReleaseDistance;
        /// <summary>Seconds the holding hand was lost (untracked) while holding.</summary>
        public double LostSeconds;
        /// <summary>Seconds the holding hand was tracked but low confidence while holding.</summary>
        public double UncertainSeconds;

        public double Duration { get { return EndTime - GrabTime; } }

        /// <summary>A release far from the servante almost certainly wasn't intended: the grip let go by mistake.</summary>
        public bool LikelyAccidental(float threshold = 0.15f)
        {
            return Outcome == TrialOutcome.Exposed && ReleaseDistance > threshold;
        }
    }

    /// <summary>
    /// Records ditch attempts and reports the Day 2 exit number: successes in the last 20 attempts
    /// (target: about 19 of 20). Mirrors tools/analyze_ditch_trials.py.
    /// </summary>
    public sealed class DitchTrialRecorder
    {
        public const int Window = 20;

        private readonly List<DitchTrial> trials = new List<DitchTrial>();
        private bool active;
        private DitchTrial current;
        private double lastTime = double.NaN;

        public IReadOnlyList<DitchTrial> Trials { get { return trials; } }

        public bool Active { get { return active; } }

        public void Begin(double time, HandId hand)
        {
            current = new DitchTrial();
            current.Number = trials.Count + 1;
            current.Hand = hand;
            current.GrabTime = time;
            active = true;
            lastTime = time;
        }

        /// <summary>Call every frame while holding, with the holding hand's presence.</summary>
        public void Tick(double time, bool lost, bool uncertain)
        {
            if (!active)
            {
                return;
            }
            double dt = double.IsNaN(lastTime) ? 0 : Math.Max(0, time - lastTime);
            lastTime = time;
            if (lost)
            {
                current.LostSeconds += dt;
            }
            else if (uncertain)
            {
                current.UncertainSeconds += dt;
            }
        }

        public DitchTrial End(double time, TrialOutcome outcome, float releaseDistance)
        {
            current.EndTime = time;
            current.Outcome = outcome;
            current.ReleaseDistance = releaseDistance;
            active = false;
            trials.Add(current);
            return current;
        }

        public int Count { get { return trials.Count; } }

        public int Successes { get { return CountWhere(trials.Count, TrialOutcome.Ditched); } }

        /// <summary>(successes, attempts) over the last <see cref="Window"/> finished attempts, ignoring abandoned ones.</summary>
        public void Recent(out int successes, out int attempts)
        {
            successes = 0;
            attempts = 0;
            for (int i = trials.Count - 1; i >= 0 && attempts < Window; i--)
            {
                if (trials[i].Outcome == TrialOutcome.Abandoned)
                {
                    continue;
                }
                attempts++;
                if (trials[i].Outcome == TrialOutcome.Ditched)
                {
                    successes++;
                }
            }
        }

        /// <summary>Day 2 exit check: at least 19 of the last 20 attempts ditched.</summary>
        public bool MeetsExitCriterion(int needed = 19)
        {
            int s;
            int a;
            Recent(out s, out a);
            return a >= Window && s >= needed;
        }

        private int CountWhere(int last, TrialOutcome outcome)
        {
            int n = 0;
            for (int i = Math.Max(0, trials.Count - last); i < trials.Count; i++)
            {
                if (trials[i].Outcome == outcome)
                {
                    n++;
                }
            }
            return n;
        }
    }
}
