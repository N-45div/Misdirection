using System;
using Misdirection.Core.Crowd;

namespace Misdirection.Core.Tricks
{
    public enum TrickPhase
    {
        /// <summary>Coin on the mat.</summary>
        Idle,
        /// <summary>Holding the coin; show it to the crowd.</summary>
        Showing,
        /// <summary>Coin shown. Misdirect, then ditch it in the servante.</summary>
        Misdirect,
        /// <summary>Coin ditched. Show the empty hand.</summary>
        Vanished,
        /// <summary>Vanish revealed. Optionally steal the coin back from the servante.</summary>
        AfterVanish,
        /// <summary>Coin secretly palmed. Look at a spectator and pluck it from behind their ear.</summary>
        Loaded,
        /// <summary>Trick done; applause, then reset.</summary>
        Finale,
        /// <summary>Coin seen in plain view; the trick failed. Resets shortly.</summary>
        Busted,
    }

    public enum Reaction { None, Heckle, Gasp, Applause }

    /// <summary>The crowd's response to one reveal.</summary>
    [Serializable]
    public struct RevealResult
    {
        public string Kind;
        public float Amazement;
        public int Tips;
        public Reaction Reaction;
        /// <summary>Spectator who saw the method (heckler), or -1.</summary>
        public int SeenBy;
        /// <summary>For a production: the spectator the coin came from.</summary>
        public int Spectator;
    }

    /// <summary>What the scene measures each frame for the trick.</summary>
    public struct TrickInput
    {
        /// <summary>Height of the coin-holding hand above the table (metres); NaN when nobody holds it.</summary>
        public float HolderHeight;
        /// <summary>The hand that last held the coin is open, raised and turned toward the crowd.</summary>
        public bool EmptyHandShown;
        /// <summary>Spectator the magician is looking at (-1 for none) and for how long.</summary>
        public int LookedAt;
        public double LookDwell;
        /// <summary>The hand holding the palmed coin is making a raised pinch (the "pluck").</summary>
        public bool ProducePinch;
        public int Spectators;
        public float AverageSuspicion;
    }

    [Serializable]
    public sealed class VanishTrickSettings
    {
        public float ShowHeight = 0.15f;
        public double ShowSeconds = 0.6;
        public double RevealHoldSeconds = 0.4;
        public double ProduceDwellSeconds = 0.4;
        public double FinaleSeconds = 3.0;
        public double BustedSeconds = 2.5;
        public int TipsPerSpectator = 5;
        /// <summary>A method seen at least this well gets heckled ("I saw that!").</summary>
        public float HeckleCoverage = 0.6f;
        public float ApplauseAt = 0.75f;
        public float GaspAt = 0.45f;
        /// <summary>Extra amazement for producing the coin from a spectator rather than from the hand.</summary>
        public float ProductionBonus = 0.2f;
    }

    /// <summary>
    /// Trick 1: vanish, then produce. Show the coin, misdirect, ditch it in the servante, show the
    /// empty hand (reveal 1); steal it back and pluck it from behind a spectator's ear while
    /// looking at them (reveal 2). Amazement falls when the crowd saw a method or is already
    /// suspicious. Pure logic: the scene reports events and per-frame input.
    /// </summary>
    public sealed class VanishTrick
    {
        private readonly VanishTrickSettings settings;
        private double shownSince = -1;
        private double revealSince = -1;
        private WitnessReport vanishWitness;
        private WitnessReport stealWitness;

        public VanishTrick(VanishTrickSettings settings = null)
        {
            this.settings = settings ?? new VanishTrickSettings();
            Reset(0);
        }

        public TrickPhase Phase { get; private set; }
        public double PhaseSince { get; private set; }
        public HandId Holder { get; private set; }
        /// <summary>The hand that held the coin most recently (the one that must look empty).</summary>
        public HandId LastHolder { get; private set; }
        public int TotalTips { get; private set; }
        public RevealResult LastReveal { get; private set; }
        public string BustReason { get; private set; }

        /// <summary>The coin is in the servante and may be stolen back.</summary>
        public bool CanSteal { get { return Phase == TrickPhase.AfterVanish; } }

        public string Prompt
        {
            get
            {
                switch (Phase)
                {
                    case TrickPhase.Idle: return "Pick up the coin.";
                    case TrickPhase.Showing: return "Hold it up so they can see it.";
                    case TrickPhase.Misdirect: return "Make them look away, then ditch it at the edge.";
                    case TrickPhase.Vanished: return "Show them your empty hand.";
                    case TrickPhase.AfterVanish: return "Encore: steal it back from the edge.";
                    case TrickPhase.Loaded: return "Look at someone and pluck it from behind their ear.";
                    case TrickPhase.Finale: return "Take a bow.";
                    default: return BustReason ?? "They saw it.";
                }
            }
        }

        public void PickedUp(double time, HandId hand)
        {
            if (Phase != TrickPhase.Idle)
            {
                return;
            }
            Holder = hand;
            LastHolder = hand;
            SetPhase(time, TrickPhase.Showing);
        }

        /// <summary>The holding hand let go. <paramref name="witness"/> is the crowd's view of that moment.</summary>
        public void Released(double time, HandId hand, bool insideServante, WitnessReport witness)
        {
            if (hand != Holder || Holder == HandId.None)
            {
                return;
            }
            Holder = HandId.None;
            switch (Phase)
            {
                case TrickPhase.Showing:
                case TrickPhase.Misdirect:
                case TrickPhase.Loaded:
                    if (!insideServante)
                    {
                        Bust(time, "The coin fell in plain view.");
                    }
                    else if (Phase == TrickPhase.Loaded)
                    {
                        // Put it back: the encore is still available.
                        SetPhase(time, TrickPhase.AfterVanish);
                    }
                    else if (Phase == TrickPhase.Showing)
                    {
                        Bust(time, "You ditched it before they saw it.");
                    }
                    else
                    {
                        vanishWitness = witness;
                        SetPhase(time, TrickPhase.Vanished);
                    }
                    break;
            }
        }

        /// <summary>The coin was secretly taken back out of the servante.</summary>
        public bool Stolen(double time, HandId hand, WitnessReport witness)
        {
            if (!CanSteal || hand == HandId.None)
            {
                return false;
            }
            Holder = hand;
            LastHolder = hand;
            stealWitness = witness;
            SetPhase(time, TrickPhase.Loaded);
            return true;
        }

        /// <summary>Advance one frame. Returns a reveal on the frame one happens.</summary>
        public RevealResult? Tick(double time, TrickInput input)
        {
            switch (Phase)
            {
                case TrickPhase.Showing:
                    if (!float.IsNaN(input.HolderHeight) && input.HolderHeight >= settings.ShowHeight)
                    {
                        if (shownSince < 0)
                        {
                            shownSince = time;
                        }
                        if (time - shownSince >= settings.ShowSeconds - 1e-9)
                        {
                            SetPhase(time, TrickPhase.Misdirect);
                        }
                    }
                    else
                    {
                        shownSince = -1;
                    }
                    break;

                case TrickPhase.Vanished:
                    if (input.EmptyHandShown)
                    {
                        if (revealSince < 0)
                        {
                            revealSince = time;
                        }
                        if (time - revealSince >= settings.RevealHoldSeconds - 1e-9)
                        {
                            RevealResult vanish = Score("vanish", vanishWitness, input, 0f, -1);
                            SetPhase(time, TrickPhase.AfterVanish);
                            return Commit(vanish);
                        }
                    }
                    else
                    {
                        revealSince = -1;
                    }
                    break;

                case TrickPhase.Loaded:
                    if (input.ProducePinch && input.LookedAt >= 0 && input.LookDwell >= settings.ProduceDwellSeconds - 1e-9)
                    {
                        RevealResult produce = Score("produce", stealWitness, input, settings.ProductionBonus, input.LookedAt);
                        SetPhase(time, TrickPhase.Finale);
                        return Commit(produce);
                    }
                    break;

                case TrickPhase.Finale:
                    if (time - PhaseSince >= settings.FinaleSeconds)
                    {
                        Reset(time);
                    }
                    break;

                case TrickPhase.Busted:
                    if (time - PhaseSince >= settings.BustedSeconds)
                    {
                        Reset(time);
                    }
                    break;
            }
            return null;
        }

        /// <summary>Amazement from how cleanly the method stayed out of every cone and how suspicious the crowd already is.</summary>
        public RevealResult Score(string kind, WitnessReport method, TrickInput input, float bonus, int spectator)
        {
            return RevealScoring.Score(kind, method, input, bonus, spectator, settings.HeckleCoverage, settings.ApplauseAt,
                                       settings.GaspAt, settings.TipsPerSpectator, settings.ProductionBonus);
        }

        public void Reset(double time)
        {
            Holder = HandId.None;
            vanishWitness = new WitnessReport { SeenBy = -1 };
            stealWitness = new WitnessReport { SeenBy = -1 };
            BustReason = null;
            SetPhase(time, TrickPhase.Idle);
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
            SetPhase(time, TrickPhase.Busted);
        }

        private void SetPhase(double time, TrickPhase phase)
        {
            Phase = phase;
            PhaseSince = time;
            shownSince = -1;
            revealSince = -1;
        }
    }

    /// <summary>How every trick turns a reveal into amazement, a crowd reaction and tips.</summary>
    public static class RevealScoring
    {
        /// <summary>
        /// Amazement = how cleanly the method stayed out of every cone × how much the crowd still trusts you
        /// (half off at full suspicion), plus a bonus for a clean production. A method seen at least
        /// <paramref name="heckleCoverage"/> well is heckled and earns nothing.
        /// </summary>
        public static RevealResult Score(string kind, WitnessReport method, TrickInput input, float bonus, int spectator,
                                         float heckleCoverage, float applauseAt, float gaspAt, int tipsPerSpectator, float maxBonus)
        {
            RevealResult r = new RevealResult();
            r.Kind = kind;
            r.Spectator = spectator;
            r.SeenBy = method.MaxCoverage >= heckleCoverage ? method.SeenBy : -1;
            float clean = 1f - Clamp01(method.MaxCoverage);
            float doubt = 1f - 0.5f * Clamp01(input.AverageSuspicion);
            r.Amazement = Math.Min(1f + maxBonus, Math.Max(0f, clean * doubt + (clean > 0.5f ? bonus : 0f)));
            float applause = applauseAt;
            float gasp = gaspAt;
            int perSpectator = tipsPerSpectator;
            if (r.SeenBy >= 0)
            {
                r.Reaction = Reaction.Heckle;
            }
            else if (r.Amazement >= applause)
            {
                r.Reaction = Reaction.Applause;
            }
            else if (r.Amazement >= gasp)
            {
                r.Reaction = Reaction.Gasp;
            }
            else
            {
                r.Reaction = Reaction.Heckle;
            }
            r.Tips = r.Reaction == Reaction.Heckle ? 0 : (int)Math.Round(r.Amazement * perSpectator * Math.Max(1, input.Spectators));
            return r;
        }

        private static float Clamp01(float v)
        {
            return Math.Max(0f, Math.Min(1f, v));
        }
    }
}
