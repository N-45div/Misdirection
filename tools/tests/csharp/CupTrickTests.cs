// Unit tests for Trick 2 (coin under the cup) and the shared reveal scoring.
using System;
using Misdirection.Core.Crowd;
using Misdirection.Core.Tricks;

public static class CupSim
{
    public const double Dt = 1.0 / 72.0;

    public static RevealResult? Run(CupTrick trick, ref double t, double seconds, TrickInput input, float lift = 0f)
    {
        RevealResult? reveal = null;
        int frames = (int)Math.Round(seconds / Dt);
        for (int i = 0; i < frames; i++)
        {
            t += Dt;
            RevealResult? r = trick.Tick(t, input, lift);
            if (r.HasValue) reveal = r;
        }
        return reveal;
    }

    /// <summary>Picks up and shows the coin; leaves the trick in Misdirect.</summary>
    public static CupTrick Shown(ref double t)
    {
        var trick = new CupTrick();
        trick.PickedUp(t, HandId.Right);
        Run(trick, ref t, 0.8, TrickSim.Input(height: 0.25f));
        Assert.Equal(CupPhase.Misdirect, trick.Phase, "shown");
        Assert.True(trick.SecretInPlay, "coin is the secret now");
        return trick;
    }
}

public static class CupTrickTests
{
    [Test]
    public static void CleanLoadVanishThenLiftGetsApplauseTwice()
    {
        double t = 0;
        CupTrick trick = CupSim.Shown(ref t);
        trick.Released(t, HandId.Right, ReleaseZone.UnderCup, TrickSim.Unseen);
        Assert.Equal(CupPhase.Loaded, trick.Phase);
        Assert.False(CupSim.Run(trick, ref t, 1.0, TrickSim.Input()).HasValue, "nothing happens until hands are shown");

        RevealResult? vanish = CupSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: true));
        Assert.True(vanish.HasValue, "vanish revealed");
        Assert.Equal("vanish", vanish.Value.Kind);
        Assert.Equal(Reaction.Applause, vanish.Value.Reaction);
        Assert.Equal(CupPhase.Vanished, trick.Phase);
        Assert.True(trick.CupLiftable);

        Assert.False(CupSim.Run(trick, ref t, 0.5, TrickSim.Input(), 0.05f).HasValue, "a nudge isn't a lift");
        RevealResult? cup = CupSim.Run(trick, ref t, 0.2, TrickSim.Input(), 0.12f);
        Assert.True(cup.HasValue, "lifted");
        Assert.Equal("cup", cup.Value.Kind);
        Assert.Near(1.2, cup.Value.Amazement, 1e-6, "full effect earns the production bonus");
        Assert.Equal(CupPhase.Finale, trick.Phase);
        Assert.Equal(15 + 18, trick.TotalTips);
        CupSim.Run(trick, ref t, 3.2, TrickSim.Input());
        Assert.Equal(CupPhase.Idle, trick.Phase, "resets after the bow");
    }

    [Test]
    public static void LiftingStraightAwaySkipsTheVanishAndTheBonus()
    {
        double t = 0;
        CupTrick trick = CupSim.Shown(ref t);
        trick.Released(t, HandId.Right, ReleaseZone.UnderCup, TrickSim.Unseen);
        RevealResult? cup = CupSim.Run(trick, ref t, 0.2, TrickSim.Input(), 0.1f);
        Assert.Equal("cup", cup.Value.Kind);
        Assert.Near(1.0, cup.Value.Amazement, 1e-6, "no bonus without the vanish");
    }

    [Test]
    public static void SeenLoadIsHeckledOnBothReveals()
    {
        double t = 0;
        CupTrick trick = CupSim.Shown(ref t);
        trick.Released(t, HandId.Right, ReleaseZone.UnderCup, TrickSim.Seen(0.9f, 2));
        RevealResult? vanish = CupSim.Run(trick, ref t, 0.5, TrickSim.Input(emptyHand: true));
        Assert.Equal(Reaction.Heckle, vanish.Value.Reaction);
        Assert.Equal(2, vanish.Value.SeenBy);
        RevealResult? cup = CupSim.Run(trick, ref t, 0.2, TrickSim.Input(), 0.1f);
        Assert.Equal(Reaction.Heckle, cup.Value.Reaction, "they knew it was there");
        Assert.Equal(0, trick.TotalTips);
    }

    [Test]
    public static void WrongPlacesBust()
    {
        double t = 0;
        CupTrick early = new CupTrick();
        early.PickedUp(t, HandId.Left);
        early.Released(t, HandId.Left, ReleaseZone.UnderCup, TrickSim.Unseen);
        Assert.Equal(CupPhase.Busted, early.Phase);
        Assert.Equal("You hid it before they saw it.", early.Prompt);

        CupTrick dropped = CupSim.Shown(ref t);
        dropped.Released(t, HandId.Right, ReleaseZone.Table, TrickSim.Unseen);
        Assert.Equal("The coin fell in plain view.", dropped.Prompt);

        CupTrick servante = CupSim.Shown(ref t);
        servante.Released(t, HandId.Right, ReleaseZone.Servante, TrickSim.Unseen);
        Assert.Equal(CupPhase.Busted, servante.Phase, "ditching isn't this trick");
        CupSim.Run(servante, ref t, 2.6, TrickSim.Input());
        Assert.Equal(CupPhase.Idle, servante.Phase, "resets");
    }

    [Test]
    public static void OnlyTheHoldingHandCountsAndCupStaysPutBeforeTheLoad()
    {
        double t = 0;
        CupTrick trick = CupSim.Shown(ref t);
        trick.Released(t, HandId.Left, ReleaseZone.Table, TrickSim.Unseen);
        Assert.Equal(CupPhase.Misdirect, trick.Phase, "the other hand opening changes nothing");
        Assert.False(trick.CupLiftable, "nothing under the cup yet");
        Assert.False(CupSim.Run(trick, ref t, 0.3, TrickSim.Input(), 0.2f).HasValue, "lifting the empty cup early is ignored");
    }

    [Test]
    public static void SharedScoringMatchesTrickOne()
    {
        TrickInput input = TrickSim.Input(suspicion: 0.4f);
        WitnessReport glimpse = TrickSim.Seen(0.3f, 0);
        RevealResult a = new VanishTrick().Score("x", glimpse, input, 0f, -1);
        RevealResult b = RevealScoring.Score("x", glimpse, input, 0f, -1, 0.6f, 0.75f, 0.45f, 5, 0.2f);
        Assert.Near(a.Amazement, b.Amazement, 1e-6, "amazement");
        Assert.Equal(a.Reaction, b.Reaction);
        Assert.Equal(a.Tips, b.Tips);
    }
}
