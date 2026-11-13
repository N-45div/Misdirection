using System;
using System.IO;
using System.Text;
using System.Xml;
using Misdirection.Access;
using Misdirection.Show;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Day 11 helpers: add the accessibility layer (Access Tray, one-handed play, relaxed timing,
    /// captions, high contrast, voice) to the show scene, reset the saved options, and keep the Android
    /// manifest from asking for any permission when the app starts.
    /// </summary>
    public static class AccessSetup
    {
        public const string ManifestPath = "Assets/Plugins/Android/AndroidManifest.xml";
        private const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        private const string SkipDialog = "unityplayer.SkipPermissionsDialog";
        private const string RecordAudio = "android.permission.RECORD_AUDIO";

        [MenuItem("Misdirection/Day 11/Add Accessibility (tray, one-handed, captions, voice) to Open Scene", priority = 1100)]
        public static void AddAccessMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<ShowDirector>(FindObjectsInactive.Include) == null)
            {
                EditorUtility.DisplayDialog("Accessibility",
                    "The accessibility layer joins the show: add the Show Director first (Misdirection > Day 9), or open the bootstrap's Show.unity.", "OK");
                return;
            }
            if (EnsureAccess(true))
            {
                Selection.activeObject = UnityEngine.Object.FindFirstObjectByType<AccessRuntime>(FindObjectsInactive.Include);
            }
            else
            {
                Debug.Log("[Misdirection] The accessibility layer is already in this scene.");
            }
        }

        [MenuItem("Misdirection/Day 11/Reset Access Settings", priority = 1101)]
        public static void ResetAccessSettingsMenu()
        {
            AccessRuntime.ForgetSettings();
            Debug.Log("[Misdirection] Access settings are back to their defaults (one hand: auto, captions on, the rest off).");
        }

        [MenuItem("Misdirection/Day 11/Check: No Permission Prompt at Startup", priority = 1102)]
        public static void CheckManifestMenu()
        {
            bool changed = EnsureNoStartupPermissionPrompt();
            Debug.Log("[Misdirection] " + ManifestPath + (changed
                ? ": fixed. Unity will no longer ask for permissions by itself when the app starts."
                : ": fine. Unity does not ask for permissions by itself when the app starts."));
        }

        /// <summary>
        /// Adds the accessibility layer to a scene that has the Show Director, wired to the show's hands,
        /// table, crowd, look source and ghost hands. Safe to run again. Returns true if the scene changed.
        /// </summary>
        public static bool EnsureAccess(bool interactive)
        {
            ShowDirector show = UnityEngine.Object.FindFirstObjectByType<ShowDirector>(FindObjectsInactive.Include);
            if (show == null)
            {
                if (!interactive)
                {
                    throw new InvalidOperationException("No Show Director in the open scene.");
                }
                return false;
            }
            if (UnityEngine.Object.FindFirstObjectByType<AccessRuntime>(FindObjectsInactive.Include) != null)
            {
                return false;
            }

            GameObject root = new GameObject("Misdirection Access");
            Undo.RegisterCreatedObjectUndo(root, "Add Accessibility");
            AccessRuntime access = AccessRuntime.AddTo(root);

            SerializedObject from = new SerializedObject(show);
            SerializedObject to = new SerializedObject(access);
            foreach (string field in new[] { "leftHand", "rightHand", "table", "crowd", "lookSource", "mentor" })
            {
                SerializedProperty source = from.FindProperty(field);
                SerializedProperty target = to.FindProperty(field);
                if (source != null && target != null)
                {
                    target.objectReferenceValue = source.objectReferenceValue;
                }
            }
            if (to.FindProperty("lookSource").objectReferenceValue == null)
            {
                to.FindProperty("lookSource").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<LookSource>(FindObjectsInactive.Include);
            }
            to.FindProperty("show").objectReferenceValue = show;
            to.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Misdirection] Added the accessibility layer: Access Tray along the mat's left edge (one hand, relaxed, " +
                      "captions, high contrast, voice), one-handed play that switches itself on, captions for sound.");
            return true;
        }

        /// <summary>
        /// Makes sure Unity never shows a permission prompt by itself at startup. Unity's Android player asks
        /// for every "dangerous" permission in the manifest when it launches unless the manifest says
        /// unityplayer.SkipPermissionsDialog = true, and the optional voice "Look!" needs RECORD_AUDIO in the
        /// manifest. Meta's manifest template sets the flag to false, so this puts it right again whenever
        /// Meta's tools rewrite the file (the bootstrap and every Android build call it). The game asks for
        /// the microphone itself, only when the player switches voice on (VoiceLook). The room scan's own
        /// permission is untouched: OVRManager and MRUK still ask for it. Returns true if the file changed.
        /// </summary>
        public static bool EnsureNoStartupPermissionPrompt()
        {
            if (!File.Exists(ManifestPath))
            {
                // No custom manifest: nothing to fix here (Unity then generates one; see docs/days/day-11.md).
                return false;
            }
            XmlDocument doc = new XmlDocument();
            doc.PreserveWhitespace = true;
            doc.Load(ManifestPath);
            XmlNode manifest = doc.SelectSingleNode("/manifest");
            XmlNode application = doc.SelectSingleNode("/manifest/application");
            if (manifest == null || application == null)
            {
                return false;
            }
            bool changed = false;

            // The microphone permission, declared so that asking for it later can succeed.
            bool hasRecordAudio = false;
            foreach (XmlNode node in manifest.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element != null && element.LocalName == "uses-permission" && element.GetAttribute("name", AndroidNamespace) == RecordAudio)
                {
                    hasRecordAudio = true;
                    break;
                }
            }
            if (!hasRecordAudio)
            {
                XmlElement feature = doc.CreateElement("uses-feature");
                feature.SetAttribute("name", AndroidNamespace, "android.hardware.microphone");
                feature.SetAttribute("required", AndroidNamespace, "false");
                manifest.AppendChild(feature);
                XmlElement permission = doc.CreateElement("uses-permission");
                permission.SetAttribute("name", AndroidNamespace, RecordAudio);
                manifest.AppendChild(permission);
                changed = true;
            }

            XmlElement flag = null;
            foreach (XmlNode node in application.ChildNodes)
            {
                XmlElement element = node as XmlElement;
                if (element != null && element.LocalName == "meta-data" && element.GetAttribute("name", AndroidNamespace) == SkipDialog)
                {
                    flag = element;
                    break;
                }
            }
            if (flag == null)
            {
                flag = doc.CreateElement("meta-data");
                flag.SetAttribute("name", AndroidNamespace, SkipDialog);
                application.AppendChild(flag);
            }
            if (flag.GetAttribute("value", AndroidNamespace) != "true")
            {
                flag.SetAttribute("value", AndroidNamespace, "true");
                changed = true;
            }
            if (!changed)
            {
                return false;
            }
            XmlWriterSettings settings = new XmlWriterSettings();
            settings.Encoding = new UTF8Encoding(false);
            using (XmlWriter writer = XmlWriter.Create(ManifestPath, settings))
            {
                doc.Save(writer);
            }
            AssetDatabase.ImportAsset(ManifestPath);
            return true;
        }
    }

    /// <summary>
    /// Runs before every Android build: if Meta's tools have rewritten the manifest since the bootstrap, put
    /// "no permission prompt at startup" back before the APK is made.
    /// </summary>
    public sealed class AccessManifestGuard : IPreprocessBuildWithReport
    {
        public int callbackOrder { get { return 0; } }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform == BuildTarget.Android && AccessSetup.EnsureNoStartupPermissionPrompt())
            {
                Debug.Log("[Misdirection] " + AccessSetup.ManifestPath + ": switched Unity's startup permission dialog off before building.");
            }
        }
    }
}
