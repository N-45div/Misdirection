using UnityEngine;

namespace Misdirection
{
    /// <summary>Generated placeholder sounds (until the art/audio pass replaces them).</summary>
    public static class Tones
    {
        public static AudioClip Beep(string name, float frequency, float seconds, float volume = 0.5f)
        {
            const int rate = 48000;
            int samples = Mathf.Max(1, Mathf.CeilToInt(rate * seconds));
            float[] data = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / rate;
                float envelope = Mathf.Clamp01(time / 0.005f) * Mathf.Clamp01((seconds - time) / 0.04f);
                data[i] = Mathf.Sin(2f * Mathf.PI * frequency * time) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Shaped noise: <paramref name="crackle"/> 0 gives a soft breath (gasp), 1 gives dense clapping (applause).
        /// Deterministic (fixed seed) so builds sound the same.
        /// </summary>
        public static AudioClip Noise(string name, float seconds, float attack, float volume, float crackle, int seed = 7)
        {
            const int rate = 48000;
            int samples = Mathf.Max(1, Mathf.CeilToInt(rate * seconds));
            float[] data = new float[samples];
            System.Random random = new System.Random(seed);
            float smooth = 0f;
            float clap = 0f;
            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / rate;
                float envelope = Mathf.Clamp01(time / Mathf.Max(0.001f, attack)) * Mathf.Clamp01((seconds - time) / (seconds * 0.5f));
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                smooth = Mathf.Lerp(smooth, white, 0.15f);
                if (random.NextDouble() < 0.0012 * crackle * 4f)
                {
                    clap = 1f;
                }
                clap *= 0.996f;
                float sample = Mathf.Lerp(smooth, white * (0.35f + clap), crackle);
                data[i] = sample * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>A rising chirp (a "sparkle" for productions).</summary>
        public static AudioClip Chirp(string name, float startHz, float endHz, float seconds, float volume = 0.35f)
        {
            return Thud(name, startHz, endHz, seconds, volume);
        }

        /// <summary>A short falling "thud" (pitch drops over the clip).</summary>
        public static AudioClip Thud(string name, float startHz, float endHz, float seconds, float volume = 0.6f)
        {
            const int rate = 48000;
            int samples = Mathf.Max(1, Mathf.CeilToInt(rate * seconds));
            float[] data = new float[samples];
            float phase = 0f;
            for (int i = 0; i < samples; i++)
            {
                float time = (float)i / rate;
                float k = time / seconds;
                float freq = Mathf.Lerp(startHz, endHz, k);
                phase += 2f * Mathf.PI * freq / rate;
                float envelope = Mathf.Clamp01(time / 0.003f) * (1f - k) * (1f - k);
                data[i] = Mathf.Sin(phase) * envelope * volume;
            }
            AudioClip clip = AudioClip.Create(name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
