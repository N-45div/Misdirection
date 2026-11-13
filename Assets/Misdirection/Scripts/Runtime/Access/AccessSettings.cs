using System.Collections.Generic;
using Misdirection.Core.Access;
using Misdirection.Core.Tricks;
using UnityEngine;

namespace Misdirection.Access
{
    /// <summary>
    /// The accessibility state every other component may read (Day 11): the player's options, whether
    /// one-handed play is active and with which hand, and the few numbers high contrast and relaxed
    /// timing change. <see cref="AccessRuntime"/> writes it; hands, table, crowd, spectators and the
    /// ghost hands read it through one-line hooks, so they need no reference to anything here.
    /// With no AccessRuntime in the scene everything keeps its normal value.
    /// </summary>
    public static class AccessSettings
    {
        private static AccessOptions options = new AccessOptions();
        private static float suspicionScale = 1f;
        private static readonly Dictionary<Color32, Material> Materials = new Dictionary<Color32, Material>();

        public static AccessOptions Options { get { return options; } }

        /// <summary>One-handed accommodations apply right now (auto-detected or switched on).</summary>
        public static bool OneHanded { get; private set; }

        /// <summary>The hand a one-handed player is using (None until one has been seen).</summary>
        public static HandId ActiveHand { get; private set; }

        /// <summary>True when ghost hands and prompts should use the left hand.</summary>
        public static bool LeftHanded { get { return OneHanded && ActiveHand == HandId.Left; } }

        public static bool HighContrast { get { return options.HighContrast; } }
        public static bool Captions { get { return options.Captions; } }
        public static bool Relaxed { get { return options.Relaxed; } }

        /// <summary>Multiplier on suspicion the crowd gains from what it sees (1 unless relaxed timing is on).</summary>
        public static float SuspicionScale { get { return suspicionScale; } }

        // ------------------------------------------------------------ one-handed wording

        public static string PalmsCaption
        {
            get { return OneHanded ? "Rest one palm flat on the table" : "Rest both palms flat on the table"; }
        }

        public static string HandsBackCaption
        {
            get { return OneHanded ? "Show your hand to carry on" : "Show both hands to carry on"; }
        }

        // ------------------------------------------------------------ high contrast

        /// <summary>Magician's Eye cones are drawn this much wider (it then also covers the cone's soft edge).</summary>
        public static float ConeWidthScale { get { return HighContrast ? 1.25f : 1f; } }

        /// <summary>The cone's colour: the same hue, much more opaque in high contrast.</summary>
        public static Color ConeColor(Color color)
        {
            if (!HighContrast)
            {
                return color;
            }
            color.a = Mathf.Clamp(color.a * 3f, 0.3f, 0.8f);
            return color;
        }

        /// <summary>Scale of the suspicion markers (? ?! !!) over spectators' heads.</summary>
        public static float MarkerScale { get { return HighContrast ? 1.7f : 1f; } }

        /// <summary>Scale of sound captions and tray labels.</summary>
        public static float TextScale { get { return HighContrast ? 1.3f : 1f; } }

        /// <summary>The servante outline in high contrast: bold and bright yellow.</summary>
        public static readonly Color BoldOutlineColor = new Color(1f, 0.92f, 0.1f, 1f);
        public const float BoldOutlineWidth = 0.008f;

        // ------------------------------------------------------------ shared materials

        /// <summary>One unlit material per colour, shared by everything the accessibility layer draws.</summary>
        public static Material Shared(Color color)
        {
            Color32 key = color;
            Material material;
            if (!Materials.TryGetValue(key, out material) || material == null)
            {
                material = new Material(Shader.Find("Sprites/Default"));
                material.name = "Access " + ColorUtility.ToHtmlStringRGBA(color);
                material.color = color;
                Materials[key] = material;
            }
            return material;
        }

        // ------------------------------------------------------------ written by AccessRuntime

        public static void Publish(AccessOptions current, bool oneHanded, HandId activeHand, float suspicion)
        {
            options = current ?? new AccessOptions();
            OneHanded = oneHanded;
            ActiveHand = activeHand;
            suspicionScale = Mathf.Clamp01(suspicion);
        }

        /// <summary>Back to "no accessibility layer" (when the AccessRuntime goes away, and on every Play in the Editor).</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        public static void Clear()
        {
            options = new AccessOptions();
            OneHanded = false;
            ActiveHand = HandId.None;
            suspicionScale = 1f;
            Materials.Clear();
        }
    }
}
