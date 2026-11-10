using System;
using Misdirection.Core.Tricks;

namespace Misdirection.Core.Show
{
    /// <summary>The first five minutes, one beat at a time (docs/DESIGN.md, "First five minutes").</summary>
    public enum TutorialStep
    {
        /// <summary>Ghost hands rest palms-down on the table; the mat unfurls under the player's palms.</summary>
        PalmsOnTable,
        /// <summary>Ghost hand pinches the coin.</summary>
        PickUp,
        /// <summary>Ghost hand lifts the coin to show it.</summary>
        ShowCoin,
        /// <summary>"Look at her": the Kid's eyes, and her cone, follow your gaze off your hands.</summary>
        LookAtKid,
        /// <summary>Ghost hand drops the coin behind the near edge (the servante).</summary>
        Ditch,
        /// <summary>Ghost hand opens, palm toward the Kid.</summary>
        ShowEmpty,
        /// <summary>The Kid gasps and claps: first applause.</summary>
        Applause,
        Done,
    }

    [Serializable]
    public sealed class TutorialSettings
    {
        /// <summary>The Kid must stay locked or looking off the coin this long before the ditch is taught.</summary>
        public double LookHoldSeconds = 0.4;
        /// <summary>If the player can't get her to look away, teach the ditch anyway after this long.</summary>
        public double LookTimeoutSeconds = 8.0;
        /// <summary>How long the first applause plays before the rest of the crowd walks in.</summary>
        public double ApplauseSeconds = 3.0;
        /// <summary>Coverage below this counts as "her eyes are off the coin".</summary>
        public float OffCoinCoverage = 0.3f;
    }

    /// <summary>What the scene measures each frame for the tutorial.</summary>
    public struct TutorialInput
    {
        public bool TableReady;
        /// <summary>Trick 1's phase (the tutorial teaches its vanish).</summary>
        public TrickPhase Phase;
        /// <summary>The Kid is locked on the magician's eyes.</summary>
        public bool KidEyeContact;
        /// <summary>How much the Kid's cone covers the coin (0..1).</summary>
        public float KidCoverage;
    }

    /// <summary>
    /// The first-launch tutorial as a state machine, no text required: each step names what the ghost
    /// hands demonstrate, and moves on from what the player actually does with Trick 1. A bust or a
    /// dropped coin sends the player back to picking it up, never to a fail screen. Pure logic.
    /// </summary>
    public sealed class Tutorial
    {
        private readonly TutorialSettings settings;
        private double lookGoodSince = -1;
        private TrickPhase lastPhase = TrickPhase.Idle;

        public Tutorial(TutorialSettings settings = null)
        {
            this.settings = settings ?? new TutorialSettings();
            Step = TutorialStep.PalmsOnTable;
            FirstApplauseAt = double.NaN;
            StartedAt = double.NaN;
        }

        public TutorialStep Step { get; private set; }
        public double StepSince { get; private set; }
        public double StartedAt { get; private set; }
        /// <summary>Seconds from the tutorial's start to the first applause (NaN until then).</summary>
        public double FirstApplauseAt { get; private set; }
        public double SecondsToFirstApplause { get { return FirstApplauseAt - StartedAt; } }
        /// <summary>Times the player was sent back to pick the coin up again.</summary>
        public int Retries { get; private set; }
        public bool Finished { get { return Step == TutorialStep.Done; } }

        public void Start(double time, bool tableReady)
        {
            StartedAt = time;
            SetStep(time, tableReady ? TutorialStep.PickUp : TutorialStep.PalmsOnTable);
        }

        /// <summary>Advance one frame. Returns true when the step changed.</summary>
        public bool Tick(double time, TutorialInput input)
        {
            bool starting = double.IsNaN(StartedAt);
            if (starting)
            {
                Start(time, input.TableReady);
            }
            TutorialStep before = starting ? (TutorialStep)(-1) : Step;
            bool justBusted = input.Phase == TrickPhase.Busted && lastPhase != TrickPhase.Busted;
            lastPhase = input.Phase;

            if (justBusted && Step != TutorialStep.PalmsOnTable && Step != TutorialStep.Applause && Step != TutorialStep.Done)
            {
                // The trick plays its bust and puts the coin back by itself; the lesson restarts at the pickup.
                Retry(time);
                return Step != before;
            }

            switch (Step)
            {
                case TutorialStep.PalmsOnTable:
                    if (input.TableReady)
                    {
                        SetStep(time, TutorialStep.PickUp);
                    }
                    break;

                case TutorialStep.PickUp:
                    // Catch up if the player got ahead of the lesson.
                    if (input.Phase == TrickPhase.Showing)
                    {
                        SetStep(time, TutorialStep.ShowCoin);
                    }
                    else if (input.Phase == TrickPhase.Misdirect)
                    {
                        SetStep(time, TutorialStep.LookAtKid);
                    }
                    else if (input.Phase == TrickPhase.Vanished)
                    {
                        SetStep(time, TutorialStep.ShowEmpty);
                    }
                    break;

                case TutorialStep.ShowCoin:
                    if (input.Phase == TrickPhase.Misdirect)
                    {
                        SetStep(time, TutorialStep.LookAtKid);
                    }
                    else if (input.Phase == TrickPhase.Idle)
                    {
                        SetStep(time, TutorialStep.PickUp);
                    }
                    break;

                case TutorialStep.LookAtKid:
                    if (input.Phase == TrickPhase.Vanished)
                    {
                        // Ditched already, their own way: go straight to the reveal.
                        SetStep(time, TutorialStep.ShowEmpty);
                    }
                    else if (input.Phase == TrickPhase.Idle)
                    {
                        SetStep(time, TutorialStep.PickUp);
                    }
                    else
                    {
                        bool good = input.KidEyeContact || input.KidCoverage < settings.OffCoinCoverage;
                        if (!good)
                        {
                            lookGoodSince = -1;
                        }
                        else if (lookGoodSince < 0)
                        {
                            lookGoodSince = time;
                        }
                        bool held = lookGoodSince >= 0 && time - lookGoodSince >= settings.LookHoldSeconds - 1e-9;
                        if (held || time - StepSince >= settings.LookTimeoutSeconds)
                        {
                            SetStep(time, TutorialStep.Ditch);
                        }
                    }
                    break;

                case TutorialStep.Ditch:
                    if (input.Phase == TrickPhase.Vanished)
                    {
                        SetStep(time, TutorialStep.ShowEmpty);
                    }
                    else if (input.Phase == TrickPhase.Idle)
                    {
                        SetStep(time, TutorialStep.PickUp);
                    }
                    break;

                case TutorialStep.ShowEmpty:
                    // Moves on in Revealed().
                    if (input.Phase == TrickPhase.Idle)
                    {
                        SetStep(time, TutorialStep.PickUp);
                    }
                    break;

                case TutorialStep.Applause:
                    if (time - StepSince >= settings.ApplauseSeconds)
                    {
                        SetStep(time, TutorialStep.Done);
                    }
                    break;
            }
            return Step != before;
        }

        /// <summary>
        /// A reveal landed. The vanish, seen clean enough to clap or gasp, is the first applause.
        /// Returns true when the scene should put the coin back on the mat (she saw it: try again).
        /// </summary>
        public bool Revealed(double time, RevealResult r)
        {
            if (Step != TutorialStep.ShowEmpty && Step != TutorialStep.Ditch && Step != TutorialStep.LookAtKid)
            {
                return false;
            }
            if (r.Reaction == Reaction.Heckle)
            {
                // She saw it: try again, no fail screen.
                Retry(time);
                return true;
            }
            if (double.IsNaN(FirstApplauseAt))
            {
                FirstApplauseAt = time;
            }
            SetStep(time, TutorialStep.Applause);
            return false;
        }

        private void Retry(double time)
        {
            Retries++;
            SetStep(time, TutorialStep.PickUp);
        }

        private void SetStep(double time, TutorialStep step)
        {
            Step = step;
            StepSince = time;
            lookGoodSince = -1;
        }
    }
}
