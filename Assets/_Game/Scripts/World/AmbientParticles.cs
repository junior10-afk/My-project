using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Atmosphere particles that follow the camera: floating pollen / dust motes by day and
    /// glowing fireflies at night. Emission rates follow the day/night cycle.
    /// </summary>
    public class AmbientParticles : MonoBehaviour
    {
        public Material moteMaterial;
        public Material fireflyMaterial;
        public float dayMoteRate = 25f;
        public float nightFireflyRate = 18f;

        ParticleSystem motes, fireflies;
        DayNightCycle cycle;
        Transform cam;

        void Start()
        {
            cycle = FindAnyObjectByType<DayNightCycle>();
            cam = Camera.main ? Camera.main.transform : null;
            motes = Create("Poussières", moteMaterial, new Color(1f, 0.95f, 0.8f, 0.5f), 0.025f, 0.06f, 0.15f, 8f, 50f);
            fireflies = Create("Lucioles", fireflyMaterial, new Color(2.2f, 3f, 0.8f, 1f), 0.06f, 0.12f, 0.8f, 6f, 40f);
        }

        ParticleSystem Create(string name, Material mat, Color color, float minSize, float maxSize, float noise, float lifetime, float area)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * 0.6f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(minSize, maxSize);
            main.startColor = color;
            main.maxParticles = 600;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0f;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(area, 8f, area);

            var noiseModule = ps.noise;
            noiseModule.enabled = true;
            noiseModule.strength = noise;
            noiseModule.frequency = 0.3f;
            noiseModule.scrollSpeed = 0.2f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            col.color = g;

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = mat;
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ps.Play();
            return ps;
        }

        void LateUpdate()
        {
            if (cam) transform.position = cam.position;
            float day = cycle ? cycle.DayFactor : 1f;

            var e1 = motes.emission;
            e1.rateOverTime = dayMoteRate * day;
            var e2 = fireflies.emission;
            e2.rateOverTime = nightFireflyRate * Mathf.Clamp01(1f - day * 1.5f);
        }
    }
}
