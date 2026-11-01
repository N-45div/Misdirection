using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using TMPro;
using UnityEngine;

namespace Misdirection.Spike
{
    /// <summary>
    /// Day 1 tracking spike (Gate 1). Measures how reliably the "secret hand" stays tracked
    /// while the player turns their head toward a spectator 0, 20, 35 and 50 degrees away.
    ///
    /// Hands only: the tester starts a run by pinching both hands for one second.
    /// Each run writes a CSV to Application.persistentDataPath/spike and shows a summary
    /// with the widest crowd arc that kept move-window failures at or under 10%.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TrackingSpikeController : MonoBehaviour
    {
        private enum Phase { Idle, Settle, Record }

        private const float PinchOnStrength = 0.8f;
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [Header("Rig (auto-filled when empty)")]
        [SerializeField] private OVRCameraRig rig;
        [SerializeField] private LookSource lookSource;
        [SerializeField] private OVRHand leftHand;
        [SerializeField] private OVRHand rightHand;

        [Header("Protocol")]
        [Tooltip("Head-turn angles in degrees. 0 is the centre condition.")]
        [SerializeField] private float[] angles = { 0f, 20f, 35f, 50f };
        [Tooltip("Also measure turning toward the secret hand (the easier case).")]
        [SerializeField] private bool includeTowardTurns = true;
        [Min(1)] [SerializeField] private int repeats = 2;
        [SerializeField] private float introSeconds = 7f;
        [SerializeField] private float settleSeconds = 2f;
        [SerializeField] private float recordSeconds = 6f;
        [SerializeField] private float startPinchHoldSeconds = 1f;

        [Header("Scoring (keep in sync with tools/analyze_tracking_spike.py)")]
        [SerializeField] private float moveWindowSeconds = 1.5f;
        [SerializeField] private float dropoutSeconds = 0.15f;
        [Range(0f, 1f)] [SerializeField] private float maxWindowFailRate = 0.1f;

        [Header("Layout (metres / degrees)")]
        [SerializeField] private float markerDistance = 1.4f;
        [SerializeField] private float markerHeightAboveEyes = 0.1f;
        [SerializeField] private float markerDiameter = 0.08f;
        [SerializeField] private float aimToleranceDegrees = 6f;
        [SerializeField] private float panelDistance = 1.1f;

        private readonly Dictionary<string, SpikeCell> cells = new Dictionary<string, SpikeCell>();
        private readonly StringBuilder row = new StringBuilder(256);

        private GameObject marker;
        private Renderer markerRenderer;
        private TextMeshPro markerLabel;
        private TextMeshPro panel;
        private AudioSource audioSource;
        private AudioClip startTone;
        private AudioClip endTone;
        private AudioClip doneTone;

        private bool calibrated;
        private bool panelPlaced;
        private Vector3 calibOrigin;
        private Vector3 calibForward = Vector3.forward;

        private Phase phase = Phase.Idle;
        private SpikeCondition active;
        private MoveWindowScorer scorer;
        private float phaseEnd;
        private double runStart;
        private SpikeCsvLog log;
        private int runCount;
        private bool startRequested;
        private string lastSummary;

        private Transform Head
        {
            get { return lookSource != null && lookSource.Head != null ? lookSource.Head : (rig != null ? rig.centerEyeAnchor : null); }
        }

        /// <summary>Start a run without the pinch gesture (Inspector context menu or tests).</summary>
        [ContextMenu("Start protocol now")]
        public void RequestStart()
        {
            startRequested = true;
        }

        private IEnumerator Start()
        {
            ResolveReferences();
            BuildVisuals();

            while (true)
            {
                string prompt = runCount == 0 ? IntroText() : lastSummary + "\n\n<size=80%>Pinch both hands for 1 s to run again.</size>";
                yield return WaitForStartGesture(prompt);
                yield return RunProtocol();
            }
        }

        private void LateUpdate()
        {
            Transform head = Head;
            if (head == null)
            {
                return;
            }

            PlacePanel(head);
            UpdateMarkerAim();

            if (phase == Phase.Idle)
            {
                return;
            }

            if (lookSource != null)
            {
                lookSource.Refresh();
            }

            if (log != null)
            {
                double t = Time.timeAsDouble - runStart;
                LogHand(HandSide.Left, head, t);
                LogHand(HandSide.Right, head, t);
            }

            if (phase == Phase.Record && scorer != null)
            {
                OVRHand secret = active.SecretHand == HandSide.Left ? leftHand : rightHand;
                bool tracked = secret != null && secret.IsTracked;
                bool high = tracked && secret.HandConfidence == OVRHand.TrackingConfidence.High;
                scorer.Add(Time.timeAsDouble, tracked, high);

                float remaining = Mathf.Max(0f, phaseEnd - Time.time);
                markerLabel.text = "<color=#ff4040>●</color> REC " + remaining.ToString("0.0", Inv) + " s";
            }
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && log != null)
            {
                log.Flush();
            }
        }

        private void OnDestroy()
        {
            CloseLog();
        }

        // ---------------------------------------------------------------- protocol

        private IEnumerator WaitForStartGesture(string message)
        {
            startRequested = false;

            // A pinch still held from the last run must be released first.
            while (BothPinching() && !startRequested)
            {
                SetPanel(message + "\n\n<size=70%>Release both pinches…</size>");
                yield return null;
            }

            float held = 0f;
            while (!startRequested)
            {
                held = BothPinching() ? held + Time.deltaTime : 0f;
                if (held >= startPinchHoldSeconds)
                {
                    break;
                }
                SetPanel(message + (held > 0f ? "\n" + ProgressBar(held / startPinchHoldSeconds) : ""));
                yield return null;
            }
        }

        private IEnumerator RunProtocol()
        {
            Calibrate();
            cells.Clear();
            List<SpikeCondition> conditions = BuildConditions();
            OpenLog();

            bool haveSecret = false;
            HandSide currentSecret = HandSide.Left;

            for (int i = 0; i < conditions.Count; i++)
            {
                SpikeCondition c = conditions[i];

                if (!haveSecret || c.SecretHand != currentSecret)
                {
                    haveSecret = true;
                    currentSecret = c.SecretHand;
                    marker.SetActive(false);
                    float until = Time.time + introSeconds;
                    while (Time.time < until)
                    {
                        SetPanel(SecretHandIntro(c.SecretHand, until - Time.time));
                        yield return null;
                    }
                }

                active = c;
                PlaceMarker(c.SignedTargetDeg);
                marker.SetActive(true);
                SetPanel("<b>Condition " + (i + 1) + " / " + conditions.Count + "</b>\nFace the ball. Keep the secret hand still.");
                markerLabel.text = "Look here\n<size=70%>" + Describe(c) + "</size>";
                Play(startTone);

                phase = Phase.Settle;
                phaseEnd = Time.time + settleSeconds;
                while (Time.time < phaseEnd)
                {
                    yield return null;
                }

                scorer = new MoveWindowScorer(moveWindowSeconds, dropoutSeconds);
                phase = Phase.Record;
                phaseEnd = Time.time + recordSeconds;
                while (Time.time < phaseEnd)
                {
                    yield return null;
                }

                phase = Phase.Idle;
                scorer.Finish();
                AddToCell(c, scorer);
                scorer = null;
                Play(endTone);
                if (log != null)
                {
                    log.Flush();
                }
            }

            marker.SetActive(false);
            string path = log != null ? log.FilePath : "(not saved)";
            CloseLog();
            Play(doneTone);
            runCount++;
            lastSummary = BuildSummary(path);
            Debug.Log("[Misdirection] Tracking spike run " + runCount + " finished.\n" + StripTags(lastSummary));
        }

        private List<SpikeCondition> BuildConditions()
        {
            List<SpikeCondition> list = new List<SpikeCondition>();
            int index = 0;
            foreach (HandSide secret in new[] { HandSide.Left, HandSide.Right })
            {
                for (int r = 0; r < repeats; r++)
                {
                    foreach (float a in angles)
                    {
                        if (a < 0.5f)
                        {
                            list.Add(Make(index++, r, secret, TurnKind.Center, 0f));
                            continue;
                        }
                        list.Add(Make(index++, r, secret, TurnKind.Away, a));
                    }
                    if (includeTowardTurns)
                    {
                        foreach (float a in angles)
                        {
                            if (a >= 0.5f)
                            {
                                list.Add(Make(index++, r, secret, TurnKind.Toward, a));
                            }
                        }
                    }
                }
            }
            return list;
        }

        private static SpikeCondition Make(int index, int repeat, HandSide secret, TurnKind turn, float angle)
        {
            SpikeCondition c = new SpikeCondition();
            c.Index = index;
            c.Repeat = repeat;
            c.SecretHand = secret;
            c.Turn = turn;
            c.AngleDeg = angle;
            return c;
        }

        private void AddToCell(SpikeCondition c, MoveWindowScorer s)
        {
            string key = SpikeScoring.Key(c.SecretHand, c.Turn, c.AngleDeg);
            SpikeCell cell;
            if (!cells.TryGetValue(key, out cell))
            {
                cell = new SpikeCell();
                cells[key] = cell;
            }
            cell.Add(s);
        }

        // ---------------------------------------------------------------- logging

        private void OpenLog()
        {
            CloseLog();
            string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv);
            string headset = SafeHeadsetName();
            string file = Path.Combine(Application.persistentDataPath, "spike", "spike_" + headset + "_" + stamp + ".csv");

            List<KeyValuePair<string, string>> meta = new List<KeyValuePair<string, string>>();
            meta.Add(new KeyValuePair<string, string>("headset", headset));
            meta.Add(new KeyValuePair<string, string>("device_model", SystemInfo.deviceModel));
            meta.Add(new KeyValuePair<string, string>("app_version", Application.version));
            meta.Add(new KeyValuePair<string, string>("unity", Application.unityVersion));
            meta.Add(new KeyValuePair<string, string>("started_local", DateTime.Now.ToString("s", Inv)));
            meta.Add(new KeyValuePair<string, string>("settle_s", settleSeconds.ToString(Inv)));
            meta.Add(new KeyValuePair<string, string>("record_s", recordSeconds.ToString(Inv)));
            meta.Add(new KeyValuePair<string, string>("window_s", moveWindowSeconds.ToString(Inv)));
            meta.Add(new KeyValuePair<string, string>("dropout_s", dropoutSeconds.ToString(Inv)));
            meta.Add(new KeyValuePair<string, string>("max_fail_rate", maxWindowFailRate.ToString(Inv)));
            meta.Add(new KeyValuePair<string, string>("marker_distance_m", markerDistance.ToString(Inv)));

            try
            {
                log = new SpikeCsvLog(file, meta);
                runStart = Time.timeAsDouble;
                Debug.Log("[Misdirection] Tracking spike logging to " + file);
            }
            catch (Exception e)
            {
                log = null;
                Debug.LogError("[Misdirection] Could not open spike log: " + e.Message);
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

        private void LogHand(HandSide side, Transform head, double t)
        {
            OVRHand hand = side == HandSide.Left ? leftHand : rightHand;
            bool valid = hand != null && hand.IsDataValid;
            bool tracked = hand != null && hand.IsTracked;
            bool high = tracked && hand.HandConfidence == OVRHand.TrackingConfidence.High;
            float pinch = hand != null ? hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) : 0f;

            Vector3 handPos = HandPosition(side, hand);
            Vector3 toHand = handPos - head.position;
            Vector3 local = head.InverseTransformPoint(handPos);
            float handAz = Vector3.SignedAngle(Flatten(head.forward), Flatten(toHand), Vector3.up);
            float handEl = toHand.sqrMagnitude > 1e-6f ? Mathf.Asin(Mathf.Clamp(toHand.normalized.y, -1f, 1f)) * Mathf.Rad2Deg : 0f;
            float headYaw = Vector3.SignedAngle(calibForward, Flatten(head.forward), Vector3.up);
            float headPitch = Mathf.Asin(Mathf.Clamp(head.forward.y, -1f, 1f)) * Mathf.Rad2Deg;
            string look = lookSource != null && lookSource.ActiveKind == LookSource.Kind.Eyes ? "eyes" : "head";

            row.Length = 0;
            row.Append(t.ToString("F4", Inv)).Append(',')
               .Append(Time.frameCount.ToString(Inv)).Append(',')
               .Append(Time.unscaledDeltaTime.ToString("F5", Inv)).Append(',')
               .Append(phase == Phase.Record ? "record" : "settle").Append(',')
               .Append(active.Index.ToString(Inv)).Append(',')
               .Append(active.Repeat.ToString(Inv)).Append(',')
               .Append(active.SecretCode).Append(',')
               .Append(active.TurnCode).Append(',')
               .Append(active.SignedTargetDeg.ToString("F1", Inv)).Append(',')
               .Append(look).Append(',')
               .Append(headYaw.ToString("F2", Inv)).Append(',')
               .Append(headPitch.ToString("F2", Inv)).Append(',')
               .Append(side == HandSide.Left ? "L" : "R").Append(',')
               .Append(valid ? '1' : '0').Append(',')
               .Append(tracked ? '1' : '0').Append(',')
               .Append(high ? '1' : '0').Append(',')
               .Append(pinch.ToString("F3", Inv)).Append(',')
               .Append(handAz.ToString("F2", Inv)).Append(',')
               .Append(handEl.ToString("F2", Inv)).Append(',')
               .Append(local.x.ToString("F4", Inv)).Append(',')
               .Append(local.y.ToString("F4", Inv)).Append(',')
               .Append(local.z.ToString("F4", Inv));
            log.WriteLine(row.ToString());
        }

        private Vector3 HandPosition(HandSide side, OVRHand hand)
        {
            if (rig != null)
            {
                Transform anchor = side == HandSide.Left ? rig.leftHandAnchor : rig.rightHandAnchor;
                if (anchor != null)
                {
                    return anchor.position;
                }
            }
            return hand != null ? hand.transform.position : Vector3.zero;
        }

        // ---------------------------------------------------------------- summary

        private string BuildSummary(string path)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("<b>Run ").Append(runCount).Append(" done</b>  ·  ").Append(SafeHeadsetName())
              .Append("  ·  look: ").Append(lookSource != null && lookSource.ActiveKind == LookSource.Kind.Eyes ? "eyes" : "head").Append('\n');
            sb.Append("<size=80%>Lost move windows (pass ≤ ").Append(Mathf.RoundToInt(maxWindowFailRate * 100f)).Append("%)</size>\n");

            foreach (HandSide hand in new[] { HandSide.Left, HandSide.Right })
            {
                foreach (TurnKind turn in new[] { TurnKind.Away, TurnKind.Toward })
                {
                    if (turn == TurnKind.Toward && !includeTowardTurns)
                    {
                        continue;
                    }
                    sb.Append(hand == HandSide.Left ? "L " : "R ").Append(turn == TurnKind.Away ? "away  " : "toward").Append(":  ");
                    foreach (float a in angles)
                    {
                        TurnKind kind = a < 0.5f ? TurnKind.Center : turn;
                        SpikeCell cell;
                        string cellText = "–";
                        bool pass = false;
                        if (cells.TryGetValue(SpikeScoring.Key(hand, kind, a), out cell) && cell.Windows > 0)
                        {
                            pass = cell.FailRate <= maxWindowFailRate + 1e-6f;
                            cellText = Mathf.RoundToInt(cell.FailRate * 100f) + "%";
                        }
                        sb.Append(Mathf.RoundToInt(a)).Append("° ")
                          .Append(pass ? "<color=#7CFC00>" : "<color=#FF6347>").Append(cellText).Append("</color>   ");
                    }
                    sb.Append('\n');
                }
            }

            float arc = SpikeScoring.UsableAngle(cells, angles, TurnKind.Away, maxWindowFailRate);
            sb.Append('\n');
            if (arc < 0f)
            {
                sb.Append("<b><color=#FF6347>Secret hand unreliable even at 0°.</color></b> Check lighting and hand height.");
            }
            else
            {
                sb.Append("<b>Widest usable crowd arc: ±").Append(Mathf.RoundToInt(arc)).Append("°</b>");
            }
            sb.Append("\n<size=60%>").Append(Path.GetFileName(path)).Append("</size>");
            return sb.ToString();
        }

        // ---------------------------------------------------------------- input

        private bool BothPinching()
        {
            return IsPinching(leftHand) && IsPinching(rightHand);
        }

        private static bool IsPinching(OVRHand hand)
        {
            return hand != null
                && hand.IsTracked
                && (hand.GetFingerIsPinching(OVRHand.HandFinger.Index)
                    || hand.GetFingerPinchStrength(OVRHand.HandFinger.Index) >= PinchOnStrength);
        }

        // ---------------------------------------------------------------- layout

        private void Calibrate()
        {
            Transform head = Head;
            if (head == null)
            {
                calibOrigin = Vector3.zero;
                calibForward = Vector3.forward;
            }
            else
            {
                calibOrigin = head.position;
                Vector3 f = Flatten(head.forward);
                calibForward = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            }
            calibrated = true;
        }

        private void PlaceMarker(float signedYawDeg)
        {
            Vector3 dir = Quaternion.AngleAxis(signedYawDeg, Vector3.up) * calibForward;
            marker.transform.position = calibOrigin + dir * markerDistance + Vector3.up * markerHeightAboveEyes;
        }

        private void PlacePanel(Transform head)
        {
            Vector3 anchorPos;
            Vector3 forward;
            if (calibrated)
            {
                anchorPos = calibOrigin;
                forward = calibForward;
            }
            else
            {
                anchorPos = head.position;
                Vector3 f = Flatten(head.forward);
                forward = f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            }

            Vector3 target = anchorPos + forward * panelDistance - Vector3.up * 0.05f;
            Transform p = panel.transform;
            bool snap = calibrated || !panelPlaced;
            p.position = snap ? target : Vector3.Lerp(p.position, target, 1f - Mathf.Exp(-6f * Time.deltaTime));
            panelPlaced = true;
            FaceAwayFrom(p, head.position, true);

            if (marker.activeSelf)
            {
                Transform l = markerLabel.transform;
                l.position = marker.transform.position + Vector3.up * 0.12f;
                FaceAwayFrom(l, head.position, false);
            }
        }

        /// <summary>TextMeshPro reads correctly when its forward axis points away from the viewer.</summary>
        private static void FaceAwayFrom(Transform t, Vector3 viewer, bool keepUpright)
        {
            Vector3 dir = t.position - viewer;
            if (keepUpright)
            {
                dir = Flatten(dir);
            }
            if (dir.sqrMagnitude > 1e-6f)
            {
                t.rotation = Quaternion.LookRotation(dir.normalized, Vector3.up);
            }
        }

        private void UpdateMarkerAim()
        {
            if (!marker.activeSelf || lookSource == null)
            {
                return;
            }
            bool aimed = lookSource.AngleTo(marker.transform.position) <= aimToleranceDegrees;
            markerRenderer.material.color = aimed ? new Color(0.3f, 0.9f, 0.4f) : new Color(1f, 0.55f, 0.1f);
        }

        // ---------------------------------------------------------------- setup

        private void ResolveReferences()
        {
            if (rig == null)
            {
                rig = FindFirstObjectByType<OVRCameraRig>();
            }
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }
            if (lookSource == null)
            {
                lookSource = gameObject.AddComponent<LookSource>();
            }

            if (leftHand == null || rightHand == null)
            {
                OVRHand[] hands = FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                foreach (OVRHand h in hands)
                {
                    int side = GuessSide(h);
                    if (side == 0 && leftHand == null)
                    {
                        leftHand = h;
                    }
                    else if (side == 1 && rightHand == null)
                    {
                        rightHand = h;
                    }
                }
            }

            if (rig == null)
            {
                Debug.LogError("[Misdirection] Tracking spike needs an OVRCameraRig in the scene.");
            }
            if (leftHand == null || rightHand == null)
            {
                Debug.LogError("[Misdirection] Tracking spike could not find both OVRHand components. " +
                               "Add the Hand Tracking building block or assign them in the Inspector.");
            }
        }

        /// <summary>0 = left, 1 = right, -1 = unknown. Uses the rig hierarchy, then object names.</summary>
        private int GuessSide(OVRHand hand)
        {
            if (rig != null)
            {
                if (rig.leftHandAnchor != null && hand.transform.IsChildOf(rig.leftHandAnchor))
                {
                    return 0;
                }
                if (rig.rightHandAnchor != null && hand.transform.IsChildOf(rig.rightHandAnchor))
                {
                    return 1;
                }
            }

            for (Transform t = hand.transform; t != null; t = t.parent)
            {
                string n = t.name.ToLowerInvariant();
                if (n.Contains("left"))
                {
                    return 0;
                }
                if (n.Contains("right"))
                {
                    return 1;
                }
            }
            return -1;
        }

        private void BuildVisuals()
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Spike Marker";
            marker.transform.SetParent(transform, false);
            marker.transform.localScale = Vector3.one * markerDiameter;
            Collider markerCollider = marker.GetComponent<Collider>();
            if (markerCollider != null)
            {
                Destroy(markerCollider);
            }
            markerRenderer = marker.GetComponent<Renderer>();
            marker.SetActive(false);

            markerLabel = CreateText("Spike Marker Label", new Vector2(0.45f, 0.12f), 0.35f);
            markerLabel.transform.SetParent(marker.transform, true);

            panel = CreateText("Spike Panel", new Vector2(0.9f, 0.55f), 0.6f);

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            startTone = Tone("start", 880f, 0.12f);
            endTone = Tone("end", 660f, 0.12f);
            doneTone = Tone("done", 523.25f, 0.45f);
        }

        private TextMeshPro CreateText(string objectName, Vector2 size, float maxFontSize)
        {
            GameObject go = new GameObject(objectName, typeof(RectTransform));
            go.transform.SetParent(transform, false);
            TextMeshPro tmp = go.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = size;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.05f;
            tmp.fontSizeMax = maxFontSize;
            tmp.color = Color.white;
            tmp.richText = true;
            return tmp;
        }

        private void SetPanel(string text)
        {
            if (panel != null)
            {
                panel.text = text;
            }
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip, 0.6f);
            }
        }

        private static AudioClip Tone(string clipName, float frequency, float seconds)
        {
            const int rate = 48000;
            int samples = Mathf.CeilToInt(rate * seconds);
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / rate;
                float envelope = Mathf.Clamp01(time / 0.01f) * Mathf.Clamp01((seconds - time) / 0.04f);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * 0.5f;
            }
            AudioClip clip = AudioClip.Create("spike_" + clipName, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ---------------------------------------------------------------- text

        private static string IntroText()
        {
            return "<b>MISDIRECTION · Tracking spike</b>\n" +
                   "Sit at your table, facing where the crowd would stand.\n" +
                   "Controllers off. Rest both hands on the table.\n\n" +
                   "To start: pinch both hands (palms down) and hold for 1 second.";
        }

        private static string SecretHandIntro(HandSide secret, float secondsLeft)
        {
            string s = secret == HandSide.Left ? "LEFT" : "RIGHT";
            string other = secret == HandSide.Left ? "RIGHT" : "LEFT";
            string side = secret == HandSide.Left ? "left" : "right";
            return "<b>Secret hand: " + s + "</b>\n" +
                   "Pinch and HOLD with your " + s + " hand, low, near the table edge closest to you, a little " + side + " of centre. That is the palmed coin.\n" +
                   "Rest your " + other + " hand on the table.\n\n" +
                   "Turn your HEAD to face the ball each time it moves.\nKeep the secret hand where it is.\n\n" +
                   "<size=80%>Starting in " + Mathf.CeilToInt(Mathf.Max(0f, secondsLeft)) + "…</size>";
        }

        private static string Describe(SpikeCondition c)
        {
            string hand = c.SecretHand == HandSide.Left ? "L" : "R";
            if (c.Turn == TurnKind.Center)
            {
                return hand + " secret · centre";
            }
            return hand + " secret · " + Mathf.RoundToInt(c.AngleDeg) + "° " + c.TurnCode;
        }

        private static string ProgressBar(float fraction)
        {
            int filled = Mathf.Clamp(Mathf.RoundToInt(fraction * 10f), 0, 10);
            return "<color=#7CFC00>" + new string('■', filled) + "</color>" + new string('□', 10 - filled);
        }

        private static string StripTags(string text)
        {
            StringBuilder sb = new StringBuilder(text.Length);
            bool inTag = false;
            foreach (char ch in text)
            {
                if (ch == '<')
                {
                    inTag = true;
                }
                else if (ch == '>')
                {
                    inTag = false;
                }
                else if (!inTag)
                {
                    sb.Append(ch);
                }
            }
            return sb.ToString();
        }

        private static string SafeHeadsetName()
        {
            string name = OVRPlugin.GetSystemHeadsetType().ToString();
            StringBuilder sb = new StringBuilder(name.Length);
            foreach (char ch in name)
            {
                sb.Append(char.IsLetterOrDigit(ch) ? ch : '_');
            }
            return sb.ToString();
        }

        private static Vector3 Flatten(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
