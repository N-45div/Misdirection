using System;
using Misdirection.Core.Tricks;

namespace Misdirection.Core.Crowd
{
    public enum ArmPose { Rest, Folded, Phone, Clap, Point, HandsUp }

    /// <summary>What a spectator's face and body show this frame. All values are targets; visuals ease toward them.</summary>
    public struct Expression
    {
        /// <summary>-1 lowered, 0 neutral, 1 raised high.</summary>
        public float BrowRaise;
        /// <summary>0 relaxed, 1 fully furrowed (inner ends down).</summary>
        public float BrowFurrow;
        /// <summary>0 closed, 1 wide open ("O").</summary>
        public float MouthOpen;
        /// <summary>-1 frown, 0 flat, 1 smile.</summary>
        public float MouthCurve;
        /// <summary>-1 leaning back, 0 upright, 1 leaning in toward the table.</summary>
        public float Lean;
        public ArmPose Arms;
        /// <summary>0..1 position in the clapping cycle (only when clapping).</summary>
        public float ClapPhase;
    }

    /// <summary>
    /// Turns a spectator's state (suspicion, eye contact, looking away, the latest reaction) into an
    /// <see cref="Expression"/>, so the crowd reads clearly from across the room (Day 7). Pure logic.
    /// </summary>
    public static class ExpressionModel
    {
        public const double ReactionSeconds = 2.5;
        public const float ClapsPerSecond = 3.2f;

        public static Expression Evaluate(Archetype archetype, SuspicionLevel level, bool eyeContact, bool lookingAway,
                                          Reaction reaction, double reactionAge, bool reactionIsMine, float time)
        {
            Expression e = Resting(archetype, level, eyeContact, lookingAway);
            if (reaction == Reaction.None || reactionAge < 0 || reactionAge > ReactionSeconds)
            {
                return e;
            }
            float fade = (float)Math.Max(0.0, 1.0 - reactionAge / ReactionSeconds);
            switch (reaction)
            {
                case Reaction.Applause:
                    e.BrowRaise = Math.Max(e.BrowRaise, 0.7f);
                    e.BrowFurrow = 0f;
                    e.MouthCurve = 1f;
                    e.MouthOpen = 0.35f * fade;
                    e.Lean = 0.4f;
                    e.Arms = archetype == Archetype.Filmer ? ArmPose.Phone : ArmPose.Clap;
                    e.ClapPhase = Frac(time * ClapsPerSecond + Seed(archetype));
                    break;
                case Reaction.Gasp:
                    e.BrowRaise = 1f;
                    e.BrowFurrow = 0f;
                    e.MouthOpen = 0.6f + 0.4f * fade;
                    e.MouthCurve = 0f;
                    e.Lean = -0.6f * fade;
                    e.Arms = archetype == Archetype.Filmer ? ArmPose.Phone : ArmPose.HandsUp;
                    break;
                case Reaction.Heckle:
                    if (reactionIsMine)
                    {
                        // "I saw that!": frown, furrow, lean in and point at the magician's hands.
                        e.BrowRaise = -0.3f;
                        e.BrowFurrow = 1f;
                        e.MouthOpen = 0.5f * fade;
                        e.MouthCurve = -1f;
                        e.Lean = 0.7f;
                        e.Arms = ArmPose.Point;
                    }
                    else
                    {
                        e.BrowFurrow = Math.Max(e.BrowFurrow, 0.4f);
                        e.MouthCurve = Math.Min(e.MouthCurve, -0.3f);
                    }
                    break;
            }
            return e;
        }

        /// <summary>Between reveals: suspicion shows in the brows and lean; eye contact gets a small smile.</summary>
        public static Expression Resting(Archetype archetype, SuspicionLevel level, bool eyeContact, bool lookingAway)
        {
            Expression e = new Expression();
            switch (level)
            {
                case SuspicionLevel.Curious:
                    e.BrowRaise = 0.4f;
                    e.Lean = 0.5f;
                    break;
                case SuspicionLevel.Suspicious:
                    e.BrowRaise = -0.2f;
                    e.BrowFurrow = 0.7f;
                    e.MouthCurve = -0.4f;
                    e.Lean = -0.3f;
                    break;
                case SuspicionLevel.WalkingOff:
                    e.BrowFurrow = 1f;
                    e.MouthCurve = -0.8f;
                    e.Lean = -0.8f;
                    break;
                default:
                    e.Lean = 0.2f;
                    break;
            }
            if (eyeContact && level != SuspicionLevel.WalkingOff)
            {
                e.MouthCurve = Math.Max(e.MouthCurve, 0.4f);
                e.BrowRaise = Math.Max(e.BrowRaise, 0.2f);
            }
            if (lookingAway)
            {
                e.BrowFurrow = Math.Max(e.BrowFurrow, 0.3f);
                e.MouthCurve = Math.Min(e.MouthCurve, -0.2f);
            }
            switch (archetype)
            {
                case Archetype.Skeptic: e.Arms = ArmPose.Folded; break;
                case Archetype.Filmer: e.Arms = ArmPose.Phone; break;
                default: e.Arms = ArmPose.Rest; break;
            }
            return e;
        }

        private static float Seed(Archetype a)
        {
            // Different spectators clap slightly out of time with each other.
            return ((int)a * 0.37f) % 1f;
        }

        private static float Frac(float v)
        {
            return v - (float)Math.Floor(v);
        }
    }
}
