// Unit tests for the show flow, pause and first-launch tutorial in Misdirection.Core.Show.
using System;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;

public static class ShowFlowTests
{
    private static RevealResult Clap(int tips)
    {
        return new RevealResult { Kind = "vanish", Tips = tips, Reaction = Reaction.Applause, SeenBy = -1, Spectator = -1 };
    }

    private static RevealResult Gasp(int tips)
    {
        return new RevealResult { Kind = "vanish", Tips = tips, Reaction = Reaction.Gasp, SeenBy = -1, Spectator = -1 };
    }

    [Test]
    public static void FirstLaunchGoesThroughTheTutorialWithOneSpectator()
    {
        var show = new ShowFlow();
        Assert.Equal(ShowStage.Setup, show.Stage);
        Assert.Equal(-1, show.ActiveTrick, "no trick before the table");
        Assert.Equal(0, show.Crowd.Length, "nobody before the table");
        show.TableReady(1);
        Assert.Equal(ShowStage.Tutorial, show.Stage);
        Assert.Equal(1, show.Crowd.Length, "just the Kid");
        Assert.Equal(Archetype.Kid, show.Crowd[0]);
        Assert.Equal(0, show.ActiveTrick, "the tutorial teaches Trick 1");
        show.Revealed(Clap(5));
        Assert.Equal(0, show.Tips, "tutorial tips don't count");
        show.TutorialFinished(30);
        Assert.True(show.TutorialDone, "remembered");
        Assert.Equal(ShowStage.Performing, show.Stage);
        Assert.Equal(0, show.Act);
        Assert.Equal(3, show.Crowd.Length, "act 1 crowd");
    }

    [Test]
    public static void LaterLaunchesSkipTheTutorial()
    {
        var show = new ShowFlow(null, tutorialDone: true);
        show.TableReady(0);
        Assert.Equal(ShowStage.Performing, show.Stage);
        Assert.Equal(1, show.ShowsStarted);
    }

    [Test]
    public static void ActsGrowTheCrowdAndAlternateTricks()
    {
        var show = new ShowFlow(null, true);
        show.TableReady(0);
        Assert.Equal(0, show.ActiveTrick);
        Assert.Equal(3, show.Crowd.Length);
        show.TrickCompleted(60);
        Assert.Equal(1, show.Act);
        Assert.Equal(1, show.ActiveTrick, "act 2 is the cup");
        Assert.Equal(4, show.Crowd.Length, "the Filmer joins");
        show.TrickCompleted(120);
        Assert.Equal(2, show.Act);
        Assert.Equal(0, show.ActiveTrick, "act 3 is the vanish again");
        Assert.Equal(5, show.Crowd.Length, "the Regular joins");
    }

    [Test]
    public static void CleanShowEndsInABowWithThreeStars()
    {
        var show = new ShowFlow(null, true);
        show.TableReady(10);
        for (int act = 0; act < 3; act++)
        {
            show.Revealed(Clap(15));
            show.Revealed(Clap(18));
            show.TrickCompleted(10 + 60 * (act + 1));
        }
        Assert.Equal(ShowStage.Bow, show.Stage);
        Assert.True(show.Over, "over");
        ShowSummary s = show.LastSummary;
        Assert.Equal(99, s.Tips);
        Assert.Equal(3, s.ActsCompleted);
        Assert.Equal(6, s.Reveals);
        Assert.Equal(6, s.Applause);
        Assert.Equal(0, s.Busts);
        Assert.False(s.WalkedOff, "bowed");
        Assert.Near(180, s.Seconds, 1e-9, "show length");
        Assert.Equal(3, s.Stars);
        Assert.Equal(0, show.ActiveTrick, "the coin is back for another go");
        Assert.Equal(5, show.Crowd.Length, "the last crowd takes the bow with you");
    }

    [Test]
    public static void ThreeBustsAndTheCrowdWalksOff()
    {
        var show = new ShowFlow(null, true);
        show.TableReady(0);
        Assert.False(show.Busted(5), "one");
        Assert.False(show.Busted(9), "two");
        Assert.True(show.Busted(14), "three: walk-off");
        Assert.Equal(ShowStage.WalkOff, show.Stage);
        Assert.True(show.LastSummary.WalkedOff, "walked off");
        Assert.Equal(0, show.LastSummary.Stars);
        Assert.Equal(0, show.LastSummary.ActsCompleted);
        Assert.Equal(0, show.Crowd.Length, "nobody left");
    }

    [Test]
    public static void BustsOutsideTheShowDontCount()
    {
        var show = new ShowFlow();
        show.TableReady(0);
        Assert.False(show.Busted(1), "tutorial bust");
        Assert.Equal(0, show.Busts);
    }

    [Test]
    public static void RestartNeedsTheBowToPlayFirst()
    {
        var show = new ShowFlow(null, true);
        show.TableReady(0);
        show.Busted(1);
        show.Busted(2);
        show.Busted(3);
        Assert.False(show.CanRestart(5), "bow still playing");
        Assert.False(show.Restart(5), "too soon");
        Assert.True(show.CanRestart(7.1), "after 4 s");
        Assert.True(show.Restart(7.1), "restarted");
        Assert.Equal(ShowStage.Performing, show.Stage);
        Assert.Equal(0, show.Act);
        Assert.Equal(0, show.Busts, "fresh count");
        Assert.Equal(2, show.ShowsStarted);
    }

    [Test]
    public static void StarsRewardCleanShows()
    {
        Assert.Equal(3, ShowFlow.StarsFor(false, 0, 6, 4), "4 of 6 applause, no busts");
        Assert.Equal(2, ShowFlow.StarsFor(false, 0, 6, 3), "half applause");
        Assert.Equal(2, ShowFlow.StarsFor(false, 1, 6, 6), "one bust");
        Assert.Equal(1, ShowFlow.StarsFor(false, 2, 6, 6), "two busts");
        Assert.Equal(0, ShowFlow.StarsFor(true, 3, 1, 1), "walked off");
    }

    [Test]
    public static void GaspsCountTipsButNotApplause()
    {
        var show = new ShowFlow(null, true);
        show.TableReady(0);
        show.Revealed(Gasp(8));
        Assert.Equal(8, show.Tips);
        Assert.Equal(1, show.Reveals);
        Assert.Equal(0, show.Applause);
    }
}

public static class ShowPauseTests
{
    [Test]
    public static void BothHandsGoneTwoSecondsPausesThenBothBackResumes()
    {
        var pause = new ShowPause();
        Assert.False(pause.Update(0, true, true, true), "playing");
        Assert.False(pause.Update(0.5, false, false, true), "just gone");
        Assert.True(pause.Update(2.6, false, false, true), "paused after 2 s");
        Assert.Equal(PauseReason.HandsGone, pause.Reason);
        Assert.False(pause.Update(2.8, true, false, true), "one hand back is not enough");
        Assert.True(pause.Paused, "still paused");
        pause.Update(3.0, true, true, true);
        Assert.True(pause.Update(3.6, true, true, true), "resumed after 0.5 s with both back");
        Assert.Equal(PauseReason.None, pause.Reason);
    }

    [Test]
    public static void LosingFocusPausesAtOnce()
    {
        var pause = new ShowPause();
        Assert.True(pause.Update(0, true, true, false), "focus lost");
        Assert.Equal(PauseReason.FocusLost, pause.Reason);
        Assert.True(pause.Update(1, true, true, true), "focus back");
        Assert.False(pause.Paused, "playing");
    }

    [Test]
    public static void HandsAreIgnoredWhenNotWatched()
    {
        var pause = new ShowPause();
        pause.WatchHands = false;
        pause.Update(0, false, false, true);
        pause.Update(5, false, false, true);
        Assert.False(pause.Paused, "nobody is playing yet");
    }
}

public static class TutorialTests
{
    private static TutorialInput In(TrickPhase phase, bool table = true, bool contact = false, float coverage = 1f)
    {
        return new TutorialInput { TableReady = table, Phase = phase, KidEyeContact = contact, KidCoverage = coverage };
    }

    private static void Run(Tutorial t, ref double time, double seconds, TutorialInput input)
    {
        int frames = (int)Math.Round(seconds / TrickSim.Dt);
        for (int i = 0; i < frames; i++)
        {
            time += TrickSim.Dt;
            t.Tick(time, input);
        }
    }

    [Test]
    public static void CleanRunReachesFirstApplause()
    {
        var t = new Tutorial();
        double time = 0;
        t.Tick(time, In(TrickPhase.Idle, table: false));
        Assert.Equal(TutorialStep.PalmsOnTable, t.Step);
        Run(t, ref time, 5, In(TrickPhase.Idle, table: false));
        Assert.Equal(TutorialStep.PalmsOnTable, t.Step, "waits for the palms");
        Run(t, ref time, 0.1, In(TrickPhase.Idle));
        Assert.Equal(TutorialStep.PickUp, t.Step);
        Run(t, ref time, 0.1, In(TrickPhase.Showing));
        Assert.Equal(TutorialStep.ShowCoin, t.Step);
        Run(t, ref time, 0.1, In(TrickPhase.Misdirect));
        Assert.Equal(TutorialStep.LookAtKid, t.Step);
        Run(t, ref time, 0.2, In(TrickPhase.Misdirect, contact: true));
        Assert.Equal(TutorialStep.LookAtKid, t.Step, "eye contact must hold a moment");
        Run(t, ref time, 0.3, In(TrickPhase.Misdirect, contact: true));
        Assert.Equal(TutorialStep.Ditch, t.Step);
        Run(t, ref time, 0.1, In(TrickPhase.Vanished));
        Assert.Equal(TutorialStep.ShowEmpty, t.Step);
        Assert.False(t.Revealed(time, new RevealResult { Kind = "vanish", Reaction = Reaction.Applause }), "no reset");
        Assert.Equal(TutorialStep.Applause, t.Step);
        Assert.Near(time, t.SecondsToFirstApplause, 1e-9, "timed from the start");
        Run(t, ref time, 3.1, In(TrickPhase.AfterVanish));
        Assert.True(t.Finished, "done");
        Assert.Equal(0, t.Retries);
    }

    [Test]
    public static void TableFromTheRoomScanSkipsThePalmsStep()
    {
        var t = new Tutorial();
        t.Tick(0, In(TrickPhase.Idle, table: true));
        Assert.Equal(TutorialStep.PickUp, t.Step);
    }

    [Test]
    public static void LookingOffTheCoinCountsToo()
    {
        var t = new Tutorial();
        double time = 0;
        t.Tick(time, In(TrickPhase.Misdirect));
        Assert.Equal(TutorialStep.LookAtKid, t.Step, "straight to the look");
        Run(t, ref time, 0.5, In(TrickPhase.Misdirect, coverage: 0.1f));
        Assert.Equal(TutorialStep.Ditch, t.Step);
    }

    [Test]
    public static void StuckOnTheLookMovesOnAfterATimeout()
    {
        var t = new Tutorial();
        double time = 0;
        t.Tick(time, In(TrickPhase.Misdirect));
        Run(t, ref time, 7.5, In(TrickPhase.Misdirect, coverage: 1f));
        Assert.Equal(TutorialStep.LookAtKid, t.Step, "still teaching");
        Run(t, ref time, 1.0, In(TrickPhase.Misdirect, coverage: 1f));
        Assert.Equal(TutorialStep.Ditch, t.Step, "no soft-lock");
    }

    [Test]
    public static void ABustSendsYouBackToThePickupOnce()
    {
        var t = new Tutorial();
        double time = 0;
        t.Tick(time, In(TrickPhase.Misdirect));
        Run(t, ref time, 2.5, In(TrickPhase.Busted));
        Assert.Equal(TutorialStep.PickUp, t.Step);
        Assert.Equal(1, t.Retries, "counted once, not every frame");
        Run(t, ref time, 0.1, In(TrickPhase.Idle));
        Assert.Equal(TutorialStep.PickUp, t.Step);
    }

    [Test]
    public static void ThePickupStepCatchesUpWithAFastPlayer()
    {
        var t = new Tutorial();
        t.Tick(0, In(TrickPhase.Vanished));
        Assert.Equal(TutorialStep.ShowEmpty, t.Step, "already ditched: straight to the reveal");
    }

    [Test]
    public static void AHeckledRevealAsksForTheCoinBack()
    {
        var t = new Tutorial();
        t.Tick(0, In(TrickPhase.Misdirect));
        t.Tick(0.1, In(TrickPhase.Vanished));
        Assert.Equal(TutorialStep.ShowEmpty, t.Step, "ditched their own way");
        Assert.True(t.Revealed(0.5, new RevealResult { Kind = "vanish", Reaction = Reaction.Heckle, SeenBy = 0 }), "put the coin back");
        Assert.Equal(TutorialStep.PickUp, t.Step);
        Assert.Equal(1, t.Retries);
        Assert.True(double.IsNaN(t.FirstApplauseAt), "no applause yet");
    }

    [Test]
    public static void DroppingTheCoinMidLessonGoesBackToThePickup()
    {
        var t = new Tutorial();
        double time = 0;
        t.Tick(time, In(TrickPhase.Showing));
        Assert.Equal(TutorialStep.ShowCoin, t.Step);
        t.Tick(time += 0.1, In(TrickPhase.Idle));
        Assert.Equal(TutorialStep.PickUp, t.Step);
    }
}
