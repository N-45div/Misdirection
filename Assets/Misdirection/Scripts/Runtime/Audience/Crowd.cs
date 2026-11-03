using System.Collections.Generic;
using System.Text;
using Misdirection.Core.Crowd;
using Misdirection.Core.Hands;
using Misdirection.Hands;
using Misdirection.Table;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection.Audience
{
    /// <summary>
    /// The audience across the table. Places spectators once the table is known, feeds the
    /// attention model every frame (where the magician looks, the hands, flourishes, sounds), and
    /// gives tricks one API for secret moves: <see cref="ReportMethod"/> and <see cref="ExposeSecret"/>.
    /// </summary>
    [DefaultExecutionOrder(5)]
    public sealed class Crowd : MonoBehaviour
    {
        [SerializeField] private TableCalibrator table;
        [SerializeField] private LookSource lookSource;
        [SerializeField] private TrackedHand leftHand;
        [SerializeField] private TrackedHand rightHand;

        [Header("Who and where")]
        [SerializeField] private Archetype[] archetypes = { Archetype.Tourist, Archetype.Kid, Archetype.Skeptic };
        [Tooltip("Spectators spread across this many degrees either side of straight ahead. Set it from the Day 1 tracking spike.")]
        [SerializeField] private float arcHalfDegrees = 30f;
        [Tooltip("Horizontal distance from the player's eyes to the spectators (metres).")]
        [SerializeField] private float distance = 1.55f;
        [Tooltip("Table surface height above the floor, used to stand spectators on the floor (metres).")]
        [SerializeField] private float tableHeightAboveFloor = 0.74f;

        [Header("Attention")]
        [SerializeField] private CrowdSettings crowdSettings = new CrowdSettings();
        [SerializeField] private AttentionSettings attentionSettings = new AttentionSettings();
        [SerializeField] private FlourishSettings flourishSettings = new FlourishSettings();
        [Tooltip("Suspicion per second while the secret hand carries something inside a cone.")]
        [SerializeField] private float exposureRate = 0.15f;

        [Header("Debug and tutorial")]
        [SerializeField] private bool showCones = true;
        [Tooltip("A small dot where the magician's look lands.")]
        [SerializeField] private bool showGazeDot = true;

        private readonly List<Spectator> spectators = new List<Spectator>();
        private CrowdModel model;
        private FlourishDetector leftFlourish;
        private FlourishDetector rightFlourish;
        private Vector3 secretPoint;
        private bool hasSecret;
        private Transform gazeDot;

        public CrowdModel Model { get { return model; } }
        public IReadOnlyList<Spectator> Spectators { get { return spectators; } }
        public bool ShowCones
        {
            get { return showCones; }
            set
            {
                showCones = value;
                foreach (Spectator s in spectators)
                {
                    s.Cone.Visible = value;
                }
            }
        }

        public void Configure(TableCalibrator tableCalibrator, LookSource look, TrackedHand left, TrackedHand right)
        {
            table = tableCalibrator;
            lookSource = look;
            leftHand = left;
            rightHand = right;
        }

        /// <summary>A secret move (palm, ditch, load) happened at <paramref name="point"/>. Adds suspicion to whoever saw it.</summary>
        public WitnessReport ReportMethod(Vector3 point, float severity = 1f)
        {
            return model.Witness(point.ToNumerics(), severity);
        }

        /// <summary>Call every frame while the secret hand carries something: exposure inside a cone builds suspicion.</summary>
        public void ExposeSecret(Vector3 point)
        {
            secretPoint = point;
            hasSecret = true;
            model.Expose(point.ToNumerics(), Time.deltaTime, exposureRate);
        }

        /// <summary>Nothing secret is in play this frame (clears the red cone tint).</summary>
        public void ClearSecret()
        {
            hasSecret = false;
        }

        /// <summary>A sound at <paramref name="point"/> draws eyes (a coin clink, "Look!").</summary>
        public void MakeSound(Vector3 point, float strength = 1f)
        {
            model.Sound(Time.timeAsDouble, point.ToNumerics(), strength);
        }

        public string NameOf(int index)
        {
            return index >= 0 && index < spectators.Count ? spectators[index].Mind.Profile.Archetype.ToString() : "nobody";
        }

        /// <summary>One line per spectator for HUDs: focus and suspicion.</summary>
        public string Summary()
        {
            StringBuilder sb = new StringBuilder();
            foreach (Spectator s in spectators)
            {
                SpectatorMind m = s.Mind;
                if (sb.Length > 0)
                {
                    sb.Append("   ");
                }
                sb.Append(m.Profile.Archetype).Append(": ").Append(FocusWord(m)).Append(' ')
                  .Append(Mathf.RoundToInt(m.Suspicion * 100f)).Append('%');
            }
            return sb.ToString();
        }

        private void Awake()
        {
            model = new CrowdModel(crowdSettings, attentionSettings);
            leftFlourish = new FlourishDetector(flourishSettings);
            rightFlourish = new FlourishDetector(flourishSettings);
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }
            if (showGazeDot)
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = "Gaze Dot";
                dot.transform.SetParent(transform, false);
                dot.transform.localScale = Vector3.one * 0.015f;
                Collider c = dot.GetComponent<Collider>();
                if (c != null)
                {
                    Destroy(c);
                }
                dot.GetComponent<Renderer>().material.color = new Color(0.4f, 0.8f, 1f);
                gazeDot = dot.transform;
                dot.SetActive(false);
            }
        }

        private void OnEnable()
        {
            if (table != null)
            {
                table.OnCalibrated += Spawn;
            }
        }

        private void OnDisable()
        {
            if (table != null)
            {
                table.OnCalibrated -= Spawn;
            }
        }

        /// <summary>(Re)places the audience across the table, facing the mat.</summary>
        public void Spawn()
        {
            foreach (Spectator s in spectators)
            {
                if (s != null)
                {
                    Destroy(s.gameObject);
                }
            }
            spectators.Clear();
            model.Clear();
            if (table == null || !table.Calibrated || archetypes == null || archetypes.Length == 0)
            {
                return;
            }

            Vector3 eyes = lookSource != null && lookSource.Head != null ? lookSource.Head.position : table.Frame.Origin.ToUnity() + Vector3.up * 0.45f;
            Vector3 forward = table.Frame.Forward.ToUnity();
            Vector3 mat = Stage();
            float floor = table.Frame.Origin.Y - tableHeightAboveFloor;
            for (int i = 0; i < archetypes.Length; i++)
            {
                float t = archetypes.Length == 1 ? 0.5f : (float)i / (archetypes.Length - 1);
                float azimuth = Mathf.Lerp(-arcHalfDegrees, arcHalfDegrees, t);
                Vector3 dir = Quaternion.AngleAxis(azimuth, Vector3.up) * forward;
                Vector3 feet = new Vector3(eyes.x, floor, eyes.z) + dir * distance;

                SpectatorProfile profile = SpectatorProfile.For(archetypes[i]);
                GameObject go = new GameObject(archetypes[i] + " Spectator");
                go.transform.SetParent(transform, false);
                go.transform.position = feet;
                Spectator spectator = go.AddComponent<Spectator>();
                SpectatorMind mind = model.Add(profile, (feet + Vector3.up * profile.EyeHeight).ToNumerics());
                spectator.Build(mind, mat, profile.EyeHeight, showCones);
                spectators.Add(spectator);
            }
            Debug.Log("[Misdirection] Crowd placed: " + archetypes.Length + " spectators across ±" + arcHalfDegrees + "°.");
        }

        private void LateUpdate()
        {
            if (spectators.Count == 0 || lookSource == null || table == null || !table.Calibrated)
            {
                if (gazeDot != null)
                {
                    gazeDot.gameObject.SetActive(false);
                }
                return;
            }
            double now = Time.timeAsDouble;
            float dt = Time.deltaTime;
            Vector3 eyes = lookSource.Origin;
            float tableY = table.Frame.Origin.Y;

            Flourish(leftHand, leftFlourish, now, eyes, tableY);
            Flourish(rightHand, rightFlourish, now, eyes, tableY);

            model.Update(now, dt, eyes.ToNumerics(), lookSource.Direction.ToNumerics(), HandsCenter().ToNumerics(), Stage().ToNumerics(), tableY);

            foreach (Spectator s in spectators)
            {
                s.Tick(eyes, secretPoint, hasSecret);
            }
            if (gazeDot != null)
            {
                gazeDot.gameObject.SetActive(true);
                gazeDot.position = model.GazePoint.ToUnity();
            }
        }

        private void Flourish(TrackedHand hand, FlourishDetector detector, double now, Vector3 eyes, float tableY)
        {
            if (hand == null || hand.Presence == null)
            {
                return;
            }
            Vector3 palm = hand.StablePalm.position;
            if (detector.Update(now, hand.Presence.ReliableForDecisions, palm.ToNumerics(), hand.PalmNormal.ToNumerics(),
                                hand.Shape.Openness, eyes.ToNumerics(), tableY))
            {
                model.Flourish(now, palm.ToNumerics());
            }
        }

        private Vector3 HandsCenter()
        {
            Vector3 sum = Vector3.zero;
            int n = 0;
            foreach (TrackedHand hand in new[] { leftHand, rightHand })
            {
                if (hand != null && hand.Presence != null && hand.Presence.HasPose)
                {
                    sum += hand.StablePalm.position;
                    n++;
                }
            }
            return n > 0 ? sum / n : Stage();
        }

        private Vector3 Stage()
        {
            return table.Frame.ToWorld(new NVector3(0f, 0f, 0.24f)).ToUnity();
        }

        private static string FocusWord(SpectatorMind m)
        {
            switch (m.Focus)
            {
                case AttentionFocus.EyeContact: return "eye contact";
                case AttentionFocus.LookingAway: return "looking away";
                case AttentionFocus.Gaze: return "your look";
                case AttentionFocus.Flourish: return "flourish";
                case AttentionFocus.Sound: return "sound";
                default: return "hands";
            }
        }
    }
}
