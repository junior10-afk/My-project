using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Imports the MPFB (MakeHuman) characters exported from Blender: Humanoid avatar,
    /// extracted URP materials (alpha-clipped double-sided hair/lashes/brows, skin/eye tuning)
    /// and a ready prefab per character.
    /// </summary>
    public static class CharacterImporter
    {
        public const string CharDir = "Assets/_Game/Art/Characters";
        public const string PrefabDir = "Assets/_Game/Prefabs/Characters";

        public static readonly string[] Names = { "Hero", "Kaira", "Nerys", "Torvan", "Elara", "Bram" };

        [MenuItem("Last World/Import Characters")]
        public static void ImportAll()
        {
            foreach (var n in Names) Import(n);
            AssetDatabase.SaveAssets();
        }

        public static GameObject Import(string name)
        {
            string fbx = $"{CharDir}/{name}/{name}.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (!importer)
            {
                Debug.LogWarning($"[CharacterImporter] Missing {fbx}");
                return null;
            }

            importer.animationType = ModelImporterAnimationType.Human;
            importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
            importer.importBlendShapes = false;
            importer.importAnimation = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();

            // Embedded textures are only auto-extracted on the first import: extract again so garments
            // added later in Blender bring their textures along.
            string texDir = $"{CharDir}/{name}/{name}.fbm";
            ArtLibrary.EnsureFolder(texDir);
            if (importer.ExtractTextures(texDir)) AssetDatabase.Refresh();

            // Extract embedded materials so they can be edited. Remapped materials are hidden from
            // LoadAllAssetsAtPath, so drop the remaps first to see the FBX's current garment list.
            string matDir = $"{CharDir}/{name}/Materials";
            ArtLibrary.EnsureFolder(matDir);
            foreach (var id in importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).ToArray())
                importer.RemoveRemap(id);
            importer.SaveAndReimport();
            var embedded = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().ToArray();
            var fbxMats = embedded.Select(m => m.name).ToHashSet();
            foreach (var g in AssetDatabase.FindAssets("t:Material", new[] { matDir }))
            {
                string p = AssetDatabase.GUIDToAssetPath(g);
                if (!fbxMats.Contains(Path.GetFileNameWithoutExtension(p))) AssetDatabase.DeleteAsset(p); // garment removed in Blender
            }
            foreach (var mat in embedded)
            {
                string target = $"{matDir}/{mat.name}.mat";
                if (!File.Exists(target))
                {
                    var err = AssetDatabase.ExtractAsset(mat, target);
                    if (!string.IsNullOrEmpty(err)) Debug.LogWarning(err);
                }
                else importer.AddRemap(new AssetImporter.SourceAssetIdentifier(mat), AssetDatabase.LoadAssetAtPath<Material>(target));
            }
            AssetDatabase.WriteImportSettingsIfDirty(fbx);
            AssetDatabase.ImportAsset(fbx, ImportAssetOptions.ForceUpdate);

            var mats = AssetDatabase.FindAssets("t:Material", new[] { matDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            AssignTextures(mats, texDir);
            foreach (var m in mats) FixMaterial(m, texDir);
            ApplyOutfitColors(name, mats);

            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
            var avatar = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Avatar>().FirstOrDefault();
            Debug.Log($"[CharacterImporter] {name}: avatar {(avatar && avatar.isValid && avatar.isHuman ? "OK (humanoid)" : "INVALID")}");

            ArtLibrary.EnsureFolder(PrefabDir);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(model);
            PrefabUtility.UnpackPrefabInstance(inst, PrefabUnpackMode.OutermostRoot, InteractionMode.AutomatedAction);
            inst.name = name;
            var animator = inst.GetComponent<Animator>();
            if (!animator) animator = inst.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                smr.updateWhenOffscreen = true;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            }
            var prefab = PrefabUtility.SaveAsPrefabAsset(inst, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(inst);
            return prefab;
        }

        /// <summary>
        /// MakeHuman texture names rarely match material names (e.g. "BootsViking.png" for "..._viking_boots"),
        /// so match by keywords: skin for the body, *_eye for the eyes, shared name tokens for clothes and hair,
        /// and *normal* textures as normal maps.
        /// </summary>
        static void AssignTextures(List<Material> mats, string texDir)
        {
            var textures = AssetDatabase.FindAssets("t:Texture2D", new[] { texDir })
                .Select(g => AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(g))).ToList();
            var diffuse = textures.Where(t => !t.name.ToLowerInvariant().Contains("normal")).ToList();
            var normals = textures.Where(t => t.name.ToLowerInvariant().Contains("normal")).ToList();
            var used = new HashSet<Texture2D>();
            foreach (var m in mats)
                if (m.GetTexture("_BaseMap") is Texture2D t) used.Add(t);

            static string[] Tokens(string s) => s.ToLowerInvariant().Split('.', '_', '-', ' ').Where(x => x.Length > 3 && x != "body").ToArray();

            Texture2D Best(IEnumerable<Texture2D> pool, Material m)
            {
                string mn = m.name.ToLowerInvariant();
                string key = mn.Contains('.') ? mn.Substring(mn.IndexOf('.') + 1) : mn;
                if (key == "body") return pool.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("skin"))
                                          ?? pool.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("male") && !t.name.ToLowerInvariant().Contains("suit"));
                if (key.Contains("low-poly") || key.Contains("high-poly")) return pool.FirstOrDefault(t => t.name.ToLowerInvariant().Contains("eye"));
                var tokens = Tokens(key);
                Texture2D best = null;
                int bestScore = 0;
                foreach (var t in pool)
                {
                    string tn = t.name.ToLowerInvariant();
                    int score = tokens.Count(tok => tn.Contains(tok));
                    if (score > bestScore) { bestScore = score; best = t; }
                }
                return best;
            }

            foreach (var m in mats)
            {
                if (m.GetTexture("_BaseMap") == null)
                {
                    var t = Best(diffuse.Where(x => !used.Contains(x)), m);
                    if (t)
                    {
                        m.SetTexture("_BaseMap", t);
                        used.Add(t);
                    }
                }
                var n = Best(normals, m);
                if (n)
                {
                    if (AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(n)) is TextureImporter ti && ti.textureType != TextureImporterType.NormalMap)
                    {
                        ti.textureType = TextureImporterType.NormalMap;
                        ti.SaveAndReimport();
                    }
                    m.SetTexture("_BumpMap", n);
                    m.EnableKeyword("_NORMALMAP");
                }
            }
            // Any remaining untextured material takes a leftover diffuse texture (one-to-one)
            var leftoverMats = mats.Where(m => m.GetTexture("_BaseMap") == null).ToList();
            var leftoverTex = diffuse.Where(t => !used.Contains(t)).ToList();
            if (leftoverMats.Count == 1 && leftoverTex.Count == 1) leftoverMats[0].SetTexture("_BaseMap", leftoverTex[0]);
            foreach (var m in mats) EditorUtility.SetDirty(m);
        }

        static void FixMaterial(Material m, string texDir)
        {
            if (!m) return;
            string n = m.name.ToLowerInvariant();
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            Texture baseTex = m.HasProperty("_BaseMap") ? m.GetTexture("_BaseMap") : m.mainTexture;
            if (m.shader != lit)
            {
                m.shader = lit;
            }
            if (!baseTex)
            {
                // Fall back to a texture in the .fbm folder whose name shares a token with the material
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { texDir }))
                {
                    string p = AssetDatabase.GUIDToAssetPath(guid);
                    string tn = Path.GetFileNameWithoutExtension(p).ToLowerInvariant();
                    string key = n.Split('.').Last().Split('_').First();
                    if (key.Length > 2 && tn.Contains(key)) { baseTex = AssetDatabase.LoadAssetAtPath<Texture2D>(p); break; }
                }
            }
            if (baseTex) m.SetTexture("_BaseMap", baseTex);
            m.SetColor("_BaseColor", Color.white);
            m.SetFloat("_Metallic", 0f);

            bool alpha = n.Contains("hair") || n.Contains("eyebrow") || n.Contains("eyelash") || n.Contains("short0") || n.Contains("long0")
                         || n.Contains("ponytail") || n.Contains("braid") || n.Contains("bob0");
            if (alpha)
            {
                m.SetFloat("_AlphaClip", 1f);
                m.SetFloat("_Cutoff", n.Contains("eyelash") ? 0.3f : 0.45f);
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetFloat("_Cull", 0f);
                m.doubleSidedGI = true;
                m.SetFloat("_Smoothness", 0.45f);
                m.renderQueue = 2450;
                if (baseTex is Texture2D t) SetAlphaImport(t);
            }
            else if (n.Contains("eye") || n.Contains("low-poly"))
            {
                m.SetFloat("_Smoothness", 0.92f);
            }
            else if (n.Contains("skin") || n.Contains("body") || n.Contains("teeth") || n.Contains("tongue"))
            {
                m.SetFloat("_Smoothness", 0.42f);
            }
            else
            {
                m.SetFloat("_Smoothness", 0.25f);
                // Cloth with alpha (e.g. robes with cut-outs) still needs clipping to avoid black borders
                if (baseTex is Texture2D t && t.format == TextureFormat.DXT5 || n.Contains("robe"))
                {
                    m.SetFloat("_AlphaClip", 1f);
                    m.SetFloat("_Cutoff", 0.3f);
                    m.EnableKeyword("_ALPHATEST_ON");
                    m.SetFloat("_Cull", 0f);
                }
            }
            EditorUtility.SetDirty(m);
        }

        /// <summary>
        /// Signature outfit colour per character and garment keyword (see STORY.md): several characters
        /// share the same MakeHuman garments, so each gets its own recoloured texture.
        /// </summary>
        static readonly Dictionary<string, (string garment, Color color)[]> OutfitColors = new()
        {
            ["Hero"] = new[] { ("tunic", new Color(0.24f, 0.46f, 0.42f)), ("pants", new Color(0.3f, 0.26f, 0.2f)) },   // wind teal
            ["Kaira"] = new[] { ("tunic", new Color(0.55f, 0.13f, 0.09f)) },                                           // ember red
            ["Nerys"] = new[] { ("robe", new Color(0.17f, 0.42f, 0.5f)) },                                             // tide blue
            ["Torvan"] = new[] { ("tunic", new Color(0.3f, 0.27f, 0.42f)), ("pants", new Color(0.2f, 0.19f, 0.23f)) }, // storm violet
            ["Elara"] = new[] { ("robe", new Color(0.86f, 0.81f, 0.68f)) },                                            // altar cream
            ["Bram"] = new[] { ("tunic", new Color(0.5f, 0.34f, 0.18f)), ("pants", new Color(0.3f, 0.22f, 0.15f)) },  // merchant ochre
        };

        static void ApplyOutfitColors(string character, List<Material> mats)
        {
            if (!OutfitColors.TryGetValue(character, out var garments)) return;
            foreach (var m in mats)
            {
                string n = m.name.ToLowerInvariant();
                foreach (var (garment, color) in garments)
                {
                    if (!n.Contains(garment) || m.GetTexture("_BaseMap") is not Texture2D src) continue;
                    if (src.name.EndsWith("_" + character)) break; // already recoloured
                    m.SetTexture("_BaseMap", Recolor(src, color, $"{CharDir}/{character}/Recolor", character));
                    EditorUtility.SetDirty(m);
                    break;
                }
            }
        }

        /// <summary>Luminance-preserving recolour: the texture's average brightness maps to <paramref name="color"/>.</summary>
        static Texture2D Recolor(Texture2D src, Color color, string dir, string suffix)
        {
            ArtLibrary.EnsureFolder(dir);
            string path = $"{dir}/{src.name}_{suffix}.png";
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(AssetDatabase.GetAssetPath(src)));
            var px = tex.GetPixels();
            double sum = 0;
            foreach (var p in px) sum += p.grayscale;
            float avg = Mathf.Max(0.02f, (float)(sum / px.Length));
            for (int i = 0; i < px.Length; i++)
            {
                float k = Mathf.Clamp(px[i].grayscale / avg, 0f, 2.2f);
                var c = color * k;
                c.a = px[i].a;
                px[i] = c;
            }
            tex.SetPixels(px);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void SetAlphaImport(Texture2D t)
        {
            string p = AssetDatabase.GetAssetPath(t);
            if (AssetImporter.GetAtPath(p) is TextureImporter ti && !ti.alphaIsTransparency)
            {
                ti.alphaIsTransparency = true;
                ti.SaveAndReimport();
            }
        }
    }
}
