using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Signed Quest APK builds from the menu or the command line.
    ///
    /// Signing comes from environment variables, or from the untracked file
    /// secrets/android-signing.properties (see docs/days/day-01.md):
    ///   MISDIRECTION_KEYSTORE / keystore         absolute path to the .keystore
    ///   MISDIRECTION_KEYSTORE_PASS / keystore_pass
    ///   MISDIRECTION_KEY_ALIAS / key_alias
    ///   MISDIRECTION_KEY_PASS / key_pass
    /// Optional: MISDIRECTION_APP_ID, MISDIRECTION_VERSION_CODE, MISDIRECTION_SCENES (comma-separated),
    ///           MISDIRECTION_DEV_BUILD=1, MISDIRECTION_ALLOW_DEBUG_SIGNING=1.
    ///
    /// Command line:
    ///   Unity -batchmode -quit -projectPath . -buildTarget Android \
    ///         -executeMethod Misdirection.EditorTools.MisdirectionBuild.BuildFromCommandLine
    /// </summary>
    public static class MisdirectionBuild
    {
        /// <summary>The Meta dashboard locks the package name on first upload. Change it before Day 1's upload, never after.</summary>
        public const string DefaultApplicationId = "com.n45div.misdirection";

        public const string OutputDirectory = "Builds/Android";
        public const string SigningFile = "secrets/android-signing.properties";

        // Version codes must rise with every dashboard upload: minutes since the competition window opened.
        private static readonly DateTime VersionEpoch = new DateTime(2026, 9, 24, 0, 0, 0, DateTimeKind.Utc);

        [MenuItem("Misdirection/Build/Signed Release APK", priority = 10)]
        public static void BuildRelease()
        {
            Run(false, false);
        }

        [MenuItem("Misdirection/Build/Signed Release APK + Install on Quest", priority = 11)]
        public static void BuildReleaseAndRun()
        {
            Run(false, true);
        }

        [MenuItem("Misdirection/Build/Development APK + Install on Quest", priority = 30)]
        public static void BuildDevelopmentAndRun()
        {
            Run(true, true);
        }

        public static void BuildFromCommandLine()
        {
            bool development = Value("MISDIRECTION_DEV_BUILD", null) == "1";
            bool ok = Run(development, false);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        private static bool Run(bool development, bool autoRun)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android
                && !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
            {
                return Fail("Could not switch to Android. Install Android Build Support (OpenJDK, SDK & NDK) in Unity Hub.");
            }

            string[] scenes = ResolveScenes();
            if (scenes.Length == 0)
            {
                return Fail("No scenes to build. Enable scenes in Build Profiles or set MISDIRECTION_SCENES.");
            }

            Dictionary<string, string> signing = ReadSigningFile();
            string keystore = Value("MISDIRECTION_KEYSTORE", Lookup(signing, "keystore"));
            bool customKeystore = !string.IsNullOrEmpty(keystore);

            if (!development && !customKeystore && Value("MISDIRECTION_ALLOW_DEBUG_SIGNING", null) != "1")
            {
                return Fail("Release builds must be signed with the team keystore. Set MISDIRECTION_KEYSTORE " +
                            "or create " + SigningFile + " (see docs/days/day-01.md).");
            }
            if (customKeystore && !File.Exists(keystore))
            {
                return Fail("Keystore not found: " + keystore);
            }

            // Project identity: persisted on purpose.
            NamedBuildTarget android = NamedBuildTarget.Android;
            string appId = Value("MISDIRECTION_APP_ID", null);
            string currentId = PlayerSettings.GetApplicationIdentifier(android);
            if (!string.IsNullOrEmpty(appId))
            {
                PlayerSettings.SetApplicationIdentifier(android, appId);
            }
            else if (string.IsNullOrEmpty(currentId) || currentId.StartsWith("com.DefaultCompany", StringComparison.Ordinal) || currentId.StartsWith("com.Unity", StringComparison.Ordinal))
            {
                PlayerSettings.SetApplicationIdentifier(android, DefaultApplicationId);
            }
            PlayerSettings.SetScriptingBackend(android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            EditorUserBuildSettings.buildAppBundle = false;

            // Per-build values: restored afterwards so ProjectSettings does not churn in git.
            int oldVersionCode = PlayerSettings.Android.bundleVersionCode;
            string oldVersionName = PlayerSettings.bundleVersion;
            bool oldUseCustom = PlayerSettings.Android.useCustomKeystore;
            string oldKeystore = PlayerSettings.Android.keystoreName;
            string oldAlias = PlayerSettings.Android.keyaliasName;

            int versionCode = ResolveVersionCode();
            string versionName = "0.1." + versionCode;
            string apkName = "Misdirection-" + versionName + (development ? "-dev" : "") + ".apk";
            string apkPath = Path.Combine(OutputDirectory, apkName);

            try
            {
                PlayerSettings.Android.bundleVersionCode = versionCode;
                PlayerSettings.bundleVersion = versionName;

                if (customKeystore)
                {
                    PlayerSettings.Android.useCustomKeystore = true;
                    PlayerSettings.Android.keystoreName = keystore;
                    PlayerSettings.Android.keystorePass = Value("MISDIRECTION_KEYSTORE_PASS", Lookup(signing, "keystore_pass")) ?? "";
                    PlayerSettings.Android.keyaliasName = Value("MISDIRECTION_KEY_ALIAS", Lookup(signing, "key_alias")) ?? "";
                    PlayerSettings.Android.keyaliasPass = Value("MISDIRECTION_KEY_PASS", Lookup(signing, "key_pass")) ?? "";
                }
                else
                {
                    PlayerSettings.Android.useCustomKeystore = false;
                }

                Directory.CreateDirectory(OutputDirectory);

                BuildPlayerOptions options = new BuildPlayerOptions();
                options.scenes = scenes;
                options.locationPathName = apkPath;
                options.target = BuildTarget.Android;
                options.targetGroup = BuildTargetGroup.Android;
                options.options = BuildOptions.None;
                if (development)
                {
                    options.options |= BuildOptions.Development;
                }
                if (autoRun)
                {
                    options.options |= BuildOptions.AutoRunPlayer;
                }

                Debug.Log("[Misdirection] Building " + apkPath + " (" + PlayerSettings.GetApplicationIdentifier(android) +
                          ", versionCode " + versionCode + ", " + (customKeystore ? "team keystore" : "debug keystore") +
                          ") scenes: " + string.Join(", ", scenes));

                BuildReport report = BuildPipeline.BuildPlayer(options);
                if (report.summary.result != BuildResult.Succeeded)
                {
                    return Fail("Build " + report.summary.result + " with " + report.summary.totalErrors + " error(s). See the Console.");
                }

                File.WriteAllText(Path.Combine(OutputDirectory, "latest.txt"),
                    "apk=" + Path.GetFullPath(apkPath) + "\n" +
                    "version_code=" + versionCode + "\n" +
                    "version_name=" + versionName + "\n" +
                    "app_id=" + PlayerSettings.GetApplicationIdentifier(android) + "\n" +
                    "signed_with=" + (customKeystore ? "team" : "debug") + "\n" +
                    "development=" + (development ? "1" : "0") + "\n");

                Debug.Log("[Misdirection] Build OK: " + Path.GetFullPath(apkPath) + " (" + (report.summary.totalSize / (1024 * 1024)) + " MB)");
                return true;
            }
            finally
            {
                PlayerSettings.Android.bundleVersionCode = oldVersionCode;
                PlayerSettings.bundleVersion = oldVersionName;
                PlayerSettings.Android.useCustomKeystore = oldUseCustom;
                PlayerSettings.Android.keystoreName = oldKeystore;
                PlayerSettings.Android.keyaliasName = oldAlias;
                PlayerSettings.Android.keystorePass = "";
                PlayerSettings.Android.keyaliasPass = "";
            }
        }

        private static string[] ResolveScenes()
        {
            List<string> result = new List<string>();
            string fromEnv = Value("MISDIRECTION_SCENES", null);
            if (!string.IsNullOrEmpty(fromEnv))
            {
                foreach (string part in fromEnv.Split(','))
                {
                    string scene = part.Trim();
                    if (scene.Length > 0)
                    {
                        result.Add(scene);
                    }
                }
                return result.ToArray();
            }

            foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
            {
                if (scene.enabled && !string.IsNullOrEmpty(scene.path))
                {
                    result.Add(scene.path);
                }
            }
            return result.ToArray();
        }

        private static int ResolveVersionCode()
        {
            string fromEnv = Value("MISDIRECTION_VERSION_CODE", null);
            int parsed;
            if (!string.IsNullOrEmpty(fromEnv) && int.TryParse(fromEnv, out parsed) && parsed > 0)
            {
                return parsed;
            }
            return Math.Max(1, (int)(DateTime.UtcNow - VersionEpoch).TotalMinutes);
        }

        private static Dictionary<string, string> ReadSigningFile()
        {
            Dictionary<string, string> values = new Dictionary<string, string>();
            if (!File.Exists(SigningFile))
            {
                return values;
            }
            foreach (string raw in File.ReadAllLines(SigningFile))
            {
                string line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                {
                    continue;
                }
                int eq = line.IndexOf('=');
                if (eq > 0)
                {
                    values[line.Substring(0, eq).Trim()] = line.Substring(eq + 1).Trim();
                }
            }
            return values;
        }

        private static string Lookup(Dictionary<string, string> values, string key)
        {
            string value;
            return values.TryGetValue(key, out value) ? value : null;
        }

        private static string Value(string envName, string fallback)
        {
            string value = Environment.GetEnvironmentVariable(envName);
            return string.IsNullOrEmpty(value) ? fallback : value;
        }

        private static bool Fail(string message)
        {
            Debug.LogError("[Misdirection] " + message);
            if (!Application.isBatchMode)
            {
                EditorUtility.DisplayDialog("Misdirection build", message, "OK");
            }
            return false;
        }
    }

    /// <summary>Warns when the Meta XR Core SDK is missing or off the v207 lock (our runtime code is disabled without it).</summary>
    [InitializeOnLoad]
    internal static class MetaSdkVersionCheck
    {
        private const string LockedMajor = "207.";

        static MetaSdkVersionCheck()
        {
            EditorApplication.delayCall += Check;
        }

        private static void Check()
        {
            UnityEditor.PackageManager.PackageInfo info =
                UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.meta.xr.sdk.core/package.json");
            if (info == null)
            {
                Debug.LogWarning("[Misdirection] Meta XR Core SDK (com.meta.xr.sdk.core) is not installed, so the Misdirection " +
                                 "runtime scripts are switched off. Install Meta XR All-in-One SDK v207 (docs/days/day-01.md).");
                return;
            }
            if (!info.version.StartsWith(LockedMajor, StringComparison.Ordinal))
            {
                Debug.LogWarning("[Misdirection] Meta XR Core SDK is " + info.version + ". The team is locked to v207 for the whole " +
                                 "build; do not upgrade mid-sprint.");
            }
        }
    }
}
