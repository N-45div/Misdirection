using Misdirection.Audience;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Day 12 helper: put the ISDK gaze bridge (<see cref="IsdkSpectatorGaze"/>) on the scene's crowd,
    /// so it shows in the Inspector and can be tuned or switched off there. The crowd adds one by
    /// itself at runtime when the scene has none, so this is for visibility, not a requirement.
    /// </summary>
    public static class GazeSetup
    {
        [MenuItem("Misdirection/Day 12/Add ISDK Gaze (spectator faces) to Open Scene", priority = 1203)]
        public static void EnsureIsdkGazeMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<Crowd>(FindObjectsInactive.Include) == null)
            {
                EditorUtility.DisplayDialog("ISDK Gaze", "This scene has no crowd. Open the bootstrap's Show.unity or Main.unity first.", "OK");
                return;
            }
            EnsureIsdkGaze(true);
        }

        /// <summary>
        /// Adds <see cref="IsdkSpectatorGaze"/> next to the open scene's Crowd, wired to it and to the
        /// scene's look source. Safe to run again. Returns true if the scene changed; false when there
        /// is no crowd or it is already there.
        /// </summary>
        public static bool EnsureIsdkGaze(bool interactive)
        {
            Crowd crowd = UnityEngine.Object.FindFirstObjectByType<Crowd>(FindObjectsInactive.Include);
            if (crowd == null)
            {
                return false;
            }
            IsdkSpectatorGaze gaze = crowd.GetComponent<IsdkSpectatorGaze>();
            if (gaze != null)
            {
                Debug.Log("[Misdirection] Gaze: scene '" + SceneManager.GetActiveScene().name + "' already has the ISDK gaze bridge on its crowd.");
                return false;
            }
            gaze = Undo.AddComponent<IsdkSpectatorGaze>(crowd.gameObject);
            SerializedObject so = new SerializedObject(gaze);
            so.FindProperty("crowd").objectReferenceValue = crowd;
            so.FindProperty("lookSource").objectReferenceValue = new SerializedObject(crowd).FindProperty("lookSource").objectReferenceValue;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Misdirection] Gaze: added the ISDK gaze bridge to the crowd in '" + SceneManager.GetActiveScene().name +
                      "': spectator faces become ISDK GazeInteractables, hovered by an ISDK GazeInteractor along the look ray " +
                      "(head direction on Quest 3 / 3S).");
            if (interactive)
            {
                Selection.activeObject = gaze;
            }
            return true;
        }
    }
}
