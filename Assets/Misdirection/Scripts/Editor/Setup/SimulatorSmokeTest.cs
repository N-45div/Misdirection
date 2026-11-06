using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Misdirection.Audience;
using Misdirection.Core.Table;
using Misdirection.Table;
using Misdirection.Tricks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Plays Show.unity in the Meta XR Simulator and checks the basics without a headset: no script
    /// errors, the table gets set (room scan, else placed in front of the simulated head), the crowd
    /// spawns, a trick is running. Writes Builds/Smoke/report.json and a screenshot, then exits
    /// (0 = pass, 1 = checks failed, 2 = timed out). Needs the Editor UI, so no -batchmode:
    ///
    ///   Unity -projectPath . -executeMethod Misdirection.EditorTools.SimulatorSmokeTest.Run -logFile smoke.log
    ///
    /// (tools/sim_smoke.sh does this). Run the bootstrap first: it applies Meta's PC-target settings
    /// headless (in the UI they pop a blocking restart dialog). Entering and leaving Play mode reloads scripts, so progress
    /// lives in SessionState and an [InitializeOnLoad] hook carries on after each reload.
    /// </summary>
    [InitializeOnLoad]
    public static class SimulatorSmokeTest
    {
        private const string StageKey = "Misdirection.Smoke.Stage";
        private const string StartKey = "Misdirection.Smoke.Start";
        private const string ExitCodeKey = "Misdirection.Smoke.ExitCode";
        private const string ActivateMenu = "Window/Meta/Meta XR Simulator/Activate";
        private const double WarmUpSeconds = 15;
        private const double ForceTableAfterSeconds = 20;
        private const double ReportAfterSeconds = 30;
        private const double TimeoutSeconds = 600;

        public const string ReportDir = "Builds/Smoke";

        private static readonly List<string> Errors = new List<string>();
        private static readonly List<string> Notes = new List<string>();
        private static double playStart = -1;
        private static bool forcedTable;
        private static int framesAtWarmUp = -1;
        private static double timeAtWarmUp;
        private static Task pending;

        static SimulatorSmokeTest()
        {
            string stage = SessionState.GetString(StageKey, "");
            if (stage.Length == 0)
            {
                return;
            }
            Application.logMessageReceived += OnLog;
            EditorApplication.update += Tick;
        }

        /// <summary>Entry point for -executeMethod.</summary>
        public static void Run()
        {
            SessionState.SetString(StageKey, "activate");
            SessionState.SetFloat(StartKey, (float)EditorApplication.timeSinceStartup);
            Application.logMessageReceived -= OnLog;
            Application.logMessageReceived += OnLog;
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
            Log("starting");
        }

        private static void Tick()
        {
            if (pending != null && !pending.IsCompleted)
            {
                return;
            }
            pending = null;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                return;
            }
            double elapsed = EditorApplication.timeSinceStartup - SessionState.GetFloat(StartKey, 0f);
            string stage = SessionState.GetString(StageKey, "");
            if (elapsed > TimeoutSeconds && stage != "exit")
            {
                Finish(2, "timed out in stage " + stage);
                return;
            }
            try
            {
                switch (stage)
                {
                    case "activate": Activate(); break;
                    case "wait-active": WaitActive(); break;
                    case "play": EnterPlay(); break;
                    case "playing": WhilePlaying(); break;
                    case "exit": Exit(); break;
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(1, "smoke test crashed: " + e.Message);
            }
        }

        private static void Activate()
        {
            if (!SimulatorActive())
            {
                Log("activating Meta XR Simulator");
                EditorApplication.ExecuteMenuItem(ActivateMenu);
            }
            SessionState.SetString(StageKey, "wait-active");
        }

        private static void WaitActive()
        {
            if (SimulatorActive())
            {
                Log("simulator active");
                SessionState.SetString(StageKey, "play");
            }
        }

        private static void EnterPlay()
        {
            // Start every run without last session's table, so the room-scan path is what gets tested.
            PlayerPrefs.DeleteKey(TableAnchorStore.PrefsKey);
            PlayerPrefs.Save();
            EditorSceneManager.OpenScene(MisdirectionBootstrap.ShowScenePath, OpenSceneMode.Single);
            Log("entering Play mode in Show.unity");
            SessionState.SetString(StageKey, "playing");
            // Screenshots should show real materials, not the cyan placeholder of async shader compilation.
            EditorSettings.asyncShaderCompilation = false;
            EditorApplication.EnterPlaymode();
        }

        private static void WhilePlaying()
        {
            if (!EditorApplication.isPlaying)
            {
                return; // still entering
            }
            if (playStart < 0)
            {
                playStart = EditorApplication.timeSinceStartup;
            }
            double t = EditorApplication.timeSinceStartup - playStart;

            if (t >= WarmUpSeconds && framesAtWarmUp < 0)
            {
                framesAtWarmUp = Time.frameCount;
                timeAtWarmUp = EditorApplication.timeSinceStartup;
            }

            TableCalibrator table = UnityEngine.Object.FindFirstObjectByType<TableCalibrator>();
            if (t >= ForceTableAfterSeconds && table != null && !table.Calibrated && !forcedTable)
            {
                forcedTable = true;
                Transform head = Camera.main != null ? Camera.main.transform : null;
                Vector3 eyes = head != null ? head.position : new Vector3(0f, 1.2f, 0f);
                Vector3 fwd = head != null ? head.forward : Vector3.forward;
                fwd.y = 0f;
                fwd = fwd.sqrMagnitude > 1e-4f ? fwd.normalized : Vector3.forward;
                Vector3 nearEdge = eyes + fwd * 0.3f - Vector3.up * 0.45f;
                table.SetFrame(new TableFrame(nearEdge.ToNumerics(), fwd.ToNumerics()), TableSource.Palms);
                Notes.Add("no table after " + ForceTableAfterSeconds + " s: placed one in front of the simulated head");
            }

            if (t < ReportAfterSeconds)
            {
                return;
            }
            WriteReport(table);
            EditorApplication.ExitPlaymode();
            SessionState.SetString(StageKey, "exit");
        }

        private static void Exit()
        {
            if (EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            int code = SessionState.GetInt(ExitCodeKey, 1);
            Finish(code, "done");
        }

        private static void WriteReport(TableCalibrator table)
        {
            Directory.CreateDirectory(ReportDir);
            string shot = Path.Combine(ReportDir, "show.png");
            ScreenCapture.CaptureScreenshot(shot);

            Crowd crowd = UnityEngine.Object.FindFirstObjectByType<Crowd>();
            RoomTableFinder room = UnityEngine.Object.FindFirstObjectByType<RoomTableFinder>();
            string activeTrick = "none";
            foreach (MonoBehaviour m in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (m is ISequencedTrick && m.enabled)
                {
                    activeTrick = m.GetType().Name;
                }
            }
            double seconds = EditorApplication.timeSinceStartup - timeAtWarmUp;
            double fps = framesAtWarmUp >= 0 && seconds > 0 ? (Time.frameCount - framesAtWarmUp) / seconds : 0;

            bool tableOk = table != null && table.Calibrated;
            int spectators = crowd != null ? crowd.Spectators.Count : 0;
            bool crowdOk = spectators > 0;
            bool trickOk = activeTrick != "none";
            bool pass = Errors.Count == 0 && tableOk && crowdOk && trickOk;
            SessionState.SetInt(ExitCodeKey, pass ? 0 : 1);

            CultureInfo inv = CultureInfo.InvariantCulture;
            StringBuilder json = new StringBuilder();
            json.Append("{\n");
            json.Append("  \"pass\": ").Append(pass ? "true" : "false").Append(",\n");
            json.Append("  \"simulator_active\": ").Append(SimulatorActive() ? "true" : "false").Append(",\n");
            json.Append("  \"table_set\": ").Append(tableOk ? "true" : "false").Append(",\n");
            json.Append("  \"table_source\": \"").Append(table != null ? table.Source.ToString() : "missing").Append("\",\n");
            json.Append("  \"room_status\": \"").Append(Escape(room != null ? room.Status : "no RoomTableFinder")).Append("\",\n");
            Vector3 headPos = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
            json.Append("  \"head\": \"").Append(headPos.ToString("F2")).Append("\",\n");
            json.Append("  \"floor_y\": ").Append(room != null && room.HasFloor ? room.FloorY.ToString("F2", inv) : "null").Append(",\n");
            json.Append("  \"spectators\": ").Append(spectators).Append(",\n");
            json.Append("  \"crowd_layout\": \"").Append(Escape(crowd != null ? crowd.LayoutNote : "")).Append("\",\n");
            json.Append("  \"active_trick\": \"").Append(activeTrick).Append("\",\n");
            json.Append("  \"editor_fps\": ").Append(fps.ToString("F1", inv)).Append(",\n");
            json.Append("  \"notes\": [").Append(JoinQuoted(Notes)).Append("],\n");
            json.Append("  \"errors\": [").Append(JoinQuoted(Errors)).Append("]\n");
            json.Append("}\n");
            File.WriteAllText(Path.Combine(ReportDir, "report.json"), json.ToString());
            Log("report written: pass=" + pass + " table=" + (table != null ? table.Source.ToString() : "missing") +
                " spectators=" + spectators + " trick=" + activeTrick + " errors=" + Errors.Count);
        }

        private static void OnLog(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Error && type != LogType.Exception && type != LogType.Assert)
            {
                return;
            }
            // Only play-mode problems count; editor tooling noise before Play doesn't.
            if (!EditorApplication.isPlaying || Errors.Count >= 20)
            {
                return;
            }
            string first = stackTrace != null ? stackTrace.Split('\n')[0] : "";
            Errors.Add(type + ": " + message + (first.Length > 0 ? " @ " + first : ""));
        }

        private static bool SimulatorActive()
        {
            string runtime = Environment.GetEnvironmentVariable("XR_RUNTIME_JSON") ?? "";
            return runtime.IndexOf("MetaXRSimulator", StringComparison.OrdinalIgnoreCase) >= 0
                || runtime.IndexOf("meta_openxr_simulator", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void Finish(int code, string message)
        {
            Log("finished (" + code + "): " + message);
            EditorApplication.update -= Tick;
            Application.logMessageReceived -= OnLog;
            SessionState.EraseString(StageKey);
            SessionState.EraseFloat(StartKey);
            SessionState.EraseInt(ExitCodeKey);
            EditorSettings.asyncShaderCompilation = true;
            if (EditorApplication.isPlaying)
            {
                EditorApplication.ExitPlaymode();
            }
            EditorApplication.Exit(code);
        }

        private static string JoinQuoted(List<string> items)
        {
            StringBuilder sb = new StringBuilder();
            foreach (string s in items)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }
                sb.Append('"').Append(Escape(s)).Append('"');
            }
            return sb.ToString();
        }

        private static string Escape(string s)
        {
            return (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", " ").Replace("\r", " ");
        }

        private static void Log(string message)
        {
            Debug.Log("[Misdirection] Smoke: " + message);
        }
    }
}
