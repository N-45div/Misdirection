using System;
using System.Collections.Generic;
using System.IO;
using Misdirection.Core.Progress;
using UnityEngine;

namespace Misdirection.Progress
{
    /// <summary>
    /// Saved progress on this device: persistentDataPath/progress.json. Local only, no accounts and
    /// no Platform SDK. Saves are atomic (written to a temporary file, then swapped in, keeping the
    /// previous save as a backup), and loading never throws: a missing file starts fresh, a corrupt
    /// one is set aside for the backup, and anything loaded is repaired by ProgressSave.Upgrade.
    /// Also answers "what day is it?" for the daily show, with a development-only day offset.
    /// </summary>
    public static class ProgressStore
    {
        public const string FileName = "progress.json";

        /// <summary>
        /// PlayerPrefs key: days added to today's date, to test "day two" without waiting. Only read in
        /// the Editor and in development builds; a release build always uses the real date.
        /// </summary>
        public const string DayOffsetKey = "misdirection.dev.dayOffset";

        public static string FilePath
        {
            get { return Path.Combine(Application.persistentDataPath, FileName); }
        }

        private static string TempPath { get { return FilePath + ".tmp"; } }
        private static string BackupPath { get { return FilePath + ".bak"; } }
        private static string CorruptPath { get { return FilePath + ".corrupt"; } }

        /// <summary>The day offset is honoured here only: the Editor and development builds.</summary>
        public static bool DevDatesAllowed
        {
            get { return Application.isEditor || Debug.isDebugBuild; }
        }

        /// <summary>Days added to the real date (0 in release builds, whatever was stored).</summary>
        public static int DayOffset
        {
            get { return DevDatesAllowed ? PlayerPrefs.GetInt(DayOffsetKey, 0) : 0; }
            set
            {
                if (value == 0)
                {
                    PlayerPrefs.DeleteKey(DayOffsetKey);
                }
                else
                {
                    PlayerPrefs.SetInt(DayOffsetKey, value);
                }
                PlayerPrefs.Save();
            }
        }

        /// <summary>Today's local calendar date as yyyymmdd (plus the development day offset).</summary>
        public static int Today()
        {
            DateTime now = DateTime.Now;
            int offset = DayOffset;
            if (offset != 0)
            {
                try
                {
                    now = now.AddDays(offset);
                }
                catch (ArgumentOutOfRangeException)
                {
                    // An absurd offset: ignore it.
                }
            }
            return CalendarDay.From(now);
        }

        /// <summary>
        /// Loads the saved progress. Never throws and never returns null: no file means a fresh start;
        /// an unreadable file is logged, set aside, and the backup is used instead. A save that was
        /// interrupted after its temporary file was written is picked up too.
        /// </summary>
        public static ProgressData Load()
        {
            string problem;
            ProgressData main = Read(FilePath, out problem);
            bool corrupt = main == null && problem != null;
            if (corrupt)
            {
                Debug.LogWarning("[Misdirection] Progress: " + FileName + " can't be read (" + problem + "). Trying the backup.");
                SetAside(FilePath);
            }

            // An interrupted save leaves the newer copy in .tmp; the backup only matters when the file is gone.
            List<ProgressData> copies = new List<ProgressData>();
            if (main != null)
            {
                copies.Add(main);
            }
            string ignored;
            ProgressData interrupted = Read(TempPath, out ignored);
            if (interrupted != null)
            {
                copies.Add(interrupted);
            }
            if (main == null)
            {
                ProgressData backup = Read(BackupPath, out ignored);
                if (backup != null)
                {
                    copies.Add(backup);
                }
            }

            if (copies.Count == 0)
            {
                if (corrupt)
                {
                    Debug.LogWarning("[Misdirection] Progress: no usable backup either. Starting fresh.");
                }
                else
                {
                    Debug.Log("[Misdirection] Progress: no saved progress yet. Starting fresh.");
                }
                return new ProgressData();
            }
            ProgressData loaded = Finish(copies[0]);
            for (int i = 1; i < copies.Count; i++)
            {
                loaded = ProgressSave.Merge(loaded, Finish(copies[i]));
            }
            if (main == null || interrupted != null)
            {
                Debug.LogWarning("[Misdirection] Progress: recovered from " + (main == null ? "the backup" : "an interrupted save") + ".");
                // Put a good progress.json back straight away (this also clears the temporary file).
                Save(loaded);
            }
            return loaded;
        }

        /// <summary>Writes the progress atomically. Returns false (and logs) if it couldn't; never throws.</summary>
        public static bool Save(ProgressData data)
        {
            if (data == null)
            {
                return false;
            }
            try
            {
                string json = JsonUtility.ToJson(data, true);
                string directory = Path.GetDirectoryName(FilePath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }
                // Write the whole new save beside the old one first: a crash mid-write can't damage progress.json.
                File.WriteAllText(TempPath, json);
                if (File.Exists(FilePath))
                {
                    try
                    {
                        File.Replace(TempPath, FilePath, BackupPath);
                    }
                    catch (Exception)
                    {
                        // File.Replace isn't available on every file system: do the same in two steps.
                        File.Copy(FilePath, BackupPath, true);
                        File.Delete(FilePath);
                        File.Move(TempPath, FilePath);
                    }
                }
                else
                {
                    File.Move(TempPath, FilePath);
                }
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Misdirection] Progress: couldn't save (" + e.Message + "). Progress from this show may be lost.");
                return false;
            }
        }

        /// <summary>Forgets everything saved (the file, its backup and any interrupted save). For testing.</summary>
        public static void Delete()
        {
            foreach (string path in new[] { FilePath, TempPath, BackupPath, CorruptPath })
            {
                try
                {
                    if (File.Exists(path))
                    {
                        File.Delete(path);
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Misdirection] Progress: couldn't delete " + path + " (" + e.Message + ").");
                }
            }
        }

        /// <summary>Reads one file. Null with no <paramref name="problem"/> = no such file; null with one = unreadable.</summary>
        private static ProgressData Read(string path, out string problem)
        {
            problem = null;
            try
            {
                if (!File.Exists(path))
                {
                    return null;
                }
                string json = File.ReadAllText(path);
                ProgressData data = string.IsNullOrEmpty(json) || json.Trim().Length == 0 ? null : JsonUtility.FromJson<ProgressData>(json);
                if (data == null)
                {
                    problem = "the file is empty";
                }
                return data;
            }
            catch (Exception e)
            {
                // Bad JSON (ArgumentException), a locked file, no permission, ...: all the same to the player.
                problem = e.GetType().Name + ": " + e.Message;
                return null;
            }
        }

        /// <summary>Moves an unreadable save out of the way (kept as .corrupt for a look later), so it never replaces the good backup.</summary>
        private static void SetAside(string path)
        {
            try
            {
                File.Copy(path, CorruptPath, true);
                File.Delete(path);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Misdirection] Progress: couldn't set the unreadable file aside (" + e.Message + ").");
            }
        }

        private static ProgressData Finish(ProgressData loaded)
        {
            if (ProgressSave.IsFromNewerBuild(loaded))
            {
                Debug.LogWarning("[Misdirection] Progress: saved by a newer build (format " + loaded.Version + ", this build writes " +
                                 ProgressData.CurrentVersion + "). Keeping what this build understands.");
            }
            return ProgressSave.Upgrade(loaded);
        }
    }
}
