using Misdirection.Core.Access;
using Misdirection.Core.Table;
using Misdirection.Hands;
using Misdirection.Table;
using TMPro;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection.Access
{
    /// <summary>
    /// The Access Tray (Day 11): five toggle tokens in a short row along the left edge of the mat, one
    /// per accessibility option, each a different shape with a one- or two-word label. A token is
    /// switched by looking at it for 1.5 s (a ring fills: eyes-only and head-only navigation), or by
    /// a deliberate poke or pinch with either hand. Looking only counts while the coin rests on the
    /// mat or the show is waiting for the player's hands, so a glance can't change the rules
    /// mid-move. A token that is on is raised, full-size and bright; one that is off is low, small
    /// and grey: shape and size, not colour alone. Primitives and shared materials only.
    /// </summary>
    [DefaultExecutionOrder(32)]
    public sealed class AccessTray : MonoBehaviour
    {
        [Header("Where (table frame, metres)")]
        [Tooltip("Sideways position of the row: just outside the mat's left edge.")]
        [SerializeField] private float edgeX = -0.255f;
        [Tooltip("Distance of the nearest token from the near edge (clear of the servante).")]
        [SerializeField] private float firstZ = 0.11f;
        [SerializeField] private float spacing = 0.07f;

        [Header("Looking (eyes-only)")]
        [SerializeField] private float dwellSeconds = 1.5f;
        [Tooltip("A token counts as looked at within this many degrees (Meta's minimum gaze target is 3 degrees).")]
        [SerializeField] private float gazeDegrees = 3.5f;

        [Header("Hands")]
        [Tooltip("A pointing finger must rest on a token this long. A pinch counts four times as fast.")]
        [SerializeField] private float pokeSeconds = 0.5f;
        [SerializeField] private float touchRadius = 0.035f;
        [SerializeField] private float touchHeight = 0.07f;
        [Range(0f, 1f)] [SerializeField] private float pinchStrength = 0.8f;
        [Tooltip("A flat, open hand resting on a token is not a poke: fingers must be curled below this openness, or pinching.")]
        [Range(0f, 1f)] [SerializeField] private float pokeMaxOpenness = 0.6f;

        private static readonly Color OnColor = new Color(1f, 0.85f, 0.2f, 1f);
        private static readonly Color OffColor = new Color(0.5f, 0.53f, 0.6f, 1f);
        private static readonly Color RingColor = new Color(1f, 1f, 1f, 1f);
        private static readonly Color TrackColor = new Color(1f, 1f, 1f, 0.3f);

        private sealed class Token
        {
            public Transform Root;
            public Transform Shape;
            public Renderer[] Body;
            public TextMeshPro Label;
            public int Style = -1;
        }

        private const int RingPoints = 40;

        private AccessRuntime access;
        private readonly Token[] tokens = new Token[AccessOptions.ToggleCount];
        private DwellSelector gaze;
        private DwellSelector leftTouch;
        private DwellSelector rightTouch;
        private LineRenderer ring;
        private LineRenderer track;
        private bool labelsDirty = true;
        private bool lastOneHanded;
        private int gazeTarget = -1;

        private TableFrame guess;
        private bool hasGuess;
        private float guessStaleFor;

        /// <summary>The token being looked at or touched (-1 for none), for tests and the smoke report.</summary>
        public int Hovered { get; private set; }

        /// <summary>0..1 fill of the ring on <see cref="Hovered"/>.</summary>
        public float Fill { get; private set; }

        /// <summary>Where a token sits in the world right now (for captions about the tray).</summary>
        public Vector3 TokenPosition(AccessToggle toggle)
        {
            Token t = tokens[(int)toggle];
            return t != null ? t.Root.position : transform.position;
        }

        private void Awake()
        {
            access = GetComponent<AccessRuntime>();
            gaze = new DwellSelector(dwellSeconds);
            leftTouch = new DwellSelector(pokeSeconds);
            rightTouch = new DwellSelector(pokeSeconds);
            for (int i = 0; i < tokens.Length; i++)
            {
                tokens[i] = Build((AccessToggle)i);
            }
            track = Ring("Dwell Track", TrackColor, 0.0025f, 0);
            track.loop = true;
            ring = Ring("Dwell Ring", RingColor, 0.005f, 1);
            Hovered = -1;
            SetVisible(false);
        }

        private void OnEnable()
        {
            if (access != null)
            {
                access.Changed += OnChanged;
            }
        }

        private void OnDisable()
        {
            if (access != null)
            {
                access.Changed -= OnChanged;
            }
        }

        private void OnChanged(AccessToggle toggle)
        {
            labelsDirty = true;
        }

        private void LateUpdate()
        {
            TableFrame frame;
            if (access == null || !CurrentFrame(out frame))
            {
                SetVisible(false);
                Hovered = -1;
                Fill = 0f;
                return;
            }
            SetVisible(true);
            Vector3 viewer = Viewer();
            Place(frame, viewer);

            double now = Time.unscaledTimeAsDouble;
            AccessOptions options = access.Options;

            // Nothing counts while the system's own menu has the player's attention.
            bool usable = !access.FocusLost;

            // Looking: only while nothing is mid-move.
            bool lookAllowed = usable && (access.WaitingForHands || !access.TrickBusy);
            gazeTarget = lookAllowed ? LookedAt(gazeTarget) : -1;
            int chosen = gaze.Update(now, gazeTarget, lookAllowed);

            // Hands: a deliberate poke or pinch, any time the hand is free.
            double leftRate;
            double rightRate;
            int leftTarget = Touched(access.LeftHand, frame, viewer, out leftRate);
            int rightTarget = Touched(access.RightHand, frame, viewer, out rightRate);
            int byLeft = leftTouch.Update(now, leftTarget, usable, leftRate);
            int byRight = rightTouch.Update(now, rightTarget, usable, rightRate);
            if (chosen < 0)
            {
                chosen = byLeft >= 0 ? byLeft : byRight;
            }
            if (chosen >= 0)
            {
                access.Toggle((AccessToggle)chosen);
                labelsDirty = true;
            }

            // One ring: whichever way a token is being chosen right now.
            DwellSelector showing = gaze.Target >= 0 ? gaze : leftTouch.Target >= 0 ? leftTouch : rightTouch;
            Hovered = showing.Target;
            Fill = showing.Progress;
            DrawRing(viewer);

            if (access.OneHandedActive != lastOneHanded)
            {
                lastOneHanded = access.OneHandedActive;
                labelsDirty = true;
            }
            Restyle(options);
        }

        // ------------------------------------------------------------ choosing

        /// <summary>The token the look ray points at, with a little stickiness so the choice doesn't flicker between neighbours.</summary>
        private int LookedAt(int current)
        {
            LookSource look = access.Look;
            if (look == null || look.Head == null)
            {
                return -1;
            }
            int best = -1;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < tokens.Length; i++)
            {
                Vector3 center = tokens[i].Root.position + Vector3.up * 0.015f;
                Vector3 to = center - look.Origin;
                float distance = to.magnitude;
                if (distance < 0.05f)
                {
                    continue;
                }
                float angle = Vector3.Angle(look.Direction, to);
                // Never smaller than the token itself, however close the player leans in.
                float limit = Mathf.Max(gazeDegrees, Mathf.Atan2(0.03f, distance) * Mathf.Rad2Deg);
                if (i == current)
                {
                    limit *= 1.5f;
                }
                if (angle <= limit && angle < bestAngle)
                {
                    best = i;
                    bestAngle = angle;
                }
            }
            return best;
        }

        /// <summary>
        /// The token under a hand's fingertips, if the hand means it: reliably tracked, not carrying the coin,
        /// pointing or pinching (a flat resting palm is not a poke), and never a pinch with the palm toward the
        /// face, which is the system gesture.
        /// </summary>
        private int Touched(TrackedHand hand, TableFrame frame, Vector3 viewer, out double rate)
        {
            rate = 1.0;
            if (hand == null || hand.Presence == null || !hand.Presence.ReliableForDecisions || hand.Carrying)
            {
                return -1;
            }
            bool pinching = hand.Shape.PinchStrength >= pinchStrength;
            float openness = hand.Shape.Openness;
            bool pointing = !float.IsNaN(openness) && openness <= pokeMaxOpenness;
            if (!pinching && !pointing)
            {
                return -1;
            }
            Vector3 point = hand.PinchPoint;
            Vector3 toFace = viewer - point;
            if (toFace.sqrMagnitude > 1e-6f && Vector3.Dot(hand.PalmNormal, toFace.normalized) > 0.5f)
            {
                return -1;
            }
            NVector3 local = frame.ToLocal(point.ToNumerics());
            if (local.Y < -0.03f || local.Y > touchHeight)
            {
                return -1;
            }
            int best = -1;
            float bestDistance = touchRadius;
            for (int i = 0; i < tokens.Length; i++)
            {
                float dx = local.X - edgeX;
                float dz = local.Z - (firstZ + i * spacing);
                float d = Mathf.Sqrt(dx * dx + dz * dz);
                if (d <= bestDistance)
                {
                    best = i;
                    bestDistance = d;
                }
            }
            if (best >= 0 && pinching)
            {
                rate = 4.0;
            }
            return best;
        }

        // ------------------------------------------------------------ placing and drawing

        /// <summary>
        /// The table's frame. Before the table is known: a guess at the table in front of the player (where the
        /// ghost hands also wait), held still so the tokens don't slide about under a head-gaze dwell.
        /// </summary>
        private bool CurrentFrame(out TableFrame frame)
        {
            TableCalibrator table = access.Table;
            if (table != null && table.Calibrated)
            {
                hasGuess = false;
                frame = table.Frame;
                return true;
            }
            frame = guess;
            LookSource look = access.Look;
            Transform head = look != null && look.Head != null ? look.Head : Camera.main != null ? Camera.main.transform : null;
            if (head == null || Time.unscaledTime < 1.5f)
            {
                // Head tracking needs a moment before its pose means anything.
                return hasGuess;
            }
            Vector3 flat = head.forward;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.04f)
            {
                return hasGuess;
            }
            flat.Normalize();
            if (hasGuess)
            {
                // Re-place only when the player has clearly moved or turned away from it for a while.
                Vector3 toTray = guess.Origin.ToUnity() - head.position;
                toTray.y = 0f;
                bool off = toTray.magnitude > 0.7f || Vector3.Angle(flat, toTray) > 75f;
                guessStaleFor = off ? guessStaleFor + Time.unscaledDeltaTime : 0f;
                if (guessStaleFor < 2f)
                {
                    return true;
                }
            }
            guess = new TableFrame((head.position + flat * 0.30f - Vector3.up * 0.45f).ToNumerics(), flat.ToNumerics());
            hasGuess = true;
            guessStaleFor = 0f;
            frame = guess;
            return true;
        }

        private void Place(TableFrame frame, Vector3 viewer)
        {
            Quaternion rotation = Quaternion.LookRotation(frame.Forward.ToUnity(), Vector3.up);
            float textScale = AccessSettings.TextScale;
            for (int i = 0; i < tokens.Length; i++)
            {
                Token t = tokens[i];
                t.Root.SetPositionAndRotation(frame.ToWorld(new NVector3(edgeX, 0.002f, firstZ + i * spacing)).ToUnity(), rotation);
                // Labels sit just inside the row, over the dark mat, turned to face the player.
                t.Label.transform.position = t.Root.position + t.Root.right * (0.03f + 0.055f * textScale) + Vector3.up * 0.022f;
                t.Label.transform.localScale = Vector3.one * textScale;
                WorldText.Face(t.Label.transform, viewer, false);
            }
        }

        private void Restyle(AccessOptions options)
        {
            int contrast = AccessSettings.HighContrast ? 2 : 0;
            for (int i = 0; i < tokens.Length; i++)
            {
                Token t = tokens[i];
                AccessToggle toggle = (AccessToggle)i;
                bool on = options.IsOn(toggle);
                int style = (on ? 1 : 0) + contrast;
                if (style != t.Style)
                {
                    t.Style = style;
                    Color color = on ? (contrast != 0 ? Color.white : OnColor) : OffColor;
                    Material material = AccessSettings.Shared(color);
                    for (int r = 0; r < t.Body.Length; r++)
                    {
                        t.Body[r].sharedMaterial = material;
                    }
                    // On: raised and full-size. Off: low and small. Readable without colour.
                    t.Shape.localPosition = new Vector3(0f, on ? 0.012f : 0f, 0f);
                    t.Shape.localScale = Vector3.one * (on ? 1f : 0.65f);
                    labelsDirty = true;
                }
            }
            if (!labelsDirty)
            {
                return;
            }
            labelsDirty = false;
            for (int i = 0; i < tokens.Length; i++)
            {
                AccessToggle toggle = (AccessToggle)i;
                string state = options.StateWord(toggle);
                if (toggle == AccessToggle.OneHand && options.OneHand == OneHandMode.Auto && access.OneHandedActive)
                {
                    state = "auto (on)";
                }
                tokens[i].Label.text = "<b>" + AccessOptions.Label(toggle) + "</b>\n<size=80%>" + state + "</size>";
                tokens[i].Label.color = options.IsOn(toggle) ? Color.white : new Color(0.8f, 0.82f, 0.86f);
            }
        }

        /// <summary>A faint full circle round the token being chosen, and a bright arc that fills as the dwell runs.</summary>
        private void DrawRing(Vector3 viewer)
        {
            bool show = Hovered >= 0 && Hovered < tokens.Length;
            if (track.gameObject.activeSelf != show)
            {
                track.gameObject.SetActive(show);
            }
            bool filling = show && Fill > 0.01f;
            if (ring.gameObject.activeSelf != filling)
            {
                ring.gameObject.SetActive(filling);
            }
            if (!show)
            {
                return;
            }
            Vector3 center = tokens[Hovered].Root.position + Vector3.up * 0.022f;
            Vector3 normal = viewer - center;
            if (normal.sqrMagnitude < 1e-6f)
            {
                normal = Vector3.up;
            }
            normal.Normalize();
            Vector3 a = Vector3.Cross(normal, Vector3.up);
            if (a.sqrMagnitude < 1e-4f)
            {
                a = Vector3.right;
            }
            a.Normalize();
            Vector3 b = Vector3.Cross(a, normal).normalized;
            const float radius = 0.032f;

            track.positionCount = RingPoints;
            for (int i = 0; i < RingPoints; i++)
            {
                float angle = i * Mathf.PI * 2f / RingPoints;
                track.SetPosition(i, center + (a * Mathf.Sin(angle) + b * Mathf.Cos(angle)) * radius);
            }
            if (!filling)
            {
                return;
            }
            // Starts at the top and runs clockwise, like a clock hand.
            int points = Mathf.Clamp(Mathf.CeilToInt(Fill * RingPoints) + 1, 2, RingPoints + 1);
            ring.positionCount = points;
            for (int i = 0; i < points; i++)
            {
                float angle = Mathf.Min(Fill, (float)i / RingPoints) * Mathf.PI * 2f;
                ring.SetPosition(i, center + (a * Mathf.Sin(angle) + b * Mathf.Cos(angle)) * radius);
            }
        }

        private Vector3 Viewer()
        {
            LookSource look = access.Look;
            if (look != null && look.Head != null)
            {
                return look.Head.position;
            }
            return Camera.main != null ? Camera.main.transform.position : Vector3.up * 1.2f;
        }

        private void SetVisible(bool visible)
        {
            for (int i = 0; i < tokens.Length; i++)
            {
                if (tokens[i] != null && tokens[i].Root.gameObject.activeSelf != visible)
                {
                    tokens[i].Root.gameObject.SetActive(visible);
                }
            }
            if (!visible)
            {
                if (ring != null)
                {
                    ring.gameObject.SetActive(false);
                }
                if (track != null)
                {
                    track.gameObject.SetActive(false);
                }
            }
        }

        // ------------------------------------------------------------ building

        /// <summary>One shape per option, so the tokens can be told apart without reading or colour.</summary>
        private Token Build(AccessToggle toggle)
        {
            Token t = new Token();
            t.Root = new GameObject(AccessOptions.Label(toggle) + " Token").transform;
            t.Root.SetParent(transform, false);
            t.Shape = new GameObject("Shape").transform;
            t.Shape.SetParent(t.Root, false);

            switch (toggle)
            {
                case AccessToggle.OneHand:
                    // An upright pill: one finger.
                    t.Body = new[] { Part(PrimitiveType.Capsule, t.Shape, new Vector3(0f, 0.02f, 0f), new Vector3(0.018f, 0.02f, 0.018f), Quaternion.identity) };
                    break;
                case AccessToggle.Relaxed:
                    // A ball: soft, no edges.
                    t.Body = new[] { Part(PrimitiveType.Sphere, t.Shape, new Vector3(0f, 0.015f, 0f), Vector3.one * 0.03f, Quaternion.identity) };
                    break;
                case AccessToggle.Captions:
                    // A flat bar: a line of text.
                    t.Body = new[] { Part(PrimitiveType.Cube, t.Shape, new Vector3(0f, 0.006f, 0f), new Vector3(0.042f, 0.012f, 0.02f), Quaternion.identity) };
                    break;
                case AccessToggle.HighContrast:
                {
                    // A disc with a black centre: light and dark.
                    Renderer disc = Part(PrimitiveType.Cylinder, t.Shape, new Vector3(0f, 0.003f, 0f), new Vector3(0.036f, 0.003f, 0.036f), Quaternion.identity);
                    Renderer dot = Part(PrimitiveType.Cylinder, t.Shape, new Vector3(0f, 0.0035f, 0f), new Vector3(0.017f, 0.0035f, 0.017f), Quaternion.identity);
                    dot.sharedMaterial = AccessSettings.Shared(Color.black);
                    dot.sortingOrder = 1;
                    t.Body = new[] { disc };
                    break;
                }
                default:
                    // A cube balanced on its corner: a sharp sound.
                    t.Body = new[] { Part(PrimitiveType.Cube, t.Shape, new Vector3(0f, 0.019f, 0f), Vector3.one * 0.022f, Quaternion.Euler(45f, 0f, 45f)) };
                    break;
            }

            t.Label = WorldText.Create(t.Root, "Label", new Vector2(0.11f, 0.034f), 0.12f);
            t.Label.alignment = TextAlignmentOptions.Left;
            t.Label.text = AccessOptions.Label(toggle);
            return t;
        }

        private static Renderer Part(PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Quaternion localRotation)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Destroy(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = AccessSettings.Shared(OffColor);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return r;
        }

        private LineRenderer Ring(string name, Color color, float width, int order)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);
            LineRenderer line = go.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.loop = false;
            line.startWidth = line.endWidth = width;
            line.sharedMaterial = AccessSettings.Shared(Color.white);
            line.startColor = line.endColor = color;
            line.sortingOrder = order;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            go.SetActive(false);
            return line;
        }
    }
}
