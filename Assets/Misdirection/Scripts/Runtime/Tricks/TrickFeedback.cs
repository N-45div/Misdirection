using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Misdirection.Audience;
using Misdirection.Core.Tricks;
using Misdirection.Spike;
using Misdirection.Table;
using TMPro;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection.Tricks
{
    /// <summary>
    /// What every trick shows and plays: the HUD prompt, floating crowd reactions ("Applause! +15 tips",
    /// "I saw that!"), placeholder sounds, and a per-event CSV for playtests. Plain class owned by a
    /// trick director (MonoBehaviour); call <see cref="Update"/> from its LateUpdate.
    /// </summary>
    public sealed class TrickFeedback : IDisposable
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        public const string FormatTag = "misdirection_show_events v1";
        public const string CsvHeader = "t,trick,phase,event,kind,amazement,tips,reaction,seen_by,spectator";

        private readonly TextMeshPro hud;
        private readonly TextMeshPro reaction;
        private readonly AudioSource audio;
        private readonly string trickName;
        private double reactionUntil;
        private double reactionStart;
        private Vector3 reactionAnchor;
        private SpikeCsvLog log;
        private bool writeCsv;

        public readonly AudioClip Pickup = Tones.Beep("pickup", 1046f, 0.06f, 0.3f);
        public readonly AudioClip Hide = Tones.Thud("hide", 220f, 90f, 0.18f, 0.4f);
        public readonly AudioClip Clink = Tones.Beep("clink", 1568f, 0.18f, 0.45f);
        public readonly AudioClip Applause = Tones.Noise("applause", 2.2f, 0.15f, 0.45f, 1f, 11);
        public readonly AudioClip Gasp = Tones.Noise("gasp", 0.6f, 0.08f, 0.35f, 0f, 3);
        public readonly AudioClip Heckle = Tones.Thud("heckle", 160f, 120f, 0.5f, 0.5f);
        public readonly AudioClip Sparkle = Tones.Chirp("sparkle", 900f, 2400f, 0.3f);

        public TrickFeedback(GameObject owner, string trickName, bool writeCsv = true)
        {
            this.trickName = trickName;
            this.writeCsv = writeCsv;
            audio = owner.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            hud = WorldText.Create(owner.transform, trickName + " HUD", new Vector2(0.6f, 0.22f), 0.4f);
            reaction = WorldText.Create(owner.transform, trickName + " Reaction", new Vector2(0.5f, 0.15f), 0.6f);
            reaction.gameObject.SetActive(false);
        }

        public void Play(AudioClip clip)
        {
            if (audio != null && clip != null)
            {
                audio.PlayOneShot(clip);
            }
        }

        public void SetHud(string text)
        {
            if (hud.text != text)
            {
                hud.text = text;
            }
        }

        /// <summary>The prompt with tips, the last reaction and (if any) the crowd's state.</summary>
        public void SetPrompt(string prompt, int tips, RevealResult last, Crowd crowd)
        {
            string crowdLine = crowd != null && crowd.Spectators.Count > 0 ? "\n<size=55%>" + crowd.Summary() + "</size>" : "";
            string lastLine = last.Kind != null ? "   Last: " + last.Reaction + " (" + Mathf.RoundToInt(last.Amazement * 100f) + "%)" : "";
            SetHud("<b>" + prompt + "</b>\n<size=70%>Tips: " + tips + lastLine + "</size>" + crowdLine);
        }

        public void SetVisible(bool visible)
        {
            hud.gameObject.SetActive(visible);
            if (!visible)
            {
                reaction.gameObject.SetActive(false);
            }
        }

        /// <summary>Applause, gasp or heckle for a reveal, floating above whoever it concerns.</summary>
        public void React(RevealResult r, Crowd crowd, TableCalibrator table, double now)
        {
            bool spatial = crowd != null && crowd.Spectators.Count > 0;
            if (spatial)
            {
                // Each spectator claps, gasps or heckles from where they stand.
                crowd.React(r.Reaction, r.SeenBy);
            }
            if (!spatial || (r.Reaction == Reaction.Heckle && r.SeenBy < 0))
            {
                switch (r.Reaction)
                {
                    case Reaction.Applause: Play(Applause); break;
                    case Reaction.Gasp: Play(Gasp); break;
                    default: Play(Heckle); break;
                }
            }
            string line;
            if (r.Reaction == Reaction.Heckle)
            {
                line = r.SeenBy >= 0 ? "<color=#FF7043>\"I saw that!\"</color>" : "<color=#FFB74D>\"Hm.\"</color>";
            }
            else
            {
                line = (r.Reaction == Reaction.Applause ? "<color=#7CFC00>Applause!</color>" : "<color=#FFD54F>\"Ooh!\"</color>") +
                       "\n<size=70%>+" + r.Tips + " tips</size>";
            }
            ShowReaction(line, r.SeenBy >= 0 ? r.SeenBy : r.Spectator, crowd, table, now);
        }

        public void ShowReaction(string text, int spectator, Crowd crowd, TableCalibrator table, double now)
        {
            reaction.text = text;
            reactionStart = now;
            reactionUntil = now + 2.5;
            reactionAnchor = crowd != null && spectator >= 0 && spectator < crowd.Spectators.Count
                ? crowd.Spectators[spectator].AboveHead
                : table.Frame.ToWorld(new NVector3(0f, 0.45f, 0.9f)).ToUnity();
        }

        /// <summary>Keeps the HUD across the table and the reaction floating up, both facing the player.</summary>
        public void Update(double now, Vector3 viewer, Vector3 viewerForward, TableCalibrator table)
        {
            Vector3 pos;
            if (table != null && table.Calibrated)
            {
                pos = table.Frame.ToWorld(new NVector3(0f, 0.16f, 0.62f)).ToUnity();
            }
            else
            {
                Vector3 fwd = viewerForward;
                fwd.y = 0f;
                pos = viewer + (fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward) * 0.8f - Vector3.up * 0.15f;
            }
            hud.transform.position = pos;
            WorldText.Face(hud.transform, viewer);

            bool visible = now < reactionUntil;
            reaction.gameObject.SetActive(visible);
            if (visible)
            {
                reaction.transform.position = reactionAnchor + Vector3.up * (float)(now - reactionStart) * 0.04f;
                WorldText.Face(reaction.transform, viewer);
            }
        }

        /// <summary>One CSV row per show event (pickups, hides, reveals) under persistentDataPath/show.</summary>
        public void Log(double now, string phase, string evt, string detail, RevealResult? r, Func<int, string> nameOf)
        {
            if (!writeCsv)
            {
                return;
            }
            if (log == null && !OpenLog())
            {
                return;
            }
            string kind = r.HasValue ? r.Value.Kind : detail;
            log.WriteLine(string.Join(",", new[]
            {
                now.ToString("F3", Inv),
                trickName,
                phase,
                evt,
                (kind ?? "").Replace(',', ' '),
                r.HasValue ? r.Value.Amazement.ToString("F3", Inv) : "",
                r.HasValue ? r.Value.Tips.ToString(Inv) : "",
                r.HasValue ? r.Value.Reaction.ToString() : "",
                r.HasValue && r.Value.SeenBy >= 0 ? nameOf(r.Value.SeenBy) : "",
                r.HasValue && r.Value.Spectator >= 0 ? nameOf(r.Value.Spectator) : "",
            }));
            log.Flush();
        }

        private bool OpenLog()
        {
            string headset = OVRPlugin.GetSystemHeadsetType().ToString();
            string file = Path.Combine(Application.persistentDataPath, "show",
                "show_" + headset + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss", Inv) + "_" + trickName + ".csv");
            try
            {
                log = new SpikeCsvLog(file, new List<KeyValuePair<string, string>>
                {
                    new KeyValuePair<string, string>("headset", headset),
                    new KeyValuePair<string, string>("app_version", Application.version),
                    new KeyValuePair<string, string>("trick", trickName),
                }, CsvHeader, FormatTag);
                return true;
            }
            catch (Exception e)
            {
                writeCsv = false;
                Debug.LogError("[Misdirection] Could not open show log: " + e.Message);
                return false;
            }
        }

        public void Dispose()
        {
            if (log != null)
            {
                log.Dispose();
                log = null;
            }
        }
    }
}
