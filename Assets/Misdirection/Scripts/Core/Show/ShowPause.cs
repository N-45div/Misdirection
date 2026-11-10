using Misdirection.Core.Hands;

namespace Misdirection.Core.Show
{
    public enum PauseReason
    {
        None,
        /// <summary>Both hands out of view for a while: "the crowd waits".</summary>
        HandsGone,
        /// <summary>The app lost input focus (system menu, headset off, another app on top).</summary>
        FocusLost,
    }

    /// <summary>
    /// When the show pauses and resumes. Both hands gone for 2 s pauses it ("the crowd waits") and both
    /// steadily back resumes it; losing input focus pauses it at once and getting focus back resumes it.
    /// Feed it unscaled time: the scene freezes game time while paused.
    /// </summary>
    public sealed class ShowPause
    {
        private readonly BothHandsPause hands;

        public ShowPause(double pauseAfterSeconds = 2.0, double resumeAfterSeconds = 0.5)
        {
            hands = new BothHandsPause(pauseAfterSeconds, resumeAfterSeconds);
        }

        /// <summary>Set false to ignore hand loss (e.g. before the table exists, when nobody is playing yet).</summary>
        public bool WatchHands = true;

        public PauseReason Reason { get; private set; }
        public bool Paused { get { return Reason != PauseReason.None; } }

        /// <summary>Returns true on the frame the paused state changes.</summary>
        public bool Update(double unscaledTime, bool leftTracked, bool rightTracked, bool hasFocus)
        {
            bool before = Paused;
            hands.Update(unscaledTime, leftTracked, rightTracked);
            if (!hasFocus)
            {
                Reason = PauseReason.FocusLost;
            }
            else if (WatchHands && hands.Paused)
            {
                Reason = PauseReason.HandsGone;
            }
            else
            {
                Reason = PauseReason.None;
            }
            return before != Paused;
        }
    }
}
