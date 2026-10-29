using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;

namespace LastWorld
{
    /// <summary>
    /// Drives sun, moon, ambient light, fog and sky exposure from an in-game clock.
    /// Hold T to fast-forward time.
    /// </summary>
    public class DayNightCycle : MonoBehaviour
    {
        public Light sun;
        public Light moon;
        [Tooltip("Realtime probe re-rendered periodically so reflections follow the sky.")]
        public ReflectionProbe skyProbe;
        public float probeRefreshInterval = 2f;
        [Range(0f, 24f)] public float timeOfDay = 8.5f;
        [Tooltip("Real minutes for a full in-game day.")]
        public float dayLengthMinutes = 24f;
        public float sunYaw = 160f;
        public float maxSunIntensity = 2.2f;
        public float maxMoonIntensity = 0.35f;
        public float fastForwardMultiplier = 60f;

        /// <summary>0 at night, 1 in full daylight.</summary>
        public float DayFactor { get; private set; } = 1f;

        float nextProbeTime;

        static readonly Color NightSky = new(0.05f, 0.07f, 0.15f);
        static readonly Color DaySky = new(0.52f, 0.63f, 0.80f);
        static readonly Color DuskSky = new(0.70f, 0.45f, 0.38f);
        static readonly Color NightFog = new(0.04f, 0.05f, 0.10f);
        static readonly Color DayFog = new(0.62f, 0.73f, 0.88f);
        static readonly Color DuskFog = new(0.85f, 0.58f, 0.45f);

        public string TimeString
        {
            get
            {
                int h = (int)timeOfDay;
                int m = (int)((timeOfDay - h) * 60f);
                return $"{h:00}:{m:00}";
            }
        }

        void Start()
        {
            // Work on a copy so play mode doesn't rewrite the skybox asset.
            if (RenderSettings.skybox) RenderSettings.skybox = new Material(RenderSettings.skybox);
            Apply();
        }

        void Update()
        {
            float speed = 24f / (dayLengthMinutes * 60f);
            var kb = Keyboard.current;
            if (kb != null && kb.tKey.isPressed) speed *= fastForwardMultiplier;
            timeOfDay = Mathf.Repeat(timeOfDay + speed * Time.deltaTime, 24f);
            Apply();

            if (skyProbe && Time.time >= nextProbeTime)
            {
                nextProbeTime = Time.time + probeRefreshInterval;
                skyProbe.RenderProbe();
            }
        }

        public void Apply()
        {
            float angle = timeOfDay / 24f * 360f - 90f;
            float elevation = Mathf.Sin(angle * Mathf.Deg2Rad);
            float day = Smooth(-0.12f, 0.2f, elevation);
            float dusk = Mathf.Clamp01(1f - Mathf.Abs(elevation - 0.05f) / 0.3f);
            DayFactor = day;

            if (sun)
            {
                sun.transform.rotation = Quaternion.Euler(angle, sunYaw, 0f);
                sun.intensity = maxSunIntensity * Smooth(-0.05f, 0.15f, elevation);
                sun.color = Color.Lerp(new Color(1f, 0.93f, 0.84f), new Color(1f, 0.52f, 0.28f), dusk);
                sun.enabled = elevation > -0.08f;
                RenderSettings.sun = sun;
            }
            if (moon)
            {
                moon.transform.rotation = Quaternion.Euler(angle + 180f, sunYaw, 0f);
                moon.intensity = maxMoonIntensity * (1f - day);
                moon.enabled = elevation < 0.1f;
            }

            Color sky = Color.Lerp(Color.Lerp(NightSky, DaySky, day), DuskSky, dusk * 0.5f);
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = sky;
            RenderSettings.ambientEquatorColor = sky * 0.75f;
            RenderSettings.ambientGroundColor = sky * 0.4f;
            RenderSettings.fogColor = Color.Lerp(Color.Lerp(NightFog, DayFog, day), DuskFog, dusk * 0.6f);

            var skybox = RenderSettings.skybox;
            if (skybox && skybox.HasProperty("_Exposure"))
                skybox.SetFloat("_Exposure", Mathf.Lerp(0.12f, 1.3f, day));
        }

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }
    }
}
