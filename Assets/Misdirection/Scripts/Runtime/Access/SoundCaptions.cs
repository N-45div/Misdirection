using System.Collections.Generic;
using Misdirection.Audience;
using Misdirection.Core.Crowd;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;
using Misdirection.Show;
using Misdirection.Table;
using TMPro;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection.Access
{
    /// <summary>
    /// Captions for sound (Day 11): every sound that tells the player something gets a short caption
    /// where it comes from. Each spectator's "[applause]", "[gasp]" or "Hm!" appears in front of them,
    /// "[murmur]" when their suspicion rises, "[clink]" where a coin drops, and the show's own cues
    /// ("[chime]", "[fanfare]", "[the crowd walks off]") over the table. On by default; the Captions
    /// token on the Access Tray switches them off. The mentor's text hints are MentorHands' own.
    /// </summary>
    [DefaultExecutionOrder(36)]
    public sealed class SoundCaptions : MonoBehaviour
    {
        [SerializeField] private int poolSize = 8;
        [Tooltip("Captions are sized for this distance and scaled so they look the same size nearer or further (metres).")]
        [SerializeField] private float designDistance = 1.5f;

        private sealed class Line
        {
            public TextMeshPro Text;
            public Vector3 Anchor;
            public double Start;
            public double Until;
        }

        private AccessRuntime access;
        private readonly List<Line> lines = new List<Line>();
        private Material outlined;

        private readonly List<Spectator> known = new List<Spectator>();
        private readonly List<SuspicionLevel> levels = new List<SuspicionLevel>();
        private ShowStage lastStage = (ShowStage)(-1);
        private int lastAct = -1;
        private bool showSeen;
        private MentorDemo lastDemo = MentorDemo.None;

        public static SoundCaptions Current { get; private set; }

        /// <summary>
        /// Set just before calling Crowd.MakeSound to caption that sound as something other than a coin's
        /// clink (the voice "Look!" does). Used once, then cleared.
        /// </summary>
        public static string NextSoundText { get; set; }

        /// <summary>The last caption (shown or not) and how many there have been: for logs and tests.</summary>
        public string Last { get; private set; }
        public int Count { get; private set; }

        // ------------------------------------------------------------ hooks (called from Crowd)

        /// <summary>The crowd reacted to a reveal or a bust: caption each spectator's sound.</summary>
        public static void CrowdReacted(Crowd crowd, Reaction reaction, int seenBy)
        {
            if (Current != null && crowd != null)
            {
                Current.OnCrowdReacted(crowd, reaction, seenBy);
            }
        }

        /// <summary>A sound drew the crowd's eyes to <paramref name="point"/> (a coin's clink, a call).</summary>
        public static void SoundMade(Vector3 point)
        {
            string text = NextSoundText ?? "[clink]";
            NextSoundText = null;
            if (Current != null)
            {
                Current.Say(text, point + Vector3.up * 0.06f, 1.6f, text[0] != '[');
            }
        }

        /// <summary>
        /// Show a caption at a point in the world. <paramref name="always"/> shows it even with captions off
        /// (for things that are not sounds: the voice's "Look!" confirmation, a microphone problem).
        /// </summary>
        public void Say(string text, Vector3 world, float seconds = 2.2f, bool always = false)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }
            Last = text;
            Count++;
            if (!always && !AccessSettings.Captions)
            {
                return;
            }
            double now = Time.unscaledTimeAsDouble;
            Line line = null;
            for (int i = 0; i < lines.Count; i++)
            {
                // A free line, else the one that has been up longest.
                if (now >= lines[i].Until)
                {
                    line = lines[i];
                    break;
                }
                if (line == null || lines[i].Start < line.Start)
                {
                    line = lines[i];
                }
            }
            if (line == null)
            {
                return;
            }
            line.Text.text = text;
            line.Anchor = world;
            line.Start = now;
            line.Until = now + seconds;
        }

        // ------------------------------------------------------------ life cycle

        private void Awake()
        {
            Current = this;
            access = GetComponent<AccessRuntime>();
            for (int i = 0; i < Mathf.Max(1, poolSize); i++)
            {
                Line line = new Line();
                line.Text = WorldText.Create(transform, "Sound Caption " + (i + 1), new Vector2(0.6f, 0.1f), 0.3f);
                line.Text.text = "";
                Outline(line.Text);
                line.Text.gameObject.SetActive(false);
                lines.Add(line);
            }
        }

        private void OnDestroy()
        {
            if (Current == this)
            {
                Current = null;
            }
            if (outlined != null)
            {
                Destroy(outlined);
            }
        }

        private void LateUpdate()
        {
            if (access != null)
            {
                WatchSuspicion(access.Crowd);
                WatchShow(access.Show, access.Table);
                WatchMentor(access.Mentor, access.Table);
            }
            Draw();
        }

        // ------------------------------------------------------------ what makes a sound

        private void OnCrowdReacted(Crowd crowd, Reaction reaction, int seenBy)
        {
            IReadOnlyList<Spectator> spectators = crowd.Spectators;
            if (spectators.Count == 0)
            {
                return;
            }
            Vector3 viewer = Viewer();
            switch (reaction)
            {
                case Reaction.Applause:
                    for (int i = 0; i < spectators.Count; i++)
                    {
                        Say("[applause]", InFrontOf(spectators[i], viewer), 2.2f);
                    }
                    break;
                case Reaction.Gasp:
                    for (int i = 0; i < spectators.Count; i++)
                    {
                        Say("[gasp]", InFrontOf(spectators[i], viewer), 1.5f);
                    }
                    break;
                case Reaction.Heckle:
                    if (seenBy >= 0 && seenBy < spectators.Count)
                    {
                        // The one who saw it says so; the others only frown.
                        Say("Hm!", InFrontOf(spectators[seenBy], viewer), 1.8f);
                    }
                    else
                    {
                        Say("[groan]", InFrontOf(spectators[spectators.Count / 2], viewer), 1.8f);
                    }
                    break;
            }
        }

        /// <summary>Each spectator's murmur gets louder as their suspicion rises: caption the step up.</summary>
        private void WatchSuspicion(Crowd crowd)
        {
            if (crowd == null)
            {
                return;
            }
            IReadOnlyList<Spectator> spectators = crowd.Spectators;
            bool same = spectators.Count == known.Count;
            for (int i = 0; same && i < spectators.Count; i++)
            {
                same = spectators[i] == known[i];
            }
            if (!same)
            {
                // A new line-up: start from how they are now.
                known.Clear();
                levels.Clear();
                for (int i = 0; i < spectators.Count; i++)
                {
                    known.Add(spectators[i]);
                    levels.Add(spectators[i] != null && spectators[i].Mind != null ? spectators[i].Mind.Level : SuspicionLevel.Calm);
                }
                return;
            }
            for (int i = 0; i < spectators.Count; i++)
            {
                if (spectators[i] == null || spectators[i].Mind == null)
                {
                    continue;
                }
                SuspicionLevel level = spectators[i].Mind.Level;
                if (level > levels[i])
                {
                    string text = level == SuspicionLevel.Curious ? "[murmur]" : level == SuspicionLevel.Suspicious ? "[louder murmur]" : "[grumbling]";
                    Say(text, InFrontOf(spectators[i], Viewer()), 1.8f);
                }
                levels[i] = level;
            }
        }

        /// <summary>The show's own cues: the crowd arriving for an act, the bow's fanfare, the walk-off.</summary>
        private void WatchShow(ShowDirector show, TableCalibrator table)
        {
            if (show == null || show.Flow == null)
            {
                return;
            }
            ShowStage stage = show.Flow.Stage;
            int act = show.Flow.Act;
            if (showSeen && (stage != lastStage || act != lastAct) && table != null && table.Calibrated)
            {
                Vector3 overTable = table.Frame.ToWorld(new NVector3(0f, 0.2f, 0.7f)).ToUnity();
                switch (stage)
                {
                    case ShowStage.Performing: Say("[the crowd gathers]", overTable, 2f); break;
                    case ShowStage.Bow: Say("[fanfare]", overTable, 2.2f); break;
                    case ShowStage.WalkOff: Say("[the crowd walks off]", overTable, 3f); break;
                }
            }
            showSeen = true;
            lastStage = stage;
            lastAct = act;
        }

        /// <summary>The mentor chimes each time the ghost hands start showing something new.</summary>
        private void WatchMentor(MentorHands mentor, TableCalibrator table)
        {
            if (mentor == null)
            {
                return;
            }
            MentorDemo demo = mentor.Demo;
            if (demo != lastDemo && demo != MentorDemo.None)
            {
                Vector3 at;
                if (table != null && table.Calibrated)
                {
                    at = table.Frame.ToWorld(new NVector3(0.17f, 0.04f, 0.06f)).ToUnity();
                }
                else
                {
                    Vector3 viewer = Viewer();
                    Transform head = Head();
                    Vector3 forward = head != null ? head.forward : Vector3.forward;
                    forward.y = 0f;
                    at = viewer + (forward.sqrMagnitude > 1e-4f ? forward.normalized : Vector3.forward) * 0.45f - Vector3.up * 0.35f;
                }
                Say("[chime]", at, 1.2f);
            }
            lastDemo = demo;
        }

        // ------------------------------------------------------------ drawing

        private void Draw()
        {
            double now = Time.unscaledTimeAsDouble;
            Vector3 viewer = Viewer();
            float textScale = AccessSettings.TextScale;
            for (int i = 0; i < lines.Count; i++)
            {
                Line line = lines[i];
                bool visible = now < line.Until;
                if (line.Text.gameObject.activeSelf != visible)
                {
                    line.Text.gameObject.SetActive(visible);
                }
                if (!visible)
                {
                    continue;
                }
                // Drifts up a little so two captions from the same place don't sit exactly on each other.
                Vector3 position = line.Anchor + Vector3.up * (float)(now - line.Start) * 0.015f;
                line.Text.transform.position = position;
                float distance = Vector3.Distance(position, viewer);
                line.Text.transform.localScale = Vector3.one * Mathf.Clamp(distance / Mathf.Max(0.1f, designDistance), 0.35f, 1.5f) * textScale;
                WorldText.Face(line.Text.transform, viewer, false);
            }
        }

        /// <summary>In front of a spectator's chest, clear of the reaction text and suspicion marker above their head.</summary>
        private static Vector3 InFrontOf(Spectator spectator, Vector3 viewer)
        {
            if (spectator == null)
            {
                return viewer;
            }
            Vector3 eyes = spectator.EyesPosition;
            Vector3 toViewer = viewer - eyes;
            toViewer.y = 0f;
            Vector3 forward = toViewer.sqrMagnitude > 1e-4f ? toViewer.normalized : Vector3.zero;
            return eyes + Vector3.down * 0.22f + forward * 0.16f;
        }

        /// <summary>White text with a black edge reads against any room. One material for the whole pool.</summary>
        private void Outline(TextMeshPro text)
        {
            if (outlined == null && text.fontSharedMaterial != null)
            {
                outlined = new Material(text.fontSharedMaterial);
                outlined.name = "Access Caption Outline";
                outlined.EnableKeyword(ShaderUtilities.Keyword_Outline);
                outlined.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
                outlined.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            }
            if (outlined != null)
            {
                text.fontSharedMaterial = outlined;
            }
        }

        private Transform Head()
        {
            LookSource look = access != null ? access.Look : null;
            if (look != null && look.Head != null)
            {
                return look.Head;
            }
            return Camera.main != null ? Camera.main.transform : null;
        }

        private Vector3 Viewer()
        {
            Transform head = Head();
            return head != null ? head.position : Vector3.up * 1.2f;
        }
    }
}
