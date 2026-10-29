using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Runtime combat visuals built from particles, line renderers and lights:
    /// impact bursts, slash arcs, lightning bolts, expanding shock rings and persistent zones.
    /// </summary>
    public class CombatFX : MonoBehaviour
    {
        public Material additive;
        public Material alphaBlend;

        static CombatFX instance;

        void Awake() => instance = this;

        public static void Burst(Vector3 pos, Color color, float radius, int count)
        {
            if (!instance) return;
            var go = new GameObject("FX Burst");
            go.transform.position = pos;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.duration = 0.5f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(radius * 1.2f, radius * 3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.4f);
            main.startColor = color * 2.5f;
            main.maxParticles = count;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0.25f;
            var em = ps.emission;
            em.rateOverTime = 0f;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.25f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));
            var drag = ps.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 4f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = instance.additive;
            ps.Play();
            Flash(pos, color, radius * 2.5f, 5f, 0.25f);
            Destroy(go, 2f);
        }

        public static void Flash(Vector3 pos, Color color, float range, float intensity, float life)
        {
            var l = new GameObject("FX Light").AddComponent<Light>();
            l.transform.position = pos;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            l.gameObject.AddComponent<FXFade>().life = life;
        }

        public static void Slash(Vector3 center, Quaternion rotation, float radius, Color color, float arc = 150f, float tilt = 0f)
        {
            if (!instance) return;
            var lr = Line("FX Slash", color, 0.35f, 0.22f);
            const int n = 18;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float a = Mathf.Lerp(-arc * 0.5f, arc * 0.5f, i / (n - 1f)) * Mathf.Deg2Rad;
                var local = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a)) * radius;
                lr.SetPosition(i, center + rotation * (Quaternion.Euler(0f, 0f, tilt) * local));
            }
            var curve = new AnimationCurve(new Keyframe(0f, 0.02f), new Keyframe(0.5f, 1f), new Keyframe(1f, 0.02f));
            lr.widthCurve = curve;
        }

        public static void Lightning(Vector3 from, Vector3 to, Color color)
        {
            if (!instance) return;
            var lr = Line("FX Bolt", color, 0.12f, 0.3f);
            const int n = 12;
            lr.positionCount = n;
            for (int i = 0; i < n; i++)
            {
                float t = i / (n - 1f);
                var p = Vector3.Lerp(from, to, t);
                if (i > 0 && i < n - 1) p += Random.insideUnitSphere * 0.6f;
                lr.SetPosition(i, p);
            }
            Burst(to, color, 1.2f, 18);
        }

        public static void Ring(Vector3 pos, float radius, Color color, float duration = 0.45f)
        {
            if (!instance) return;
            var lr = Line("FX Ring", color, 0.25f, duration);
            lr.loop = true;
            lr.positionCount = 40;
            var ring = lr.gameObject.AddComponent<ExpandRing>();
            ring.center = pos + Vector3.up * 0.15f;
            ring.radius = radius;
            ring.duration = duration;
        }

        /// <summary>Persistent looping area effect (caller moves / destroys it).</summary>
        public static GameObject Zone(Vector3 pos, float radius, Color color, float rate = 40f)
        {
            var go = new GameObject("FX Zone");
            go.transform.position = pos;
            if (!instance) return go;
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.15f, 0.45f);
            main.startColor = color * 2f;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = rate;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius;
            shape.rotation = new Vector3(-90f, 0f, 0f);
            var vel = ps.velocityOverLifetime;
            vel.enabled = true;
            vel.space = ParticleSystemSimulationSpace.Local;
            vel.orbitalY = 1.5f;
            vel.y = 1f;
            go.GetComponent<ParticleSystemRenderer>().sharedMaterial = instance.additive;
            ps.Play();

            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = instance.additive;
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = 48;
            lr.widthMultiplier = 0.12f;
            lr.startColor = lr.endColor = color * 1.5f;
            for (int i = 0; i < 48; i++)
            {
                float a = i / 48f * Mathf.PI * 2f;
                lr.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, 0.1f, Mathf.Sin(a) * radius));
            }
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = radius * 2f;
            light.intensity = 2f;
            return go;
        }

        static LineRenderer Line(string name, Color color, float width, float life)
        {
            var go = new GameObject(name);
            var lr = go.AddComponent<LineRenderer>();
            lr.sharedMaterial = instance.additive;
            lr.widthMultiplier = width;
            lr.startColor = lr.endColor = color * 2.5f;
            lr.numCapVertices = 4;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.AddComponent<FXFade>().life = life;
            return lr;
        }
    }

    /// <summary>Fades out a line renderer or light, then destroys the object.</summary>
    public class FXFade : MonoBehaviour
    {
        public float life = 0.3f;
        float t;
        LineRenderer lr;
        Light lamp;
        Color c0;
        float i0;

        void Start()
        {
            lr = GetComponent<LineRenderer>();
            lamp = GetComponent<Light>();
            if (lr) c0 = lr.startColor;
            if (lamp) i0 = lamp.intensity;
        }

        void Update()
        {
            t += Time.deltaTime;
            float k = Mathf.Clamp01(1f - t / life);
            if (lr)
            {
                var c = c0;
                c.a *= k;
                lr.startColor = lr.endColor = c;
            }
            if (lamp) lamp.intensity = i0 * k;
            if (t >= life) Destroy(gameObject);
        }
    }

    public class ExpandRing : MonoBehaviour
    {
        public Vector3 center;
        public float radius = 3f;
        public float duration = 0.4f;
        float t;
        LineRenderer lr;

        void Start() => lr = GetComponent<LineRenderer>();

        void Update()
        {
            t += Time.deltaTime;
            float r = Mathf.Lerp(0.2f, radius, Mathf.Sqrt(Mathf.Clamp01(t / duration)));
            for (int i = 0; i < lr.positionCount; i++)
            {
                float a = i / (float)lr.positionCount * Mathf.PI * 2f;
                lr.SetPosition(i, center + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
            }
        }
    }
}
