using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// High-end URP configuration: 4K cascaded soft shadows, HDR colour grading, depth/opaque textures
    /// (needed by the water), Forward+, SSAO and a cinematic post-processing profile.
    /// </summary>
    public static class RenderSetup
    {
        const string ProfilePath = "Assets/_Game/Settings/WorldVolume.asset";

        public static VolumeProfile Configure()
        {
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
                ConfigureAsset(urp);
            else
                Debug.LogWarning("[RenderSetup] No URP asset active.");
            return BuildProfile();
        }

        static void ConfigureAsset(UniversalRenderPipelineAsset asset)
        {
            var so = new SerializedObject(asset);
            Set(so, "m_RequireDepthTexture", true);
            Set(so, "m_RequireOpaqueTexture", true);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_MSAA", 1);
            Set(so, "m_RenderScale", 1f);
            Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_ShadowDistance", 320f);
            Set(so, "m_ShadowCascadeCount", 4);
            Set(so, "m_Cascade4Split", new Vector3(0.035f, 0.11f, 0.33f));
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", 3);
            Set(so, "m_AdditionalLightShadowsSupported", true);
            Set(so, "m_AdditionalLightsPerObjectLimit", 8);
            Set(so, "m_ColorGradingMode", 1);
            Set(so, "m_ColorGradingLutSize", 64);
            so.ApplyModifiedPropertiesWithoutUndo();

            var list = so.FindProperty("m_RendererDataList");
            if (list != null && list.arraySize > 0 && list.GetArrayElementAtIndex(0).objectReferenceValue is ScriptableRendererData data)
            {
                var rso = new SerializedObject(data);
                Set(rso, "m_RenderingMode", 2); // Forward+
                rso.ApplyModifiedPropertiesWithoutUndo();
                AddSsao(data);
                EditorUtility.SetDirty(data);
            }
            EditorUtility.SetDirty(asset);
        }

        static void AddSsao(ScriptableRendererData data)
        {
            var type = Type.GetType("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion, Unity.RenderPipelines.Universal.Runtime");
            if (type == null)
            {
                Debug.LogWarning("[RenderSetup] SSAO feature type not found.");
                return;
            }
            foreach (var f in data.rendererFeatures)
                if (f && f.GetType() == type) return;

            var feature = (ScriptableRendererFeature)ScriptableObject.CreateInstance(type);
            feature.name = "SSAO";
            AssetDatabase.AddObjectToAsset(feature, data);

            var fso = new SerializedObject(feature);
            Set(fso, "m_Settings.Intensity", 1.4f);
            Set(fso, "m_Settings.Radius", 0.45f);
            Set(fso, "m_Settings.Source", 1); // depth normals
            Set(fso, "m_Settings.Downsample", false);
            fso.ApplyModifiedPropertiesWithoutUndo();

            var so = new SerializedObject(data);
            var features = so.FindProperty("m_RendererFeatures");
            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            var map = so.FindProperty("m_RendererFeatureMap");
            if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out _, out long localId))
            {
                map.arraySize++;
                map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            }
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        static VolumeProfile BuildProfile()
        {
            ArtLibrary.EnsureFolder("Assets/_Game/Settings");
            if (AssetDatabase.LoadMainAssetAtPath(ProfilePath)) AssetDatabase.DeleteAsset(ProfilePath);
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(p, ProfilePath);

            Add<Tonemapping>(p, t => t.mode.Override(TonemappingMode.ACES));
            Add<Bloom>(p, b =>
            {
                b.intensity.Override(0.55f);
                b.threshold.Override(1.05f);
                b.scatter.Override(0.72f);
                b.tint.Override(new Color(1f, 0.95f, 0.88f));
                b.highQualityFiltering.Override(true);
            });
            Add<ColorAdjustments>(p, c =>
            {
                c.postExposure.Override(0.45f);
                c.contrast.Override(14f);
                c.saturation.Override(12f);
            });
            Add<WhiteBalance>(p, w => w.temperature.Override(4f));
            Add<ShadowsMidtonesHighlights>(p, s =>
            {
                s.shadows.Override(new Vector4(0.94f, 0.98f, 1.1f, 0f));
                s.highlights.Override(new Vector4(1.06f, 1.02f, 0.94f, 0f));
            });
            Add<Vignette>(p, v =>
            {
                v.intensity.Override(0.2f);
                v.smoothness.Override(0.45f);
            });
            Add<DepthOfField>(p, d =>
            {
                d.mode.Override(DepthOfFieldMode.Gaussian);
                d.gaussianStart.Override(350f);
                d.gaussianEnd.Override(1800f);
                d.gaussianMaxRadius.Override(0.7f);
                d.highQualitySampling.Override(true);
            });
            AssetDatabase.SaveAssets();
            return p;
        }

        static void Add<T>(VolumeProfile profile, Action<T> init) where T : VolumeComponent
        {
            var c = profile.Add<T>(false);
            c.name = typeof(T).Name;
            init(c);
            AssetDatabase.AddObjectToAsset(c, profile);
        }

        static void Set(SerializedObject so, string name, object value)
        {
            var p = so.FindProperty(name);
            if (p == null)
            {
                Debug.Log($"[RenderSetup] Property not found (skipped): {name}");
                return;
            }
            switch (value)
            {
                case bool b: p.boolValue = b; break;
                case int i: p.intValue = i; break; // for enums this is the underlying value, not the index
                case float f: p.floatValue = f; break;
                case Vector3 v: p.vector3Value = v; break;
            }
        }
    }
}
