using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Fully synthesized audio (no external files): wind ambience, day birds / night crickets,
    /// a gentle generative exploration theme and combat / UI sound effects.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }
        public float masterVolume = 0.8f;
        public float musicVolume = 0.35f;
        public float sfxVolume = 0.8f;

        const int Rate = 44100;
        readonly Dictionary<string, AudioClip> clips = new();
        AudioSource wind, music, nature;
        readonly List<AudioSource> sfx = new();
        int next;
        DayNightCycle cycle;
        WindController windCtl;
        System.Random rng = new(5);

        void Awake()
        {
            Instance = this;
            wind = MakeSource(true);
            music = MakeSource(true);
            nature = MakeSource(false);
            for (int i = 0; i < 12; i++) sfx.Add(MakeSource(false));

            clips["wind"] = Make(8f, (t, i) => Noise() * (0.5f + 0.5f * Mathf.PerlinNoise(t * 0.4f, 0.3f)), 0.985f, 0.35f);
            clips["swing"] = Make(0.22f, (t, i) => Noise() * Mathf.Sin(Mathf.PI * t / 0.22f), 0.6f, 0.5f);
            clips["hit"] = Make(0.18f, (t, i) => (Noise() * 0.6f + Mathf.Sin(t * 2f * Mathf.PI * 110f) * 0.8f) * Mathf.Exp(-t * 28f), 0.3f, 0.8f);
            clips["slam"] = Make(0.6f, (t, i) => (Noise() * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * (60f - t * 40f))) * Mathf.Exp(-t * 7f), 0.85f, 0.9f);
            clips["dodge"] = Make(0.25f, (t, i) => Noise() * Mathf.Sin(Mathf.PI * t / 0.25f) * 0.7f, 0.92f, 0.5f);
            clips["skill"] = Make(0.7f, (t, i) => (Noise() * 0.4f + Mathf.Sin(t * 2f * Mathf.PI * (300f + t * 500f)) * 0.5f) * Mathf.Exp(-t * 4f), 0.5f, 0.6f);
            clips["burst"] = Make(1.4f, (t, i) => (Noise() * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * (80f + t * 220f)) * 0.7f) * Mathf.Exp(-t * 2f), 0.6f, 0.8f);
            clips["parry"] = Make(0.6f, (t, i) => (Mathf.Sin(t * 2f * Mathf.PI * 1320f) + Mathf.Sin(t * 2f * Mathf.PI * 1980f) * 0.5f) * Mathf.Exp(-t * 6f), 0f, 0.5f);
            clips["telegraph"] = Make(0.35f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * 880f) * Mathf.Exp(-t * 8f), 0f, 0.35f);
            clips["hurt"] = Make(0.25f, (t, i) => (Noise() * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 180f)) * Mathf.Exp(-t * 14f), 0.4f, 0.6f);
            clips["death"] = Make(0.8f, (t, i) => Noise() * Mathf.Exp(-t * 5f), 0.9f, 0.6f);
            clips["switch"] = Make(0.4f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * (520f + t * 600f)) * Mathf.Exp(-t * 7f), 0f, 0.35f);
            clips["pickup"] = Arpeggio(new[] { 784f, 988f, 1175f }, 0.09f, 0.4f);
            clips["chest"] = Arpeggio(new[] { 523f, 659f, 784f, 1047f, 1319f }, 0.11f, 0.45f);
            clips["quest"] = Arpeggio(new[] { 392f, 523f, 659f, 784f }, 0.18f, 0.5f);
            clips["ui"] = Make(0.06f, (t, i) => Mathf.Sin(t * 2f * Mathf.PI * 1400f) * Mathf.Exp(-t * 60f), 0f, 0.3f);
            clips["bird"] = Bird();
            clips["cricket"] = Cricket();
            clips["music"] = Music();

            wind.clip = clips["wind"];
            wind.volume = 0.25f;
            wind.Play();
            music.clip = clips["music"];
            music.volume = musicVolume * masterVolume;
            music.Play();
        }

        void Start()
        {
            cycle = FindAnyObjectByType<DayNightCycle>();
            windCtl = FindAnyObjectByType<WindController>();
        }

        AudioSource MakeSource(bool loop)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.loop = loop;
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            return s;
        }

        void Update()
        {
            AudioListener.volume = masterVolume;
            float gust = windCtl ? windCtl.CurrentStrength : 1f;
            wind.volume = Mathf.Lerp(wind.volume, 0.12f + gust * 0.12f, Time.unscaledDeltaTime);
            wind.pitch = 0.85f + gust * 0.15f;
            bool combat = PlayerCombat.Instance && PlayerCombat.Instance.InCombat;
            music.volume = Mathf.Lerp(music.volume, musicVolume * (combat ? 1.3f : 1f), Time.unscaledDeltaTime);
            music.pitch = Mathf.Lerp(music.pitch, combat ? 1.06f : 1f, Time.unscaledDeltaTime);

            if (!nature.isPlaying && Random.value < Time.deltaTime * 0.25f)
            {
                bool day = !cycle || cycle.DayFactor > 0.4f;
                nature.clip = clips[day ? "bird" : "cricket"];
                nature.volume = (day ? 0.18f : 0.12f) * sfxVolume;
                nature.pitch = Random.Range(0.85f, 1.2f);
                nature.Play();
            }
        }

        public static void Play(string id, float pitch = 1f)
        {
            if (!Instance || !Instance.clips.TryGetValue(id, out var clip)) return;
            var s = Instance.sfx[Instance.next++ % Instance.sfx.Count];
            s.pitch = pitch * Random.Range(0.95f, 1.05f);
            s.volume = Instance.sfxVolume;
            s.PlayOneShot(clip);
        }

        // ---------------------------------------------------------------- Synthesis

        float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

        /// <summary>Generates a clip from a sample function with a one-pole low-pass (smoothing 0..1).</summary>
        AudioClip Make(float seconds, System.Func<float, int, float> f, float smoothing, float gain)
        {
            int n = (int)(seconds * Rate);
            var data = new float[n];
            float lp = 0f;
            for (int i = 0; i < n; i++)
            {
                float s = f(i / (float)Rate, i);
                lp = Mathf.Lerp(s, lp, smoothing);
                data[i] = Mathf.Clamp(lp * gain, -1f, 1f);
            }
            // tiny fade to avoid clicks
            int fade = Mathf.Min(200, n / 4);
            for (int i = 0; i < fade; i++)
            {
                data[i] *= i / (float)fade;
                data[n - 1 - i] *= i / (float)fade;
            }
            var clip = AudioClip.Create("synth", n, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        AudioClip Arpeggio(float[] notes, float step, float gain) =>
            Make(step * notes.Length + 0.5f, (t, i) =>
            {
                float s = 0f;
                for (int k = 0; k < notes.Length; k++)
                {
                    float lt = t - k * step;
                    if (lt < 0f) continue;
                    s += (Mathf.Sin(lt * 2f * Mathf.PI * notes[k]) + 0.3f * Mathf.Sin(lt * 4f * Mathf.PI * notes[k])) * Mathf.Exp(-lt * 5f);
                }
                return s * 0.4f;
            }, 0f, gain);

        AudioClip Bird() =>
            Make(1.6f, (t, i) =>
            {
                float chirp = Mathf.Repeat(t, 0.4f);
                if (chirp > 0.16f || t > 1.3f) return 0f;
                float f = 2800f + Mathf.Sin(chirp * 60f) * 600f + chirp * 3000f;
                return Mathf.Sin(t * 2f * Mathf.PI * f) * Mathf.Sin(Mathf.PI * chirp / 0.16f);
            }, 0.1f, 0.4f);

        AudioClip Cricket() =>
            Make(2f, (t, i) =>
            {
                float pulse = Mathf.Repeat(t, 0.5f) < 0.18f ? 1f : 0f;
                return Mathf.Sin(t * 2f * Mathf.PI * 4400f) * (Mathf.Repeat(t * 30f, 1f) < 0.5f ? 1f : 0f) * pulse;
            }, 0.2f, 0.3f);

        /// <summary>Soft 32-second pad loop: Am - F - C - G with a slow pentatonic melody.</summary>
        AudioClip Music()
        {
            float[][] chords =
            {
                new[] { 220f, 261.6f, 329.6f }, new[] { 174.6f, 220f, 261.6f },
                new[] { 261.6f, 329.6f, 392f }, new[] { 196f, 246.9f, 293.7f },
            };
            float[] melody = { 659.3f, 587.3f, 523.3f, 440f, 523.3f, 587.3f, 659.3f, 784f, 659.3f, 587.3f, 523.3f, 587.3f, 440f, 392f, 440f, 523.3f };
            const float bar = 8f;
            return Make(32f, (t, i) =>
            {
                int c = (int)(t / bar) % 4;
                float lt = t % bar;
                float env = Mathf.Min(1f, lt / 2f) * Mathf.Min(1f, (bar - lt) / 1.5f);
                float s = 0f;
                foreach (var f in chords[c])
                    s += Mathf.Sin(t * 2f * Mathf.PI * f) * 0.18f + Mathf.Sin(t * 2f * Mathf.PI * f * 2.001f) * 0.04f;
                s *= env;
                int m = (int)(t / 2f) % melody.Length;
                float mt = t % 2f;
                s += Mathf.Sin(t * 2f * Mathf.PI * melody[m]) * 0.12f * Mathf.Exp(-mt * 1.2f) * Mathf.Min(1f, mt * 20f);
                return s;
            }, 0.5f, 0.5f);
        }
    }
}
