using UnityEngine;

namespace Misdirection.Tricks
{
    /// <summary>
    /// Greybox magician's cup, mouth down on the mat. It can be pinched near the top and lifted;
    /// on release it settles back onto the table where it was lifted. Rules live in Core (CupTrick).
    /// </summary>
    public sealed class CupActor : MonoBehaviour
    {
        [SerializeField] private float diameter = 0.075f;
        [SerializeField] private float height = 0.09f;
        [SerializeField] private Color color = new Color(0.75f, 0.15f, 0.12f);
        [SerializeField] private float settleSeconds = 0.2f;

        private Transform cup;
        private Vector3 restPosition;
        private Quaternion restRotation = Quaternion.identity;
        private float tableY;
        private bool held;
        private Vector3 grabOffset;
        private float settleT = 1f;
        private Vector3 settleFrom;

        /// <summary>Centre of the cup's mouth on the table (where a loaded coin sits).</summary>
        public Vector3 Base { get { return new Vector3(cup.position.x, tableY, cup.position.z); } }

        /// <summary>Top of the cup (where a pinch picks it up).</summary>
        public Vector3 Top { get { return cup.position + Vector3.up * height * 0.5f; } }

        /// <summary>Height of the mouth above the table (0 when resting).</summary>
        public float Lift { get { return Mathf.Max(0f, cup.position.y - height * 0.5f - tableY); } }

        public bool Held { get { return held; } }

        private void Awake()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Cup";
            go.transform.SetParent(transform, false);
            // The cylinder primitive is 2 units tall and 1 unit wide.
            go.transform.localScale = new Vector3(diameter, height * 0.5f, diameter);
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Destroy(c);
            }
            go.GetComponent<Renderer>().material.color = color;
            cup = go.transform;
        }

        /// <summary>Put the cup mouth-down on the table at <paramref name="tablePoint"/>.</summary>
        public void Place(Vector3 tablePoint, Quaternion tableRotation)
        {
            tableY = tablePoint.y;
            restRotation = tableRotation;
            restPosition = tablePoint + Vector3.up * height * 0.5f;
            held = false;
            settleT = 1f;
            cup.gameObject.SetActive(true);
            cup.SetPositionAndRotation(restPosition, restRotation);
        }

        public void Grab(Vector3 pinchPoint)
        {
            held = true;
            settleT = 1f;
            grabOffset = cup.position - pinchPoint;
        }

        /// <summary>Call every frame while held. The cup can't go below the table.</summary>
        public void Follow(Vector3 pinchPoint)
        {
            if (!held)
            {
                return;
            }
            Vector3 target = pinchPoint + grabOffset;
            target.y = Mathf.Max(target.y, tableY + height * 0.5f);
            cup.SetPositionAndRotation(Vector3.Lerp(cup.position, target, 1f - Mathf.Exp(-25f * Time.deltaTime)), restRotation);
        }

        /// <summary>Let go: the cup settles straight down onto the table.</summary>
        public void Release()
        {
            if (!held)
            {
                return;
            }
            held = false;
            settleFrom = cup.position;
            restPosition = new Vector3(cup.position.x, tableY + height * 0.5f, cup.position.z);
            settleT = 0f;
        }

        private void Update()
        {
            if (held || settleT >= 1f || cup == null)
            {
                return;
            }
            settleT = Mathf.Min(1f, settleT + Time.deltaTime / Mathf.Max(0.01f, settleSeconds));
            cup.position = Vector3.Lerp(settleFrom, restPosition, settleT * settleT);
        }
    }
}
