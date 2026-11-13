using Misdirection.Audience;
using Misdirection.Core.Access;
using Misdirection.Table;
using UnityEngine;
#if UNITY_ANDROID && !UNITY_EDITOR
using UnityEngine.Android;
#endif

namespace Misdirection.Access
{
    /// <summary>
    /// The voice alternative to the show-hand flourish (Day 11), for players without a free hand:
    /// say "Look!" (or clap, or tap the table) and the crowd's eyes go to where you are looking.
    /// Off by default. The microphone is only opened, and its permission only asked for, at the
    /// moment the player switches the Voice token on: never at startup, not even when voice was left
    /// on last time and the permission has since gone (then voice switches itself off instead). If
    /// there is no microphone or the player says no, the token switches itself back off and a caption
    /// says why. No speech is recognised or stored: only the loudness is read
    /// (Misdirection.Core.Access.LoudSoundDetector).
    /// </summary>
    [DefaultExecutionOrder(34)]
    public sealed class VoiceLook : MonoBehaviour
    {
        private enum State { Off, Asking, Listening }

        [SerializeField] private LoudSoundSettings settings = new LoudSoundSettings();
        [SerializeField] private int sampleRate = 16000;
        [Tooltip("How strongly a call pulls the crowd's attention (a coin's clink is 1).")]
        [SerializeField] private float strength = 1f;
        [Tooltip("While the game's own sound is at least this loud (0..1), the microphone is ignored: it would hear the headset's speakers.")]
        [SerializeField] private float gameSoundLevel = 0.06f;
        [Tooltip("Give up waiting for the permission answer after this long (seconds).")]
        [SerializeField] private float askTimeoutSeconds = 60f;

        private const int BlockSize = 256;

        private AccessRuntime access;
        private LoudSoundDetector detector;
        private State state = State.Off;
        private AudioClip clip;
        private string device;
        private int readPosition;
        private readonly float[] block = new float[BlockSize];
        private readonly float[] output = new float[BlockSize];
        private double askedAt;
        private bool granted;
        private bool denied;
        private bool playerAsked;

        /// <summary>True while the microphone is open and being listened to.</summary>
        public bool Listening { get { return state == State.Listening; } }

        /// <summary>Calls heard since the microphone was opened (for logs and tests).</summary>
        public int Calls { get { return detector != null ? detector.Count : 0; } }

        private void Awake()
        {
            access = GetComponent<AccessRuntime>();
            detector = new LoudSoundDetector(settings);
        }

        private void OnEnable()
        {
            if (access != null)
            {
                access.Changed += OnOptionChanged;
            }
        }

        private void OnDisable()
        {
            if (access != null)
            {
                access.Changed -= OnOptionChanged;
            }
            StopListening();
        }

        private void OnOptionChanged(AccessToggle toggle)
        {
            // The player has just switched voice on: now, and only now, the permission may be asked for.
            if (toggle == AccessToggle.Voice && access.Options.Voice)
            {
                playerAsked = true;
            }
        }

        private void OnApplicationPause(bool paused)
        {
            // Headset taken off or the app put in the background: let go of the microphone. Update() opens it
            // again when the app is back, if voice is still on. A permission question in progress is left alone
            // (the system's dialog can itself pause the app; asking again here would loop).
            if (paused && state == State.Listening)
            {
                StopListening();
            }
        }

        private void Update()
        {
            bool wanted = access != null && access.Options.Voice;
            if (!wanted)
            {
                if (state != State.Off)
                {
                    StopListening();
                }
                return;
            }
            switch (state)
            {
                case State.Off: Begin(); break;
                case State.Asking: WaitForAnswer(); break;
                case State.Listening: Listen(); break;
            }
        }

        // ------------------------------------------------------------ permission

        /// <summary>
        /// Voice is on and the microphone is not open yet. This is the only place the permission is asked for,
        /// and only straight after the player switched voice on. Voice left on from last time with the
        /// permission gone (revoked, or granted "only this time") switches off quietly: no prompt at startup.
        /// </summary>
        private void Begin()
        {
            askedAt = Time.unscaledTimeAsDouble;
            granted = false;
            denied = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!Permission.HasUserAuthorizedPermission(Permission.Microphone))
            {
                if (!playerAsked)
                {
                    Unavailable("Voice needs the microphone again: switch it back on to allow it");
                    return;
                }
                playerAsked = false;
                state = State.Asking;
                PermissionCallbacks callbacks = new PermissionCallbacks();
                callbacks.PermissionGranted += OnPermissionGranted;
                callbacks.PermissionDenied += OnPermissionDenied;
                Permission.RequestUserPermission(Permission.Microphone, callbacks);
                Debug.Log("[Misdirection] Voice: asking for the microphone (the player switched voice on).");
                return;
            }
#endif
            playerAsked = false;
            OpenMicrophone();
        }

        private void OnPermissionGranted(string permission)
        {
            granted = true;
        }

        private void OnPermissionDenied(string permission)
        {
            denied = true;
        }

        private void WaitForAnswer()
        {
            bool allowed = granted;
#if UNITY_ANDROID && !UNITY_EDITOR
            // Belt and braces: some system dialogs answer without calling back.
            allowed |= Permission.HasUserAuthorizedPermission(Permission.Microphone);
#endif
            if (allowed)
            {
                OpenMicrophone();
            }
            else if (denied || Time.unscaledTimeAsDouble - askedAt > askTimeoutSeconds)
            {
                Unavailable("Microphone not allowed: voice is off");
            }
        }

        // ------------------------------------------------------------ microphone

        private void OpenMicrophone()
        {
            string[] devices = Microphone.devices;
            if (devices == null || devices.Length == 0)
            {
                Unavailable("No microphone found: voice is off");
                return;
            }
            device = devices[0];
            // Use the rate we want if the device allows it (0, 0 means any rate).
            int minRate;
            int maxRate;
            Microphone.GetDeviceCaps(device, out minRate, out maxRate);
            int rate = sampleRate;
            if (maxRate > 0)
            {
                rate = Mathf.Clamp(rate, Mathf.Max(1, minRate), maxRate);
            }
            clip = Microphone.Start(device, true, 1, rate);
            if (clip == null)
            {
                Unavailable("The microphone would not start: voice is off");
                return;
            }
            readPosition = 0;
            detector.Reset();
            state = State.Listening;
            Debug.Log("[Misdirection] Voice: listening on \"" + device + "\" for a short loud sound (\"Look!\").");
        }

        private void StopListening()
        {
            if (clip != null)
            {
                Microphone.End(device);
                clip = null;
            }
            state = State.Off;
        }

        private void Unavailable(string why)
        {
            StopListening();
            Debug.LogWarning("[Misdirection] Voice: " + why + ".");
            if (access == null)
            {
                return;
            }
            SoundCaptions captions = SoundCaptions.Current;
            AccessTray tray = GetComponent<AccessTray>();
            if (captions != null && tray != null)
            {
                captions.Say(why, tray.TokenPosition(AccessToggle.Voice) + Vector3.up * 0.09f, 4f, true);
            }
            access.VoiceUnavailable();
        }

        /// <summary>Reads what the microphone has recorded since last frame, one short block at a time.</summary>
        private void Listen()
        {
            if (clip == null || !Microphone.IsRecording(device))
            {
                Unavailable("The microphone stopped: voice is off");
                return;
            }
            double now = Time.unscaledTimeAsDouble;
            if (GameIsLoud())
            {
                // The headset's speakers sit next to its microphones: don't hear our own applause as a call.
                detector.Suppress(now + 0.35);
            }
            int samples = clip.samples;
            int writePosition = Microphone.GetPosition(device);
            if (samples <= 0 || writePosition < 0)
            {
                return;
            }
            int available = (writePosition - readPosition + samples) % samples;
            int rate = Mathf.Max(1, clip.frequency);
            bool heard = false;
            while (available >= BlockSize)
            {
                // GetData wraps round the end of the looping clip by itself.
                clip.GetData(block, readPosition);
                readPosition = (readPosition + BlockSize) % samples;
                available -= BlockSize;
                double sum = 0;
                for (int i = 0; i < BlockSize; i++)
                {
                    sum += block[i] * block[i];
                }
                float level = Mathf.Sqrt((float)(sum / BlockSize));
                // This block ended "available" samples before now.
                heard |= detector.Update(now - (double)available / rate, level);
            }
            if (heard)
            {
                Call();
            }
        }

        private bool GameIsLoud()
        {
            if (AudioListener.pause)
            {
                return false;
            }
            AudioListener.GetOutputData(output, 0);
            float peak = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                peak = Mathf.Max(peak, Mathf.Abs(output[i]));
            }
            return peak >= gameSoundLevel;
        }

        /// <summary>"Look!": a sound at the point the player is looking at, on the table or among the crowd.</summary>
        private void Call()
        {
            Crowd crowd = access != null ? access.Crowd : null;
            TableCalibrator table = access != null ? access.Table : null;
            if (crowd == null || crowd.Model == null || crowd.Spectators.Count == 0 || table == null || !table.Calibrated)
            {
                return;
            }
            if (Time.timeScale <= 0f)
            {
                // Paused: the crowd is waiting, not listening.
                return;
            }
            // The crowd model already resolves the look ray onto a spectator's face, the table or a point ahead.
            Vector3 point = crowd.Model.GazePoint.ToUnity();
            SoundCaptions.NextSoundText = "Look!";
            crowd.MakeSound(point, strength);
            SoundCaptions.NextSoundText = null;
            Debug.Log("[Misdirection] Voice: \"Look!\" heard; the crowd's eyes go to " + point.ToString("F2") + ".");
        }
    }
}
