using System.Globalization;
using UnityEngine;

namespace Misdirection.Perf
{
    /// <summary>
    /// Development builds and the Editor only: every few seconds, logs the average and worst frame
    /// time and the frame rate as "[Misdirection] Perf: ...", so a logcat capture
    /// (adb logcat -s Unity) shows whether the build holds 72 fps. On the headset it adds the
    /// runtime's own numbers when it offers them (OVRPlugin perf metrics): app CPU and GPU time per
    /// frame, GPU load and frames the compositor dropped. Release builds switch it off in Awake, so
    /// it costs nothing there.
    /// </summary>
    public sealed class PerfLogger : MonoBehaviour
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        [Tooltip("Length of each measuring window (seconds).")]
        [SerializeField] private float windowSeconds = 5f;
        [Tooltip("Log in release builds too (off: development builds and the Editor only).")]
        [SerializeField] private bool alsoInRelease;

        private float elapsed;
        private int frames;
        private float worst;
        private int droppedBefore = -1;
        private bool metricsFailed;

        /// <summary>The last window as text, for the smoke report.</summary>
        public static string LastWindow { get; private set; }

        /// <summary>Average frames per second over the last window (0 until the first one ends).</summary>
        public static float LastFps { get; private set; }

        private void Awake()
        {
            if (!alsoInRelease && !Debug.isDebugBuild && !Application.isEditor)
            {
                enabled = false;
            }
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            elapsed += dt;
            frames++;
            if (dt > worst)
            {
                worst = dt;
            }
            if (elapsed < windowSeconds)
            {
                return;
            }

            float average = elapsed / frames;
            LastFps = 1f / Mathf.Max(average, 1e-6f);
            string line = LastFps.ToString("F1", Inv) + " fps, frame " + (average * 1000f).ToString("F1", Inv) + " ms avg, " +
                          (worst * 1000f).ToString("F1", Inv) + " ms worst, over " + elapsed.ToString("F1", Inv) + " s (" + frames +
                          " frames)" + HeadsetNumbers();
            LastWindow = line;
            Debug.Log("[Misdirection] Perf: " + line);
            elapsed = 0f;
            frames = 0;
            worst = 0f;
        }

        /// <summary>The runtime's per-frame numbers, read once per window. Empty in the Editor or when unsupported.</summary>
        private string HeadsetNumbers()
        {
            if (Application.isEditor || metricsFailed)
            {
                return "";
            }
            try
            {
                string text = "";
                float? cpu = Metric(OVRPlugin.PerfMetrics.App_CpuTime_Float);
                float? gpu = Metric(OVRPlugin.PerfMetrics.App_GpuTime_Float);
                float? load = Metric(OVRPlugin.PerfMetrics.System_GpuUtilPercentage_Float);
                // Printed as the runtime reports them: the v207 SDK does not state the units (expect
                // seconds for the times and 0..1 for the load).
                if (cpu.HasValue)
                {
                    text += ", app CPU time " + cpu.Value.ToString("F4", Inv);
                }
                if (gpu.HasValue)
                {
                    text += ", app GPU time " + gpu.Value.ToString("F4", Inv);
                }
                if (load.HasValue)
                {
                    text += ", GPU load " + load.Value.ToString("F2", Inv);
                }
                if (OVRPlugin.IsPerfMetricsSupported(OVRPlugin.PerfMetrics.Compositor_DroppedFrameCount_Int))
                {
                    int? dropped = OVRPlugin.GetPerfMetricsInt(OVRPlugin.PerfMetrics.Compositor_DroppedFrameCount_Int);
                    if (dropped.HasValue)
                    {
                        if (droppedBefore >= 0)
                        {
                            text += ", " + (dropped.Value - droppedBefore) + " dropped";
                        }
                        droppedBefore = dropped.Value;
                    }
                }
                OVRDisplay display = OVRManager.display;
                if (display != null)
                {
                    text += ", display " + display.displayFrequency.ToString("F0", Inv) + " Hz";
                }
                return text;
            }
            catch (System.Exception e)
            {
                // A missing metric must never stop the show: say so once and carry on with frame times.
                metricsFailed = true;
                Debug.LogWarning("[Misdirection] Perf: headset metrics unavailable (" + e.Message + ")");
                return "";
            }
        }

        private static float? Metric(OVRPlugin.PerfMetrics metric)
        {
            return OVRPlugin.IsPerfMetricsSupported(metric) ? OVRPlugin.GetPerfMetricsFloat(metric) : null;
        }

        /// <summary>Play mode can restart without reloading scripts.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LastWindow = null;
            LastFps = 0f;
        }
    }
}
