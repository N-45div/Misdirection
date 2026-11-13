using System;
using System.Collections.Generic;
using Oculus.Interaction;
using Oculus.Interaction.Input;
using Oculus.Interaction.Surfaces;
using UnityEngine;

namespace Misdirection.Audience
{
    /// <summary>
    /// "Which spectator am I looking at" through Meta's Interaction SDK (ISDK v207 gaze interaction,
    /// Day 12). Every spectator's face is an ISDK <see cref="GazeInteractable"/>; one ISDK
    /// <see cref="GazeInteractor"/> hovers them, hit-testing with ISDK's <see cref="GazeConecaster"/>
    /// along the ray that <see cref="LookSource"/> supplies: head direction on Quest 3 / 3S, the eyes
    /// on devices that track them. The hovered spectator goes to the crowd model
    /// (<see cref="Core.Crowd.CrowdModel.ReportHover"/>), which drives eye contact, misdirection and
    /// the look-and-pinch pluck exactly as before.
    ///
    /// What is deliberately not used: the Eye Gaze building block and ISDK's own eye-gaze data source.
    /// On Quest 3 / 3S they fall back to hand rays (docs/OPEN_ITEMS.md #14), which would break "where
    /// you look, they look". This component is the ISDK gaze source instead (<see cref="IGaze"/>).
    ///
    /// Same feel as the crowd model's own test: the cone is the model's acquire angle (6 degrees, so
    /// a face is a 12 degree target; Meta's minimum is 3), ISDK's dwell is off because the game has
    /// its own (0.3 s eye contact, the pluck), and the model keeps a face until the look drifts 9
    /// degrees away. If anything here fails, or the component is absent, the model's own test runs.
    ///
    /// The pinch is not wired to ISDK selection continuously: the coin is held by a pinch for most
    /// of a trick, and a selecting ISDK interactor stops looking for new targets, which would freeze
    /// who is looked at. Instead the trick reports the pluck itself (<see cref="Pluck"/>), which
    /// selects and releases the hovered face in one frame.
    /// </summary>
    [DefaultExecutionOrder(-10)]
    public sealed class IsdkSpectatorGaze : MonoBehaviour, IGaze, ISelector
    {
        [SerializeField] private Crowd crowd;
        [SerializeField] private LookSource lookSource;
        [Tooltip("Radius of the gaze target at the middle of each face (metres). Small on purpose: the cone angle, " +
                 "not the target, sets how close the look must be, so near and far spectators behave alike.")]
        [SerializeField] private float faceTargetRadius = 0.005f;
        [Tooltip("How far the gaze reaches (metres).")]
        [SerializeField] private float coneLength = 10f;

        private readonly List<Spectator> owners = new List<Spectator>();
        private readonly List<GazeInteractable> faces = new List<GazeInteractable>();
        private GazeInteractor interactor;
        private GazeConecaster conecaster;
        private Transform pointer;
        private Pose pose = Pose.identity;
        private bool poseValid;
        private bool failed;

        // ISDK's gaze source and selector interfaces.
        public event Action WhenUpdated = delegate { };
        public event Action WhenSelected = delegate { };
        public event Action WhenUnselected = delegate { };

        public bool IsTrackedDataValid { get { return poseValid; } }

        /// <summary>True while the looked-at spectator is coming from the ISDK interactor.</summary>
        public bool Working { get; private set; }

        /// <summary>Index of the spectator the ISDK interactor hovers, or -1.</summary>
        public int Hovered { get; private set; }

        /// <summary>One line for HUDs and the smoke report.</summary>
        public string Status { get; private set; }

        public bool TryGetWorldPose(out Pose worldPose)
        {
            worldPose = pose;
            return poseValid;
        }

        public void Configure(Crowd audience, LookSource look)
        {
            crowd = audience;
            lookSource = look;
        }

        /// <summary>
        /// The pluck happened (looked at a spectator and pinched): tell ISDK as a select and release of
        /// the hovered face, so pointer events and ISDK's own bookkeeping see a look-and-pinch.
        /// </summary>
        public void Pluck()
        {
            if (failed || interactor == null)
            {
                return;
            }
            try
            {
                WhenSelected();
                WhenUnselected();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Awake()
        {
            Hovered = -1;
            Status = "ISDK gaze: starting";
            if (crowd == null)
            {
                crowd = GetComponent<Crowd>();
            }
        }

        private void Start()
        {
            try
            {
                Build();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void OnDisable()
        {
            Working = false;
            Hovered = -1;
        }

        /// <summary>The ISDK interactor, its cone-cast and the transform it reports pointer poses from.</summary>
        private void Build()
        {
            if (crowd == null || crowd.Model == null)
            {
                throw new InvalidOperationException("no Crowd on this object");
            }
            GameObject host = new GameObject("ISDK Gaze Interactor");
            host.transform.SetParent(transform, false);
            pointer = new GameObject("Gaze Pointer").transform;
            pointer.SetParent(host.transform, false);

            conecaster = host.AddComponent<GazeConecaster>();
            conecaster.InjectAllGazeConecaster(this);
            conecaster.ConeAngle = crowd.Model.Settings.FaceAngleDegrees;
            conecaster.ConeLength = coneLength;
            // No ISDK dwell: the game's own dwell times sit on top of who is looked at.
            conecaster.DwellTimespanSeconds = 0f;
            // Unscaled, so the cone-cast's bookkeeping keeps moving while the show is paused.
            conecaster.SetTimeProvider(UnscaledTime);

            interactor = host.AddComponent<GazeInteractor>();
            interactor.InjectAllGazeInteractor(this, pointer, conecaster);
            // Driven from LateUpdate below, once LookSource has this frame's head pose, so the crowd
            // model gets this frame's answer (ISDK interactors otherwise drive themselves in Update).
            interactor.IsRootDriver = false;
            Status = "ISDK gaze: built, waiting for the interactor to start";
        }

        private void LateUpdate()
        {
            if (failed || interactor == null || crowd == null || crowd.Model == null)
            {
                return;
            }
            try
            {
                Step();
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        private void Step()
        {
            LookSource look = lookSource != null ? lookSource : LookSource.Current;
            if (look == null || look.Head == null || look.Direction.sqrMagnitude < 1e-8f)
            {
                poseValid = false;
                Working = false;
                return;
            }
            pose = new Pose(look.Origin, Quaternion.LookRotation(look.Direction));
            poseValid = true;
            pointer.SetPositionAndRotation(pose.position, pose.rotation);

            SyncFaces();
            // ISDK's order: the gaze source announces new data, then the interactor asks its hit test.
            WhenUpdated();
            interactor.Drive();

            if (interactor.State == InteractorState.Disabled)
            {
                // Not started yet (ISDK components start on the frame after they are added).
                Working = false;
                return;
            }
            Hovered = IndexOf(interactor.Interactable);
            crowd.Model.ReportHover(Hovered);
            if (!Working)
            {
                Working = true;
                Status = "ISDK GazeInteractor (" + conecaster.ConeAngle.ToString("F0") + " degree cone, " + look.ActiveKind + ")";
                Debug.Log("[Misdirection] Gaze: spectator faces are ISDK gaze interactables; looked-at spectator from the " + Status + ".");
            }
        }

        /// <summary>Every spectator in the crowd has a face target; targets of spectators that left are forgotten.</summary>
        private void SyncFaces()
        {
            for (int i = owners.Count - 1; i >= 0; i--)
            {
                if (owners[i] == null)
                {
                    owners.RemoveAt(i);
                    faces.RemoveAt(i);
                }
            }
            IReadOnlyList<Spectator> spectators = crowd.Spectators;
            for (int i = 0; i < spectators.Count; i++)
            {
                Spectator spectator = spectators[i];
                if (spectator == null || owners.Contains(spectator))
                {
                    continue;
                }
                GazeInteractable face = AddFace(spectator);
                if (face != null)
                {
                    owners.Add(spectator);
                    faces.Add(face);
                }
            }
        }

        /// <summary>A small ISDK gaze target at the midpoint of the eyes, the point the crowd model's own test aims at.</summary>
        private GazeInteractable AddFace(Spectator spectator)
        {
            Transform head = spectator.Head;
            if (head == null)
            {
                return null;
            }
            GameObject go = new GameObject("Face (ISDK gaze target)");
            go.transform.SetParent(head, false);
            // Place it before adding the collider, so the collider starts in the right spot.
            go.transform.SetPositionAndRotation(spectator.EyesPosition, head.rotation);
            SphereCollider sphere = go.AddComponent<SphereCollider>();
            sphere.radius = faceTargetRadius / Mathf.Max(1e-4f, head.lossyScale.z);
            ColliderSurface surface = go.AddComponent<ColliderSurface>();
            surface.InjectAllColliderSurface(sphere);
            GazeInteractable face = go.AddComponent<GazeInteractable>();
            face.InjectAllGazeInteractable(surface);
            return face;
        }

        private int IndexOf(GazeInteractable face)
        {
            if (face == null)
            {
                return -1;
            }
            int slot = faces.IndexOf(face);
            if (slot < 0)
            {
                return -1;
            }
            Spectator owner = owners[slot];
            IReadOnlyList<Spectator> spectators = crowd.Spectators;
            for (int i = 0; i < spectators.Count; i++)
            {
                if (spectators[i] == owner)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>Anything went wrong inside ISDK: step aside for good and let the crowd model look for itself.</summary>
        private void Fail(Exception e)
        {
            failed = true;
            Working = false;
            Hovered = -1;
            Status = "crowd model (ISDK gaze failed: " + e.GetType().Name + ")";
            Debug.LogWarning("[Misdirection] Gaze: the ISDK gaze interactor failed (" + e.GetType().Name + ": " + e.Message +
                             "). The crowd model's own gaze test takes over; nothing else changes.");
            try
            {
                if (interactor != null)
                {
                    interactor.gameObject.SetActive(false);
                }
            }
            catch (Exception)
            {
                // Already stepping aside; nothing more to do.
            }
        }

        private static float UnscaledTime()
        {
            return Time.unscaledTime;
        }
    }
}
