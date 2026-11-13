using System;
using Misdirection.Core.Tricks;

namespace Misdirection.Core.Access
{
    [Serializable]
    public sealed class OneHandedSettings
    {
        /// <summary>One hand in view this long, the other gone all that time, switches one-handed play on (auto mode).</summary>
        public double AutoAfterSeconds = 8.0;
        /// <summary>The same, when the other hand has never been seen since the app started.</summary>
        public double NeverSeenAfterSeconds = 5.0;
        /// <summary>Both hands steadily in view this long switches it off again (auto mode).</summary>
        public double BothBackSeconds = 1.5;
        /// <summary>A different hand must be the only one in view this long before it becomes the playing hand.</summary>
        public double SwitchHandSeconds = 0.5;
    }

    /// <summary>
    /// Decides when the one-handed accommodations apply and which hand is playing. In auto mode they
    /// switch on once only one hand has been in view for a while (nobody has to find a menu first),
    /// and off again when both hands are steadily back. On and off are the manual overrides.
    /// Feed it unscaled time every frame: it must keep working while the show is paused. Pure logic.
    /// </summary>
    public sealed class OneHandedDetector
    {
        private readonly OneHandedSettings settings;
        private bool started;
        private double lastTime;
        private bool leftSeen;
        private bool rightSeen;
        private double leftSolo;
        private double rightSolo;
        private double bothFor;
        private double leftAlone;
        private double rightAlone;
        private bool autoActive;

        public OneHandedDetector(OneHandedSettings settings = null)
        {
            this.settings = settings ?? new OneHandedSettings();
            Mode = OneHandMode.Auto;
            ActiveHand = HandId.None;
        }

        public OneHandMode Mode { get; set; }

        /// <summary>True when the game should play one-handed (calibration, pause, ghost hands).</summary>
        public bool Active { get; private set; }

        /// <summary>The hand the player is using: the only one in view, or the last one that was. None until a hand is seen.</summary>
        public HandId ActiveHand { get; private set; }

        /// <summary>0..1: how close auto mode is to switching on (for debugging and the tray).</summary>
        public float AutoProgress { get; private set; }

        /// <summary>Advance one frame. Returns true when <see cref="Active"/> changed.</summary>
        public bool Update(double time, bool leftTracked, bool rightTracked)
        {
            // Long frames (a hitch, the headset coming back on) count as a short one: never jump to a decision.
            double dt = started ? Math.Min(0.25, Math.Max(0.0, time - lastTime)) : 0.0;
            started = true;
            lastTime = time;
            leftSeen |= leftTracked;
            rightSeen |= rightTracked;

            bool both = leftTracked && rightTracked;
            bool onlyLeft = leftTracked && !rightTracked;
            bool onlyRight = rightTracked && !leftTracked;

            if (both)
            {
                bothFor += dt;
                if (bothFor >= settings.BothBackSeconds - 1e-9)
                {
                    // Two hands, steadily: a two-handed player. Start counting from nothing again.
                    leftSolo = 0;
                    rightSolo = 0;
                    autoActive = false;
                }
            }
            else
            {
                bothFor = 0;
            }

            // Time with exactly one hand in view. Neither hand in view (looking away, paused) keeps the count.
            if (onlyLeft)
            {
                leftSolo += dt;
                rightSolo = 0;
            }
            else if (onlyRight)
            {
                rightSolo += dt;
                leftSolo = 0;
            }
            double leftNeeded = rightSeen ? settings.AutoAfterSeconds : settings.NeverSeenAfterSeconds;
            double rightNeeded = leftSeen ? settings.AutoAfterSeconds : settings.NeverSeenAfterSeconds;
            if (leftSolo >= leftNeeded - 1e-9 || rightSolo >= rightNeeded - 1e-9)
            {
                autoActive = true;
            }
            AutoProgress = autoActive ? 1f : (float)Math.Max(Ratio(leftSolo, leftNeeded), Ratio(rightSolo, rightNeeded));

            UpdateActiveHand(dt, onlyLeft, onlyRight);

            bool before = Active;
            Active = Mode == OneHandMode.On || (Mode == OneHandMode.Auto && autoActive);
            return before != Active;
        }

        /// <summary>The playing hand follows whichever hand is the only one in view, once that has settled.</summary>
        private void UpdateActiveHand(double dt, bool onlyLeft, bool onlyRight)
        {
            leftAlone = onlyLeft ? leftAlone + dt : 0;
            rightAlone = onlyRight ? rightAlone + dt : 0;
            if (ActiveHand == HandId.None)
            {
                if (onlyLeft)
                {
                    ActiveHand = HandId.Left;
                }
                else if (onlyRight)
                {
                    ActiveHand = HandId.Right;
                }
                return;
            }
            if (ActiveHand != HandId.Left && leftAlone >= settings.SwitchHandSeconds - 1e-9)
            {
                ActiveHand = HandId.Left;
            }
            else if (ActiveHand != HandId.Right && rightAlone >= settings.SwitchHandSeconds - 1e-9)
            {
                ActiveHand = HandId.Right;
            }
        }

        private static double Ratio(double value, double needed)
        {
            return needed <= 0 ? 1.0 : Math.Min(1.0, value / needed);
        }
    }
}
