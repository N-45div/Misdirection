using UnityEngine;

namespace Misdirection
{
    /// <summary>
    /// Where the magician is looking. Eye gaze when the device has it and it is confident
    /// (Quest Pro, Meta VR Glasses), head direction otherwise (Quest 3 / 3S).
    /// Every gameplay system asks this component, never the camera, so the
    /// "where you look, they look" rule works the same on every device.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public sealed class LookSource : MonoBehaviour
    {
        public enum Kind { Head, Eyes }

        [Tooltip("Head transform. Leave empty to use OVRCameraRig.centerEyeAnchor.")]
        [SerializeField] private Transform head;

        [Tooltip("Optional. Used only while eye tracking is running and confident.")]
        [SerializeField] private OVREyeGaze eyeGaze;

        [Range(0f, 1f)]
        [SerializeField] private float minEyeConfidence = 0.5f;

        [Tooltip("Ignore eye gaze even when available (A/B tests, accessibility option).")]
        [SerializeField] private bool forceHead;

        public static LookSource Current { get; private set; }

        public Kind ActiveKind { get; private set; }
        public Vector3 Origin { get; private set; }
        public Vector3 Direction { get; private set; }
        public Transform Head { get { return head; } }
        public Ray Ray { get { return new Ray(Origin, Direction); } }

        public bool ForceHead
        {
            get { return forceHead; }
            set { forceHead = value; }
        }

        private void Awake()
        {
            Direction = Vector3.forward;
            ResolveReferences();
        }

        private void OnEnable()
        {
            Current = this;
        }

        private void OnDisable()
        {
            if (Current == this)
            {
                Current = null;
            }
        }

        private void LateUpdate()
        {
            Refresh();
        }

        /// <summary>Recompute origin and direction. Safe to call more than once per frame.</summary>
        public void Refresh()
        {
            if (head == null)
            {
                ResolveReferences();
                if (head == null)
                {
                    return;
                }
            }

            Origin = head.position;

            if (!forceHead && EyesUsable())
            {
                ActiveKind = Kind.Eyes;
                Direction = eyeGaze.transform.forward;
            }
            else
            {
                ActiveKind = Kind.Head;
                Direction = head.forward;
            }
        }

        /// <summary>Unsigned angle in degrees between the look direction and a world point.</summary>
        public float AngleTo(Vector3 worldPoint)
        {
            Vector3 toPoint = worldPoint - Origin;
            if (toPoint.sqrMagnitude < 1e-6f)
            {
                return 0f;
            }
            return Vector3.Angle(Direction, toPoint);
        }

        private bool EyesUsable()
        {
            return eyeGaze != null
                && eyeGaze.isActiveAndEnabled
                && eyeGaze.EyeTrackingEnabled
                && eyeGaze.Confidence >= minEyeConfidence;
        }

        private void ResolveReferences()
        {
            if (head == null)
            {
                OVRCameraRig rig = FindFirstObjectByType<OVRCameraRig>();
                if (rig != null)
                {
                    head = rig.centerEyeAnchor;
                }
            }

            if (eyeGaze == null)
            {
                eyeGaze = FindFirstObjectByType<OVREyeGaze>();
            }
        }
    }
}
