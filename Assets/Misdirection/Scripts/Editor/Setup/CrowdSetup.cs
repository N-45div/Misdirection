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
            MonoBehaviour practice = UnityEngine.Object.FindFirstObjectByType<DitchPractice>(FindObjectsInactive.Include);
            if (practice == null)
            {
                practice = UnityEngine.Object.FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include);
            }
            if (practice == null)
            {
                EditorUtility.DisplayDialog("Crowd",
                    "Add the ditch practice first (Misdirection > Day 2 > Add Ditch Practice to Open Scene): the crowd uses its " +
                    "table calibration and tracked hands.", "OK");
                return;
            }
            Selection.activeGameObject = AddCrowd(practice, true).gameObject;
        }

        /// <summary>
        /// Adds a crowd wired to the scene's table, hands and look source, and hands it to <paramref name="user"/>
        /// (a DitchPractice or TrickDirector: anything with a serialized "crowd" field).
        /// </summary>
        public static Crowd AddCrowd(MonoBehaviour user, bool interactive)
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
            so.FindProperty("room").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<RoomTableFinder>(FindObjectsInactive.Include);
            so.ApplyModifiedPropertiesWithoutUndo();

            if (user != null)
            {
                SerializedObject us = new SerializedObject(user);
                SerializedProperty field = us.FindProperty("crowd");
                if (field != null)
                {
                    field.objectReferenceValue = crowd;
                    us.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Misdirection] Added the crowd (Tourist, Kid, Skeptic). They appear after the table is calibrated.");
            return crowd;
        }
    }
}
