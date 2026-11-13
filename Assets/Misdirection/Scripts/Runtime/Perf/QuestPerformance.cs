using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.Perf
{
    /// <summary>
    /// Asks the headset for the settings the game is tuned for on Quest 3 / 3S (Day 12): fixed
    /// foveated rendering, sustained CPU and GPU levels, and a 72 Hz display. These are requests made
    /// at runtime through OVRManager (nothing in the scene file stores them), so this component makes
    /// them once at start and once more a moment later, in case the XR session was not running yet.
    ///
    /// Nothing here can be tested without a headset, so every call checks support first, is wrapped
    /// so a failure cannot stop the show, and logs what it set as "[Misdirection] Perf: ...".
    /// </summary>
    public sealed class QuestPerformance : MonoBehaviour
    {
        [Tooltip("Fewer pixels shaded toward the edges of the lenses. High suits a light scene like this one.")]
        [SerializeField] private OVRManager.FoveatedRenderingLevel foveation = OVRManager.FoveatedRenderingLevel.High;
        [Tooltip("Let the headset use less foveation while the GPU has headroom (never more than the level above).")]
        [SerializeField] private bool dynamicFoveation = true;
        [SerializeField] private OVRManager.ProcessorPerformanceLevel cpuLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
        [SerializeField] private OVRManager.ProcessorPerformanceLevel gpuLevel = OVRManager.ProcessorPerformanceLevel.SustainedHigh;
        [Tooltip("Display refresh rate to ask for (Hz). 72 is the Day 12 target; the rules ask for at least 60 fps.")]
        [SerializeField] private float displayHz = 72f;
        [Tooltip("Also apply in the Editor and the Meta XR Simulator (off: these settings only matter on the headset).")]
        [SerializeField] private bool applyInEditor;
        [Tooltip("Ask again after this many seconds (0 = only once).")]
        [SerializeField] private float reapplyAfterSeconds = 2f;

        /// <summary>What the last request set, for logs and the smoke report.</summary>
        public static string LastApplied { get; private set; }

        private IEnumerator Start()
        {
            Apply();
            if (reapplyAfterSeconds > 0f)
            {
                yield return new WaitForSecondsRealtime(reapplyAfterSeconds);
                Apply();
            }
        }

        /// <summary>Makes the requests now. Safe to call again.</summary>
        public void Apply()
        {
            string wanted = "foveation " + foveation + (dynamicFoveation ? " (dynamic)" : "") + ", CPU " + cpuLevel +
                            ", GPU " + gpuLevel + ", " + displayHz.ToString("F0") + " Hz";
            if (Application.isEditor && !applyInEditor)
            {
                Report("not applied in the Editor (on the headset: " + wanted + ")");
                return;
            }
            bool present;
            try
            {
                present = OVRManager.isHmdPresent;
            }
            catch (Exception e)
            {
                Report("no headset (" + e.Message + ")");
                return;
            }
            if (!present)
            {
                Report("no headset present; nothing applied");
                return;
            }
            Report(Foveation() + ", " + Processors() + ", " + Display());
        }

        private string Foveation()
        {
            try
            {
                if (!OVRManager.fixedFoveatedRenderingSupported)
                {
                    return "foveation not supported here";
                }
                OVRManager.foveatedRenderingLevel = foveation;
                OVRManager.useDynamicFoveatedRendering = dynamicFoveation;
                return "foveation " + OVRManager.foveatedRenderingLevel + (OVRManager.useDynamicFoveatedRendering ? " (dynamic)" : "");
            }
            catch (Exception e)
            {
                return "foveation failed (" + e.Message + ")";
            }
        }

        private string Processors()
        {
            try
            {
                OVRManager.suggestedCpuPerfLevel = cpuLevel;
                OVRManager.suggestedGpuPerfLevel = gpuLevel;
                return "CPU " + OVRManager.suggestedCpuPerfLevel + ", GPU " + OVRManager.suggestedGpuPerfLevel;
            }
            catch (Exception e)
            {
                return "CPU/GPU levels failed (" + e.Message + ")";
            }
        }

        private string Display()
        {
            try
            {
                OVRDisplay display = OVRManager.display;
                if (display == null)
                {
                    return "no display";
                }
                float[] available = display.displayFrequenciesAvailable;
                bool offered = false;
                if (available != null)
                {
                    for (int i = 0; i < available.Length; i++)
                    {
                        offered |= Mathf.Abs(available[i] - displayHz) < 0.5f;
                    }
                }
                if (offered && Mathf.Abs(display.displayFrequency - displayHz) >= 0.5f)
                {
                    display.displayFrequency = displayHz;
                }
                return display.displayFrequency.ToString("F0") + " Hz" + (offered ? "" : " (" + displayHz.ToString("F0") + " Hz not offered)");
            }
            catch (Exception e)
            {
                return "display rate failed (" + e.Message + ")";
            }
        }

        private static void Report(string what)
        {
            if (what == LastApplied)
            {
                return;
            }
            LastApplied = what;
            Debug.Log("[Misdirection] Perf: headset settings: " + what);
        }

        /// <summary>Play mode can restart without reloading scripts.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            LastApplied = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        /// <summary>
        /// Safety net: the bootstrap adds this component to each scene (PerformanceSetup), but a scene saved
        /// before Day 12 has none, and without it the headset would run at default settings. So if a scene
        /// with an OVRManager loads without one, add it (and the perf log) for this run.
        /// </summary>
        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindFirstObjectByType<QuestPerformance>(FindObjectsInactive.Include) != null ||
                FindFirstObjectByType<OVRManager>(FindObjectsInactive.Include) == null)
            {
                return;
            }
            GameObject host = new GameObject("Misdirection Performance (added at runtime)");
            host.AddComponent<QuestPerformance>();
            if (FindFirstObjectByType<PerfLogger>(FindObjectsInactive.Include) == null)
            {
                host.AddComponent<PerfLogger>();
            }
            Debug.Log("[Misdirection] Perf: scene '" + scene.name + "' has no performance settings object; added one for this run. " +
                      "Run Misdirection > Setup > Run Project Bootstrap to save it into the scene.");
        }
    }
}
