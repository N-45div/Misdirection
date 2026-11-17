using System;
using Misdirection.Access;
using Misdirection.Progress;
using Misdirection.Show;
using Misdirection.Table;
using Misdirection.Trailer;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Records trailer footage: plays Show.unity in the Editor (no XR runtime) with
    /// <see cref="TrailerAutopilot"/> giving scripted head and hand input, and writes 30 fps frames plus
    /// shot markers to the folder in MISDIRECTION_TRAILER_OUT (default D:/video/capture/session). Exits
    /// when the performance ends (0) or times out (2). Entering Play reloads scripts, so progress lives
    /// in SessionState and an [InitializeOnLoad] hook carries on after each reload.
    ///
    ///   Unity -projectPath . -executeMethod Misdirection.EditorTools.TrailerCapture.Run -logFile trailer.log
    /// </summary>
    [InitializeOnLoad]
    public static class TrailerCapture
    {
        private const string StageKey = "Misdirection.Trailer.Stage";
        private const string StartKey = "Misdirection.Trailer.Start";
        private const double TimeoutSeconds = 3600;

        static TrailerCapture()
        {
            if (SessionState.GetString(StageKey, "").Length > 0)
            {
                EditorApplication.update += Tick;
            }
        }

        [MenuItem("Misdirection/Trailer/Record Trailer Footage (Play mode, scripted input)", priority = 1600)]
        public static void Run()
        {
            string outDir = Environment.GetEnvironmentVariable("MISDIRECTION_TRAILER_OUT");
            if (string.IsNullOrEmpty(outDir))
            {
                outDir = "D:/video/capture/session";
            }
            // A first launch at a fresh table, like a judge's: tutorial on, no saved table, default access.
            PlayerPrefs.DeleteKey(TableAnchorStore.PrefsKey);
            ShowDirector.ForgetTutorial();
            AccessRuntime.ForgetSettings();
            ProgressStore.Delete();
            // No XR runtime during the capture: the autopilot owns the head pose (a registered
            // simulator would otherwise drive the camera). Restored in Finish.
            UnityEngine.XR.Management.XRGeneralSettings xr = XrSettings();
            if (xr != null)
            {
                SessionState.SetBool(XrKey, xr.InitManagerOnStart);
                xr.InitManagerOnStart = false;
            }
            PlayerPrefs.SetString(TrailerAutopilot.SessionKey, "A");
            PlayerPrefs.SetString(TrailerAutopilot.OutKey, outDir);
            PlayerPrefs.DeleteKey(TrailerAutopilot.DoneKey);
            PlayerPrefs.Save();
            SessionState.SetString(StageKey, "play");
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Trailer] recording to " + outDir);
        }

        /// <summary>
        /// The same scripted performance, played inside Meta XR Simulator in real time so the simulator's
        /// own window can be screen-recorded (the rules ask for footage "via XR Simulator"). No frames are
        /// written; the XR runtime renders the view.
        /// </summary>
        [MenuItem("Misdirection/Trailer/Play Scripted Performance in Meta XR Simulator", priority = 1601)]
        public static void RunSimulator()
        {
            PlayerPrefs.DeleteKey(TableAnchorStore.PrefsKey);
            ShowDirector.ForgetTutorial();
            AccessRuntime.ForgetSettings();
            ProgressStore.Delete();
            UnityEngine.XR.Management.XRGeneralSettings xr = XrSettings();
            if (xr != null)
            {
                SessionState.SetBool(XrKey, xr.InitManagerOnStart);
                xr.InitManagerOnStart = true;
            }
            PlayerPrefs.SetString(TrailerAutopilot.SessionKey, TrailerAutopilot.SimulatorSession);
            PlayerPrefs.SetString(TrailerAutopilot.OutKey, "D:/video/capture/sim");
            PlayerPrefs.DeleteKey(TrailerAutopilot.DoneKey);
            PlayerPrefs.Save();
            SessionState.SetString(StageKey, "activate");
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Debug.Log("[Trailer] simulator performance starting");
        }

        private static void Tick()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            string stage = SessionState.GetString(StageKey, "");
            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f);
            if (elapsed > TimeoutSeconds && stage != "exit")
            {
                Finish(2);
                return;
            }
            switch (stage)
            {
                case "activate":
                    if (!SimulatorSmokeTest.SimulatorActive())
                    {
                        Debug.Log("[Trailer] activating Meta XR Simulator");
                        EditorApplication.ExecuteMenuItem(SimulatorSmokeTest.ActivateMenuPath);
                    }
                    SessionState.SetString(StageKey, "wait-active");
                    break;
                case "wait-active":
                    if (SimulatorSmokeTest.SimulatorActive())
                    {
                        Debug.Log("[Trailer] simulator active");
                        SessionState.SetString(StageKey, "play");
                    }
                    break;
                case "play":
                    // The SDK's welcome window opens over everything on a fresh Editor start.
                    foreach (EditorWindow w in Resources.FindObjectsOfTypeAll<EditorWindow>())
                    {
                        if (w != null && (w.GetType().Name.IndexOf("Welcome", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                          w.titleContent.text.IndexOf("Welcome", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            w.Close();
                        }
                    }
                    EditorSceneManager.OpenScene(MisdirectionBootstrap.ShowScenePath, OpenSceneMode.Single);
                    SessionState.SetString(StageKey, "playing");
                    EditorSettings.asyncShaderCompilation = false;
                    EditorApplication.EnterPlaymode();
                    break;
                case "playing":
                    if (EditorApplication.isPlaying && PlayerPrefs.GetInt(TrailerAutopilot.DoneKey, 0) == 1)
                    {
                        EditorApplication.ExitPlaymode();
                        SessionState.SetString(StageKey, "exit");
                    }
                    break;
                case "exit":
                    if (!EditorApplication.isPlaying && !EditorApplication.isPlayingOrWillChangePlaymode)
                    {
                        Finish(0);
                    }
                    break;
            }
        }

        private const string XrKey = "Misdirection.Trailer.XrInit";

        private static UnityEngine.XR.Management.XRGeneralSettings XrSettings()
        {
            return UnityEditor.XR.Management.XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Standalone);
        }

        private static void Finish(int code)
        {
            Debug.Log("[Trailer] finished (" + code + ")");
            EditorApplication.update -= Tick;
            SessionState.EraseString(StageKey);
            SessionState.EraseFloat(StartKey);
            PlayerPrefs.DeleteKey(TrailerAutopilot.SessionKey);
            PlayerPrefs.Save();
            UnityEngine.XR.Management.XRGeneralSettings xr = XrSettings();
            if (xr != null)
            {
                xr.InitManagerOnStart = SessionState.GetBool(XrKey, true);
                EditorUtility.SetDirty(xr);
                AssetDatabase.SaveAssets();
            }
            EditorSettings.asyncShaderCompilation = true;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-executeMethod") >= 0)
            {
                EditorApplication.Exit(code);
            }
        }
    }
}
