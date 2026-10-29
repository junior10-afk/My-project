using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Turns the raw Poly Haven downloads into game-ready assets: importer settings, packed
    /// channel maps (URP metallic/smoothness, occlusion, terrain mask maps), PBR materials,
    /// terrain layers and LOD-culled prefabs for scanned models.
    /// </summary>
    public static class ArtLibrary
    {
        public const string PolyHaven = "Assets/_Game/Art/PolyHaven";
        const string PackedDir = "Assets/_Game/Generated/Packed";
        const string MatDir = "Assets/_Game/Materials/PolyHaven";
        const string PrefabDir = "Assets/_Game/Prefabs/PolyHaven";

        public struct Channel
        {
            public Texture2D tex;
            public int index;
            public bool invert;
            public float constant;

            public static Channel From(Texture2D t, int i, bool inv = false) => new() { tex = t, index = i, invert = inv };
            public static Channel Const(float v) => new() { constant = v };
        }

        // ------------------------------------------------------------ Importers

        public static void ConfigureImporters()
        {
            var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { PolyHaven });
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in guids)
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (AssetImporter.GetAtPath(path) is not TextureImporter imp) continue;
                    string n = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
                    bool changed = false;
                    if (n.Contains("_nor_gl"))
                    {
                        if (imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; changed = true; }
                    }
                    else if (n.Contains("_arm") || n.Contains("_rough") || n.Contains("_disp") || n.Contains("_alpha") || n.Contains("_ao"))
                    {
                        if (imp.sRGBTexture) { imp.sRGBTexture = false; changed = true; }
                    }
                    if (imp.maxTextureSize != 2048) { imp.maxTextureSize = 2048; changed = true; }
                    if (changed) imp.SaveAndReimport();
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { PolyHaven }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter mi) continue;
                bool changed = false;
                if (mi.importAnimation) { mi.importAnimation = false; changed = true; }
                if (mi.importCameras || mi.importLights) { mi.importCameras = false; mi.importLights = false; changed = true; }
                if (!mi.isReadable) { mi.isReadable = true; changed = true; } // needed for MeshColliders at runtime
                if (changed) mi.SaveAndReimport();
            }
        }

        // ------------------------------------------------------------ Texture lookup / packing

        public static Texture2D Find(string folder, string token)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (Path.GetFileName(path).ToLowerInvariant().Contains(token.ToLowerInvariant()))
                    return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            }
            return null;
        }

        public static Color[] Read(Texture2D tex, int size, bool srgb)
        {
            var rt = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32,
                srgb ? RenderTextureReadWrite.sRGB : RenderTextureReadWrite.Linear);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tmp = new Texture2D(size, size, TextureFormat.RGBA32, false, !srgb);
            tmp.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tmp.Apply();
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
            var px = tmp.GetPixels();
            Object.DestroyImmediate(tmp);
            return px;
        }

        /// <summary>Packs four source channels into a new PNG (cached by path).</summary>
        public static Texture2D Pack(string name, int size, Channel r, Channel g, Channel b, Channel a, bool srgb = false, bool force = false)
        {
            EnsureFolder(PackedDir);
            string path = $"{PackedDir}/{name}.png";
            if (!force && File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            var channels = new[] { r, g, b, a };
            var cache = new Dictionary<Texture2D, Color[]>();
            var data = new Color[4][];
            for (int c = 0; c < 4; c++)
            {
                var ch = channels[c];
                if (!ch.tex) continue;
                if (!cache.TryGetValue(ch.tex, out var px))
                {
                    // Colour channels of an sRGB packed output keep sRGB encoding; data maps stay linear.
                    bool readSrgb = srgb && c < 3;
                    px = Read(ch.tex, size, readSrgb);
                    cache[ch.tex] = px;
                }
                data[c] = px;
            }

            var outPx = new Color[size * size];
            for (int i = 0; i < outPx.Length; i++)
            {
                var col = new Color();
                for (int c = 0; c < 4; c++)
                {
                    var ch = channels[c];
                    float v = data[c] != null ? data[c][i][ch.index] : ch.constant;
                    if (ch.invert) v = 1f - v;
                    col[c] = v;
                }
                outPx[i] = col;
            }
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true, !srgb);
            tex.SetPixels(outPx);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = srgb;
            imp.alphaIsTransparency = srgb;
            imp.maxTextureSize = 2048;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ------------------------------------------------------------ Materials & terrain layers

        /// <summary>URP Lit material from a Poly Haven texture set (diff / nor_gl / arm).</summary>
        public static Material SurfaceMaterial(string id, float tiling = 1f, Color? tint = null)
        {
            string folder = $"{PolyHaven}/Textures/{id}";
            var diff = Find(folder, "_diff");
            var nor = Find(folder, "_nor_gl");
            var arm = Find(folder, "_arm");

            var mat = LoadOrCreateMaterial($"{MatDir}/{id}.mat", "Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", diff);
            mat.SetColor("_BaseColor", tint ?? Color.white);
            mat.SetTextureScale("_BaseMap", Vector2.one * tiling);
            if (nor)
            {
                mat.SetTexture("_BumpMap", nor);
                mat.SetFloat("_BumpScale", 1f);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (arm)
            {
                var ms = Pack($"{id}_MetalSmooth", 1024, Channel.From(arm, 2), Channel.Const(0), Channel.Const(0), Channel.From(arm, 1, true));
                var ao = Pack($"{id}_AO", 1024, Channel.From(arm, 0), Channel.From(arm, 0), Channel.From(arm, 0), Channel.Const(1));
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f);
                mat.SetTexture("_OcclusionMap", ao);
                mat.SetFloat("_OcclusionStrength", 1f);
                mat.EnableKeyword("_OCCLUSIONMAP");
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static TerrainLayer TerrainLayerFor(string id, float tileSize, string outPath, Color? tint = null)
        {
            string folder = $"{PolyHaven}/Textures/{id}";
            var diff = Find(folder, "_diff");
            var nor = Find(folder, "_nor_gl");
            var arm = Find(folder, "_arm");
            var disp = Find(folder, "_disp");

            var layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(outPath);
            if (!layer)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, outPath);
            }
            layer.diffuseTexture = diff;
            layer.normalMapTexture = nor;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tileSize, tileSize);
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = tint ?? Color.white;
            if (arm)
            {
                layer.maskMapTexture = Pack($"{id}_TerrainMask", 1024,
                    Channel.Const(0), Channel.From(arm, 0),
                    disp ? Channel.From(disp, 0) : Channel.Const(0.5f),
                    Channel.From(arm, 1, true));
            }
            EditorUtility.SetDirty(layer);
            return layer;
        }

        public static Material FoliageMaterial(string name, Texture2D albedoWithAlpha, Texture2D normal, Color tint,
            float cutoff = 0.45f, float windStrength = 0.3f, float flutter = 0.04f, bool vertexColors = true,
            float translucency = 0.8f, float smoothness = 0.2f, bool alphaClip = true, float windHeight = 0.5f)
        {
            var mat = LoadOrCreateMaterial($"{MatDir}/{name}.mat", "LastWorld/Foliage");
            mat.SetTexture("_BaseMap", albedoWithAlpha);
            mat.SetColor("_BaseColor", tint);
            if (normal) mat.SetTexture("_BumpMap", normal);
            mat.SetFloat("_Cutoff", cutoff);
            mat.SetFloat("_WindStrength", windStrength);
            mat.SetFloat("_WindFlutter", flutter);
            mat.SetFloat("_WindHeight", windHeight);
            mat.SetFloat("_UseVertexColor", vertexColors ? 1f : 0f);
            mat.SetFloat("_Translucency", translucency);
            mat.SetFloat("_Smoothness", smoothness);
            mat.SetFloat("_AlphaClip", alphaClip ? 1f : 0f);
            mat.SetFloat("_Cull", alphaClip ? 0f : 2f);
            if (alphaClip) mat.EnableKeyword("_ALPHATEST_ON"); else mat.DisableKeyword("_ALPHATEST_ON");
            mat.renderQueue = alphaClip ? 2450 : 2000;
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------ Model prefabs

        /// <summary>
        /// Builds a prefab from a Poly Haven model with proper URP materials.
        /// foliage = use the wind/translucency shader with alpha; rocks get MeshColliders.
        /// </summary>
        public static GameObject ModelPrefab(string id, bool foliage, float cullScreenHeight, bool colliders,
            float windStrength = 0.15f, Color? tint = null)
        {
            string folder = $"{PolyHaven}/Models/{id}";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>($"{folder}/{id}.fbx");
            if (!model)
            {
                Debug.LogWarning($"[ArtLibrary] Model missing: {id}");
                return null;
            }

            var sets = TextureSets(folder);
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var root = new GameObject(id);
            instance.transform.SetParent(root.transform, false);

            var renderers = instance.GetComponentsInChildren<MeshRenderer>();

            // Poly Haven FBX files ship their own LOD0..LOD3 meshes with a LODGroup on a child.
            // Collect those levels and rebuild a single LODGroup on the prefab root (required by terrain trees).
            var levels = new List<List<Renderer>>();
            var childGroups = instance.GetComponentsInChildren<LODGroup>();
            if (childGroups.Length > 0)
            {
                foreach (var g in childGroups)
                {
                    var lods = g.GetLODs();
                    for (int i = 0; i < lods.Length; i++)
                    {
                        while (levels.Count <= i) levels.Add(new List<Renderer>());
                        levels[i].AddRange(lods[i].renderers.Where(x => x));
                    }
                    Object.DestroyImmediate(g);
                }
            }
            else levels.Add(new List<Renderer>(renderers));
            var lod0 = new HashSet<Renderer>(levels[0]);

            var matCache = new Dictionary<string, Material>();
            Bounds bounds = default;
            bool hasBounds = false;
            foreach (var r in renderers)
            {
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    string slot = mats[i] ? mats[i].name : id;
                    string prefix = MatchSet(slot, sets.Keys, id);
                    string key = prefix ?? id;
                    if (!matCache.TryGetValue(key, out var m))
                    {
                        m = BuildModelMaterial(key, sets.TryGetValue(key, out var s) ? s : null, foliage, windStrength, tint ?? Color.white);
                        matCache[key] = m;
                    }
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;

                if (!lod0.Contains(r)) continue;
                if (colliders)
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf && mf.sharedMesh) r.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
                }
                if (!hasBounds) { bounds = r.bounds; hasBounds = true; }
                else bounds.Encapsulate(r.bounds);
            }

            // Pivot at the base centre so placement on terrain is straightforward.
            if (hasBounds)
                instance.transform.localPosition -= new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);

            // Transitions spread logarithmically from 30% of the screen down to the cull height.
            int n = levels.Count;
            var lodArray = new LOD[n];
            for (int i = 0; i < n; i++)
            {
                float h = n == 1 ? cullScreenHeight : cullScreenHeight * Mathf.Pow(0.3f / cullScreenHeight, (float)(n - 1 - i) / (n - 1));
                lodArray[i] = new LOD(h, levels[i].ToArray());
            }
            var lod = root.AddComponent<LODGroup>();
            lod.SetLODs(lodArray);
            lod.RecalculateBounds();

            EnsureFolder(PrefabDir);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{id}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        public static Vector3 PrefabSize(GameObject prefab)
        {
            var b = new Bounds();
            bool init = false;
            foreach (var r in prefab.GetComponentsInChildren<Renderer>())
            {
                if (!init) { b = r.bounds; init = true; }
                else b.Encapsulate(r.bounds);
            }
            return b.size;
        }

        class TexSet
        {
            public Texture2D diff, nor, rough, alpha;
        }

        static Dictionary<string, TexSet> TextureSets(string folder)
        {
            var sets = new Dictionary<string, TexSet>(StringComparer.OrdinalIgnoreCase);
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { folder }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string n = Path.GetFileNameWithoutExtension(path);
                foreach (var token in new[] { "_diff", "_nor_gl", "_rough", "_alpha" })
                {
                    int idx = n.IndexOf(token, StringComparison.OrdinalIgnoreCase);
                    if (idx < 0) continue;
                    string prefix = n.Substring(0, idx);
                    if (!sets.TryGetValue(prefix, out var s)) sets[prefix] = s = new TexSet();
                    var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                    switch (token)
                    {
                        case "_diff": s.diff = tex; break;
                        case "_nor_gl": s.nor = tex; break;
                        case "_rough": s.rough = tex; break;
                        case "_alpha": s.alpha = tex; break;
                    }
                }
            }
            return sets;
        }

        static string MatchSet(string slot, IEnumerable<string> prefixes, string id)
        {
            var list = prefixes.ToList();
            string s = slot.ToLowerInvariant();
            var exact = list.FirstOrDefault(p => p.ToLowerInvariant() == s);
            if (exact != null) return exact;
            var contained = list.Where(p => s.Contains(p.ToLowerInvariant()) || p.ToLowerInvariant().Contains(s))
                .OrderByDescending(p => p.Length).FirstOrDefault();
            if (contained != null) return contained;
            return list.OrderBy(p => p.Length).FirstOrDefault();
        }

        static Material BuildModelMaterial(string key, TexSet set, bool foliage, float windStrength, Color tint)
        {
            if (set == null) return LoadOrCreateMaterial($"{MatDir}/{key}.mat", "Universal Render Pipeline/Lit");

            if (foliage || set.alpha)
            {
                Texture2D albedo = set.diff;
                if (set.alpha && set.diff)
                    albedo = Pack($"{key}_AlbedoAlpha", 1024, Channel.From(set.diff, 0), Channel.From(set.diff, 1), Channel.From(set.diff, 2),
                        Channel.From(set.alpha, 0), srgb: true);
                return FoliageMaterial(key, albedo, set.nor, tint, 0.4f, windStrength, 0.02f, vertexColors: false,
                    translucency: 0.6f, smoothness: 0.15f, alphaClip: set.alpha != null);
            }

            var mat = LoadOrCreateMaterial($"{MatDir}/{key}.mat", "Universal Render Pipeline/Lit");
            mat.SetTexture("_BaseMap", set.diff);
            mat.SetColor("_BaseColor", tint);
            if (set.nor)
            {
                mat.SetTexture("_BumpMap", set.nor);
                mat.EnableKeyword("_NORMALMAP");
            }
            if (set.rough)
            {
                var ms = Pack($"{key}_MetalSmooth", 1024, Channel.Const(0), Channel.Const(0), Channel.Const(0), Channel.From(set.rough, 0, true));
                mat.SetTexture("_MetallicGlossMap", ms);
                mat.EnableKeyword("_METALLICSPECGLOSSMAP");
                mat.SetFloat("_Smoothness", 1f);
            }
            mat.enableInstancing = true;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        // ------------------------------------------------------------ Utils

        public static Material LoadOrCreateMaterial(string path, string shader)
        {
            EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var sh = Shader.Find(shader);
            if (!m)
            {
                m = new Material(sh);
                AssetDatabase.CreateAsset(m, path);
            }
            else if (m.shader != sh) m.shader = sh;
            return m;
        }

        public static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            string parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
