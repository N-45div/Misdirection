// Unit tests for Trick 1 (vanish + produce) in Misdirection.Core.Tricks.
using System;
using Misdirection.Core.Crowd;
using Misdirection.Core.Tricks;

public static class TrickSim
{
    public const double Dt = 1.0 / 72.0;

    public static WitnessReport Unseen { get { return new WitnessReport { MaxCoverage = 0f, SeenBy = -1 }; } }

    public static WitnessReport Seen(float coverage, int by)
    {
        return new WitnessReport { MaxCoverage = coverage, SeenBy = by };
    }

    public static TrickInput Input(float height = float.NaN, bool emptyHand = false, int lookedAt = -1, double dwell = 0, bool pinch = false, float suspicion = 0f)
    {
        return new TrickInput
        {
            HolderHeight = height,
            EmptyHandShown = emptyHand,
            LookedAt = lookedAt,
            LookDwell = dwell,
            ProducePinch = pinch,
            Spectators = 3,
            AverageSuspicion = suspicion,
        };
    }

    /// <summary>Ticks with the same input; returns the last reveal (if any) and advances time.</summary>
    public static RevealResult? Run(VanishTrick trick, ref double t, double seconds, TrickInput input)
    {
        RevealResult? reveal = null;
        int frames = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
        {
            t += Dt;
            RevealResult? r = trick.Tick(t, input);
            if (r.HasValue) reveal = r;
        }
        return reveal;
    }

    /// <summary>Picks up, shows and ditches the coin; leaves the trick in Vanished.</summary>
    public static VanishTrick Ditched(ref double t, WitnessReport witness)
    {
        var trick = new VanishTrick();
        trick.PickedUp(t, HandId.Right);
        Run(trick, ref t, 0.8, Input(height: 0.25f));
        Assert.Equal(TrickPhase.Misdirect, trick.Phase, "shown");
        trick.Released(t, HandId.Right, true, witness);
        Assert.Equal(TrickPhase.Vanished, trick.Phase, "ditched");
        return trick;
    }
}

public static class VanishTrickTests
{
    [Test]
    public static void FullTrickCleanGetsApplauseAndTips()
    {
        double t = 0;
        VanishTrick trick = TrickSim.Ditched(ref t, TrickSim.Unseen);
        RevealResult? vanish = TrickSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: true));
        Assert.True(vanish.HasValue, "vanish revealed");
        Assert.Equal("vanish", vanish.Value.Kind);
        Assert.Equal(Reaction.Applause, vanish.Value.Reaction);
        Assert.Near(1.0, vanish.Value.Amazement, 1e-6, "clean vanish");
        Assert.Equal(15, vanish.Value.Tips, "5 per spectator");
        Assert.Equal(TrickPhase.AfterVanish, trick.Phase);

        Assert.True(trick.Stolen(t, HandId.Left, TrickSim.Unseen), "steal from the servante");
        Assert.Equal(TrickPhase.Loaded, trick.Phase);
        RevealResult? produce = TrickSim.Run(trick, ref t, 0.6, TrickSim.Input(lookedAt: 1, dwell: 0.6, pinch: true));
        Assert.True(produce.HasValue, "produced");
        Assert.Equal(1, produce.Value.Spectator, "from the kid's ear");
        Assert.Near(1.2, produce.Value.Amazement, 1e-6, "production bonus");
        Assert.Equal(18, produce.Value.Tips);
        Assert.Equal(TrickPhase.Finale, trick.Phase);
        Assert.Equal(33, trick.TotalTips, "tips add up");

        TrickSim.Run(trick, ref t, 3.2, TrickSim.Input());
        Assert.Equal(TrickPhase.Idle, trick.Phase, "resets after the bow");
    }

    [Test]
    public static void ShowingNeedsTheCoinHeldUpForAMoment()
    {
        double t = 0;
        var trick = new VanishTrick();
        trick.PickedUp(t, HandId.Right);
        TrickSim.Run(trick, ref t, 2.0, TrickSim.Input(height: 0.05f));
        Assert.Equal(TrickPhase.Showing, trick.Phase, "too low");
        TrickSim.Run(trick, ref t, 0.3, TrickSim.Input(height: 0.25f));
        Assert.Equal(TrickPhase.Showing, trick.Phase, "too brief");
        TrickSim.Run(trick, ref t, 0.4, TrickSim.Input(height: 0.25f));
        Assert.Equal(TrickPhase.Misdirect, trick.Phase);
    }

    [Test]
    public static void SeenDitchIsHeckled()
    {
        double t = 0;
        VanishTrick trick = TrickSim.Ditched(ref t, TrickSim.Seen(0.8f, 2));
        RevealResult? r = TrickSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: true));
        Assert.Equal(Reaction.Heckle, r.Value.Reaction);
        Assert.Equal(2, r.Value.SeenBy, "the skeptic saw it");
        Assert.Equal(0, r.Value.Tips);
    }

    [Test]
    public static void GlimpsedDitchAndSuspiciousCrowdGetAGasp()
    {
        double t = 0;
        VanishTrick trick = TrickSim.Ditched(ref t, TrickSim.Seen(0.3f, 0));
        RevealResult? r = TrickSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: true, suspicion: 0.4f));
        Assert.Near(0.56, r.Value.Amazement, 1e-5, "0.7 clean x 0.8 trust");
        Assert.Equal(Reaction.Gasp, r.Value.Reaction);
        Assert.Equal(-1, r.Value.SeenBy, "a glimpse isn't a heckle");
        Assert.Equal(8, r.Value.Tips);
    }

    [Test]
    public static void DroppingInPlainViewBustsThenResets()
    {
        double t = 0;
        var trick = new VanishTrick();
        trick.PickedUp(t, HandId.Left);
        TrickSim.Run(trick, ref t, 0.8, TrickSim.Input(height: 0.25f));
        trick.Released(t, HandId.Left, false, TrickSim.Unseen);
        Assert.Equal(TrickPhase.Busted, trick.Phase);
        Assert.Equal("The coin fell in plain view.", trick.Prompt);
        TrickSim.Run(trick, ref t, 2.6, TrickSim.Input());
        Assert.Equal(TrickPhase.Idle, trick.Phase);
    }

    [Test]
    public static void DitchingBeforeShowingIsABust()
    {
        double t = 0;
        var trick = new VanishTrick();
        trick.PickedUp(t, HandId.Left);
        trick.Released(t, HandId.Left, true, TrickSim.Unseen);
        Assert.Equal(TrickPhase.Busted, trick.Phase);
    }

    [Test]
    public static void ProduceNeedsALookedAtSpectatorAndADwell()
    {
        double t = 0;
        VanishTrick trick = TrickSim.Ditched(ref t, TrickSim.Unseen);
        TrickSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: true));
        Assert.False(trick.Stolen(t, HandId.None, TrickSim.Unseen), "a steal needs a hand");
        Assert.True(trick.Stolen(t, HandId.Right, TrickSim.Unseen));
        Assert.False(TrickSim.Run(trick, ref t, 1.0, TrickSim.Input(lookedAt: -1, pinch: true)).HasValue, "not looking at anyone");
        Assert.False(TrickSim.Run(trick, ref t, 1.0, TrickSim.Input(lookedAt: 0, dwell: 0.2, pinch: true)).HasValue, "glance too short");
        Assert.False(TrickSim.Run(trick, ref t, 1.0, TrickSim.Input(lookedAt: 0, dwell: 1.0, pinch: false)).HasValue, "no pinch");
        Assert.Equal(TrickPhase.Loaded, trick.Phase);
    }

    [Test]
    public static void StealOnlyAfterTheVanishAndReditchKeepsTheEncore()
    {
        double t = 0;
        VanishTrick trick = TrickSim.Ditched(ref t, TrickSim.Unseen);
        Assert.False(trick.Stolen(t, HandId.Right, TrickSim.Unseen), "show the empty hand first");
        TrickSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: true));
        Assert.True(trick.Stolen(t, HandId.Right, TrickSim.Unseen));
        trick.Released(t, HandId.Right, true, TrickSim.Unseen);
        Assert.Equal(TrickPhase.AfterVanish, trick.Phase, "put back in the servante");
        Assert.True(trick.CanSteal);
    }

    [Test]
    public static void CoinCanOnlyBeStolenFromTheServante()
    {
        var coin = new CoinModel();
        Assert.False(coin.TrySteal(HandId.Left), "resting coin isn't a steal");
        coin.TryGrab(HandId.Left);
        coin.Release(HandId.Left, true);
        Assert.True(coin.TrySteal(HandId.Right), "ditched coin can be stolen");
        Assert.Equal(CoinState.Held, coin.State);
        Assert.Equal(HandId.Right, coin.Holder);
    }
}
