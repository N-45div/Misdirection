using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using Misdirection.Spike;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>Day 1 helpers: drop the tracking spike into the open scene and sanity-check the scene.</summary>
    public static class SpikeSceneSetup
    {
        [MenuItem("Misdirection/Day 1/Add Tracking Spike to Open Scene", priority = 100)]
        public static void AddTrackingSpike()
        {
            OVRCameraRig[] rigs = UnityEngine.Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rigs.Length == 0)
            {
                EditorUtility.DisplayDialog("Tracking spike",
                    "No OVRCameraRig in this scene. Add the Camera Rig, Passthrough and Hand Tracking building blocks first " +
                    "(Meta > Tools > Building Blocks).", "OK");
                return;
            }
            OVRCameraRig rig = rigs[0];

            TrackingSpikeController existing = UnityEngine.Object.FindFirstObjectByType<TrackingSpikeController>(FindObjectsInactive.Include);
            if (existing != null)
            {
                Selection.activeGameObject = existing.gameObject;
                Debug.Log("[Misdirection] Tracking spike already in scene; selected it.");
                return;
            }

            LookSource look = UnityEngine.Object.FindFirstObjectByType<LookSource>(FindObjectsInactive.Include);
            if (look == null)
            {
                GameObject lookGo = new GameObject("Misdirection Look Source");
                Undo.RegisterCreatedObjectUndo(lookGo, "Add Look Source");
                look = lookGo.AddComponent<LookSource>();
                OVREyeGaze gaze = UnityEngine.Object.FindFirstObjectByType<OVREyeGaze>(FindObjectsInactive.Include);
                if (gaze != null)
                {
                    SerializedObject lookSo = new SerializedObject(look);
                    lookSo.FindProperty("eyeGaze").objectReferenceValue = gaze;
                    lookSo.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            GameObject go = new GameObject("Misdirection Tracking Spike");
            Undo.RegisterCreatedObjectUndo(go, "Add Tracking Spike");
            TrackingSpikeController spike = go.AddComponent<TrackingSpikeController>();

            OVRHand left;
            OVRHand right;
            FindHands(rig, out left, out right);

            SerializedObject so = new SerializedObject(spike);
            so.FindProperty("rig").objectReferenceValue = rig;
            so.FindProperty("lookSource").objectReferenceValue = look;
            so.FindProperty("leftHand").objectReferenceValue = left;
            so.FindProperty("rightHand").objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = go;

            if (left == null || right == null)
            {
                Debug.LogWarning("[Misdirection] Added the tracking spike, but could not match both OVRHand components to " +
                                 "left/right. Assign them on 'Misdirection Tracking Spike' or add the Hand Tracking building block.");
            }
            else
            {
                Debug.Log("[Misdirection] Added the tracking spike. Save the scene, put it first in Build Profiles, then build.");
            }
        }

        [MenuItem("Misdirection/Day 1/Check Scene and Project", priority = 101)]
        public static void CheckScene()
        {
            List<string> ok = new List<string>();
            List<string> problems = new List<string>();

            OVRCameraRig[] rigs = UnityEngine.Object.FindObjectsByType<OVRCameraRig>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (rigs.Length == 1)
            {
                ok.Add("One OVRCameraRig");
            }
            else
            {
                problems.Add(rigs.Length + " OVRCameraRig components (need exactly 1; building blocks and samples can add duplicates)");
            }

            if (rigs.Length > 0)
            {
                OVRHand left;
                OVRHand right;
                FindHands(rigs[0], out left, out right);
                if (left != null && right != null)
                {
                    ok.Add("Left and right OVRHand found");
                }
                else
                {
                    problems.Add("OVRHand left/right not both found (Hand Tracking building block)");
                }
            }

            if (UnityEngine.Object.FindFirstObjectByType<OVRPassthroughLayer>(FindObjectsInactive.Include) != null)
            {
                ok.Add("OVRPassthroughLayer present");
            }
            else
            {
                problems.Add("No OVRPassthroughLayer (Passthrough building block)");
            }

            OVRManager manager = UnityEngine.Object.FindFirstObjectByType<OVRManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                problems.Add("No OVRManager in scene");
            }
            else
            {
                SerializedProperty pt = new SerializedObject(manager).FindProperty("isInsightPassthroughEnabled");
                if (pt != null && !pt.boolValue)
                {
                    problems.Add("OVRManager: Insight Passthrough is not enabled");
                }
                else if (pt != null)
                {
                    ok.Add("OVRManager passthrough enabled");
                }
            }

            if (UnityEngine.Object.FindFirstObjectByType<OVREyeGaze>(FindObjectsInactive.Include) != null)
            {
                ok.Add("OVREyeGaze present (eyes used when available, head otherwise)");
            }
            else
            {
                ok.Add("No OVREyeGaze: look source will use head direction (fine on Quest 3/3S)");
            }

            string handSupport = ReadHandTrackingSupport();
            if (handSupport == null)
            {
                problems.Add("Could not read OVRProjectConfig hand tracking support; check OVRManager > Quest Features by hand");
            }
            else if (handSupport == "HandsOnly")
            {
                ok.Add("Hand tracking support: Hands Only");
            }
            else
            {
                problems.Add("Hand tracking support is " + handSupport + "; set OVRManager > Quest Features > Hand Tracking Support to Hands Only");
            }

            string active = SceneManager.GetActiveScene().path;
            bool inBuild = false;
            foreach (EditorBuildSettingsScene s in EditorBuildSettings.scenes)
            {
                if (s.enabled && s.path == active)
                {
                    inBuild = true;
                }
            }
            if (inBuild)
            {
                ok.Add("Active scene is in the build list");
            }
            else
            {
                problems.Add("Active scene is not enabled in Build Profiles");
            }

            StringBuilder sb = new StringBuilder();
            foreach (string line in ok)
            {
                sb.Append("OK   ").Append(line).Append('\n');
            }
            foreach (string line in problems)
            {
                sb.Append("FIX  ").Append(line).Append('\n');
            }

            if (problems.Count == 0)
            {
                Debug.Log("[Misdirection] Scene check passed.\n" + sb);
            }
            else
            {
                Debug.LogWarning("[Misdirection] Scene check found " + problems.Count + " issue(s).\n" + sb);
            }
            EditorUtility.DisplayDialog("Misdirection scene check", sb.ToString(), "OK");
        }

        /// <summary>Match OVRHand components to sides by rig hierarchy (edit mode safe), then by name.</summary>
        private static void FindHands(OVRCameraRig rig, out OVRHand left, out OVRHand right)
        {
            left = null;
            right = null;
            Transform leftAnchor = rig.transform.Find("TrackingSpace/LeftHandAnchor");
            Transform rightAnchor = rig.transform.Find("TrackingSpace/RightHandAnchor");

            OVRHand[] hands = UnityEngine.Object.FindObjectsByType<OVRHand>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (OVRHand hand in hands)
            {
                int side = -1;
                if (leftAnchor != null && hand.transform.IsChildOf(leftAnchor))
                {
                    side = 0;
                }
                else if (rightAnchor != null && hand.transform.IsChildOf(rightAnchor))
                {
                    side = 1;
                }
                else
                {
                    for (Transform t = hand.transform; t != null && side < 0; t = t.parent)
                    {
                        string n = t.name.ToLowerInvariant();
                        if (n.Contains("left"))
                        {
                            side = 0;
                        }
                        else if (n.Contains("right"))
                        {
                            side = 1;
                        }
                    }
                }

                if (side == 0 && left == null)
                {
                    left = hand;
                }
                else if (side == 1 && right == null)
                {
                    right = hand;
                }
            }
        }

        /// <summary>Reads OVRProjectConfig.handTrackingSupport by reflection so an SDK rename cannot break compilation.</summary>
        private static string ReadHandTrackingSupport()
        {
            try
            {
                Type configType = typeof(OVRCameraRig).Assembly.GetType("OVRProjectConfig");
                if (configType == null)
                {
                    return null;
                }

                object config = null;
                PropertyInfo cached = configType.GetProperty("CachedProjectConfig", BindingFlags.Public | BindingFlags.Static);
                if (cached != null)
                {
                    config = cached.GetValue(null, null);
                }
                if (config == null)
                {
                    MethodInfo get = configType.GetMethod("GetProjectConfig", BindingFlags.Public | BindingFlags.Static);
                    if (get != null)
                    {
                        ParameterInfo[] ps = get.GetParameters();
                        object[] args = new object[ps.Length];
                        for (int i = 0; i < ps.Length; i++)
                        {
                            args[i] = ps[i].HasDefaultValue ? ps[i].DefaultValue : null;
                        }
                        config = get.Invoke(null, args);
                    }
                }
                if (config == null)
                {
                    return null;
                }

                FieldInfo field = configType.GetField("handTrackingSupport", BindingFlags.Public | BindingFlags.Instance);
                if (field == null)
                {
                    return null;
                }
                object value = field.GetValue(config);
                return value != null ? value.ToString() : null;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
