using System;
using Misdirection.Audience;
using Misdirection.Show;
using Misdirection.Tricks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Day 6 helpers: add Trick 2 (coin under the cup) next to Trick 1 and run them as a show.
    /// Day 9: the ShowDirector (tutorial, acts, bow, pause) and the mentor's ghost hands.
    /// </summary>
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
            bool directed = UnityEngine.Object.FindFirstObjectByType<ShowDirector>(FindObjectsInactive.Include) != null;
            if (sequence == null && !directed)
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

        [MenuItem("Misdirection/Day 9/Add Show Director (tutorial, acts, pause) to Open Scene", priority = 900)]
        public static void AddShowDirectorMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include) == null)
            {
                EditorUtility.DisplayDialog("Show Director", "The show runs Trick 1 and Trick 2: open the bootstrap's Show.unity first.", "OK");
                return;
            }
            EnsureCupTrickAndSequence(true);
            EnsureShowDirector(true);
            Selection.activeObject = UnityEngine.Object.FindFirstObjectByType<ShowDirector>(FindObjectsInactive.Include);
        }

        [MenuItem("Misdirection/Day 9/Replay the Tutorial Next Launch", priority = 901)]
        public static void ReplayTutorialMenu()
        {
            ShowDirector.ForgetTutorial();
            Debug.Log("[Misdirection] The tutorial will play on the next launch (Play mode or a fresh install).");
        }

        /// <summary>
        /// Day 8 + 9: adds the ShowDirector (acts with a growing crowd, the first-launch tutorial, bow and
        /// walk-off, pause) and the mentor's ghost hands, using Trick 1's hands, table, crowd and look source.
        /// Removes the older ShowSequence. Safe to run again. Returns true if the scene changed.
        /// </summary>
        public static bool EnsureShowDirector(bool interactive)
        {
            TrickDirector trick1 = UnityEngine.Object.FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include);
            CupTrickDirector trick2 = UnityEngine.Object.FindFirstObjectByType<CupTrickDirector>(FindObjectsInactive.Include);
            if (trick1 == null)
            {
                if (!interactive)
                {
                    throw new InvalidOperationException("No Trick 1 (TrickDirector) in the open scene.");
                }
                return false;
            }
            bool changed = false;
            foreach (ShowSequence old in UnityEngine.Object.FindObjectsByType<ShowSequence>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                Undo.DestroyObjectImmediate(old);
                changed = true;
            }

            ShowDirector director = UnityEngine.Object.FindFirstObjectByType<ShowDirector>(FindObjectsInactive.Include);
            if (director == null)
            {
                SerializedObject t1 = new SerializedObject(trick1);
                GameObject root = new GameObject("Misdirection Show");
                Undo.RegisterCreatedObjectUndo(root, "Add Show Director");

                GameObject mentorGo = new GameObject("Mentor Ghost Hands");
                mentorGo.transform.SetParent(root.transform, false);
                MentorHands mentor = mentorGo.AddComponent<MentorHands>();
                SerializedObject ms = new SerializedObject(mentor);
                ms.FindProperty("table").objectReferenceValue = t1.FindProperty("table").objectReferenceValue;
                LookSource look = t1.FindProperty("lookSource").objectReferenceValue as LookSource;
                if (look == null)
                {
                    look = UnityEngine.Object.FindFirstObjectByType<LookSource>(FindObjectsInactive.Include);
                }
                ms.FindProperty("head").objectReferenceValue = look != null ? look.Head : null;
                ms.ApplyModifiedPropertiesWithoutUndo();

                director = root.AddComponent<ShowDirector>();
                SerializedObject ds = new SerializedObject(director);
                foreach (string field in new[] { "leftHand", "rightHand", "table", "crowd", "lookSource" })
                {
                    SerializedProperty from = t1.FindProperty(field);
                    SerializedProperty to = ds.FindProperty(field);
                    if (from != null && to != null)
                    {
                        to.objectReferenceValue = from.objectReferenceValue;
                    }
                }
                if (ds.FindProperty("crowd").objectReferenceValue == null)
                {
                    ds.FindProperty("crowd").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<Crowd>(FindObjectsInactive.Include);
                }
                ds.FindProperty("mentor").objectReferenceValue = mentor;
                SerializedProperty list = ds.FindProperty("tricks");
                list.arraySize = trick2 != null ? 2 : 1;
                list.GetArrayElementAtIndex(0).objectReferenceValue = trick1;
                if (trick2 != null)
                {
                    list.GetArrayElementAtIndex(1).objectReferenceValue = trick2;
                }
                ds.ApplyModifiedPropertiesWithoutUndo();
                changed = true;
                Debug.Log("[Misdirection] Added the Show Director: tutorial on first launch, then three acts (" +
                          (trick2 != null ? "vanish, cup, vanish" : "vanish only") + "), bow or walk-off, pause when both hands leave.");
            }
            if (changed)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
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
