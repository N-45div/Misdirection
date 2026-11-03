using System;
using Misdirection.Audience;
using Misdirection.Hands;
using Misdirection.Table;
using Misdirection.Tricks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>Day 3 helpers: add the audience to a scene that already has the ditch practice.</summary>
    public static class CrowdSetup
    {
        [MenuItem("Misdirection/Day 3/Add Crowd to Open Scene", priority = 300)]
        public static void AddCrowdMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Crowd>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[Misdirection] This scene already has a crowd.");
                return;
            }
            DitchPractice practice = UnityEngine.Object.FindFirstObjectByType<DitchPractice>(FindObjectsInactive.Include);
            if (practice == null)
            {
                EditorUtility.DisplayDialog("Crowd",
                    "Add the ditch practice first (Misdirection > Day 2 > Add Ditch Practice to Open Scene): the crowd uses its " +
                    "table calibration and tracked hands.", "OK");
                return;
            }
            Selection.activeGameObject = AddCrowd(practice, true).gameObject;
        }

        /// <summary>Adds a crowd wired to the practice's table, hands and look source, and tells the practice about it.</summary>
        public static Crowd AddCrowd(DitchPractice practice, bool interactive)
        {
            TableCalibrator table = UnityEngine.Object.FindFirstObjectByType<TableCalibrator>(FindObjectsInactive.Include);
            LookSource look = SpikeSceneSetup.EnsureLookSource();
            TrackedHand left = null;
            TrackedHand right = null;
            foreach (TrackedHand hand in UnityEngine.Object.FindObjectsByType<TrackedHand>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (hand.Side == Core.Tricks.HandId.Left)
                {
                    left = hand;
                }
                else if (hand.Side == Core.Tricks.HandId.Right)
                {
                    right = hand;
                }
            }
            if (table == null || left == null || right == null)
            {
                string message = "The crowd needs the table calibrator and both tracked hands from the ditch practice.";
                if (!interactive)
                {
                    throw new InvalidOperationException(message);
                }
                Debug.LogWarning("[Misdirection] " + message);
            }

            GameObject go = new GameObject("Misdirection Crowd");
            Undo.RegisterCreatedObjectUndo(go, "Add Crowd");
            Crowd crowd = go.AddComponent<Crowd>();
            SerializedObject so = new SerializedObject(crowd);
            so.FindProperty("table").objectReferenceValue = table;
            so.FindProperty("lookSource").objectReferenceValue = look;
            so.FindProperty("leftHand").objectReferenceValue = left;
            so.FindProperty("rightHand").objectReferenceValue = right;
            so.ApplyModifiedPropertiesWithoutUndo();

            if (practice != null)
            {
                SerializedObject ps = new SerializedObject(practice);
                ps.FindProperty("crowd").objectReferenceValue = crowd;
                ps.ApplyModifiedPropertiesWithoutUndo();
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Misdirection] Added the crowd (Tourist, Kid, Skeptic). They appear after the table is calibrated.");
            return crowd;
        }
    }
}
