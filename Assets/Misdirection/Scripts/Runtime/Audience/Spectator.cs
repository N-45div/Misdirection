using Misdirection.Core.Crowd;
using Misdirection.Core.Tricks;
using TMPro;
using UnityEngine;

namespace Misdirection.Audience
{
    /// <summary>
    /// A spectator: head and eyes that visibly follow the attention model, a readable face and body
    /// (<see cref="SpectatorFigure"/>: brows, mouth, lean, arms, accessory), their own 3-D sound
    /// (<see cref="SpectatorVoice"/>), a shape-coded suspicion marker (? / ?! / !!) and an optional
    /// "Magician's Eye" cone.
    /// </summary>
    public sealed class Spectator : MonoBehaviour
    {
        [SerializeField] private float headTurnRate = 7f;
        [SerializeField] private float maxHeadYaw = 75f;
        [SerializeField] private float maxHeadPitch = 45f;

        private SpectatorMind mind;
        private Transform head;
        private Transform leftPupil;
        private Transform rightPupil;
        private Transform leftEye;
        private Transform rightEye;
        private TextMeshPro marker;
        private AttentionCone cone;
        private SpectatorFigure figure;
        private SpectatorVoice voice;
        private Vector3 bodyForward = Vector3.forward;
        private Reaction reaction = Reaction.None;
        private double reactionStart;
        private bool reactionMine;

        public SpectatorMind Mind { get { return mind; } }

        /// <summary>Midpoint between the eyes (the face the magician looks at).</summary>
        public Vector3 EyesPosition { get { return head != null ? head.position + head.forward * 0.09f : transform.position; } }

        public AttentionCone Cone { get { return cone; } }

        /// <summary>Just behind the right ear: where a coin gets "found".</summary>
        public Vector3 EarPosition { get { return head != null ? head.position + head.right * 0.12f - head.forward * 0.02f : transform.position; } }

        /// <summary>World position just above the head (for floating reactions).</summary>
        public Vector3 AboveHead { get { return head != null ? head.position + Vector3.up * 0.32f : transform.position; } }

        /// <summary>Builds the greybox body standing at the transform's position, facing <paramref name="faceToward"/>.</summary>
        public void Build(SpectatorMind spectatorMind, Vector3 faceToward, float eyeHeightAboveFeet, bool showCone)
        {
            mind = spectatorMind;
            Vector3 flat = faceToward - transform.position;
            flat.y = 0f;
            bodyForward = flat.sqrMagnitude > 1e-4f ? flat.normalized : Vector3.forward;
            transform.rotation = Quaternion.LookRotation(bodyForward, Vector3.up);

            Color body = ColorFor(mind.Profile.Archetype);
            float headY = eyeHeightAboveFeet + 0.02f;
            float bodyHeight = Mathf.Max(0.6f, headY - 0.18f);

            Transform torsoCapsule = Part(PrimitiveType.Capsule, "Body", transform, new Vector3(0f, bodyHeight * 0.5f, 0f),
                 new Vector3(0.36f, bodyHeight * 0.5f, 0.24f), body).transform;

            Color skin = Color.Lerp(body, new Color(0.95f, 0.85f, 0.75f), 0.7f);
            GameObject headGo = Part(PrimitiveType.Sphere, "Head", transform, new Vector3(0f, headY, 0f),
                                     Vector3.one * 0.21f, skin);
            head = headGo.transform;
            leftEye = Part(PrimitiveType.Sphere, "Eye L", head, new Vector3(-0.17f, 0.08f, 0.42f), Vector3.one * 0.2f, Color.white).transform;
            rightEye = Part(PrimitiveType.Sphere, "Eye R", head, new Vector3(0.17f, 0.08f, 0.42f), Vector3.one * 0.2f, Color.white).transform;
            leftPupil = Part(PrimitiveType.Sphere, "Pupil L", head, leftEye.localPosition, Vector3.one * 0.1f, Color.black).transform;
            rightPupil = Part(PrimitiveType.Sphere, "Pupil R", head, rightEye.localPosition, Vector3.one * 0.1f, Color.black).transform;

            figure = gameObject.AddComponent<SpectatorFigure>();
            figure.Build(mind.Profile.Archetype, torsoCapsule, head, bodyHeight, body, skin);
            voice = headGo.AddComponent<SpectatorVoice>();
            voice.Build(mind.Profile.Archetype);

            marker = WorldText.Create(transform, "Suspicion", new Vector2(0.3f, 0.15f), 0.6f);
            marker.transform.localPosition = new Vector3(0f, headY + 0.24f, 0f);

            GameObject coneGo = new GameObject("Magician's Eye Cone");
            coneGo.transform.SetParent(transform, false);
            cone = coneGo.AddComponent<AttentionCone>();
            cone.Visible = showCone;
        }

        /// <summary>React to a reveal or bust; <paramref name="mine"/> = this spectator saw the method (heckler).</summary>
        public void React(Reaction r, bool mine)
        {
            reaction = r;
            reactionStart = Time.timeAsDouble;
            reactionMine = mine;
            if (voice != null)
            {
                voice.React(r, mine);
            }
        }

        public void Tick(Vector3 viewer, Vector3 secretPoint, bool hasSecret, Vector3 hands)
        {
            if (mind == null)
            {
                return;
            }
            Expression expression = ExpressionModel.Evaluate(mind.Profile.Archetype, mind.Level, mind.EyeContact, mind.LookingAway,
                reaction, Time.timeAsDouble - reactionStart, reactionMine, Time.time);
            figure.Apply(expression, hands);
            voice.Tick(mind.Level, mind.EyeContact);
            mind.Eyes = EyesPosition.ToNumerics();
            Vector3 target = mind.AttentionPoint.ToUnity();
            AimHead(target);
            AimPupil(leftEye, leftPupil, target);
            AimPupil(rightEye, rightPupil, target);
            UpdateMarker(viewer);

            float covered = hasSecret ? mind.Coverage(secretPoint.ToNumerics()) : 0f;
            cone.Show(EyesPosition, mind.ConeTarget.ToUnity(), mind.Profile.ConeHalfAngle, covered, mind.EyeContact);
        }

        private void AimHead(Vector3 target)
        {
            Vector3 to = target - head.position;
            if (to.sqrMagnitude < 1e-6f)
            {
                return;
            }
            Quaternion desired = Quaternion.LookRotation(to.normalized, Vector3.up);
            Vector3 local = (Quaternion.Inverse(transform.rotation) * desired).eulerAngles;
            float yaw = Mathf.Clamp(Mathf.DeltaAngle(0f, local.y), -maxHeadYaw, maxHeadYaw);
            float pitch = Mathf.Clamp(Mathf.DeltaAngle(0f, local.x), -maxHeadPitch, maxHeadPitch);
            Quaternion clamped = transform.rotation * Quaternion.Euler(pitch, yaw, 0f);
            head.rotation = Quaternion.Slerp(head.rotation, clamped, 1f - Mathf.Exp(-headTurnRate * Time.deltaTime));
        }

        /// <summary>Eyes lead the head: the pupil slides toward the target inside the eye.</summary>
        private static void AimPupil(Transform eye, Transform pupil, Vector3 target)
        {
            Vector3 dir = (target - eye.position).normalized;
            float radius = eye.lossyScale.x * 0.5f;
            pupil.position = eye.position + dir * radius * 0.75f;
        }

        private void UpdateMarker(Vector3 viewer)
        {
            string text;
            switch (mind.Level)
            {
                case SuspicionLevel.Curious: text = "<color=#FFD54F>?</color>"; break;
                case SuspicionLevel.Suspicious: text = "<color=#FF9800>?!</color>"; break;
                case SuspicionLevel.WalkingOff: text = "<color=#F44336>!!</color>"; break;
                default: text = mind.EyeContact ? "<color=#64B5F6>◉</color>" : ""; break;
            }
            if (marker.text != text)
            {
                marker.text = text;
            }
            WorldText.Face(marker.transform, viewer);
        }

        private static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = localScale;
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Destroy(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            r.material.color = color;
            // No shadows on spectators: cheaper on Quest, and no shadow acne on the curved primitives.
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }

        public static Color ColorFor(Archetype archetype)
        {
            switch (archetype)
            {
                case Archetype.Tourist: return new Color(0.25f, 0.55f, 0.95f);
                case Archetype.Kid: return new Color(0.35f, 0.85f, 0.4f);
                case Archetype.Skeptic: return new Color(0.6f, 0.35f, 0.85f);
                case Archetype.Filmer: return new Color(0.55f, 0.55f, 0.6f);
                default: return new Color(0.7f, 0.45f, 0.25f);
            }
        }
    }

    /// <summary>
    /// The "Magician's Eye": a translucent cone from a spectator's eyes along their attention.
    /// Shown in the tutorial and debug builds; turns red when the secret hand is inside it.
    /// </summary>
    public sealed class AttentionCone : MonoBehaviour
    {
        private const int Segments = 24;
        private static Mesh unitCone;

        private MeshRenderer meshRenderer;
        private Material material;

        public bool Visible { get; set; }

        private void Awake()
        {
            MeshFilter filter = gameObject.AddComponent<MeshFilter>();
            filter.sharedMesh = UnitCone();
            meshRenderer = gameObject.AddComponent<MeshRenderer>();
            material = new Material(Shader.Find("Sprites/Default"));
            meshRenderer.sharedMaterial = material;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;
        }

        public void Show(Vector3 apex, Vector3 target, float halfAngleDegrees, float secretCoverage, bool eyeContact)
        {
            meshRenderer.enabled = Visible;
            if (!Visible)
            {
                return;
            }
            Vector3 axis = target - apex;
            float length = Mathf.Max(0.3f, axis.magnitude + 0.15f);
            if (axis.sqrMagnitude < 1e-6f)
            {
                return;
            }
            float radius = Mathf.Tan(halfAngleDegrees * Mathf.Deg2Rad) * length;
            transform.SetPositionAndRotation(apex, Quaternion.LookRotation(axis.normalized, Vector3.up));
            transform.localScale = new Vector3(radius, radius, length);
            Color calm = eyeContact ? new Color(0.4f, 0.7f, 1f, 0.10f) : new Color(1f, 1f, 1f, 0.08f);
            material.color = Color.Lerp(calm, new Color(1f, 0.15f, 0.1f, 0.28f), secretCoverage);
        }

        /// <summary>Apex at the origin, base circle of radius 1 at z = 1. Sides only.</summary>
        private static Mesh UnitCone()
        {
            if (unitCone != null)
            {
                return unitCone;
            }
            Vector3[] vertices = new Vector3[Segments + 1];
            Color[] colors = new Color[Segments + 1];
            int[] triangles = new int[Segments * 3];
            vertices[0] = Vector3.zero;
            colors[0] = Color.white;
            for (int i = 0; i < Segments; i++)
            {
                float a = i * Mathf.PI * 2f / Segments;
                vertices[i + 1] = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 1f);
                colors[i + 1] = new Color(1f, 1f, 1f, 0.35f);
                triangles[i * 3] = 0;
                triangles[i * 3 + 1] = 1 + (i + 1) % Segments;
                triangles[i * 3 + 2] = 1 + i;
            }
            unitCone = new Mesh { name = "Unit Cone", vertices = vertices, colors = colors, triangles = triangles };
            unitCone.RecalculateNormals();
            unitCone.RecalculateBounds();
            return unitCone;
        }
    }
}
