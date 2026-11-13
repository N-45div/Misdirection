using System;
using System.Collections.Generic;
using System.Globalization;
using Misdirection.Perf;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Day 12 (performance on Quest 3 / 3S): everything the build needs that can't be measured without
    /// a headset, set by code so it is the same on every machine. Every method is safe to run again
    /// and logs each value it sets or finds already set.
    /// </summary>
    public static class PerformanceSetup
    {
        public const string ResourcesFolder = "Assets/Misdirection/Resources/Misdirection";
        public const string LitTemplatePath = ResourcesFolder + "/SharedLit.mat";
        public const string OpenXrSettingsPath = "Assets/XR/Settings/OpenXR Package Settings.asset";
        public const string PerformanceObjectName = "Misdirection Performance";

        // What the Android URP asset should say for Quest 3 / 3S (see docs/days/day-12.md).
        private const int Msaa = 4;
        private const float RenderScale = 1f;
        // OpenXRSettings.RenderMode.SinglePassInstanced: one draw call for both eyes (multiview on Quest).
        private const int SinglePassInstanced = 1;

        [MenuItem("Misdirection/Day 12/Apply Quest 3 Render Settings (URP, multiview)", priority = 1201)]
        public static void EnsureQuestRenderSettingsMenu()
        {
            EnsureQuestRenderSettings();
        }

        [MenuItem("Misdirection/Day 12/Add Headset Performance Settings and Perf Log to Open Scene", priority = 1202)]
        public static void EnsureScenePerformanceMenu()
        {
            EnsureScenePerformance(true);
        }

        /// <summary>
        /// Project-wide render settings for Quest 3 / 3S, on every URP asset an Android quality level uses:
        /// MSAA 4x, HDR off, render scale 1, no main-light shadow maps (the scene's light casts none and
        /// spectators opt out), no additional lights, SRP Batcher on; and single-pass (multiview) stereo
        /// for the Android OpenXR loader. Meta's Fix All already sets MSAA, HDR and dynamic resolution;
        /// this agrees with it and fills in the rest. Returns true if anything changed.
        /// </summary>
        public static bool EnsureQuestRenderSettings()
        {
            bool changed = false;
            List<RenderPipelineAsset> pipelines = new List<RenderPipelineAsset>();
            QualitySettings.GetAllRenderPipelineAssetsForPlatform("Android", ref pipelines);
            int seen = 0;
            foreach (RenderPipelineAsset pipeline in pipelines)
            {
                if (pipeline == null)
                {
                    continue;
                }
                seen++;
                SerializedObject so = new SerializedObject(pipeline);
                bool assetChanged = false;
                assetChanged |= SetInt(so, "m_MSAA", Msaa);
                assetChanged |= SetBool(so, "m_SupportsHDR", false);
                assetChanged |= SetFloat(so, "m_RenderScale", RenderScale);
                assetChanged |= SetBool(so, "m_MainLightShadowsSupported", false);
                // 0 = LightRenderingMode.Disabled: only the one directional light is used.
                assetChanged |= SetInt(so, "m_AdditionalLightsRenderingMode", 0);
                assetChanged |= SetBool(so, "m_AdditionalLightShadowsSupported", false);
                assetChanged |= SetBool(so, "m_UseSRPBatcher", true);
                if (assetChanged)
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(pipeline);
                    changed = true;
                }
            }
            if (seen == 0)
            {
                Debug.LogWarning("[Misdirection] Performance: no URP asset on any Android quality level; render settings not applied.");
            }
            changed |= EnsureMultiview();
            if (changed)
            {
                AssetDatabase.SaveAssets();
            }
            return changed;
        }

        /// <summary>Android OpenXR settings: both eyes in one pass. Meta's Fix All asks for this too.</summary>
        private static bool EnsureMultiview()
        {
            bool found = false;
            bool changed = false;
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(OpenXrSettingsPath))
            {
                // The per-platform OpenXRSettings sub-asset is named after its build target group.
                if (asset == null || asset.name != "Android")
                {
                    continue;
                }
                SerializedObject so = new SerializedObject(asset);
                if (so.FindProperty("m_renderMode") == null)
                {
                    continue;
                }
                found = true;
                if (SetInt(so, "m_renderMode", SinglePassInstanced))
                {
                    so.ApplyModifiedPropertiesWithoutUndo();
                    EditorUtility.SetDirty(asset);
                    changed = true;
                }
            }
            if (!found)
            {
                Log("OpenXR Android settings not found at " + OpenXrSettingsPath + ": stereo mode left as it is");
            }
            return changed;
        }

        /// <summary>
        /// Adds one object to the open scene with <see cref="QuestPerformance"/> (foveation, CPU/GPU levels,
        /// 72 Hz, requested on the headset at start) and <see cref="PerfLogger"/> (frame times in
        /// development builds). Safe to run again. Returns true if the scene changed.
        /// </summary>
        public static bool EnsureScenePerformance(bool interactive)
        {
            QuestPerformance settings = UnityEngine.Object.FindFirstObjectByType<QuestPerformance>(FindObjectsInactive.Include);
            PerfLogger logger = UnityEngine.Object.FindFirstObjectByType<PerfLogger>(FindObjectsInactive.Include);
            if (settings != null && logger != null)
            {
                Log("scene '" + SceneManager.GetActiveScene().name + "': headset performance settings and perf log already there");
                return false;
            }
            GameObject host = settings != null ? settings.gameObject : logger != null ? logger.gameObject : null;
            if (host == null)
            {
                host = new GameObject(PerformanceObjectName);
                Undo.RegisterCreatedObjectUndo(host, "Add Performance Settings");
            }
            if (settings == null)
            {
                Undo.AddComponent<QuestPerformance>(host);
            }
            if (logger == null)
            {
                Undo.AddComponent<PerfLogger>(host);
            }
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Log("scene '" + SceneManager.GetActiveScene().name + "': added '" + PerformanceObjectName +
                "' (on the headset: foveation High (dynamic), CPU and GPU SustainedHigh, 72 Hz; perf log in development builds)");
            if (interactive)
            {
                Selection.activeGameObject = host;
            }
            return true;
        }

        [MenuItem("Misdirection/Day 12/Check Shaders That Ship (shared materials)", priority = 1200)]
        public static void EnsureShippedShadersMenu()
        {
            EnsureShippedShaders();
        }

        /// <summary>
        /// Makes sure the two shaders <see cref="SharedMaterials"/> uses are in every build: the opaque one
        /// through a template material in a Resources folder, the transparent one through the Always
        /// Included Shaders list. Returns true if anything changed.
        /// </summary>
        public static bool EnsureShippedShaders()
        {
            bool changed = EnsureLitTemplate();
            changed |= EnsureAlwaysIncluded(SharedMaterials.OverlayShaderName);
            return changed;
        }

        /// <summary>
        /// Saves the opaque template (a copy of URP's default Lit material, which is what primitives
        /// looked like in the Editor before Day 12) where <c>Resources.Load</c> finds it at runtime.
        /// </summary>
        private static bool EnsureLitTemplate()
        {
            Shader shader = Shader.Find(SharedMaterials.LitShaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("Shader '" + SharedMaterials.LitShaderName + "' not found. Is URP still installed?");
            }
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(LitTemplatePath);
            if (existing != null && existing.shader == shader)
            {
                Log("shared material template: already at " + LitTemplatePath + " (" + shader.name + ")");
                return false;
            }

            Material template = new Material(shader);
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            Material urpDefault = pipeline != null ? pipeline.defaultMaterial : null;
            if (urpDefault != null && urpDefault.shader == shader)
            {
                template.CopyPropertiesFromMaterial(urpDefault);
            }
            template.color = Color.white;
            // The SRP Batcher does the batching; GPU instancing would only add shader variants.
            template.enableInstancing = false;

            if (existing != null)
            {
                EditorUtility.CopySerialized(template, existing);
                UnityEngine.Object.DestroyImmediate(template);
                EditorUtility.SetDirty(existing);
            }
            else
            {
                EnsureFolder(ResourcesFolder);
                AssetDatabase.CreateAsset(template, LitTemplatePath);
            }
            AssetDatabase.SaveAssets();
            Log("shared material template: saved " + LitTemplatePath + " (" + shader.name +
                (urpDefault != null && urpDefault.shader == shader ? ", copied from URP's default material)" : ", shader defaults)"));
            return true;
        }

        /// <summary>Adds a built-in shader to Project Settings > Graphics > Always Included Shaders if it isn't there.</summary>
        private static bool EnsureAlwaysIncluded(string shaderName)
        {
            Shader shader = Shader.Find(shaderName);
            if (shader == null)
            {
                throw new InvalidOperationException("Shader '" + shaderName + "' not found.");
            }
            UnityEngine.Object[] settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
            if (settings == null || settings.Length == 0)
            {
                throw new InvalidOperationException("Could not open ProjectSettings/GraphicsSettings.asset.");
            }
            SerializedObject graphics = new SerializedObject(settings[0]);
            SerializedProperty list = graphics.FindProperty("m_AlwaysIncludedShaders");
            if (list == null)
            {
                throw new InvalidOperationException("GraphicsSettings has no m_AlwaysIncludedShaders. Has Unity changed?");
            }
            for (int i = 0; i < list.arraySize; i++)
            {
                if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader)
                {
                    Log("always-included shaders: '" + shaderName + "' already listed");
                    return false;
                }
            }
            list.arraySize++;
            list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
            graphics.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Log("always-included shaders: added '" + shaderName + "'");
            return true;
        }

        private static bool SetInt(SerializedObject so, string name, int value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("[Misdirection] Performance: " + so.targetObject.name + " has no " + name + " (has the package changed?)");
                return false;
            }
            if (property.intValue == value)
            {
                Log(so.targetObject.name + "." + name + ": already " + value);
                return false;
            }
            Log(so.targetObject.name + "." + name + ": " + property.intValue + " -> " + value);
            property.intValue = value;
            return true;
        }

        private static bool SetBool(SerializedObject so, string name, bool value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("[Misdirection] Performance: " + so.targetObject.name + " has no " + name + " (has the package changed?)");
                return false;
            }
            if (property.boolValue == value)
            {
                Log(so.targetObject.name + "." + name + ": already " + (value ? "on" : "off"));
                return false;
            }
            Log(so.targetObject.name + "." + name + ": " + (property.boolValue ? "on" : "off") + " -> " + (value ? "on" : "off"));
            property.boolValue = value;
            return true;
        }

        private static bool SetFloat(SerializedObject so, string name, float value)
        {
            SerializedProperty property = so.FindProperty(name);
            if (property == null)
            {
                Debug.LogWarning("[Misdirection] Performance: " + so.targetObject.name + " has no " + name + " (has the package changed?)");
                return false;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            if (Mathf.Approximately(property.floatValue, value))
            {
                Log(so.targetObject.name + "." + name + ": already " + value.ToString("0.##", inv));
                return false;
            }
            Log(so.targetObject.name + "." + name + ": " + property.floatValue.ToString("0.##", inv) + " -> " + value.ToString("0.##", inv));
            property.floatValue = value;
            return true;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }
            int slash = path.LastIndexOf('/');
            string parent = path.Substring(0, slash);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, path.Substring(slash + 1));
        }

        private static void Log(string message)
        {
            Debug.Log("[Misdirection] Performance: " + message);
        }
    }

    /// <summary>
    /// Any build, from the menu, a script or CI, checks the shaders first, so a build made without
    /// running the bootstrap can't ship invisible spectators; Android builds also get the Quest
    /// render settings.
    /// </summary>
    internal sealed class PerformanceCheckBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -100; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            PerformanceSetup.EnsureShippedShaders();
            if (report.summary.platform == BuildTarget.Android)
            {
                PerformanceSetup.EnsureQuestRenderSettings();
            }
        }
    }
}
