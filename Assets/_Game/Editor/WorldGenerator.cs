using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Builds the open-world scene from scratch:
    /// 2 km island with ridged mountains, terraced cliffs, lakes and hydraulic erosion; 7 PBR terrain layers
    /// (Poly Haven) painted by height/slope/biome with dirt paths between points of interest; procedural
    /// conifer/broadleaf forests, photo grass, scanned undergrowth, rocks and cliff faces; Gerstner water,
    /// cloud dome, starting camp with animated campfire, waypoints, ruins and a mountain-top tower;
    /// day/night lighting, wind, atmosphere particles, cinematic post-processing and the player rig.
    /// Menu: Last World/Generate World.
    /// </summary>
    public static class WorldGenerator
    {
        const string Root = "Assets/_Game";
        const string GenDir = Root + "/Generated";
        const string MatDir = Root + "/Materials";
        const string ScenePath = Root + "/Scenes/World.unity";

        const int HeightRes = 1025;
        const int AlphaRes = 2048;
        const int DetailRes = 1024;
        const float WorldSize = 2048f;
        const float WorldHeight = 420f;
        const float SeaLevelNorm = 0.08f;
        static float SeaLevel => SeaLevelNorm * WorldHeight;

        static readonly Vector2 SpawnUV = new(0.5f, 0.22f);

        // Terrain layer indices
        const int LGrass = 0, LForest = 1, LCliff = 2, LMountain = 3, LSand = 4, LSnow = 5, LDirt = 6;

        static System.Random rng;
        static Vector2[] offsets;

        [MenuItem("Last World/Generate World")]
        public static void GenerateMenu() => Generate(1337);

        public static void Generate(int seed)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            rng = new System.Random(seed);
            offsets = new Vector2[10];
            for (int i = 0; i < offsets.Length; i++) offsets[i] = new Vector2(Rand(0f, 5000f), Rand(0f, 5000f));

            EnsureFolders();
            ArtLibrary.ConfigureImporters();
            var veg = TreeFactory.BuildAll(seed);
            var profile = RenderSetup.Configure();
            Log(timer, "art & render setup");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var data = BuildTerrainData();
            Log(timer, "heightmap + erosion");

            Vector3 camp = Ground(data, SpawnUV.x * WorldSize, SpawnUV.y * WorldSize);
            var waypoints = ChooseWaypoints(data, camp);
            var paths = BuildPathNetwork(camp, waypoints);

            float[,,] alphas = PaintSplat(data, paths);
            Log(timer, "splat");
            PlaceVegetation(data, alphas, veg);
            PlaceGrass(data, alphas, veg);
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Log(timer, "vegetation");

            CreateTerrain(data);
            BuildWater();
            BuildClouds();

            var props = new GameObject("Décors").transform;
            PlaceRocksAndCliffs(data, alphas, props);
            BuildCamp(data, props, camp);
            BuildWaypoints(data, props, waypoints);
            BuildRuins(data, props, camp, waypoints);
            BuildRuinTower(data, props);
            Log(timer, "props");

            var cycle = BuildLighting(profile, camp);
            int playerLayer = LayerMask.NameToLayer("Player");
            var player = BuildPlayer(camp + new Vector3(0f, 0.3f, -3f), playerLayer);
            BuildCamera(player, playerLayer);
            new GameObject("HUD").AddComponent<HUD>();
            BuildAmbientParticles();

            EditorSceneManager.SaveScene(scene, ScenePath);
            var scenes = new List<EditorBuildSettingsScene> { new(ScenePath, true) };
            foreach (var s in EditorBuildSettings.scenes)
                if (s.path != ScenePath) scenes.Add(s);
            EditorBuildSettings.scenes = scenes.ToArray();
            AssetDatabase.SaveAssets();
            Debug.Log($"[WorldGenerator] Monde généré en {timer.Elapsed.TotalSeconds:F0}s : {data.treeInstanceCount} arbres/végétaux, scène {ScenePath}");
        }

        static void Log(System.Diagnostics.Stopwatch t, string step) => Debug.Log($"[WorldGenerator] {step} — {t.Elapsed.TotalSeconds:F1}s");

        // ================================================================ Heightmap

        static TerrainData BuildTerrainData()
        {
            var data = new TerrainData();
            data.heightmapResolution = HeightRes;
            data.size = new Vector3(WorldSize, WorldHeight, WorldSize);
            data.alphamapResolution = AlphaRes;
            data.baseMapResolution = 1024;
            data.SetDetailResolution(DetailRes, 32);
            SaveAsset(data, GenDir + "/WorldTerrain.asset");

            var lakes = new List<Vector3>();
            for (int guard = 0; lakes.Count < 4 && guard < 400; guard++)
            {
                var c = new Vector2(Rand(0.22f, 0.78f), Rand(0.3f, 0.8f));
                if (Vector2.Distance(c, SpawnUV) < 0.14f) continue;
                bool ok = true;
                foreach (var l in lakes)
                    if (Vector2.Distance(c, new Vector2(l.x, l.y)) < 0.18f) ok = false;
                if (ok) lakes.Add(new Vector3(c.x, c.y, Rand(0.022f, 0.04f)));
            }

            int n = HeightRes;
            var map = new float[n * n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
                map[z * n + x] = RawHeight(x / (n - 1f), z / (n - 1f), lakes);

            Erode(map, n, 300000, 4242);

            float spawnHeight = Mathf.Clamp(map[(int)(SpawnUV.y * (n - 1)) * n + (int)(SpawnUV.x * (n - 1))], SeaLevelNorm + 0.03f, 0.2f);
            var h = new float[n, n];
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float u = x / (n - 1f), v = z / (n - 1f);
                float ds = Vector2.Distance(new Vector2(u, v), SpawnUV);
                h[z, x] = Mathf.Clamp01(Mathf.Lerp(spawnHeight, map[z * n + x], Smooth(0.014f, 0.034f, ds)));
            }
            data.SetHeights(0, 0, h);
            return data;
        }

        static float RawHeight(float u, float v, List<Vector3> lakes)
        {
            float dx = u - 0.5f, dz = v - 0.5f;
            float warp = (Fbm(u * 3f, v * 3f, 4, 0) - 0.5f) * 0.38f;
            float d = Mathf.Sqrt(dx * dx + dz * dz) * 2f + warp;
            float island = Smooth(0.93f, 0.7f, d);

            float baseLand = Fbm(u * 6f, v * 6f, 6, 1);
            float mountainMask = Smooth(0.5f, 0.68f, Fbm(u * 2.2f, v * 2.2f, 3, 2));
            float ridges = Ridged(u * 7f, v * 7f, 6, 3);
            float hills = Smooth(0.45f, 0.72f, Fbm(u * 3.5f, v * 3.5f, 4, 5)) * 0.08f * (1f - mountainMask);
            float land = 0.095f + baseLand * 0.13f + hills + Mathf.Pow(ridges, 1.6f) * mountainMask * 0.72f;

            const float steps = 22f;
            float t = land * steps;
            float frac = t - Mathf.Floor(t);
            float terraced = (Mathf.Floor(t) + Smooth(0.5f, 1f, frac)) / steps;
            land = Mathf.Lerp(land, terraced, 0.55f * mountainMask);

            float seabed = baseLand * 0.05f;
            float height = Mathf.Lerp(seabed, land, island);

            foreach (var l in lakes)
            {
                float ld = Vector2.Distance(new Vector2(u, v), new Vector2(l.x, l.y));
                float bed = SeaLevelNorm - 0.02f;
                height = Mathf.Lerp(Mathf.Min(height, bed), height, Smooth(l.z * 0.55f, l.z * 1.5f, ld));
            }
            return height;
        }

        /// <summary>Particle-based hydraulic erosion (after S. Lague): carves gullies and deposits sediment in valleys.</summary>
        static void Erode(float[] map, int size, int iterations, int seed)
        {
            var prng = new System.Random(seed);
            const int radius = 3;
            const float inertia = 0.05f, capacityFactor = 6f, minCapacity = 0.0005f;
            const float erodeSpeed = 0.3f, depositSpeed = 0.3f, evaporate = 0.015f, gravity = 4f;
            const int maxLifetime = 40;

            var offX = new List<int>();
            var offY = new List<int>();
            var weights = new List<float>();
            float wsum = 0f;
            for (int y = -radius; y <= radius; y++)
            for (int x = -radius; x <= radius; x++)
            {
                float dist = Mathf.Sqrt(x * x + y * y);
                if (dist >= radius) continue;
                offX.Add(x);
                offY.Add(y);
                float w = 1f - dist / radius;
                weights.Add(w);
                wsum += w;
            }
            for (int i = 0; i < weights.Count; i++) weights[i] /= wsum;

            for (int it = 0; it < iterations; it++)
            {
                float posX = (float)prng.NextDouble() * (size - 2), posY = (float)prng.NextDouble() * (size - 2);
                float dirX = 0f, dirY = 0f, speed = 1f, water = 1f, sediment = 0f;

                for (int life = 0; life < maxLifetime; life++)
                {
                    int nodeX = (int)posX, nodeY = (int)posY;
                    int idx = nodeY * size + nodeX;
                    float cx = posX - nodeX, cy = posY - nodeY;
                    HeightGradient(map, size, posX, posY, out float h, out float gx, out float gy);

                    dirX = dirX * inertia - gx * (1f - inertia);
                    dirY = dirY * inertia - gy * (1f - inertia);
                    float len = Mathf.Sqrt(dirX * dirX + dirY * dirY);
                    if (len < 1e-6f) break;
                    dirX /= len;
                    dirY /= len;
                    posX += dirX;
                    posY += dirY;
                    if (posX < 0 || posX >= size - 1 || posY < 0 || posY >= size - 1) break;

                    HeightGradient(map, size, posX, posY, out float newH, out _, out _);
                    float deltaH = newH - h;
                    float capacity = Mathf.Max(-deltaH * speed * water * capacityFactor, minCapacity);

                    if (sediment > capacity || deltaH > 0f)
                    {
                        float amount = deltaH > 0f ? Mathf.Min(deltaH, sediment) : (sediment - capacity) * depositSpeed;
                        sediment -= amount;
                        map[idx] += amount * (1 - cx) * (1 - cy);
                        map[idx + 1] += amount * cx * (1 - cy);
                        map[idx + size] += amount * (1 - cx) * cy;
                        map[idx + size + 1] += amount * cx * cy;
                    }
                    else
                    {
                        float amount = Mathf.Min((capacity - sediment) * erodeSpeed, -deltaH);
                        for (int b = 0; b < offX.Count; b++)
                        {
                            int bx = nodeX + offX[b], by = nodeY + offY[b];
                            if (bx < 0 || by < 0 || bx >= size || by >= size) continue;
                            int bi = by * size + bx;
                            float delta = Mathf.Min(map[bi], amount * weights[b]);
                            map[bi] -= delta;
                            sediment += delta;
                        }
                    }
                    speed = Mathf.Sqrt(Mathf.Max(0f, speed * speed - deltaH * gravity));
                    water *= 1f - evaporate;
                }
            }
        }

        static void HeightGradient(float[] map, int size, float x, float y, out float h, out float gx, out float gy)
        {
            int cx = (int)x, cy = (int)y;
            float u = x - cx, v = y - cy;
            int i = cy * size + cx;
            float nw = map[i], ne = map[i + 1], sw = map[i + size], se = map[i + size + 1];
            gx = (ne - nw) * (1 - v) + (se - sw) * v;
            gy = (sw - nw) * (1 - u) + (se - ne) * u;
            h = nw * (1 - u) * (1 - v) + ne * u * (1 - v) + sw * (1 - u) * v + se * u * v;
        }

        // ================================================================ Points of interest & paths

        static List<Vector3> ChooseWaypoints(TerrainData data, Vector3 camp)
        {
            var points = new List<Vector3>();
            var taken = new List<Vector3> { camp };
            for (int guard = 0; points.Count < 8 && guard < 6000; guard++)
            {
                float u = Rand(0.14f, 0.86f), v = Rand(0.14f, 0.86f);
                float height = data.GetInterpolatedHeight(u, v);
                if (height < SeaLevel + 5f || height > 260f || data.GetSteepness(u, v) > 12f) continue;
                var p = new Vector3(u * WorldSize, height, v * WorldSize);
                bool far = true;
                foreach (var q in taken)
                    if (Vector3.Distance(p, q) < 300f) far = false;
                if (!far) continue;
                points.Add(p);
                taken.Add(p);
            }
            return points;
        }

        /// <summary>Connects camp and waypoints with a minimum spanning tree of wobbly paths (world XZ points).</summary>
        static List<List<Vector2>> BuildPathNetwork(Vector3 camp, List<Vector3> waypoints)
        {
            var nodes = new List<Vector2> { new(camp.x, camp.z) };
            foreach (var w in waypoints) nodes.Add(new Vector2(w.x, w.z));
            var connected = new List<int> { 0 };
            var remaining = new List<int>();
            for (int i = 1; i < nodes.Count; i++) remaining.Add(i);
            var paths = new List<List<Vector2>>();

            while (remaining.Count > 0)
            {
                float best = float.MaxValue;
                int bi = -1, bj = -1;
                foreach (int i in connected)
                foreach (int j in remaining)
                {
                    float d = Vector2.Distance(nodes[i], nodes[j]);
                    if (d < best) { best = d; bi = i; bj = j; }
                }
                connected.Add(bj);
                remaining.Remove(bj);

                var a = nodes[bi];
                var b = nodes[bj];
                var perp = new Vector2(-(b - a).y, (b - a).x).normalized;
                float o = Rand(0f, 100f);
                var path = new List<Vector2>();
                int samples = Mathf.CeilToInt(best / 1.5f);
                for (int s = 0; s <= samples; s++)
                {
                    float t = (float)s / samples;
                    float wobble = (Mathf.PerlinNoise(o + t * 4f, o) - 0.5f) * 2f * best * 0.12f * Mathf.Sin(t * Mathf.PI);
                    path.Add(Vector2.Lerp(a, b, t) + perp * wobble);
                }
                paths.Add(path);
            }
            return paths;
        }

        // ================================================================ Texturing

        static float[,,] PaintSplat(TerrainData data, List<List<Vector2>> paths)
        {
            string layerDir = GenDir + "/TerrainLayers";
            ArtLibrary.EnsureFolder(layerDir);
            data.terrainLayers = new[]
            {
                ArtLibrary.TerrainLayerFor("leafy_grass", 3.5f, $"{layerDir}/Grass.terrainlayer", new Color(0.5f, 0.74f, 0.32f, 1f)),
                ArtLibrary.TerrainLayerFor("forest_leaves_04", 4f, $"{layerDir}/ForestFloor.terrainlayer", new Color(0.8f, 0.88f, 0.7f, 1f)),
                ArtLibrary.TerrainLayerFor("mossy_rock", 10f, $"{layerDir}/Cliff.terrainlayer"),
                ArtLibrary.TerrainLayerFor("rock_wall_08", 16f, $"{layerDir}/MountainRock.terrainlayer"),
                ArtLibrary.TerrainLayerFor("coast_sand_01", 6f, $"{layerDir}/Sand.terrainlayer"),
                ArtLibrary.TerrainLayerFor("snow_02", 8f, $"{layerDir}/Snow.terrainlayer"),
                ArtLibrary.TerrainLayerFor("dirt_floor", 3f, $"{layerDir}/Dirt.terrainlayer"),
            };

            int res = AlphaRes;
            var pathMask = new float[res, res];
            float texel = WorldSize / res;
            foreach (var path in paths)
            foreach (var p in path)
            {
                float width = 1.6f + Mathf.PerlinNoise(p.x * 0.02f, p.y * 0.02f) * 1.2f;
                int r = Mathf.CeilToInt((width + 1.5f) / texel);
                int px = (int)(p.x / texel), pz = (int)(p.y / texel);
                for (int dz = -r; dz <= r; dz++)
                for (int dx = -r; dx <= r; dx++)
                {
                    int x = px + dx, z = pz + dz;
                    if (x < 0 || z < 0 || x >= res || z >= res) continue;
                    float dist = Mathf.Sqrt(dx * dx + dz * dz) * texel;
                    pathMask[z, x] = Mathf.Max(pathMask[z, x], Smooth(width + 1.5f, width * 0.5f, dist));
                }
            }

            int layers = data.terrainLayers.Length;
            var a = new float[res, res, layers];
            var w = new float[layers];
            float sea = SeaLevel;
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                float u = (x + 0.5f) / res, v = (z + 0.5f) / res;
                float height = data.GetInterpolatedHeight(u, v);
                float steep = data.GetSteepness(u, v);
                float noise = Fbm(u * 40f, v * 40f, 3, 6);

                float rock = Smooth(28f + noise * 8f, 40f + noise * 6f, steep);
                float highRock = rock * Smooth(130f, 210f, height);
                float cliff = rock - highRock;
                float alpine = Smooth(200f, 270f, height + noise * 30f) * (1f - rock);
                float snow = Smooth(300f, 345f, height + noise * 35f) * (1f - rock * 0.5f);
                float sand = Smooth(sea + 3.5f + noise * 2f, sea + 0.6f, height) * (1f - rock);
                float rest = Mathf.Max(0f, 1f - rock - sand - snow - alpine);
                float forest = Smooth(0.45f, 0.57f, Fbm(u * 9f, v * 9f, 5, 4)) * Smooth(210f, 160f, height);
                float dirt = Smooth(0.66f, 0.74f, Fbm(u * 28f, v * 28f, 3, 7)) * rest * 0.7f;
                float path = pathMask[z, x] * (1f - rock);

                w[LGrass] = rest * (1f - forest) * (1f - dirt);
                w[LForest] = rest * forest * (1f - dirt);
                w[LCliff] = cliff;
                w[LMountain] = highRock + alpine * (1f - snow);
                w[LSand] = sand;
                w[LSnow] = snow;
                w[LDirt] = rest * dirt;
                for (int i = 0; i < layers; i++) w[i] *= 1f - path;
                w[LDirt] += path;

                float sum = 0f;
                for (int i = 0; i < layers; i++) sum += w[i];
                if (sum <= 1e-5f) { w[LGrass] = 1f; sum = 1f; }
                for (int i = 0; i < layers; i++) a[z, x, i] = w[i] / sum;
            }
            data.SetAlphamaps(0, 0, a);
            return a;
        }

        // ================================================================ Vegetation

        static void PlaceVegetation(TerrainData data, float[,,] alphas, TreeFactory.Result veg)
        {
            var protos = new List<TreePrototype>();
            int Add(GameObject prefab)
            {
                if (!prefab) return -1;
                protos.Add(new TreePrototype { prefab = prefab });
                return protos.Count - 1;
            }

            var fir = new List<int>();
            foreach (var p in veg.conifers) fir.Add(Add(p));
            var broad = new List<int>();
            foreach (var p in veg.broadleaves) broad.Add(Add(p));

            int Under(string id, float cull, float wind = 0.15f) => Add(ArtLibrary.ModelPrefab(id, true, cull, false, wind));
            int fern = Under("fern_02", 0.03f, 0.25f);
            int shrub2 = Under("shrub_02", 0.02f);
            int shrub3 = Under("shrub_03", 0.04f);
            int shrub4 = Under("shrub_04", 0.04f);
            int flower = Under("flower_ursinia", 0.05f, 0.2f);
            int periwinkle = Under("periwinkle_plant", 0.05f);
            int nettle = Under("nettle_plant", 0.05f, 0.2f);
            int branches = Add(ArtLibrary.ModelPrefab("dry_branches_medium_01", false, 0.05f, false));
            int stump = Add(ArtLibrary.ModelPrefab("tree_stump_01", false, 0.03f, false));
            int deadTrunk = Add(ArtLibrary.ModelPrefab("dead_tree_trunk", false, 0.02f, false));
            data.treePrototypes = protos.ToArray();

            var instances = new List<TreeInstance>();
            void Place(int proto, float u, float v, float height, float scale, float yScale = 1f)
            {
                if (proto < 0) return;
                instances.Add(new TreeInstance
                {
                    position = new Vector3(u, height / WorldHeight, v),
                    prototypeIndex = proto,
                    widthScale = scale,
                    heightScale = scale * yScale,
                    rotation = Rand(0f, Mathf.PI * 2f),
                    color = Color.white,
                    lightmapColor = Color.white,
                });
            }

            int res = data.alphamapResolution;
            bool Sample(float u, float v, out float height, out float steep, out float grass, out float forest, out float dirt)
            {
                height = data.GetInterpolatedHeight(u, v);
                steep = data.GetSteepness(u, v);
                int ax = Mathf.Clamp((int)(u * res), 0, res - 1), az = Mathf.Clamp((int)(v * res), 0, res - 1);
                grass = alphas[az, ax, LGrass];
                forest = alphas[az, ax, LForest];
                dirt = alphas[az, ax, LDirt];
                return height > SeaLevel + 1.5f && Vector2.Distance(new Vector2(u, v), SpawnUV) > 0.02f;
            }

            // Trees on a jittered 6 m grid
            const float treeCell = 6f;
            int cells = (int)(WorldSize / treeCell);
            for (int cz = 0; cz < cells; cz++)
            for (int cx = 0; cx < cells; cx++)
            {
                float u = (cx + Rand(0.1f, 0.9f)) * treeCell / WorldSize;
                float v = (cz + Rand(0.1f, 0.9f)) * treeCell / WorldSize;
                if (!Sample(u, v, out float h, out float steep, out float grass, out float forest, out float dirt)) continue;
                if (steep > 30f || h > 250f || dirt > 0.4f) continue;
                float chance = forest * 0.85f + grass * 0.025f;
                if (rng.NextDouble() > chance) continue;

                float coniferBias = Smooth(70f, 170f, h) * 0.8f + Fbm(u * 5f, v * 5f, 3, 8) * 0.5f - 0.15f;
                bool conifer = rng.NextDouble() < coniferBias;
                int proto = conifer ? fir[rng.Next(fir.Count)] : broad[rng.Next(broad.Count)];
                Place(proto, u, v, h, Rand(0.8f, 1.25f), Rand(0.9f, 1.15f));
            }

            // Undergrowth on a jittered 3 m grid
            const float underCell = 3f;
            cells = (int)(WorldSize / underCell);
            for (int cz = 0; cz < cells; cz++)
            for (int cx = 0; cx < cells; cx++)
            {
                float u = (cx + Rand(0f, 1f)) * underCell / WorldSize;
                float v = (cz + Rand(0f, 1f)) * underCell / WorldSize;
                if (!Sample(u, v, out float h, out float steep, out float grass, out float forest, out float dirt)) continue;
                if (steep > 32f || h > 240f || dirt > 0.5f) continue;
                double roll = rng.NextDouble();
                if (forest > 0.5f)
                {
                    if (roll < 0.16) Place(fern, u, v, h, Rand(0.8f, 1.4f));
                    else if (roll < 0.19) Place(shrub2, u, v, h, Rand(0.4f, 0.7f));
                    else if (roll < 0.2) Place(branches, u, v, h, Rand(0.8f, 1.2f));
                    else if (roll < 0.204) Place(stump, u, v, h, Rand(0.8f, 1.2f));
                    else if (roll < 0.206) Place(deadTrunk, u, v, h, Rand(0.6f, 0.9f));
                }
                else if (grass > 0.5f)
                {
                    float meadow = Fbm(u * 14f, v * 14f, 3, 9);
                    if (roll < 0.035 * Smooth(0.5f, 0.65f, meadow) * 3f) Place(flower, u, v, h, Rand(0.9f, 1.3f));
                    else if (roll < 0.05) Place(shrub3, u, v, h, Rand(0.8f, 1.3f));
                    else if (roll < 0.06) Place(shrub4, u, v, h, Rand(0.8f, 1.3f));
                    else if (roll < 0.068) Place(periwinkle, u, v, h, Rand(0.9f, 1.2f));
                    else if (roll < 0.075) Place(nettle, u, v, h, Rand(0.9f, 1.3f));
                }
            }
            data.SetTreeInstances(instances.ToArray(), true);
        }

        static void PlaceGrass(TerrainData data, float[,,] alphas, TreeFactory.Result veg)
        {
            DetailPrototype Proto(GameObject prefab, float min, float max) => new()
            {
                usePrototypeMesh = true,
                prototype = prefab,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = min, maxWidth = max,
                minHeight = min, maxHeight = max,
                noiseSpread = 0.3f,
                healthyColor = Color.white,
                dryColor = Color.white,
                alignToGround = 0.6f,
            };
            data.detailPrototypes = new[] { Proto(veg.grass, 0.8f, 1.3f), Proto(veg.grassTall, 0.8f, 1.2f) };
            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);

            int res = data.detailResolution, aRes = data.alphamapResolution;
            var short_ = new int[res, res];
            var tall = new int[res, res];
            for (int z = 0; z < res; z++)
            for (int x = 0; x < res; x++)
            {
                int ax = Mathf.Min(x * aRes / res, aRes - 1), az = Mathf.Min(z * aRes / res, aRes - 1);
                float g = alphas[az, ax, LGrass] + alphas[az, ax, LForest] * 0.25f + alphas[az, ax, LMountain] * 0.1f;
                float u = (x + 0.5f) / res, v = (z + 0.5f) / res;
                float meadow = Fbm(u * 12f, v * 12f, 3, 9);
                short_[z, x] = g > 0.15f ? Mathf.RoundToInt(g * 16f * Rand(0.65f, 1f)) : 0;
                tall[z, x] = g > 0.5f && meadow > 0.5f ? Mathf.RoundToInt(5f * Smooth(0.5f, 0.68f, meadow) * Rand(0.5f, 1f)) : 0;
            }
            data.SetDetailLayer(0, 0, 0, short_);
            data.SetDetailLayer(0, 0, 1, tall);
        }

        static void CreateTerrain(TerrainData data)
        {
            var go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            var terrain = go.GetComponent<Terrain>();
            var mat = SaveAsset(new Material(Shader.Find("Universal Render Pipeline/Terrain/Lit")), MatDir + "/Terrain.mat");
            mat.EnableKeyword("_NORMALMAP");
            mat.EnableKeyword("_MASKMAP");
            mat.SetFloat("_EnableInstancedPerPixelNormal", 1f);
            mat.EnableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
            terrain.materialTemplate = mat;
            terrain.drawInstanced = true;
            terrain.heightmapPixelError = 2f;
            terrain.basemapDistance = 2500f;
            terrain.treeDistance = 2500f;
            terrain.treeBillboardDistance = 2500f;
            terrain.treeMaximumFullLODCount = 100000;
            terrain.detailObjectDistance = 130f;
            terrain.detailObjectDensity = 1f;
            terrain.shadowCastingMode = ShadowCastingMode.On;
        }

        // ================================================================ Water & sky

        static void BuildWater()
        {
            var mat = ArtLibrary.LoadOrCreateMaterial(MatDir + "/Ocean.mat", "LastWorld/Water");
            mat.SetTexture("_NormalMap", MakeWaterNormal());
            mat.SetTexture("_FoamMap", MakeFoamTexture());
            EditorUtility.SetDirty(mat);

            var calm = ArtLibrary.LoadOrCreateMaterial(MatDir + "/OceanFar.mat", "LastWorld/Water");
            calm.CopyPropertiesFromMaterial(mat);
            foreach (var w in new[] { "_WaveA", "_WaveB", "_WaveC", "_WaveD" })
            {
                var wave = mat.GetVector(w);
                wave.z = 0f;
                calm.SetVector(w, wave);
            }
            EditorUtility.SetDirty(calm);

            var root = new GameObject("Eau").transform;
            var center = new Vector3(WorldSize * 0.5f, SeaLevel, WorldSize * 0.5f);
            const float inner = 1100f;

            var near = new GameObject("Mer (vagues)");
            near.transform.SetParent(root, false);
            near.transform.position = center;
            near.AddComponent<MeshFilter>().sharedMesh = SaveAsset(WaterGrid(inner * 2f, 440), GenDir + "/WaterGrid.asset");
            var r1 = near.AddComponent<MeshRenderer>();
            r1.sharedMaterial = mat;
            r1.shadowCastingMode = ShadowCastingMode.Off;

            var far = new GameObject("Mer (horizon)");
            far.transform.SetParent(root, false);
            far.transform.position = center + Vector3.down * 0.05f;
            far.AddComponent<MeshFilter>().sharedMesh = SaveAsset(WaterRing(inner, 9000f), GenDir + "/WaterRing.asset");
            var r2 = far.AddComponent<MeshRenderer>();
            r2.sharedMaterial = calm;
            r2.shadowCastingMode = ShadowCastingMode.Off;
        }

        static void BuildClouds()
        {
            var mat = ArtLibrary.LoadOrCreateMaterial(MatDir + "/Clouds.mat", "LastWorld/Clouds");
            var dome = new GameObject("Nuages");
            dome.AddComponent<MeshFilter>().sharedMesh = SaveAsset(Hemisphere(3200f, 48, 16), GenDir + "/CloudDome.asset");
            var r = dome.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            dome.AddComponent<SkyDome>();
        }

        // ================================================================ Rocks & cliffs

        static void PlaceRocksAndCliffs(TerrainData data, float[,,] alphas, Transform parent)
        {
            string[] scatterIds = { "rock_07", "rock_09", "stone_01", "boulder_01", "namaqualand_boulder_05" };
            string[] mossIds = { "rock_moss_set_01", "rock_moss_set_02" };
            string[] cliffIds = { "rock_face_01", "rock_face_02" };
            var scatter = new List<GameObject>();
            foreach (var id in scatterIds) { var p = ArtLibrary.ModelPrefab(id, false, 0.006f, true); if (p) scatter.Add(p); }
            var moss = new List<GameObject>();
            foreach (var id in mossIds) { var p = ArtLibrary.ModelPrefab(id, false, 0.006f, true); if (p) moss.Add(p); }
            var cliffs = new List<GameObject>();
            foreach (var id in cliffIds) { var p = ArtLibrary.ModelPrefab(id, false, 0.004f, true); if (p) cliffs.Add(p); }

            var group = new GameObject("Rochers").transform;
            group.SetParent(parent, false);
            int res = data.alphamapResolution;

            // Scanned assets are real-world sized (some "rocks" are 15 cm pebbles): scale to a target size instead.
            var maxDim = new Dictionary<GameObject, float>();
            foreach (var p in scatter) maxDim[p] = MaxDim(p);
            foreach (var p in moss) maxDim[p] = MaxDim(p);
            foreach (var p in cliffs) maxDim[p] = MaxDim(p);

            GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, float targetSize)
            {
                float scale = targetSize / maxDim[prefab];
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                go.transform.SetPositionAndRotation(pos, rot);
                go.transform.localScale = Vector3.one * scale;
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic);
                return go;
            }

            // Scattered boulders, more in mountains and along beaches
            int placed = 0;
            for (int guard = 0; placed < 650 && guard < 20000; guard++)
            {
                float u = Rand(0.05f, 0.95f), v = Rand(0.05f, 0.95f);
                float h = data.GetInterpolatedHeight(u, v);
                if (h < SeaLevel - 2f || Vector2.Distance(new Vector2(u, v), SpawnUV) < 0.02f) continue;
                int ax = Mathf.Clamp((int)(u * res), 0, res - 1), az = Mathf.Clamp((int)(v * res), 0, res - 1);
                float rocky = alphas[az, ax, LCliff] + alphas[az, ax, LMountain] + alphas[az, ax, LSand] * 0.5f;
                float forest = alphas[az, ax, LForest];
                if (rng.NextDouble() > 0.15f + rocky * 0.8f + forest * 0.2f) continue;

                bool mossy = forest > 0.5f && moss.Count > 0 && rng.NextDouble() < 0.5;
                var prefab = mossy ? moss[rng.Next(moss.Count)] : scatter[rng.Next(scatter.Count)];
                Vector3 normal = data.GetInterpolatedNormal(u, v);
                var rot = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, normal, 0.6f)) * Quaternion.Euler(0f, Rand(0f, 360f), 0f);
                float size = mossy ? Rand(3.5f, 8f) : (rng.NextDouble() < 0.25 ? Rand(3f, 7f) : Rand(0.8f, 2.2f));
                Spawn(prefab, new Vector3(u * WorldSize, h - 0.12f * size, v * WorldSize), rot, size);
                placed++;
            }

            // Real rock faces dressed on steep slopes so cliffs read as solid stone up close
            int cliffCount = 0;
            for (int guard = 0; cliffCount < 260 && guard < 40000 && cliffs.Count > 0; guard++)
            {
                float u = Rand(0.05f, 0.95f), v = Rand(0.05f, 0.95f);
                float steep = data.GetSteepness(u, v);
                if (steep < 42f) continue;
                float h = data.GetInterpolatedHeight(u, v);
                if (h < SeaLevel) continue;
                Vector3 normal = data.GetInterpolatedNormal(u, v);
                Vector3 outward = Vector3.ProjectOnPlane(normal, Vector3.up).normalized;
                if (outward.sqrMagnitude < 0.01f) continue;
                var rot = Quaternion.LookRotation(outward, Vector3.up) * Quaternion.Euler(Rand(-10f, 10f), Rand(-25f, 25f) + 180f, Rand(-8f, 8f));
                float size = Rand(14f, 30f);
                Spawn(cliffs[rng.Next(cliffs.Count)], new Vector3(u * WorldSize, h - size * 0.2f, v * WorldSize) - outward * size * 0.1f, rot, size);
                cliffCount++;
            }
        }

        // ================================================================ Camp

        static void BuildCamp(TerrainData data, Transform parent, Vector3 c)
        {
            var stone = ArtLibrary.SurfaceMaterial("medieval_blocks_02");
            var mossyStone = ArtLibrary.SurfaceMaterial("mossy_stone_wall");
            var planks = ArtLibrary.SurfaceMaterial("weathered_planks");
            var fabric = SimpleMat("TentCanvas", new Color(0.72f, 0.62f, 0.47f), 0.15f);
            var fabricNormal = ArtLibrary.Find($"{ArtLibrary.PolyHaven}/Textures/fabric_pattern_05", "_nor_gl");
            if (fabricNormal)
            {
                fabric.SetTexture("_BumpMap", fabricNormal);
                fabric.SetTextureScale("_BaseMap", Vector2.one * 2f);
                fabric.EnableKeyword("_NORMALMAP");
            }
            var gold = SimpleMat("Gold", new Color(0.92f, 0.74f, 0.32f), 0.75f);
            gold.SetFloat("_Metallic", 0.9f);
            var glow = SimpleMat("AnemoGlow", new Color(0.4f, 1f, 0.85f), 0.8f, new Color(0.9f, 3.2f, 2.6f));
            var ember = SimpleMat("Ember", new Color(1f, 0.45f, 0.1f), 0.5f, new Color(4f, 1.4f, 0.25f));

            var camp = new GameObject("Camp de départ").transform;
            camp.SetParent(parent, false);
            camp.position = c;

            // Altar of the winds
            var altar = new GameObject("Autel des Vents").transform;
            altar.SetParent(camp, false);
            altar.position = Ground(data, c.x, c.z + 14f);
            BoxObj("Socle", altar, new Vector3(0f, 0.05f, 0f), new Vector3(8f, 0.9f, 8f), stone);
            BoxObj("Marche", altar, new Vector3(0f, 0.75f, 0f), new Vector3(5.6f, 0.6f, 5.6f), stone);
            BoxObj("Pilier", altar, new Vector3(0f, 3.55f, 0f), new Vector3(1.2f, 5f, 1.2f), mossyStone);
            BoxObj("Chapiteau", altar, new Vector3(0f, 6.2f, 0f), new Vector3(1.8f, 0.4f, 1.8f), stone);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var dir = Quaternion.Euler(0f, a, 0f) * Vector3.forward;
                BoxObj("Colonnette", altar, dir * 3.2f + Vector3.up * 1.6f, new Vector3(0.5f, 2.2f, 0.5f), mossyStone);
            }
            Prim(PrimitiveType.Cylinder, "Anneau", altar, new Vector3(0f, 6.5f, 0f), new Vector3(2.6f, 0.05f, 2.6f), gold);
            var orb = Prim(PrimitiveType.Sphere, "Orbe", altar, new Vector3(0f, 7.7f, 0f), Vector3.one * 1.3f, glow);
            orb.AddComponent<Hover>();
            AddPointLight(orb.transform, new Color(0.45f, 1f, 0.85f), 16f, 4f, false);

            // Campfire with animated flames and smoke
            var fire = new GameObject("Feu de camp").transform;
            fire.SetParent(camp, false);
            fire.position = Ground(data, c.x - 7f, c.z - 2f);
            var pit = ArtLibrary.ModelPrefab("stone_fire_pit", false, 0.01f, true);
            if (pit) ((GameObject)PrefabUtility.InstantiatePrefab(pit, fire)).transform.localScale = Vector3.one * 1.1f;
            var logs = ArtLibrary.ModelPrefab("dead_tree_trunk", false, 0.01f, true);
            if (logs)
            {
                for (int i = 0; i < 2; i++)
                {
                    var log = (GameObject)PrefabUtility.InstantiatePrefab(logs, fire);
                    float a = i == 0 ? 0f : 110f;
                    log.transform.localPosition = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, -0.05f, 2.6f);
                    log.transform.localRotation = Quaternion.Euler(0f, a + 90f, 0f);
                    log.transform.localScale = Vector3.one * 0.7f;
                }
            }
            var emberGO = Prim(PrimitiveType.Sphere, "Braises", fire, new Vector3(0f, 0.15f, 0f), new Vector3(0.7f, 0.2f, 0.7f), ember);
            var fireLight = AddPointLight(emberGO.transform, new Color(1f, 0.58f, 0.25f), 12f, 4f, true);
            fireLight.transform.localPosition = new Vector3(0f, 3f, 0f);
            fireLight.gameObject.AddComponent<LightFlicker>();
            BuildFireParticles(fire);

            // Tents (A-frame from two fabric panels)
            var tentSpots = new[] { (new Vector2(9f, -7f), 20f), (new Vector2(11f, 3f), -15f), (new Vector2(-11f, 7f), 70f) };
            foreach (var (offset, yaw) in tentSpots)
            {
                var tent = new GameObject("Tente").transform;
                tent.SetParent(camp, false);
                tent.position = Ground(data, c.x + offset.x, c.z + offset.y);
                tent.rotation = Quaternion.Euler(0f, yaw, 0f);
                BoxObj("Pan G", tent, new Vector3(-0.8f, 0.95f, 0f), new Vector3(0.05f, 2.4f, 3.6f), fabric, new Vector3(0f, 0f, -38f));
                BoxObj("Pan D", tent, new Vector3(0.8f, 0.95f, 0f), new Vector3(0.05f, 2.4f, 3.6f), fabric, new Vector3(0f, 0f, 38f));
                BoxObj("Mât", tent, new Vector3(0f, 1.9f, 0f), new Vector3(0.1f, 0.1f, 4f), planks);
            }

            // Crates and a small table
            for (int i = 0; i < 5; i++)
            {
                var p = Ground(data, c.x + 5f + i * 0.95f, c.z - 10f + (i % 2) * 0.9f);
                var crate = BoxObj("Caisse", camp, Vector3.zero, Vector3.one * 0.85f, planks, new Vector3(0f, Rand(-15f, 15f), 0f));
                crate.transform.position = p + Vector3.up * 0.42f;
            }
            var table = new GameObject("Table").transform;
            table.SetParent(camp, false);
            table.position = Ground(data, c.x - 2f, c.z - 8f);
            BoxObj("Plateau", table, new Vector3(0f, 0.8f, 0f), new Vector3(2f, 0.08f, 1f), planks);
            foreach (var lx in new[] { -0.9f, 0.9f })
            foreach (var lz in new[] { -0.4f, 0.4f })
                BoxObj("Pied", table, new Vector3(lx, 0.4f, lz), new Vector3(0.08f, 0.8f, 0.08f), planks);
        }

        static void BuildFireParticles(Transform parent)
        {
            var dot = SoftDotTexture();
            var fireMat = ParticleMat("FX_Fire", dot, true);
            var smokeMat = ParticleMat("FX_Smoke", dot, false);

            var flames = new GameObject("Flammes");
            flames.transform.SetParent(parent, false);
            flames.transform.localPosition = new Vector3(0f, 0.2f, 0f);
            flames.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var ps = flames.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 1f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.8f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(3f, 1.2f, 0.3f, 1f), new Color(2.5f, 1.8f, 0.6f, 1f));
            main.maxParticles = 150;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 45f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 10f;
            shape.radius = 0.3f;
            var size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0.1f));
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            col.color = g;
            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.4f;
            noise.frequency = 1.5f;
            flames.GetComponent<ParticleSystemRenderer>().sharedMaterial = fireMat;

            var smoke = new GameObject("Fumée");
            smoke.transform.SetParent(parent, false);
            smoke.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            smoke.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var sp = smoke.AddComponent<ParticleSystem>();
            var sm = sp.main;
            sm.startLifetime = new ParticleSystem.MinMaxCurve(3f, 5f);
            sm.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
            sm.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.4f);
            sm.startColor = new Color(0.35f, 0.33f, 0.32f, 0.35f);
            sm.maxParticles = 60;
            sm.simulationSpace = ParticleSystemSimulationSpace.World;
            var sem = sp.emission;
            sem.rateOverTime = 7f;
            var sshape = sp.shape;
            sshape.shapeType = ParticleSystemShapeType.Cone;
            sshape.angle = 8f;
            sshape.radius = 0.2f;
            var ssize = sp.sizeOverLifetime;
            ssize.enabled = true;
            ssize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 3f));
            var scol = sp.colorOverLifetime;
            scol.enabled = true;
            var sg = new Gradient();
            sg.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            scol.color = sg;
            var snoise = sp.noise;
            snoise.enabled = true;
            snoise.strength = 0.5f;
            snoise.frequency = 0.4f;
            smoke.GetComponent<ParticleSystemRenderer>().sharedMaterial = smokeMat;
        }

        // ================================================================ Waypoints, ruins, tower

        static void BuildWaypoints(TerrainData data, Transform parent, List<Vector3> points)
        {
            var stone = ArtLibrary.SurfaceMaterial("medieval_blocks_02");
            var glow = SimpleMat("AnemoGlow", new Color(0.4f, 1f, 0.85f), 0.8f, new Color(0.9f, 3.2f, 2.6f));
            var group = new GameObject("Points de téléportation").transform;
            group.SetParent(parent, false);
            for (int i = 0; i < points.Count; i++)
            {
                var wp = new GameObject($"Point de téléportation {i + 1}").transform;
                wp.SetParent(group, false);
                wp.position = points[i];
                BoxObj("Socle", wp, new Vector3(0f, 0.1f, 0f), new Vector3(2.6f, 0.6f, 2.6f), stone);
                BoxObj("Obélisque", wp, new Vector3(0f, 2.3f, 0f), new Vector3(0.75f, 3.8f, 0.75f), stone, new Vector3(0f, 45f, 0f));
                var orb = Prim(PrimitiveType.Sphere, "Orbe", wp, new Vector3(0f, 4.9f, 0f), Vector3.one * 0.6f, glow);
                orb.AddComponent<Hover>();
                AddPointLight(orb.transform, new Color(0.45f, 1f, 0.85f), 8f, 2.5f, false);
            }
        }

        static void BuildRuins(TerrainData data, Transform parent, Vector3 camp, List<Vector3> waypoints)
        {
            var mossy = ArtLibrary.SurfaceMaterial("mossy_stone_wall");
            var blocks = ArtLibrary.SurfaceMaterial("medieval_blocks_02");
            var group = new GameObject("Ruines anciennes").transform;
            group.SetParent(parent, false);

            var taken = new List<Vector3>(waypoints) { camp };
            int sites = 0;
            for (int guard = 0; sites < 6 && guard < 4000; guard++)
            {
                float u = Rand(0.15f, 0.85f), v = Rand(0.15f, 0.85f);
                float h = data.GetInterpolatedHeight(u, v);
                if (h < SeaLevel + 6f || h > 220f || data.GetSteepness(u, v) > 14f) continue;
                var center = new Vector3(u * WorldSize, h, v * WorldSize);
                bool far = true;
                foreach (var q in taken)
                    if (Vector3.Distance(center, q) < 200f) far = false;
                if (!far) continue;
                taken.Add(center);
                sites++;

                var site = new GameObject($"Ruine {sites}").transform;
                site.SetParent(group, false);
                site.position = center;
                int pillars = rng.Next(6, 11);
                float radius = Rand(7f, 12f);
                for (int i = 0; i < pillars; i++)
                {
                    float a = i * Mathf.PI * 2f / pillars;
                    var p = Ground(data, center.x + Mathf.Cos(a) * radius, center.z + Mathf.Sin(a) * radius) - center;
                    double state = rng.NextDouble();
                    if (state < 0.25) continue; // gone
                    if (state < 0.45)
                    {
                        // Fallen pillar lying on the ground
                        BoxObj("Colonne tombée", site, p + Vector3.up * 0.4f, new Vector3(0.9f, 0.9f, Rand(3f, 5f)), mossy,
                            new Vector3(0f, Rand(0f, 360f), Rand(-5f, 5f)));
                        continue;
                    }
                    float height = Rand(2.5f, 7f);
                    BoxObj("Colonne", site, p + Vector3.up * (height * 0.5f - 0.3f), new Vector3(0.95f, height, 0.95f), mossy,
                        new Vector3(Rand(-4f, 4f), Rand(0f, 90f), Rand(-4f, 4f)));
                    BoxObj("Base", site, p + Vector3.up * 0.1f, new Vector3(1.4f, 0.5f, 1.4f), blocks);
                }
                // Cracked floor slabs
                for (int s = 0; s < 14; s++)
                {
                    var off = new Vector3(Rand(-radius, radius) * 0.7f, 0f, Rand(-radius, radius) * 0.7f);
                    var p = Ground(data, center.x + off.x, center.z + off.z) - center;
                    BoxObj("Dalle", site, p, new Vector3(Rand(1.5f, 2.5f), 0.3f, Rand(1.5f, 2.5f)), blocks,
                        new Vector3(Rand(-3f, 3f), Rand(0f, 90f), Rand(-3f, 3f)));
                }
            }
        }

        static void BuildRuinTower(TerrainData data, Transform parent)
        {
            float best = -1f;
            Vector2 bestUV = Vector2.one * 0.5f;
            for (int i = 0; i <= 100; i++)
            for (int j = 0; j <= 100; j++)
            {
                float u = 0.1f + 0.8f * i / 100f, v = 0.1f + 0.8f * j / 100f;
                float h = data.GetInterpolatedHeight(u, v);
                if (h > best) { best = h; bestUV = new Vector2(u, v); }
            }

            var mossy = ArtLibrary.SurfaceMaterial("mossy_stone_wall");
            var blocks = ArtLibrary.SurfaceMaterial("medieval_blocks_02");
            var planks = ArtLibrary.SurfaceMaterial("weathered_planks");
            var gold = SimpleMat("Gold", new Color(0.92f, 0.74f, 0.32f), 0.75f);
            var tower = new GameObject("Tour en ruine").transform;
            tower.SetParent(parent, false);
            tower.position = Ground(data, bestUV.x * WorldSize, bestUV.y * WorldSize);

            const int floors = 8;
            const float floorH = 3.6f, half = 4f, thick = 0.8f, baseY = -10f;
            for (int f = 0; f < floors; f++)
            {
                float y = baseY + f * floorH + floorH * 0.5f;
                for (int side = 0; side < 4; side++)
                {
                    if (f >= 5 && rng.NextDouble() < 0.3) continue;
                    bool zSide = side < 2;
                    float sign = side % 2 == 0 ? 1f : -1f;
                    var pos = zSide ? new Vector3(0f, y, half * sign) : new Vector3(half * sign, y, 0f);
                    var size = zSide ? new Vector3(half * 2f + thick, floorH, thick) : new Vector3(thick, floorH, half * 2f);
                    BoxObj("Mur", tower, pos, size, f % 3 == 0 ? blocks : mossy);
                }
                if (f % 2 == 1)
                    BoxObj("Corniche", tower, new Vector3(0f, baseY + (f + 1) * floorH, 0f), new Vector3(half * 2f + 1.6f, 0.35f, half * 2f + 1.6f), blocks);
            }
            float topY = baseY + floors * floorH;
            BoxObj("Sommet", tower, new Vector3(0f, topY, 0f), new Vector3(half * 2f + 1.6f, 0.5f, half * 2f + 1.6f), blocks);
            var chest = new GameObject("Coffre ancien").transform;
            chest.SetParent(tower, false);
            chest.localPosition = new Vector3(0f, topY + 0.25f, 0f);
            BoxObj("Corps", chest, new Vector3(0f, 0.4f, 0f), new Vector3(1.3f, 0.8f, 0.85f), planks);
            BoxObj("Couvercle", chest, new Vector3(0f, 0.9f, 0f), new Vector3(1.35f, 0.25f, 0.9f), planks);
            BoxObj("Ferrure", chest, new Vector3(0f, 0.6f, 0.44f), new Vector3(0.25f, 0.4f, 0.04f), gold, null, 1f, false);
            AddPointLight(chest, new Color(1f, 0.85f, 0.5f), 7f, 2.5f, false).transform.localPosition = Vector3.up * 1.5f;
        }

        // ================================================================ Lighting & atmosphere

        static DayNightCycle BuildLighting(VolumeProfile profile, Vector3 camp)
        {
            var sun = new GameObject("Soleil").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.95f;
            sun.intensity = 2.2f;

            var moon = new GameObject("Lune").AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.shadows = LightShadows.Soft;
            moon.color = new Color(0.6f, 0.7f, 1f);
            moon.intensity = 0.35f;

            var sky = new Material(Shader.Find("Skybox/Procedural"));
            sky.SetFloat("_SunSize", 0.03f);
            sky.SetFloat("_SunSizeConvergence", 8f);
            sky.SetFloat("_AtmosphereThickness", 1.05f);
            sky.SetColor("_SkyTint", new Color(0.48f, 0.55f, 0.66f));
            sky.SetColor("_GroundColor", new Color(0.36f, 0.4f, 0.45f));
            sky.SetFloat("_Exposure", 1.3f);
            RenderSettings.skybox = SaveAsset(sky, MatDir + "/Sky.mat");
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.0009f;
            RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;

            var probeGO = new GameObject("Sonde de reflets (ciel)");
            probeGO.transform.position = camp + Vector3.up * 30f;
            var probe = probeGO.AddComponent<ReflectionProbe>();
            probe.mode = ReflectionProbeMode.Realtime;
            probe.refreshMode = ReflectionProbeRefreshMode.ViaScripting;
            probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
            probe.size = new Vector3(8000f, 3000f, 8000f);
            probe.cullingMask = 0;
            probe.clearFlags = ReflectionProbeClearFlags.Skybox;
            probe.resolution = 128;
            probe.importance = 0;

            var world = new GameObject("Cycle jour-nuit");
            var cycle = world.AddComponent<DayNightCycle>();
            cycle.sun = sun;
            cycle.moon = moon;
            cycle.skyProbe = probe;
            cycle.timeOfDay = 9f;
            cycle.Apply();
            world.AddComponent<WindController>();

            var volume = new GameObject("Global Volume").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
            return cycle;
        }

        static void BuildAmbientParticles()
        {
            var dot = SoftDotTexture();
            var go = new GameObject("Particules d'ambiance");
            var amb = go.AddComponent<AmbientParticles>();
            amb.moteMaterial = ParticleMat("FX_Mote", dot, true);
            amb.fireflyMaterial = ParticleMat("FX_Firefly", dot, true);
        }

        // ================================================================ Player & camera

        static PlayerController BuildPlayer(Vector3 position, int layer)
        {
            var root = new GameObject("Joueur");
            root.tag = "Player";
            root.transform.position = position;

            var cc = root.AddComponent<CharacterController>();
            cc.height = 1.75f;
            cc.radius = 0.32f;
            cc.center = new Vector3(0f, 0.875f, 0f);
            cc.slopeLimit = 50f;
            cc.stepOffset = 0.35f;
            cc.skinWidth = 0.05f;

            var skin = SimpleMat("P_Skin", new Color(1f, 0.86f, 0.74f), 0.3f);
            var outfit = SimpleMat("P_Outfit", new Color(0.92f, 0.93f, 0.95f), 0.3f);
            var accent = SimpleMat("P_Accent", new Color(0.2f, 0.62f, 0.6f), 0.3f);
            var hair = SimpleMat("P_Hair", new Color(0.95f, 0.82f, 0.45f), 0.4f);
            var dark = SimpleMat("P_Dark", new Color(0.12f, 0.12f, 0.15f), 0.3f);

            var visual = new GameObject("Visuel").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0f, 1f, 0f);
            Prim(PrimitiveType.Capsule, "Corps", visual, Vector3.zero, new Vector3(0.5f, 0.45f, 0.38f), outfit);
            Prim(PrimitiveType.Capsule, "Ceinture", visual, new Vector3(0f, -0.1f, 0f), new Vector3(0.52f, 0.06f, 0.4f), accent);
            Prim(PrimitiveType.Capsule, "Jambe G", visual, new Vector3(-0.12f, -0.62f, 0f), new Vector3(0.17f, 0.36f, 0.17f), dark);
            Prim(PrimitiveType.Capsule, "Jambe D", visual, new Vector3(0.12f, -0.62f, 0f), new Vector3(0.17f, 0.36f, 0.17f), dark);
            Prim(PrimitiveType.Capsule, "Bras G", visual, new Vector3(-0.32f, 0.05f, 0f), new Vector3(0.13f, 0.3f, 0.13f), outfit, false, new Vector3(0f, 0f, -8f));
            Prim(PrimitiveType.Capsule, "Bras D", visual, new Vector3(0.32f, 0.05f, 0f), new Vector3(0.13f, 0.3f, 0.13f), outfit, false, new Vector3(0f, 0f, 8f));
            Prim(PrimitiveType.Sphere, "Tête", visual, new Vector3(0f, 0.6f, 0f), Vector3.one * 0.34f, skin);
            Prim(PrimitiveType.Sphere, "Cheveux", visual, new Vector3(0f, 0.66f, -0.03f), new Vector3(0.37f, 0.3f, 0.37f), hair);
            Prim(PrimitiveType.Sphere, "Oeil G", visual, new Vector3(-0.065f, 0.61f, 0.155f), Vector3.one * 0.05f, dark);
            Prim(PrimitiveType.Sphere, "Oeil D", visual, new Vector3(0.065f, 0.61f, 0.155f), Vector3.one * 0.05f, dark);
            Prim(PrimitiveType.Cube, "Cape", visual, new Vector3(0f, -0.05f, -0.21f), new Vector3(0.42f, 0.85f, 0.03f), accent, false, new Vector3(8f, 0f, 0f));

            var glider = new GameObject("Planeur").transform;
            glider.SetParent(root.transform, false);
            glider.localPosition = new Vector3(0f, 1.9f, 0.2f);
            Prim(PrimitiveType.Cube, "Aile G", glider, new Vector3(-0.65f, 0f, 0f), new Vector3(1.3f, 0.04f, 0.7f), accent, false, new Vector3(0f, 0f, 8f));
            Prim(PrimitiveType.Cube, "Aile D", glider, new Vector3(0.65f, 0f, 0f), new Vector3(1.3f, 0.04f, 0.7f), accent, false, new Vector3(0f, 0f, -8f));
            Prim(PrimitiveType.Cube, "Armature", glider, Vector3.zero, new Vector3(0.06f, 0.06f, 0.9f), dark);
            glider.gameObject.SetActive(false);

            if (layer >= 0) SetLayerRecursively(root, layer);
            else Debug.LogWarning("[WorldGenerator] Layer 'Player' absent : ajoute-le dans Tags & Layers.");

            var pc = root.AddComponent<PlayerController>();
            pc.visual = visual;
            pc.glider = glider.gameObject;
            pc.waterLevel = SeaLevel;
            CharacterFactory.Attach(pc, layer); // replaces the placeholder primitives with the skinned hero
            return pc;
        }

        static void BuildCamera(PlayerController player, int playerLayer)
        {
            var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
            var cam = camGO.AddComponent<Camera>();
            cam.farClipPlane = 4000f;
            cam.nearClipPlane = 0.1f;
            cam.fieldOfView = 55f;
            cam.allowHDR = true;
            camGO.AddComponent<AudioListener>();
            var camData = camGO.AddComponent<UniversalAdditionalCameraData>();
            camData.renderPostProcessing = true;
            camData.antialiasing = AntialiasingMode.TemporalAntiAliasing;
            camData.antialiasingQuality = AntialiasingQuality.High;
            camData.renderShadows = true;

            var orbit = camGO.AddComponent<ThirdPersonCamera>();
            orbit.target = player.transform;
            if (playerLayer >= 0) orbit.collisionMask = ~(1 << playerLayer);
            camGO.transform.position = player.transform.position + new Vector3(0f, 2.5f, -5f);
            camGO.transform.LookAt(player.transform.position + Vector3.up * 1.6f);
            player.cameraTransform = camGO.transform;
        }

        // ================================================================ Procedural textures

        static Texture2D MakeWaterNormal()
        {
            const int n = 512;
            string path = GenDir + "/WaterNormal.png";
            var waves = new List<(int kx, int ky, float amp, float phase)>();
            var wr = new System.Random(99);
            for (int i = 0; i < 40; i++)
            {
                int kx = wr.Next(-14, 15), ky = wr.Next(-14, 15);
                if (kx == 0 && ky == 0) continue;
                float k = Mathf.Sqrt(kx * kx + ky * ky);
                waves.Add((kx, ky, 1f / Mathf.Pow(k, 1.2f), (float)wr.NextDouble() * Mathf.PI * 2f));
            }
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = 0f, dy = 0f;
                foreach (var w in waves)
                {
                    float a = 2f * Mathf.PI * (w.kx * x + w.ky * y) / n + w.phase;
                    float c = Mathf.Cos(a) * w.amp * 2f * Mathf.PI / n;
                    dx += c * w.kx;
                    dy += c * w.ky;
                }
                var normal = new Vector3(-dx * 18f, -dy * 18f, 1f).normalized;
                px[y * n + x] = new Color(normal.x * 0.5f + 0.5f, normal.y * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, true);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.textureType = TextureImporterType.NormalMap;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D MakeFoamTexture()
        {
            const int n = 512;
            string path = GenDir + "/WaterFoam.png";
            var px = new Color[n * n];
            float o = Rand(0f, 100f);
            float F(float x, float y)
            {
                float s = 0f, a = 0.5f, f = 1f, norm = 0f;
                for (int i = 0; i < 5; i++)
                {
                    s += a * Mathf.PerlinNoise((x / n * 8f + o) * f, (y / n * 8f + o) * f);
                    norm += a;
                    a *= 0.5f;
                    f *= 2f;
                }
                return s / norm;
            }
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                float val = F(x, y) * (1 - u) * (1 - v) + F(x - n, y) * u * (1 - v) + F(x, y - n) * (1 - u) * v + F(x - n, y - n) * u * v;
                val = Mathf.Clamp01((val - 0.5f) * 3f + 0.5f);
                px[y * n + x] = new Color(val, val, val, 1f);
            }
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, true, true);
            tex.SetPixels(px);
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.sRGBTexture = false;
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Texture2D SoftDotTexture()
        {
            string path = GenDir + "/SoftDot.png";
            if (File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            const int n = 64;
            var tex = new Texture2D(n, n, TextureFormat.RGBA32, false);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(n / 2f - 0.5f, n / 2f - 0.5f)) / (n / 2f);
                float a = Mathf.Pow(Mathf.Clamp01(1f - d), 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
            tex.Apply();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material ParticleMat(string name, Texture2D tex, bool additive)
        {
            var m = ArtLibrary.LoadOrCreateMaterial($"{MatDir}/{name}.mat", "Universal Render Pipeline/Particles/Unlit");
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", additive ? 2f : 0f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
            EditorUtility.SetDirty(m);
            return m;
        }

        // ================================================================ Meshes

        static Mesh WaterGrid(float size, int quads)
        {
            int w = quads + 1;
            var v = new Vector3[w * w];
            float step = size / quads, half = size * 0.5f;
            for (int z = 0; z < w; z++)
            for (int x = 0; x < w; x++)
                v[z * w + x] = new Vector3(x * step - half, 0f, z * step - half);
            var t = new int[quads * quads * 6];
            int k = 0;
            for (int z = 0; z < quads; z++)
            for (int x = 0; x < quads; x++)
            {
                int i = z * w + x;
                t[k++] = i + w; t[k++] = i + w + 1; t[k++] = i + 1;
                t[k++] = i + w; t[k++] = i + 1; t[k++] = i;
            }
            var m = new Mesh { indexFormat = IndexFormat.UInt32, vertices = v, triangles = t };
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, new Vector3(size, 10f, size));
            return m;
        }

        static Mesh WaterRing(float inner, float outer)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float[] edges = { -outer, -inner, inner, outer };
            for (int zi = 0; zi < 3; zi++)
            for (int xi = 0; xi < 3; xi++)
            {
                if (xi == 1 && zi == 1) continue;
                int s = verts.Count;
                verts.Add(new Vector3(edges[xi], 0f, edges[zi]));
                verts.Add(new Vector3(edges[xi + 1], 0f, edges[zi]));
                verts.Add(new Vector3(edges[xi], 0f, edges[zi + 1]));
                verts.Add(new Vector3(edges[xi + 1], 0f, edges[zi + 1]));
                tris.AddRange(new[] { s + 2, s + 3, s + 1, s + 2, s + 1, s });
            }
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetTriangles(tris, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        static Mesh Hemisphere(float radius, int lon, int lat)
        {
            var v = new List<Vector3>();
            var t = new List<int>();
            for (int y = 0; y <= lat; y++)
            {
                float theta = (float)y / lat * Mathf.PI * 0.5f;
                for (int x = 0; x <= lon; x++)
                {
                    float phi = (float)x / lon * Mathf.PI * 2f;
                    v.Add(new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi)) * radius);
                }
            }
            for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int i = y * (lon + 1) + x;
                t.AddRange(new[] { i, i + 1, i + lon + 1, i + 1, i + lon + 2, i + lon + 1 });
            }
            var m = new Mesh();
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.bounds = new Bounds(Vector3.zero, Vector3.one * radius * 2f);
            return m;
        }

        /// <summary>Box with UVs in world units so tiling textures keep a constant scale.</summary>
        static Mesh Box(Vector3 size, float uvPerMeter)
        {
            var h = size * 0.5f;
            var verts = new List<Vector3>();
            var norms = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            void Face(Vector3 n, Vector3 u, Vector3 v)
            {
                float hu = Mathf.Abs(Vector3.Dot(h, u)), hv = Mathf.Abs(Vector3.Dot(h, v)), hn = Mathf.Abs(Vector3.Dot(h, n));
                Vector3 c = n * hn;
                int s = verts.Count;
                verts.Add(c - u * hu - v * hv); uvs.Add(new Vector2(0f, 0f));
                verts.Add(c + u * hu - v * hv); uvs.Add(new Vector2(2f * hu * uvPerMeter, 0f));
                verts.Add(c + u * hu + v * hv); uvs.Add(new Vector2(2f * hu * uvPerMeter, 2f * hv * uvPerMeter));
                verts.Add(c - u * hu + v * hv); uvs.Add(new Vector2(0f, 2f * hv * uvPerMeter));
                for (int i = 0; i < 4; i++) norms.Add(n);
                tris.AddRange(new[] { s + 2, s + 3, s, s + 2, s, s + 1 });
            }
            foreach (var n in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                Face(n, Vector3.Cross(Vector3.up, n), Vector3.up);
            Face(Vector3.up, Vector3.left, Vector3.forward);
            Face(Vector3.down, Vector3.right, Vector3.forward);
            var m = new Mesh();
            m.SetVertices(verts);
            m.SetNormals(norms);
            m.SetUVs(0, uvs);
            m.SetTriangles(tris, 0);
            m.RecalculateTangents();
            m.RecalculateBounds();
            return m;
        }

        static GameObject BoxObj(string name, Transform parent, Vector3 localPos, Vector3 size, Material mat,
            Vector3? euler = null, float uvPerMeter = 0.45f, bool collider = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            go.AddComponent<MeshFilter>().sharedMesh = Box(size, uvPerMeter);
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            if (collider) go.AddComponent<BoxCollider>().size = size;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // ================================================================ Small helpers

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 localPos, Vector3 localScale,
            Material mat, bool keepCollider = false, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            g.name = name;
            g.transform.SetParent(parent, false);
            g.transform.localPosition = localPos;
            g.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            g.transform.localScale = localScale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            if (!keepCollider) Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }

        static Light AddPointLight(Transform parent, Color color, float range, float intensity, bool shadows)
        {
            var l = new GameObject("Lumière").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            return l;
        }

        static Material SimpleMat(string name, Color color, float smoothness, Color? emission = null)
        {
            var m = ArtLibrary.LoadOrCreateMaterial($"{MatDir}/{name}.mat", "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            EditorUtility.SetDirty(m);
            return m;
        }

        static T SaveAsset<T>(T obj, string path) where T : Object
        {
            if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        static void EnsureFolders()
        {
            foreach (var f in new[] { GenDir, MatDir, Root + "/Prefabs", Root + "/Scenes" })
                ArtLibrary.EnsureFolder(f);
        }

        static void SetLayerRecursively(GameObject go, int layer)
        {
            go.layer = layer;
            foreach (Transform child in go.transform) SetLayerRecursively(child.gameObject, layer);
        }

        static float MaxDim(GameObject prefab)
        {
            var s = ArtLibrary.PrefabSize(prefab);
            return Mathf.Max(0.01f, Mathf.Max(s.x, Mathf.Max(s.y, s.z)));
        }

        static Vector3 Ground(TerrainData data, float wx, float wz) =>
            new(wx, data.GetInterpolatedHeight(wx / WorldSize, wz / WorldSize), wz);

        // ================================================================ Noise

        static float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        static float Fbm(float x, float y, int octaves, int o)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                sum += amp * Mathf.PerlinNoise(x * freq + offsets[o].x + i * 17.3f, y * freq + offsets[o].y + i * 29.1f);
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }

        static float Ridged(float x, float y, int octaves, int o)
        {
            float sum = 0f, amp = 0.5f, freq = 1f, norm = 0f;
            for (int i = 0; i < octaves; i++)
            {
                float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x * freq + offsets[o].x + i * 11.7f, y * freq + offsets[o].y + i * 23.9f) * 2f - 1f);
                sum += amp * n * n;
                norm += amp;
                amp *= 0.5f;
                freq *= 2f;
            }
            return sum / norm;
        }
    }
}
