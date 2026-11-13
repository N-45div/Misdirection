using System;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

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
    /// running the bootstrap can't ship invisible spectators.
    /// </summary>
    internal sealed class ShaderCheckBeforeBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return -100; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            PerformanceSetup.EnsureShippedShaders();
        }
    }
}
