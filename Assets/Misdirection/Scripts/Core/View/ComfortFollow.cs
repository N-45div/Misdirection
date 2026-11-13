using System;
using System.Numerics;

namespace Misdirection.Core.View
{
    [Serializable]
    public sealed class ComfortSettings
    {
        /// <summary>Width of the comfortable zone around the view direction (degrees). Glasses see 70.</summary>
        public float HorizontalDegrees = 50f;
        /// <summary>Height of the comfortable zone around the view direction (degrees). Glasses see 66.</summary>
        public float VerticalDegrees = 40f;
        /// <summary>
        /// The home position must be outside the zone this long before the element moves, so a glance
        /// away (a moment of eye contact, a look at the hands) doesn't drag the text around.
        /// </summary>
        public double DelaySeconds = 0.6;
        /// <summary>
        /// Once moved, the element stays put until the view drifts this many degrees further, so a still
        /// head means a still element.
        /// </summary>
        public float SlackDegrees = 4f;
        /// <summary>How fast the element eases to where it should be (1/s). 5 settles in about half a second.</summary>
        public float FollowRate = 5f;
    }

    /// <summary>
    /// Keeps one piece of essential UI where it can be read on a narrow field of view (Day 12). The
    /// element has a home (say, a spot above the table). While the home is inside the comfortable zone
    /// around the view direction, it sits at home. When the player looks away for longer than a glance,
    /// it eases to the nearest point inside the zone and then stays there, fixed in the room, until the
    /// view moves on again; when the home is comfortable again it eases back. It never jumps and never
    /// chases small head movements. Pure logic: feed it the eye, the view direction and the frame time.
    /// </summary>
    public sealed class ComfortFollow
    {
        private readonly ComfortSettings settings;
        private Vector3 goal;
        private double outFor;
        private bool started;

        public ComfortFollow(ComfortSettings settings = null)
        {
            this.settings = settings ?? new ComfortSettings();
        }

        /// <summary>Where to draw the element now.</summary>
        public Vector3 Position { get; private set; }

        /// <summary>True while the element is away from home because home is not comfortable to look at.</summary>
        public bool Displaced { get; private set; }

        /// <summary>Forget where the element was: the next update places it at once (use after it was hidden).</summary>
        public void Reset()
        {
            started = false;
            Displaced = false;
            outFor = 0;
        }

        /// <summary>
        /// Call every frame the element shows. <paramref name="forward"/> is the centre of the view (the head
        /// direction, since that is where a display's field of view is centred). Returns <see cref="Position"/>.
        /// </summary>
        public Vector3 Update(Vector3 eye, Vector3 forward, Vector3 home, float dt)
        {
            FieldOfView comfort = new FieldOfView(settings.HorizontalDegrees, settings.VerticalDegrees);
            if (forward.LengthSquared() < 1e-12f)
            {
                // No view direction: nothing to be comfortable about.
                started = true;
                Displaced = false;
                goal = home;
                Position = home;
                return Position;
            }
            bool comfortable = ViewCone.Contains(eye, forward, comfort, home);
            if (!started)
            {
                // First frame on show: appear where it can be read, no glide.
                started = true;
                Displaced = !comfortable;
                outFor = 0;
                goal = comfortable ? home : ViewCone.Nearest(eye, forward, comfort, home);
                Position = goal;
                return Position;
            }

            if (comfortable)
            {
                Displaced = false;
                outFor = 0;
                goal = home;
            }
            else if (!Displaced)
            {
                // Looking away, but perhaps only for a moment: stay home until the delay has passed.
                outFor += Math.Max(0f, dt);
                goal = home;
                if (outFor >= settings.DelaySeconds)
                {
                    Displaced = true;
                    goal = ViewCone.Nearest(eye, forward, comfort, home);
                }
            }
            else if (!ViewCone.Contains(eye, forward, comfort.Grown(settings.SlackDegrees), goal))
            {
                // Already moved, and the view has gone on past the slack: move again.
                goal = ViewCone.Nearest(eye, forward, comfort, home);
            }

            float k = dt > 0f ? 1f - (float)Math.Exp(-settings.FollowRate * dt) : 0f;
            Position = Position + (goal - Position) * k;
            return Position;
        }
    }
}
