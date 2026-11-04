using System;
using Misdirection.Core.Hands;
using Misdirection.Core.Table;
using Misdirection.Hands;
using UnityEngine;

namespace Misdirection.Table
{
    /// <summary>Where the current table frame came from, most trusted first.</summary>
    public enum TableSource
    {
        None,
        /// <summary>The player's palms-flat gesture: an explicit choice, never overridden automatically.</summary>
        Palms,
        /// <summary>The table saved last session (spatial anchor), relocalised.</summary>
        SavedAnchor,
        /// <summary>Picked from the Quest's room scan (MRUK).</summary>
        Room,
    }

    /// <summary>
    /// Owns the player's real table as a <see cref="TableFrame"/>. Three sources feed it: the saved
    /// anchor from last time (<see cref="TableAnchorStore"/>), the room scan (<see cref="RoomTableFinder"/>),
    /// and the design's onboarding gesture, both palms flat on the table with the heels of the hands at
    /// the near edge, which always works and always wins. Also draws the close-up mat and (for testing)
    /// the servante zone.
    /// </summary>
    [DefaultExecutionOrder(-20)]
    public sealed class TableCalibrator : MonoBehaviour
    {
        [SerializeField] private TrackedHand leftHand;
        [SerializeField] private TrackedHand rightHand;
        [SerializeField] private LookSource lookSource;

        [Header("Gesture")]
        [SerializeField] private float holdSeconds = 1.0f;
        [Tooltip("Holding the gesture this long recalibrates an already calibrated table.")]
        [SerializeField] private float recalibrateHoldSeconds = 3.0f;
        [Range(0f, 1f)] [SerializeField] private float minOpenness = 0.75f;
        [Tooltip("Wrists must be at least this far below the eyes (hands on a table, not raised).")]
        [SerializeField] private float minDropBelowEyes = 0.30f;
        [Tooltip("Wrists must stay within this speed (m/s) to count as resting.")]
        [SerializeField] private float maxRestSpeed = 0.05f;

        [Header("Zone and visuals")]
        [SerializeField] private ServanteSettings servante = new ServanteSettings();
        [SerializeField] private bool showServante = true;
        [SerializeField] private Vector2 matSize = new Vector2(0.42f, 0.30f);
        [SerializeField] private float matCenterZ = 0.24f;

        private ServanteZone zone;
        private float heldFor;
        private Vector3 lastLeft;
        private Vector3 lastRight;
        private GameObject mat;
        private LineRenderer outline;

        public bool Calibrated { get; private set; }
        public TableSource Source { get; private set; }
        public TableFrame Frame { get; private set; }
        public ServanteZone Zone { get { return zone; } }

        /// <summary>Set false while a trick is in progress so resting palms can't move the table.</summary>
        public bool AllowRecalibration { get; set; }

        /// <summary>0..1 progress of the current calibration hold (for UI).</summary>
        public float Progress { get; private set; }

        public event Action OnCalibrated;

        public void Configure(TrackedHand left, TrackedHand right, LookSource look)
        {
            leftHand = left;
            rightHand = right;
            lookSource = look;
        }

        /// <summary>Use a known table frame (room scan, saved anchor, tests).</summary>
        public void SetFrame(TableFrame frame, TableSource source = TableSource.Room)
        {
            Frame = frame;
            Calibrated = true;
            Source = source;
            zone.Reset();
            PlaceVisuals();
            if (OnCalibrated != null)
            {
                OnCalibrated();
            }
        }

        private void Awake()
        {
            zone = new ServanteZone(servante);
            AllowRecalibration = true;
        }

        private void Update()
        {
            if (leftHand == null || rightHand == null)
            {
                return;
            }
            if (Calibrated && !AllowRecalibration)
            {
                heldFor = 0f;
                Progress = 0f;
                return;
            }

            Vector3 l = leftHand.StableWrist.position;
            Vector3 r = rightHand.StableWrist.position;
            bool resting = PalmsResting(l, r);
            lastLeft = l;
            lastRight = r;

            heldFor = resting ? heldFor + Time.deltaTime : 0f;
            float needed = Calibrated ? recalibrateHoldSeconds : holdSeconds;
            Progress = Mathf.Clamp01(heldFor / needed);
            if (heldFor >= needed)
            {
                heldFor = float.NegativeInfinity; // require letting go before the next calibration
                Vector3 head = Head();
                SetFrame(TableFrame.FromPalms(l.ToNumerics(), r.ToNumerics(), head.ToNumerics()), TableSource.Palms);
                Debug.Log("[Misdirection] Table calibrated by palms at " + Frame.Origin.ToUnity() + ", facing " + Frame.Forward.ToUnity());
            }
            else if (!resting && float.IsNegativeInfinity(heldFor))
            {
                heldFor = 0f;
            }
        }

        private bool PalmsResting(Vector3 l, Vector3 r)
        {
            HandPresence lp = leftHand.Presence;
            HandPresence rp = rightHand.Presence;
            if (lp == null || rp == null || !lp.ReliableForDecisions || !rp.ReliableForDecisions)
            {
                return false;
            }
            float lo = leftHand.Shape.Openness;
            float ro = rightHand.Shape.Openness;
            if (float.IsNaN(lo) || float.IsNaN(ro) || lo < minOpenness || ro < minOpenness)
            {
                return false;
            }
            float eyes = Head().y;
            if (eyes - l.y < minDropBelowEyes || eyes - r.y < minDropBelowEyes || Mathf.Abs(l.y - r.y) > 0.06f)
            {
                return false;
            }
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            return Vector3.Distance(l, lastLeft) / dt <= maxRestSpeed && Vector3.Distance(r, lastRight) / dt <= maxRestSpeed;
        }

        private Vector3 Head()
        {
            if (lookSource != null && lookSource.Head != null)
            {
                return lookSource.Head.position;
            }
            Camera cam = Camera.main;
            return cam != null ? cam.transform.position : Vector3.up * 1.2f;
        }

        private void PlaceVisuals()
        {
            Quaternion rot = Quaternion.LookRotation(Frame.Forward.ToUnity(), Vector3.up);
            if (mat == null)
            {
                mat = GameObject.CreatePrimitive(PrimitiveType.Cube);
                mat.name = "Close-up Mat";
                mat.transform.SetParent(transform, false);
                Collider c = mat.GetComponent<Collider>();
                if (c != null)
                {
                    Destroy(c);
                }
                mat.GetComponent<Renderer>().material.color = new Color(0.08f, 0.22f, 0.15f);
            }
            mat.transform.SetPositionAndRotation(Frame.ToWorld(new System.Numerics.Vector3(0f, 0.001f, matCenterZ)).ToUnity(), rot);
            mat.transform.localScale = new Vector3(matSize.x, 0.002f, matSize.y);

            if (!showServante)
            {
                return;
            }
            if (outline == null)
            {
                GameObject go = new GameObject("Servante Outline");
                go.transform.SetParent(transform, false);
                outline = go.AddComponent<LineRenderer>();
                outline.useWorldSpace = true;
                outline.startWidth = outline.endWidth = 0.003f;
                outline.material = new Material(Shader.Find("Sprites/Default"));
                outline.startColor = outline.endColor = new Color(0.6f, 0.4f, 1f, 0.8f);
            }
            System.Numerics.Vector3 c0 = zone.Center;
            System.Numerics.Vector3 h = zone.Size * 0.5f;
            // One continuous path that covers all 12 box edges.
            int[,] path = { { -1, -1, -1 }, { 1, -1, -1 }, { 1, -1, 1 }, { -1, -1, 1 }, { -1, -1, -1 }, { -1, 1, -1 }, { 1, 1, -1 },
                            { 1, -1, -1 }, { 1, 1, -1 }, { 1, 1, 1 }, { 1, -1, 1 }, { 1, 1, 1 }, { -1, 1, 1 }, { -1, -1, 1 },
                            { -1, 1, 1 }, { -1, 1, -1 } };
            Vector3[] points = new Vector3[path.GetLength(0)];
            for (int i = 0; i < points.Length; i++)
            {
                System.Numerics.Vector3 local = c0 + new System.Numerics.Vector3(path[i, 0] * h.X, path[i, 1] * h.Y, path[i, 2] * h.Z);
                points[i] = Frame.ToWorld(local).ToUnity();
            }
            outline.positionCount = points.Length;
            outline.SetPositions(points);
        }
    }
}
