using Misdirection.Table;
using TMPro;
using UnityEngine;

namespace Misdirection.Show
{
    /// <summary>What the mentor's ghost hands are demonstrating.</summary>
    public enum MentorDemo
    {
        None,
        /// <summary>Both hands lower and rest palms-down on the table (calibration).</summary>
        PalmsOnTable,
        /// <summary>One hand reaches for the coin and pinches it.</summary>
        PickUp,
        /// <summary>The pinched coin rises to show it.</summary>
        ShowCoin,
        /// <summary>No hand: a pulsing ring around a spectator's head ("look at her").</summary>
        LookAt,
        /// <summary>The coin hand drifts back to the near edge and opens just past it.</summary>
        Ditch,
        /// <summary>An open hand turns palm-out toward the crowd.</summary>
        ShowEmpty,
        /// <summary>Both hands held up in view: "show me your hands" (paused).</summary>
        HandsBack,
    }

    /// <summary>
    /// Translucent ghost hands that show the player what to do, so the first five minutes need no
    /// text (docs/DESIGN.md). Built from primitives; animated in unscaled time so they keep moving
    /// while the show is paused. An optional short caption sits under the hands for accessibility.
    /// </summary>
    [DefaultExecutionOrder(40)]
    public sealed class MentorHands : MonoBehaviour
    {
        [SerializeField] private TableCalibrator table;
        [SerializeField] private Transform head;
        [SerializeField] private Color ghost = new Color(0.55f, 0.9f, 1f, 0.38f);
        [Tooltip("Short captions under the ghost hands (accessibility; the hands alone should be enough).")]
        [SerializeField] private bool captions = true;
        [SerializeField] private float loopSeconds = 2.4f;

        private GhostHand right;
        private GhostHand left;
        private TextMeshPro caption;
        private LineRenderer ring;
        private AudioSource audioSource;
        private AudioClip cue;
        private MentorDemo demo = MentorDemo.None;
        private double demoStart;
        private Vector3 coinWorld;
        private bool hasCoin;
        private Vector3 lookWorld;
        private bool hasLook;

        public MentorDemo Demo { get { return demo; } }

        public bool Captions
        {
            get { return captions; }
            set { captions = value; }
        }

        public void Configure(TableCalibrator tableCalibrator, Transform viewer)
        {
            table = tableCalibrator;
            head = viewer;
        }

        /// <summary>Where the coin is now (the pickup demo reaches for it).</summary>
        public void SetCoin(Vector3 world)
        {
            coinWorld = world;
            hasCoin = true;
        }

        /// <summary>The face to ring for <see cref="MentorDemo.LookAt"/> (call every frame: spectators move).</summary>
        public void SetLookTarget(Vector3 world)
        {
            lookWorld = world;
            hasLook = true;
        }

        /// <summary>Starts a demonstration (no-op if it's already playing).</summary>
        public void Show(MentorDemo next, string captionText)
        {
            if (next != MentorDemo.LookAt)
            {
                hasLook = false;
            }
            if (caption != null)
            {
                string text = captions ? captionText ?? "" : "";
                if (caption.text != text)
                {
                    caption.text = text;
                }
            }
            if (next == demo)
            {
                return;
            }
            demo = next;
            demoStart = Time.unscaledTimeAsDouble;
            if (next != MentorDemo.None && audioSource != null && cue != null)
            {
                audioSource.PlayOneShot(cue);
            }
        }

        public void Hide()
        {
            Show(MentorDemo.None, "");
        }

        private void Awake()
        {
            Material material = new Material(Shader.Find("Sprites/Default"));
            material.color = ghost;
            right = new GhostHand(transform, false, material);
            left = new GhostHand(transform, true, material);
            caption = WorldText.Create(transform, "Mentor Caption", new Vector2(0.5f, 0.08f), 0.35f);
            caption.text = "";

            GameObject ringGo = new GameObject("Look Ring");
            ringGo.transform.SetParent(transform, false);
            ring = ringGo.AddComponent<LineRenderer>();
            ring.useWorldSpace = true;
            ring.loop = true;
            ring.positionCount = 32;
            ring.startWidth = ring.endWidth = 0.008f;
            ring.material = material;
            ring.startColor = ring.endColor = ghost;

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
            cue = Tones.Chirp("mentor cue", 660f, 990f, 0.18f, 0.25f);
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }
            SetVisible(false, false, false);
        }

        private void LateUpdate()
        {
            if (head == null && Camera.main != null)
            {
                head = Camera.main.transform;
            }
            if (demo == MentorDemo.None || head == null)
            {
                SetVisible(false, false, false);
                return;
            }

            Frame f = CurrentFrame();
            float t = (float)(((Time.unscaledTimeAsDouble - demoStart) % loopSeconds) / loopSeconds);
            float pulse = 0.5f + 0.5f * Mathf.Sin((float)Time.unscaledTimeAsDouble * 4f);
            Vector3 captionAt = f.At(0f, 0.05f, 0.10f);

            switch (demo)
            {
                case MentorDemo.PalmsOnTable:
                {
                    // Hover, lower, rest (the mat "unfurls" under real palms), lift away.
                    float h = t < 0.35f ? Mathf.Lerp(0.10f, 0.012f, Smooth(t / 0.35f)) : t < 0.8f ? 0.012f : Mathf.Lerp(0.012f, 0.10f, Smooth((t - 0.8f) / 0.2f));
                    Quaternion down = f.Rotation;
                    right.Place(f.At(0.10f, h, 0.02f), down, HandPose.Flat);
                    left.Place(f.At(-0.10f, h, 0.02f), down, HandPose.Flat);
                    SetVisible(true, true, false);
                    captionAt = f.At(0f, 0.16f, 0.10f);
                    break;
                }
                case MentorDemo.PickUp:
                {
                    Vector3 coin = hasCoin ? coinWorld : f.At(0f, 0f, 0.24f);
                    Vector3 above = coin + Vector3.up * 0.12f - f.Forward * 0.06f;
                    Vector3 at = coin + Vector3.up * 0.02f - f.Forward * 0.07f;
                    float reach = Smooth(Mathf.Clamp01(t / 0.45f));
                    float lift = Smooth(Mathf.Clamp01((t - 0.65f) / 0.3f));
                    Vector3 pos = Vector3.Lerp(Vector3.Lerp(above, at, reach), above, lift);
                    float pinch = Mathf.Clamp01((t - 0.45f) / 0.15f);
                    right.Place(pos, f.Tilted(35f), HandPose.Lerp(HandPose.Open, HandPose.Pinch, pinch));
                    SetVisible(true, false, false);
                    captionAt = coin + Vector3.up * 0.20f;
                    break;
                }
                case MentorDemo.ShowCoin:
                {
                    float rise = Smooth(Mathf.Clamp01(t / 0.5f));
                    Vector3 pos = f.At(0.04f, Mathf.Lerp(0.03f, 0.22f, rise), 0.17f);
                    right.Place(pos, f.Tilted(10f), HandPose.Pinch);
                    SetVisible(true, false, false);
                    captionAt = f.At(0f, 0.30f, 0.20f);
                    break;
                }
                case MentorDemo.LookAt:
                {
                    SetVisible(false, false, hasLook);
                    if (hasLook)
                    {
                        DrawRing(lookWorld, 0.17f + 0.03f * pulse, (head.position - lookWorld).normalized);
                        captionAt = lookWorld + Vector3.up * 0.30f;
                    }
                    break;
                }
                case MentorDemo.Ditch:
                {
                    // From holding height, drift back and down past the near edge, then open.
                    float move = Smooth(Mathf.Clamp01(t / 0.55f));
                    Vector3 from = f.At(0.06f, 0.10f, 0.14f);
                    Vector3 to = f.At(0.06f, -0.04f, -0.05f);
                    float open = Mathf.Clamp01((t - 0.6f) / 0.15f);
                    right.Place(Vector3.Lerp(from, to, move), f.Tilted(Mathf.Lerp(20f, 60f, move)), HandPose.Lerp(HandPose.Pinch, HandPose.Open, open));
                    SetVisible(true, false, false);
                    captionAt = f.At(0f, 0.10f, -0.02f);
                    break;
                }
                case MentorDemo.ShowEmpty:
                {
                    // Rise, turn the palm out toward the crowd, hold.
                    float turn = Smooth(Mathf.Clamp01(t / 0.4f));
                    Vector3 pos = f.At(0.08f, Mathf.Lerp(0.04f, 0.20f, turn), 0.18f);
                    Quaternion palmOut = Quaternion.LookRotation(Vector3.up, -f.Forward);
                    right.Place(pos, Quaternion.Slerp(f.Rotation, palmOut, turn), HandPose.Open);
                    SetVisible(true, false, false);
                    captionAt = f.At(0f, 0.36f, 0.20f);
                    break;
                }
                case MentorDemo.HandsBack:
                {
                    Quaternion up = Quaternion.LookRotation(Vector3.up, -f.Forward);
                    float bob = 0.015f * pulse;
                    right.Place(f.At(0.12f, 0.18f + bob, 0.10f), up, HandPose.Open);
                    left.Place(f.At(-0.12f, 0.18f + bob, 0.10f), up, HandPose.Open);
                    SetVisible(true, true, false);
                    captionAt = f.At(0f, 0.36f, 0.12f);
                    break;
                }
            }

            caption.gameObject.SetActive(captions && caption.text.Length > 0);
            caption.transform.position = captionAt;
            WorldText.Face(caption.transform, head.position);
        }

        private void SetVisible(bool showRight, bool showLeft, bool showRing)
        {
            right.SetActive(showRight);
            left.SetActive(showLeft);
            ring.gameObject.SetActive(showRing);
            if (caption != null && !showRight && !showLeft && !showRing)
            {
                caption.gameObject.SetActive(false);
            }
        }

        private void DrawRing(Vector3 center, float radius, Vector3 normal)
        {
            Vector3 a = Vector3.Cross(normal, Vector3.up);
            if (a.sqrMagnitude < 1e-4f)
            {
                a = Vector3.right;
            }
            a.Normalize();
            Vector3 b = Vector3.Cross(normal, a).normalized;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float ang = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, center + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * radius);
            }
        }

        /// <summary>The table's frame, or before calibration a guess at the table in front of the player.</summary>
        private Frame CurrentFrame()
        {
            if (table != null && table.Calibrated)
            {
                Vector3 o = table.Frame.Origin.ToUnity();
                Vector3 fwd = table.Frame.Forward.ToUnity();
                return new Frame(o, fwd);
            }
            Vector3 flat = head.forward;
            flat.y = 0f;
            flat = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            return new Frame(head.position + flat * 0.30f - Vector3.up * 0.45f, flat);
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        /// <summary>Table-local placement: x right, y up, z away from the player, origin at the near edge.</summary>
        private struct Frame
        {
            public readonly Vector3 Origin;
            public readonly Vector3 Forward;
            public readonly Vector3 Right;

            public Frame(Vector3 origin, Vector3 forward)
            {
                Origin = origin;
                Forward = forward;
                Right = Vector3.Cross(Vector3.up, forward).normalized;
            }

            public Quaternion Rotation { get { return Quaternion.LookRotation(Forward, Vector3.up); } }

            /// <summary>Fingers forward, tipped down by <paramref name="degrees"/> (reaching, holding).</summary>
            public Quaternion Tilted(float degrees)
            {
                return Quaternion.AngleAxis(degrees, Right) * Rotation;
            }

            public Vector3 At(float x, float y, float z)
            {
                return Origin + Right * x + Vector3.up * y + Forward * z;
            }
        }
    }

    /// <summary>Finger curls (degrees) for a ghost hand.</summary>
    public struct HandPose
    {
        public float Index, Middle, Ring, Pinky, Thumb, Spread;

        public static readonly HandPose Flat = new HandPose { Index = 4f, Middle = 4f, Ring = 4f, Pinky = 4f, Thumb = 10f, Spread = 3f };
        public static readonly HandPose Open = new HandPose { Index = 0f, Middle = 0f, Ring = 0f, Pinky = 0f, Thumb = 0f, Spread = 9f };
        public static readonly HandPose Pinch = new HandPose { Index = 50f, Middle = 75f, Ring = 85f, Pinky = 90f, Thumb = 45f, Spread = 0f };

        public static HandPose Lerp(HandPose a, HandPose b, float t)
        {
            return new HandPose
            {
                Index = Mathf.Lerp(a.Index, b.Index, t),
                Middle = Mathf.Lerp(a.Middle, b.Middle, t),
                Ring = Mathf.Lerp(a.Ring, b.Ring, t),
                Pinky = Mathf.Lerp(a.Pinky, b.Pinky, t),
                Thumb = Mathf.Lerp(a.Thumb, b.Thumb, t),
                Spread = Mathf.Lerp(a.Spread, b.Spread, t),
            };
        }
    }

    /// <summary>
    /// One stylised hand from primitives: a palm and five capsule fingers on knuckle pivots. Local
    /// space: wrist at the origin, fingers along +z, back of the hand +y (palm faces -y).
    /// </summary>
    public sealed class GhostHand
    {
        private readonly Transform root;
        private readonly Transform[] fingers = new Transform[4];
        private readonly float[] baseYaw = new float[4];
        private readonly Transform thumb;
        private readonly float side;

        public GhostHand(Transform parent, bool isLeft, Material material)
        {
            side = isLeft ? 1f : -1f; // thumb side along x: -x for a right hand seen palm-down
            root = new GameObject(isLeft ? "Ghost Hand L" : "Ghost Hand R").transform;
            root.SetParent(parent, false);

            Part(PrimitiveType.Cube, root, new Vector3(0f, 0f, 0.045f), new Vector3(0.078f, 0.022f, 0.088f), Quaternion.identity, material);

            float[] xs = { 0.028f, 0.009f, -0.010f, -0.028f };
            float[] lengths = { 0.072f, 0.080f, 0.074f, 0.058f };
            for (int i = 0; i < 4; i++)
            {
                Transform knuckle = new GameObject("Finger " + i).transform;
                knuckle.SetParent(root, false);
                // Index on the thumb side, pinky on the other; spread fans them out from the middle.
                knuckle.localPosition = new Vector3(side * xs[i], 0f, 0.088f);
                Capsule(knuckle, lengths[i], 0.0095f, material);
                fingers[i] = knuckle;
                baseYaw[i] = (i - 1.5f) * -side;
            }
            thumb = new GameObject("Thumb").transform;
            thumb.SetParent(root, false);
            thumb.localPosition = new Vector3(side * 0.036f, -0.006f, 0.025f);
            Capsule(thumb, 0.060f, 0.0105f, material);
        }

        public void SetActive(bool active)
        {
            if (root.gameObject.activeSelf != active)
            {
                root.gameObject.SetActive(active);
            }
        }

        public void Place(Vector3 wrist, Quaternion rotation, HandPose pose)
        {
            root.SetPositionAndRotation(wrist, rotation);
            float[] curls = { pose.Index, pose.Middle, pose.Ring, pose.Pinky };
            for (int i = 0; i < 4; i++)
            {
                fingers[i].localRotation = Quaternion.Euler(curls[i], baseYaw[i] * pose.Spread * 0.6f, 0f);
            }
            // Thumb points forward and out; curling swings it under the index.
            thumb.localRotation = Quaternion.Euler(pose.Thumb * 0.6f, side * (40f - pose.Thumb * 0.6f), 0f);
        }

        private static void Capsule(Transform pivot, float length, float radius, Material material)
        {
            Part(PrimitiveType.Capsule, pivot, new Vector3(0f, 0f, length * 0.5f),
                 new Vector3(radius * 2f, length * 0.5f, radius * 2f), Quaternion.Euler(90f, 0f, 0f), material);
        }

        private static void Part(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 localScale, Quaternion localRot, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Object.Destroy(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
    }
}
