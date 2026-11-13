// Unit tests for the Day 10 "reason to come back" rules in Misdirection.Core.Progress: the daily
// crowd and challenge, the trick book, the returning Regular's memory, streaks and the save data.
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

    /// <summary>Plays a clean show of the day's plan and books it. Returns what changed.</summary>
    public static ShowOutcome PlayCleanShow(ProgressData data, int today, Archetype ear)
    {
        DailyPlan plan = ProgressRules.PlanFor(data, today);
        ShowRecord record = CleanShow(plan.Acts, ear);
        return ProgressRules.FinishShow(data, today, plan.Challenge, record, Summary(record));
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
        // A reveal reported with nobody placed keeps what is known about the act.
        record.AddReveal(1, 1, ProgressSim.Reveal("cup", Reaction.Gasp, 9), new Archetype[0]);
        record.AddReveal(0, 0, ProgressSim.Reveal("vanish", Reaction.Gasp, 9, 2), null);
        Assert.Equal(3, record.CrowdOf(1).Length, "still the three who fitted");
        Assert.Equal(3, record.CrowdOf(0).Length, "still the plan");
        Assert.Equal(ShowRecord.Nobody, record.Events[2].SeenBy, "nobody to name");
        Assert.Equal(1, record.TrickOf(1));
        Assert.Equal(ShowRecord.Nobody, record.TrickOf(7), "no such act");
        Assert.Equal(0, record.CrowdOf(7).Length, "no such act");
    }
}

public static class TrickBookTests
{
    private static TrickMastery[] NewBook()
    {
        return new[] { new TrickMastery(), new TrickMastery() };
    }

    [Test]
    public static void FirstCleanRevealEarnsTheFirstStarEvenIfTheActNeverFinishes()
    {
        TrickMastery[] book = NewBook();
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord record = new ShowRecord(acts);
        record.AddReveal(0, 0, ProgressSim.Reveal("vanish", Reaction.Heckle, 0, 2), acts[0].Crowd);
        int[] gained = TrickBook.Learn(book, record, 0);
        Assert.Equal(0, book[0].Stars, "a heckle earns nothing");
        Assert.Equal(0, gained[0]);

        record.AddReveal(0, 0, ProgressSim.Reveal("vanish", Reaction.Gasp, 8), acts[0].Crowd);
        gained = TrickBook.Learn(book, record, 0);
        Assert.Equal(1, book[0].Stars, "a gasp is a clean reveal");
        Assert.Equal(1, gained[0]);
        Assert.Equal(0, book[1].Stars, "the cup wasn't performed");
        Assert.Equal(0, book[0].Performances, "the act never finished");
    }

    [Test]
    public static void ApplauseOnEveryRevealOfAWholePerformanceEarnsTheSecond()
    {
        TrickMastery[] book = NewBook();
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord record = new ShowRecord(acts);
        ProgressSim.CleanAct(record, acts, 0, Archetype.Kid);
        int[] gained = TrickBook.Learn(book, record, 1);
        Assert.True(book[0].Clean && book[0].Ovation, "clean and ovation");
        Assert.False(book[0].ToughCrowd, "no Filmer in act 1");
        Assert.Equal(2, book[0].Stars);
        Assert.Equal(2, gained[0]);
        Assert.Equal(1, book[0].Performances);
    }

    [Test]
    public static void ABustAGaspOrABareLiftIsNotAnOvation()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();

        TrickMastery[] bust = NewBook();
        ShowRecord withBust = new ShowRecord(acts);
        withBust.AddBust(0, 0);
        ProgressSim.CleanAct(withBust, acts, 0, Archetype.Kid);
        TrickBook.Learn(bust, withBust, 1);
        Assert.Equal(1, bust[0].Stars, "bust in the performance");

        TrickMastery[] gasp = NewBook();
        ShowRecord withGasp = new ShowRecord(acts);
        withGasp.AddReveal(0, 0, ProgressSim.Reveal("vanish", Reaction.Gasp, 8), acts[0].Crowd);
        withGasp.AddReveal(0, 0, ProgressSim.Reveal("produce", Reaction.Applause, 18, -1, 1), acts[0].Crowd);
        TrickBook.Learn(gasp, withGasp, 1);
        Assert.Equal(1, gasp[0].Stars, "one gasp");

        // Lifting the cup without showing the vanish first is one reveal, not the whole trick.
        TrickMastery[] lift = NewBook();
        ShowRecord bareLift = new ShowRecord(acts);
        bareLift.AddReveal(1, 1, ProgressSim.Reveal("cup", Reaction.Applause, 20), acts[1].Crowd);
        TrickBook.Learn(lift, bareLift, 2);
        Assert.Equal(1, lift[1].Stars, "bare lift");

        // Not finished: the show ended (walk-off) before the act did.
        TrickMastery[] cut = NewBook();
        ShowRecord cutShort = new ShowRecord(acts);
        ProgressSim.CleanAct(cutShort, acts, 0, Archetype.Kid);
        TrickBook.Learn(cut, cutShort, 0);
        Assert.Equal(1, cut[0].Stars, "unfinished act");
    }

    [Test]
    public static void ToughCrowdStarNeedsTheSkepticAndTheFilmerAndNoHeckle()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();

        // Act 2 (cup) has both. Gasps are fine, a heckle is not.
        TrickMastery[] book = NewBook();
        ShowRecord gasps = new ShowRecord(acts);
        gasps.AddReveal(1, 1, ProgressSim.Reveal("vanish", Reaction.Gasp, 10), acts[1].Crowd);
        gasps.AddReveal(1, 1, ProgressSim.Reveal("cup", Reaction.Gasp, 12), acts[1].Crowd);
        TrickBook.Learn(book, gasps, 2);
        Assert.True(book[1].ToughCrowd, "unheckled in front of both");
        Assert.False(book[1].Ovation, "gasps aren't an ovation");
        Assert.Equal(2, book[1].Stars);

        TrickMastery[] heckledBook = NewBook();
        ShowRecord heckled = new ShowRecord(acts);
        heckled.AddReveal(1, 1, ProgressSim.Reveal("vanish", Reaction.Heckle, 0, 3), acts[1].Crowd);
        heckled.AddReveal(1, 1, ProgressSim.Reveal("cup", Reaction.Applause, 24), acts[1].Crowd);
        TrickBook.Learn(heckledBook, heckled, 2);
        Assert.False(heckledBook[1].ToughCrowd, "the Filmer heckled");

        // The same clean performance in a room that only fitted three people: no Filmer watching.
        TrickMastery[] smallRoom = NewBook();
        ShowRecord fitted = new ShowRecord(acts);
        Archetype[] three = { Archetype.Tourist, Archetype.Kid, Archetype.Skeptic };
        fitted.AddReveal(1, 1, ProgressSim.Reveal("vanish", Reaction.Applause, 15), three);
        fitted.AddReveal(1, 1, ProgressSim.Reveal("cup", Reaction.Applause, 18), three);
        TrickBook.Learn(smallRoom, fitted, 2);
        Assert.False(smallRoom[1].ToughCrowd, "no Filmer in the room");
        Assert.Equal(2, smallRoom[1].Stars, "clean and ovation");
    }

    [Test]
    public static void ACleanClassicShowMastersBothTricksAndStarsAreNeverLost()
    {
        TrickMastery[] book = NewBook();
        ShowAct[] acts = ProgressSim.ClassicActs();
        int[] gained = TrickBook.Learn(book, ProgressSim.CleanShow(acts, Archetype.Kid), 3);
        Assert.Equal(3, book[0].Stars, "vanish: act 3 has the Skeptic and the Filmer");
        Assert.Equal(3, book[1].Stars, "cup: act 2 has the Skeptic and the Filmer");
        Assert.Equal(3, gained[0]);
        Assert.Equal(3, gained[1]);
        Assert.Equal(2, book[0].Performances, "acts 1 and 3");
        Assert.Equal("", TrickBook.NextStarHint(book[0]), "nothing left to earn");

        // A terrible show afterwards takes nothing away.
        ShowRecord bad = new ShowRecord(acts);
        bad.AddReveal(0, 0, ProgressSim.Reveal("vanish", Reaction.Heckle, 0, 2), acts[0].Crowd);
        bad.AddBust(0, 0);
        gained = TrickBook.Learn(book, bad, 0);
        Assert.Equal(3, book[0].Stars);
        Assert.Equal(0, gained[0]);
    }

    [Test]
    public static void HintsNameTheNextStar()
    {
        TrickMastery m = new TrickMastery();
        Assert.True(TrickBook.NextStarHint(m).Contains("no heckle"), TrickBook.NextStarHint(m));
        m.Clean = true;
        Assert.True(TrickBook.NextStarHint(m).Contains("applause"), TrickBook.NextStarHint(m));
        m.Ovation = true;
        Assert.True(TrickBook.NextStarHint(m).Contains("Filmer"), TrickBook.NextStarHint(m));
        Assert.Equal("Vanish", TrickBook.NameOf(0));
        Assert.Equal("Cup", TrickBook.NameOf(1));
    }
}

public static class RegularTests
{
    private static RegularMemory Remembered(Archetype ear, int repeats, int date = ProgressSim.Day1)
    {
        return new RegularMemory { Date = date, ProducedFrom = (int)ear, EndTrick = ProgressSim.Vanish, Repeats = repeats };
    }

    /// <summary>A Regular across the table whose cone has settled on the hands.</summary>
    private static SpectatorMind WatchingRegular(out CrowdModel crowd)
    {
        crowd = new CrowdModel();
        SpectatorMind regular = crowd.Add(SpectatorProfile.For(Archetype.Regular), Stage.SkepticEyes);
        Stage.Run(crowd, 0, 2.0, Stage.Hands);
        return regular;
    }

    [Test]
    public static void WithNothingRememberedHeIsAnOrdinarySpectator()
    {
        RegularAttitude a = RegularRule.AttitudeFor(new RegularMemory(), ProgressSim.Vanish);
        Assert.False(a.Wary);
        Assert.Near(1.0, a.SuspicionGainScale);
        Assert.Near(0.0, a.SuspicionFloor);
        Assert.Equal(RegularReaction.None, RegularRule.OnProduce(new RegularMemory(), Archetype.Kid));
        Assert.False(RegularRule.AttitudeFor(null, ProgressSim.Vanish).Wary, "no memory at all");
        Assert.False(RegularRule.AttitudeFor(Remembered(Archetype.Kid, 0), ProgressSim.Vanish).Wary, "seen once: not wary yet");
    }

    [Test]
    public static void RepeatingTheSignatureMakesHimWaryAndVaryingRelaxesHim()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();
        RegularMemory memory = new RegularMemory();

        RegularRule.Remember(memory, ProgressSim.CleanShow(acts, Archetype.Kid), ProgressSim.Day1);
        Assert.True(memory.HasSignature);
        Assert.Equal((int)Archetype.Kid, memory.ProducedFrom);
        Assert.Equal(ProgressSim.Vanish, memory.EndTrick, "the show ends on the vanish");
        Assert.Equal(0, memory.Repeats, "seen once");

        // Day 2: the same ear again.
        Assert.Equal(RegularReaction.SameAgain, RegularRule.OnProduce(memory, Archetype.Kid));
        RegularRule.Remember(memory, ProgressSim.CleanShow(acts, Archetype.Kid), ProgressSim.Day2);
        Assert.Equal(1, memory.Repeats);
        Assert.Equal(ProgressSim.Day2, memory.Date);
        RegularAttitude wary = RegularRule.AttitudeFor(memory, ProgressSim.Vanish);
        Assert.True(wary.Wary, "wary after a repeat");
        Assert.True(wary.SuspicionGainScale > 1.2f, "suspects faster: x" + wary.SuspicionGainScale);
        Assert.True(wary.SuspicionFloor >= new AttentionSettings().CuriousAt, "starts Curious");

        // Day 3: the same again; warier still.
        RegularRule.Remember(memory, ProgressSim.CleanShow(acts, Archetype.Kid), ProgressSim.Day3);
        Assert.Equal(2, memory.Repeats);
        Assert.True(RegularRule.AttitudeFor(memory, ProgressSim.Vanish).SuspicionGainScale > wary.SuspicionGainScale, "more with every repeat");

        // Then a different ear: he relaxes.
        Assert.Equal(RegularReaction.SomethingNew, RegularRule.OnProduce(memory, Archetype.Skeptic));
        RegularRule.Remember(memory, ProgressSim.CleanShow(acts, Archetype.Skeptic), 20261114);
        Assert.Equal(0, memory.Repeats, "a new signature starts over");
        Assert.Equal((int)Archetype.Skeptic, memory.ProducedFrom);
        Assert.False(RegularRule.AttitudeFor(memory, ProgressSim.Vanish).Wary, "relaxed again");
    }

    [Test]
    public static void WarinessIsCappedAndOnlyForTheTrickHeKeepsSeeing()
    {
        RegularSettings settings = new RegularSettings();
        float atCap = RegularRule.AttitudeFor(Remembered(Archetype.Kid, settings.MaxRepeats), ProgressSim.Vanish, settings).SuspicionGainScale;
        float beyond = RegularRule.AttitudeFor(Remembered(Archetype.Kid, 50), ProgressSim.Vanish, settings).SuspicionGainScale;
        Assert.Near(atCap, beyond, 1e-5, "capped");
        Assert.Near(1.0 + settings.GainPerRepeat * settings.MaxRepeats, atCap, 1e-5);
        Assert.False(RegularRule.AttitudeFor(Remembered(Archetype.Kid, 3), ProgressSim.Cup, settings).Wary, "the cup is not the method he remembers");
    }

    [Test]
    public static void AShowWithoutAProductionLeavesHisMemoryAlone()
    {
        ShowAct[] acts = ProgressSim.ClassicActs();
        RegularMemory memory = Remembered(Archetype.Kid, 1);
        ShowRecord walkedOff = new ShowRecord(acts);
        walkedOff.AddBust(0, 0);
        walkedOff.AddBust(0, 0);
        walkedOff.AddBust(0, 0);
        RegularRule.Remember(memory, walkedOff, ProgressSim.Day2);
        Assert.Equal(1, memory.Repeats);
        Assert.Equal(ProgressSim.Day1, memory.Date);
        Assert.Equal((int)Archetype.Kid, memory.ProducedFrom);

        // The same ear but the show ended on the other trick (walked off in the cup act): a different signature.
        ShowRecord endedOnCup = new ShowRecord(acts);
        ProgressSim.CleanAct(endedOnCup, acts, 0, Archetype.Kid);
        endedOnCup.AddBust(1, 1);
        RegularRule.Remember(memory, endedOnCup, ProgressSim.Day2);
        Assert.Equal(0, memory.Repeats, "different ending");
        Assert.Equal(ProgressSim.Cup, memory.EndTrick);
    }

    [Test]
    public static void HeSaysYesterdayOnlyWhenItWasYesterday()
    {
        RegularMemory memory = Remembered(Archetype.Kid, 0, ProgressSim.Day1);
        Assert.Equal("Same as yesterday!", RegularRule.Line(RegularReaction.SameAgain, memory, ProgressSim.Day2));
        Assert.Equal("Same as last time!", RegularRule.Line(RegularReaction.SameAgain, memory, ProgressSim.Day3));
        Assert.Equal("Same as last time!", RegularRule.Line(RegularReaction.SameAgain, memory, ProgressSim.Day1), "earlier today");
        Assert.Equal("That's new!", RegularRule.Line(RegularReaction.SomethingNew, memory, ProgressSim.Day2));
        Assert.Equal("", RegularRule.Line(RegularReaction.None, memory, ProgressSim.Day2));
    }

    [Test]
    public static void AWaryRegularSuspectsFasterAndStaysCurious()
    {
        CrowdModel calmCrowd;
        CrowdModel waryCrowd;
        SpectatorMind calm = WatchingRegular(out calmCrowd);
        SpectatorMind wary = WatchingRegular(out waryCrowd);
        RegularAttitude attitude = RegularRule.AttitudeFor(Remembered(Archetype.Kid, 2), ProgressSim.Vanish);
        RegularRule.Apply(wary, attitude);
        Assert.Equal(SuspicionLevel.Curious, wary.Level, "walks in Curious");
        Assert.Equal(SuspicionLevel.Calm, calm.Level);
        Assert.Near(attitude.SuspicionGainScale, wary.SuspicionGainScale, 1e-5);

        // Both see the same sloppy move at the hands.
        float calmBefore = calm.Suspicion;
        float waryBefore = wary.Suspicion;
        Assert.True(calm.Witness(Stage.Hands, 0.3f) > 0.9f, "he was watching the hands");
        wary.Witness(Stage.Hands, 0.3f);
        float calmGain = calm.Suspicion - calmBefore;
        float waryGain = wary.Suspicion - waryBefore;
        Assert.Near(calmGain * attitude.SuspicionGainScale, waryGain, 1e-4, "gains suspicion faster");

        // A minute of nothing: the calm one forgets, the wary one stays Curious. A new trick's reset keeps the floor too.
        Stage.Run(calmCrowd, 2.0, 60, Stage.Mat);
        Stage.Run(waryCrowd, 2.0, 60, Stage.Mat);
        Assert.Near(0.0, calm.Suspicion, 1e-4, "suspicion decays");
        Assert.Near(attitude.SuspicionFloor, wary.Suspicion, 1e-4, "never below the floor");
        wary.ResetSuspicion();
        Assert.Equal(SuspicionLevel.Curious, wary.Level, "still Curious after a reset");
    }

    [Test]
    public static void HisReactionOnTheSpot()
    {
        CrowdModel crowd;
        SpectatorMind regular = WatchingRegular(out crowd);
        RegularSettings settings = new RegularSettings { RepeatBump = 0.4f };
        RegularRule.Apply(regular, RegularRule.AttitudeFor(Remembered(Archetype.Kid, 1), ProgressSim.Vanish, settings));
        float before = regular.Suspicion;

        RegularRule.React(regular, RegularReaction.SameAgain, settings);
        Assert.Near(before + settings.RepeatBump, regular.Suspicion, 1e-4, "the same ear again raises suspicion");
        Assert.Equal(SuspicionLevel.Suspicious, regular.Level);

        RegularRule.React(regular, RegularReaction.SomethingNew, settings);
        Assert.Near(0.0, regular.Suspicion, 1e-5, "something new clears it");
        Assert.Near(1.0, regular.SuspicionGainScale, 1e-5, "and his wariness");
        Assert.Near(0.0, regular.SuspicionFloor, 1e-5);

        RegularRule.React(regular, RegularReaction.None, settings);
        Assert.Near(0.0, regular.Suspicion, 1e-5, "nothing to react to");
        RegularRule.React(null, RegularReaction.SameAgain, settings);
        RegularRule.Apply(null, new RegularAttitude());
    }

    [Test]
    public static void OtherSpectatorsAreUntouchedByDefault()
    {
        // The hook defaults to "normal": an unprimed mind behaves exactly as before Day 10.
        var crowd = Stage.ThreeSpectators();
        Stage.Run(crowd, 0, 1.0, Stage.Hands);
        SpectatorMind skeptic = crowd.Spectators[2];
        Assert.Near(1.0, skeptic.SuspicionGainScale);
        Assert.Near(0.0, skeptic.SuspicionFloor);
        skeptic.AddSuspicion(0.5f);
        skeptic.ResetSuspicion();
        Assert.Near(0.0, skeptic.Suspicion, 1e-6);
        skeptic.SetWariness(-3f, 99f);
        Assert.Near(0.0, skeptic.SuspicionGainScale, 1e-6, "scale can't go negative");
        Assert.Near(new AttentionSettings().MaxSuspicion, skeptic.SuspicionFloor, 1e-6, "floor is capped at the maximum");
    }
}

public static class ProgressRulesTests
{
    [Test]
    public static void TheFirstShowStartsDayOneAndAStreakOfOne()
    {
        ProgressData data = new ProgressData();
        Assert.False(ProgressRules.IsReturning(data, ProgressSim.Day1), "never played");
        Assert.Equal(1, ProgressRules.DayNumber(data, ProgressSim.Day1));
        Assert.Equal(0, ProgressRules.StreakOn(data, ProgressSim.Day1));

        ShowOutcome o = ProgressSim.PlayCleanShow(data, ProgressSim.Day1, Archetype.Kid);
        Assert.True(o.NewDay);
        Assert.Equal(1, o.DayNumber);
        Assert.Equal(1, o.Streak);
        Assert.Equal(1, data.ShowsPlayed);
        Assert.Equal(1, data.ShowsFinished);
        Assert.Equal(1, data.DaysPlayed);
        Assert.Equal(ProgressSim.Day1, data.FirstPlayedDate);
        Assert.Equal(ProgressSim.Day1, data.LastPlayedDate);
        Assert.Equal(1, data.CurrentStreak);
        Assert.Equal(1, data.LongestStreak);
        Assert.Equal(3, data.BestStars);
        Assert.True(data.BestTips > 0, "tips booked");
        Assert.False(o.NewBestTips, "the very first show isn't a 'new best'");
        Assert.False(ProgressRules.IsReturning(data, ProgressSim.Day1), "still the first day");
        Assert.Equal(1, ProgressRules.DayNumber(data, ProgressSim.Day1));
    }

    [Test]
    public static void ConsecutiveDaysGrowTheStreakAndASecondShowTodayDoesNot()
    {
        ProgressData data = new ProgressData();
        ProgressSim.PlayCleanShow(data, ProgressSim.Day1, Archetype.Kid);
        ShowOutcome again = ProgressSim.PlayCleanShow(data, ProgressSim.Day1, Archetype.Kid);
        Assert.False(again.NewDay, "same day");
        Assert.Equal(1, again.Streak);
        Assert.Equal(1, data.DaysPlayed);
        Assert.Equal(2, data.ShowsPlayed);

        Assert.Equal(2, ProgressRules.DayNumber(data, ProgressSim.Day2), "about to be day 2");
        Assert.Equal(1, ProgressRules.StreakOn(data, ProgressSim.Day2), "yesterday's streak is still alive");
        ShowOutcome day2 = ProgressSim.PlayCleanShow(data, ProgressSim.Day2, Archetype.Kid);
        Assert.True(day2.NewDay);
        Assert.Equal(2, day2.DayNumber);
        Assert.Equal(2, day2.Streak);
        ShowOutcome day3 = ProgressSim.PlayCleanShow(data, ProgressSim.Day3, Archetype.Kid);
        Assert.Equal(3, day3.Streak);
        Assert.Equal(3, data.LongestStreak);
        Assert.Equal(3, data.DaysPlayed);
        Assert.Equal(ProgressSim.Day1, data.FirstPlayedDate);
    }

    [Test]
    public static void AMissedDayStartsTheStreakAgainButKeepsTheLongest()
    {
        ProgressData data = new ProgressData();
        ProgressSim.PlayCleanShow(data, ProgressSim.Day1, Archetype.Kid);
        ProgressSim.PlayCleanShow(data, ProgressSim.Day2, Archetype.Kid);
        ProgressSim.PlayCleanShow(data, ProgressSim.Day3, Archetype.Kid);
        Assert.Equal(0, ProgressRules.StreakOn(data, 20261116), "two days missed: broken");
        ShowOutcome late = ProgressSim.PlayCleanShow(data, 20261116, Archetype.Kid);
        Assert.Equal(1, late.Streak, "starts again");
        Assert.Equal(4, late.DayNumber, "the fourth day played");
        Assert.Equal(3, data.LongestStreak, "longest kept");
        Assert.Equal(1, data.CurrentStreak);
        // Across a year end.
        ProgressSim.PlayCleanShow(data, 20261231, Archetype.Kid);
        ShowOutcome newYear = ProgressSim.PlayCleanShow(data, 20270101, Archetype.Kid);
        Assert.Equal(2, newYear.Streak, "31 Dec then 1 Jan is a streak");
    }

    [Test]
    public static void AWalkOffStillCountsAsPlayingToday()
    {
        ProgressData data = new ProgressData();
        ProgressSim.PlayCleanShow(data, ProgressSim.Day1, Archetype.Kid);
        DailyPlan plan = ProgressRules.PlanFor(data, ProgressSim.Day2);
        ShowRecord record = new ShowRecord(plan.Acts);
        record.AddBust(0, 0);
        record.AddBust(0, 0);
        record.AddBust(0, 0);
        ShowOutcome o = ProgressRules.FinishShow(data, ProgressSim.Day2, plan.Challenge, record, ProgressSim.Summary(record, true, 0));
        Assert.Equal(2, o.Streak, "turning up keeps the streak");
        Assert.Equal(2, data.ShowsPlayed);
        Assert.Equal(1, data.ShowsFinished, "but it wasn't a finished show");
        Assert.False(o.ChallengePassedNow);
        Assert.Equal(3, data.BestStars, "bests untouched");
    }

    [Test]
    public static void AClockThatGoesBackwardsChangesNoDays()
    {
        ProgressData data = new ProgressData();
        ProgressSim.PlayCleanShow(data, ProgressSim.Day2, Archetype.Kid);
        ProgressSim.PlayCleanShow(data, ProgressSim.Day3, Archetype.Kid);
        ShowOutcome back = ProgressSim.PlayCleanShow(data, ProgressSim.Day1, Archetype.Kid);
        Assert.False(back.NewDay);
        Assert.Equal(2, data.DaysPlayed);
        Assert.Equal(2, data.CurrentStreak);
        Assert.Equal(ProgressSim.Day3, data.LastPlayedDate, "the last day played doesn't move back");
        Assert.Equal(3, data.ShowsPlayed, "the show still counts");

        // No usable date at all: the show counts, the calendar doesn't.
        ProgressData undated = new ProgressData();
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord record = ProgressSim.CleanShow(acts, Archetype.Kid);
        ShowOutcome o = ProgressRules.FinishShow(undated, 0, new DailyChallenge(ChallengeKind.NoBusts, 0), record, ProgressSim.Summary(record));
        Assert.Equal(1, undated.ShowsPlayed);
        Assert.Equal(0, undated.DaysPlayed);
        Assert.Equal(0, undated.Challenges.Length, "no date, no history entry");
        Assert.True(o.ChallengePassedNow);
        Assert.True(o.ChallengePassedToday);
    }

    [Test]
    public static void BestsOnlyGoUpAndSayWhenTheyAreBeaten()
    {
        ProgressData data = new ProgressData();
        ShowAct[] acts = ProgressSim.ClassicActs();
        var challenge = new DailyChallenge(ChallengeKind.NoBusts, 0);

        // A modest first show: one clean act, then two busts.
        ShowRecord modest = new ShowRecord(acts);
        ProgressSim.CleanAct(modest, acts, 0, Archetype.Kid);
        modest.AddBust(1, 1);
        modest.AddBust(1, 1);
        ProgressSim.CleanAct(modest, acts, 1, Archetype.Kid);
        ProgressSim.CleanAct(modest, acts, 2, Archetype.Kid);
        ShowSummary modestSummary = ProgressSim.Summary(modest);
        modestSummary.Tips = 40;
        ProgressRules.FinishShow(data, ProgressSim.Day1, challenge, modest, modestSummary);
        Assert.Equal(40, data.BestTips);
        Assert.Equal(1, data.BestStars, "two busts: one star");

        ShowRecord clean = ProgressSim.CleanShow(acts, Archetype.Kid);
        ShowSummary cleanSummary = ProgressSim.Summary(clean);
        ShowOutcome better = ProgressRules.FinishShow(data, ProgressSim.Day1, challenge, clean, cleanSummary);
        Assert.True(better.NewBestTips, "new best tips");
        Assert.True(better.NewBestStars, "new best stars");
        Assert.Equal(cleanSummary.Tips, data.BestTips);
        Assert.Equal(3, data.BestStars);
        Assert.Equal(40 + cleanSummary.Tips, data.TotalTips);

        ShowOutcome worse = ProgressRules.FinishShow(data, ProgressSim.Day1, challenge, modest, modestSummary);
        Assert.False(worse.NewBestTips);
        Assert.Equal(cleanSummary.Tips, data.BestTips, "a worse show doesn't lower the best");
        Assert.Equal(3, data.BestStars);
    }

    [Test]
    public static void DayTwoVisiblyDiffersFromDayOne()
    {
        ProgressData data = new ProgressData();
        DailyPlan day1 = ProgressRules.PlanFor(data, ProgressSim.Day1);
        Assert.False(day1.RegularReturns, "day one: no Regular");
        Assert.Equal(0, ProgressSim.Count(day1.Acts[2].Crowd, Archetype.Regular));
        ProgressSim.PlayCleanShow(data, ProgressSim.Day1, Archetype.Kid);

        // Later the same day: still day one's show.
        DailyPlan again = ProgressRules.PlanFor(data, ProgressSim.Day1);
        Assert.True(ProgressSim.SameShow(day1, again), "same day, same show");
        Assert.Equal(day1.Challenge.Kind, again.Challenge.Kind);

        // The next day: the Regular is back, the line-up and the challenge are new, and it says "Day 2".
        Assert.True(ProgressRules.IsReturning(data, ProgressSim.Day2));
        DailyPlan day2 = ProgressRules.PlanFor(data, ProgressSim.Day2);
        Assert.True(day2.RegularReturns, "day two: the Regular returns");
        Assert.Equal(1, ProgressSim.Count(day2.Acts[2].Crowd, Archetype.Regular));
        Assert.False(ProgressSim.SameShow(day1, day2), "a different crowd");
        Assert.False(day1.Challenge.Kind == day2.Challenge.Kind, "a different challenge");
        Assert.Equal(2, ProgressRules.DayNumber(data, ProgressSim.Day2));
        Assert.False(ProgressText.CrowdLine(day1.Acts) == ProgressText.CrowdLine(day2.Acts), "the banner reads differently");

        // And he remembers day one: the same ear again is "Same as yesterday!".
        Assert.Equal(RegularReaction.SameAgain, RegularRule.OnProduce(data.Regular, Archetype.Kid));
        Assert.Equal("Same as yesterday!", RegularRule.Line(RegularReaction.SameAgain, data.Regular, ProgressSim.Day2));
        ProgressSim.PlayCleanShow(data, ProgressSim.Day2, Archetype.Kid);
        Assert.True(RegularRule.AttitudeFor(data.Regular, ProgressSim.Vanish).Wary, "day three: he walks in wary");
    }

    [Test]
    public static void AShowFillsInTheTrickBook()
    {
        ProgressData data = new ProgressData();
        ShowAct[] acts = ProgressSim.ClassicActs();
        ShowRecord record = ProgressSim.CleanShow(acts, Archetype.Kid);
        ShowOutcome o = ProgressRules.FinishShow(data, ProgressSim.Day1, new DailyChallenge(ChallengeKind.NoBusts, 0), record, ProgressSim.Summary(record));
        Assert.Equal(2, o.StarsGained.Length);
        Assert.Equal(3, o.StarsGained[0]);
        Assert.Equal(3, o.StarsGained[1]);
        Assert.Equal(3, data.Tricks[0].Stars);
        ShowOutcome second = ProgressRules.FinishShow(data, ProgressSim.Day1, new DailyChallenge(ChallengeKind.NoBusts, 0), record, ProgressSim.Summary(record));
        Assert.Equal(0, second.StarsGained[0], "nothing new the second time");
        Assert.Equal(1, data.Regular.Repeats, "and the Regular has now seen it twice");
    }

    [Test]
    public static void TheChallengeStaysPassedForTheDayAndHistoryKeepsTwoWeeks()
    {
        ProgressData data = new ProgressData();
        ShowAct[] acts = ProgressSim.ClassicActs();
        var challenge = new DailyChallenge(ChallengeKind.NoBusts, 0);
        ShowRecord clean = ProgressSim.CleanShow(acts, Archetype.Kid);
        ShowRecord busted = ProgressSim.CleanShow(acts, Archetype.Kid);
        busted.AddBust(2, 0);

        ShowOutcome miss = ProgressRules.FinishShow(data, ProgressSim.Day1, challenge, busted, ProgressSim.Summary(busted));
        Assert.False(miss.ChallengePassedToday);
        Assert.False(ProgressRules.ChallengePassedOn(data, ProgressSim.Day1));
        ShowOutcome hit = ProgressRules.FinishShow(data, ProgressSim.Day1, challenge, clean, ProgressSim.Summary(clean));
        Assert.True(hit.ChallengePassedNow);
        Assert.True(ProgressRules.ChallengePassedOn(data, ProgressSim.Day1));
        ShowOutcome missAgain = ProgressRules.FinishShow(data, ProgressSim.Day1, challenge, busted, ProgressSim.Summary(busted));
        Assert.False(missAgain.ChallengePassedNow, "this show missed it");
        Assert.True(missAgain.ChallengePassedToday, "but it was already met today");
        Assert.Equal(1, data.Challenges.Length, "one entry per day");

        // Twenty more days, missing the challenge on odd ones.
        for (int i = 1; i <= 20; i++)
        {
            int day = CalendarDay.AddDays(ProgressSim.Day1, i);
            ShowRecord record = i % 2 == 0 ? clean : busted;
            ProgressRules.FinishShow(data, day, challenge, record, ProgressSim.Summary(record));
        }
        Assert.Equal(ProgressData.HistoryDays, data.Challenges.Length, "two weeks kept");
        Assert.Equal(CalendarDay.AddDays(ProgressSim.Day1, 20), data.Challenges[data.Challenges.Length - 1].Date, "newest last");
        Assert.Equal(CalendarDay.AddDays(ProgressSim.Day1, 7), data.Challenges[0].Date, "oldest first");
        Assert.True(ProgressRules.ChallengePassedOn(data, CalendarDay.AddDays(ProgressSim.Day1, 20)));
        Assert.False(ProgressRules.ChallengePassedOn(data, CalendarDay.AddDays(ProgressSim.Day1, 19)));
        Assert.False(ProgressRules.ChallengePassedOn(data, ProgressSim.Day1), "day one has dropped off the history");
        Assert.Equal(21, data.CurrentStreak);
    }
}

public static class ProgressSaveTests
{
    [Test]
    public static void NothingLoadedIsAFreshSave()
    {
        ProgressData data = ProgressSave.Upgrade(null);
        Assert.Equal(ProgressData.CurrentVersion, data.Version);
        Assert.Equal(0, data.ShowsPlayed);
        Assert.Equal(0, data.DaysPlayed);
        Assert.Equal(TrickBook.TrickCount, data.Tricks.Length);
        Assert.Equal(0, data.Tricks[0].Stars);
        Assert.Equal(0, data.Challenges.Length);
        Assert.False(data.Regular.HasSignature);
        Assert.False(ProgressRules.IsReturning(data, ProgressSim.Day1));
    }

    [Test]
    public static void BrokenValuesAreRepaired()
    {
        ProgressData data = new ProgressData();
        data.ShowsPlayed = -4;
        data.ShowsFinished = 9;
        data.BestTips = -1;
        data.BestStars = 17;
        data.DaysPlayed = 2;
        data.FirstPlayedDate = 20269999;
        data.LastPlayedDate = ProgressSim.Day2;
        data.CurrentStreak = 40;
        data.LongestStreak = -2;
        data.Tricks = null;
        data.Challenges = new[]
        {
            null,
            new ChallengeResult { Date = ProgressSim.Day2, Kind = 1, Passed = false },
            new ChallengeResult { Date = 20261340, Kind = 2, Passed = true },
            new ChallengeResult { Date = ProgressSim.Day1, Kind = 0, Passed = true },
            new ChallengeResult { Date = ProgressSim.Day2, Kind = 1, Passed = true },
        };
        data.Regular = null;

        ProgressData fixedUp = ProgressSave.Upgrade(data);
        Assert.Equal(2, fixedUp.ShowsPlayed, "at least one show per day played");
        Assert.Equal(2, fixedUp.ShowsFinished, "no more bows than shows");
        Assert.Equal(0, fixedUp.BestTips);
        Assert.Equal(3, fixedUp.BestStars);
        Assert.Equal(ProgressSim.Day2, fixedUp.FirstPlayedDate, "an unreadable first date falls back to the last");
        Assert.Equal(2, fixedUp.CurrentStreak, "a streak can't be longer than the days played");
        Assert.Equal(2, fixedUp.LongestStreak);
        Assert.Equal(2, fixedUp.Tricks.Length);
        Assert.True(fixedUp.Tricks[0] != null && fixedUp.Tricks[1] != null, "trick entries");
        Assert.Equal(2, fixedUp.Challenges.Length, "null, invalid and duplicate entries dropped");
        Assert.Equal(ProgressSim.Day1, fixedUp.Challenges[0].Date, "oldest first");
        Assert.True(fixedUp.Challenges[1].Passed, "a day passed once stays passed");
        Assert.True(fixedUp.Regular != null && !fixedUp.Regular.HasSignature, "empty memory");

        // Never played but with leftovers: back to a clean slate.
        ProgressData leftovers = new ProgressData { DaysPlayed = 5, CurrentStreak = 3, LongestStreak = 9, LastPlayedDate = -1 };
        leftovers.Regular = new RegularMemory { Date = ProgressSim.Day1, ProducedFrom = 42, Repeats = -3 };
        leftovers.Tricks = new TrickMastery[] { null };
        ProgressSave.Upgrade(leftovers);
        Assert.Equal(0, leftovers.DaysPlayed);
        Assert.Equal(0, leftovers.CurrentStreak);
        Assert.Equal(0, leftovers.LongestStreak);
        Assert.Equal(0, leftovers.LastPlayedDate);
        Assert.False(leftovers.Regular.HasSignature, "an archetype that doesn't exist is forgotten");
        Assert.Equal(2, leftovers.Tricks.Length);
        Assert.True(leftovers.Tricks[0] != null, "null trick entry replaced");
    }

    [Test]
    public static void OlderAndNewerVersionsBothLoad()
    {
        // From before the version field existed.
        ProgressData old = new ProgressData { Version = 0, ShowsPlayed = 3, DaysPlayed = 2, LastPlayedDate = ProgressSim.Day2, CurrentStreak = 2 };
        Assert.False(ProgressSave.IsFromNewerBuild(old));
        ProgressSave.Upgrade(old);
        Assert.Equal(ProgressData.CurrentVersion, old.Version);
        Assert.Equal(3, old.ShowsPlayed);
        Assert.Equal(2, old.CurrentStreak);

        // From a newer build with a third trick: what this build understands is kept, nothing is thrown away.
        ProgressData newer = new ProgressData { Version = ProgressData.CurrentVersion + 5, BestTips = 77, LastPlayedDate = ProgressSim.Day1, DaysPlayed = 1 };
        newer.Tricks = new[] { new TrickMastery { Clean = true }, new TrickMastery(), new TrickMastery { Clean = true, Ovation = true } };
        newer.Challenges = new[] { new ChallengeResult { Date = ProgressSim.Day1, Kind = 99, Passed = true } };
        Assert.True(ProgressSave.IsFromNewerBuild(newer));
        ProgressSave.Upgrade(newer);
        Assert.Equal(ProgressData.CurrentVersion, newer.Version, "written back in this build's format");
        Assert.Equal(77, newer.BestTips);
        Assert.Equal(3, newer.Tricks.Length, "the unknown trick's stars are kept");
        Assert.Equal(2, newer.Tricks[2].Stars);
        Assert.Equal(99, newer.Challenges[0].Kind, "an unknown challenge kind survives");
        Assert.False(ProgressSave.IsFromNewerBuild(null));
    }

    [Test]
    public static void MergeKeepsEverythingEarnedInEitherCopy()
    {
        // The file: played up to day 3. The backup: one save older, but (say) with a star the file lost.
        ProgressData file = new ProgressData
        {
            ShowsPlayed = 8, ShowsFinished = 6, BestTips = 90, BestStars = 2, TotalTips = 300, DaysPlayed = 6,
            FirstPlayedDate = 20261103, LastPlayedDate = ProgressSim.Day3, CurrentStreak = 3, LongestStreak = 3,
        };
        file.Tricks[0].Clean = true;
        file.Tricks[0].Performances = 6;
        file.Challenges = new[]
        {
            new ChallengeResult { Date = ProgressSim.Day2, Kind = 1, Passed = false },
            new ChallengeResult { Date = ProgressSim.Day3, Kind = 2, Passed = true },
        };
        file.Regular = new RegularMemory { Date = ProgressSim.Day3, ProducedFrom = (int)Archetype.Kid, EndTrick = 0, Repeats = 2 };

        ProgressData backup = new ProgressData
        {
            ShowsPlayed = 7, ShowsFinished = 7, BestTips = 110, BestStars = 3, TotalTips = 260, DaysPlayed = 5,
            FirstPlayedDate = 20261101, LastPlayedDate = ProgressSim.Day2, CurrentStreak = 2, LongestStreak = 4,
        };
        backup.Tricks[0].Ovation = true;
        backup.Tricks[1].Clean = true;
        backup.Tricks[0].Performances = 4;
        backup.Challenges = new[]
        {
            new ChallengeResult { Date = ProgressSim.Day1, Kind = 0, Passed = true },
            new ChallengeResult { Date = ProgressSim.Day2, Kind = 1, Passed = true },
        };
        backup.Regular = new RegularMemory { Date = ProgressSim.Day2, ProducedFrom = (int)Archetype.Skeptic, EndTrick = 0, Repeats = 0 };

        foreach (bool swapped in new[] { false, true })
        {
            ProgressData m = swapped ? ProgressSave.Merge(backup, file) : ProgressSave.Merge(file, backup);
            string order = swapped ? " (backup first)" : " (file first)";
            Assert.Equal(8, m.ShowsPlayed, "shows" + order);
            Assert.Equal(7, m.ShowsFinished, "bows" + order);
            Assert.Equal(110, m.BestTips, "best tips" + order);
            Assert.Equal(3, m.BestStars, "best stars" + order);
            Assert.Equal(300, m.TotalTips, "total tips" + order);
            Assert.Equal(6, m.DaysPlayed, "days" + order);
            Assert.Equal(20261101, m.FirstPlayedDate, "the earliest first day" + order);
            Assert.Equal(ProgressSim.Day3, m.LastPlayedDate, "last day" + order);
            Assert.Equal(3, m.CurrentStreak, "the streak of the copy played most recently" + order);
            Assert.Equal(4, m.LongestStreak, "longest" + order);
            Assert.Equal(2, m.Tricks[0].Stars, "vanish stars from both" + order);
            Assert.Equal(1, m.Tricks[1].Stars, "cup star from the backup" + order);
            Assert.Equal(6, m.Tricks[0].Performances, "performances" + order);
            Assert.Equal(3, m.Challenges.Length, "one entry per day" + order);
            Assert.True(m.Challenges[1].Passed, "day 2 was passed in the backup" + order);
            Assert.Equal((int)Archetype.Kid, m.Regular.ProducedFrom, "the newer memory" + order);
            Assert.Equal(2, m.Regular.Repeats, "the newer memory" + order);
        }
        Assert.Equal(false, file.Challenges[0].Passed, "merging doesn't change its inputs");
        Assert.False(file.Tricks[0].Ovation, "merging doesn't change its inputs");
    }

    [Test]
    public static void MergeCopesWithMissingCopies()
    {
        ProgressData only = new ProgressData { ShowsPlayed = 2, DaysPlayed = 1, LastPlayedDate = ProgressSim.Day1, CurrentStreak = 1, BestTips = 50 };
        ProgressData a = ProgressSave.Merge(only, null);
        ProgressData b = ProgressSave.Merge(null, only);
        Assert.Equal(2, a.ShowsPlayed);
        Assert.Equal(2, b.ShowsPlayed);
        Assert.Equal(50, a.BestTips);
        Assert.Equal(ProgressSim.Day1, b.LastPlayedDate);
        ProgressData empty = ProgressSave.Merge(null, null);
        Assert.Equal(0, empty.ShowsPlayed);
        Assert.Equal(TrickBook.TrickCount, empty.Tricks.Length);
    }
}

public static class ProgressTextTests
{
    [Test]
    public static void TheCrowdLineNamesWhoStartsAndWhoJoins()
    {
        Assert.Equal("Tourist, Kid, Skeptic + Filmer + Regular", ProgressText.CrowdLine(ProgressSim.ClassicActs()));
        // A second Kid joining in the middle of the arc is still named as the newcomer.
        ShowAct[] acts =
        {
            new ShowAct(0, Archetype.Kid, Archetype.Tourist, Archetype.Filmer),
            new ShowAct(1, Archetype.Kid, Archetype.Kid, Archetype.Tourist, Archetype.Filmer),
            new ShowAct(0, Archetype.Skeptic, Archetype.Kid, Archetype.Kid, Archetype.Tourist, Archetype.Filmer),
        };
        Assert.Equal("Kid, Tourist, Filmer + Kid + Skeptic", ProgressText.CrowdLine(acts));
        Assert.Equal("", ProgressText.CrowdLine(null));
    }

    [Test]
    public static void ShortLinesForTheBannerAndTheBook()
    {
        Assert.Equal("Day 1", ProgressText.DayLine(0));
        Assert.Equal("Day 12", ProgressText.DayLine(12));
        Assert.Equal("Day 3 in a row", ProgressText.StreakLine(3));
        Assert.Equal("Day 1: come back tomorrow", ProgressText.StreakLine(1));
        var challenge = new DailyChallenge(ChallengeKind.TipTarget, 70);
        Assert.Equal("Challenge: Finish with 70 tips or more - OK", ProgressText.ChallengeLine(challenge, true));
        Assert.Equal("Challenge: Finish with 70 tips or more - not yet", ProgressText.ChallengeLine(challenge, false));
        Assert.Equal("Challenge: OK", ProgressText.ChallengeVerdict(true));
        Assert.Equal("Challenge: not yet", ProgressText.ChallengeVerdict(false));
        Assert.Equal("Next: a reveal with no heckle", ProgressText.NextStarLine(new TrickMastery()));
        Assert.Equal("Mastered", ProgressText.NextStarLine(new TrickMastery { Clean = true, Ovation = true, ToughCrowd = true }));
        Assert.Equal("Best: 96 tips (new!)", ProgressText.BestLine(96, true));
        Assert.Equal("Best: 96 tips", ProgressText.BestLine(96, false));
        Assert.True(ProgressText.TomorrowLine(false).Contains("Regular"), "day one teases the Regular");
        Assert.False(ProgressText.TomorrowLine(true).Contains("Regular"));
    }

    [Test]
    public static void EveryLineIsShortPlainAscii()
    {
        // The default TextMesh Pro font has no ticks, stars or fancy punctuation.
        var lines = new List<string>();
        for (int i = 0; i < 60; i++)
        {
            int date = CalendarDay.AddDays(ProgressSim.Day1, i);
            DailyPlan plan = DailyShow.Plan(date, i % 2 == 0);
            lines.Add(plan.Challenge.Text);
            lines.Add(ProgressText.CrowdLine(plan.Acts));
            lines.Add(ProgressText.ChallengeLine(plan.Challenge, i % 3 == 0));
        }
        lines.Add(ProgressText.DayLine(365));
        lines.Add(ProgressText.StreakLine(1));
        lines.Add(ProgressText.StreakLine(100));
        lines.Add(ProgressText.BestLine(144, true));
        lines.Add(ProgressText.TomorrowLine(true));
        lines.Add(ProgressText.TomorrowLine(false));
        lines.Add(ProgressText.NextStarLine(new TrickMastery()));
        lines.Add(ProgressText.NextStarLine(new TrickMastery { Clean = true }));
        lines.Add(ProgressText.NextStarLine(new TrickMastery { Clean = true, Ovation = true }));
        lines.Add(ProgressText.NextStarLine(new TrickMastery { Clean = true, Ovation = true, ToughCrowd = true }));
        lines.Add(ProgressText.ChallengeVerdict(true));
        lines.Add(ProgressText.ChallengeVerdict(false));
        foreach (RegularReaction r in Enum.GetValues(typeof(RegularReaction)))
        {
            lines.Add(RegularRule.Line(r, new RegularMemory { Date = ProgressSim.Day1 }, ProgressSim.Day2));
            lines.Add(RegularRule.Line(r, new RegularMemory { Date = ProgressSim.Day1 }, ProgressSim.Day3));
        }
        foreach (string line in lines)
        {
            Assert.True(line.Length <= 60, "too long for the table: " + line);
            foreach (char c in line)
            {
                Assert.True(c >= ' ' && c <= '~', "not plain ASCII: '" + c + "' in: " + line);
            }
        }
    }
}
