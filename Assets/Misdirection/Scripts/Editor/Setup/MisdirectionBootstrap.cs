using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Meta.XR.BuildingBlocks.Editor;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Configures the project and builds the Day 1 scenes with no clicks, so a fresh clone (or CI)
    /// can go straight to a Quest APK.
    ///
    ///   Unity -batchmode -projectPath . -buildTarget Android \
    ///         -executeMethod Misdirection.EditorTools.MisdirectionBootstrap.RunBatch
    ///
    /// Don't pass -quit: Meta's Building Blocks and Project Setup Tool finish on editor update ticks,
    /// and this exits by itself (code 0 on success). Safe to run again: existing scenes are kept.
    /// </summary>
    public static class MisdirectionBootstrap
    {
        public const string SpikeScenePath = "Assets/Misdirection/Scenes/Spike.unity";
        public const string MainScenePath = "Assets/Misdirection/Scenes/Main.unity";

        // Building Block IDs from Meta XR SDK v207 (BlockData assets). Camera Rig comes in as a dependency.
        private const string PassthroughBlock = "f0540b20-dfd6-420e-b20d-c270f88dc77e";
        private const string HandTrackingBlock = "8b26b298-7bf4-490e-b245-a039c0184303";
        private const string MrukBlock = "d4413f4f-542f-451f-92ee-4801f27d676a";

        private const double TimeoutSeconds = 900;

        private static IEnumerator<object> steps;
        private static Task waitingOn;
        private static bool batch;
        private static double deadline;

        [MenuItem("Misdirection/Setup/Run Project Bootstrap", priority = 1)]
        public static void RunFromMenu()
        {
            Begin(false);
        }

        public static void RunBatch()
        {
            Begin(true);
        }

        private static void Begin(bool isBatch)
        {
            if (steps != null)
            {
                Debug.LogWarning("[Misdirection] Bootstrap already running.");
                return;
            }
            batch = isBatch;
            deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
            steps = Steps();
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            try
            {
                if (EditorApplication.timeSinceStartup > deadline)
                {
                    Finish(false, "timed out after " + TimeoutSeconds + " s");
                    return;
                }
                if (waitingOn != null)
                {
                    if (!waitingOn.IsCompleted)
                    {
                        return;
                    }
                    Task done = waitingOn;
                    waitingOn = null;
                    if (done.IsFaulted && done.Exception != null)
                    {
                        throw done.Exception.GetBaseException();
                    }
                }
                if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                {
                    return;
                }
                if (!steps.MoveNext())
                {
                    Finish(true, "done");
                    return;
                }
                waitingOn = steps.Current as Task;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(false, e.Message);
            }
        }

        private static IEnumerator<object> Steps()
        {
            Log("1/7 Android build target");
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            {
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            }
            yield return null;

            Log("2/7 Meta project config: hands only, passthrough, anchors, scene, eye tracking");
            ConfigureMetaProject();
            yield return null;

            Log("3/7 TextMesh Pro essentials");
            if (!AssetDatabase.IsValidFolder("Assets/TextMesh Pro"))
            {
                TMP_PackageResourceImporter.ImportResources(true, false, false);
            }
            yield return null;

            Log("4/7 Spike scene");
            foreach (object step in BuildScene(SpikeScenePath, new[] { PassthroughBlock, HandTrackingBlock }, SceneContent.TrackingSpike))
            {
                yield return step;
            }

            Log("5/7 Main scene (Day 2 ditch practice)");
            foreach (object step in BuildScene(MainScenePath, new[] { PassthroughBlock, HandTrackingBlock, MrukBlock }, SceneContent.DitchPractice))
            {
                yield return step;
            }

            Log("6/7 Build scene list: Spike, Main");
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SpikeScenePath, true),
                new EditorBuildSettingsScene(MainScenePath, true),
            };
            yield return null;

            // Fixes can unlock further fixes (e.g. after the XR loader is assigned), so run twice.
            Log("7/7 Meta Project Setup Tool: Fix All");
            yield return OVRProjectSetup.FixAllAsync(BuildTargetGroup.Android);
            yield return null;
            yield return OVRProjectSetup.FixAllAsync(BuildTargetGroup.Android);
            AssetDatabase.SaveAssets();
        }

        private static void ConfigureMetaProject()
        {
            OVRProjectConfig config = OVRProjectConfig.CachedProjectConfig;
            config.handTrackingSupport = OVRProjectConfig.HandTrackingSupport.HandsOnly;
            config.insightPassthroughEnabled = true;
            config.anchorSupport = OVRProjectConfig.AnchorSupport.Enabled;
            config.sceneSupport = OVRProjectConfig.FeatureSupport.Supported;
            config.eyeTrackingSupport = OVRProjectConfig.FeatureSupport.Supported;
            OVRProjectConfig.CommitProjectConfig(config);
        }

        private enum SceneContent { TrackingSpike, DitchPractice }

        private static IEnumerable<object> BuildScene(string path, string[] blockIds, SceneContent content)
        {
            if (File.Exists(path))
            {
                Log("   keeping existing " + path);
                yield break;
            }

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            GameObject lightGo = new GameObject("Directional Light");
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Directional;
            lightGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            foreach (string id in blockIds)
            {
                BlockData block = GetBlock(id);
                Log("   installing building block: " + block.BlockName);
                yield return InstallWithDependencies(block);
            }

            OVRCameraRig rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (rig == null)
            {
                throw new InvalidOperationException("Building blocks did not create an OVRCameraRig in " + path);
            }

            SpikeSceneSetup.EnsureLookSource();
            if (content == SceneContent.TrackingSpike)
            {
                SpikeSceneSetup.AddTrackingSpike(rig, false);
            }
            else
            {
                SecretHandSetup.AddDitchPractice(rig, false);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new IOException("Could not save " + path);
            }
            Log("   saved " + path);
        }

        // Utils.GetBlockData and BlockData.InstallWithDependencies are internal in v207; we are locked to v207.
        private static BlockData GetBlock(string id)
        {
            Type utils = typeof(BlockData).Assembly.GetType("Meta.XR.BuildingBlocks.Editor.Utils");
            MethodInfo get = utils == null ? null : utils.GetMethod("GetBlockData", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static,
                null, new[] { typeof(string) }, null);
            if (get == null)
            {
                throw new MissingMethodException("Meta.XR.BuildingBlocks.Editor.Utils.GetBlockData(string) not found. Is the SDK still v207?");
            }
            BlockData block = get.Invoke(null, new object[] { id }) as BlockData;
            if (block == null)
            {
                throw new InvalidOperationException("Building block " + id + " not found.");
            }
            return block;
        }

        private static Task InstallWithDependencies(BlockData block)
        {
            MethodInfo install = typeof(BlockData).GetMethod("InstallWithDependencies", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                null, new[] { typeof(GameObject) }, null);
            if (install == null)
            {
                throw new MissingMethodException("BlockData.InstallWithDependencies(GameObject) not found. Is the SDK still v207?");
            }
            return (Task)install.Invoke(block, new object[] { null });
        }

        private static void Finish(bool ok, string message)
        {
            EditorApplication.update -= Tick;
            steps = null;
            waitingOn = null;
            if (ok)
            {
                Debug.Log("[Misdirection] Bootstrap " + message + ".");
            }
            else
            {
                Debug.LogError("[Misdirection] Bootstrap failed: " + message);
            }
            if (batch)
            {
                EditorApplication.Exit(ok ? 0 : 1);
            }
        }

        private static void Log(string message)
        {
            Debug.Log("[Misdirection] Bootstrap " + message);
        }
    }
}
