using System;
using Misdirection.Core.Crowd;
using Misdirection.Core.Tricks;

namespace Misdirection.Core.Show
{
    public enum ShowStage
    {
        /// <summary>No table yet: palms on the table (or the room scan) comes first.</summary>
        Setup,
        /// <summary>First launch only: one spectator, one vanish, first applause.</summary>
        Tutorial,
        /// <summary>The show proper: acts in order, the crowd growing each act.</summary>
        Performing,
        /// <summary>Every act done: tips counted, the crowd applauds. Pick up the coin to go again.</summary>
        Bow,
        /// <summary>Too many busts: the crowd walks away. Pick up the coin to go again.</summary>
        WalkOff,
    }

    /// <summary>One act of the show: which trick, in front of whom.</summary>
    [Serializable]
    public sealed class ShowAct
    {
        /// <summary>Index into the scene's trick list (0 = vanish and produce, 1 = coin under the cup).</summary>
        public int Trick;
        public Archetype[] Crowd = new Archetype[0];

        public ShowAct() { }

        public ShowAct(int trick, params Archetype[] crowd)
        {
            Trick = trick;
            Crowd = crowd ?? new Archetype[0];
        }
    }

    [Serializable]
    public sealed class ShowSettings
    {
        /// <summary>The set: a crowd that gathers as the show goes on (3, then 4, then 5 people).</summary>
        public ShowAct[] Acts =
        {
            new ShowAct(0, Archetype.Tourist, Archetype.Kid, Archetype.Skeptic),
            new ShowAct(1, Archetype.Tourist, Archetype.Kid, Archetype.Skeptic, Archetype.Filmer),
            new ShowAct(0, Archetype.Tourist, Archetype.Kid, Archetype.Skeptic, Archetype.Filmer, Archetype.Regular),
        };

        /// <summary>This many busts in one show and the crowd walks away.</summary>
        public int BustsToWalkOff = 3;

        /// <summary>The bow (or walk-off) plays at least this long before a new show can start.</summary>
        public double OverSeconds = 4.0;

        /// <summary>The tutorial's single spectator.</summary>
        public Archetype[] TutorialCrowd = { Archetype.Kid };
    }

    /// <summary>How a finished show went (for the bow, the logs and, later, the trick book).</summary>
    public struct ShowSummary
    {
        public int Tips;
        public int ActsCompleted;
        public int Reveals;
        public int Applause;
        public int Busts;
        public bool WalkedOff;
        public double Seconds;
        /// <summary>0 (walked off) to 3 (clean show).</summary>
        public int Stars;
    }

    /// <summary>
    /// The show from first launch to the bow: setup, the first-launch tutorial, a set of acts with a
    /// growing crowd, then a bow or a walk-off, and "pick up the coin to go again". Pure logic: the
    /// scene reports the table, reveals, busts and finished tricks; this decides what happens next.
    /// </summary>
    public sealed class ShowFlow
    {
        private readonly ShowSettings settings;

        public ShowFlow(ShowSettings settings = null, bool tutorialDone = false)
        {
            this.settings = settings ?? new ShowSettings();
            if (this.settings.Acts == null || this.settings.Acts.Length == 0)
            {
                throw new ArgumentException("A show needs at least one act.");
            }
            TutorialDone = tutorialDone;
            Stage = ShowStage.Setup;
        }

        public ShowSettings Settings { get { return settings; } }
        public ShowStage Stage { get; private set; }
        public double StageSince { get; private set; }

        /// <summary>Index of the current act (meaningful while Performing).</summary>
        public int Act { get; private set; }
        public int ActCount { get { return settings.Acts.Length; } }
        public ShowAct CurrentAct { get { return settings.Acts[Math.Max(0, Math.Min(Act, settings.Acts.Length - 1))]; } }

        public bool TutorialDone { get; private set; }

        /// <summary>Counts for the show in progress (or the one that just ended).</summary>
        public int Tips { get; private set; }
        public int Reveals { get; private set; }
        public int Applause { get; private set; }
        public int Busts { get; private set; }
        public int ShowsStarted { get; private set; }
        public double ShowStartedAt { get; private set; }
        public ShowSummary LastSummary { get; private set; }

        public bool Over { get { return Stage == ShowStage.Bow || Stage == ShowStage.WalkOff; } }

        /// <summary>The crowd that should be on stage right now.</summary>
        public Archetype[] Crowd
        {
            get
            {
                switch (Stage)
                {
                    case ShowStage.Tutorial: return settings.TutorialCrowd;
                    case ShowStage.Setup:
                    case ShowStage.WalkOff: return new Archetype[0];
                    default: return CurrentAct.Crowd;
                }
            }
        }

        /// <summary>Which trick should be live (-1 for none). During a bow it's act 1's trick, so the coin is there to pick up.</summary>
        public int ActiveTrick
        {
            get
            {
                switch (Stage)
                {
                    case ShowStage.Setup: return -1;
                    case ShowStage.Tutorial: return settings.Acts[0].Trick;
                    case ShowStage.Performing: return CurrentAct.Trick;
                    default: return settings.Acts[0].Trick;
                }
            }
        }

        /// <summary>The table is known (palms, saved anchor or room scan).</summary>
        public void TableReady(double time)
        {
            if (Stage != ShowStage.Setup)
            {
                return;
            }
            if (TutorialDone)
            {
                StartShow(time);
            }
            else
            {
                SetStage(time, ShowStage.Tutorial);
            }
        }

        public void TutorialFinished(double time)
        {
            if (Stage != ShowStage.Tutorial)
            {
                return;
            }
            TutorialDone = true;
            StartShow(time);
        }

        /// <summary>A reveal landed. Only reveals in the show proper count toward its tips.</summary>
        public void Revealed(RevealResult r)
        {
            if (Stage != ShowStage.Performing)
            {
                return;
            }
            Tips += r.Tips;
            Reveals++;
            if (r.Reaction == Reaction.Applause)
            {
                Applause++;
            }
        }

        /// <summary>The method was exposed. Returns true if that was the last straw (the crowd walks off).</summary>
        public bool Busted(double time)
        {
            if (Stage != ShowStage.Performing)
            {
                return false;
            }
            Busts++;
            if (Busts >= Math.Max(1, settings.BustsToWalkOff))
            {
                End(time, true);
                return true;
            }
            return false;
        }

        /// <summary>The live trick finished (after its bow). Moves to the next act, or to the show's bow.</summary>
        public void TrickCompleted(double time)
        {
            if (Stage != ShowStage.Performing)
            {
                return;
            }
            if (Act + 1 >= settings.Acts.Length)
            {
                End(time, false);
                return;
            }
            Act++;
            StageSince = time;
        }

        /// <summary>After a bow or walk-off, picking up the coin starts a new show once the bow has played.</summary>
        public bool CanRestart(double time)
        {
            return Over && time - StageSince >= settings.OverSeconds - 1e-9;
        }

        public bool Restart(double time)
        {
            if (!CanRestart(time))
            {
                return false;
            }
            StartShow(time);
            return true;
        }

        /// <summary>For "replay the tutorial": the next show starts with it again.</summary>
        public void ForgetTutorial()
        {
            TutorialDone = false;
        }

        /// <summary>Stars for a finished show: 0 walked off, 1 finished, 2 at most one bust, 3 clean with mostly applause.</summary>
        public static int StarsFor(bool walkedOff, int busts, int reveals, int applause)
        {
            if (walkedOff)
            {
                return 0;
            }
            if (busts == 0 && reveals > 0 && applause * 3 >= reveals * 2)
            {
                return 3;
            }
            return busts <= 1 ? 2 : 1;
        }

        private void StartShow(double time)
        {
            Act = 0;
            Tips = 0;
            Reveals = 0;
            Applause = 0;
            Busts = 0;
            ShowsStarted++;
            ShowStartedAt = time;
            SetStage(time, ShowStage.Performing);
        }

        private void End(double time, bool walkedOff)
        {
            ShowSummary s = new ShowSummary();
            s.Tips = Tips;
            s.ActsCompleted = walkedOff ? Act : Act + 1;
            s.Reveals = Reveals;
            s.Applause = Applause;
            s.Busts = Busts;
            s.WalkedOff = walkedOff;
            s.Seconds = time - ShowStartedAt;
            s.Stars = StarsFor(walkedOff, Busts, Reveals, Applause);
            LastSummary = s;
            SetStage(time, walkedOff ? ShowStage.WalkOff : ShowStage.Bow);
        }

        private void SetStage(double time, ShowStage stage)
        {
            Stage = stage;
            StageSince = time;
        }
    }
}
