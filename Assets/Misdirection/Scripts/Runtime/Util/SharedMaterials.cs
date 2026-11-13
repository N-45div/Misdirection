using System.Collections.Generic;
using UnityEngine;

namespace Misdirection
{
    /// <summary>
    /// One material per colour, shared by everything the game builds from primitives at runtime
    /// (spectators, coin, cup, mat, stars, ghost hands, cones, outlines). Before Day 12 every part did
    /// <c>renderer.material.color = ...</c>, which makes a private material per renderer: about a
    /// hundred materials for a five-person crowd, each its own draw-call state.
    ///
    /// Two shaders, both guaranteed to be in the Android build:
    ///
    ///  * <see cref="Lit"/>: opaque, "Universal Render Pipeline/Lit". SRP Batcher compatible, so every
    ///    opaque part batches whatever its colour. It ships because the bootstrap (and a build
    ///    pre-process step) saves a template material under a Resources folder
    ///    (<see cref="LitTemplateResource"/>); Unity includes Resources assets, their shader and the
    ///    variants they use in every build. It is a copy of URP's default Lit material, which is what
    ///    the Editor gave primitives before, so the look is the same.
    ///  * <see cref="Overlay()"/>: transparent, unlit, vertex colours: the built-in "Sprites/Default".
    ///    It is in Project Settings > Graphics > Always Included Shaders (checked by the bootstrap), so
    ///    <c>Shader.Find</c> finds it on the headset. It is not SRP Batcher compatible, which is why
    ///    per-object colours on it use a MaterialPropertyBlock (<see cref="Tint"/>) and one material.
    ///
    /// Why not the primitive's own default material: in a player build URP has no default material,
    /// so <c>GameObject.CreatePrimitive</c> falls back to the built-in pipeline's Standard material,
    /// which URP does not draw (magenta in development builds, nothing in release builds).
    /// </summary>
    public static class SharedMaterials
    {
        /// <summary>Resources path of the opaque template (Assets/Misdirection/Resources/Misdirection/SharedLit.mat).</summary>
        public const string LitTemplateResource = "Misdirection/SharedLit";
        public const string LitShaderName = "Universal Render Pipeline/Lit";
        public const string OverlayShaderName = "Sprites/Default";

        private static readonly Dictionary<int, Material> LitByColor = new Dictionary<int, Material>();
        private static readonly Dictionary<int, Material> OverlayByColor = new Dictionary<int, Material>();
        private static readonly int ColorId = Shader.PropertyToID("_Color");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        // GameObject.CreatePrimitive needs these component types in the player. Engine-code stripping is
        // on for Android, and Unity's documentation says to reference them from a script so they stay.
        private static readonly System.Type[] PrimitiveParts =
        {
            typeof(MeshFilter), typeof(MeshRenderer), typeof(BoxCollider), typeof(SphereCollider),
            typeof(CapsuleCollider), typeof(MeshCollider),
        };

        private static Material litTemplate;
        private static Material overlayTemplate;
        private static string litSource = "not used yet";
        private static MaterialPropertyBlock block;

        /// <summary>Where the opaque shader came from: the Resources template, Shader.Find, or the unlit fallback.</summary>
        public static string LitSource { get { return litSource; } }

        /// <summary>How many materials exist (opaque + transparent), for the smoke report.</summary>
        public static int Count { get { return LitByColor.Count + OverlayByColor.Count; } }

        /// <summary>The shared opaque material for <paramref name="color"/>. Never change it: assign it to <c>sharedMaterial</c>.</summary>
        public static Material Lit(Color color)
        {
            int key = Key(color);
            Material material;
            if (LitByColor.TryGetValue(key, out material) && material != null)
            {
                return material;
            }
            Material template = LitTemplate();
            if (template == null)
            {
                return null;
            }
            material = new Material(template);
            material.name = "Shared Lit " + ColorUtility.ToHtmlStringRGBA(color);
            material.color = color;
            LitByColor[key] = material;
            return material;
        }

        /// <summary>
        /// The shared transparent material (white): colour it per object with vertex colours (a
        /// LineRenderer's start/end colour) or with <see cref="Tint"/>.
        /// </summary>
        public static Material Overlay()
        {
            return Overlay(Color.white);
        }

        /// <summary>The shared transparent material for <paramref name="color"/> (alpha = opacity).</summary>
        public static Material Overlay(Color color)
        {
            int key = Key(color);
            Material material;
            if (OverlayByColor.TryGetValue(key, out material) && material != null)
            {
                return material;
            }
            Material template = OverlayTemplate();
            if (template == null)
            {
                return null;
            }
            material = new Material(template);
            material.name = "Shared Overlay " + ColorUtility.ToHtmlStringRGBA(color);
            material.color = color;
            OverlayByColor[key] = material;
            return material;
        }

        /// <summary>
        /// Colours one renderer without giving it its own material (a MaterialPropertyBlock). For colours
        /// that change while playing: cones, state dots. Call it only when the colour changes.
        /// </summary>
        public static void Tint(Renderer renderer, Color color)
        {
            if (renderer == null)
            {
                return;
            }
            if (block == null)
            {
                block = new MaterialPropertyBlock();
            }
            // Sprites/Default reads _Color, URP shaders read _BaseColor.
            block.SetColor(ColorId, color);
            block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(block);
        }

        /// <summary>One line for logs and the smoke report.</summary>
        public static string Describe()
        {
            return LitByColor.Count + " opaque (" + litSource + "), " + OverlayByColor.Count + " transparent (" + OverlayShaderName + ")";
        }

        private static Material LitTemplate()
        {
            if (litTemplate != null)
            {
                return litTemplate;
            }
            Material saved = Resources.Load<Material>(LitTemplateResource);
            if (saved != null && saved.shader != null && saved.shader.isSupported)
            {
                litTemplate = saved;
                litSource = "Resources/" + LitTemplateResource;
                return litTemplate;
            }
            Shader shader = Shader.Find(LitShaderName);
            if (shader != null && shader.isSupported)
            {
                litTemplate = new Material(shader);
                litTemplate.name = "Shared Lit Template";
                litSource = "Shader.Find";
                if (!Application.isEditor)
                {
                    Debug.LogWarning("[Misdirection] Shared materials: no template at Resources/" + LitTemplateResource +
                                     "; found the shader by name. Run Misdirection > Setup > Run Project Bootstrap before building.");
                }
                return litTemplate;
            }
            // Last resort so nothing turns invisible: flat, unlit colour.
            litTemplate = OverlayTemplate();
            litSource = "fallback " + OverlayShaderName;
            Debug.LogWarning("[Misdirection] Shared materials: '" + LitShaderName + "' is not in this build; spectators and props " +
                             "are drawn unlit. Run Misdirection > Setup > Run Project Bootstrap, then rebuild.");
            return litTemplate;
        }

        private static Material OverlayTemplate()
        {
            if (overlayTemplate != null)
            {
                return overlayTemplate;
            }
            Shader shader = Shader.Find(OverlayShaderName);
            if (shader == null)
            {
                Debug.LogError("[Misdirection] Shared materials: '" + OverlayShaderName + "' is missing. Add it to Project Settings > " +
                               "Graphics > Always Included Shaders (the bootstrap does).");
                return null;
            }
            overlayTemplate = new Material(shader);
            overlayTemplate.name = "Shared Overlay Template";
            return overlayTemplate;
        }

        /// <summary>Colours that differ by less than one 8-bit step share a material.</summary>
        private static int Key(Color color)
        {
            Color32 c = color;
            return (c.r << 24) | (c.g << 16) | (c.b << 8) | c.a;
        }

        /// <summary>Play mode can restart without reloading scripts: forget materials from the last run.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LitByColor.Clear();
            OverlayByColor.Clear();
            litTemplate = null;
            overlayTemplate = null;
            litSource = "not used yet";
            block = null;
            // Touch the list so the compiler keeps it (see PrimitiveParts).
            if (PrimitiveParts.Length == 0)
            {
                Debug.LogWarning("[Misdirection] Shared materials: primitive part list is empty.");
            }
        }
    }
}
