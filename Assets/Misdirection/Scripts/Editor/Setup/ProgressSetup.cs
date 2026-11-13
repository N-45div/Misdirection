using System;
using Misdirection.Audience;
using Misdirection.Core.Crowd;
using Misdirection.Core.Progress;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;
using Misdirection.Progress;
using Misdirection.Show;
using Misdirection.Table;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.EditorTools
{
    /// <summary>
    /// Day 10 helpers: the Progress Director (daily crowd, daily challenge, trick book, streak, the
    /// returning Regular) and the menu items for testing "day two" without waiting a day.
    /// </summary>
    public static class ProgressSetup
    {
        [MenuItem("Misdirection/Day 10/Add Progress Director (daily show, trick book) to Open Scene", priority = 1000)]
        public static void AddProgressDirectorMenu()
        {
            if (UnityEngine.Object.FindFirstObjectByType<ShowDirector>(FindObjectsInactive.Include) == null)
            {
                EditorUtility.DisplayDialog("Progress Director",
                    "Progress follows the show: add the Show Director first (Misdirection > Day 9), or open the bootstrap's Show.unity.", "OK");
                return;
            }
            EnsureProgressDirector(true);
            Selection.activeObject = UnityEngine.Object.FindFirstObjectByType<ProgressDirector>(FindObjectsInactive.Include);
        }

        [MenuItem("Misdirection/Day 10/Reset Progress", priority = 1001)]
        public static void ResetProgressMenu()
        {
            ProgressStore.Delete();
            ProgressStore.DayOffset = 0;
            ProgressDirector running = Application.isPlaying ? UnityEngine.Object.FindFirstObjectByType<ProgressDirector>() : null;
            if (running != null)
            {
                running.ReloadProgress();
            }
            Debug.Log("[Misdirection] Progress reset: no shows, no stars, no streak, and the date is today's again (" + ProgressStore.Today() +
                      "). The tutorial flag is separate (Misdirection > Day 9).");
        }

        [MenuItem("Misdirection/Day 10/Simulate Next Day", priority = 1002)]
        public static void SimulateNextDayMenu()
        {
            ProgressStore.DayOffset = ProgressStore.DayOffset + 1;
            Debug.Log("[Misdirection] Simulating the next day: the game now thinks it is " + ProgressStore.Today() + " (" + ProgressStore.DayOffset +
                      " day(s) ahead). The new day's crowd and challenge arrive with the next show" +
                      (Application.isPlaying ? ": finish this one and pick up the coin." : ": press Play.") +
                      " Editor and development builds only.");
        }

        [MenuItem("Misdirection/Day 10/Back to Today (clear the simulated date)", priority = 1003)]
        public static void BackToTodayMenu()
        {
            ProgressStore.DayOffset = 0;
            Debug.Log("[Misdirection] The date is today's again (" + ProgressStore.Today() + "). Saved progress is untouched: a save " +
                      "\"from the future\" just waits until the real date catches up (Reset Progress clears it).");
        }

        /// <summary>
        /// Testing without hands: books one clean show of today's plan through the real rules (the coin
        /// produced from the Kid's ear every time), exactly as if it had just been played and saved.
        /// Then "Simulate Next Day" shows what a returning player sees.
        /// </summary>
        [MenuItem("Misdirection/Day 10/Book a Practice Show Today (testing)", priority = 1004)]
        public static void BookPracticeShowMenu()
        {
            ProgressData data = ProgressStore.Load();
            int today = ProgressStore.Today();
            DailyPlan plan = ProgressRules.PlanFor(data, today);
            ShowSettings settings = DailyShow.ToSettings(plan);
            ShowFlow flow = new ShowFlow(settings, true);
            ShowRecord record = new ShowRecord(plan.Acts);
            flow.TableReady(0);
            for (int act = 0; act < plan.Acts.Length; act++)
            {
                Archetype[] crowd = plan.Acts[act].Crowd;
                int trick = plan.Acts[act].Trick;
                RevealResult vanish = new RevealResult { Kind = "vanish", Amazement = 1f, Tips = 5 * crowd.Length, Reaction = Reaction.Applause, SeenBy = -1, Spectator = -1 };
                RevealResult second = vanish;
                second.Kind = trick == 0 ? ShowRecord.Produce : "cup";
                second.Tips = 6 * crowd.Length;
                second.Spectator = trick == 0 ? Mathf.Max(0, Array.IndexOf(crowd, Archetype.Kid)) : -1;
                foreach (RevealResult reveal in new[] { vanish, second })
                {
                    flow.Revealed(reveal);
                    record.AddReveal(act, trick, reveal, crowd);
                }
                flow.TrickCompleted(60 * (act + 1));
            }
            ShowOutcome outcome = ProgressRules.FinishShow(data, today, plan.Challenge, record, flow.LastSummary);
            ProgressStore.Save(data);
            ProgressDirector running = Application.isPlaying ? UnityEngine.Object.FindFirstObjectByType<ProgressDirector>() : null;
            if (running != null)
            {
                running.ReloadProgress();
            }
            Debug.Log("[Misdirection] Booked a practice show for " + today + ": day " + outcome.DayNumber + ", " + ProgressText.StreakLine(outcome.Streak) +
                      ", " + flow.LastSummary.Tips + " tips. " + ProgressText.ChallengeLine(plan.Challenge, outcome.ChallengePassedToday) + ".");
        }

        [MenuItem("Misdirection/Day 10/Preview Trick Book (Play mode)", priority = 1005)]
        public static void PreviewBookMenu()
        {
            ProgressDirector running = Application.isPlaying ? UnityEngine.Object.FindFirstObjectByType<ProgressDirector>() : null;
            if (running == null)
            {
                Debug.Log("[Misdirection] Press Play in Show.unity first: the trick book stands on the table once the table is set.");
                return;
            }
            running.PreviewBook(20f);
            Debug.Log("[Misdirection] Trick book on the table for 20 s (left of the mat), with the saved progress.");
        }

        [MenuItem("Misdirection/Day 10/Log Saved Progress", priority = 1006)]
        public static void LogProgressMenu()
        {
            ProgressData data = ProgressStore.Load();
            int today = ProgressStore.Today();
            DailyPlan plan = ProgressRules.PlanFor(data, today);
            Debug.Log("[Misdirection] Progress at " + ProgressStore.FilePath + "\n" +
                      "Today " + today + " (offset " + ProgressStore.DayOffset + "): day " + ProgressRules.DayNumber(data, today) +
                      ", streak " + ProgressRules.StreakOn(data, today) + " (longest " + data.LongestStreak + ")\n" +
                      "Crowd: " + ProgressText.CrowdLine(plan.Acts) + "\n" +
                      ProgressText.ChallengeLine(plan.Challenge, ProgressRules.ChallengePassedOn(data, today)) + "\n" +
                      "Shows " + data.ShowsPlayed + " (" + data.ShowsFinished + " to the bow), best " + data.BestTips + " tips, " + data.BestStars + " stars\n" +
                      "Trick book: " + TrickBook.NameOf(0) + " " + data.Tricks[0].Stars + "/3, " + TrickBook.NameOf(1) + " " + data.Tricks[1].Stars + "/3\n" +
                      JsonUtility.ToJson(data, true));
        }

        /// <summary>
        /// Day 10: adds the ProgressDirector next to the ShowDirector, wired to it and to its crowd,
        /// table and look source. Safe to run again. Returns true if the scene changed.
        /// </summary>
        public static bool EnsureProgressDirector(bool interactive)
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
            if (UnityEngine.Object.FindFirstObjectByType<ProgressDirector>(FindObjectsInactive.Include) != null)
            {
                return false;
            }

            GameObject go = new GameObject("Misdirection Progress");
            Undo.RegisterCreatedObjectUndo(go, "Add Progress Director");
            // A child of the show, so the two travel together.
            go.transform.SetParent(show.transform, false);
            ProgressDirector progress = go.AddComponent<ProgressDirector>();

            SerializedObject from = new SerializedObject(show);
            SerializedObject to = new SerializedObject(progress);
            to.FindProperty("show").objectReferenceValue = show;
            foreach (string field in new[] { "crowd", "table", "lookSource" })
            {
                SerializedProperty source = from.FindProperty(field);
                SerializedProperty target = to.FindProperty(field);
                if (source != null && target != null)
                {
                    target.objectReferenceValue = source.objectReferenceValue;
                }
            }
            if (to.FindProperty("crowd").objectReferenceValue == null)
            {
                to.FindProperty("crowd").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<Crowd>(FindObjectsInactive.Include);
            }
            if (to.FindProperty("table").objectReferenceValue == null)
            {
                to.FindProperty("table").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<TableCalibrator>(FindObjectsInactive.Include);
            }
            if (to.FindProperty("lookSource").objectReferenceValue == null)
            {
                to.FindProperty("lookSource").objectReferenceValue = UnityEngine.Object.FindFirstObjectByType<LookSource>(FindObjectsInactive.Include);
            }
            to.ApplyModifiedPropertiesWithoutUndo();

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Misdirection] Added the Progress Director: a new crowd and challenge every day, the trick book at the bow, " +
                      "streaks, and a Regular who remembers. Saved on the device only.");
            return true;
        }
    }
}
