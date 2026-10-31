using UnityEngine;
using UnityEngine.Rendering;

namespace LastWorld
{
    public enum RegionWeather { None, Embers, Ash, Rain, Snow, Pale, Sparks, Motes }

    /// <summary>
    /// A named region of Aerendal (Cendrelune, Ysmère…): entering it shows its name, tints the fog and ambient
    /// light and fills the air with the region's weather (embers, rain, snow, pale motes…). Blends in and out
    /// smoothly over the outer 30 % of its radius.
    /// </summary>
    public class RegionZone : MonoBehaviour
    {
        public static RegionZone Current { get; private set; }

        public string regionName = "Région";
        public float radius = 220f;
        public Color fogColor = new(0.6f, 0.6f, 0.65f);
        [Range(0f, 1f)] public float fogStrength = 0.5f;
        public float fogDensityMultiplier = 1.5f;
        public Color ambientTint = Color.white;
        public RegionWeather weather = RegionWeather.None;
        public float weatherRate = 40f;

        static float baseDensity = -1f;
        static string lastBanner;
        float weight;
        bool applied;
        ParticleSystem ps;
        Transform cam;

        void Start()
        {
            if (baseDensity < 0f) baseDensity = RenderSettings.fogDensity;
            cam = Camera.main ? Camera.main.transform : null;
            if (weather != RegionWeather.None) ps = BuildWeather();
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            if (ps) Destroy(ps.gameObject);
        }

        void LateUpdate()
        {
            var p = PlayerController.Instance;
            if (!p) return;
            Vector3 d = p.transform.position - transform.position;
            d.y = 0f;
            float dist = d.magnitude;
            weight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(radius, radius * 0.7f, dist));

            if (weight > 0.5f && Current != this)
            {
                Current = this;
                if (lastBanner != regionName && !CinematicDirector.Playing)
                {
                    lastBanner = regionName;
                    GameUI.Banner(regionName, Color.Lerp(fogColor, Color.white, 0.5f));
                }
            }
            else if (weight < 0.2f && Current == this) Current = null;

            if (weight > 0.001f)
            {
                applied = true;
                RenderSettings.fogColor = Color.Lerp(RenderSettings.fogColor, fogColor, fogStrength * weight);
                RenderSettings.fogDensity = Mathf.Lerp(baseDensity, baseDensity * fogDensityMultiplier, weight);
                RenderSettings.ambientSkyColor = Color.Lerp(RenderSettings.ambientSkyColor, RenderSettings.ambientSkyColor * ambientTint, weight);
                RenderSettings.ambientEquatorColor = Color.Lerp(RenderSettings.ambientEquatorColor, RenderSettings.ambientEquatorColor * ambientTint, weight);
            }
            else if (applied)
            {
                applied = false;
                if (Current == null) RenderSettings.fogDensity = baseDensity;
            }

            if (ps)
            {
                if (cam) ps.transform.position = cam.position;
                var e = ps.emission;
                e.rateOverTime = weatherRate * weight;
            }
        }

        // ---------------------------------------------------------------- Weather

        ParticleSystem BuildWeather()
        {
            var amb = FindAnyObjectByType<AmbientParticles>();
            Material soft = amb ? amb.moteMaterial : null;
            Material glow = amb ? amb.fireflyMaterial : null;

            float gravity = 0f, speed = 0.2f, size0 = 0.05f, size1 = 0.1f, life = 7f, noise = 0.5f, area = 50f;
            Color color = Color.white;
            Material mat = soft;
            bool stretch = false;
            switch (weather)
            {
                case RegionWeather.Embers: gravity = -0.05f; speed = 0.8f; size0 = 0.04f; size1 = 0.1f; life = 6f; noise = 1.2f; color = new Color(3f, 1.1f, 0.3f, 1f); mat = glow; break;
                case RegionWeather.Ash: gravity = 0.04f; speed = 0.2f; size0 = 0.05f; size1 = 0.13f; life = 9f; noise = 0.8f; color = new Color(0.55f, 0.52f, 0.5f, 0.8f); break;
                case RegionWeather.Rain: gravity = 2.5f; speed = 6f; size0 = 0.03f; size1 = 0.05f; life = 1.4f; noise = 0.1f; color = new Color(0.6f, 0.75f, 1f, 0.55f); stretch = true; break;
                case RegionWeather.Snow: gravity = 0.03f; speed = 0.3f; size0 = 0.05f; size1 = 0.1f; life = 10f; noise = 0.6f; color = new Color(1f, 1f, 1f, 0.9f); break;
                case RegionWeather.Pale: gravity = -0.01f; speed = 0.15f; size0 = 0.08f; size1 = 0.2f; life = 9f; noise = 0.4f; color = new Color(1f, 1f, 1f, 0.55f); break;
                case RegionWeather.Sparks: gravity = 0f; speed = 1.2f; size0 = 0.03f; size1 = 0.07f; life = 1.6f; noise = 2f; color = new Color(1.6f, 1.3f, 3.2f, 1f); mat = glow; break;
                case RegionWeather.Motes: gravity = -0.02f; speed = 0.2f; size0 = 0.04f; size1 = 0.1f; life = 8f; noise = 0.5f; color = new Color(1.5f, 2.4f, 2.2f, 1f); mat = glow; break;
            }

            var go = new GameObject($"Météo — {regionName}");
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(life * 0.6f, life);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.5f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size0, size1);
            main.startColor = color;
            main.maxParticles = 1200;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            var emission = system.emission;
            emission.rateOverTime = 0f;
            var shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area, weather == RegionWeather.Rain || weather == RegionWeather.Snow || weather == RegionWeather.Ash ? 4f : 10f, area);
            shape.position = new Vector3(0f, weather == RegionWeather.Rain || weather == RegionWeather.Snow || weather == RegionWeather.Ash ? 12f : 1f, 0f);
            var n = system.noise;
            n.enabled = true;
            n.strength = noise;
            n.frequency = 0.3f;
            var col = system.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.75f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var r = go.GetComponent<ParticleSystemRenderer>();
            if (mat) r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            if (stretch)
            {
                r.renderMode = ParticleSystemRenderMode.Stretch;
                r.lengthScale = 6f;
            }
            system.Play();
            return system;
        }
    }
}
