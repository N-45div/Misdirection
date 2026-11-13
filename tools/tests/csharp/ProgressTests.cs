// Unit tests for the Day 10 "reason to come back" rules in Misdirection.Core.Progress.
using System;
using System.Collections.Generic;
using System.Numerics;
using Misdirection.Core.Crowd;
using Misdirection.Core.Progress;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;

public static class ProgressSim
{
    /// <summary>Wed 11 Nov 2026 (Day 10 of the build).</summary>
    public const int Day1 = 20261111;
    public const int Day2 = 20261112;
    public const int Day3 = 20261113;

    public const int Vanish = 0;
    public const int Cup = 1;

    /// <summary>The fixed Day 9 show: Tourist, Kid, Skeptic; then the Filmer; then the Regular.</summary>
    public static ShowAct[] ClassicActs()
    {
        return new ShowSettings().Acts;
    }

    public static RevealResult Reveal(string kind, Reaction reaction, int tips, int seenBy = -1, int spectator = -1)
    {
        return new RevealResult { Kind = kind, Reaction = reaction, Tips = reaction == Reaction.Heckle ? 0 : tips, SeenBy = seenBy, Spectator = spectator };
    }

    /// <summary>Plays one act cleanly: applause for the vanish and for the reappearance (from <paramref name="ear"/>'s ear in a vanish act).</summary>
    public static void CleanAct(ShowRecord record, ShowAct[] acts, int act, Archetype ear)
    {
        Archetype[] crowd = acts[act].Crowd;
        int tips = 5 * crowd.Length;
        record.AddReveal(act, acts[act].Trick, Reveal("vanish", Reaction.Applause, tips), crowd);
        if (acts[act].Trick == Vanish)
        {
            int from = Math.Max(0, Array.IndexOf(crowd, ear));
            record.AddReveal(act, Vanish, Reveal("produce", Reaction.Applause, tips + crowd.Length, -1, from), crowd);
        }
        else
        {
            record.AddReveal(act, Cup, Reveal("cup", Reaction.Applause, tips + crowd.Length), crowd);
        }
    }

    /// <summary>A whole clean show, the coin found behind <paramref name="ear"/>'s ear each time.</summary>
    public static ShowRecord CleanShow(ShowAct[] acts, Archetype ear)
    {
        ShowRecord record = new ShowRecord(acts);
        for (int act = 0; act < acts.Length; act++)
        {
            CleanAct(record, acts, act, ear);
        }
        return record;
    }

    /// <summary>The summary the show flow would give for a record.</summary>
    public static ShowSummary Summary(ShowRecord record, bool walkedOff = false, int actsCompleted = -1)
    {
        ShowSummary s = new ShowSummary();
        foreach (ShowEvent e in record.Events)
        {
            if (e.Kind == ShowEventKind.Bust)
            {
                s.Busts++;
                continue;
            }
            s.Reveals++;
            s.Tips += e.Tips;
            if (e.Reaction == Reaction.Applause)
            {
                s.Applause++;
            }
        }
        s.WalkedOff = walkedOff;
        s.ActsCompleted = actsCompleted >= 0 ? actsCompleted : record.ActCount;
        s.Stars = ShowFlow.StarsFor(walkedOff, s.Busts, s.Reveals, s.Applause);
        return s;
    }

    public static int Count(Archetype[] crowd, Archetype archetype)
    {
        int n = 0;
        foreach (Archetype a in crowd)
        {
            if (a == archetype)
            {
                n++;
            }
        }
        return n;
    }

    public static bool SameCrowd(Archetype[] a, Archetype[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    public static bool SameShow(DailyPlan a, DailyPlan b)
    {
        for (int i = 0; i < a.Acts.Length; i++)
        {
            if (!SameCrowd(a.Acts[i].Crowd, b.Acts[i].Crowd))
            {
                return false;
            }
        }
        return true;
    }

    public static string Names(Archetype[] crowd)
    {
        return string.Join(",", Array.ConvertAll(crowd, a => a.ToString()));
    }
}

public static class CalendarDayTests
{
    [Test]
    public static void OnlyRealDatesAreValid()
    {
        Assert.True(CalendarDay.IsValid(20261111), "11 Nov 2026");
        Assert.True(CalendarDay.IsValid(20240229), "leap day");
        Assert.False(CalendarDay.IsValid(20230229), "29 Feb in a common year");
        Assert.False(CalendarDay.IsValid(20261301), "month 13");
        Assert.False(CalendarDay.IsValid(20261100), "day 0");
        Assert.False(CalendarDay.IsValid(0), "none");
        Assert.False(CalendarDay.IsValid(-20261111), "negative");
        Assert.Equal(20261111, CalendarDay.From(new DateTime(2026, 11, 11, 23, 59, 0)), "from a date and time");
    }

    [Test]
    public static void ConsecutiveDaysDifferByOneAcrossMonthAndYearEnds()
    {
        Assert.Equal(1, CalendarDay.DaysBetween(20261111, 20261112));
        Assert.Equal(1, CalendarDay.DaysBetween(20261130, 20261201), "month end");
        Assert.Equal(1, CalendarDay.DaysBetween(20261231, 20270101), "year end");
        Assert.Equal(-7, CalendarDay.DaysBetween(20261111, 20261104), "backwards");
        Assert.Equal(0, CalendarDay.DaysBetween(20261111, 20269999), "an invalid date is no distance");
        Assert.Equal(20261201, CalendarDay.AddDays(20261130, 1));
        Assert.Equal(20261231, CalendarDay.AddDays(20270101, -1));
        Assert.Equal(CalendarDay.None, CalendarDay.AddDays(0, 1), "nothing to add to");
    }
}

public static class DailyShowTests
{
    private const int Days = 400;

    private static int Date(int offset)
    {
        return CalendarDay.AddDays(ProgressSim.Day1, offset);
    }

    [Test]
    public static void TheSameDateAlwaysGivesTheSameShow()
    {
        for (int i = 0; i < 30; i++)
        {
            DailyPlan a = DailyShow.Plan(Date(i), false);
            DailyPlan b = DailyShow.Plan(Date(i), false);
            Assert.True(ProgressSim.SameShow(a, b), "same line-ups on " + Date(i));
            Assert.Equal(a.Challenge.Kind, b.Challenge.Kind, "same challenge");
            Assert.Equal(a.Challenge.Target, b.Challenge.Target, "same target");
            Assert.Equal(a.Seed, b.Seed, "same seed");
        }
        Assert.Equal(DailyShow.SeedFor(20261111), DailyShow.SeedFor(20261111));
        Assert.False(DailyShow.SeedFor(20261111) == DailyShow.SeedFor(20261112), "neighbouring days have different seeds");
    }

    [Test]
    public static void ActsStayThreeFourFiveWithVanishCupVanish()
    {
        for (int i = 0; i < Days; i++)
        {
            DailyPlan plan = DailyShow.Plan(Date(i), i % 2 == 0);
            Assert.Equal(3, plan.Acts.Length, "acts");
            Assert.Equal(3, plan.Acts[0].Crowd.Length, "act 1 size");
            Assert.Equal(4, plan.Acts[1].Crowd.Length, "act 2 size");
            Assert.Equal(5, plan.Acts[2].Crowd.Length, "act 3 size");
            Assert.Equal(0, plan.Acts[0].Trick, "act 1 is the vanish");
            Assert.Equal(1, plan.Acts[1].Trick, "act 2 is the cup");
            Assert.Equal(0, plan.Acts[2].Trick, "act 3 is the vanish");
        }
    }

    [Test]
    public static void TheShowFlowRunsTodaysPlan()
    {
        DailyPlan plan = DailyShow.Plan(ProgressSim.Day1, true);
        ShowSettings template = new ShowSettings { BustsToWalkOff = 5, OverSeconds = 2.0 };
        ShowSettings settings = DailyShow.ToSettings(plan, template);
        Assert.Equal(5, settings.BustsToWalkOff, "other settings come from the template");
        Assert.Near(2.0, settings.OverSeconds);
        var show = new ShowFlow(settings, true);
        show.TableReady(0);
        for (int act = 0; act < 3; act++)
        {
            Assert.Equal(plan.Acts[act].Trick, show.ActiveTrick, "trick of act " + (act + 1));
            Assert.True(ProgressSim.SameCrowd(plan.Acts[act].Crowd, show.Crowd), "crowd of act " + (act + 1));
            show.TrickCompleted(60 * (act + 1));
        }
        Assert.Equal(ShowStage.Bow, show.Stage);
    }

    [Test]
    public static void KidIsInActOneAndTheSkepticArrivesByActTwo()
    {
        for (int i = 0; i < Days; i++)
        {
            DailyPlan plan = DailyShow.Plan(Date(i), i % 3 == 0);
            Assert.True(ProgressSim.Count(plan.Acts[0].Crowd, Archetype.Kid) >= 1, "Kid in act 1 on " + Date(i));
            Assert.True(ProgressSim.Count(plan.Acts[1].Crowd, Archetype.Skeptic) >= 1, "Skeptic by act 2 on " + Date(i));
        }
    }

    [Test]
    public static void ActOneIsLearnable()
    {
        for (int i = 0; i < Days; i++)
        {
            Archetype[] act1 = DailyShow.Plan(Date(i), true).Acts[0].Crowd;
            int hard = ProgressSim.Count(act1, Archetype.Skeptic) + ProgressSim.Count(act1, Archetype.Filmer);
            Assert.True(hard <= 1, "at most one hard watcher in act 1 on " + Date(i) + ": " + ProgressSim.Names(act1));
            Assert.Equal(0, ProgressSim.Count(act1, Archetype.Regular), "no Regular in act 1");
        }
    }

    [Test]
    public static void TheCrowdOnlyGrowsSoActThreeIsTheHardest()
    {
        for (int i = 0; i < Days; i++)
        {
            DailyPlan plan = DailyShow.Plan(Date(i), i % 2 == 1);
            for (int act = 1; act < 3; act++)
            {
                // Everyone from the act before is still there, in the same order, plus one newcomer.
                List<Archetype> now = new List<Archetype>(plan.Acts[act].Crowd);
                int at = 0;
                foreach (Archetype stayed in plan.Acts[act - 1].Crowd)
                {
                    at = now.IndexOf(stayed, at);
                    Assert.True(at >= 0, stayed + " stays for act " + (act + 1) + " on " + Date(i));
                    at++;
                }
                Assert.True(DailyShow.Difficulty(plan.Acts[act].Crowd) > DailyShow.Difficulty(plan.Acts[act - 1].Crowd),
                            "act " + (act + 1) + " is harder than the one before");
            }
        }
        Assert.True(DailyShow.Difficulty(Archetype.Skeptic) > DailyShow.Difficulty(Archetype.Regular), "Skeptic is harder than the Regular");
        Assert.True(DailyShow.Difficulty(Archetype.Regular) > DailyShow.Difficulty(Archetype.Kid), "Regular is harder than the Kid");
    }

    [Test]
    public static void TheRegularOnlyComesBackForReturningPlayers()
    {
        for (int i = 0; i < Days; i++)
        {
            DailyPlan fresh = DailyShow.Plan(Date(i), false);
            DailyPlan back = DailyShow.Plan(Date(i), true);
            Assert.False(fresh.RegularReturns);
            Assert.True(back.RegularReturns);
            foreach (ShowAct act in fresh.Acts)
            {
                Assert.Equal(0, ProgressSim.Count(act.Crowd, Archetype.Regular), "no Regular for a first-day player");
            }
            Assert.Equal(0, ProgressSim.Count(back.Acts[1].Crowd, Archetype.Regular), "not before act 3");
            Assert.Equal(1, ProgressSim.Count(back.Acts[2].Crowd, Archetype.Regular), "the Regular joins act 3");
            Assert.True(ProgressSim.SameCrowd(fresh.Acts[0].Crowd, back.Acts[0].Crowd), "act 1 is the date's, whoever plays");
            Assert.True(ProgressSim.SameCrowd(fresh.Acts[1].Crowd, back.Acts[1].Crowd), "act 2 is the date's, whoever plays");
        }
    }

    [Test]
    public static void NeverMoreThanTwoOfAKind()
    {
        for (int i = 0; i < Days; i++)
        {
            Archetype[] all = DailyShow.Plan(Date(i), i % 2 == 0).Acts[2].Crowd;
            foreach (Archetype a in Enum.GetValues(typeof(Archetype)))
            {
                Assert.True(ProgressSim.Count(all, a) <= DailyShow.MaxOfAKind, "too many " + a + " on " + Date(i) + ": " + ProgressSim.Names(all));
            }
        }
    }

    [Test]
    public static void DifferentDatesUsuallyDiffer()
    {
        var seen = new HashSet<string>();
        int sameAsYesterday = 0;
        DailyPlan before = null;
        for (int i = 0; i < Days; i++)
        {
            DailyPlan plan = DailyShow.Plan(Date(i), true);
            seen.Add(ProgressSim.Names(plan.Acts[0].Crowd) + "|" + ProgressSim.Names(plan.Acts[1].Crowd) + "|" + ProgressSim.Names(plan.Acts[2].Crowd));
            if (before != null && ProgressSim.SameShow(before, plan))
            {
                sameAsYesterday++;
            }
            before = plan;
        }
        Assert.True(seen.Count >= 100, "only " + seen.Count + " different shows in " + Days + " days");
        Assert.True(sameAsYesterday <= Days / 50, sameAsYesterday + " days repeated the day before's show");
    }

    [Test]
    public static void BothTricksCanMeetTheToughCrowdOnAGoodShareOfDays()
    {
        // The third trick-book star needs the Skeptic and the Filmer watching: some days, not every day.
        int cupDays = 0;
        int vanishDays = 0;
        for (int i = 0; i < Days; i++)
        {
            DailyPlan plan = DailyShow.Plan(Date(i), true);
            if (ProgressSim.Count(plan.Acts[1].Crowd, Archetype.Filmer) > 0)
            {
                cupDays++;
            }
            if (ProgressSim.Count(plan.Acts[2].Crowd, Archetype.Filmer) > 0)
            {
                vanishDays++;
            }
        }
        Assert.True(cupDays > Days / 4 && cupDays < Days * 3 / 4, "cup tough-crowd days: " + cupDays + " of " + Days);
        Assert.True(vanishDays >= cupDays && vanishDays < Days, "vanish tough-crowd days: " + vanishDays + " of " + Days);
    }
}

public static class DailyChallengeTests
{
    private static int Date(int offset)
    {
        return CalendarDay.AddDays(ProgressSim.Day1, offset);
    }

    [Test]
    public static void NeverTheSameChallengeTwoDaysRunningAndAllFiveComeRound()
    {
        int[] counts = new int[DailyChallenges.KindCount];
        // Start on the first day of a five-day round, so 400 days are exactly 80 rounds.
        int start = 0;
        while (CalendarDay.Ordinal(Date(start)) % DailyChallenges.KindCount != 0)
        {
            start++;
        }
        ChallengeKind before = DailyChallenges.KindFor(Date(start - 1));
        for (int i = start; i < start + 400; i++)
        {
            ChallengeKind kind = DailyChallenges.KindFor(Date(i));
            Assert.False(kind == before, "the same challenge two days running on " + Date(i));
            Assert.Equal(kind, DailyChallenges.KindFor(Date(i)), "same date, same challenge");
            counts[(int)kind]++;
            before = kind;
        }
        for (int k = 0; k < counts.Length; k++)
        {
            Assert.Equal(80, counts[k], "each challenge comes round every five days: " + (ChallengeKind)k);
        }
    }

    [Test]
    public static void QuietWatcherIsTheFilmerWhenSheIsThereElseTheSkeptic()
    {
        int filmer = 0;
        int skeptic = 0;
        for (int i = 0; i < 400; i++)
        {
            DailyPlan plan = DailyShow.Plan(Date(i), false);
            if (plan.Challenge.Kind != ChallengeKind.QuietWatcher)
            {
                continue;
            }
            bool filmerToday = ProgressSim.Count(plan.Acts[2].Crowd, Archetype.Filmer) > 0;
            Assert.Equal((int)(filmerToday ? Archetype.Filmer : Archetype.Skeptic), plan.Challenge.Target, "target on " + Date(i));
            Assert.True(ProgressSim.Count(plan.Acts[2].Crowd, (Archetype)plan.Challenge.Target) > 0, "the target is in today's crowd");
            if (filmerToday) filmer++; else skeptic++;
        }
        Assert.True(filmer > 0 && skeptic > 0, "both targets turn up: " + filmer + " Filmer days, " + skeptic + " Skeptic days");
    }

    [Test]
    public static void TipTargetsAreSteppedAndReachable()
    {
        var targets = new HashSet<int>();
        for (int i = 0; i < 400; i++)
        {
            DailyChallenge c = DailyShow.Plan(Date(i), false).Challenge;
            if (c.Kind == ChallengeKind.TipTarget)
            {
                Assert.True(c.Target >= 60 && c.Target <= 90 && c.Target % 10 == 0, "tip target " + c.Target);
                targets.Add(c.Target);
            }
        }
        Assert.True(targets.Count >= 3, "tip targets vary");
        // A clean classic show earns more than the highest target.
        ShowRecord clean = ProgressSim.CleanShow(ProgressSim.ClassicActs(), Archetype.Kid);
        Assert.True(ProgressSim.Summary(clean).Tips >= 90, "clean show tips " + ProgressSim.Summary(clean).Tips);
    }

    [Test]
    public static void NoBustsNeedsAFinishedShowWithoutOne()
    {
        var challenge = new DailyChallenge(ChallengeKind.NoBusts, 0);
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord clean = ProgressSim.CleanShow(acts, Archetype.Kid);
        Assert.True(DailyChallenges.Passed(challenge, clean, ProgressSim.Summary(clean)), "clean show");

        ShowRecord oneBust = ProgressSim.CleanShow(acts, Archetype.Kid);
        oneBust.AddBust(1, ProgressSim.Cup);
        Assert.False(DailyChallenges.Passed(challenge, oneBust, ProgressSim.Summary(oneBust)), "one bust");

        ShowRecord none = new ShowRecord(acts);
        Assert.False(DailyChallenges.Passed(challenge, none, ProgressSim.Summary(none, true, 0)), "walked off");
        Assert.False(DailyChallenges.Passed(null, clean, ProgressSim.Summary(clean)), "no challenge");
    }

    [Test]
    public static void SkepticEarNeedsAnUnheckledProductionFromTheSkeptic()
    {
        var challenge = new DailyChallenge(ChallengeKind.SkepticEar, 0);
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord fromKid = ProgressSim.CleanShow(acts, Archetype.Kid);
        Assert.False(DailyChallenges.Passed(challenge, fromKid, ProgressSim.Summary(fromKid)), "from the Kid");

        ShowRecord fromSkeptic = ProgressSim.CleanShow(acts, Archetype.Skeptic);
        Assert.True(DailyChallenges.Passed(challenge, fromSkeptic, ProgressSim.Summary(fromSkeptic)), "from the Skeptic");

        // Heckled: somebody saw the steal. Spectator 2 of the classic act 1 is the Skeptic.
        ShowRecord heckled = new ShowRecord(acts);
        heckled.AddReveal(0, ProgressSim.Vanish, ProgressSim.Reveal("produce", Reaction.Heckle, 0, 0, 2), acts[0].Crowd);
        Assert.False(DailyChallenges.Passed(challenge, heckled, ProgressSim.Summary(heckled, false, 1)), "heckled");

        // One good production is enough, even if the show then goes wrong.
        ShowRecord thenWalkedOff = new ShowRecord(acts);
        ProgressSim.CleanAct(thenWalkedOff, acts, 0, Archetype.Skeptic);
        thenWalkedOff.AddBust(1, ProgressSim.Cup);
        Assert.True(DailyChallenges.Passed(challenge, thenWalkedOff, ProgressSim.Summary(thenWalkedOff, true, 1)), "then walked off");
    }

    [Test]
    public static void FullApplauseActNeedsEveryRevealOfOneFinishedAct()
    {
        var challenge = new DailyChallenge(ChallengeKind.FullApplauseAct, 0);
        ShowAct[] acts = ProgressSim.ClassicActs();

        // Act 1: a gasp spoils it. Act 2: all applause.
        ShowRecord mixed = new ShowRecord(acts);
        mixed.AddReveal(0, ProgressSim.Vanish, ProgressSim.Reveal("vanish", Reaction.Gasp, 8), acts[0].Crowd);
        mixed.AddReveal(0, ProgressSim.Vanish, ProgressSim.Reveal("produce", Reaction.Applause, 18, -1, 1), acts[0].Crowd);
        Assert.False(DailyChallenges.Passed(challenge, mixed, ProgressSim.Summary(mixed, false, 1)), "a gasp in the only act");
        ProgressSim.CleanAct(mixed, acts, 1, Archetype.Kid);
        Assert.True(DailyChallenges.Passed(challenge, mixed, ProgressSim.Summary(mixed, false, 2)), "act 2 was all applause");

        // All applause, but a bust in the same act.
        ShowRecord busted = new ShowRecord(acts);
        busted.AddBust(0, ProgressSim.Vanish);
        ProgressSim.CleanAct(busted, acts, 0, Archetype.Kid);
        Assert.False(DailyChallenges.Passed(challenge, busted, ProgressSim.Summary(busted, false, 1)), "bust in the act");

        // All applause so far, but the act never finished (walked off).
        ShowRecord unfinished = new ShowRecord(acts);
        unfinished.AddReveal(0, ProgressSim.Vanish, ProgressSim.Reveal("vanish", Reaction.Applause, 15), acts[0].Crowd);
        Assert.False(DailyChallenges.Passed(challenge, unfinished, ProgressSim.Summary(unfinished, true, 0)), "unfinished act");
    }

    [Test]
    public static void TipTargetNeedsABowWithEnoughTips()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord clean = ProgressSim.CleanShow(acts, Archetype.Kid);
        ShowSummary s = ProgressSim.Summary(clean);
        Assert.True(DailyChallenges.Passed(new DailyChallenge(ChallengeKind.TipTarget, s.Tips), clean, s), "exactly the target");
        Assert.False(DailyChallenges.Passed(new DailyChallenge(ChallengeKind.TipTarget, s.Tips + 1), clean, s), "one short");
        ShowSummary walked = ProgressSim.Summary(clean, true, 2);
        Assert.False(DailyChallenges.Passed(new DailyChallenge(ChallengeKind.TipTarget, 10), clean, walked), "walked off with the tips");
    }

    [Test]
    public static void QuietWatcherFailsWhenTheTargetSawTheMethod()
    {
        var challenge = new DailyChallenge(ChallengeKind.QuietWatcher, (int)Archetype.Filmer);
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord clean = ProgressSim.CleanShow(acts, Archetype.Kid);
        Assert.True(DailyChallenges.Passed(challenge, clean, ProgressSim.Summary(clean)), "nobody heckled");

        // In the classic act 2 the Filmer is spectator 3, the Skeptic spectator 2.
        ShowRecord skepticSaw = ProgressSim.CleanShow(acts, Archetype.Kid);
        skepticSaw.AddReveal(1, ProgressSim.Cup, ProgressSim.Reveal("cup", Reaction.Heckle, 0, 2), acts[1].Crowd);
        Assert.True(DailyChallenges.Passed(challenge, skepticSaw, ProgressSim.Summary(skepticSaw)), "the Skeptic heckled, not the Filmer");

        ShowRecord filmerSaw = ProgressSim.CleanShow(acts, Archetype.Kid);
        filmerSaw.AddReveal(1, ProgressSim.Cup, ProgressSim.Reveal("cup", Reaction.Heckle, 0, 3), acts[1].Crowd);
        Assert.False(DailyChallenges.Passed(challenge, filmerSaw, ProgressSim.Summary(filmerSaw)), "the Filmer heckled");
        Assert.False(DailyChallenges.Passed(challenge, clean, ProgressSim.Summary(clean, true, 2)), "walked off");
        Assert.Equal("No heckles from the Filmer", challenge.Text);
    }
}

public static class ShowRecordTests
{
    [Test]
    public static void SpectatorIndexesBecomeArchetypes()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord record = new ShowRecord(acts);
        // Tourist (0), Kid (1), Skeptic (2): the Skeptic saw the ditch; later the coin comes from the Kid.
        record.AddReveal(0, 0, ProgressSim.Reveal("vanish", Reaction.Heckle, 0, 2), acts[0].Crowd);
        record.AddReveal(0, 0, ProgressSim.Reveal("produce", Reaction.Applause, 18, -1, 1), acts[0].Crowd);
        record.AddBust(1, 1);
        Assert.Equal(3, record.Events.Count);
        Assert.Equal((int)Archetype.Skeptic, record.Events[0].SeenBy, "heckler");
        Assert.Equal(ShowRecord.Nobody, record.Events[0].ProducedFrom, "a vanish produces from nobody");
        Assert.Equal(0, record.Events[0].Tips, "no tips for a heckle");
        Assert.Equal((int)Archetype.Kid, record.Events[1].ProducedFrom, "ear");
        Assert.Equal(ShowRecord.Nobody, record.Events[1].SeenBy);
        Assert.Equal(ShowEventKind.Bust, record.Events[2].Kind);
        Assert.Equal(2, record.RevealsIn(0));
        Assert.Equal(1, record.ApplauseIn(0));
        Assert.Equal(1, record.HecklesIn(0));
        Assert.Equal(0, record.BustsIn(0));
        Assert.Equal(1, record.BustsIn(1));
        Assert.Equal(0, record.RevealsIn(2));
    }

    [Test]
    public static void TheSignatureIsTheLastEarAndTheTrickTheShowEndedOn()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord record = new ShowRecord(acts);
        Assert.Equal(ShowRecord.Nobody, record.LastProducedFrom, "nothing yet");
        Assert.Equal(ShowRecord.Nobody, record.EndTrick, "nothing yet");
        ProgressSim.CleanAct(record, acts, 0, Archetype.Tourist);
        Assert.Equal((int)Archetype.Tourist, record.LastProducedFrom);
        Assert.Equal(0, record.EndTrick);
        record.AddBust(1, 1);
        Assert.Equal((int)Archetype.Tourist, record.LastProducedFrom, "still the last production");
        Assert.Equal(1, record.EndTrick, "ended in the cup act");
    }

    [Test]
    public static void ASmallRoomsRealCrowdReplacesThePlan()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord record = new ShowRecord(acts);
        Assert.True(record.CrowdHas(1, Archetype.Filmer), "planned");
        // Only three of act 2's four fitted the room.
        Archetype[] fitted = { Archetype.Tourist, Archetype.Kid, Archetype.Skeptic };
        record.AddReveal(1, 1, ProgressSim.Reveal("cup", Reaction.Applause, 20, -1, 3), fitted);
        Assert.False(record.CrowdHas(1, Archetype.Filmer), "the Filmer never fitted");
        Assert.Equal(3, record.CrowdOf(1).Length);
        Assert.Equal(ShowRecord.Nobody, record.Events[0].ProducedFrom, "an index past the crowd is nobody");
        Assert.Equal(1, record.TrickOf(1));
        Assert.Equal(ShowRecord.Nobody, record.TrickOf(7), "no such act");
        Assert.Equal(0, record.CrowdOf(7).Length, "no such act");
    }
}
