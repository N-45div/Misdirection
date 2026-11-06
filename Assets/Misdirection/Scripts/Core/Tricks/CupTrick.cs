using System;
using Misdirection.Core.Crowd;

namespace Misdirection.Core.Tricks
{
    public enum CupPhase
    {
        /// <summary>Coin on the mat, cup mouth-down beside it.</summary>
        Idle,
        /// <summary>Holding the coin; show it.</summary>
        Showing,
        /// <summary>Coin shown. Misdirect, then slip it under the cup's rim.</summary>
        Misdirect,
        /// <summary>Coin secretly under the cup. Show your hands empty.</summary>
        Loaded,
        /// <summary>Hands shown empty. Lift the cup.</summary>
        Vanished,
        /// <summary>Coin revealed under the cup; applause, then reset.</summary>
        Finale,
        /// <summary>The method was exposed. Resets shortly.</summary>
        Busted,
    }

    /// <summary>Where the coin-holding hand let go.</summary>
    public enum ReleaseZone { Table, Servante, UnderCup }

    [Serializable]
    public sealed class CupTrickSettings
    {
        public float ShowHeight = 0.15f;
        public double ShowSeconds = 0.6;
        public double RevealHoldSeconds = 0.4;
        /// <summary>Lifting the cup this high counts as the reveal (metres).</summary>
        public float LiftHeight = 0.08f;
        public double FinaleSeconds = 3.0;
        public double BustedSeconds = 2.5;
        public int TipsPerSpectator = 5;
        public float HeckleCoverage = 0.6f;
        public float ApplauseAt = 0.75f;
        public float GaspAt = 0.45f;
        /// <summary>The coin turning up under a cup they watched is worth more than a plain vanish.</summary>
        public float ProductionBonus = 0.2f;
    }

    /// <summary>
    /// Trick 2 (Day 6, first cut candidate): the coin under the cup. Show the coin, misdirect, slip it
    /// under the cup's rim (the load), show both hands empty (reveal 1: vanish), lift the cup
    /// (reveal 2: it's there). Both reveals are scored by how well the crowd saw the load.
    /// Pure logic; the scene reports events and per-frame input.
    /// </summary>
    public sealed class CupTrick
    {
        private readonly CupTrickSettings settings;
        private double shownSince = -1;
        private double revealSince = -1;
        private WitnessReport loadWitness;
        private bool vanishRevealed;

        public CupTrick(CupTrickSettings settings = null)
        {
            this.settings = settings ?? new CupTrickSettings();
            Reset(0);
        }

        public CupPhase Phase { get; private set; }
        public double PhaseSince { get; private set; }
        public HandId Holder { get; private set; }
        public HandId LastHolder { get; private set; }
        public int TotalTips { get; private set; }
        public RevealResult LastReveal { get; private set; }
        public string BustReason { get; private set; }

        /// <summary>True while the coin is in the secret hand and must stay hidden.</summary>
        public bool SecretInPlay { get { return Phase == CupPhase.Misdirect; } }

        /// <summary>The cup may be lifted for the reveal.</summary>
        public bool CupLiftable { get { return Phase == CupPhase.Loaded || Phase == CupPhase.Vanished; } }

        public string Prompt
        {
            get
            {
                switch (Phase)
                {
                    case CupPhase.Idle: return "Pick up the coin.";
                    case CupPhase.Showing: return "Hold it up so they can see it.";
                    case CupPhase.Misdirect: return "Make them look away, then slip it under the cup's rim.";
                    case CupPhase.Loaded: return "Show them your empty hands.";
                    case CupPhase.Vanished: return "Lift the cup.";
                    case CupPhase.Finale: return "Take a bow.";
                    default: return BustReason ?? "They saw it.";
                }
            }
        }

        public void PickedUp(double time, HandId hand)
        {
            if (Phase != CupPhase.Idle || hand == HandId.None)
            {
                return;
            }
            Holder = hand;
            LastHolder = hand;
            SetPhase(time, CupPhase.Showing);
        }

        /// <summary>The coin-holding hand let go in <paramref name="zone"/>; <paramref name="witness"/> is how well the crowd saw it.</summary>
        public void Released(double time, HandId hand, ReleaseZone zone, WitnessReport witness)
        {
            if (hand != Holder || Holder == HandId.None)
            {
                return;
            }
            Holder = HandId.None;
            if (Phase == CupPhase.Showing)
            {
                Bust(time, zone == ReleaseZone.Table ? "The coin fell in plain view." : "You hid it before they saw it.");
                return;
            }
            if (Phase != CupPhase.Misdirect)
            {
                return;
            }
            switch (zone)
            {
                case ReleaseZone.UnderCup:
                    loadWitness = witness;
                    SetPhase(time, CupPhase.Loaded);
                    break;
                case ReleaseZone.Servante:
                    Bust(time, "Wrong trick: it has to end up under the cup.");
                    break;
                default:
                    Bust(time, "The coin fell in plain view.");
                    break;
            }
        }

        /// <summary>Advance one frame. Returns a reveal on the frame one happens.</summary>
        /// <param name="cupLift">How high the cup is lifted off the mat (metres); 0 when resting.</param>
        public RevealResult? Tick(double time, TrickInput input, float cupLift)
        {
            switch (Phase)
            {
                case CupPhase.Showing:
                    if (!float.IsNaN(input.HolderHeight) && input.HolderHeight >= settings.ShowHeight)
                    {
                        if (shownSince < 0)
                        {
                            shownSince = time;
                        }
                        if (time - shownSince >= settings.ShowSeconds - 1e-9)
                        {
                            SetPhase(time, CupPhase.Misdirect);
                        }
                    }
                    else
                    {
                        shownSince = -1;
                    }
                    break;

                case CupPhase.Loaded:
                    if (cupLift >= settings.LiftHeight)
                    {
                        // Skipped the vanish: straight to the production.
                        return Produce(time, input);
                    }
                    if (input.EmptyHandShown)
                    {
                        if (revealSince < 0)
                        {
                            revealSince = time;
                        }
                        if (time - revealSince >= settings.RevealHoldSeconds - 1e-9)
                        {
                            RevealResult vanish = Score("vanish", input, 0f, -1);
                            vanishRevealed = true;
                            SetPhase(time, CupPhase.Vanished);
                            return Commit(vanish);
                        }
                    }
                    else
                    {
                        revealSince = -1;
                    }
                    break;

                case CupPhase.Vanished:
                    if (cupLift >= settings.LiftHeight)
                    {
                        return Produce(time, input);
                    }
                    break;

                case CupPhase.Finale:
                    if (time - PhaseSince >= settings.FinaleSeconds)
                    {
                        Reset(time);
                    }
                    break;

                case CupPhase.Busted:
                    if (time - PhaseSince >= settings.BustedSeconds)
                    {
                        Reset(time);
                    }
                    break;
            }
            return null;
        }

        public void Reset(double time)
        {
            Holder = HandId.None;
            loadWitness = new WitnessReport { SeenBy = -1 };
            vanishRevealed = false;
            BustReason = null;
            SetPhase(time, CupPhase.Idle);
        }

        private RevealResult? Produce(double time, TrickInput input)
        {
            // The full effect (vanish then reappearance) earns the production bonus; a bare lift doesn't.
            RevealResult produce = Score("cup", input, vanishRevealed ? settings.ProductionBonus : 0f, -1);
            SetPhase(time, CupPhase.Finale);
            return Commit(produce);
        }

        private RevealResult Score(string kind, TrickInput input, float bonus, int spectator)
        {
            return RevealScoring.Score(kind, loadWitness, input, bonus, spectator, settings.HeckleCoverage, settings.ApplauseAt,
                                       settings.GaspAt, settings.TipsPerSpectator, settings.ProductionBonus);
        }

        private RevealResult? Commit(RevealResult r)
        {
            TotalTips += r.Tips;
            LastReveal = r;
            return r;
        }

        private void Bust(double time, string reason)
        {
            BustReason = reason;
            SetPhase(time, CupPhase.Busted);
        }

        private void SetPhase(double time, CupPhase phase)
        {
            Phase = phase;
            PhaseSince = time;
            shownSince = -1;
            revealSince = -1;
        }
    }
}
