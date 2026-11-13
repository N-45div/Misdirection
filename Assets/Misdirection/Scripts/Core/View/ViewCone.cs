using System;
using System.Numerics;

namespace Misdirection.Core.View
{
    /// <summary>
    /// A field of view: how many degrees it reaches to the left, right, up and down of straight
    /// ahead. Headsets are quoted as width x height (Meta VR Glasses: 70 x 66; Quest 3: about
    /// 110 x 96), but the height need not be centred: the glasses see further down than up.
    /// </summary>
    public struct FieldOfView
    {
        public float Left;
        public float Right;
        public float Up;
        public float Down;

        /// <summary>A field of view centred on straight ahead: <paramref name="horizontalDegrees"/> wide, <paramref name="verticalDegrees"/> tall.</summary>
        public FieldOfView(float horizontalDegrees, float verticalDegrees)
        {
            Left = horizontalDegrees * 0.5f;
            Right = horizontalDegrees * 0.5f;
            Up = verticalDegrees * 0.5f;
            Down = verticalDegrees * 0.5f;
        }

        public FieldOfView(float leftDegrees, float rightDegrees, float upDegrees, float downDegrees)
        {
            Left = leftDegrees;
            Right = rightDegrees;
            Up = upDegrees;
            Down = downDegrees;
        }

        /// <summary>Total width in degrees.</summary>
        public float Horizontal { get { return Left + Right; } }

        /// <summary>Total height in degrees.</summary>
        public float Vertical { get { return Up + Down; } }

        /// <summary>
        /// The narrowest device we design for: Meta VR Glasses, 70 x 66 degrees. The split (35 to each
        /// side, 25 up, 41 down) is what Meta's own field-of-view simulation in the v207 SDK masks to
        /// (OVRManager > Simulate Device Field of View).
        /// </summary>
        public static FieldOfView Glasses { get { return new FieldOfView(35f, 35f, 25f, 41f); } }

        /// <summary>
        /// Where essential text is comfortable to read on every device: 50 degrees wide, 20 up and 33
        /// down (the glasses' view with 10 degrees to spare at the sides, 8 at the bottom, 5 at the top,
        /// which is room for the text's own size).
        /// </summary>
        public static FieldOfView Comfort { get { return new FieldOfView(25f, 25f, 20f, 33f); } }

        /// <summary>This field of view with <paramref name="degrees"/> added on every side.</summary>
        public FieldOfView Grown(float degrees)
        {
            return new FieldOfView(Left + degrees, Right + degrees, Up + degrees, Down + degrees);
        }
    }

    /// <summary>
    /// Field-of-view geometry for FoV-aware layout (Day 12): is a world point inside the view "cone"
    /// (really a rectangular frustum: so many degrees left, right, up and down of the view direction),
    /// and if not, which point inside is nearest. Pure maths, no engine types.
    /// Axes follow Unity: x right, y up, z forward.
    /// </summary>
    public static class ViewCone
    {
        private const float MaxHalfAngle = 89f;

        /// <summary>True if <paramref name="point"/> is in front of the eye and inside the field of view (world up).</summary>
        public static bool Contains(Vector3 eye, Vector3 forward, FieldOfView fov, Vector3 point)
        {
            return Contains(eye, forward, Vector3.UnitY, fov, point);
        }

        /// <summary>
        /// True if <paramref name="point"/> is in front of the eye and inside the field of view.
        /// <paramref name="up"/> is the viewer's up (the head's up for a display, world up for a comfort zone).
        /// </summary>
        public static bool Contains(Vector3 eye, Vector3 forward, Vector3 up, FieldOfView fov, Vector3 point)
        {
            Vector3 f, r, u;
            if (!Basis(forward, up, out f, out r, out u))
            {
                return false;
            }
            Vector3 v = point - eye;
            float z = Vector3.Dot(v, f);
            if (z <= 1e-6f)
            {
                return false;
            }
            float x = Vector3.Dot(v, r);
            float y = Vector3.Dot(v, u);
            // A hair of tolerance so a point placed exactly on the edge counts as inside.
            float slack = 1e-4f * z + 1e-6f;
            return x >= -z * Tan(fov.Left) - slack && x <= z * Tan(fov.Right) + slack
                && y >= -z * Tan(fov.Down) - slack && y <= z * Tan(fov.Up) + slack;
        }

        /// <summary>The nearest point inside the field of view (world up). See the overload with an up vector.</summary>
        public static Vector3 Nearest(Vector3 eye, Vector3 forward, FieldOfView fov, Vector3 point)
        {
            return Nearest(eye, forward, Vector3.UnitY, fov, point);
        }

        /// <summary>
        /// The point inside the field of view that is nearest to <paramref name="point"/>, at the same
        /// distance from the eye: the point itself when it is already inside, otherwise the point slid
        /// to the nearest edge (or corner). A point beside or behind the viewer lands on the edge on
        /// its own side.
        /// </summary>
        public static Vector3 Nearest(Vector3 eye, Vector3 forward, Vector3 up, FieldOfView fov, Vector3 point)
        {
            Vector3 f, r, u;
            if (!Basis(forward, up, out f, out r, out u))
            {
                return point;
            }
            Vector3 v = point - eye;
            float distance = v.Length();
            if (distance < 1e-6f)
            {
                return point;
            }
            float x = Vector3.Dot(v, r);
            float y = Vector3.Dot(v, u);
            float z = Vector3.Dot(v, f);
            float tanLeft = Tan(fov.Left);
            float tanRight = Tan(fov.Right);
            float tanUp = Tan(fov.Up);
            float tanDown = Tan(fov.Down);
            if (z > 1e-6f && x >= -z * tanLeft && x <= z * tanRight && y >= -z * tanDown && y <= z * tanUp)
            {
                return point;
            }
            // Work on the plane one unit ahead of the eye: clamp where the point's direction crosses it.
            // Beside or behind the viewer the direction never crosses, so treat it as crossing very far out.
            float depth = Math.Max(z, 1e-3f * distance);
            float tx = Clamp(x / depth, -tanLeft, tanRight);
            float ty = Clamp(y / depth, -tanDown, tanUp);
            Vector3 direction = Vector3.Normalize(f + r * tx + u * ty);
            return eye + direction * distance;
        }

        /// <summary>
        /// Where a point sits in the view, in degrees: <paramref name="yaw"/> positive to the right,
        /// <paramref name="pitch"/> positive upward, both zero straight ahead. A point is inside a field
        /// of view when its yaw is between -Left and Right and its pitch between -Down and Up.
        /// </summary>
        public static void Angles(Vector3 eye, Vector3 forward, Vector3 up, Vector3 point, out float yaw, out float pitch)
        {
            yaw = 0f;
            pitch = 0f;
            Vector3 f, r, u;
            if (!Basis(forward, up, out f, out r, out u))
            {
                return;
            }
            Vector3 v = point - eye;
            float z = Vector3.Dot(v, f);
            yaw = (float)(Math.Atan2(Vector3.Dot(v, r), z) * 180.0 / Math.PI);
            pitch = (float)(Math.Atan2(Vector3.Dot(v, u), z) * 180.0 / Math.PI);
        }

        /// <summary>Unit forward, right and up for a view direction. False if the direction is zero.</summary>
        private static bool Basis(Vector3 forward, Vector3 upHint, out Vector3 f, out Vector3 r, out Vector3 u)
        {
            f = Vector3.UnitZ;
            r = Vector3.UnitX;
            u = Vector3.UnitY;
            if (forward.LengthSquared() < 1e-12f)
            {
                return false;
            }
            f = Vector3.Normalize(forward);
            r = Vector3.Cross(upHint, f);
            if (r.LengthSquared() < 1e-8f)
            {
                // Looking straight up or down (or no usable up): any horizontal "right" will do.
                r = Vector3.Cross(Vector3.UnitZ, f);
                if (r.LengthSquared() < 1e-8f)
                {
                    r = Vector3.Cross(Vector3.UnitX, f);
                }
            }
            r = Vector3.Normalize(r);
            u = Vector3.Cross(f, r);
            return true;
        }

        private static float Tan(float sideDegrees)
        {
            float side = Math.Max(0.1f, Math.Min(MaxHalfAngle, sideDegrees));
            return (float)Math.Tan(side * Math.PI / 180.0);
        }

        private static float Clamp(float value, float min, float max)
        {
            return value < min ? min : value > max ? max : value;
        }
    }
}
