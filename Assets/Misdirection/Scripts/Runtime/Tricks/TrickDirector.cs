using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Misdirection.Audience;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Core.Tricks;
using Misdirection.Hands;
using Misdirection.Spike;
using Misdirection.Table;
using TMPro;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection.Tricks
{
    /// <summary>
    /// Trick 1 in the headset (Day 4 fun gate): show the coin, misdirect, ditch it in the servante,
    /// show the empty hand; then steal it back and pluck it from behind a spectator's ear by
    /// looking at them and pinching. The crowd reacts with applause, a gasp or a heckle.
    /// Rules live in Misdirection.Core.Tricks.VanishTrick; this wires hands, coin, crowd and feedback.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class TrickDirector : MonoBehaviour
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string FormatTag = "misdirection_show_events v1";
        public const string CsvHeader = "t,phase,event,kind,amazement,tips,reaction,seen_by,spectator";

        [SerializeField] private TrackedHand leftHand;
        [SerializeField] private TrackedHand rightHand;
        [SerializeField] private TableCalibrator table;
        [SerializeField] private CoinActor coin;
        [SerializeField] private Crowd crowd;
        [SerializeField] private LookSource lookSource;

        [SerializeField] private PinchGripSettings grip = new PinchGripSettings();
        [SerializeField] private VanishTrickSettings trickSettings = new VanishTrickSettings();
        [SerializeField] private float grabRadius = 0.045f;
        [Tooltip("To steal, the pinch must be this close to (or inside) the servante (metres).")]
        [SerializeField] private float stealMargin = 0.03f;
        [SerializeField] private Vector3 coinRestLocal = new Vector3(0f, 0.002f, 0.24f);

        [Header("Reveal and production gestures")]
        [Range(0f, 1f)] [SerializeField] private float revealOpenness = 0.75f;
        [SerializeField] private float revealHeight = 0.12f;
        [Range(0f, 1f)] [SerializeField] private float producePinchStrength = 0.8f;
        [SerializeField] private float produceHeight = 0.12f;
        [SerializeField] private float produceFlightSeconds = 0.45f;
        [SerializeField] private bool writeCsv = true;

        private readonly CoinModel model = new CoinModel();
        private VanishTrick trick;
        private PinchGrip leftGrip;
        private PinchGrip rightGrip;
        private TrickPhase lastPhase = TrickPhase.Idle;
        private int lookIndex = -1;
        private double lookSince;
        private bool flying;
        private double flightStart;
        private Vector3 flightFrom;

        private TextMeshPro hud;
        private TextMeshPro reactionText;
        private double reactionUntil;
        private Vector3 reactionAnchor;
        private AudioSource audioSource;
        private AudioClip pickupTone, ditchTone, clinkTone, applause, gasp, heckle, sparkle;
        private SpikeCsvLog log;

        public VanishTrick Trick { get { return trick; } }

        public void Configure(TrackedHand left, TrackedHand right, TableCalibrator tableCalibrator, CoinActor coinActor, Crowd audience, LookSource look)
        {
            leftHand = left;
            rightHand = right;
            table = tableCalibrator;
            coin = coinActor;
            crowd = audience;
            lookSource = look;
        }

        public void SetCrowd(Crowd audience)
        {
            crowd = audience;
        }

        private void Awake()
        {
            trick = new VanishTrick(trickSettings);
            leftGrip = new PinchGrip(grip);
            rightGrip = new PinchGrip(grip);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            pickupTone = Tones.Beep("pickup", 1046f, 0.06f, 0.3f);
            ditchTone = Tones.Thud("ditch", 220f, 90f, 0.18f, 0.4f);
            clinkTone = Tones.Beep("clink", 1568f, 0.18f, 0.45f);
            applause = Tones.Noise("applause", 2.2f, 0.15f, 0.45f, 1f, 11);
            gasp = Tones.Noise("gasp", 0.6f, 0.08f, 0.35f, 0f, 3);
            heckle = Tones.Thud("heckle", 160f, 120f, 0.5f, 0.5f);
            sparkle = Tones.Chirp("sparkle", 900f, 2400f, 0.3f);
            hud = WorldText.Create(transform, "Trick HUD", new Vector2(0.6f, 0.22f), 0.4f);
            reactionText = WorldText.Create(transform, "Reaction", new Vector2(0.5f, 0.15f), 0.6f);
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }
        }

        private void OnEnable()
        {
            if (table != null)
            {
                table.OnCalibrated += ResetCoin;
            }
        }

        private void OnDisable()
        {
            if (table != null)
            {
                table.OnCalibrated -= ResetCoin;
            }
            if (log != null)
            {
                log.Dispose();
                log = null;
            }
        }

        private void LateUpdate()
        {
            if (leftHand == null || rightHand == null || table == null || coin == null)
            {
                return;
            }
            double now = Time.timeAsDouble;
            Vector3 viewer = Viewer();
            if (!table.Calibrated)
            {
                coin.ShowDitched();
                hud.text = "<b>Misdirection</b>\nRest both palms flat on the table, heels at the near edge, and hold still.";
                PlaceHud(viewer);
                return;
            }

            TrackLook(now);
            Step(leftHand, leftGrip, now);
            Step(rightHand, rightGrip, now);

            TrackedHand holder = HandFor(model.Holder);
            bool secret = trick.Phase == TrickPhase.Misdirect || trick.Phase == TrickPhase.Loaded;
            if (holder != null && model.State == CoinState.Held)
            {
                holder.Carrying = true;
                table.Zone.Update(table.Frame.ToLocal(holder.StablePalm.position.ToNumerics()));
                if (!flying)
                {
                    coin.ShowHeld(holder.StablePalm);
                }
                if (crowd != null && secret)
                {
                    crowd.ExposeSecret(holder.StablePalm.position);
                }
            }
            if (crowd != null && (!secret || holder == null))
            {
                crowd.ClearSecret();
            }

            RevealResult? reveal = trick.Tick(now, BuildInput(holder));
            if (reveal.HasValue)
            {
                OnReveal(reveal.Value, now);
            }
            UpdateFlight(now, holder);

            if (trick.Phase != lastPhase)
            {
                OnPhaseChanged(lastPhase, trick.Phase, now);
                lastPhase = trick.Phase;
            }

            UpdateHud();
            PlaceHud(viewer);
            UpdateReaction(now, viewer);
        }

        // ------------------------------------------------------------ hands and coin

        private void Step(TrackedHand hand, PinchGrip handGrip, double now)
        {
            HandId id = hand.Side;
            bool pickup = trick.Phase == TrickPhase.Idle && model.State == CoinState.Resting
                && Vector3.Distance(hand.PinchPoint, coin.Position) <= grabRadius;
            bool steal = trick.CanSteal && model.State == CoinState.Ditched
                && table.Zone.Distance(table.Frame.ToLocal(hand.PinchPoint.ToNumerics())) <= stealMargin;

            GripEvent e = handGrip.Update(now, hand.Presence.ReliableForDecisions, hand.Shape, pickup || steal);
            if (e == GripEvent.Grabbed)
            {
                if (pickup && model.TryGrab(id))
                {
                    trick.PickedUp(now, id);
                    Play(pickupTone);
                    Log(now, "pickup", "", null);
                }
                else if (steal && model.TrySteal(id))
                {
                    WitnessReport w = crowd != null ? crowd.ReportMethod(hand.PinchPoint, 0.8f) : Unseen();
                    trick.Stolen(now, id, w);
                    Log(now, "steal", w.Unseen ? "unseen" : "seen by " + Name(w.SeenBy), null);
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
                bool inside = table.Zone.Update(table.Frame.ToLocal(palm.ToNumerics()));
                CoinState state = model.Release(id, inside);
                WitnessReport w;
                if (state == CoinState.Ditched)
                {
                    coin.ShowDitched();
                    Play(ditchTone);
                    w = crowd != null ? crowd.ReportMethod(palm, 1f) : Unseen();
                }
                else
                {
                    coin.ShowExposed(palm, table.Frame.Origin.Y, TableRotation());
                    Play(clinkTone);
                    if (crowd != null)
                    {
                        crowd.MakeSound(palm);
                    }
                    w = crowd != null ? crowd.ReportMethod(palm, 0.6f) : Unseen();
                }
                trick.Released(now, id, inside, w);
                Log(now, inside ? "ditch" : "drop", w.Unseen ? "unseen" : "seen by " + Name(w.SeenBy), null);
            }
        }

        private TrickInput BuildInput(TrackedHand holder)
        {
            float tableY = table.Frame.Origin.Y;
            TrickInput input = new TrickInput();
            input.HolderHeight = holder != null && model.State == CoinState.Held ? holder.StablePalm.position.y - tableY : float.NaN;

            TrackedHand revealer = HandFor(trick.LastHolder);
            input.EmptyHandShown = revealer != null && model.Holder != revealer.Side && IsShowingEmpty(revealer, tableY);

            input.LookedAt = lookIndex;
            input.LookDwell = lookIndex >= 0 ? Time.timeAsDouble - lookSince : 0;
            input.ProducePinch = holder != null && trick.Phase == TrickPhase.Loaded
                && holder.Presence.ReliableForDecisions
                && holder.Shape.PinchStrength >= producePinchStrength
                && holder.StablePalm.position.y - tableY >= produceHeight;

            input.Spectators = crowd != null ? crowd.Spectators.Count : 0;
            input.AverageSuspicion = AverageSuspicion();
            return input;
        }

        /// <summary>Open, raised hand turned toward the crowd (palm out or up, not down).</summary>
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

        private void TrackLook(double now)
        {
            int looked = crowd != null && crowd.Model != null ? crowd.Model.LookedAt : -1;
            if (looked != lookIndex)
            {
                lookIndex = looked;
                lookSince = now;
            }
        }

        private void OnReveal(RevealResult r, double now)
        {
            Log(now, "reveal", r.Kind, r);
            switch (r.Reaction)
            {
                case Reaction.Applause: Play(applause); break;
                case Reaction.Gasp: Play(gasp); break;
                default: Play(heckle); break;
            }

            string line;
            int anchorIndex = r.SeenBy >= 0 ? r.SeenBy : r.Spectator;
            if (r.Reaction == Reaction.Heckle)
            {
                line = r.SeenBy >= 0 ? "<color=#FF7043>\"I saw that!\"</color>" : "<color=#FFB74D>\"Hm.\"</color>";
            }
            else
            {
                line = (r.Reaction == Reaction.Applause ? "<color=#7CFC00>Applause!</color>" : "<color=#FFD54F>\"Ooh!\"</color>") +
                       "\n<size=70%>+" + r.Tips + " tips</size>";
            }
            ShowReaction(line, anchorIndex, now);

            if (r.Kind == "produce" && r.Spectator >= 0 && crowd != null && r.Spectator < crowd.Spectators.Count)
            {
                Play(sparkle);
                flying = true;
                flightStart = now;
                flightFrom = crowd.Spectators[r.Spectator].EarPosition;
                coin.ShowAt(flightFrom, Quaternion.identity);
            }
        }

        private void UpdateFlight(double now, TrackedHand holder)
        {
            if (!flying)
            {
                return;
            }
            float t = (float)((now - flightStart) / Mathf.Max(0.05f, produceFlightSeconds));
            Vector3 to = holder != null ? holder.PinchPoint : coin.Position;
            if (t >= 1f || holder == null)
            {
                flying = false;
                return;
            }
            float ease = 1f - (1f - t) * (1f - t);
            Vector3 arc = Vector3.up * Mathf.Sin(t * Mathf.PI) * 0.06f;
            coin.ShowAt(Vector3.Lerp(flightFrom, to, ease) + arc, Quaternion.identity);
        }

        private void OnPhaseChanged(TrickPhase from, TrickPhase to, double now)
        {
            Log(now, "phase", to.ToString(), null);
            if (to == TrickPhase.Busted)
            {
                Play(heckle);
                ShowReaction("<color=#FF7043>" + trick.Prompt + "</color>", -1, now);
            }
            if (to == TrickPhase.Idle)
            {
                if (from == TrickPhase.Finale && crowd != null)
                {
                    // A new trick starts with a fresh, curious crowd.
                    foreach (Spectator s in crowd.Spectators)
                    {
                        s.Mind.ResetSuspicion();
                    }
                }
                ResetCoin();
            }
        }

        private void ResetCoin()
        {
            model.Reset();
            leftGrip.ForceRelease();
            rightGrip.ForceRelease();
            leftHand.Carrying = false;
            rightHand.Carrying = false;
            flying = false;
            if (trick.Phase != TrickPhase.Idle)
            {
                trick.Reset(Time.timeAsDouble);
                lastPhase = TrickPhase.Idle;
            }
            if (table != null && table.Calibrated)
            {
                table.Zone.Reset();
                coin.ShowResting(table.Frame.ToWorld(coinRestLocal.ToNumerics()).ToUnity(), TableRotation());
            }
        }

        // ------------------------------------------------------------ feedback

        private void UpdateHud()
        {
            string crowdLine = crowd != null && crowd.Spectators.Count > 0 ? "\n<size=55%>" + crowd.Summary() + "</size>" : "";
            hud.text = "<b>" + trick.Prompt + "</b>\n<size=70%>Tips: " + trick.TotalTips +
                       (trick.LastReveal.Kind != null ? "   Last: " + trick.LastReveal.Reaction + " (" +
                        Mathf.RoundToInt(trick.LastReveal.Amazement * 100f) + "%)" : "") + "</size>" + crowdLine;
        }

        private void PlaceHud(Vector3 viewer)
        {
            Vector3 pos;
            if (table.Calibrated)
            {
                pos = table.Frame.ToWorld(new NVector3(0f, 0.16f, 0.62f)).ToUnity();
            }
            else
            {
                Vector3 fwd = lookSource != null && lookSource.Head != null ? lookSource.Head.forward : Vector3.forward;
                fwd.y = 0f;
                pos = viewer + (fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward) * 0.8f - Vector3.up * 0.15f;
            }
            hud.transform.position = pos;
            WorldText.Face(hud.transform, viewer);
        }

        private void ShowReaction(string text, int spectator, double now)
        {
            reactionText.text = text;
            reactionUntil = now + 2.5;
            reactionAnchor = crowd != null && spectator >= 0 && spectator < crowd.Spectators.Count
                ? crowd.Spectators[spectator].AboveHead
                : table.Frame.ToWorld(new NVector3(0f, 0.45f, 0.9f)).ToUnity();
        }

        private void UpdateReaction(double now, Vector3 viewer)
        {
            bool visible = now < reactionUntil;
            reactionText.gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }
            float rise = (float)(2.5 - (reactionUntil - now)) * 0.04f;
            reactionText.transform.position = reactionAnchor + Vector3.up * rise;
            WorldText.Face(reactionText.transform, viewer);
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        // ------------------------------------------------------------ helpers

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

        private Vector3 Viewer()
        {
            return lookSource != null && lookSource.Head != null ? lookSource.Head.position : Vector3.up * 1.2f;
        }

        private void Log(double now, string evt, string detail, RevealResult? r)
        {
            if (!writeCsv)
            {
                return;
            }
            if (log == null)
            {
                string headset = OVRPlugin.GetSystemHeadsetType().ToString();
                string file = Path.Combine(Application.persistentDataPath, "show",
                    "show_" + headset + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv) + ".csv");
                try
                {
                    log = new SpikeCsvLog(file, new List<KeyValuePair<string, string>>
                    {
                        new KeyValuePair<string, string>("headset", headset),
                        new KeyValuePair<string, string>("app_version", Application.version),
                    }, CsvHeader, FormatTag);
                }
                catch (Exception e)
                {
                    writeCsv = false;
                    Debug.LogError("[Misdirection] Could not open show log: " + e.Message);
                    return;
                }
            }
            string kind = r.HasValue ? r.Value.Kind : detail;
            log.WriteLine(string.Join(",", new[]
            {
                now.ToString("F3", Inv),
                trick.Phase.ToString(),
                evt,
                (kind ?? "").Replace(',', ' '),
                r.HasValue ? r.Value.Amazement.ToString("F3", Inv) : "",
                r.HasValue ? r.Value.Tips.ToString(Inv) : "",
                r.HasValue ? r.Value.Reaction.ToString() : "",
                r.HasValue && r.Value.SeenBy >= 0 ? Name(r.Value.SeenBy) : "",
                r.HasValue && r.Value.Spectator >= 0 ? Name(r.Value.Spectator) : "",
            }));
            log.Flush();
        }
    }
}
