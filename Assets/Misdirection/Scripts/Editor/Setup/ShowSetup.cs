using System;
using Misdirection.Audience;
using Misdirection.Tricks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>Day 6 helpers: add Trick 2 (coin under the cup) next to Trick 1 and run them as a show.</summary>
    public static class ShowSetup
    {
        [MenuItem("Misdirection/Day 6/Add Trick 2 (Cup) and Show Sequence to Open Scene", priority = 600)]
        public static void AddCupTrickMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include) == null)
            {
                EditorUtility.DisplayDialog("Trick 2",
                    "Trick 2 joins Trick 1: add it first (Misdirection > Day 4), or open the bootstrap's Show.unity.", "OK");
                return;
            }
            if (EnsureCupTrickAndSequence(true))
            {
                Selection.activeObject = UnityEngine.Object.FindFirstObjectByType<CupTrickDirector>(FindObjectsInactive.Include);
            }
        }

        /// <summary>
        /// In a scene with Trick 1: adds the cup and Trick 2 sharing Trick 1's hands, table, coin and crowd,
        /// and a ShowSequence that alternates them. Safe to run again. Returns true if the scene changed.
        /// </summary>
        public static bool EnsureCupTrickAndSequence(bool interactive)
        {
            TrickDirector trick1 = UnityEngine.Object.FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include);
            if (trick1 == null)
            {
                if (!interactive)
                {
                    throw new InvalidOperationException("No Trick 1 (TrickDirector) in the open scene.");
                }
                return false;
            }
            bool changed = false;
            CupTrickDirector trick2 = UnityEngine.Object.FindFirstObjectByType<CupTrickDirector>(FindObjectsInactive.Include);
            if (trick2 == null)
            {
                trick2 = AddCupTrick(trick1);
                changed = true;
            }
            ShowSequence sequence = UnityEngine.Object.FindFirstObjectByType<ShowSequence>(FindObjectsInactive.Include);
            if (sequence == null)
            {
                sequence = trick1.gameObject.AddComponent<ShowSequence>();
                Undo.RegisterCreatedObjectUndo(sequence, "Add Show Sequence");
                SerializedObject ss = new SerializedObject(sequence);
                SerializedProperty list = ss.FindProperty("tricks");
                list.arraySize = 2;
                list.GetArrayElementAtIndex(0).objectReferenceValue = trick1;
                list.GetArrayElementAtIndex(1).objectReferenceValue = trick2;
                ss.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                Debug.Log("[Misdirection] Show: Trick 1 (vanish + produce) then Trick 2 (coin under the cup), repeating.");
            }
            return changed;
        }

        private static CupTrickDirector AddCupTrick(TrickDirector trick1)
        {
            SerializedObject t1 = new SerializedObject(trick1);
            GameObject root = trick1.gameObject;

            GameObject cupGo = new GameObject("Cup");
            cupGo.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(cupGo, "Add Cup");
            CupActor cup = cupGo.AddComponent<CupActor>();

            CupTrickDirector trick2 = root.AddComponent<CupTrickDirector>();
            SerializedObject t2 = new SerializedObject(trick2);
            foreach (string field in new[] { "leftHand", "rightHand", "table", "coin", "crowd", "lookSource" })
            {
                SerializedProperty from = t1.FindProperty(field);
                SerializedProperty to = t2.FindProperty(field);
                if (from != null && to != null)
                {
                    to.objectReferenceValue = from.objectReferenceValue;
                }
            }
            if (t2.FindProperty("crowd").objectReferenceValue == null)
            {
                t2.FindProperty("crowd").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<Crowd>(FindObjectsInactive.Include);
            }
            t2.FindProperty("cup").objectReferenceValue = cup;
            t2.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log("[Misdirection] Added Trick 2 (coin under the cup), sharing Trick 1's hands, table, coin and crowd.");
            return trick2;
        }
    }
}
