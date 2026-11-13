using System;
using Misdirection.Audience;
using Misdirection.Core.Crowd;
using Misdirection.Core.Progress;
using Misdirection.Core.Show;
using Misdirection.Core.Tricks;
using Misdirection.Show;
using Misdirection.Table;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Misdirection.Progress
{
    /// <summary>
    /// The reason to come back (Day 10). Everything is saved on the device; nothing goes online.
    ///  - Hands the Show Director today's seeded crowd before the first act (a new line-up every
    ///    calendar day; the Regular only returns once you have played on an earlier day).
    ///  - Shows a short banner at the start of the first act: the day, today's crowd, the challenge.
    ///  - Listens to the show (reveals, busts, the end) and, at the bow or walk-off, books the show:
    ///    days and streak, best tips, trick book stars, the daily challenge, the Regular's memory.
    ///    Then saves, and stands the trick book on the table beside the mat.
    ///  - Makes the returning Regular act on what he remembers: wary if you keep repeating yourself,
    ///    "Same as yesterday!" when the coin comes from the same ear, relaxed by something new.
    /// Rules live in Misdirection.Core.Progress; this wires them to the show, the crowd and the table.
    /// </summary>
    [DefaultExecutionOrder(45)]
    public sealed class ProgressDirector : MonoBehaviour
    {
        [SerializeField] private ShowDirector show;
        [SerializeField] private Crowd crowd;
        [SerializeField] private TableCalibrator table;
        [SerializeField] private LookSource lookSource;

        [Header("Daily show")]
        [Tooltip("Use the day's seeded crowd. Off: the Show Director's own acts (the fixed Day 9 show); the challenge, trick book and streak still work.")]
        [SerializeField] private bool dailyCrowd = true;
        [Tooltip("How long the day banner (day, today's crowd, challenge) shows at the start of the first act (seconds).")]
        [SerializeField] private float dayBannerSeconds = 4f;
        [Tooltip("Where the day banner floats, in table space: x to the right, y up, z away from the player (metres).")]
        [SerializeField] private Vector3 dayBannerLocal = new Vector3(0f, 0.47f, 0.55f);

        [Header("Trick book")]
        [Tooltip("Where the trick book stands at the bow, in table space (metres): beside the mat, inside the table.")]
        [SerializeField] private Vector3 bookLocal = new Vector3(-0.25f, 0.135f, 0.40f);

        [Header("The returning Regular")]
        [SerializeField] private RegularSettings regular = new RegularSettings();
        [Tooltip("How long his line (\"Same as yesterday!\") floats above his head (seconds).")]
        [SerializeField] private float regularLineSeconds = 2.5f;

        private ProgressData data;
        private int today;
        private DailyPlan plan;
        private bool planIsStale;
        private ShowRecord record;
        private ShowOutcome lastOutcome;
        private bool subscribed;

        private TextMeshPro dayBanner;
        private double dayBannerUntil = -1;
        private TrickBookView book;
        private double bookPreviewUntil = -1;

        private TextMeshPro regularLine;
        private double regularLineStart;
        private double regularLineUntil = -1;
        private Vector3 regularLineAnchor;
        private SpectatorMind primedRegular;
        private bool regularRelaxed;
        private RegularReaction pendingReaction = RegularReaction.None;

        /// <summary>The saved progress (never null once awake).</summary>
        public ProgressData Data { get { return data; } }

        /// <summary>The calendar day (yyyymmdd) the current plan is for.</summary>
        public int Today { get { return today; } }

        /// <summary>Which day of playing this is: 1 on the first day, 2 on the second day played, ...</summary>
        public int DayNumber { get { return ProgressRules.DayNumber(data, today); } }

        /// <summary>Days in a row up to today (0 once a day has been missed, until today's first show ends).</summary>
        public int Streak { get { return ProgressRules.StreakOn(data, today); } }

        public DailyPlan Plan { get { return plan; } }
        public DailyChallenge Challenge { get { return plan != null ? plan.Challenge : null; } }
        public string ChallengeText { get { return plan != null && plan.Challenge != null ? plan.Challenge.Text : ""; } }
        public bool ChallengePassedToday { get { return ProgressRules.ChallengePassedOn(data, today); } }

        /// <summary>What the last finished show changed (null before the first one this session).</summary>
        public ShowOutcome LastOutcome { get { return lastOutcome; } }

        public void Configure(ShowDirector showDirector, Crowd audience, TableCalibrator tableCalibrator, LookSource look)
        {
            show = showDirector;
            crowd = audience;
            table = tableCalibrator;
            lookSource = look;
        }

        /// <summary>
        /// Testing: reads the save again (after "Reset Progress" while playing). Before the first act the
        /// new plan applies at once; with a show under way it waits for the next one, so the crowd never
        /// swaps under you.
        /// </summary>
        public void ReloadProgress()
        {
            data = LoadSafely();
            lastOutcome = null;
            if (book != null)
            {
                book.Hide();
            }
            PlanToday(!ShowUnderWay());
        }

        /// <summary>
        /// Testing: stands the trick book up for a while with the saved progress, without playing a
        /// show to its bow (handy in the simulator, where a whole show takes a steady webcam).
        /// </summary>
        public void PreviewBook(float seconds)
        {
            if (book == null || data == null)
            {
                return;
            }
            ShowOutcome preview = new ShowOutcome();
            preview.Date = today;
            preview.DayNumber = DayNumber;
            preview.Streak = Math.Max(1, Streak);
            preview.ChallengePassedToday = ChallengePassedToday;
            preview.StarsGained = new int[data.Tricks.Length];
            book.Show(data, Challenge, preview, ProgressRules.IsReturning(data, today));
            bookPreviewUntil = Time.unscaledTimeAsDouble + seconds;
        }

        // ------------------------------------------------------------ lifecycle

        /// <summary>
        /// Scenes saved before Day 10 have no ProgressDirector yet (the bootstrap adds one). So that a
        /// build from such a scene still has progression, add one at runtime wherever there is a show.
        /// To switch progression off, leave a disabled ProgressDirector in the scene.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AddToShowScenes()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            AddIfMissing();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            AddIfMissing();
        }

        private static void AddIfMissing()
        {
            if (FindFirstObjectByType<ShowDirector>() == null || FindFirstObjectByType<ProgressDirector>(FindObjectsInactive.Include) != null)
            {
                return;
            }
            GameObject go = new GameObject("Misdirection Progress (added at runtime)");
            go.AddComponent<ProgressDirector>();
            Debug.Log("[Misdirection] Progress: no Progress Director in this scene, added one for this run. " +
                      "Run the bootstrap (or Misdirection > Day 10) to save it into the scene.");
        }

        private void Awake()
        {
            if (show == null)
            {
                show = FindFirstObjectByType<ShowDirector>();
            }
            if (crowd == null)
            {
                crowd = FindFirstObjectByType<Crowd>();
            }
            if (table == null)
            {
                table = FindFirstObjectByType<TableCalibrator>();
            }
            if (lookSource == null)
            {
                lookSource = FindFirstObjectByType<LookSource>();
            }
            data = LoadSafely();
            today = ProgressStore.Today();

            dayBanner = WorldText.Create(transform, "Day Banner", new Vector2(0.62f, 0.16f), 0.45f);
            // Three short lines that shrink to fit rather than wrap.
            dayBanner.textWrappingMode = TextWrappingModes.NoWrap;
            dayBanner.gameObject.SetActive(false);
            regularLine = WorldText.Create(transform, "Regular Line", new Vector2(0.5f, 0.12f), 0.5f);
            regularLine.gameObject.SetActive(false);
            book = new TrickBookView(transform);
        }

        private void OnEnable()
        {
            if (show != null && !subscribed)
            {
                show.ShowStarted += OnShowStarted;
                show.ShowRevealed += OnShowRevealed;
                show.ShowBusted += OnShowBusted;
                show.ShowEnded += OnShowEnded;
                subscribed = true;
            }
        }

        private void OnDisable()
        {
            if (show != null && subscribed)
            {
                show.ShowStarted -= OnShowStarted;
                show.ShowRevealed -= OnShowRevealed;
                show.ShowBusted -= OnShowBusted;
                show.ShowEnded -= OnShowEnded;
            }
            subscribed = false;
        }

        private void Start()
        {
            // Every Awake has run, so the Show Director has its flow; the first act can't start before
            // the first LateUpdate, so today's acts are always in place in time.
            PlanToday(!ShowUnderWay());
        }

        private void OnDestroy()
        {
            if (book != null)
            {
                book.Dispose();
            }
        }

        // ------------------------------------------------------------ today's show

        private static ProgressData LoadSafely()
        {
            try
            {
                return ProgressStore.Load();
            }
            catch (Exception e)
            {
                // Load is written not to throw; if it ever does, a show without saved progress beats no show.
                Debug.LogWarning("[Misdirection] Progress: couldn't load (" + e.Message + "). Starting fresh.");
                return new ProgressData();
            }
        }

        /// <summary>A show is being performed or taking its bow: not the moment to change who is in the crowd.</summary>
        private bool ShowUnderWay()
        {
            ShowFlow flow = show != null ? show.Flow : null;
            return flow != null && (flow.Stage == ShowStage.Performing || flow.Over);
        }

        /// <summary>
        /// Works out today's show. <paramref name="applyCrowd"/> is false while a show is under way:
        /// the day's crowd then waits for the next show to start.
        /// </summary>
        private void PlanToday(bool applyCrowd)
        {
            planIsStale = !applyCrowd;
            today = ProgressStore.Today();
            plan = ProgressRules.PlanFor(data, today);
            if (show != null)
            {
                if (dailyCrowd && applyCrowd)
                {
                    show.UseActs(plan.Acts);
                }
                else if (show.Flow != null)
                {
                    // Not the day's seeded crowd (switched off, or a show is under way): keep the acts
                    // being played and fit the challenge to them.
                    plan.Acts = show.Flow.Settings.Acts;
                    plan.Challenge = DailyChallenges.For(today, plan.Acts);
                }
            }
            Debug.Log("[Misdirection] Progress: " + today + " is day " + DayNumber + " (streak " + Streak + ", " + data.ShowsPlayed +
                      " shows so far). Crowd: " + ProgressText.CrowdLine(plan.Acts) + ". Challenge: " + plan.Challenge.Text + ".");
        }

        private void EnsureRecord()
        {
            if (plan == null)
            {
                PlanToday(false);
            }
            if (record == null)
            {
                record = new ShowRecord(ShowActs());
            }
        }

        /// <summary>The acts the show is really running (they are the plan's, unless the daily crowd is switched off).</summary>
        private ShowAct[] ShowActs()
        {
            return show != null && show.Flow != null ? show.Flow.Settings.Acts : plan.Acts;
        }

        // ------------------------------------------------------------ show events

        private void OnShowStarted()
        {
            try
            {
                // A new day may have begun while the app stayed open (or "Simulate Next Day" was used).
                if (plan == null || planIsStale || ProgressStore.Today() != today)
                {
                    PlanToday(true);
                }
                record = new ShowRecord(ShowActs());
                primedRegular = null;
                regularRelaxed = false;
                pendingReaction = RegularReaction.None;
                bookPreviewUntil = -1;
                book.Hide();

                bool done = ChallengePassedToday;
                dayBanner.text = "<b>" + ProgressText.DayLine(DayNumber) + "</b>\n<size=55%>Today's crowd: " +
                                 ProgressText.CrowdLine(plan.Acts) + "\nChallenge: " + plan.Challenge.Text + (done ? " - OK" : "") + "</size>";
                dayBannerUntil = Time.timeAsDouble + dayBannerSeconds;
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnShowRevealed(int trick, RevealResult reveal)
        {
            try
            {
                EnsureRecord();
                Archetype[] there = WhoIsThere();
                record.AddReveal(show.Flow.Act, trick, reveal, there);
                bool regularWatching = crowd != null && crowd.IndexOf(Archetype.Regular) >= 0;
                if (regularWatching && reveal.Kind == ShowRecord.Produce && reveal.Spectator >= 0 && reveal.Spectator < there.Length)
                {
                    // Acted on in LateUpdate, after the trick has made the whole crowd react to the reveal.
                    pendingReaction = RegularRule.OnProduce(data.Regular, there[reveal.Spectator]);
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnShowBusted(int trick)
        {
            try
            {
                EnsureRecord();
                record.AddBust(show.Flow.Act, trick);
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        private void OnShowEnded(ShowSummary summary)
        {
            try
            {
                EnsureRecord();
                bool regularAlreadyComes = ProgressRules.IsReturning(data, today);
                lastOutcome = ProgressRules.FinishShow(data, today, plan.Challenge, record, summary);
                record = null;
                ProgressStore.Save(data);
                book.Show(data, plan.Challenge, lastOutcome, regularAlreadyComes);

                string stars = "";
                for (int i = 0; i < TrickBook.TrickCount && i < data.Tricks.Length; i++)
                {
                    stars += (i > 0 ? ", " : "") + TrickBook.NameOf(i) + " " + data.Tricks[i].Stars + "/" + TrickBook.MaxStars;
                }
                Debug.Log("[Misdirection] Progress: saved. Day " + lastOutcome.DayNumber + ", " + ProgressText.StreakLine(lastOutcome.Streak) +
                          ". " + ProgressText.ChallengeLine(plan.Challenge, lastOutcome.ChallengePassedToday) + ". Trick book: " + stars +
                          ". " + ProgressText.BestLine(data.BestTips, lastOutcome.NewBestTips) + ".");
            }
            catch (Exception e)
            {
                Debug.LogException(e, this);
            }
        }

        /// <summary>The archetypes actually standing there, in the crowd's own order (reveals name spectators by that index).</summary>
        private Archetype[] WhoIsThere()
        {
            if (crowd == null)
            {
                return show.Flow.CurrentAct.Crowd;
            }
            Archetype[] there = new Archetype[crowd.Spectators.Count];
            for (int i = 0; i < there.Length; i++)
            {
                there[i] = crowd.Spectators[i].Mind.Profile.Archetype;
            }
            return there;
        }

        // ------------------------------------------------------------ every frame

        private void LateUpdate()
        {
            double now = Time.timeAsDouble;
            ShowFlow flow = show != null ? show.Flow : null;
            bool tableKnown = table != null && table.Calibrated;
            Vector3 viewer = lookSource != null && lookSource.Head != null ? lookSource.Head.position : Vector3.up * 1.2f;

            PrimeRegular(flow);
            if (pendingReaction != RegularReaction.None)
            {
                RegularReacts(now);
            }

            bool bannerOn = tableKnown && flow != null && flow.Stage == ShowStage.Performing && flow.Act == 0 && now < dayBannerUntil;
            if (dayBanner.gameObject.activeSelf != bannerOn)
            {
                dayBanner.gameObject.SetActive(bannerOn);
            }
            if (bannerOn)
            {
                dayBanner.transform.position = table.Frame.ToWorld(dayBannerLocal.ToNumerics()).ToUnity();
                WorldText.Face(dayBanner.transform, viewer);
            }

            // The trick book stands only while the show is over (bow or walk-off), or for a test preview.
            bool bookWanted = (flow != null && flow.Over) || Time.unscaledTimeAsDouble < bookPreviewUntil;
            if (book.Visible && (!bookWanted || !tableKnown))
            {
                book.Hide();
            }
            if (book.Visible)
            {
                book.Update(now, table.Frame.ToWorld(bookLocal.ToNumerics()).ToUnity(), viewer);
            }

            bool lineOn = now < regularLineUntil;
            if (regularLine.gameObject.activeSelf != lineOn)
            {
                regularLine.gameObject.SetActive(lineOn);
            }
            if (lineOn)
            {
                regularLine.transform.position = regularLineAnchor + Vector3.up * (float)(now - regularLineStart) * 0.04f;
                WorldText.Face(regularLine.transform, viewer);
            }
        }

        // ------------------------------------------------------------ the returning Regular

        /// <summary>
        /// Applies what the Regular remembers to his mind, once each time he is (re)placed: the crowd
        /// builds new minds whenever the line-up changes.
        /// </summary>
        private void PrimeRegular(ShowFlow flow)
        {
            if (crowd == null || flow == null || flow.Stage != ShowStage.Performing || data == null)
            {
                return;
            }
            int index = crowd.IndexOf(Archetype.Regular);
            if (index < 0)
            {
                primedRegular = null;
                return;
            }
            SpectatorMind mind = crowd.Spectators[index].Mind;
            if (ReferenceEquals(mind, primedRegular))
            {
                return;
            }
            primedRegular = mind;
            if (regularRelaxed)
            {
                return; // he has already seen something new this show
            }
            RegularAttitude attitude = RegularRule.AttitudeFor(data.Regular, flow.ActiveTrick, regular);
            RegularRule.Apply(mind, attitude);
            if (attitude.Wary)
            {
                Debug.Log("[Misdirection] Progress: the Regular has seen this " + (data.Regular.Repeats + 1) + " times running: he arrives Curious, suspicion x" +
                          attitude.SuspicionGainScale.ToString("F2") + ".");
            }
        }

        /// <summary>The coin came from someone's ear while the Regular watched: "Same as yesterday!" or "That's new!".</summary>
        private void RegularReacts(double now)
        {
            RegularReaction reaction = pendingReaction;
            pendingReaction = RegularReaction.None;
            int index = crowd != null ? crowd.IndexOf(Archetype.Regular) : -1;
            if (index < 0)
            {
                return;
            }
            Spectator who = crowd.Spectators[index];
            RegularRule.React(who.Mind, reaction, regular);
            string line = RegularRule.Line(reaction, data.Regular, today);
            if (reaction == RegularReaction.SameAgain)
            {
                // He points and says "hm!" while the others clap.
                who.React(Reaction.Heckle, true);
                regularLine.text = "<color=#FF7043>\"" + line + "\"</color>";
            }
            else
            {
                regularRelaxed = true;
                regularLine.text = "<color=#7CFC00>\"" + line + "\"</color>";
            }
            regularLineAnchor = who.AboveHead + Vector3.up * 0.12f;
            regularLineStart = now;
            regularLineUntil = now + regularLineSeconds;
            Debug.Log("[Misdirection] Progress: the Regular says \"" + line + "\"");
        }
    }
}
