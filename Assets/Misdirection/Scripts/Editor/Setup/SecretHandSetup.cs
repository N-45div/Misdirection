using System;
using Misdirection.Core.Tricks;
using Misdirection.Hands;
using Misdirection.Table;
using Misdirection.Tricks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>Day 2 helpers: put the secret-hand ditch practice into a scene.</summary>
    public static class SecretHandSetup
    {
        [MenuItem("Misdirection/Day 2/Add Ditch Practice to Open Scene", priority = 200)]
        public static void AddDitchPracticeMenu()
        {
            OVRCameraRig rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (rig == null)
            {
                EditorUtility.DisplayDialog("Ditch practice",
                    "No OVRCameraRig in this scene. Run Misdirection > Setup > Run Project Bootstrap, or add the Passthrough " +
                    "and Hand Tracking building blocks first.", "OK");
                return;
            }
            if (UnityEngine.Object.FindFirstObjectByType<DitchPractice>(FindObjectsInactive.Include) != null)
            {
                Debug.Log("[Misdirection] Ditch practice is already in this scene.");
                return;
            }
            DitchPractice practice = AddDitchPractice(rig, true);
            Selection.activeGameObject = practice.gameObject;
        }

        /// <summary>Adds tracked hands, the table calibrator, the coin and the practice loop, all wired up.</summary>
        public static DitchPractice AddDitchPractice(OVRCameraRig rig, bool interactive)
        {
            LookSource look = SpikeSceneSetup.EnsureLookSource();

            OVRHand leftOvr;
            OVRHand rightOvr;
            SpikeSceneSetup.FindHandsPublic(rig, out leftOvr, out rightOvr);
            if (leftOvr == null || rightOvr == null)
            {
                string message = "Ditch practice needs both OVRHand components (Hand Tracking building block).";
                if (!interactive)
                {
                    throw new InvalidOperationException(message);
                }
                Debug.LogWarning("[Misdirection] " + message + " Assign them on the 'Misdirection Hands' children.");
            }

            GameObject handsRoot = new GameObject("Misdirection Hands");
            Undo.RegisterCreatedObjectUndo(handsRoot, "Add Misdirection Hands");
            TrackedHand left = CreateHand(handsRoot.transform, HandId.Left, leftOvr);
            TrackedHand right = CreateHand(handsRoot.transform, HandId.Right, rightOvr);

            GameObject root = new GameObject("Misdirection Ditch Practice");
            Undo.RegisterCreatedObjectUndo(root, "Add Ditch Practice");
            TableCalibrator table = root.AddComponent<TableCalibrator>();
            Wire(table, "leftHand", left, "rightHand", right, "lookSource", look);
            AddRoomAwareness(root, table, look);

            GameObject coinGo = new GameObject("Coin");
            coinGo.transform.SetParent(root.transform, false);
            CoinActor coin = coinGo.AddComponent<CoinActor>();

            DitchPractice practice = root.AddComponent<DitchPractice>();
            Wire(practice, "leftHand", left, "rightHand", right, "lookSource", look);
            SerializedObject so = new SerializedObject(practice);
            so.FindProperty("table").objectReferenceValue = table;
            so.FindProperty("coin").objectReferenceValue = coin;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Misdirection] Added ditch practice (tracked hands, table calibrator, coin).");
            return practice;
        }

        [MenuItem("Misdirection/Day 4/Add Trick 1 (Vanish + Produce) to Open Scene", priority = 400)]
        public static void AddTrickMenu()
        {
            OVRCameraRig rig = UnityEngine.Object.FindFirstObjectByType<OVRCameraRig>(FindObjectsInactive.Include);
            if (rig == null)
            {
                EditorUtility.DisplayDialog("Trick 1", "No OVRCameraRig in this scene. Run Misdirection > Setup > Run Project Bootstrap.", "OK");
                return;
            }
            if (UnityEngine.Object.FindFirstObjectByType<DitchPractice>(FindObjectsInactive.Include) != null ||
                UnityEngine.Object.FindFirstObjectByType<TrickDirector>(FindObjectsInactive.Include) != null)
            {
                EditorUtility.DisplayDialog("Trick 1",
                    "This scene already has a ditch practice or a trick, and both would grab the same pinches. " +
                    "Use the bootstrap's Show.unity, or a fresh scene with the building blocks.", "OK");
                return;
            }
            TrickDirector director = AddTrick(rig, true);
            CrowdSetup.AddCrowd(director, true);
            Selection.activeGameObject = director.gameObject;
        }

        /// <summary>Adds tracked hands, the table calibrator, the coin and the Trick 1 director, all wired up (add the crowd after).</summary>
        public static TrickDirector AddTrick(OVRCameraRig rig, bool interactive)
        {
            LookSource look = SpikeSceneSetup.EnsureLookSource();
            OVRHand leftOvr;
            OVRHand rightOvr;
            SpikeSceneSetup.FindHandsPublic(rig, out leftOvr, out rightOvr);
            if ((leftOvr == null || rightOvr == null) && !interactive)
            {
                throw new InvalidOperationException("Trick 1 needs both OVRHand components (Hand Tracking building block).");
            }

            GameObject handsRoot = new GameObject("Misdirection Hands");
            Undo.RegisterCreatedObjectUndo(handsRoot, "Add Misdirection Hands");
            TrackedHand left = CreateHand(handsRoot.transform, HandId.Left, leftOvr);
            TrackedHand right = CreateHand(handsRoot.transform, HandId.Right, rightOvr);

            GameObject root = new GameObject("Misdirection Trick 1");
            Undo.RegisterCreatedObjectUndo(root, "Add Trick 1");
            TableCalibrator table = root.AddComponent<TableCalibrator>();
            Wire(table, "leftHand", left, "rightHand", right, "lookSource", look);
            AddRoomAwareness(root, table, look);
            SerializedObject ts = new SerializedObject(table);
            ts.FindProperty("showServante").boolValue = false; // in a show, the servante is the player's secret
            ts.ApplyModifiedPropertiesWithoutUndo();

            GameObject coinGo = new GameObject("Coin");
            coinGo.transform.SetParent(root.transform, false);
            CoinActor coin = coinGo.AddComponent<CoinActor>();

            TrickDirector director = root.AddComponent<TrickDirector>();
            Wire(director, "leftHand", left, "rightHand", right, "lookSource", look);
            SerializedObject so = new SerializedObject(director);
            so.FindProperty("table").objectReferenceValue = table;
            so.FindProperty("coin").objectReferenceValue = coin;
            so.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Misdirection] Added Trick 1 (tracked hands, table calibrator, coin, director).");
            return director;
        }

        /// <summary>Day 5: find the table in the room scan, remember it between sessions (palms stay the fallback).</summary>
        public static RoomTableFinder AddRoomAwareness(GameObject root, TableCalibrator table, LookSource look)
        {
            RoomTableFinder finder = root.AddComponent<RoomTableFinder>();
            SerializedObject fs = new SerializedObject(finder);
            fs.FindProperty("table").objectReferenceValue = table;
            fs.FindProperty("lookSource").objectReferenceValue = look;
            fs.ApplyModifiedPropertiesWithoutUndo();

            TableAnchorStore store = root.AddComponent<TableAnchorStore>();
            SerializedObject ss = new SerializedObject(store);
            ss.FindProperty("table").objectReferenceValue = table;
            ss.ApplyModifiedPropertiesWithoutUndo();
            return finder;
        }

        private static TrackedHand CreateHand(Transform parent, HandId side, OVRHand ovrHand)
        {
            GameObject go = new GameObject(side + " Tracked Hand");
            go.transform.SetParent(parent, false);
            TrackedHand hand = go.AddComponent<TrackedHand>();
            SerializedObject so = new SerializedObject(hand);
            so.FindProperty("side").enumValueIndex = (int)side;
            so.FindProperty("hand").objectReferenceValue = ovrHand;
            so.FindProperty("skeleton").objectReferenceValue = ovrHand != null ? ovrHand.GetComponent<OVRSkeleton>() : null;
            so.ApplyModifiedPropertiesWithoutUndo();
            return hand;
        }

        private static void Wire(UnityEngine.Object target, string leftName, UnityEngine.Object left, string rightName, UnityEngine.Object right,
                                 string lookName, UnityEngine.Object look)
        {
            SerializedObject so = new SerializedObject(target);
            so.FindProperty(leftName).objectReferenceValue = left;
            so.FindProperty(rightName).objectReferenceValue = right;
            so.FindProperty(lookName).objectReferenceValue = look;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
