// Unit tests for the Day 7 crowd expression model.
using Misdirection.Core.Crowd;
using Misdirection.Core.Tricks;

public static class ExpressionTests
{
    static Expression Eval(Archetype a, SuspicionLevel level, Reaction r = Reaction.None, double age = 0, bool mine = false,
                           bool eye = false, bool away = false, float time = 0f)
    {
        return ExpressionModel.Evaluate(a, level, eye, away, r, age, mine, time);
    }

    [Test]
    public static void SuspicionShowsInBrowsMouthAndLean()
    {
        Expression calm = Eval(Archetype.Tourist, SuspicionLevel.Calm);
        Expression curious = Eval(Archetype.Tourist, SuspicionLevel.Curious);
        Expression suspicious = Eval(Archetype.Tourist, SuspicionLevel.Suspicious);
        Expression leaving = Eval(Archetype.Tourist, SuspicionLevel.WalkingOff);
        Assert.True(curious.BrowRaise > calm.BrowRaise, "curious raises brows");
        Assert.True(curious.Lean > calm.Lean, "curious leans in");
        Assert.True(suspicious.BrowFurrow > 0.5f && suspicious.MouthCurve < 0f, "suspicious furrows and frowns");
        Assert.True(leaving.Lean < suspicious.Lean, "about to walk off leans away");
    }

    [Test]
    public static void ArchetypesHaveTheirRestingPoses()
    {
        Assert.Equal(ArmPose.Folded, Eval(Archetype.Skeptic, SuspicionLevel.Calm).Arms);
        Assert.Equal(ArmPose.Phone, Eval(Archetype.Filmer, SuspicionLevel.Calm).Arms);
        Assert.Equal(ArmPose.Rest, Eval(Archetype.Kid, SuspicionLevel.Calm).Arms);
    }

    [Test]
    public static void EyeContactSmilesButStaringTooLongDoesNot()
    {
        Assert.True(Eval(Archetype.Tourist, SuspicionLevel.Calm, eye: true).MouthCurve > 0.3f, "eye contact smiles");
        Assert.True(Eval(Archetype.Tourist, SuspicionLevel.Calm, away: true).MouthCurve < 0f, "uncomfortable when stared at");
    }

    [Test]
    public static void ApplauseClapsGaspOpensTheMouthHeckleFrownsAndPoints()
    {
        Expression clap = Eval(Archetype.Kid, SuspicionLevel.Calm, Reaction.Applause, 0.5);
        Assert.Equal(ArmPose.Clap, clap.Arms);
        Assert.True(clap.MouthCurve > 0.9f, "smiling");
        Assert.Equal(ArmPose.Phone, Eval(Archetype.Filmer, SuspicionLevel.Calm, Reaction.Applause, 0.5).Arms, "the filmer keeps filming");

        Expression gasp = Eval(Archetype.Tourist, SuspicionLevel.Calm, Reaction.Gasp, 0.1);
        Assert.True(gasp.MouthOpen > 0.9f && gasp.BrowRaise >= 1f, "jaw drop");
        Assert.True(gasp.Lean < 0f, "leans back");

        Expression heckler = Eval(Archetype.Skeptic, SuspicionLevel.Suspicious, Reaction.Heckle, 0.2, mine: true);
        Assert.Equal(ArmPose.Point, heckler.Arms);
        Assert.True(heckler.MouthCurve < -0.9f && heckler.BrowFurrow >= 1f, "I saw that!");
        Expression bystander = Eval(Archetype.Tourist, SuspicionLevel.Calm, Reaction.Heckle, 0.2, mine: false);
        Assert.Equal(ArmPose.Rest, bystander.Arms, "only the heckler points");
    }

    [Test]
    public static void ReactionsFadeBackToRest()
    {
        Expression after = Eval(Archetype.Kid, SuspicionLevel.Calm, Reaction.Applause, ExpressionModel.ReactionSeconds + 0.1);
        Expression rest = Eval(Archetype.Kid, SuspicionLevel.Calm);
        Assert.Equal(rest.Arms, after.Arms);
        Assert.Near(rest.MouthCurve, after.MouthCurve, 1e-6, "back to resting face");
    }

    [Test]
    public static void ClappingCyclesAndSpectatorsAreOutOfTime()
    {
        Expression a = Eval(Archetype.Tourist, SuspicionLevel.Calm, Reaction.Applause, 0.5, time: 0f);
        Expression b = Eval(Archetype.Tourist, SuspicionLevel.Calm, Reaction.Applause, 0.5, time: 0.15f);
        Assert.True(a.ClapPhase != b.ClapPhase, "phase moves with time");
        Expression kid = Eval(Archetype.Kid, SuspicionLevel.Calm, Reaction.Applause, 0.5, time: 0f);
        Assert.True(kid.ClapPhase != a.ClapPhase, "different spectators clap out of time");
        Assert.True(a.ClapPhase >= 0f && a.ClapPhase < 1f, "phase in [0,1)");
    }
}
