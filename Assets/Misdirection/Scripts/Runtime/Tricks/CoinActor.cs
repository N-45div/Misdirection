using UnityEngine;

namespace Misdirection.Tricks
{
    /// <summary>The coin's visuals. Game rules live in Misdirection.Core.Tricks.CoinModel; this only shows them.</summary>
    public sealed class CoinActor : MonoBehaviour
    {
        [SerializeField] private float diameter = 0.026f;
        [SerializeField] private float thickness = 0.0025f;
        [SerializeField] private Color color = new Color(0.95f, 0.75f, 0.25f);
        [Tooltip("How quickly a held coin follows the palm (1/s).")]
        [SerializeField] private float followRate = 25f;
        [SerializeField] private float fallSeconds = 0.25f;

        private Transform coin;
        private Vector3 fallFrom;
        private Vector3 fallTo;
        private float fallT = 1f;
        private bool following;

        public Vector3 Position { get { return coin != null ? coin.position : transform.position; } }

        private void Awake()
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Coin";
            go.transform.SetParent(transform, false);
            // The cylinder primitive is 2 units tall and 1 unit wide.
            go.transform.localScale = new Vector3(diameter, thickness * 0.5f, diameter);
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Destroy(c);
            }
            go.GetComponent<Renderer>().material.color = color;
            coin = go.transform;
        }

        public void ShowResting(Vector3 position, Quaternion tableRotation)
        {
            following = false;
            fallT = 1f;
            coin.gameObject.SetActive(true);
            coin.SetPositionAndRotation(position + Vector3.up * thickness * 0.5f, tableRotation);
        }

        /// <summary>Call every frame while held. Snaps on the first frame, then follows smoothly.</summary>
        public void ShowHeld(Pose palm)
        {
            coin.gameObject.SetActive(true);
            fallT = 1f;
            if (!following)
            {
                following = true;
                coin.SetPositionAndRotation(palm.position, palm.rotation);
                return;
            }
            float k = 1f - Mathf.Exp(-followRate * Time.deltaTime);
            coin.SetPositionAndRotation(Vector3.Lerp(coin.position, palm.position, k), Quaternion.Slerp(coin.rotation, palm.rotation, k));
        }

        /// <summary>Place the coin anywhere (used for animated flights such as a production).</summary>
        public void ShowAt(Vector3 position, Quaternion rotation)
        {
            following = false;
            fallT = 1f;
            coin.gameObject.SetActive(true);
            coin.SetPositionAndRotation(position, rotation);
        }

        public void ShowDitched()
        {
            following = false;
            coin.gameObject.SetActive(false);
        }

        /// <summary>Released in plain view: drops onto the table surface below the release point.</summary>
        public void ShowExposed(Vector3 releasePoint, float tableHeight, Quaternion tableRotation)
        {
            following = false;
            coin.gameObject.SetActive(true);
            fallFrom = releasePoint;
            fallTo = new Vector3(releasePoint.x, tableHeight + thickness * 0.5f, releasePoint.z);
            fallT = 0f;
            coin.rotation = tableRotation;
        }

        private void Update()
        {
            if (fallT >= 1f || coin == null)
            {
                return;
            }
            fallT = Mathf.Min(1f, fallT + Time.deltaTime / Mathf.Max(0.01f, fallSeconds));
            coin.position = Vector3.Lerp(fallFrom, fallTo, fallT * fallT);
        }
    }
}
