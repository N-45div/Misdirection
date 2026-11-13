using System;
using System.Collections.Generic;
using Misdirection.Audience;
using Misdirection.Core.Access;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;
using Misdirection.Hands;
using Misdirection.Show;
using Misdirection.Table;
using Misdirection.Tricks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.Access
{
    /// <summary>
    /// The accessibility layer's hub (Day 11). Loads and saves the player's options, decides when
    /// one-handed play is active, and applies what the options change: relaxed timing on the tricks,
    /// tutorial and crowd; the mentor's captions; high-contrast styling of the servante outline and
    /// the coin. Publishes the result to <see cref="AccessSettings"/>, which the rest of the game reads.
    /// The <see cref="AccessTray"/>, <see cref="SoundCaptions"/> and <see cref="VoiceLook"/> sit on the
    /// same object. Rules live in Misdirection.Core.Access.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public sealed class AccessRuntime : MonoBehaviour
    {
        public const string PrefsKey = "misdirection.access";

        [SerializeField] private TrackedHand leftHand;
        [SerializeField] private TrackedHand rightHand;
        [SerializeField] private TableCalibrator table;
        [SerializeField] private Crowd crowd;
        [SerializeField] private LookSource lookSource;
        [SerializeField] private MentorHands mentor;
        [SerializeField] private ShowDirector show;

        [SerializeField] private OneHandedSettings oneHanded = new OneHandedSettings();
        [SerializeField] private RelaxedTimingSettings relaxed = new RelaxedTimingSettings();

        private AccessOptions options = new AccessOptions();
        private OneHandedDetector detector;
        private RelaxedTimingBaseline baseline;
        private TrickDirector vanish;
        private CupTrickDirector cup;
        private readonly Dictionary<SpectatorProfile, float> designedTurnRate = new Dictionary<SpectatorProfile, float>();
        private bool dirty = true;
        private LineRenderer styledOutline;
        private float designedOutlineWidth;
        private Color designedOutlineColor;
        private GameObject coinRim;
        private AudioSource audioSource;
        private AudioClip onTone, offTone;

        public static AccessRuntime Current { get; private set; }

        public AccessOptions Options { get { return options; } }
        public bool OneHandedActive { get { return detector != null && detector.Active; } }
        public HandId ActiveHand { get { return detector != null ? detector.ActiveHand : HandId.None; } }

        public TrackedHand LeftHand { get { return leftHand; } }
        public TrackedHand RightHand { get { return rightHand; } }
        public TableCalibrator Table { get { return table; } }
        public Crowd Crowd { get { return crowd; } }
        public LookSource Look { get { return lookSource; } }
        public MentorHands Mentor { get { return mentor; } }
        public ShowDirector Show { get { return show; } }

        /// <summary>Fires after an option changes (the tray redraws, the voice starts or stops listening).</summary>
        public event Action<AccessToggle> Changed;

        /// <summary>Compact state for logs and the simulator smoke test, e.g. "one=auto;relaxed=0;captions=1;contrast=0;voice=0".</summary>
        public string Summary { get { return options.Encode(); } }

        /// <summary>
        /// True while a trick is mid-move (coin picked up, bust or bow playing): eyes-only toggles wait,
        /// so a look at the tray can't change the rules under a move.
        /// </summary>
        public bool TrickBusy
        {
            get
            {
                if (vanish != null && vanish.isActiveAndEnabled && vanish.Trick != null && vanish.Trick.Phase != TrickPhase.Idle)
                {
                    return true;
                }
                return cup != null && cup.isActiveAndEnabled && cup.Trick != null && cup.Trick.Phase != CupPhase.Idle;
            }
        }

        /// <summary>The show is paused waiting for the player's hands: nothing is mid-move, so the tray may be looked at.</summary>
        public bool WaitingForHands { get { return show != null && show.PauseReason == PauseReason.HandsGone; } }

        /// <summary>The system has taken focus (its menu is up, the headset is off): the tray ignores looks and hands.</summary>
        public bool FocusLost { get { return show != null && show.PauseReason == PauseReason.FocusLost; } }

        public void Configure(TrackedHand left, TrackedHand right, TableCalibrator tableCalibrator, Crowd audience, LookSource look,
                              MentorHands mentorHands, ShowDirector showDirector)
        {
            leftHand = left;
            rightHand = right;
            table = tableCalibrator;
            crowd = audience;
            lookSource = look;
            mentor = mentorHands;
            show = showDirector;
        }

        /// <summary>Flip one option (the tray, or a test), save it and apply it.</summary>
        public void Toggle(AccessToggle toggle)
        {
            options.Toggle(toggle);
            Save();
            dirty = true;
            Publish();
            Play(options.IsOn(toggle) ? onTone : offTone);
            Debug.Log("[Misdirection] Access: " + AccessOptions.Label(toggle) + " " + options.StateWord(toggle) + " (" + options.Encode() + ")");
            if (Changed != null)
            {
                Changed(toggle);
            }
        }

        /// <summary>Switch voice off without the player asking (no microphone, or permission refused).</summary>
        public void VoiceUnavailable()
        {
            if (options.Voice)
            {
                Toggle(AccessToggle.Voice);
            }
        }

        /// <summary>Back to the defaults on the next launch (and now, if the game is running).</summary>
        public static void ForgetSettings()
        {
            PlayerPrefs.DeleteKey(PrefsKey);
            PlayerPrefs.Save();
            if (Current != null)
            {
                Current.options = new AccessOptions();
                Current.dirty = true;
                Current.Publish();
                if (Current.Changed != null)
                {
                    Current.Changed(AccessToggle.OneHand);
                }
            }
        }

        /// <summary>Adds the whole accessibility layer to <paramref name="host"/>. Scene references resolve themselves if left unset.</summary>
        public static AccessRuntime AddTo(GameObject host)
        {
            AccessRuntime runtime = host.AddComponent<AccessRuntime>();
            host.AddComponent<AccessTray>();
            host.AddComponent<SoundCaptions>();
            return runtime;
        }

        // ------------------------------------------------------------ safety net

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void WatchScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AfterFirstScene()
        {
            EnsureInScene();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureInScene();
        }

        /// <summary>
        /// A show scene saved before Day 11 has no accessibility layer in it. Accessibility must never
        /// depend on somebody having re-run the editor setup, so add it here when it is missing.
        /// </summary>
        private static void EnsureInScene()
        {
            if (FindFirstObjectByType<AccessRuntime>(FindObjectsInactive.Include) != null)
            {
                return;
            }
            if (FindFirstObjectByType<ShowDirector>() == null)
            {
                return;
            }
            AddTo(new GameObject("Misdirection Access (added at runtime)"));
            Debug.Log("[Misdirection] Access: this scene has no accessibility layer saved in it; added one at runtime. " +
                      "Run Misdirection > Day 11 > Add Accessibility to Open Scene to save it into the scene.");
        }

        // ------------------------------------------------------------ life cycle

        private void Awake()
        {
            Current = this;
            options = AccessOptions.Decode(PlayerPrefs.GetString(PrefsKey, ""));
            detector = new OneHandedDetector(oneHanded);
            ResolveReferences();

            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
            // The tray works while the show is paused.
            audioSource.ignoreListenerPause = true;
            onTone = Tones.Chirp("access on", 660f, 990f, 0.12f, 0.25f);
            offTone = Tones.Chirp("access off", 660f, 440f, 0.12f, 0.25f);
            Publish();
        }

        private void Start()
        {
            ResolveReferences();
            // Every other component has run Awake by now: remember the timings as designed, then apply.
            baseline = RelaxedTimingBaseline.Capture(
                vanish != null ? vanish.Settings : null,
                cup != null ? cup.Settings : null,
                show != null ? show.TutorialSettings : null,
                crowd != null ? crowd.Attention : null);
            dirty = true;
            Debug.Log("[Misdirection] Access: ready (" + options.Encode() + ").");
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
                AccessSettings.Clear();
            }
        }

        private void LateUpdate()
        {
            // TrackedHand (order -40) has just read this frame's tracking. Unscaled time: this must keep
            // working while the show is paused, or a one-handed player could never resume.
            bool left = leftHand != null && leftHand.Tracked;
            bool right = rightHand != null && rightHand.Tracked;
            detector.Mode = options.OneHand;
            if (detector.Update(Time.unscaledTimeAsDouble, left, right))
            {
                Debug.Log("[Misdirection] Access: one-handed play " + (detector.Active ? "on (" + detector.ActiveHand + " hand)" : "off") + ".");
                if (Changed != null)
                {
                    Changed(AccessToggle.OneHand);
                }
            }
            Publish();

            if (dirty && baseline != null)
            {
                dirty = false;
                ApplyOptions();
            }
            ApplyCrowdPace();
            ApplyContrast();
        }

        private void Publish()
        {
            AccessSettings.Publish(options, detector != null && detector.Active, detector != null ? detector.ActiveHand : HandId.None,
                                   RelaxedTiming.SuspicionScale(options.Relaxed, relaxed));
        }

        // ------------------------------------------------------------ applying the options

        private void ApplyOptions()
        {
            baseline.Apply(
                vanish != null ? vanish.Settings : null,
                cup != null ? cup.Settings : null,
                show != null ? show.TutorialSettings : null,
                crowd != null ? crowd.Attention : null,
                options.Relaxed, relaxed);
            if (mentor != null)
            {
                mentor.Captions = options.Captions;
            }
        }

        /// <summary>
        /// Relaxed timing slows how fast each spectator's attention turns. Spectators come and go with
        /// the acts, so this checks the current line-up every frame (five people at most).
        /// </summary>
        private void ApplyCrowdPace()
        {
            if (crowd == null)
            {
                return;
            }
            IReadOnlyList<Spectator> spectators = crowd.Spectators;
            if (designedTurnRate.Count > 64)
            {
                ForgetDepartedSpectators(spectators);
            }
            for (int i = 0; i < spectators.Count; i++)
            {
                if (spectators[i] == null || spectators[i].Mind == null)
                {
                    continue;
                }
                SpectatorProfile profile = spectators[i].Mind.Profile;
                float designed;
                if (!designedTurnRate.TryGetValue(profile, out designed))
                {
                    designed = profile.TurnRate;
                    designedTurnRate[profile] = designed;
                }
                float wanted = RelaxedTiming.TurnRate(designed, options.Relaxed, relaxed);
                if (!Mathf.Approximately(profile.TurnRate, wanted))
                {
                    profile.TurnRate = wanted;
                }
            }
        }

        private void ForgetDepartedSpectators(IReadOnlyList<Spectator> spectators)
        {
            Dictionary<SpectatorProfile, float> keep = new Dictionary<SpectatorProfile, float>();
            for (int i = 0; i < spectators.Count; i++)
            {
                float designed;
                if (spectators[i] != null && spectators[i].Mind != null && designedTurnRate.TryGetValue(spectators[i].Mind.Profile, out designed))
                {
                    keep[spectators[i].Mind.Profile] = designed;
                }
            }
            designedTurnRate.Clear();
            foreach (KeyValuePair<SpectatorProfile, float> pair in keep)
            {
                designedTurnRate[pair.Key] = pair.Value;
            }
        }

        /// <summary>High contrast: a bold servante outline and a bright rim round the coin. (Cones and markers read AccessSettings themselves.)</summary>
        private void ApplyContrast()
        {
            bool bold = options.HighContrast;

            LineRenderer outline = table != null ? table.ServanteOutline : null;
            if (outline != styledOutline)
            {
                // The table draws its outline the first time it is calibrated: remember how it was designed.
                styledOutline = outline;
                if (outline != null)
                {
                    designedOutlineWidth = outline.startWidth;
                    designedOutlineColor = outline.startColor;
                }
            }
            if (outline != null)
            {
                float width = bold ? AccessSettings.BoldOutlineWidth : designedOutlineWidth;
                Color color = bold ? AccessSettings.BoldOutlineColor : designedOutlineColor;
                if (!Mathf.Approximately(outline.startWidth, width) || outline.startColor != color)
                {
                    outline.startWidth = outline.endWidth = width;
                    outline.startColor = outline.endColor = color;
                }
            }

            if (bold && coinRim == null)
            {
                coinRim = BuildCoinRim();
            }
            if (coinRim != null && coinRim.activeSelf != bold)
            {
                coinRim.SetActive(bold);
            }
        }

        /// <summary>A white ring with a black edge under the coin, so it stands out on any table. Follows the coin by being its child.</summary>
        private GameObject BuildCoinRim()
        {
            CoinActor coin = vanish != null ? vanish.Coin : cup != null ? cup.Coin : null;
            Transform visual = coin != null ? coin.Visual : null;
            if (visual == null)
            {
                return null;
            }
            GameObject rim = new GameObject("High Contrast Rim");
            rim.transform.SetParent(visual, false);
            // The coin's transform is already scaled to its diameter and thickness; these are multiples of it.
            // Drawn outermost first (the shared material doesn't write depth, so the order is set by hand).
            Disc(rim.transform, 1.75f, 0.45f, Color.black, 0);
            Disc(rim.transform, 1.5f, 0.6f, Color.white, 1);
            Disc(rim.transform, 1.15f, 0.8f, Color.black, 2);
            return rim;
        }

        private static void Disc(Transform parent, float width, float thickness, Color color, int order)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Rim";
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(width, thickness, width);
            Collider c = go.GetComponent<Collider>();
            if (c != null)
            {
                Destroy(c);
            }
            Renderer r = go.GetComponent<Renderer>();
            r.sharedMaterial = AccessSettings.Shared(color);
            r.sortingOrder = order;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        // ------------------------------------------------------------ helpers

        private void ResolveReferences()
        {
            if (show == null)
            {
                show = FindFirstObjectByType<ShowDirector>();
            }
            if (table == null)
            {
                table = FindFirstObjectByType<TableCalibrator>();
            }
            if (crowd == null)
            {
                crowd = FindFirstObjectByType<Crowd>();
            }
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }
            if (mentor == null)
            {
                mentor = FindFirstObjectByType<MentorHands>();
            }
            if (leftHand == null || rightHand == null)
            {
                foreach (TrackedHand hand in FindObjectsByType<TrackedHand>(FindObjectsSortMode.None))
                {
                    if (hand.Side == HandId.Left && leftHand == null)
                    {
                        leftHand = hand;
                    }
                    else if (hand.Side == HandId.Right && rightHand == null)
                    {
                        rightHand = hand;
                    }
                }
            }
            // Tricks take turns being enabled, so look at disabled ones too.
            if (vanish == null)
            {
                vanish = FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include);
            }
            if (cup == null)
            {
                cup = FindFirstObjectByType<CupTrickDirector>(FindObjectsInactive.Include);
            }
        }

        private void Save()
        {
            PlayerPrefs.SetString(PrefsKey, options.Encode());
            PlayerPrefs.Save();
        }

        private void Play(AudioClip clip)
        {
            if (audioSource != null && clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }
}
