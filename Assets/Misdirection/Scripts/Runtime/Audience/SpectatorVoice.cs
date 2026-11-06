using Misdirection.Core.Crowd;
using Misdirection.Core.Tricks;
using UnityEngine;

namespace Misdirection.Audience
{
    /// <summary>
    /// A spectator's sound, placed at their head (Day 7): a quiet murmur that grows with curiosity and
    /// suspicion, and their own gasp, clapping or "hm!" when the crowd reacts. Generated placeholder
    /// audio until the sound pass; everything is 3-D so the room sounds like people stand where they do.
    /// </summary>
    public sealed class SpectatorVoice : MonoBehaviour
    {
        private const int Rate = 48000;

        private static AudioClip murmurClip;
        private static AudioClip gaspClip;
        private static AudioClip clapClip;
        private static AudioClip hmClip;
        private static AudioClip ooClip;

        private AudioSource murmur;
        private AudioSource oneShot;
        private float pitch = 1f;

        public void Build(Archetype archetype)
        {
            EnsureClips();
            pitch = archetype == Archetype.Kid ? 1.35f : archetype == Archetype.Skeptic ? 0.92f : 1f + Random.Range(-0.06f, 0.06f);
            murmur = Source("Murmur", 0.6f);
            murmur.clip = murmurClip;
            murmur.loop = true;
            murmur.volume = 0f;
            murmur.time = Random.Range(0f, murmurClip.length);
            murmur.Play();
            oneShot = Source("Voice", 1f);
        }

        /// <summary>Call every frame: murmur louder when curious or suspicious, quiet during eye contact.</summary>
        public void Tick(SuspicionLevel level, bool eyeContact)
        {
            if (murmur == null)
            {
                return;
            }
            float target = level == SuspicionLevel.Calm ? 0.06f : level == SuspicionLevel.Curious ? 0.12f : 0.2f;
            if (eyeContact)
            {
                target *= 0.3f;
            }
            murmur.volume = Mathf.MoveTowards(murmur.volume, target, Time.deltaTime * 0.3f);
        }

        public void React(Reaction reaction, bool mine)
        {
            if (oneShot == null)
            {
                return;
            }
            oneShot.pitch = pitch * Random.Range(0.95f, 1.05f);
            switch (reaction)
            {
                case Reaction.Applause:
                    oneShot.PlayOneShot(clapClip, 0.8f);
                    oneShot.PlayOneShot(ooClip, 0.35f);
                    break;
                case Reaction.Gasp:
                    oneShot.PlayOneShot(gaspClip, 0.9f);
                    break;
                case Reaction.Heckle:
                    if (mine)
                    {
                        oneShot.PlayOneShot(hmClip, 1f);
                    }
                    break;
            }
        }

        private AudioSource Source(string name, float spatial)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = spatial;
            s.rolloffMode = AudioRolloffMode.Linear;
            s.minDistance = 0.5f;
            s.maxDistance = 6f;
            s.dopplerLevel = 0f;
            return s;
        }

        private static void EnsureClips()
        {
            if (murmurClip != null)
            {
                return;
            }
            System.Random rng = new System.Random(42);
            murmurClip = Make("murmur", 4f, (t, r) =>
            {
                // Band-limited chatter: noise shaped by a few slow, overlapping "syllable" envelopes.
                float syllables = 0.5f + 0.5f * Mathf.Sin(t * 7.3f) * Mathf.Sin(t * 3.1f + 1f);
                return (float)(r.NextDouble() * 2 - 1) * syllables;
            }, rng, 0.08f, 0.25f, loopFade: true);
            gaspClip = Make("gasp", 0.7f, (t, r) =>
            {
                float env = Mathf.Clamp01(t / 0.05f) * Mathf.Clamp01((0.7f - t) / 0.4f);
                return (float)(r.NextDouble() * 2 - 1) * env;
            }, rng, 0.25f, 0.6f);
            clapClip = Make("claps", 2.2f, (t, r) =>
            {
                // ~3.4 claps a second with jitter, fading out.
                float period = 0.29f;
                float local = (t + 0.03f * Mathf.Sin(t * 13f)) % period;
                float clap = Mathf.Exp(-local * 70f);
                float fade = Mathf.Clamp01((2.2f - t) / 0.8f);
                return (float)(r.NextDouble() * 2 - 1) * clap * fade;
            }, rng, 0.9f, 0.9f);
            hmClip = Make("hm", 0.45f, (t, r) =>
            {
                // A low "hm!" with a falling pitch.
                float f = Mathf.Lerp(190f, 140f, t / 0.45f);
                float env = Mathf.Clamp01(t / 0.02f) * Mathf.Clamp01((0.45f - t) / 0.15f);
                return (Mathf.Sin(2f * Mathf.PI * f * t) + 0.4f * Mathf.Sin(4f * Mathf.PI * f * t)) * env;
            }, rng, 1f, 0.5f);
            ooClip = Make("ooh", 0.8f, (t, r) =>
            {
                float f = Mathf.Lerp(220f, 300f, t / 0.8f);
                float env = Mathf.Clamp01(t / 0.1f) * Mathf.Clamp01((0.8f - t) / 0.3f);
                return (Mathf.Sin(2f * Mathf.PI * f * t) + 0.3f * (float)(rng.NextDouble() * 2 - 1)) * env;
            }, rng, 0.5f, 0.35f);
        }

        /// <summary>Renders a clip; <paramref name="smoothing"/> 0..1 low-passes the signal (1 = raw).</summary>
        private static AudioClip Make(string name, float seconds, System.Func<float, System.Random, float> sample, System.Random rng,
                                      float smoothing, float volume, bool loopFade = false)
        {
            int n = Mathf.CeilToInt(seconds * Rate);
            float[] data = new float[n];
            float y = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = (float)i / Rate;
                y = Mathf.Lerp(y, sample(t, rng), smoothing);
                data[i] = y * volume;
            }
            if (loopFade)
            {
                // Crossfade the ends so the loop has no click.
                int fade = Rate / 10;
                for (int i = 0; i < fade; i++)
                {
                    float w = (float)i / fade;
                    data[i] = data[i] * w + data[n - fade + i] * (1f - w);
                }
            }
            AudioClip clip = AudioClip.Create("spectator_" + name, n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
