using System;
using Misdirection.Audience;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Tricks;
using Misdirection.Hands;
using Misdirection.Table;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection.Tricks
{
    /// <summary>
    /// Trick 2 in the headset (Day 6, first cut candidate): the coin under the cup. Show the coin,
    /// misdirect, slip it under the cup's rim (open the coin hand right by the cup's base), show the
    /// hand empty, then pinch the cup's top and lift it: the coin is there. Rules live in
    /// Misdirection.Core.Tricks.CupTrick; this wires hands, coin, cup, crowd and feedback.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class CupTrickDirector : MonoBehaviour, ISequencedTrick
    {
        [SerializeField] private TrackedHand leftHand;
        [SerializeField] private TrackedHand rightHand;
        [SerializeField] private TableCalibrator table;
        [SerializeField] private CoinActor coin;
        [SerializeField] private CupActor cup;
        [SerializeField] private Crowd crowd;
        [SerializeField] private LookSource lookSource;

        [SerializeField] private PinchGripSettings grip = new PinchGripSettings();
        [SerializeField] private CupTrickSettings trickSettings = new CupTrickSettings();
        [SerializeField] private float grabRadius = 0.045f;
        [Tooltip("Pinch within this distance of the cup's top to pick the cup up (metres).")]
        [SerializeField] private float cupGrabRadius = 0.06f;
        [Tooltip("Opening the coin hand within this horizontal distance of the cup's base loads it (metres).")]
        [SerializeField] private float loadRadius = 0.07f;
        [Tooltip("...and no higher than this above the table (metres).")]
        [SerializeField] private float loadHeight = 0.09f;
        [SerializeField] private Vector3 coinRestLocal = new Vector3(-0.06f, 0.002f, 0.24f);
        [SerializeField] private Vector3 cupRestLocal = new Vector3(0.09f, 0f, 0.27f);

        [Header("Reveal gesture")]
        [Range(0f, 1f)] [SerializeField] private float revealOpenness = 0.75f;
        [SerializeField] private float revealHeight = 0.12f;
        [SerializeField] private bool writeCsv = true;

        private readonly CoinModel model = new CoinModel();
        private CupTrick trick;
        private PinchGrip leftCoinGrip, rightCoinGrip, leftCupGrip, rightCupGrip;
        private HandId cupHolder = HandId.None;
        private CupPhase lastPhase = CupPhase.Idle;
        private TrickFeedback feedback;
        private bool hudVisible = true;

        public CupTrick Trick { get { return trick; } }
        public CoinActor Coin { get { return coin; } }

        /// <summary>Fires after the bow (Finale → Idle).</summary>
        public event Action Completed;
        public event Action<RevealResult> Revealed;
        public event Action Busted;

        public bool HudVisible
        {
            get { return hudVisible; }
            set
            {
                hudVisible = value;
                if (feedback != null)
                {
                    feedback.SetHudVisible(value && isActiveAndEnabled);
                }
            }
        }

        public void ResetTrick()
        {
            if (trick != null)
            {
                ResetProps();
            }
        }

        public void Configure(TrackedHand left, TrackedHand right, TableCalibrator tableCalibrator, CoinActor coinActor, CupActor cupActor,
                              Crowd audience, LookSource look)
        {
            leftHand = left;
            rightHand = right;
            table = tableCalibrator;
            coin = coinActor;
            cup = cupActor;
            crowd = audience;
            lookSource = look;
        }

        private void Awake()
        {
            trick = new CupTrick(trickSettings);
            leftCoinGrip = new PinchGrip(grip);
            rightCoinGrip = new PinchGrip(grip);
            leftCupGrip = new PinchGrip(grip);
            rightCupGrip = new PinchGrip(grip);
            feedback = new TrickFeedback(gameObject, "cup", writeCsv);
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }
        }

        private void OnEnable()
        {
            if (table != null)
            {
                table.OnCalibrated += ResetProps;
            }
            if (feedback != null)
            {
                feedback.SetVisible(true);
                feedback.SetHudVisible(hudVisible);
            }
            if (table != null && table.Calibrated)
            {
                ResetProps();
            }
        }

        private void OnDisable()
        {
            if (table != null)
            {
                table.OnCalibrated -= ResetProps;
            }
            if (feedback != null)
            {
                feedback.SetVisible(false);
            }
            if (cup != null)
            {
                cup.gameObject.SetActive(false);
            }
        }

        private void OnDestroy()
        {
            if (feedback != null)
            {
                feedback.Dispose();
            }
        }

        private void LateUpdate()
        {
            if (leftHand == null || rightHand == null || table == null || coin == null || cup == null)
            {
                return;
            }
            double now = Time.timeAsDouble;
            Vector3 viewer = lookSource != null && lookSource.Head != null ? lookSource.Head.position : Vector3.up * 1.2f;
            Vector3 viewerForward = lookSource != null && lookSource.Head != null ? lookSource.Head.forward : Vector3.forward;
            if (!table.Calibrated)
            {
                coin.ShowDitched();
                feedback.SetHud("<b>Misdirection</b>\nRest both palms flat on the table, heels at the near edge, and hold still.");
                feedback.Update(now, viewer, viewerForward, table);
                return;
            }

            StepCoin(leftHand, leftCoinGrip, now);
            StepCoin(rightHand, rightCoinGrip, now);
            StepCup(leftHand, leftCupGrip, now);
            StepCup(rightHand, rightCupGrip, now);

            TrackedHand holder = HandFor(model.Holder);
            if (holder != null && model.State == CoinState.Held)
            {
                holder.Carrying = true;
                coin.ShowHeld(holder.StablePalm);
                if (crowd != null && trick.SecretInPlay)
                {
                    crowd.ExposeSecret(holder.StablePalm.position);
                }
            }
            if (crowd != null && (!trick.SecretInPlay || holder == null))
            {
                crowd.ClearSecret();
            }

            TrackedHand lifter = HandFor(cupHolder);
            if (lifter != null)
            {
                cup.Follow(lifter.PinchPoint);
            }

            RevealResult? reveal = trick.Tick(now, BuildInput(holder), cup.Lift);
            if (reveal.HasValue)
            {
                feedback.React(reveal.Value, crowd, table, now);
                feedback.Log(now, trick.Phase.ToString(), "reveal", null, reveal, Name);
                if (Revealed != null)
                {
                    Revealed(reveal.Value);
                }
                if (reveal.Value.Kind == "cup")
                {
                    feedback.Play(feedback.Sparkle);
                    coin.ShowResting(cup.Base, TableRotation());
                }
            }

            if (trick.Phase != lastPhase)
            {
                feedback.Log(now, trick.Phase.ToString(), "phase", trick.Phase.ToString(), null, Name);
                if (trick.Phase == CupPhase.Busted)
                {
                    feedback.Play(feedback.Heckle);
                    if (crowd != null)
                    {
                        crowd.React(Reaction.Heckle, -1);
                    }
                    feedback.ShowReaction("<color=#FF7043>" + trick.Prompt + "</color>", -1, crowd, table, now);
                    if (Busted != null)
                    {
                        Busted();
                    }
                }
                if (trick.Phase == CupPhase.Idle)
                {
                    bool finished = lastPhase == CupPhase.Finale;
                    if (finished && crowd != null)
                    {
                        // A new trick starts with a fresh, curious crowd.
                        foreach (Spectator s in crowd.Spectators)
                        {
                            s.Mind.ResetSuspicion();
                        }
                    }
                    ResetProps();
                    lastPhase = trick.Phase;
                    if (finished && Completed != null)
                    {
                        Completed();
                    }
                    return;
                }
                lastPhase = trick.Phase;
            }

            feedback.SetPrompt(trick.Prompt, trick.TotalTips, trick.LastReveal, crowd);
            feedback.Update(now, viewer, viewerForward, table);
        }

        private void StepCoin(TrackedHand hand, PinchGrip handGrip, double now)
        {
            HandId id = hand.Side;
            bool canGrab = trick.Phase == CupPhase.Idle && model.State == CoinState.Resting && cupHolder != id
                && Vector3.Distance(hand.PinchPoint, coin.Position) <= grabRadius;
            GripEvent e = handGrip.Update(now, hand.Presence.ReliableForDecisions, hand.Shape, canGrab);
            if (e == GripEvent.Grabbed)
            {
                if (model.TryGrab(id))
                {
                    trick.PickedUp(now, id);
                    feedback.Play(feedback.Pickup);
                    feedback.Log(now, trick.Phase.ToString(), "pickup", "", null, Name);
                }
                else
                {
                    handGrip.ForceRelease();
                }
            }
            else if (e == GripEvent.Released && model.Holder == id)
            {
                hand.Carrying = false;
                Vector3 palm = hand.StablePalm.position;
                ReleaseZone zone = ZoneFor(palm);
                WitnessReport w = Unseen();
                if (zone == ReleaseZone.UnderCup || zone == ReleaseZone.Servante)
                {
                    model.Release(id, true);
                    coin.ShowDitched();
                    feedback.Play(feedback.Hide);
                    if (crowd != null)
                    {
                        w = crowd.ReportMethod(palm, 1f);
                    }
                }
                else
                {
                    model.Release(id, false);
                    coin.ShowExposed(palm, table.Frame.Origin.Y, TableRotation());
                    feedback.Play(feedback.Clink);
                    if (crowd != null)
                    {
                        crowd.MakeSound(palm);
                        w = crowd.ReportMethod(palm, 0.6f);
                    }
                }
                trick.Released(now, id, zone, w);
                feedback.Log(now, trick.Phase.ToString(), zone.ToString().ToLowerInvariant(), w.Unseen ? "unseen" : "seen by " + Name(w.SeenBy), null, Name);
            }
        }

        private void StepCup(TrackedHand hand, PinchGrip handGrip, double now)
        {
            HandId id = hand.Side;
            bool canGrab = cupHolder == HandId.None && model.Holder != id
                && Vector3.Distance(hand.PinchPoint, cup.Top) <= cupGrabRadius;
            GripEvent e = handGrip.Update(now, hand.Presence.ReliableForDecisions, hand.Shape, canGrab || cupHolder == id);
            if (e == GripEvent.Grabbed && canGrab)
            {
                cupHolder = id;
                cup.Grab(hand.PinchPoint);
            }
            else if (e == GripEvent.Released && cupHolder == id)
            {
                cupHolder = HandId.None;
                cup.Release();
            }
        }

        private ReleaseZone ZoneFor(Vector3 palm)
        {
            Vector3 b = cup.Base;
            Vector2 flat = new Vector2(palm.x - b.x, palm.z - b.z);
            if (flat.magnitude <= loadRadius && palm.y - table.Frame.Origin.Y <= loadHeight)
            {
                return ReleaseZone.UnderCup;
            }
            return table.Zone.Update(table.Frame.ToLocal(palm.ToNumerics())) ? ReleaseZone.Servante : ReleaseZone.Table;
        }

        private TrickInput BuildInput(TrackedHand holder)
        {
            float tableY = table.Frame.Origin.Y;
            TrickInput input = new TrickInput();
            input.HolderHeight = holder != null && model.State == CoinState.Held ? holder.StablePalm.position.y - tableY : float.NaN;
            TrackedHand revealer = HandFor(trick.LastHolder);
            input.EmptyHandShown = revealer != null && model.Holder != revealer.Side && IsShowingEmpty(revealer, tableY);
            input.LookedAt = crowd != null && crowd.Model != null ? crowd.Model.LookedAt : -1;
            input.Spectators = crowd != null ? crowd.Spectators.Count : 0;
            input.AverageSuspicion = AverageSuspicion();
            return input;
        }

        private bool IsShowingEmpty(TrackedHand hand, float tableY)
        {
            if (!hand.Presence.ReliableForDecisions)
            {
                return false;
            }
            float open = hand.Shape.Openness;
            return !float.IsNaN(open) && open >= revealOpenness
                && hand.StablePalm.position.y - tableY >= revealHeight
                && hand.PalmNormal.y > -0.3f;
        }

        private void ResetProps()
        {
            model.Reset();
            leftCoinGrip.ForceRelease();
            rightCoinGrip.ForceRelease();
            leftCupGrip.ForceRelease();
            rightCupGrip.ForceRelease();
            cupHolder = HandId.None;
            if (leftHand != null)
            {
                leftHand.Carrying = false;
            }
            if (rightHand != null)
            {
                rightHand.Carrying = false;
            }
            if (trick.Phase != CupPhase.Idle)
            {
                trick.Reset(Time.timeAsDouble);
                lastPhase = CupPhase.Idle;
            }
            if (table == null || !table.Calibrated)
            {
                return;
            }
            table.Zone.Reset();
            coin.ShowResting(table.Frame.ToWorld(coinRestLocal.ToNumerics()).ToUnity(), TableRotation());
            cup.Place(table.Frame.ToWorld(cupRestLocal.ToNumerics()).ToUnity(), TableRotation());
        }

        private TrackedHand HandFor(HandId id)
        {
            if (id == HandId.Left)
            {
                return leftHand;
            }
            return id == HandId.Right ? rightHand : null;
        }

        private float AverageSuspicion()
        {
            if (crowd == null || crowd.Spectators.Count == 0)
            {
                return 0f;
            }
            float sum = 0f;
            foreach (Spectator s in crowd.Spectators)
            {
                sum += s.Mind.Suspicion;
            }
            return sum / crowd.Spectators.Count;
        }

        private string Name(int index)
        {
            return crowd != null ? crowd.NameOf(index) : "nobody";
        }

        private static WitnessReport Unseen()
        {
            return new WitnessReport { MaxCoverage = 0f, SeenBy = -1 };
        }

        private Quaternion TableRotation()
        {
            return Quaternion.LookRotation(table.Frame.Forward.ToUnity(), Vector3.up);
        }
    }
}
