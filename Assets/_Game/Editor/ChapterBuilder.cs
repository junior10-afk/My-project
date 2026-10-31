using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Builds the world of Chapters II to X on top of the Chapter I island: reshapes the terrain (forge volcano, lagoon,
    /// citadel plateau, Seal Tower…), paints and dresses each region, and places its NPCs, camps, bosses, chests, markers,
    /// waypoints and story props. Called by ContentBuilder.Build(); ids match <c>ChapterQuests</c>.
    /// </summary>
    public static partial class ChapterBuilder
    {
        static Terrain terrain;
        static TerrainData data;
        static Transform root;
        static System.Random rng;
        static GameObject template; // a waypoint to clone

        /// <summary>Key positions shared with the cinematics (site name → ground position).</summary>
        public static readonly Dictionary<string, Vector3> Sites = new();

        // terrain layers: Grass, ForestFloor, Cliff, MountainRock, Sand, Snow, Dirt
        const int Grass = 0, Forest = 1, Cliff = 2, Rock = 3, Sand = 4, Snow = 5, Dirt = 6;
        const float SeaLevel = 0.08f * 420f;

        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        static Vector2 XZ(Vector3 v) => new(v.x, v.z);

        // Prefabs
        static GameObject[] slime = new GameObject[8];
        static GameObject golemBrasier, golemMarees, golemOrage, golemPale, slimePale;

        public static void Build(Terrain t, Transform content, Vector3 spawn)
        {
            terrain = t;
            data = t.terrainData;
            rng = new System.Random(4242);
            Sites.Clear();
            root = new GameObject("Chapitres").transform;
            root.SetParent(content, false);
            var wps = GameObject.Find("Points de téléportation");
            template = wps && wps.transform.childCount > 0 ? wps.transform.GetChild(0).gameObject : null;

            foreach (var e in new[] { Element.Anemo, Element.Pyro, Element.Hydro, Element.Electro, Element.Cryo })
                slime[(int)e] = AssetDatabase.LoadAssetAtPath<GameObject>($"{ContentBuilder.PrefabDir}/Slime_{e}.prefab");
            slimePale = ContentBuilder.SlimePrefab(Element.Physical);
            golemBrasier = ContentBuilder.GolemPrefab(false, "Golem_Braise", "Gardien de braise", Element.Pyro, new Color(1f, 0.4f, 0.08f), new Color(0.32f, 0.26f, 0.24f));
            golemMarees = ContentBuilder.GolemPrefab(false, "Golem_Marees", "Gardien des marées", Element.Hydro, new Color(0.3f, 0.7f, 1f), new Color(0.6f, 0.75f, 0.9f));
            golemOrage = ContentBuilder.GolemPrefab(false, "Golem_Orage", "Gardien d'orage", Element.Electro, new Color(0.75f, 0.5f, 1f), new Color(0.55f, 0.5f, 0.72f));
            golemPale = ContentBuilder.GolemPrefab(false, "Golem_Pale", "Écho de pierre", Element.Cryo, new Color(1.2f, 1.2f, 1.4f), new Color(0.92f, 0.94f, 1f));

            BuildCendrelune();
            BuildYsmere();
            BuildValdrak();
            BuildTerresPales();
            BuildTour();
            BuildSanctuary();
            BuildSiege(spawn);
            BuildCompanionsAndGates(spawn);
            var anchors = Group("Sites cinématiques");
            foreach (var site in Sites)
            {
                var anchor = Group(site.Key, anchors);
                anchor.position = site.Value;
            }

            terrain.Flush();
            var tc = terrain.GetComponent<TerrainCollider>();
            if (tc) { tc.enabled = false; tc.enabled = true; }
            EditorUtility.SetDirty(data);
            PlayerSettings.bundleVersion = "1.0.0";
            Debug.Log($"[ChapterBuilder] Chapters II–X built ({Sites.Count} sites).");
        }

        /// <summary>Restores cinematic locations after a domain reload, without rebuilding the world.</summary>
        public static bool RestoreSites(Terrain world)
        {
            var chapters = GameObject.Find("Gameplay/Chapitres");
            if (!chapters || !world) return false;
            var anchors = chapters.transform.Find("Sites cinématiques");
            if (anchors)
            {
                Sites.Clear();
                foreach (Transform site in anchors) Sites[site.name] = site.position;
            }
            else
            {
                // Scenes created before site anchors existed: recover from the persistent quest markers.
                var marks = new Dictionary<string, Vector3>();
                foreach (var marker in chapters.GetComponentsInChildren<QuestMarkerAnchor>(true))
                    marks[marker.id] = marker.transform.position;
                string[] required = { "cendrelune", "forge", "citadel", "refuge", "sceau_summit", "SanctuaryMark", "ysmere" };
                if (required.Any(id => !marks.ContainsKey(id))) return false;
                Vector3 Ground(float x, float z) => new(x, world.SampleHeight(new Vector3(x, 0f, z)) + world.transform.position.y, z);
                Sites.Clear();
                Sites["cendrelune"] = marks["cendrelune"];
                Sites["forge_rim"] = marks["forge"];
                Sites["forge"] = Ground(marks["forge"].x, marks["forge"].z + 34f);
                Sites["archives"] = Ground(marks["ysmere"].x - 112f, marks["ysmere"].z);
                Sites["citadel"] = marks["citadel"];
                Sites["valdrak"] = Ground(marks["citadel"].x + 40f, marks["citadel"].z);
                Sites["refuge"] = marks["refuge"];
                Sites["sceau_summit"] = marks["sceau_summit"];
                Sites["sanctuaire_mark"] = marks["SanctuaryMark"];
                Sites["sanctuaire"] = marks["SanctuaryMark"] + Vector3.forward * 22f;
                var altar = GameObject.Find("Autel des Vents");
                if (!altar) return false;
                Sites["camp"] = altar.transform.position;
                // Persist recovered positions so subsequent editor sessions have stable anchors.
                var saved = new GameObject("Sites cinématiques").transform;
                saved.SetParent(chapters.transform, false);
                foreach (var site in Sites)
                {
                    var anchor = new GameObject(site.Key).transform;
                    anchor.SetParent(saved, false);
                    anchor.position = site.Value;
                }
            }
            string[] cinematicSites = { "forge", "cendrelune", "archives", "citadel", "valdrak", "refuge", "sceau_summit", "sanctuaire", "sanctuaire_mark", "camp" };
            return cinematicSites.All(Sites.ContainsKey);
        }

        // ================================================================ Terrain shaping

        static float Smooth(float a, float b, float v) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v));

        /// <summary>Rewrites the heights inside a disc: f(distance, oldWorldHeight) → new world height.</summary>
        static void Modify(Vector3 c, float radius, Func<float, float, float> f)
        {
            int res = data.heightmapResolution;
            var size = data.size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - radius) / size.x * (res - 1)));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.x + radius) / size.x * (res - 1)));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.z - radius) / size.z * (res - 1)));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.z + radius) / size.z * (res - 1)));
            var h = data.GetHeights(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            for (int zi = 0; zi < h.GetLength(0); zi++)
                for (int xi = 0; xi < h.GetLength(1); xi++)
                {
                    float wx = (x0 + xi) / (float)(res - 1) * size.x, wz = (z0 + zi) / (float)(res - 1) * size.z;
                    float d = Vector2.Distance(new Vector2(wx, wz), XZ(c));
                    if (d > radius) continue;
                    h[zi, xi] = Mathf.Clamp01(f(d, h[zi, xi] * size.y) / size.y);
                }
            data.SetHeights(x0, z0, h);
        }

        static float HeightAt(float x, float z) => terrain.SampleHeight(new Vector3(x, 0f, z)) + terrain.transform.position.y;

        /// <summary>Levels a disc to <paramref name="height"/>, blending into the surroundings between inner and outer radius.</summary>
        static void Flatten(Vector3 c, float inner, float outer, float height) =>
            Modify(c, outer, (d, old) => Mathf.Lerp(old, height, 1f - Smooth(inner, outer, d)));

        /// <summary>Raises or lowers a straight band (a ramp road) from one ground height to another.</summary>
        static void Road(Vector3 a, Vector3 b, float halfWidth, float ha, float hb)
        {
            float len = Vector2.Distance(XZ(a), XZ(b));
            Vector2 dir = (XZ(b) - XZ(a)) / len;
            int res = data.heightmapResolution;
            var size = data.size;
            float step = size.x / (res - 1);
            for (float s = 0f; s <= len; s += step)
            {
                Vector2 p = XZ(a) + dir * s;
                float target = Mathf.Lerp(ha, hb, s / len);
                Modify(new Vector3(p.x, 0f, p.y), halfWidth * 2f, (d, old) => Mathf.Lerp(old, target, 1f - Smooth(halfWidth, halfWidth * 2f, d)));
            }
        }

        /// <summary>Paints a terrain layer inside a disc (weight fades toward the edge).</summary>
        static void Paint(Vector3 c, float radius, int layer, float strength = 1f)
        {
            int res = data.alphamapResolution;
            var size = data.size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - radius) / size.x * res));
            int x1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.x + radius) / size.x * res));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.z - radius) / size.z * res));
            int z1 = Mathf.Min(res - 1, Mathf.CeilToInt((c.z + radius) / size.z * res));
            var maps = data.GetAlphamaps(x0, z0, x1 - x0 + 1, z1 - z0 + 1);
            int layers = maps.GetLength(2);
            for (int zi = 0; zi < maps.GetLength(0); zi++)
                for (int xi = 0; xi < maps.GetLength(1); xi++)
                {
                    float wx = (x0 + xi + 0.5f) / res * size.x, wz = (z0 + zi + 0.5f) / res * size.z;
                    float d = Vector2.Distance(new Vector2(wx, wz), XZ(c));
                    if (d > radius) continue;
                    float w = strength * (1f - Smooth(radius * 0.55f, radius, d));
                    for (int l = 0; l < layers; l++) maps[zi, xi, l] = Mathf.Lerp(maps[zi, xi, l], l == layer ? 1f : 0f, w);
                }
            data.SetAlphamaps(x0, z0, maps);
        }

        /// <summary>Removes grass details and trees around a point.</summary>
        static void Clear(Vector3 c, float radius, float treeRadius = -1f)
        {
            int dres = data.detailResolution;
            var size = data.size;
            int x0 = Mathf.Max(0, Mathf.FloorToInt((c.x - radius) / size.x * dres));
            int x1 = Mathf.Min(dres - 1, Mathf.CeilToInt((c.x + radius) / size.x * dres));
            int z0 = Mathf.Max(0, Mathf.FloorToInt((c.z - radius) / size.z * dres));
            int z1 = Mathf.Min(dres - 1, Mathf.CeilToInt((c.z + radius) / size.z * dres));
            for (int layer = 0; layer < data.detailPrototypes.Length; layer++)
            {
                var map = data.GetDetailLayer(x0, z0, x1 - x0 + 1, z1 - z0 + 1, layer);
                for (int zi = 0; zi < map.GetLength(0); zi++)
                    for (int xi = 0; xi < map.GetLength(1); xi++)
                    {
                        float wx = (x0 + xi + 0.5f) / dres * size.x, wz = (z0 + zi + 0.5f) / dres * size.z;
                        if (Vector2.Distance(new Vector2(wx, wz), XZ(c)) <= radius) map[zi, xi] = 0;
                    }
                data.SetDetailLayer(x0, z0, layer, map);
            }
            CinematicBuilder.ClearTrees(c, treeRadius < 0f ? radius : treeRadius, radius);
        }

        // ================================================================ Dressing helpers

        static Vector3 G(float x, float z)
        {
            var p = new Vector3(x, 0f, z);
            p.y = HeightAt(x, z);
            return p;
        }

        static Transform Group(string name, Transform parent = null)
        {
            var g = new GameObject(name).transform;
            g.SetParent(parent ? parent : root, false);
            return g;
        }

        static Material stoneMat, mossMat, plankMat;

        /// <summary>
        /// Dressed stone: the medieval blocks texture desaturated and brightened (the original reads as red brick under
        /// Aerendal's sun), then tinted per use (charred, pale, bluish…).
        /// </summary>
        static Material Stone(Color? tint = null)
        {
            if (!stoneMat)
            {
                var src = ArtLibrary.SurfaceMaterial("medieval_blocks_02");
                string path = $"{ContentBuilder.MatDir}/PaleStone.mat";
                stoneMat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (!stoneMat)
                {
                    stoneMat = new Material(src);
                    AssetDatabase.CreateAsset(stoneMat, path);
                }
                stoneMat.CopyPropertiesFromMaterial(src);
                stoneMat.SetTexture("_BaseMap", PaleTexture(src.GetTexture("_BaseMap") as Texture2D));
                stoneMat.SetColor("_BaseColor", Color.white);
                EditorUtility.SetDirty(stoneMat);
            }
            return tint.HasValue ? ContentBuilder.Tinted(stoneMat, tint.Value) : stoneMat;
        }

        static Texture2D PaleTexture(Texture2D src)
        {
            string path = $"{ContentBuilder.MatDir}/PaleStone_diff.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing || !src) return existing ? existing : src;
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(System.IO.File.ReadAllBytes(AssetDatabase.GetAssetPath(src)));
            var px = tex.GetPixels32();
            for (int i = 0; i < px.Length; i++)
            {
                float l = (0.3f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b) / 255f;
                byte v = (byte)(Mathf.Clamp01(0.42f + l * 0.8f) * 255f);
                px[i] = new Color32(v, (byte)(v * 0.99f), (byte)Mathf.Min(255f, v * 1.02f), 255);
            }
            tex.SetPixels32(px);
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Planks(Color? tint = null)
        {
            plankMat ??= ArtLibrary.SurfaceMaterial("weathered_planks");
            return tint.HasValue ? ContentBuilder.Tinted(plankMat, tint.Value) : plankMat;
        }

        static Material Glow(string name, Color color, float intensity = 3f) => ContentBuilder.Mat(name, color, 0.4f, 0f, color * intensity);

        /// <summary>A box whose UVs follow its real size (one texture repeat every <paramref name="tile"/> metres).</summary>
        static GameObject Block(Transform parent, Vector3 center, Vector3 size, float yaw, Material mat, float pitch = 0f, float tile = 3f, bool collider = true)
        {
            var go = new GameObject("Bloc");
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(center, Quaternion.Euler(pitch, yaw, 0f));
            go.AddComponent<MeshFilter>().sharedMesh = CubeMesh(size, tile);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic); // merged into a few draw calls
            if (collider)
            {
                var bc = go.AddComponent<BoxCollider>();
                bc.size = size;
            }
            return go;
        }

        static Mesh CubeMesh(Vector3 size, float tile)
        {
            Vector3 h = size * 0.5f;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            void Face(Vector3 origin, Vector3 u, Vector3 w, Vector3 normalUnused)
            {
                int i = v.Count;
                v.Add(origin); v.Add(origin + u); v.Add(origin + u + w); v.Add(origin + w);
                float su = u.magnitude / tile, sw = w.magnitude / tile;
                uv.Add(new Vector2(0, 0)); uv.Add(new Vector2(su, 0)); uv.Add(new Vector2(su, sw)); uv.Add(new Vector2(0, sw));
                tri.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
            Face(new Vector3(-h.x, -h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.back);   // back  (-z)
            Face(new Vector3(h.x, -h.y, h.z), new Vector3(-size.x, 0, 0), new Vector3(0, size.y, 0), Vector3.forward); // front (+z)
            Face(new Vector3(-h.x, -h.y, h.z), new Vector3(0, 0, -size.z), new Vector3(0, size.y, 0), Vector3.left);   // left  (-x)
            Face(new Vector3(h.x, -h.y, -h.z), new Vector3(0, 0, size.z), new Vector3(0, size.y, 0), Vector3.right);   // right (+x)
            Face(new Vector3(-h.x, h.y, -h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, size.z), Vector3.up);      // top
            Face(new Vector3(-h.x, -h.y, h.z), new Vector3(size.x, 0, 0), new Vector3(0, 0, -size.z), Vector3.down);   // bottom
            var m = new Mesh { name = "Bloc" };
            m.SetVertices(v);
            m.SetUVs(0, uv);
            m.SetTriangles(tri, 0);
            m.RecalculateNormals();
            m.RecalculateTangents();
            return m;
        }

        /// <summary>A Poly Haven prop standing on the ground.</summary>
        static GameObject Place(Transform parent, string prefabName, Vector3 pos, float yaw, float scale = 1f, Color? tint = null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/_Game/Prefabs/PolyHaven/{prefabName}.prefab");
            if (!prefab) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.transform.localScale = Vector3.one * scale;
            if (tint.HasValue)
                foreach (var r in go.GetComponentsInChildren<Renderer>())
                    r.sharedMaterials = r.sharedMaterials.Select(m => ContentBuilder.Tinted(m, tint.Value)).ToArray();
            return go;
        }

        static void Scatter(Transform parent, string prefabName, Vector3 center, float rMin, float rMax, int count, float sMin, float sMax, Color? tint = null, float minHeight = 0f)
        {
            for (int i = 0; i < count; i++)
            {
                float a = R(0f, Mathf.PI * 2f), r = Mathf.Sqrt(R(rMin * rMin, rMax * rMax));
                var p = G(center.x + Mathf.Cos(a) * r, center.z + Mathf.Sin(a) * r);
                if (p.y < Mathf.Max(SeaLevel + 0.6f, minHeight)) continue;
                Place(parent, prefabName, p, R(0f, 360f), R(sMin, sMax), tint);
            }
        }

        static GameObject Marker(string id, Vector3 pos, Transform parent = null)
        {
            var go = new GameObject($"Repère {id}");
            go.transform.SetParent(parent ? parent : root, false);
            go.transform.position = pos;
            go.AddComponent<QuestMarkerAnchor>().id = id;
            return go;
        }

        static Light PointLight(Transform parent, Vector3 world, Color color, float range, float intensity)
        {
            var l = new GameObject("Lumière").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.position = world;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
            return l;
        }

        static void AddWaypoint(Transform parent, string id, string display, Vector3 pos, float yaw)
        {
            if (!template) return;
            var go = UnityEngine.Object.Instantiate(template, parent);
            go.name = $"Sentinelle — {display}";
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            var wp = go.GetComponent<Waypoint>();
            if (!wp) return;
            wp.id = id;
            wp.displayName = display;
        }

        static void Zone(Transform parent, Vector3 pos, float radius, string regionName, Color fog, float fogStrength, float density, Color ambient, RegionWeather weather, float rate)
        {
            var go = new GameObject($"Région — {regionName}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var z = go.AddComponent<RegionZone>();
            z.regionName = regionName;
            z.radius = radius;
            z.fogColor = fog;
            z.fogStrength = fogStrength;
            z.fogDensityMultiplier = density;
            z.ambientTint = ambient;
            z.weather = weather;
            z.weatherRate = rate;
        }

        // ---------------------------------------------------------------- NPCs, chests, camps, bosses, story props

        static GameObject Npc(Transform parent, string model, string id, string display, string title, Vector3 pos, Vector3 lookAt, string idle = "Idle", Color? outfit = null, float scale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterImporter.PrefabDir}/{model}.prefab");
            var rootGo = new GameObject(display);
            rootGo.transform.SetParent(parent, false);
            rootGo.transform.position = pos;
            Vector3 d = lookAt - pos;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) rootGo.transform.rotation = Quaternion.LookRotation(d);
            if (!prefab) return rootGo;
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, rootGo.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            inst.transform.localScale = Vector3.one * scale;
            if (outfit.HasValue)
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = r.sharedMaterials.Select(m => IsGarment(m) ? ContentBuilder.Tinted(m, outfit.Value) : m).ToArray();
            var npc = rootGo.AddComponent<NPC>();
            npc.id = id;
            npc.displayName = display;
            npc.title = title;
            npc.range = 3f;
            var rig = inst.GetComponent<Animator>();
            rig.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimationSetup.ControllerPath);
            var anim = rootGo.AddComponent<NpcAnimator>();
            anim.rig = rig;
            anim.idleState = idle;
            anim.speakerName = display;
            var col = rootGo.AddComponent<CapsuleCollider>();
            col.height = 1.8f * scale;
            col.radius = 0.3f;
            col.center = new Vector3(0f, 0.9f * scale, 0f);
            return rootGo;
        }

        static bool IsGarment(Material m)
        {
            if (!m) return false;
            string n = m.name.ToLowerInvariant();
            return n.Contains("tunic") || n.Contains("robe") || n.Contains("suit") || n.Contains("pants");
        }

        static void Chest(Transform parent, string id, int tier, Vector3 pos, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{ContentBuilder.PrefabDir}/Chest.prefab");
            if (prefab) ContentBuilder.PlaceChest(prefab, parent, id, tier, pos, yaw);
        }

        static EnemySpawner Camp(Transform parent, string id, string label, Vector3 pos, GameObject[] prefabs, int count, float radius, int level, bool story = false, bool visibleMarker = true)
        {
            var s = ContentBuilder.Camp(parent, id, label, pos, prefabs, count, radius, level);
            if (story)
            {
                s.spawnOnStart = false;
                s.respawn = false;
            }
            if (visibleMarker)
            {
                var m = new GameObject("Marqueur").AddComponent<QuestMarkerAnchor>();
                m.transform.SetParent(s.transform, false);
                m.id = id;
            }
            return s;
        }

        static EnemySpawner Boss(Transform parent, string id, GameObject prefab, Vector3 pos, int level, GameObject[] adds = null)
        {
            var s = Camp(parent, id, prefab.name, pos, new[] { prefab }, 1, 0.5f, level, true);
            if (adds != null) s.extraPrefabs = adds;
            return s;
        }

        /// <summary>A glowing story pillar / brazier the hero can touch.</summary>
        static StoryObject Prop(Transform parent, string id, Vector3 pos, Color color, string prompt, string reportId, string scene, string gateQuest, int gateStep, bool brazier = false, string cinematic = null)
        {
            var g = new GameObject($"{prompt} — {id}");
            g.transform.SetParent(parent, false);
            g.transform.position = pos;
            var stone = Stone(new Color(0.7f, 0.7f, 0.75f));
            GameObject glowGo;
            if (brazier)
            {
                Block(g.transform, pos + Vector3.up * 0.55f, new Vector3(0.9f, 1.1f, 0.9f), 0f, stone, 0f, 1.5f);
                Block(g.transform, pos + Vector3.up * 1.2f, new Vector3(1.3f, 0.25f, 1.3f), 0f, stone, 0f, 1.5f);
                glowGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glowGo.transform.SetParent(g.transform, false);
                glowGo.transform.position = pos + Vector3.up * 1.65f;
                glowGo.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
            }
            else
            {
                Block(g.transform, pos + Vector3.up * 0.9f, new Vector3(0.8f, 1.8f, 0.8f), R(0f, 45f), stone, 0f, 1.5f);
                glowGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                glowGo.transform.SetParent(g.transform, false);
                glowGo.transform.position = pos + Vector3.up * 2.35f;
                glowGo.transform.localScale = Vector3.one * 0.55f;
            }
            UnityEngine.Object.DestroyImmediate(glowGo.GetComponent<Collider>());
            glowGo.name = "Éclat";
            var gr = glowGo.GetComponent<Renderer>();
            gr.sharedMaterial = Glow($"Prop_{ColorUtility.ToHtmlStringRGB(color)}", color);
            var light = PointLight(g.transform, glowGo.transform.position, color, 9f, 2.5f);
            var so = g.AddComponent<StoryObject>();
            so.id = id;
            so.prompt = prompt;
            so.reportId = reportId;
            so.sceneKey = scene;
            so.gateQuest = gateQuest;
            so.gateStep = gateStep;
            so.glow = gr;
            so.glowLight = light;
            so.glowColor = color;
            so.cinematicId = cinematic;
            so.range = 3.2f;
            var col = g.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 1f, 0f);
            col.size = new Vector3(1f, 2f, 1f);
            return so;
        }

        /// <summary>A scorched or ruined stone wall piece: used to suggest houses, walls and portals.</summary>
        static void RuinWall(Transform parent, Vector3 pos, float yaw, float length, float height, Material mat)
        {
            Block(parent, pos + Vector3.up * height * 0.5f, new Vector3(length, height, 0.7f), yaw, mat, 0f, 2.5f);
        }
    }
}
