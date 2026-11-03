using System;
using System.Collections.Generic;
using System.Numerics;

namespace Misdirection.Core.Crowd
{
    [Serializable]
    public sealed class CrowdSettings
    {
        /// <summary>A face counts as looked at within this many degrees (Meta's minimum gaze target is 3°).</summary>
        public float FaceAngleDegrees = 6f;
        /// <summary>Once looking at a face, it stays looked at until the look drifts past this many degrees.</summary>
        public float FaceReleaseDegrees = 9f;
        /// <summary>Approximate head radius used to widen the target for close spectators (metres).</summary>
        public float FaceRadius = 0.11f;
        public double FlourishDecaySeconds = 1.2;
        public double SoundDecaySeconds = 1.0;
        /// <summary>When the look doesn't hit the table, the gaze point sits this far along the look ray.</summary>
        public float GazeDepth = 1.5f;
        /// <summary>Looks that hit the table further away than this are treated as looking ahead.</summary>
        public float MaxTableDistance = 3f;
    }

    /// <summary>Result of reporting a secret move to the crowd.</summary>
    public struct WitnessReport
    {
        /// <summary>Best coverage among spectators (0 = unseen, 1 = squarely inside someone's cone).</summary>
        public float MaxCoverage;
        /// <summary>Index of the spectator who saw it best, or -1.</summary>
        public int SeenBy;

        public bool Unseen { get { return MaxCoverage <= 0f; } }
    }

    /// <summary>
    /// The crowd's shared attention: resolves where the magician is looking ("where you look, they
    /// look"), keeps decaying flourish and sound pulses, and updates every spectator. Pure logic.
    /// </summary>
    public sealed class CrowdModel
    {
        private readonly List<SpectatorMind> spectators = new List<SpectatorMind>();
        private readonly CrowdSettings settings;
        private readonly AttentionSettings attention;
        private Vector3 flourishPoint;
        private double flourishTime = double.NegativeInfinity;
        private float flourishStrength;
        private Vector3 soundPoint;
        private double soundTime = double.NegativeInfinity;
        private float soundStrength;

        public CrowdModel(CrowdSettings settings = null, AttentionSettings attention = null)
        {
            this.settings = settings ?? new CrowdSettings();
            this.attention = attention ?? new AttentionSettings();
            LookedAt = -1;
        }

        public IReadOnlyList<SpectatorMind> Spectators { get { return spectators; } }

        /// <summary>Index of the spectator the magician is looking at, or -1.</summary>
        public int LookedAt { get; private set; }

        public Vector3 GazePoint { get; private set; }

        public Stimuli LastStimuli { get; private set; }

        public SpectatorMind Add(SpectatorProfile profile, Vector3 eyes)
        {
            SpectatorMind mind = new SpectatorMind(spectators.Count, profile, eyes, attention);
            spectators.Add(mind);
            return mind;
        }

        public void Clear()
        {
            spectators.Clear();
            LookedAt = -1;
        }

        /// <summary>The show hand made a flourish at <paramref name="point"/>.</summary>
        public void Flourish(double time, Vector3 point, float strength = 1f)
        {
            flourishPoint = point;
            flourishTime = time;
            flourishStrength = strength;
        }

        /// <summary>A sound happened at <paramref name="point"/> ("Look!", a coin clink).</summary>
        public void Sound(double time, Vector3 point, float strength = 1f)
        {
            soundPoint = point;
            soundTime = time;
            soundStrength = strength;
        }

        public void Update(double time, float dt, Vector3 magicianEyes, Vector3 gazeDirection, Vector3 handsCenter, Vector3 stage, float tableHeight)
        {
            LookedAt = ResolveGaze(magicianEyes, gazeDirection, LookedAt);
            GazePoint = LookedAt >= 0
                ? spectators[LookedAt].Eyes
                : GazePointOf(magicianEyes, gazeDirection, tableHeight, settings.GazeDepth, settings.MaxTableDistance);

            Stimuli s = new Stimuli();
            s.MagicianEyes = magicianEyes;
            s.GazePoint = GazePoint;
            s.LookedAt = LookedAt;
            s.HandsCenter = handsCenter;
            s.Stage = stage;
            s.FlourishPoint = flourishPoint;
            s.FlourishSalience = Salience(time, flourishTime, flourishStrength, settings.FlourishDecaySeconds);
            s.SoundPoint = soundPoint;
            s.SoundSalience = Salience(time, soundTime, soundStrength, settings.SoundDecaySeconds);
            LastStimuli = s;

            foreach (SpectatorMind mind in spectators)
            {
                mind.Update(time, dt, s);
            }
        }

        /// <summary>A secret move (palm, ditch, load) happened at <paramref name="point"/>.</summary>
        public WitnessReport Witness(Vector3 point, float severity)
        {
            WitnessReport report = new WitnessReport();
            report.SeenBy = -1;
            foreach (SpectatorMind mind in spectators)
            {
                float seen = mind.Witness(point, severity);
                if (seen > report.MaxCoverage)
                {
                    report.MaxCoverage = seen;
                    report.SeenBy = mind.Index;
                }
            }
            return report;
        }

        /// <summary>The secret hand is carrying something at <paramref name="point"/> this frame.</summary>
        public void Expose(Vector3 point, float dt, float ratePerSecond = 0.15f)
        {
            foreach (SpectatorMind mind in spectators)
            {
                mind.Expose(point, dt, ratePerSecond);
            }
        }

        /// <summary>Best coverage of a point right now, without adding suspicion.</summary>
        public WitnessReport Peek(Vector3 point)
        {
            WitnessReport report = new WitnessReport();
            report.SeenBy = -1;
            foreach (SpectatorMind mind in spectators)
            {
                float seen = mind.Coverage(point);
                if (seen > report.MaxCoverage)
                {
                    report.MaxCoverage = seen;
                    report.SeenBy = mind.Index;
                }
            }
            return report;
        }

        public float MaxSuspicion
        {
            get
            {
                float max = 0f;
                foreach (SpectatorMind mind in spectators)
                {
                    max = Math.Max(max, mind.Suspicion);
                }
                return max;
            }
        }

        private int ResolveGaze(Vector3 origin, Vector3 direction, int current)
        {
            if (direction.LengthSquared() < 1e-8f || spectators.Count == 0)
            {
                return -1;
            }
            Vector3 dir = Vector3.Normalize(direction);
            int best = -1;
            float bestAngle = float.MaxValue;
            for (int i = 0; i < spectators.Count; i++)
            {
                Vector3 to = spectators[i].Eyes - origin;
                float distance = to.Length();
                if (distance < 1e-4f)
                {
                    continue;
                }
                float angle = AngleDegrees(dir, to / distance);
                float widen = (float)(Math.Atan2(settings.FaceRadius, distance) * 180.0 / Math.PI);
                float limit = Math.Max(i == current ? settings.FaceReleaseDegrees : settings.FaceAngleDegrees, widen);
                if (angle <= limit && angle < bestAngle)
                {
                    best = i;
                    bestAngle = angle;
                }
            }
            return best;
        }

        /// <summary>Where a look ray lands: on the table plane when it points down at a nearby table, else a point ahead.</summary>
        public static Vector3 GazePointOf(Vector3 origin, Vector3 direction, float tableHeight, float depth, float maxTableDistance)
        {
            if (direction.LengthSquared() < 1e-8f)
            {
                return origin;
            }
            Vector3 dir = Vector3.Normalize(direction);
            if (dir.Y < -0.05f)
            {
                float t = (tableHeight - origin.Y) / dir.Y;
                if (t > 0f && t <= maxTableDistance)
                {
                    return origin + dir * t;
                }
            }
            return origin + dir * depth;
        }

        public static float AngleDegrees(Vector3 a, Vector3 b)
        {
            float cos = Vector3.Dot(a, b);
            return (float)(Math.Acos(Math.Max(-1f, Math.Min(1f, cos))) * 180.0 / Math.PI);
        }

        private static float Salience(double now, double then, float strength, double decay)
        {
            double age = now - then;
            if (double.IsInfinity(age) || age < 0 || age > decay * 5)
            {
                return 0f;
            }
            return (float)(strength * Math.Exp(-age / Math.Max(1e-3, decay)));
        }
    }
}
