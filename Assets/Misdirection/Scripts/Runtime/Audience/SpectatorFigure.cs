using Misdirection.Core.Crowd;
using UnityEngine;

namespace Misdirection.Audience
{
    /// <summary>
    /// The readable part of a spectator (Day 7): brows, a mouth that smiles, frowns or drops open,
    /// arms that rest, fold, clap, point or go up, a torso that leans in or back, idle breathing and an
    /// archetype accessory. Built from primitives; it eases toward the <see cref="Expression"/> it is given.
    /// </summary>
    public sealed class SpectatorFigure : MonoBehaviour
    {
        private const float ArmLength = 0.52f;
        private const float Ease = 9f;

        private Transform torso;
        private Transform head;
        private Transform browL, browR;
        private Transform mouthL, mouthC, mouthR;
        private Transform shoulderL, shoulderR;
        private Transform phone;
        private Vector3 browBaseL, browBaseR;
        private float shoulderY;
        private float seed;

        private float browRaise, browFurrow, mouthOpen, mouthCurve, lean;

        /// <summary>The leaning upper body; the head and arms hang off it.</summary>
        public Transform Torso { get { return torso; } }

        /// <summary>
        /// Builds the figure under <paramref name="root"/> (the spectator's feet), moving the existing body capsule
        /// and head under a torso pivot so they lean together.
        /// </summary>
        public void Build(Archetype archetype, Transform body, Transform headTransform, float bodyHeight, Color clothes, Color skin)
        {
            seed = Random.value * 10f;
            head = headTransform;
            GameObject pivot = new GameObject("Torso");
            torso = pivot.transform;
            torso.SetParent(transform, false);
            torso.localPosition = new Vector3(0f, bodyHeight * 0.35f, 0f);
            body.SetParent(torso, true);
            head.SetParent(torso, true);

            // Face (head-local units: the head sphere has radius 0.5).
            Color brow = new Color(0.25f, 0.18f, 0.12f);
            browL = Part(PrimitiveType.Cube, "Brow L", head, new Vector3(-0.17f, 0.27f, 0.42f), new Vector3(0.24f, 0.05f, 0.06f), brow);
            browR = Part(PrimitiveType.Cube, "Brow R", head, new Vector3(0.17f, 0.27f, 0.42f), new Vector3(0.24f, 0.05f, 0.06f), brow);
            browBaseL = browL.localPosition;
            browBaseR = browR.localPosition;
            Color lips = new Color(0.55f, 0.2f, 0.2f);
            mouthC = Part(PrimitiveType.Cube, "Mouth", head, new Vector3(0f, -0.2f, 0.45f), new Vector3(0.16f, 0.05f, 0.05f), lips);
            mouthL = Part(PrimitiveType.Cube, "Mouth L", head, new Vector3(-0.12f, -0.2f, 0.43f), new Vector3(0.1f, 0.05f, 0.05f), lips);
            mouthR = Part(PrimitiveType.Cube, "Mouth R", head, new Vector3(0.12f, -0.2f, 0.43f), new Vector3(0.1f, 0.05f, 0.05f), lips);

            // Arms hang from shoulder pivots; the capsule runs down local -Y to a hand.
            shoulderY = body.localPosition.y + body.localScale.y * 0.75f - torso.localPosition.y;
            shoulderL = Arm("Arm L", -0.21f, clothes, skin);
            shoulderR = Arm("Arm R", 0.21f, clothes, skin);

            Accessorize(archetype, clothes);
        }

        public void Apply(Expression e, Vector3 pointAt)
        {
            float k = 1f - Mathf.Exp(-Ease * Time.deltaTime);
            browRaise = Mathf.Lerp(browRaise, e.BrowRaise, k);
            browFurrow = Mathf.Lerp(browFurrow, e.BrowFurrow, k);
            mouthOpen = Mathf.Lerp(mouthOpen, e.MouthOpen, k);
            mouthCurve = Mathf.Lerp(mouthCurve, e.MouthCurve, k);
            lean = Mathf.Lerp(lean, e.Lean, k * 0.6f);

            // Brows up for surprise/curiosity, inner ends down for suspicion.
            browL.localPosition = browBaseL + Vector3.up * browRaise * 0.07f;
            browR.localPosition = browBaseR + Vector3.up * browRaise * 0.07f;
            browL.localRotation = Quaternion.Euler(0f, 0f, -browFurrow * 22f);
            browR.localRotation = Quaternion.Euler(0f, 0f, browFurrow * 22f);

            // Mouth: corners tilt up for a smile, down for a frown; the middle opens into an "O".
            float corner = mouthCurve * 28f;
            mouthL.localRotation = Quaternion.Euler(0f, 0f, -corner);
            mouthR.localRotation = Quaternion.Euler(0f, 0f, corner);
            float cornerLift = mouthCurve * 0.035f;
            mouthL.localPosition = new Vector3(-0.12f, -0.2f + cornerLift, 0.43f);
            mouthR.localPosition = new Vector3(0.12f, -0.2f + cornerLift, 0.43f);
            mouthC.localScale = new Vector3(Mathf.Lerp(0.16f, 0.12f, mouthOpen), Mathf.Lerp(0.05f, 0.2f, mouthOpen), 0.05f);
            mouthC.localPosition = new Vector3(0f, -0.2f - mouthOpen * 0.06f, 0.45f);

            // Lean (pitch about the hips) plus a little breathing and sway.
            float t = Time.time + seed;
            torso.localRotation = Quaternion.Euler(lean * 9f + Mathf.Sin(t * 1.3f) * 0.8f, 0f, Mathf.Sin(t * 0.7f) * 1.2f);
            torso.localScale = new Vector3(1f, 1f + Mathf.Sin(t * 1.6f) * 0.008f, 1f);

            Pose(e, pointAt, k);
        }

        private void Pose(Expression e, Vector3 pointAt, float k)
        {
            Quaternion l, r;
            switch (e.Arms)
            {
                case ArmPose.Folded:
                    l = Quaternion.Euler(-80f, 55f, 0f);
                    r = Quaternion.Euler(-75f, -55f, 0f);
                    break;
                case ArmPose.Phone:
                    l = Quaternion.Euler(0f, 0f, -6f);
                    r = Quaternion.Euler(-95f, -12f, 0f);
                    break;
                case ArmPose.Clap:
                    float swing = 14f + 16f * Mathf.Sin(e.ClapPhase * Mathf.PI * 2f);
                    l = Quaternion.Euler(-70f, swing, 0f);
                    r = Quaternion.Euler(-70f, -swing, 0f);
                    break;
                case ArmPose.HandsUp:
                    l = Quaternion.Euler(-150f, 18f, 0f);
                    r = Quaternion.Euler(-150f, -18f, 0f);
                    break;
                case ArmPose.Point:
                    l = Quaternion.Euler(0f, 0f, -6f);
                    Vector3 dir = pointAt - shoulderR.position;
                    // Arms hang along local -Y, so turn "down" toward the target, in the torso's frame.
                    r = dir.sqrMagnitude > 1e-4f
                        ? Quaternion.Inverse(torso.rotation) * Quaternion.FromToRotation(Vector3.down, dir.normalized)
                        : Quaternion.Euler(-90f, 0f, 0f);
                    break;
                default:
                    l = Quaternion.Euler(0f, 0f, -6f);
                    r = Quaternion.Euler(0f, 0f, 6f);
                    break;
            }
            float armEase = e.Arms == ArmPose.Clap ? 1f - Mathf.Exp(-25f * Time.deltaTime) : k;
            shoulderL.localRotation = Quaternion.Slerp(shoulderL.localRotation, l, armEase);
            shoulderR.localRotation = Quaternion.Slerp(shoulderR.localRotation, r, armEase);
        }

        private Transform Arm(string name, float x, Color clothes, Color skin)
        {
            GameObject pivot = new GameObject(name);
            Transform shoulder = pivot.transform;
            shoulder.SetParent(torso, false);
            shoulder.localPosition = new Vector3(x, shoulderY, 0f);
            // Capsule primitive: 2 units tall; scale.y = half the arm length.
            Part(PrimitiveType.Capsule, "Sleeve", shoulder, new Vector3(0f, -ArmLength * 0.5f, 0f),
                 new Vector3(0.085f, ArmLength * 0.5f, 0.085f), clothes);
            Transform hand = Part(PrimitiveType.Sphere, "Hand", shoulder, new Vector3(0f, -ArmLength, 0f), Vector3.one * 0.08f, skin);
            if (x > 0f)
            {
                phone = hand;
            }
            return shoulder;
        }

        private void Accessorize(Archetype archetype, Color clothes)
        {
            switch (archetype)
            {
                case Archetype.Tourist:
                    // Sun hat.
                    Part(PrimitiveType.Cylinder, "Hat Brim", head, new Vector3(0f, 0.42f, 0f), new Vector3(1.5f, 0.02f, 1.5f), new Color(0.95f, 0.85f, 0.55f));
                    Part(PrimitiveType.Cylinder, "Hat Crown", head, new Vector3(0f, 0.55f, 0f), new Vector3(0.85f, 0.14f, 0.85f), new Color(0.95f, 0.85f, 0.55f));
                    break;
                case Archetype.Kid:
                    // Cap with a peak.
                    Part(PrimitiveType.Sphere, "Cap", head, new Vector3(0f, 0.25f, 0f), new Vector3(1.04f, 0.6f, 1.04f), new Color(0.95f, 0.3f, 0.2f));
                    Part(PrimitiveType.Cube, "Cap Peak", head, new Vector3(0f, 0.28f, 0.5f), new Vector3(0.7f, 0.04f, 0.4f), new Color(0.95f, 0.3f, 0.2f));
                    break;
                case Archetype.Skeptic:
                    // Glasses: two lenses and a bridge.
                    Color frame = new Color(0.08f, 0.08f, 0.1f);
                    Part(PrimitiveType.Cylinder, "Lens L", head, new Vector3(-0.17f, 0.08f, 0.47f), new Vector3(0.3f, 0.01f, 0.3f), frame).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Part(PrimitiveType.Cylinder, "Lens R", head, new Vector3(0.17f, 0.08f, 0.47f), new Vector3(0.3f, 0.01f, 0.3f), frame).localRotation = Quaternion.Euler(90f, 0f, 0f);
                    Part(PrimitiveType.Cube, "Bridge", head, new Vector3(0f, 0.1f, 0.49f), new Vector3(0.08f, 0.03f, 0.03f), frame);
                    break;
                case Archetype.Filmer:
                    if (phone != null)
                    {
                        Part(PrimitiveType.Cube, "Phone", phone, new Vector3(0f, 0.2f, 0.6f), new Vector3(0.9f, 1.8f, 0.12f), new Color(0.1f, 0.1f, 0.12f));
                    }
                    break;
                case Archetype.Regular:
                    // Scarf at the neck.
                    Part(PrimitiveType.Cylinder, "Scarf", head, new Vector3(0f, -0.5f, 0f), new Vector3(0.9f, 0.1f, 0.9f), new Color(0.2f, 0.45f, 0.3f));
                    break;
            }
        }

        private static Transform Part(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale, Color color)
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
            go.GetComponent<Renderer>().material.color = color;
            return go.transform;
        }
    }
}
