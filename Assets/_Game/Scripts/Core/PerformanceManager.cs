using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastWorld
{
    /// <summary>
    /// Graphics quality (Low / Medium / High, saved), point lights that only run near the camera (the island holds 200+),
    /// depth of field reserved for cinematics, and an automatic step down when the frame rate stays low.
    /// Settings that live in assets (render scale, shadow distance) are restored when the game stops.
    /// </summary>
    public class PerformanceManager : MonoBehaviour
    {
        public static PerformanceManager Instance { get; private set; }
        public static readonly string[] PresetNames = { "Faible", "Moyenne", "Haute" };
        public static string PresetName => PresetNames[Mathf.Clamp(Preset, 0, 2)];
        public static int Preset { get; private set; } = 1;

        [Tooltip("Average FPS under which the quality steps down one level.")]
        public float lowFps = 36f;

        static readonly float[] RenderScale = { 0.7f, 0.85f, 1f };
        static readonly float[] ShadowDist = { 0f, 70f, 110f };
        static readonly float[] LightRadius = { 28f, 45f, 60f };
        static readonly float[] LodBias = { 1f, 1.2f, 1.4f };
        static readonly float[] GrassDist = { 40f, 70f, 100f };
        static readonly float[] TreeDist = { 600f, 1000f, 1500f };
        static readonly float[] BillboardDist = { 200f, 300f, 450f };

        readonly List<Light> lights = new();
        readonly List<DepthOfField> dofs = new();
        float nextScan, nextCull, windowTime, windowAccum, lightRadius = 45f;
        int windowFrames;
        bool dofOn;
        Transform cam;

        UniversalRenderPipelineAsset urp;
        float origScale, origShadow, origLod;

        void Awake()
        {
            Instance = this;
            Preset = Mathf.Clamp(PlayerPrefs.GetInt("quality", 1), 0, 2);
        }

        void Start()
        {
            urp = UniversalRenderPipeline.asset;
            if (urp)
            {
                origScale = urp.renderScale;
                origShadow = urp.shadowDistance;
            }
            origLod = QualitySettings.lodBias;
            foreach (var v in FindObjectsByType<Volume>())
                if (v.profile && v.profile.TryGet(out DepthOfField dof)) dofs.Add(dof);
            Apply(Preset, false);
        }

        void OnDestroy() => Restore();
        void OnApplicationQuit() => Restore();

        void Restore()
        {
            if (urp)
            {
                urp.renderScale = origScale;
                urp.shadowDistance = origShadow;
            }
            if (origLod > 0f) QualitySettings.lodBias = origLod;
        }

        public static void Cycle()
        {
            if (Instance) Instance.Apply((Preset + 1) % 3, true);
        }

        public void Apply(int preset, bool save)
        {
            Preset = Mathf.Clamp(preset, 0, 2);
            if (save) PlayerPrefs.SetInt("quality", Preset);
            if (urp)
            {
                urp.renderScale = RenderScale[Preset];
                if (ShadowDist[Preset] > 0f) urp.shadowDistance = ShadowDist[Preset];
            }
            var data = Camera.main ? Camera.main.GetUniversalAdditionalCameraData() : null;
            if (data != null)
            {
                data.renderShadows = Preset > 0;
                data.antialiasing = Preset == 2 ? AntialiasingMode.TemporalAntiAliasing
                    : Preset == 1 ? AntialiasingMode.SubpixelMorphologicalAntiAliasing : AntialiasingMode.FastApproximateAntialiasing;
            }
            QualitySettings.lodBias = LodBias[Preset];
            lightRadius = LightRadius[Preset];
            var t = Terrain.activeTerrain;
            if (t)
            {
                t.detailObjectDistance = GrassDist[Preset];
                t.treeDistance = TreeDist[Preset];
                t.treeBillboardDistance = BillboardDist[Preset];
            }
            nextCull = 0f;
            dofOn = !dofOn; // force the next frame to refresh depth of field
        }

        void Update()
        {
            if (!cam && Camera.main) cam = Camera.main.transform;
            if (!cam) return;

            // Depth of field only during cinematics (and not on Low): it costs ~3 ms for nothing in play
            bool wantDof = Preset > 0 && CinematicDirector.Playing;
            if (wantDof != dofOn)
            {
                dofOn = wantDof;
                foreach (var d in dofs) d.active = wantDof;
            }

            if (Time.unscaledTime >= nextScan)
            {
                nextScan = Time.unscaledTime + 4f;
                lights.Clear();
                foreach (var l in FindObjectsByType<Light>())
                    if (l.type == LightType.Point && l.shadows == LightShadows.None) lights.Add(l);
            }

            if (Time.unscaledTime >= nextCull)
            {
                nextCull = Time.unscaledTime + 0.35f;
                Vector3 p = cam.position;
                for (int i = lights.Count - 1; i >= 0; i--)
                {
                    var l = lights[i];
                    if (!l) { lights.RemoveAt(i); continue; }
                    float reach = lightRadius + l.range * 0.5f;
                    l.enabled = (l.transform.position - p).sqrMagnitude < reach * reach;
                }
            }

            TrackFps();
        }

        void TrackFps()
        {
            // Cinematics, pauses and the seconds after a load are not representative
            if (Time.timeScale == 0f || CinematicDirector.Playing || GameState.UIOpen || Time.unscaledTime < 10f)
            {
                windowTime = windowAccum = 0f;
                windowFrames = 0;
                return;
            }
            windowTime += Time.unscaledDeltaTime;
            windowAccum += Time.unscaledDeltaTime;
            windowFrames++;
            if (windowTime < 5f) return;
            float fps = windowFrames / windowAccum;
            windowTime = windowAccum = 0f;
            windowFrames = 0;
            if (fps < lowFps && Preset > 0)
            {
                Apply(Preset - 1, true);
                GameUI.Toast($"Fluidité faible : qualité graphique réduite ({PresetName}). Réglable dans les paramètres.");
                Debug.Log($"[PerformanceManager] {fps:0} fps: quality set to {PresetName}");
            }
        }
    }
}
