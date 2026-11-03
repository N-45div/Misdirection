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
    /// Day 2 test harness for the secret-hand verbs: pick the coin up from the mat (pinch and hold),
    /// carry it to the servante at the near table edge, open the hand to ditch it. Every attempt is
    /// logged so the exit criterion ("ditch succeeds about 19 in 20 for two testers") is measured.
    /// Hands only; either hand may be the secret hand.
    /// </summary>
    [DefaultExecutionOrder(10)]
    public sealed class DitchPractice : MonoBehaviour
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string FormatTag = "misdirection_ditch_trials v1";

        public const string CsvHeader =
            "trial,hand,grab_t,end_t,duration_s,outcome,release_dist_m,lost_s,uncertain_s,likely_accidental";

        [SerializeField] private TrackedHand leftHand;
        [SerializeField] private TrackedHand rightHand;
        [SerializeField] private TableCalibrator table;
        [SerializeField] private CoinActor coin;
        [SerializeField] private LookSource lookSource;
        [Tooltip("Optional (Day 3): the audience. Ditches report whether anyone saw them.")]
        [SerializeField] private Crowd crowd;

        [SerializeField] private PinchGripSettings grip = new PinchGripSettings();
        [Tooltip("The pinch point must be this close to the coin to pick it up (metres).")]
        [SerializeField] private float grabRadius = 0.045f;
        [Tooltip("Where the coin rests, in table coordinates (x right, y up, z across the table).")]
        [SerializeField] private Vector3 coinRestLocal = new Vector3(0f, 0.002f, 0.24f);
        [SerializeField] private float resetDelay = 1.2f;
        [SerializeField] private bool writeCsv = true;

        private readonly CoinModel model = new CoinModel();
        private readonly DitchTrialRecorder trials = new DitchTrialRecorder();
        private readonly BothHandsPause pause = new BothHandsPause();
        private PinchGrip leftGrip;
        private PinchGrip rightGrip;
        private double resetAt = -1;
        private string lastMessage = "";
        private TextMeshPro hud;
        private AudioSource audioSource;
        private AudioClip pickupTone, ditchTone, exposedTone, resetTone;
        private SpikeCsvLog log;

        public DitchTrialRecorder Trials { get { return trials; } }

        public void SetCrowd(Crowd audience)
        {
            crowd = audience;
        }

        public void Configure(TrackedHand left, TrackedHand right, TableCalibrator tableCalibrator, CoinActor coinActor, LookSource look)
        {
            leftHand = left;
            rightHand = right;
            table = tableCalibrator;
            coin = coinActor;
            lookSource = look;
        }

        private void Awake()
        {
            leftGrip = new PinchGrip(grip);
            rightGrip = new PinchGrip(grip);
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            pickupTone = Tones.Beep("pickup", 1046f, 0.06f, 0.35f);
            ditchTone = Tones.Thud("ditch", 220f, 90f, 0.18f);
            exposedTone = Tones.Beep("exposed", 1568f, 0.18f, 0.45f);
            resetTone = Tones.Beep("reset", 660f, 0.04f, 0.2f);
            hud = WorldText.Create(transform, "Ditch Practice HUD", new Vector2(0.55f, 0.22f), 0.4f);
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
            CloseLog();
        }

        private void LateUpdate()
        {
            if (leftHand == null || rightHand == null || table == null || coin == null)
            {
                return;
            }
            double now = Time.timeAsDouble;
            pause.Update(now, leftHand.Tracked, rightHand.Tracked);

            if (!table.Calibrated)
            {
                coin.ShowDitched();
                SetHud("<b>Ditch practice</b>\nRest both palms flat on the table, heels at the near edge, and hold still." +
                       (table.Progress > 0f ? "\n" + Bar(table.Progress) : ""));
                PlaceHud();
                return;
            }

            Step(leftHand, leftGrip, now);
            Step(rightHand, rightGrip, now);

            TrackedHand holder = Holder();
            if (holder != null)
            {
                holder.Carrying = true;
                table.Zone.Update(table.Frame.ToLocal(holder.StablePalm.position.ToNumerics()));
                trials.Tick(now, holder.Presence.State == HandPresenceState.Lost, holder.Presence.State == HandPresenceState.Uncertain);
                coin.ShowHeld(holder.StablePalm);
                if (crowd != null)
                {
                    crowd.ExposeSecret(holder.StablePalm.position);
                }
            }
            else if (crowd != null)
            {
                crowd.ClearSecret();
            }

            if (resetAt > 0 && now >= resetAt)
            {
                ResetCoin();
                Play(resetTone);
            }

            table.AllowRecalibration = model.State == CoinState.Resting;
            UpdateHud();
            PlaceHud();
        }

        private void Step(TrackedHand hand, PinchGrip handGrip, double now)
        {
            HandId id = hand.Side;
            bool canGrab = model.Grabbable && Vector3.Distance(hand.PinchPoint, coin.Position) <= grabRadius;
            GripEvent e = handGrip.Update(now, hand.Presence.ReliableForDecisions, hand.Shape, canGrab);
            if (e == GripEvent.Grabbed)
            {
                bool wasResting = model.State == CoinState.Resting;
                if (model.TryGrab(id))
                {
                    resetAt = -1;
                    table.Zone.Reset();
                    if (wasResting || !trials.Active)
                    {
                        trials.Begin(now, id);
                    }
                    Play(pickupTone);
                }
                else
                {
                    handGrip.ForceRelease();
                }
            }
            else if (e == GripEvent.Released && model.Holder == id)
            {
                hand.Carrying = false;
                NVector3 local = table.Frame.ToLocal(hand.StablePalm.position.ToNumerics());
                bool inside = table.Zone.Update(local);
                float distance = table.Zone.Distance(local);
                CoinState state = model.Release(id, inside);
                if (state == CoinState.Ditched)
                {
                    coin.ShowDitched();
                    Play(ditchTone);
                    lastMessage = "<color=#7CFC00>Ditched.</color> " + WitnessLine(hand.StablePalm.position, 1f);
                    Finish(now, TrialOutcome.Ditched, distance);
                }
                else
                {
                    coin.ShowExposed(hand.StablePalm.position, table.Frame.Origin.Y, TableRotation());
                    Play(exposedTone);
                    if (crowd != null)
                    {
                        // The clink draws eyes to the coin, and anyone already looking saw the slip.
                        crowd.MakeSound(hand.StablePalm.position);
                        crowd.ReportMethod(hand.StablePalm.position, 0.6f);
                    }
                    lastMessage = "<color=#FF6347>Exposed</color> " + Mathf.RoundToInt(distance * 100f) + " cm from the servante.";
                    Finish(now, TrialOutcome.Exposed, distance);
                }
                resetAt = now + resetDelay;
            }
        }

        /// <summary>Reports a secret move to the crowd and says who (if anyone) saw it.</summary>
        private string WitnessLine(Vector3 point, float severity)
        {
            if (crowd == null)
            {
                return "Clean.";
            }
            WitnessReport report = crowd.ReportMethod(point, severity);
            if (report.Unseen)
            {
                return "<color=#7CFC00>Nobody saw it.</color>";
            }
            return "<color=#FF9800>The " + crowd.NameOf(report.SeenBy) + " saw it</color> (" +
                   Mathf.RoundToInt(report.MaxCoverage * 100f) + "%).";
        }

        private void Finish(double now, TrialOutcome outcome, float distance)
        {
            DitchTrial trial = trials.End(now, outcome, distance);
            WriteTrial(trial);
        }

        private void ResetCoin()
        {
            if (trials.Active)
            {
                Finish(Time.timeAsDouble, TrialOutcome.Abandoned, float.NaN);
            }
            model.Reset();
            leftGrip.ForceRelease();
            rightGrip.ForceRelease();
            if (leftHand != null)
            {
                leftHand.Carrying = false;
            }
            if (rightHand != null)
            {
                rightHand.Carrying = false;
            }
            resetAt = -1;
            if (table != null && table.Calibrated)
            {
                table.Zone.Reset();
                coin.ShowResting(table.Frame.ToWorld(coinRestLocal.ToNumerics()).ToUnity(), TableRotation());
            }
        }

        private TrackedHand Holder()
        {
            if (model.State != CoinState.Held)
            {
                return null;
            }
            return model.Holder == HandId.Left ? leftHand : rightHand;
        }

        private Quaternion TableRotation()
        {
            return Quaternion.LookRotation(table.Frame.Forward.ToUnity(), Vector3.up);
        }

        // ------------------------------------------------------------ HUD

        private void UpdateHud()
        {
            int successes;
            int attempts;
            trials.Recent(out successes, out attempts);
            string status;
            if (pause.Paused)
            {
                status = "<color=#FFD700>The crowd waits. Show both hands.</color>";
            }
            else if (model.State == CoinState.Held)
            {
                status = "Carry it to the near edge and open your hand.";
            }
            else if (model.State == CoinState.Resting)
            {
                status = "Pinch the coin and pick it up.";
            }
            else
            {
                status = lastMessage;
            }
            string score = attempts == 0
                ? "Target: 19 of 20 ditched"
                : "Last " + attempts + ": <b>" + successes + " ditched</b>" +
                  (trials.MeetsExitCriterion() ? "  <color=#7CFC00>Day 2 target met</color>" : "  (target 19/20)");
            string crowdLine = crowd != null && crowd.Spectators.Count > 0 ? "\n<size=55%>" + crowd.Summary() + "</size>" : "";
            SetHud("<b>Ditch practice</b>  ·  " + score + "\n" + status +
                   "\n<size=60%>" + HandLine("L", leftHand) + "   " + HandLine("R", rightHand) + "</size>" + crowdLine);
        }

        private static string HandLine(string label, TrackedHand hand)
        {
            HandPresence p = hand.Presence;
            if (p == null)
            {
                return label + ": -";
            }
            string o = float.IsNaN(hand.Shape.Openness) ? "-" : hand.Shape.Openness.ToString("0.0", Inv);
            return label + ": " + p.State + " pinch " + hand.Shape.PinchStrength.ToString("0.0", Inv) + " open " + o;
        }

        private void PlaceHud()
        {
            Vector3 viewer = lookSource != null && lookSource.Head != null ? lookSource.Head.position : Vector3.up * 1.2f;
            Vector3 pos;
            if (table != null && table.Calibrated)
            {
                pos = table.Frame.ToWorld(new NVector3(0f, 0.28f, 0.55f)).ToUnity();
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

        private void SetHud(string text)
        {
            if (hud != null)
            {
                hud.text = text;
            }
        }

        private static string Bar(float fraction)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(fraction * 10f), 0, 10);
            return "<color=#7CFC00>" + new string('■', filled) + "</color>" + new string('□', 10 - filled);
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }

        // ------------------------------------------------------------ CSV

        private void WriteTrial(DitchTrial t)
        {
            if (!writeCsv)
            {
                return;
            }
            if (log == null)
            {
                OpenLog();
            }
            if (log == null)
            {
                return;
            }
            log.WriteLine(string.Join(",", new[]
            {
                t.Number.ToString(Inv),
                t.Hand == HandId.Left ? "L" : "R",
                t.GrabTime.ToString("F3", Inv),
                t.EndTime.ToString("F3", Inv),
                t.Duration.ToString("F3", Inv),
                t.Outcome.ToString().ToLowerInvariant(),
                float.IsNaN(t.ReleaseDistance) ? "" : t.ReleaseDistance.ToString("F3", Inv),
                t.LostSeconds.ToString("F3", Inv),
                t.UncertainSeconds.ToString("F3", Inv),
                t.LikelyAccidental() ? "1" : "0",
            }));
            log.Flush();
        }

        private void OpenLog()
        {
            string headset = OVRPlugin.GetSystemHeadsetType().ToString();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
            string file = Path.Combine(Application.persistentDataPath, "ditch", "ditch_" + headset + "_" + stamp + ".csv");
            List<KeyValuePair<string, string>> meta = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("headset", headset),
                new KeyValuePair<string, string>("app_version", Application.version),
                new KeyValuePair<string, string>("grab_radius_m", grabRadius.ToString(Inv)),
            };
            try
            {
                log = new SpikeCsvLog(file, meta, CsvHeader, FormatTag);
                Debug.Log("[Misdirection] Ditch trials logging to " + file);
            }
            catch (Exception e)
            {
                writeCsv = false;
                Debug.LogError("[Misdirection] Could not open ditch log: " + e.Message);
            }
        }

        private void CloseLog()
        {
            if (log != null)
            {
                log.Dispose();
                log = null;
            }
        }
    }
}
