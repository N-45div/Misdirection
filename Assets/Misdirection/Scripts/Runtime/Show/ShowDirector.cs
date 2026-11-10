using System;
using System.Collections.Generic;
using Misdirection.Audience;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;
using Misdirection.Hands;
using Misdirection.Table;
using Misdirection.Tricks;
using TMPro;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection.Show
{
    /// <summary>
    /// Runs the whole show (Day 8 show flow + Day 9 first five minutes): palms on the table, a
    /// first-launch tutorial with one spectator and ghost hands, then three acts in front of a crowd
    /// that grows each act, a bow with tips and stars (or a walk-off after three busts), and "pick up
    /// the coin to go again". Both hands gone for 2 s, or the system taking focus, pauses everything.
    /// Rules live in Misdirection.Core.Show; this wires tricks, crowd, table, hands and ghost hands.
    /// </summary>
    [DefaultExecutionOrder(30)]
    public sealed class ShowDirector : MonoBehaviour
    {
        public const string TutorialDoneKey = "misdirection.tutorial.done";

        [Tooltip("Trick directors (each an ISequencedTrick). Index = ShowAct.Trick: 0 vanish and produce, 1 coin under the cup.")]
        [SerializeField] private MonoBehaviour[] tricks = new MonoBehaviour[0];
        [SerializeField] private Crowd crowd;
        [SerializeField] private TableCalibrator table;
        [SerializeField] private TrackedHand leftHand;
        [SerializeField] private TrackedHand rightHand;
        [SerializeField] private LookSource lookSource;
        [SerializeField] private MentorHands mentor;

        [SerializeField] private ShowSettings settings = new ShowSettings();
        [SerializeField] private TutorialSettings tutorialSettings = new TutorialSettings();
        [Tooltip("Hide the tricks' text prompts in the show too (ghost hands and the crowd carry it).")]
        [SerializeField] private bool hideTrickHudInShow;
        [Tooltip("Magician's Eye cones during the show (always on in the tutorial).")]
        [SerializeField] private bool showConesInShow = true;
        [Tooltip("Testing: play the tutorial every launch.")]
        [SerializeField] private bool alwaysTutorial;
        [Tooltip("Show a ghost-hand hint after this long with the coin untouched in the show (seconds).")]
        [SerializeField] private float idleHintSeconds = 15f;
        [SerializeField] private float walkOffSeconds = 2.5f;

        private ShowFlow flow;
        private Tutorial tutorial;
        private readonly ShowPause pause = new ShowPause();
        private ISequencedTrick[] seq = new ISequencedTrick[0];
        private int activeTrick = -2;
        private ShowStage lastStage = (ShowStage)(-1);
        private int lastAct = -1;
        private bool pendingTrickReset;
        private bool hasInputFocus = true;
        private bool pausedApplied;
        private float savedTimeScale = 1f;
        private double idleSince = -1;

        private TextMeshPro banner;
        private double bannerUntil;
        private readonly List<Renderer> stars = new List<Renderer>();
        private AudioSource audioSource;
        private AudioClip fanfare, arrive, walkOffTone;

        public ShowFlow Flow { get { return flow; } }
        public Tutorial Tutorial { get { return tutorial; } }
        public bool Paused { get { return pause.Paused; } }
        public PauseReason PauseReason { get { return pause.Reason; } }

        public void Configure(MonoBehaviour[] trickDirectors, Crowd audience, TableCalibrator tableCalibrator, TrackedHand left,
                              TrackedHand right, LookSource look, MentorHands mentorHands)
        {
            tricks = trickDirectors ?? new MonoBehaviour[0];
            crowd = audience;
            table = tableCalibrator;
            leftHand = left;
            rightHand = right;
            lookSource = look;
            mentor = mentorHands;
        }

        /// <summary>The tutorial plays again on the next launch.</summary>
        public static void ForgetTutorial()
        {
            PlayerPrefs.DeleteKey(TutorialDoneKey);
            PlayerPrefs.Save();
        }

        private void Awake()
        {
            bool done = !alwaysTutorial && PlayerPrefs.GetInt(TutorialDoneKey, 0) == 1;
            flow = new ShowFlow(settings, done);
            tutorial = new Tutorial(tutorialSettings);
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }

            // The show owns which trick runs; the older alternating sequence steps aside.
            foreach (ShowSequence old in FindObjectsByType<ShowSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                old.enabled = false;
            }
            seq = new ISequencedTrick[tricks.Length];
            for (int i = 0; i < tricks.Length; i++)
            {
                seq[i] = tricks[i] as ISequencedTrick;
                if (seq[i] == null && tricks[i] != null)
                {
                    Debug.LogWarning("[Misdirection] Show: " + tricks[i].GetType().Name + " can't take part in a show (no ISequencedTrick).");
                }
            }
            if (crowd != null)
            {
                crowd.SetArchetypes(flow.Crowd);
            }

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            fanfare = Tones.Chirp("bow fanfare", 523f, 1046f, 0.6f, 0.35f);
            arrive = Tones.Chirp("crowd arrives", 392f, 587f, 0.25f, 0.25f);
            walkOffTone = Tones.Thud("walk off", 196f, 98f, 0.7f, 0.45f);
            banner = WorldText.Create(transform, "Show Banner", new Vector2(0.6f, 0.2f), 0.5f);
            banner.gameObject.SetActive(false);
            for (int i = 0; i < 3; i++)
            {
                GameObject star = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                star.name = "Star " + (i + 1);
                star.transform.SetParent(transform, false);
                star.transform.localScale = new Vector3(0.05f, 0.004f, 0.05f);
                Collider c = star.GetComponent<Collider>();
                if (c != null)
                {
                    Destroy(c);
                }
                Renderer r = star.GetComponent<Renderer>();
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                star.SetActive(false);
                stars.Add(r);
            }
        }

        private void OnEnable()
        {
            OVRManager.InputFocusLost += OnInputFocusLost;
            OVRManager.InputFocusAcquired += OnInputFocusAcquired;
        }

        private void OnDisable()
        {
            OVRManager.InputFocusLost -= OnInputFocusLost;
            OVRManager.InputFocusAcquired -= OnInputFocusAcquired;
            ApplyPause(false);
        }

        private void Start()
        {
            for (int i = 0; i < seq.Length; i++)
            {
                if (seq[i] == null)
                {
                    continue;
                }
                int index = i;
                seq[i].Revealed += r => OnRevealed(index, r);
                seq[i].Busted += () => OnBusted(index);
                seq[i].Completed += () => OnCompleted(index);
            }
            ApplyTrick(flow.ActiveTrick);
            Debug.Log("[Misdirection] Show: ready (" + flow.ActCount + " acts; tutorial " + (flow.TutorialDone ? "done before" : "first") + ").");
        }

        private void OnInputFocusLost() { hasInputFocus = false; }
        private void OnInputFocusAcquired() { hasInputFocus = true; }

        private void LateUpdate()
        {
            double now = Time.timeAsDouble;
            double real = Time.unscaledTimeAsDouble;

            // Pause first: while paused, nothing else moves (game time is frozen).
            pause.WatchHands = flow.Stage != ShowStage.Setup;
            bool left = leftHand != null && leftHand.Tracked;
            bool right = rightHand != null && rightHand.Tracked;
            if (pause.Update(real, left, right, hasInputFocus))
            {
                ApplyPause(pause.Paused);
                Debug.Log("[Misdirection] Show: " + (pause.Paused ? "paused (" + pause.Reason + ")" : "resumed"));
            }
            if (pause.Paused)
            {
                if (mentor != null)
                {
                    mentor.Show(pause.Reason == PauseReason.HandsGone ? MentorDemo.HandsBack : MentorDemo.None,
                                "Show both hands to carry on");
                }
                return;
            }

            if (flow.Stage == ShowStage.Setup && table != null && table.Calibrated)
            {
                flow.TableReady(now);
            }
            if (flow.Stage == ShowStage.Tutorial)
            {
                TickTutorial(now);
            }
            if (flow.Over && flow.CanRestart(now) && PickedUp(flow.ActiveTrick))
            {
                flow.Restart(now);
                Debug.Log("[Misdirection] Show: new show (#" + flow.ShowsStarted + ").");
            }

            if (flow.Stage != lastStage || flow.Act != lastAct)
            {
                OnStageChanged(lastStage, now);
                lastStage = flow.Stage;
                lastAct = flow.Act;
            }

            ApplyTrick(flow.ActiveTrick);
            if (pendingTrickReset && activeTrick >= 0 && seq[activeTrick] != null)
            {
                pendingTrickReset = false;
                seq[activeTrick].ResetTrick();
            }
            ApplyCrowd(now);
            if (table != null)
            {
                // Resting palms can re-place the table only while the coin sits untouched.
                table.AllowRecalibration = flow.Stage == ShowStage.Setup || activeTrick < 0 || IsIdle(activeTrick);
            }
            UpdateMentor(now);
            UpdateBanner(now);
        }

        // ------------------------------------------------------------ tutorial

        private void TickTutorial(double now)
        {
            TrickDirector vanish = TrickAt(flow.ActiveTrick) as TrickDirector;
            if (vanish == null || vanish.Trick == null)
            {
                // No Trick 1 in this scene: nothing to teach with.
                FinishTutorial(now);
                return;
            }
            Spectator kid = Kid();
            TutorialInput input = new TutorialInput();
            input.TableReady = table != null && table.Calibrated;
            input.Phase = vanish.Trick.Phase;
            input.KidEyeContact = kid != null && kid.Mind.EyeContact;
            input.KidCoverage = kid != null && vanish.Coin != null ? kid.Mind.Coverage(vanish.Coin.Position.ToNumerics()) : 1f;
            if (tutorial.Tick(now, input))
            {
                Debug.Log("[Misdirection] Show: tutorial step " + tutorial.Step + " at " + (now - tutorial.StartedAt).ToString("F1") + " s");
            }
            if (tutorial.Finished)
            {
                FinishTutorial(now);
            }
        }

        private void FinishTutorial(double now)
        {
            flow.TutorialFinished(now);
            PlayerPrefs.SetInt(TutorialDoneKey, 1);
            PlayerPrefs.Save();
            pendingTrickReset = true;
            if (!double.IsNaN(tutorial.FirstApplauseAt))
            {
                Debug.Log("[Misdirection] Show: first applause " + tutorial.SecondsToFirstApplause.ToString("F1") +
                          " s into the tutorial (" + tutorial.Retries + " retries).");
            }
        }

        // ------------------------------------------------------------ trick events

        private void OnRevealed(int index, RevealResult r)
        {
            if (index != activeTrick)
            {
                return;
            }
            if (flow.Stage == ShowStage.Tutorial)
            {
                if (tutorial.Revealed(Time.timeAsDouble, r))
                {
                    pendingTrickReset = true;
                }
                return;
            }
            flow.Revealed(r);
        }

        private void OnBusted(int index)
        {
            if (index != activeTrick)
            {
                return;
            }
            if (flow.Busted(Time.timeAsDouble))
            {
                Debug.Log("[Misdirection] Show: the crowd walked off after " + flow.Busts + " busts.");
            }
        }

        private void OnCompleted(int index)
        {
            if (index == activeTrick && flow.Stage == ShowStage.Performing)
            {
                flow.TrickCompleted(Time.timeAsDouble);
            }
        }

        // ------------------------------------------------------------ stage changes

        private void OnStageChanged(ShowStage from, double now)
        {
            switch (flow.Stage)
            {
                case ShowStage.Tutorial:
                    Debug.Log("[Misdirection] Show: tutorial (one spectator).");
                    break;

                case ShowStage.Performing:
                    ShowBanner(flow.ActCount > 1 ? "Act " + (flow.Act + 1) + " of " + flow.ActCount : "", now, 2.5);
                    Play(arrive);
                    Debug.Log("[Misdirection] Show: act " + (flow.Act + 1) + " of " + flow.ActCount + ", trick " + flow.ActiveTrick +
                              ", crowd " + string.Join(", ", Names(flow.Crowd)));
                    break;

                case ShowStage.Bow:
                {
                    ShowSummary s = flow.LastSummary;
                    if (crowd != null)
                    {
                        crowd.React(Reaction.Applause, -1);
                    }
                    Play(fanfare);
                    ShowBanner("<b>Bravo!</b>\n<size=70%>" + s.Tips + " tips</size>", now, settings.OverSeconds + 600);
                    ShowStars(s.Stars);
                    Debug.Log("[Misdirection] Show: bow. " + s.Tips + " tips, " + s.Stars + " stars, " + s.Busts + " busts, " +
                              s.Seconds.ToString("F0") + " s.");
                    break;
                }

                case ShowStage.WalkOff:
                    Play(walkOffTone);
                    ShowBanner("They walked off.\n<size=70%>" + flow.LastSummary.Tips + " tips</size>", now, settings.OverSeconds + 600);
                    ShowStars(0);
                    break;
            }
            if (!flow.Over)
            {
                HideStars();
            }
            for (int i = 0; i < seq.Length; i++)
            {
                if (seq[i] != null)
                {
                    seq[i].HudVisible = flow.Stage == ShowStage.Performing && !hideTrickHudInShow;
                }
            }
        }

        private void ApplyTrick(int index)
        {
            if (index == activeTrick)
            {
                return;
            }
            activeTrick = index;
            // Disable first so only one trick ever owns the coin.
            for (int i = 0; i < tricks.Length; i++)
            {
                if (tricks[i] != null && i != index)
                {
                    tricks[i].enabled = false;
                }
            }
            if (index >= 0 && index < tricks.Length && tricks[index] != null)
            {
                tricks[index].enabled = true;
                if (seq[index] != null)
                {
                    seq[index].HudVisible = flow.Stage == ShowStage.Performing && !hideTrickHudInShow;
                }
            }
            idleSince = -1;
        }

        private void ApplyCrowd(double now)
        {
            if (crowd == null)
            {
                return;
            }
            // Let the walk-off play (frowns, "hm!") before the crowd disperses.
            if (flow.Stage == ShowStage.WalkOff && now - flow.StageSince < walkOffSeconds)
            {
                return;
            }
            if (crowd.SetArchetypes(flow.Crowd) && flow.Crowd.Length > 0 && flow.Stage != ShowStage.Tutorial)
            {
                Play(arrive);
            }
            // The tutorial always shows where eyes go (the Magician's Eye); the show does unless turned off.
            bool cones = flow.Stage == ShowStage.Tutorial || showConesInShow;
            if (crowd.ShowCones != cones)
            {
                crowd.ShowCones = cones;
            }
        }

        // ------------------------------------------------------------ ghost hands

        private void UpdateMentor(double now)
        {
            if (mentor == null)
            {
                return;
            }
            CoinActor coin = CoinOf(activeTrick);
            if (coin != null)
            {
                mentor.SetCoin(coin.Position);
            }

            switch (flow.Stage)
            {
                case ShowStage.Setup:
                    mentor.Show(MentorDemo.PalmsOnTable, "Rest both palms flat on the table");
                    break;

                case ShowStage.Tutorial:
                    switch (tutorial.Step)
                    {
                        case TutorialStep.PalmsOnTable: mentor.Show(MentorDemo.PalmsOnTable, "Rest both palms flat on the table"); break;
                        case TutorialStep.PickUp: mentor.Show(MentorDemo.PickUp, "Pinch the coin"); break;
                        case TutorialStep.ShowCoin: mentor.Show(MentorDemo.ShowCoin, "Hold it up for her"); break;
                        case TutorialStep.LookAtKid:
                        {
                            Spectator kid = Kid();
                            if (kid != null)
                            {
                                mentor.SetLookTarget(kid.EyesPosition);
                            }
                            mentor.Show(MentorDemo.LookAt, "Look her in the eyes");
                            break;
                        }
                        case TutorialStep.Ditch: mentor.Show(MentorDemo.Ditch, "Now drop it just past the edge"); break;
                        case TutorialStep.ShowEmpty: mentor.Show(MentorDemo.ShowEmpty, "Show her your empty hand"); break;
                        default: mentor.Hide(); break;
                    }
                    break;

                case ShowStage.Performing:
                    // A gentle nudge if the coin sits untouched for a while.
                    if (activeTrick >= 0 && IsIdle(activeTrick))
                    {
                        if (idleSince < 0)
                        {
                            idleSince = now;
                        }
                    }
                    else
                    {
                        idleSince = -1;
                    }
                    if (idleSince >= 0 && now - idleSince >= idleHintSeconds)
                    {
                        mentor.Show(MentorDemo.PickUp, "");
                    }
                    else
                    {
                        mentor.Hide();
                    }
                    break;

                default:
                    if (flow.CanRestart(now))
                    {
                        mentor.Show(MentorDemo.PickUp, "Pick up the coin to go again");
                    }
                    else
                    {
                        mentor.Hide();
                    }
                    break;
            }
        }

        // ------------------------------------------------------------ banner and stars

        private void ShowBanner(string text, double now, double seconds)
        {
            banner.text = text;
            bannerUntil = text.Length > 0 ? now + seconds : now;
        }

        private void UpdateBanner(double now)
        {
            bool visible = now < bannerUntil && table != null && table.Calibrated;
            banner.gameObject.SetActive(visible);
            Vector3 viewer = lookSource != null && lookSource.Head != null ? lookSource.Head.position : Vector3.up * 1.2f;
            if (visible)
            {
                banner.transform.position = table.Frame.ToWorld(new NVector3(0f, 0.32f, 0.55f)).ToUnity();
                WorldText.Face(banner.transform, viewer);
            }
            if (table != null && table.Calibrated)
            {
                for (int i = 0; i < stars.Count; i++)
                {
                    Transform s = stars[i].transform;
                    s.position = table.Frame.ToWorld(new NVector3((i - 1) * 0.07f, 0.22f, 0.55f)).ToUnity();
                    Vector3 toViewer = viewer - s.position;
                    if (toViewer.sqrMagnitude > 1e-6f)
                    {
                        // Coins standing on edge, facing the player, slowly turning.
                        s.rotation = Quaternion.LookRotation(toViewer.normalized, Vector3.up) * Quaternion.Euler(90f, 0f, 0f) *
                                     Quaternion.Euler(0f, (float)now * 90f + i * 40f, 0f);
                    }
                }
            }
        }

        private void ShowStars(int earned)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                stars[i].material.color = i < earned ? new Color(1f, 0.8f, 0.2f) : new Color(0.35f, 0.35f, 0.38f);
                stars[i].gameObject.SetActive(true);
            }
        }

        private void HideStars()
        {
            foreach (Renderer r in stars)
            {
                r.gameObject.SetActive(false);
            }
        }

        // ------------------------------------------------------------ pause

        private void ApplyPause(bool paused)
        {
            if (paused == pausedApplied)
            {
                return;
            }
            pausedApplied = paused;
            if (paused)
            {
                savedTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else
            {
                Time.timeScale = savedTimeScale;
                AudioListener.pause = false;
            }
        }

        // ------------------------------------------------------------ helpers

        private MonoBehaviour TrickAt(int index)
        {
            return index >= 0 && index < tricks.Length ? tricks[index] : null;
        }

        private bool IsIdle(int index)
        {
            TrickDirector vanish = TrickAt(index) as TrickDirector;
            if (vanish != null && vanish.Trick != null)
            {
                return vanish.Trick.Phase == TrickPhase.Idle;
            }
            CupTrickDirector cup = TrickAt(index) as CupTrickDirector;
            return cup != null && cup.Trick != null && cup.Trick.Phase == CupPhase.Idle;
        }

        /// <summary>The player has picked the coin up and is mid-trick (not resting, busted or bowing).</summary>
        private bool PickedUp(int index)
        {
            TrickDirector vanish = TrickAt(index) as TrickDirector;
            if (vanish != null && vanish.Trick != null)
            {
                TrickPhase p = vanish.Trick.Phase;
                return p != TrickPhase.Idle && p != TrickPhase.Busted && p != TrickPhase.Finale;
            }
            CupTrickDirector cup = TrickAt(index) as CupTrickDirector;
            if (cup != null && cup.Trick != null)
            {
                CupPhase p = cup.Trick.Phase;
                return p != CupPhase.Idle && p != CupPhase.Busted && p != CupPhase.Finale;
            }
            return false;
        }

        private CoinActor CoinOf(int index)
        {
            TrickDirector vanish = TrickAt(index) as TrickDirector;
            if (vanish != null)
            {
                return vanish.Coin;
            }
            CupTrickDirector cup = TrickAt(index) as CupTrickDirector;
            return cup != null ? cup.Coin : null;
        }

        private Spectator Kid()
        {
            if (crowd == null)
            {
                return null;
            }
            int i = crowd.IndexOf(Archetype.Kid);
            return i >= 0 ? crowd.Spectators[i] : null;
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        private static string[] Names(Archetype[] lineUp)
        {
            string[] names = new string[lineUp.Length];
            for (int i = 0; i < lineUp.Length; i++)
            {
                names[i] = lineUp[i].ToString();
            }
            return names;
        }
    }
}
