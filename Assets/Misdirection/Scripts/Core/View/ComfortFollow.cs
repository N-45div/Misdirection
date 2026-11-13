using System;
using System.Numerics;

namespace Misdirection.Core.View
{
    [Serializable]
    public sealed class ComfortSettings
    {
        /// <summary>Width of the comfortable zone, centred on the view direction (degrees). Glasses see 70.</summary>
        public float HorizontalDegrees = 50f;
        /// <summary>How far above the view direction the zone reaches (degrees). Glasses see 25 up.</summary>
        public float UpDegrees = 20f;
        /// <summary>How far below the view direction the zone reaches (degrees). Glasses see 41 down.</summary>
        public float DownDegrees = 33f;
        /// <summary>
        /// The home position must be outside the zone this long before the element moves, so a glance
        /// away (a moment of eye contact, a look at the hands) doesn't drag the text around.
        /// </summary>
        public double DelaySeconds = 0.6;
        /// <summary>
        /// Leeway in degrees, both ways. The home may sit this far outside the zone before the element
        /// moves at all (no fidgeting over a degree or two), and once moved the element stays put until
        /// the view drifts this much further, so a still head means a still element.
        /// </summary>
        public float SlackDegrees = 4f;
        /// <summary>How fast the element eases to where it should be (1/s). 5 settles in about half a second.</summary>
        public float FollowRate = 5f;
        /// <summary>
        /// If the home moves further than this in one frame (metres), it is a different place (a new
        /// caption, a reaction above another head): the element appears there instead of sliding across.
        /// </summary>
        public float JumpMetres = 0.05f;
    }

    /// <summary>
    /// Keeps one piece of essential UI where it can be read on a narrow field of view (Day 12). The
    /// element has a home (say, a spot above the table). While the home is inside the comfortable zone
    /// around the view direction (or within a few degrees of it), it sits at home. When the player
    /// looks away for longer than a glance, it eases to the nearest point inside the zone and then
    /// stays there, fixed in the room, until the view moves on again; when the home is comfortable
    /// again it eases back. It never jumps and never chases small head movements.
    /// While it is at home it is fixed to its home exactly, so on a wide
    /// field of view, looking at the table, nothing differs from a plain fixed label.
    /// Pure logic: feed it the eye, the view direction and the frame time.
    /// </summary>
    public sealed class ComfortFollow
    {
        private readonly ComfortSettings settings;
        private readonly float margin;
        private Vector3 goal;
        private Vector3 lastHome;
        private double outFor;
        private bool started;
        private bool attached;

        /// <param name="settings">The zone, delay and speed (shared between elements; read every update).</param>
        /// <param name="marginDegrees">
        /// Extra degrees on every side of the zone for this element. Elements with different margins come to
        /// rest at different distances from the view centre, so two of them never land on the same spot.
        /// </param>
        public ComfortFollow(ComfortSettings settings = null, float marginDegrees = 0f)
        {
            this.settings = settings ?? new ComfortSettings();
            margin = marginDegrees;
        }

        /// <summary>Where to draw the element now.</summary>
        public Vector3 Position { get; private set; }

        /// <summary>True while the element is away from home because home is not comfortable to look at.</summary>
        public bool Displaced { get; private set; }

        /// <summary>Forget where the element was: the next update places it at once (use after it was hidden).</summary>
        public void Reset()
        {
            started = false;
            attached = false;
            Displaced = false;
            outFor = 0;
        }

        /// <summary>
        /// Call every frame the element shows. <paramref name="forward"/> is the centre of the view (the head
        /// direction, since that is where a display's field of view is centred). Returns <see cref="Position"/>.
        /// </summary>
        public Vector3 Update(Vector3 eye, Vector3 forward, Vector3 home, float dt)
        {
            FieldOfView comfort = new FieldOfView(settings.HorizontalDegrees * 0.5f, settings.HorizontalDegrees * 0.5f,
                                                  settings.UpDegrees, settings.DownDegrees).Grown(margin);
            if (started && Vector3.DistanceSquared(home, lastHome) > settings.JumpMetres * settings.JumpMetres)
            {
                // The home is somewhere else altogether: start again there.
                Reset();
            }
            lastHome = home;
            if (forward.LengthSquared() < 1e-12f)
            {
                // No view direction: nothing to be comfortable about.
                started = true;
                attached = true;
                Displaced = false;
                goal = home;
                Position = home;
                return Position;
            }
            FieldOfView leeway = comfort.Grown(settings.SlackDegrees);
            bool comfortable = ViewCone.Contains(eye, forward, comfort, home);
            // "Near enough": inside the zone or within the slack around it.
            bool nearEnough = comfortable || ViewCone.Contains(eye, forward, leeway, home);
            if (!started)
            {
                // First frame on show: appear where it can be read, no glide.
                started = true;
                attached = nearEnough;
                Displaced = !nearEnough;
                outFor = 0;
                goal = nearEnough ? home : ViewCone.Nearest(eye, forward, comfort, home);
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
                goal = home;
                if (nearEnough)
                {
                    // A little outside the zone: leave it be.
                    outFor = 0;
                }
                else
                {
                    // Looking away, but perhaps only for a moment: stay home until the delay has passed.
                    outFor += Math.Max(0f, dt);
                    if (outFor >= settings.DelaySeconds)
                    {
                        Displaced = true;
                        goal = ViewCone.Nearest(eye, forward, comfort, home);
                    }
                }
            }
            else if (!ViewCone.Contains(eye, forward, leeway, goal))
            {
                // Already moved, and the view has gone on past the slack: move again.
                goal = ViewCone.Nearest(eye, forward, comfort, home);
            }

            if (attached && !Displaced)
            {
                // At home it is fixed to its home (which may itself move, like a reaction drifting up).
                Position = home;
                return Position;
            }
            float k = dt > 0f ? 1f - (float)Math.Exp(-settings.FollowRate * dt) : 0f;
            Position = Position + (goal - Position) * k;
            // Back within a millimetre of home: fixed to it again.
            attached = !Displaced && Vector3.DistanceSquared(Position, home) < 1e-6f;
            return Position;
        }
    }
}
