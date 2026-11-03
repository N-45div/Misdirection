using System;

namespace Misdirection.Core.Crowd
{
    public enum Archetype { Tourist, Kid, Skeptic, Filmer, Regular }

    /// <summary>
    /// How one kind of spectator pays attention. Weights are relative pulls toward each stimulus;
    /// the crowd model blends them (docs/DESIGN.md, "Spectator archetypes").
    /// </summary>
    [Serializable]
    public sealed class SpectatorProfile
    {
        public Archetype Archetype;
        /// <summary>Pull toward wherever the magician is looking.</summary>
        public float GazeWeight = 1f;
        /// <summary>Pull toward the show hand's flourish.</summary>
        public float FlourishWeight = 0.5f;
        /// <summary>Pull toward sounds ("Look!", a coin clink).</summary>
        public float SoundWeight = 0.5f;
        /// <summary>Baseline interest in the magician's hands, where the action is.</summary>
        public float HandsWeight = 0.3f;
        /// <summary>Half-angle of the attention cone, degrees. Inside it, a secret move is seen.</summary>
        public float ConeHalfAngle = 12f;
        /// <summary>How fast attention moves to a new target (1/s).</summary>
        public float TurnRate = 4f;
        /// <summary>Seconds of eye contact before the spectator gets uncomfortable and looks away.</summary>
        public float StareTolerance = 3.5f;
        /// <summary>Multiplier on suspicion gained from seen methods.</summary>
        public float SuspicionGain = 1f;
        /// <summary>Filmer: the phone's wide cone stays on the mat whatever the person looks at.</summary>
        public bool FixedConeOnStage;
        /// <summary>Standing eye height above the floor, metres (for placement).</summary>
        public float EyeHeight = 1.55f;

        public static SpectatorProfile For(Archetype archetype)
        {
            SpectatorProfile p = new SpectatorProfile();
            p.Archetype = archetype;
            switch (archetype)
            {
                case Archetype.Tourist:
                    // Follows your gaze faithfully.
                    p.GazeWeight = 1.0f; p.FlourishWeight = 0.5f; p.SoundWeight = 0.5f; p.HandsWeight = 0.25f;
                    p.ConeHalfAngle = 12f; p.TurnRate = 4.5f; p.StareTolerance = 4.0f; p.SuspicionGain = 0.8f;
                    break;
                case Archetype.Kid:
                    // Follows sounds and flourishes.
                    p.GazeWeight = 0.35f; p.FlourishWeight = 1.2f; p.SoundWeight = 1.2f; p.HandsWeight = 0.3f;
                    p.ConeHalfAngle = 14f; p.TurnRate = 6f; p.StareTolerance = 2.5f; p.SuspicionGain = 0.7f; p.EyeHeight = 1.15f;
                    break;
                case Archetype.Skeptic:
                    // Watches your hands whatever you do; eye contact is the way to win her.
                    p.GazeWeight = 0.2f; p.FlourishWeight = 0.15f; p.SoundWeight = 0.2f; p.HandsWeight = 1.2f;
                    p.ConeHalfAngle = 10f; p.TurnRate = 3f; p.StareTolerance = 3.0f; p.SuspicionGain = 1.4f;
                    break;
                case Archetype.Filmer:
                    // Holds a phone with a wide, fixed cone on the mat.
                    p.GazeWeight = 0.5f; p.FlourishWeight = 0.4f; p.SoundWeight = 0.6f; p.HandsWeight = 0.5f;
                    p.ConeHalfAngle = 28f; p.TurnRate = 3f; p.StareTolerance = 5f; p.SuspicionGain = 0.6f; p.FixedConeOnStage = true;
                    break;
                case Archetype.Regular:
                    // Like a tourist, but remembers your methods (memory arrives with Day 10 retention).
                    p.GazeWeight = 0.8f; p.FlourishWeight = 0.4f; p.SoundWeight = 0.4f; p.HandsWeight = 0.5f;
                    p.ConeHalfAngle = 12f; p.TurnRate = 4f; p.StareTolerance = 4f; p.SuspicionGain = 1.1f;
                    break;
            }
            return p;
        }
    }
}
