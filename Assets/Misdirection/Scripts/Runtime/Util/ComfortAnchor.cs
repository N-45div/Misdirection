using Misdirection.Core.Table;
using Misdirection.Core.View;
using UnityEngine;
using NVector3 = System.Numerics.Vector3;

namespace Misdirection
{
    /// <summary>
    /// Where the essential world-space UI sits, in the table's frame (x right, y up, z away from the
    /// player, origin at the near edge). One place, so the game and the smoke test's field-of-view
    /// check agree.
    /// </summary>
    public static class UiAnchors
    {
        /// <summary>The trick prompt (tips, crowd line) above the far side of the mat.</summary>
        public static readonly NVector3 TrickHud = new NVector3(0f, 0.16f, 0.62f);
        /// <summary>The show banner ("Act 2 of 3", "Bravo!"); the star coins sit just under it.</summary>
        public static readonly NVector3 ShowBanner = new NVector3(0f, 0.32f, 0.55f);
        /// <summary>Centre of the close-up mat, where the coin rests.</summary>
        public static readonly NVector3 MatCentre = new NVector3(0f, 0f, 0.24f);
        /// <summary>Text that follows the view never comes closer to the table top than this (metres).</summary>
        public const float TableClearance = 0.06f;
    }

    /// <summary>
    /// FoV-aware placement for one piece of essential UI (Day 12): give it the element's home position
    /// each frame and draw the element where it says. At home nothing changes; when the player has
    /// looked away for longer than a glance, the element eases into a comfortable zone around the head
    /// direction (50 degrees wide, 20 up, 33 down: inside the 70 x 66 degree view of Meta VR Glasses,
    /// which reaches 25 up and 41 down), and goes home again when home is comfortable.
    /// Rules and tests: Misdirection.Core.View.ComfortFollow.
    /// </summary>
    public sealed class ComfortAnchor
    {
        /// <summary>The zone, delay and speed every anchor uses. Change the fields to tune or to offer an option.</summary>
        public static readonly ComfortSettings Settings = new ComfortSettings();

        /// <summary>False pins every element to its home, as before Day 12.</summary>
        public static bool Enabled = true;

        // Everything laid out above the table as a set (trick prompt, show banner, stars) moves together.
        private static readonly ComfortAnchor TableUi = new ComfortAnchor();

        private readonly ComfortFollow follow;
        private int lastFrame = -100;

        public ComfortAnchor() : this(0f)
        {
        }

        /// <param name="marginDegrees">
        /// Extra degrees on every side of the comfortable zone for this element, so it comes to rest
        /// further out than the others and can't land on top of them (the mentor's caption uses this).
        /// </param>
        public ComfortAnchor(float marginDegrees)
        {
            follow = new ComfortFollow(Settings, marginDegrees);
        }

        /// <summary>
        /// How far the set of UI above the table is moved this frame (zero while the prompt's home is
        /// comfortable). Add it to the home of each member: the prompt, the banner, the stars. They share
        /// one follower so they keep their arrangement and never land on each other.
        /// </summary>
        public static Vector3 TableOffset(TableFrame table, Vector3 eye, Vector3 forward)
        {
            Vector3 reference = table.ToWorld(UiAnchors.TrickHud).ToUnity();
            TableUi.Place(reference, eye, forward, table.Origin.Y + UiAnchors.TableClearance);
            return TableUi.Offset;
        }

        /// <summary>As above, from the headset's transform. Zero when there is no head.</summary>
        public static Vector3 TableOffset(TableFrame table, Transform head)
        {
            return head != null ? TableOffset(table, head.position, head.forward) : Vector3.zero;
        }

        /// <summary>How far the last placement was from home (zero at home). Lets companions travel along.</summary>
        public Vector3 Offset { get; private set; }

        /// <summary>True while the element is away from its home.</summary>
        public bool Displaced { get { return follow.Displaced; } }

        /// <summary>Call every frame while the element shows. <paramref name="head"/> is the headset (centre of the display).</summary>
        public Vector3 Place(Vector3 home, Transform head, float floorY = float.NegativeInfinity)
        {
            if (head == null)
            {
                Offset = Vector3.zero;
                return home;
            }
            return Place(home, head.position, head.forward, floorY);
        }

        /// <summary>
        /// Call every frame while the element shows. <paramref name="floorY"/> is a height the element must
        /// not sink below when it follows the view down (the real table top plus a little).
        /// </summary>
        public Vector3 Place(Vector3 home, Vector3 eye, Vector3 forward, float floorY = float.NegativeInfinity)
        {
            if (!Enabled)
            {
                Offset = Vector3.zero;
                return home;
            }
            int frame = Time.frameCount;
            if (frame == lastFrame)
            {
                // Asked again in the same frame (members of a set): same answer, no second step.
                return home + Offset;
            }
            if (frame != lastFrame + 1)
            {
                // It was hidden for a while: appear in place rather than glide in from the old spot.
                follow.Reset();
            }
            lastFrame = frame;
            // Unscaled time: the show freezes game time while paused, but the head keeps moving.
            Vector3 shown = follow.Update(eye.ToNumerics(), forward.ToNumerics(), home.ToNumerics(), Time.unscaledDeltaTime).ToUnity();
            if (shown.y < floorY && home.y >= floorY)
            {
                shown.y = floorY;
            }
            Offset = shown - home;
            return shown;
        }
    }
}
