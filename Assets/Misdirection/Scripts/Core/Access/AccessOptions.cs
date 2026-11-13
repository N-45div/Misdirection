using System;

namespace Misdirection.Core.Access
{
    /// <summary>How the one-handed accommodations are switched.</summary>
    public enum OneHandMode
    {
        /// <summary>Switch on by themselves when only one hand has been seen for a while (the default).</summary>
        Auto,
        On,
        Off,
    }

    /// <summary>The five accessibility options, in the order they sit on the Access Tray.</summary>
    public enum AccessToggle { OneHand, Relaxed, Captions, HighContrast, Voice }

    /// <summary>
    /// The player's accessibility choices (Day 11). Inclusive by default: one-handed play switches
    /// itself on and captions start on; voice starts off so the app never asks for the microphone
    /// unless the player turns it on. Saved as one short string. Pure data.
    /// </summary>
    [Serializable]
    public sealed class AccessOptions
    {
        public const int ToggleCount = 5;

        public OneHandMode OneHand = OneHandMode.Auto;
        /// <summary>Longer windows: shorter holds, longer misdirection, a slower and more forgiving crowd.</summary>
        public bool Relaxed;
        /// <summary>Captions for sounds and the mentor's hints.</summary>
        public bool Captions = true;
        public bool HighContrast;
        /// <summary>A short loud sound ("Look!", a clap) draws the crowd's eyes to where the player looks.</summary>
        public bool Voice;

        public AccessOptions Clone()
        {
            AccessOptions o = new AccessOptions();
            o.OneHand = OneHand;
            o.Relaxed = Relaxed;
            o.Captions = Captions;
            o.HighContrast = HighContrast;
            o.Voice = Voice;
            return o;
        }

        /// <summary>Flip one option: auto → on → off → auto for one-handed, on ↔ off for the rest.</summary>
        public void Toggle(AccessToggle toggle)
        {
            switch (toggle)
            {
                case AccessToggle.OneHand:
                    OneHand = OneHand == OneHandMode.Auto ? OneHandMode.On : OneHand == OneHandMode.On ? OneHandMode.Off : OneHandMode.Auto;
                    break;
                case AccessToggle.Relaxed: Relaxed = !Relaxed; break;
                case AccessToggle.Captions: Captions = !Captions; break;
                case AccessToggle.HighContrast: HighContrast = !HighContrast; break;
                case AccessToggle.Voice: Voice = !Voice; break;
            }
        }

        /// <summary>True when the option is doing something (for one-handed: not switched off).</summary>
        public bool IsOn(AccessToggle toggle)
        {
            switch (toggle)
            {
                case AccessToggle.OneHand: return OneHand != OneHandMode.Off;
                case AccessToggle.Relaxed: return Relaxed;
                case AccessToggle.Captions: return Captions;
                case AccessToggle.HighContrast: return HighContrast;
                default: return Voice;
            }
        }

        /// <summary>The tray label: one or two words.</summary>
        public static string Label(AccessToggle toggle)
        {
            switch (toggle)
            {
                case AccessToggle.OneHand: return "One hand";
                case AccessToggle.Relaxed: return "Relaxed";
                case AccessToggle.Captions: return "Captions";
                case AccessToggle.HighContrast: return "High contrast";
                default: return "Voice";
            }
        }

        /// <summary>The option's state as a word: "auto", "on" or "off".</summary>
        public string StateWord(AccessToggle toggle)
        {
            if (toggle == AccessToggle.OneHand)
            {
                return OneHand == OneHandMode.Auto ? "auto" : OneHand == OneHandMode.On ? "on" : "off";
            }
            return IsOn(toggle) ? "on" : "off";
        }

        /// <summary>Compact form for saving and logs, e.g. "one=auto;relaxed=0;captions=1;contrast=0;voice=0".</summary>
        public string Encode()
        {
            return "one=" + StateWord(AccessToggle.OneHand)
                + ";relaxed=" + (Relaxed ? "1" : "0")
                + ";captions=" + (Captions ? "1" : "0")
                + ";contrast=" + (HighContrast ? "1" : "0")
                + ";voice=" + (Voice ? "1" : "0");
        }

        /// <summary>Reads <see cref="Encode"/>'s form. Anything missing or unreadable keeps its default.</summary>
        public static AccessOptions Decode(string text)
        {
            AccessOptions o = new AccessOptions();
            if (string.IsNullOrEmpty(text))
            {
                return o;
            }
            string[] parts = text.Split(new char[] { ';' });
            for (int i = 0; i < parts.Length; i++)
            {
                int eq = parts[i].IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }
                string key = parts[i].Substring(0, eq).Trim();
                string value = parts[i].Substring(eq + 1).Trim();
                switch (key)
                {
                    case "one":
                        if (value == "on") { o.OneHand = OneHandMode.On; }
                        else if (value == "off") { o.OneHand = OneHandMode.Off; }
                        else if (value == "auto") { o.OneHand = OneHandMode.Auto; }
                        break;
                    case "relaxed": o.Relaxed = Flag(value, o.Relaxed); break;
                    case "captions": o.Captions = Flag(value, o.Captions); break;
                    case "contrast": o.HighContrast = Flag(value, o.HighContrast); break;
                    case "voice": o.Voice = Flag(value, o.Voice); break;
                }
            }
            return o;
        }

        private static bool Flag(string value, bool fallback)
        {
            if (value == "1")
            {
                return true;
            }
            return value == "0" ? false : fallback;
        }
    }
}
