using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Misdirection.Audience;
using Misdirection.Core.Table;
using Misdirection.Core.View;
using Misdirection.Perf;
using Misdirection.Show;
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
        // Day 12: render statistics are averaged over the last seconds before the report (crowd up, show running).
        private const double RenderStatsSeconds = 5;
        private const double TimeoutSeconds = 600;

        public const string ReportDir = "Builds/Smoke";

        private static readonly List<string> Errors = new List<string>();
        private static readonly List<string> Notes = new List<string>();
        private static double playStart = -1;
        private static bool forcedTable;
        private static int framesAtWarmUp = -1;
        private static double timeAtWarmUp;
        private static Task pending;
        private static int statSamples;
        private static long statBatches, statDrawCalls, statSetPassCalls, statTriangles, statVertices;

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
            // ...and as a first launch, so the tutorial (first five minutes) is what plays.
            ShowDirector.ForgetTutorial();
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

            if (t >= ReportAfterSeconds - RenderStatsSeconds)
            {
                // What the Game view's Stats panel shows: a PC-side stand-in for draw cost on the headset.
                statSamples++;
                statBatches += UnityStats.batches;
                statDrawCalls += UnityStats.drawCalls;
                statSetPassCalls += UnityStats.setPassCalls;
                statTriangles += UnityStats.triangles;
                statVertices += UnityStats.vertices;
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
            ShowDirector show = UnityEngine.Object.FindFirstObjectByType<ShowDirector>();
            MentorHands mentor = UnityEngine.Object.FindFirstObjectByType<MentorHands>();
            string stage = show != null && show.Flow != null ? show.Flow.Stage.ToString() : "no ShowDirector";
            // With the table set on a first launch, the show must have moved on to the tutorial.
            bool showOk = show != null && show.Flow != null && show.Flow.Stage != Core.Show.ShowStage.Setup;
            bool pass = Errors.Count == 0 && tableOk && crowdOk && trickOk && showOk;
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
            json.Append("  \"show_stage\": \"").Append(stage).Append("\",\n");
            json.Append("  \"tutorial_step\": \"").Append(show != null && show.Tutorial != null ? show.Tutorial.Step.ToString() : "").Append("\",\n");
            json.Append("  \"paused\": \"").Append(show != null ? show.PauseReason.ToString() : "").Append("\",\n");
            json.Append("  \"mentor_demo\": \"").Append(mentor != null ? mentor.Demo.ToString() : "no MentorHands").Append("\",\n");
            json.Append("  \"time_scale\": ").Append(Time.timeScale.ToString("F2", inv)).Append(",\n");
            json.Append("  \"editor_fps\": ").Append(fps.ToString("F1", inv)).Append(",\n");
            // Day 12: draw cost on the PC (compare runs before and after a change), averaged over the last seconds.
            int n = Math.Max(1, statSamples);
            json.Append("  \"batches\": ").Append(statBatches / n).Append(",\n");
            json.Append("  \"draw_calls\": ").Append(statDrawCalls / n).Append(",\n");
            json.Append("  \"set_pass_calls\": ").Append(statSetPassCalls / n).Append(",\n");
            json.Append("  \"triangles\": ").Append(statTriangles / n).Append(",\n");
            json.Append("  \"vertices\": ").Append(statVertices / n).Append(",\n");
            json.Append("  \"render_stat_samples\": ").Append(statSamples).Append(",\n");
            int renderers;
            int materials = CountMaterials(out renderers);
            json.Append("  \"renderers\": ").Append(renderers).Append(",\n");
            json.Append("  \"materials_in_use\": ").Append(materials).Append(",\n");
            json.Append("  \"shared_materials\": \"").Append(Escape(SharedMaterials.Describe())).Append("\",\n");
            json.Append("  \"perf_log\": \"").Append(Escape(PerfLogger.LastWindow ?? "no PerfLogger window yet")).Append("\",\n");
            json.Append("  \"headset_settings\": \"").Append(Escape(QuestPerformance.LastApplied ?? "no QuestPerformance in the scene")).Append("\",\n");
            // Day 12: would the essentials be visible on the narrowest device, from where the head is now?
            json.Append("  \"fov_check\": ").Append(FovCheck(table, crowd)).Append(",\n");
            json.Append("  \"notes\": [").Append(JoinQuoted(Notes)).Append("],\n");
            json.Append("  \"errors\": [").Append(JoinQuoted(Errors)).Append("]\n");
            json.Append("}\n");
            File.WriteAllText(Path.Combine(ReportDir, "report.json"), json.ToString());
            Log("report written: pass=" + pass + " table=" + (table != null ? table.Source.ToString() : "missing") +
                " spectators=" + spectators + " trick=" + activeTrick + " show=" + stage + " errors=" + Errors.Count);
        }

        /// <summary>
        /// Which key anchors fall inside a 70 x 66 degree view (Meta VR Glasses) from the current head pose:
        /// the trick prompt, the show banner, the mat centre and each spectator's face, with where each
        /// sits in the view (yaw right, pitch up, degrees). Information only: it doesn't fail the run,
        /// because the simulated head may be looking anywhere.
        /// </summary>
        private static string FovCheck(TableCalibrator table, Crowd crowd)
        {
            Transform head = Camera.main != null ? Camera.main.transform : null;
            if (head == null || table == null || !table.Calibrated)
            {
                return "null";
            }
            FieldOfView fov = FieldOfView.Glasses;
            int inside = 0;
            int total = 0;
            StringBuilder sb = new StringBuilder();
            sb.Append("{\n    \"fov\": \"").Append(fov.Horizontal.ToString("F0", CultureInfo.InvariantCulture)).Append("x")
              .Append(fov.Vertical.ToString("F0", CultureInfo.InvariantCulture)).Append(" (Meta VR Glasses)\",\n");
            sb.Append("    \"hud\": ").Append(FovEntry(head, fov, table.Frame.ToWorld(UiAnchors.TrickHud).ToUnity(), ref inside, ref total)).Append(",\n");
            sb.Append("    \"banner\": ").Append(FovEntry(head, fov, table.Frame.ToWorld(UiAnchors.ShowBanner).ToUnity(), ref inside, ref total)).Append(",\n");
            sb.Append("    \"mat_centre\": ").Append(FovEntry(head, fov, table.Frame.ToWorld(UiAnchors.MatCentre).ToUnity(), ref inside, ref total)).Append(",\n");
            sb.Append("    \"spectators\": [");
            if (crowd != null)
            {
                for (int i = 0; i < crowd.Spectators.Count; i++)
                {
                    string entry = FovEntry(head, fov, crowd.Spectators[i].EyesPosition, ref inside, ref total);
                    sb.Append(i > 0 ? ", " : "").Append("{\"name\": \"").Append(Escape(crowd.NameOf(i))).Append("\", ").Append(entry.Substring(1));
                }
            }
            sb.Append("],\n");
            sb.Append("    \"inside\": ").Append(inside).Append(",\n");
            sb.Append("    \"of\": ").Append(total).Append("\n  }");
            return sb.ToString();
        }

        private static string FovEntry(Transform head, FieldOfView fov, Vector3 point, ref int inside, ref int total)
        {
            System.Numerics.Vector3 eye = head.position.ToNumerics();
            System.Numerics.Vector3 forward = head.forward.ToNumerics();
            System.Numerics.Vector3 up = head.up.ToNumerics();
            bool seen = ViewCone.Contains(eye, forward, up, fov, point.ToNumerics());
            float yaw, pitch;
            ViewCone.Angles(eye, forward, up, point.ToNumerics(), out yaw, out pitch);
            total++;
            if (seen)
            {
                inside++;
            }
            CultureInfo inv = CultureInfo.InvariantCulture;
            return "{\"inside\": " + (seen ? "true" : "false") + ", \"yaw\": " + yaw.ToString("F1", inv) + ", \"pitch\": " + pitch.ToString("F1", inv) + "}";
        }

        /// <summary>How many different materials the scene's active renderers use (fewer = fewer state changes).</summary>
        private static int CountMaterials(out int renderers)
        {
            HashSet<Material> seen = new HashSet<Material>();
            List<Material> shared = new List<Material>();
            renderers = 0;
            foreach (Renderer r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled)
                {
                    continue;
                }
                renderers++;
                r.GetSharedMaterials(shared);
                foreach (Material m in shared)
                {
                    if (m != null)
                    {
                        seen.Add(m);
                    }
                }
            }
            return seen.Count;
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
