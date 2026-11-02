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
