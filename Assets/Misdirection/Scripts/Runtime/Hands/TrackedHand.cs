using Misdirection.Core.Hands;
using Misdirection.Core.Tricks;
using UnityEngine;

namespace Misdirection.Hands
{
    /// <summary>
    /// One tracked hand as the game sees it: Meta's OVRHand/OVRSkeleton data turned into a
    /// presence state, a hand shape (pinch, thumb–index gap, openness) and a stable pose that
    /// freezes when tracking is lost and blends back when it returns. Anything a hand carries
    /// should follow <see cref="StablePalm"/>, never raw bones, so it can't be flung across the room.
    /// Supports both v207 skeleton layouts (OpenXR XRHand_* and legacy Hand_*).
    /// </summary>
    [DefaultExecutionOrder(-40)]
    public sealed class TrackedHand : MonoBehaviour
    {
        [SerializeField] private HandId side = HandId.Right;
        [SerializeField] private OVRHand hand;
        [SerializeField] private OVRSkeleton skeleton;
        [SerializeField] private HandPresenceSettings presenceSettings = new HandPresenceSettings();

        [Tooltip("Pose smoothing rate (1/s) while tracking is reliable.")]
        [SerializeField] private float smoothingRate = 30f;
        [Tooltip("Pose smoothing rate (1/s) while holding something or while uncertain: steadier, slightly laggier.")]
        [SerializeField] private float heldSmoothingRate = 14f;
        [Tooltip("Small sphere at the wrist showing the tracking state (green, amber, red, blue).")]
        [SerializeField] private bool showStateIndicator = true;

        private HandPresence presence;
        private Transform wristBone, palmBone, thumbTip, indexTip, indexKnuckle, middleTip, middleKnuckle, ringTip, ringKnuckle;
        private bool bonesCached;
        private Pose frozenWrist;
        private Pose stableWrist;
        private Vector3 palmOffsetLocal = new Vector3(0f, -0.02f, 0.06f);
        private bool stableInitialised;
        private Renderer indicator;

        public HandId Side { get { return side; } }
        public HandPresence Presence { get { return presence; } }
        public HandShape Shape { get; private set; }
        public bool Tracked { get; private set; }
        public bool HighConfidence { get; private set; }

        /// <summary>Raw wrist pose from tracking this frame (only meaningful while tracked).</summary>
        public Pose LiveWrist { get; private set; }

        /// <summary>Wrist pose to attach things to: smoothed, frozen when lost, blended back on return.</summary>
        public Pose StableWrist { get { return stableWrist; } }

        /// <summary>Where a palmed or pinched coin sits, derived from the stable wrist pose.</summary>
        public Pose StablePalm
        {
            get { return new Pose(stableWrist.position + stableWrist.rotation * palmOffsetLocal, stableWrist.rotation); }
        }

        /// <summary>Midpoint of thumb and index tips (live while tracked, else the stable palm).</summary>
        public Vector3 PinchPoint { get; private set; }

        /// <summary>Set by whoever gives this hand something to carry; adds smoothing.</summary>
        public bool Carrying { get; set; }

        public void Configure(HandId handSide, OVRHand ovrHand, OVRSkeleton ovrSkeleton)
        {
            side = handSide;
            hand = ovrHand;
            skeleton = ovrSkeleton;
            bonesCached = false;
        }

        private void Awake()
        {
            presence = new HandPresence(presenceSettings);
            frozenWrist = Pose.identity;
            stableWrist = Pose.identity;
            LiveWrist = Pose.identity;
            if (hand != null && skeleton == null)
            {
                skeleton = hand.GetComponent<OVRSkeleton>();
            }
            if (showStateIndicator)
            {
                GameObject dot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                dot.name = side + " Hand State";
                dot.transform.SetParent(transform, false);
                dot.transform.localScale = Vector3.one * 0.012f;
                Collider c = dot.GetComponent<Collider>();
                if (c != null)
                {
                    Destroy(c);
                }
                indicator = dot.GetComponent<Renderer>();
            }
        }

        private void LateUpdate()
        {
            double now = Time.timeAsDouble;
            Tracked = hand != null && hand.IsTracked;
            HighConfidence = Tracked && hand.HandConfidence == OVRHand.TrackingConfidence.High;
            presence.Update(now, Tracked, HighConfidence);

            bool haveBones = CacheBones();
            if (Tracked)
            {
                ReadLive(haveBones);
            }
            UpdateStablePose();
            UpdateIndicator();
        }

        private void ReadLive(bool haveBones)
        {
            Pose wrist = haveBones && wristBone != null
                ? new Pose(wristBone.position, wristBone.rotation)
                : new Pose(hand.transform.position, hand.transform.rotation);
            LiveWrist = wrist;

            float pinch = hand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            float thumbIndex = float.NaN;
            float openness = float.NaN;
            if (haveBones)
            {
                if (thumbTip != null && indexTip != null)
                {
                    thumbIndex = Vector3.Distance(thumbTip.position, indexTip.position);
                    PinchPoint = (thumbTip.position + indexTip.position) * 0.5f;
                }
                openness = HandOpenness.Combine(
                    FingerOpenness(indexTip, indexKnuckle, wrist.position),
                    FingerOpenness(middleTip, middleKnuckle, wrist.position),
                    FingerOpenness(ringTip, ringKnuckle, wrist.position));
                if (palmBone != null)
                {
                    palmOffsetLocal = Quaternion.Inverse(wrist.rotation) * (palmBone.position - wrist.position);
                }
                else if (middleKnuckle != null)
                {
                    // Palm centre: about halfway from the wrist to the middle knuckle.
                    palmOffsetLocal = Quaternion.Inverse(wrist.rotation) * ((middleKnuckle.position - wrist.position) * 0.5f);
                }
            }
            if (float.IsNaN(thumbIndex))
            {
                PinchPoint = wrist.position + wrist.rotation * palmOffsetLocal;
            }
            Shape = HandShape.Pinch(pinch, thumbIndex, openness);

            // The frozen pose is the last pose seen while properly tracked (not mid-recovery).
            if (presence.State == HandPresenceState.Tracked || presence.State == HandPresenceState.Uncertain)
            {
                frozenWrist = wrist;
            }
        }

        private void UpdateStablePose()
        {
            if (!presence.HasPose)
            {
                return;
            }
            Pose target;
            if (!Tracked || presence.State == HandPresenceState.Lost)
            {
                target = frozenWrist;
            }
            else
            {
                float w = presence.LiveWeight;
                target = new Pose(Vector3.Lerp(frozenWrist.position, LiveWrist.position, w),
                                  Quaternion.Slerp(frozenWrist.rotation, LiveWrist.rotation, w));
            }

            if (!stableInitialised)
            {
                stableWrist = target;
                stableInitialised = true;
                return;
            }
            bool steady = presence.State == HandPresenceState.Tracked && !Carrying;
            float rate = steady ? smoothingRate : heldSmoothingRate;
            float k = 1f - Mathf.Exp(-rate * Time.deltaTime);
            stableWrist = new Pose(Vector3.Lerp(stableWrist.position, target.position, k),
                                   Quaternion.Slerp(stableWrist.rotation, target.rotation, k));
            if (!Tracked)
            {
                PinchPoint = StablePalm.position;
            }
        }

        private void UpdateIndicator()
        {
            if (indicator == null)
            {
                return;
            }
            indicator.enabled = presence.HasPose;
            indicator.transform.position = stableWrist.position;
            Color c;
            switch (presence.State)
            {
                case HandPresenceState.Tracked: c = new Color(0.3f, 0.9f, 0.4f); break;
                case HandPresenceState.Uncertain: c = Color.Lerp(new Color(0.3f, 0.9f, 0.4f), new Color(1f, 0.65f, 0.1f), presence.Tint); break;
                case HandPresenceState.Recovering: c = new Color(0.3f, 0.6f, 1f); break;
                default: c = new Color(1f, 0.25f, 0.2f); break;
            }
            indicator.material.color = c;
        }

        private static float FingerOpenness(Transform tip, Transform knuckle, Vector3 wrist)
        {
            if (tip == null || knuckle == null)
            {
                return float.NaN;
            }
            return HandOpenness.Finger(Vector3.Distance(tip.position, wrist), Vector3.Distance(knuckle.position, wrist));
        }

        private bool CacheBones()
        {
            if (bonesCached)
            {
                return true;
            }
            if (skeleton == null || !skeleton.IsInitialized || skeleton.Bones == null || skeleton.Bones.Count == 0)
            {
                return false;
            }
            // The two layouts reuse the same numbers (Hand_WristRoot == XRHand_Palm == 0), so pick the layout first.
            bool openXr = skeleton.GetSkeletonType().IsOpenXRHandSkeleton();
            foreach (OVRBone bone in skeleton.Bones)
            {
                if (bone == null || bone.Transform == null)
                {
                    continue;
                }
                if (openXr)
                {
                    MapOpenXr(bone.Id, bone.Transform);
                }
                else
                {
                    MapLegacy(bone.Id, bone.Transform);
                }
            }
            bonesCached = wristBone != null;
            return bonesCached;
        }

        private void MapOpenXr(OVRSkeleton.BoneId id, Transform t)
        {
            switch (id)
            {
                case OVRSkeleton.BoneId.XRHand_Wrist: wristBone = t; break;
                case OVRSkeleton.BoneId.XRHand_Palm: palmBone = t; break;
                case OVRSkeleton.BoneId.XRHand_ThumbTip: thumbTip = t; break;
                case OVRSkeleton.BoneId.XRHand_IndexTip: indexTip = t; break;
                case OVRSkeleton.BoneId.XRHand_IndexProximal: indexKnuckle = t; break;
                case OVRSkeleton.BoneId.XRHand_MiddleTip: middleTip = t; break;
                case OVRSkeleton.BoneId.XRHand_MiddleProximal: middleKnuckle = t; break;
                case OVRSkeleton.BoneId.XRHand_RingTip: ringTip = t; break;
                case OVRSkeleton.BoneId.XRHand_RingProximal: ringKnuckle = t; break;
            }
        }

        private void MapLegacy(OVRSkeleton.BoneId id, Transform t)
        {
            switch (id)
            {
                case OVRSkeleton.BoneId.Hand_WristRoot: wristBone = t; break;
                case OVRSkeleton.BoneId.Hand_ThumbTip: thumbTip = t; break;
                case OVRSkeleton.BoneId.Hand_IndexTip: indexTip = t; break;
                case OVRSkeleton.BoneId.Hand_Index1: indexKnuckle = t; break;
                case OVRSkeleton.BoneId.Hand_MiddleTip: middleTip = t; break;
                case OVRSkeleton.BoneId.Hand_Middle1: middleKnuckle = t; break;
                case OVRSkeleton.BoneId.Hand_RingTip: ringTip = t; break;
                case OVRSkeleton.BoneId.Hand_Ring1: ringKnuckle = t; break;
            }
        }
    }
}
