using System.Linq;
using UnityEngine;

namespace LastWorld.EditorTools
{
    public static partial class ChapterBuilder
    {
        static readonly Color Ember = new(1f, 0.45f, 0.12f), Tide = new(0.35f, 0.75f, 1f), Storm = new(0.7f, 0.55f, 1f), PaleWhite = new(0.9f, 0.95f, 1f);

        // ================================================================ Chapitre II — Cendrelune

        static void BuildCendrelune()
        {
            var g = Group("Cendrelune");
            var village = new Vector3(470f, 0f, 1450f);
            var forge = new Vector3(545f, 0f, 1665f);
            float villageH = HeightAt(village.x, village.z);
            // The plateau's level around the forge, measured on an untouched ring (so rebuilding never stacks the hill twice)
            float forgeH = 0f;
            for (int i = 0; i < 8; i++) forgeH += HeightAt(forge.x + Mathf.Cos(i * 0.785f) * 165f, forge.z + Mathf.Sin(i * 0.785f) * 165f) / 8f;

            // Terrain: levelled village, forge volcano with a walkable crater
            Flatten(village, 45f, 80f, villageH);
            Modify(forge, 150f, (d, old) =>
            {
                // a wide cone (about 20 degrees on average) so the climb to the crater stays a walk, not a scramble
                float cone = d <= 22f ? 30f : d <= 30f ? Mathf.Lerp(30f, 38f, Smooth(22f, 30f, d)) : 38f * (1f - Smooth(30f, 130f, d));
                return Mathf.Lerp(old, forgeH + cone, 1f - Smooth(128f, 150f, d));
            });
            Paint(village, 170f, Dirt, 0.9f);
            Paint(forge, 170f, Rock, 0.95f);
            Paint(forge, 34f, Dirt, 0.8f);
            Clear(village, 150f);
            Clear(forge, 150f);
            float craterH = forgeH + 30f;
            var crater = new Vector3(forge.x, craterH, forge.z);
            var rim = G(forge.x, forge.z - 34f);
            Sites["cendrelune"] = G(village.x, village.z);
            Sites["forge"] = crater;
            Sites["forge_rim"] = rim;

            // The burnt village
            var burnt = new Color(0.34f, 0.3f, 0.28f);
            var wall = Stone(burnt);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f + 0.3f, r = R(24f, 48f);
                var c = G(village.x + Mathf.Cos(a) * r, village.z + Mathf.Sin(a) * r);
                float yaw = R(0f, 90f), w = R(6f, 8f), d = R(5f, 6.5f);
                for (int k = 0; k < 4; k++)
                {
                    bool alongX = k % 2 == 0;
                    float off = (k < 2 ? -0.5f : 0.5f) * (alongX ? d : w);
                    var local = alongX ? new Vector3(0f, 0f, off) : new Vector3(off, 0f, 0f);
                    var pos = c + Quaternion.Euler(0f, yaw, 0f) * local;
                    if (rng.NextDouble() < 0.18) continue; // a collapsed side
                    RuinWall(g, pos, yaw + (alongX ? 0f : 90f), alongX ? w : d, R(1.2f, 3.2f), wall);
                }
                Block(g, c + Vector3.up * 2.4f, new Vector3(0.3f, 0.3f, w * 0.9f), yaw + R(-8f, 8f), Planks(new Color(0.22f, 0.17f, 0.14f)), R(8f, 25f), 2f);
            }
            // Well
            var wellC = G(village.x + 6f, village.z - 4f);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * 360f;
                Block(g, wellC + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 1.1f + Vector3.up * 0.5f, new Vector3(0.9f, 1f, 0.7f), a, wall, 0f, 1.5f);
            }
            Scatter(g, "dead_tree_trunk", village, 20f, 120f, 18, 0.8f, 1.5f, new Color(0.35f, 0.3f, 0.28f));
            Scatter(g, "rock_07", village, 20f, 120f, 14, 1.2f, 3f, new Color(0.45f, 0.4f, 0.38f));
            Scatter(g, "wooden_crate_01", village, 8f, 50f, 7, 0.8f, 1.1f, new Color(0.3f, 0.25f, 0.22f));
            Scatter(g, "wooden_barrels_01", village, 8f, 50f, 5, 0.8f, 1.1f, new Color(0.3f, 0.25f, 0.22f));
            var hearth = G(village.x - 3f, village.z + 8f);
            Place(g, "stone_fire_pit", hearth, 0f, 1.1f);
            PointLight(g, hearth + Vector3.up * 1f, new Color(1f, 0.6f, 0.3f), 14f, 2.5f);
            Npc(g, "Elara", "maren", "Maren", "Dernière habitante de Cendrelune", hearth + new Vector3(1.8f, 0f, 0.6f), hearth, "Idle", new Color(0.55f, 0.45f, 0.4f), 0.95f);
            Marker("cendrelune", Sites["cendrelune"], g);
            AddWaypoint(g, "wp_cendrelune", "Sentinelle de Cendrelune", G(village.x + 24f, village.z - 36f), 200f);
            Camp(g, "camp_cendre", "Brûlés de Cendrelune", G(510f, 1560f), new[] { golemBrasier, slime[(int)Element.Pyro] }, 5, 8f, 7, true);
            for (int i = 0; i < 3; i++) Chest(g, $"cendre_{i}", 1, G(village.x + R(-60f, 60f), village.z + R(-60f, 60f)), R(0f, 360f));
            Camp(g, "camp_cendre_wild", "Slimes de braise", G(380f, 1560f), new[] { slime[(int)Element.Pyro], slime[(int)Element.Pyro], golemBrasier }, 4, 7f, 6, false, false);

            // The Forge
            var fg = Group("Forge-Volcan", g);
            var dark = Stone(new Color(0.28f, 0.24f, 0.22f));
            var lava = Glow("Lave", new Color(1f, 0.35f, 0.05f), 2.5f);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * 360f + 30f;
                var p = crater + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 20f;
                Block(fg, p + Vector3.up * 3f, new Vector3(1.4f, 6f, 1.4f), a, dark, 0f, 2f);
                var ember = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ember.transform.SetParent(fg, false);
                ember.transform.position = p + Vector3.up * 6.4f;
                ember.transform.localScale = Vector3.one * 0.9f;
                ember.GetComponent<Renderer>().sharedMaterial = lava;
                UnityEngine.Object.DestroyImmediate(ember.GetComponent<Collider>());
                PointLight(fg, p + Vector3.up * 6.4f, Ember, 12f, 2.2f);
            }
            for (int i = 0; i < 9; i++) // glowing cracks in the crater floor
            {
                float a = i / 9f * 360f + R(-10f, 10f);
                var p = crater + Quaternion.Euler(0f, a, 0f) * Vector3.forward * R(6f, 14f);
                Block(fg, p + Vector3.up * 0.06f, new Vector3(0.5f, 0.06f, R(6f, 10f)), a, lava, 0f, 4f, false);
            }
            for (int i = 0; i < 7; i++) // lava pools on the slopes
            {
                float a = R(0f, 360f), r = R(42f, 80f);
                var p = G(forge.x + Mathf.Cos(a * Mathf.Deg2Rad) * r, forge.z + Mathf.Sin(a * Mathf.Deg2Rad) * r);
                var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                pool.transform.SetParent(fg, false);
                pool.transform.position = p + Vector3.up * 0.04f;
                pool.transform.localScale = new Vector3(R(3f, 6f), 0.04f, R(3f, 6f));
                pool.GetComponent<Renderer>().sharedMaterial = lava;
                UnityEngine.Object.DestroyImmediate(pool.GetComponent<Collider>());
                PointLight(fg, p + Vector3.up * 1.2f, Ember, 11f, 2f);
            }
            Scatter(fg, "boulder_01", crater, 30f, 38f, 12, 2f, 3.4f, new Color(0.3f, 0.26f, 0.24f));
            Scatter(fg, "rock_09", forge, 40f, 85f, 16, 1.5f, 3.5f, new Color(0.34f, 0.3f, 0.28f));
            Block(fg, crater + new Vector3(0f, 0.7f, 12f), new Vector3(3.2f, 1.4f, 1.8f), 0f, dark, 0f, 2f); // the anvil
            Chest(fg, "forge_fragment", 3, crater + new Vector3(0f, 1.4f, 12f), 180f);
            Marker("forge", rim, fg);
            Boss(fg, "boss_pyro", ContentBuilder.GolemPrefab(true, "Boss_Braise", "Colosse de Braise", Element.Pyro, new Color(1f, 0.42f, 0.08f), new Color(0.3f, 0.25f, 0.23f), 1f, 1.25f, 1.1f, "boss_pyro", "Colosse de Braise — Gardien de la Forge"),
                crater + new Vector3(0f, 0f, -4f), 9, new[] { slime[(int)Element.Pyro] });

            Zone(g, G(village.x + 40f, village.z + 110f), 340f, "Hauts de Cendrelune", new Color(0.62f, 0.36f, 0.26f), 0.55f, 1.8f, new Color(1f, 0.82f, 0.7f), RegionWeather.Embers, 35f);
            Circle(g, "veille_pyro", G(400f, 1330f), Ember, ContentBuilder.GolemPrefab(true, "Boss_Brasier", "Brasier-Veilleur", Element.Pyro, new Color(1f, 0.45f, 0.1f), new Color(0.3f, 0.25f, 0.23f), 0.85f, 0.9f, 0.95f, "boss_v1", "Veille de braise"), "boss_v1", 15);
        }

        // ================================================================ Chapitre III — Ysmère

        static void BuildYsmere()
        {
            var g = Group("Ysmère");
            var L = new Vector3(365f, 0f, 930f);
            // The lagoon: a basin open to the western sea, with the Archives on an island in the middle
            Modify(L, 155f, (d, old) => Mathf.Min(old, Mathf.Lerp(29f, old, Smooth(85f, 155f, d))));
            Modify(L, 40f, (d, old) => Mathf.Lerp(old, 38.5f, 1f - Smooth(26f, 40f, d)));
            Paint(L, 185f, Sand, 0.9f);
            Paint(L, 30f, Rock, 0.9f);
            Clear(L, 170f);
            var island = G(L.x, L.z);
            Sites["ysmere"] = island;
            Sites["archives"] = island;

            var wall = Stone(new Color(0.86f, 0.87f, 0.87f));
            var shelves = Planks(new Color(0.55f, 0.5f, 0.45f));
            RuinWall(g, island + new Vector3(0f, 0.3f, 16.5f), 0f, 34f, 8f, wall);
            RuinWall(g, island + new Vector3(-16.5f, 0.3f, 3f), 90f, 20f, 6f, wall);
            RuinWall(g, island + new Vector3(16.5f, 0.3f, 8f), 90f, 12f, 4f, wall);
            for (int i = 0; i < 8; i++)
            {
                float x = -14f + i * 4f;
                Block(g, island + new Vector3(x, 3.6f, -14f), new Vector3(1.3f, R(4f, 7f), 1.3f), 0f, wall, 0f, 2f);
            }
            for (int i = 0; i < 6; i++)
                Block(g, island + new Vector3(-13f + i * 2.2f, 1.4f, 14.6f), new Vector3(1.8f, 2.4f, 0.6f), 0f, shelves, 0f, 1.5f);
            var lectern = island + new Vector3(0f, 0f, 6f);
            Block(g, lectern + Vector3.up * 0.8f, new Vector3(1.4f, 1.6f, 1.0f), 0f, wall, 0f, 1.5f);
            var book = Block(g, lectern + Vector3.up * 1.7f, new Vector3(1.2f, 0.12f, 0.8f), 0f, Glow("Livre", Tide, 2.2f), -18f, 1f, false);
            PointLight(g, lectern + Vector3.up * 2.2f, Tide, 10f, 2f);
            Npc(g, "Nerys", "sael", "Sael", "Archiviste d'Ysmère", island + new Vector3(-5f, 0.3f, 4f), lectern, "FoldArms", new Color(0.7f, 0.78f, 0.85f), 1f);
            // Causeway of stone slabs from the island to the eastern shore
            for (int i = 0; i < 17; i++)
            {
                float x = 30f + i * 5f;
                Block(g, new Vector3(L.x + x, 34.1f, L.z), new Vector3(4.6f, 0.6f, 4f), 0f, wall, 0f, 2f);
            }
            Chest(g, "lagoon_fragment", 3, island + new Vector3(0f, 0.35f, 12f), 180f);
            Boss(g, "boss_hydro", ContentBuilder.GolemPrefab(true, "Boss_Bassin", "Sentinelle du Bassin", Element.Hydro, new Color(0.3f, 0.7f, 1f), new Color(0.6f, 0.75f, 0.9f), 1.05f, 1.45f, 1.15f, "boss_hydro", "Sentinelle du Bassin — Veilleur de la Lagune"),
                island + new Vector3(0f, 0.35f, -4f), 10, new[] { slime[(int)Element.Hydro] });
            Marker("ysmere", G(L.x + 112f, L.z), g);

            // Shore: Memory Stones, the tide guardians' camp, the waystone
            for (int i = 0; i < 3; i++)
            {
                float a = (-55f + i * 55f) * Mathf.Deg2Rad;
                var p = G(L.x + Mathf.Cos(a) * 128f, L.z + Mathf.Sin(a) * 128f + (i == 1 ? 14f : 0f));
                Prop(g, $"mem_{i}", p, Tide, "Toucher la Pierre de Mémoire", "memory_stone", $"mem{i + 1}", "main3", 4);
            }
            Camp(g, "camp_marees", "Veilleurs des marées", G(L.x + 110f, L.z - 90f), new[] { golemMarees, slime[(int)Element.Hydro] }, 5, 8f, 9, true);
            AddWaypoint(g, "wp_ysmere", "Sentinelle d'Ysmère", G(L.x + 140f, L.z - 36f), 270f);
            Scatter(g, "rock_moss_set_01", L, 60f, 170f, 14, 1.2f, 2.8f, new Color(0.8f, 0.88f, 0.95f));
            Scatter(g, "dead_tree_trunk", L, 100f, 190f, 8, 0.8f, 1.3f);
            Zone(g, G(L.x + 20f, L.z), 330f, "Lagune d'Ysmère", new Color(0.42f, 0.6f, 0.72f), 0.5f, 1.6f, new Color(0.85f, 0.95f, 1.05f), RegionWeather.Rain, 90f);
            Circle(g, "veille_hydro", G(445f, 790f), Tide, ContentBuilder.GolemPrefab(true, "Boss_VeilleurMarees", "Veilleur des Marées", Element.Hydro, new Color(0.3f, 0.7f, 1f), new Color(0.6f, 0.75f, 0.9f), 0.85f, 0.9f, 0.95f, "boss_v2", "Veille de marée"), "boss_v2", 15);
        }

        // ================================================================ Chapitre IV — Valdrak

        static void BuildValdrak()
        {
            var g = Group("Valdrak");
            var V = new Vector3(1540f, 0f, 1010f);
            float h0 = 146f;
            Flatten(V, 54f, 90f, h0);
            Road(new Vector3(1270f, 0f, 1040f), new Vector3(1486f, 0f, 1012f), 11f, HeightAt(1270f, 1040f), h0);
            Paint(V, 130f, Rock, 0.95f);
            Paint(V + new Vector3(60f, 0f, 30f), 55f, Snow, 0.9f);
            Paint(V + new Vector3(-30f, 0f, -70f), 40f, Snow, 0.8f);
            Clear(V, 110f);
            var c = new Vector3(V.x, h0, V.z);
            Sites["valdrak"] = c;
            Sites["citadel"] = G(V.x - 40f, V.z);

            // Citadel of the Order of the Vigil
            var wall = Stone(new Color(0.74f, 0.72f, 0.76f));
            for (int i = 0; i < 24; i++)
            {
                float a = i * 15f;
                if (Mathf.Abs(Mathf.DeltaAngle(a, 180f)) < 8f) continue; // the gate
                var p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 45f;
                Block(g, p + Vector3.up * 3.5f, new Vector3(2.2f, 7f, 11.7f), a + 90f, wall, 0f, 3f);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                Block(g, c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 45f + Vector3.up * 7f, new Vector3(7f, 14f, 7f), a, wall, 0f, 3f);
            }
            foreach (float dz in new[] { -8.5f, 8.5f })
                Block(g, c + new Vector3(-45f, 6f, dz), new Vector3(3f, 12f, 3f), 0f, wall, 0f, 3f);
            Block(g, c + new Vector3(-45f, 12.4f, 0f), new Vector3(3.4f, 1.4f, 20f), 0f, wall, 0f, 3f);
            var steel = Glow("Foudre", Storm, 2.8f);
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f + 30f;
                var p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 24f;
                Block(g, p + Vector3.up * 7f, new Vector3(0.5f, 14f, 0.5f), a, Stone(new Color(0.45f, 0.45f, 0.55f)), 0f, 2f);
                var tip = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                tip.transform.SetParent(g, false);
                tip.transform.position = p + Vector3.up * 14.3f;
                tip.transform.localScale = Vector3.one * 0.8f;
                tip.GetComponent<Renderer>().sharedMaterial = steel;
                UnityEngine.Object.DestroyImmediate(tip.GetComponent<Collider>());
                PointLight(g, p + Vector3.up * 14f, Storm, 14f, 2.4f);
            }
            Chest(g, "storm_fragment", 3, c + new Vector3(26f, 0.3f, -6f), 270f);
            Boss(g, "boss_electro", ContentBuilder.GolemPrefab(true, "Boss_Orage", "Colosse d'Orage", Element.Electro, new Color(0.75f, 0.5f, 1f), new Color(0.5f, 0.45f, 0.7f), 1.1f, 1.7f, 1.25f, "boss_electro", "Colosse d'Orage — Sentinelle de Valdrak"),
                c + new Vector3(0f, 0.3f, 6f), 12, new[] { slime[(int)Element.Electro] });

            // The approach: Orrin at the gate, the three flames of the Vigil along the road, the guardians' camp
            Npc(g, "Torvan", "orrin", "Orrin", "Dernier Veilleur", G(V.x - 62f, V.z + 7f), c, "FoldArms", new Color(0.7f, 0.7f, 0.78f), 0.98f);
            for (int i = 0; i < 3; i++)
            {
                float x = V.x - 190f + i * 62f;
                var p = G(x, Mathf.Lerp(1034f, 1012f, i / 2f) + 7f);
                Prop(g, $"vigil_{i}", p, new Color(0.65f, 0.85f, 1f), "Allumer le flambeau", "vigil_flame", $"vigil{i}", "main4", 3, true);
            }
            Camp(g, "camp_orage", "Gardiens de la citadelle", G(V.x - 105f, V.z + 14f), new[] { golemOrage, slime[(int)Element.Electro] }, 5, 8f, 11, true);
            Marker("valdrak", G(1290f, 1038f), g);
            Marker("citadel", Sites["citadel"], g);
            AddWaypoint(g, "wp_valdrak", "Sentinelle de Valdrak", G(1310f, 1052f), 90f);
            Scatter(g, "rock_face_01", V, 60f, 140f, 12, 1.5f, 3f, new Color(0.6f, 0.6f, 0.7f));
            Scatter(g, "boulder_01", V, 50f, 140f, 14, 1.5f, 3f, new Color(0.6f, 0.6f, 0.7f));
            Zone(g, V + new Vector3(-80f, 0f, 0f), 340f, "Pics de Valdrak", new Color(0.5f, 0.52f, 0.66f), 0.5f, 1.8f, new Color(0.82f, 0.84f, 1f), RegionWeather.Snow, 80f);
            Circle(g, "veille_electro", G(1672f, 1165f), Storm, ContentBuilder.GolemPrefab(true, "Boss_VeilleurOrage", "Veilleur d'Orage", Element.Electro, new Color(0.75f, 0.5f, 1f), new Color(0.5f, 0.45f, 0.7f), 0.85f, 0.9f, 0.95f, "boss_v3", "Veille d'orage"), "boss_v3", 15);
        }

        /// <summary>A Vigil circle (Chapter VII): a levelled ring of glowing pillars guarded by a minor boss.</summary>
        static void Circle(Transform parent, string markerId, Vector3 center, Color color, GameObject boss, string bossId, int level)
        {
            Flatten(center, 18f, 30f, center.y);
            Clear(center, 26f);
            var g = Group($"Cercle — {markerId}", parent);
            var stone = Stone(new Color(0.5f, 0.5f, 0.55f));
            var glow = Glow($"Cercle_{ColorUtility.ToHtmlStringRGB(color)}", color, 2.5f);
            var c = G(center.x, center.z);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                var p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 15f;
                Block(g, p + Vector3.up * 2f, new Vector3(1.2f, 4f, 1.2f), a, stone, 0f, 2f);
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.transform.SetParent(g, false);
                orb.transform.position = p + Vector3.up * 4.5f;
                orb.transform.localScale = Vector3.one * 0.7f;
                orb.GetComponent<Renderer>().sharedMaterial = glow;
                UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>());
                PointLight(g, p + Vector3.up * 4.5f, color, 9f, 2f);
            }
            Marker(markerId, c, g);
            Boss(g, bossId, boss, c, level);
        }

        // ================================================================ Chapitre V — Les Terres Pâles

        static void BuildTerresPales()
        {
            var g = Group("Terres Pâles");
            var P = new Vector3(1010f, 0f, 1775f);
            Flatten(P, 70f, 115f, 42f);
            Paint(P, 230f, Snow, 0.95f);
            Paint(P, 60f, Sand, 0.5f);
            Clear(P, 210f);
            var refuge = new Vector3(880f, 0f, 1745f);
            Modify(refuge, 24f, (d, old) => Mathf.Lerp(old, 44f, 1f - Smooth(8f, 24f, d)));
            var pale = new Color(0.9f, 0.93f, 1f);
            var ruin = Stone(new Color(0.85f, 0.88f, 0.95f));
            Sites["terres_pales"] = G(P.x, P.z - 110f);
            Sites["refuge"] = G(refuge.x, refuge.z);
            for (int i = 0; i < 4; i++) // drowned fishermen's huts
            {
                float a = i * 90f + 20f;
                var c = G(P.x + Mathf.Cos(a * Mathf.Deg2Rad) * 80f, P.z - 20f + Mathf.Sin(a * Mathf.Deg2Rad) * 60f);
                RuinWall(g, c, a, 6f, R(1.5f, 3f), ruin);
                RuinWall(g, c + new Vector3(3f, 0f, 3f), a + 90f, 6f, R(1f, 2.5f), ruin);
            }
            Scatter(g, "dead_tree_trunk", P, 20f, 200f, 24, 0.9f, 1.6f, pale);
            Scatter(g, "rock_07", P, 20f, 200f, 20, 1.2f, 3.2f, pale);
            Scatter(g, "wicker_basket_01", P, 20f, 120f, 6, 0.9f, 1.1f, pale);
            Scatter(g, "tree_stump_01", P, 20f, 180f, 8, 0.9f, 1.4f, pale);

            // The refuge: a hurricane lantern held by Ilio
            var rp = G(refuge.x, refuge.z);
            var post = Block(g, rp + Vector3.up * 1.6f, new Vector3(0.18f, 3.2f, 0.18f), 0f, Planks(new Color(0.6f, 0.55f, 0.5f)), 0f, 1f);
            var lamp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            lamp.transform.SetParent(g, false);
            lamp.transform.position = rp + Vector3.up * 3.3f;
            lamp.transform.localScale = Vector3.one * 0.55f;
            lamp.GetComponent<Renderer>().sharedMaterial = Glow("Lanterne", new Color(1f, 0.82f, 0.45f), 3f);
            UnityEngine.Object.DestroyImmediate(lamp.GetComponent<Collider>());
            PointLight(g, rp + Vector3.up * 3.3f, new Color(1f, 0.8f, 0.45f), 24f, 3.2f);
            var ilioRefuge = Npc(g, "Bram", "ilio", "Ilio", "Enfant de la lanterne", rp + new Vector3(1.8f, 0f, 0.8f), rp, "Idle", new Color(0.55f, 0.7f, 0.9f), 0.62f);
            Prop(g, "kael_journal", rp + new Vector3(-3.5f, 0f, 2.5f), new Color(1f, 0.9f, 0.6f), "Lire le carnet", "kael_journal", "kael_journal", "main5", 5);
            Marker("refuge", rp, g);
            Marker("terres_pales", Sites["terres_pales"], g);
            Camp(g, "camp_pale", "Échos de la Pâleur", G(P.x + 40f, P.z - 80f), new[] { golemPale, slimePale, slimePale }, 6, 9f, 13, true);
            Boss(g, "boss_pale", ContentBuilder.GolemPrefab(true, "Boss_Echo", "Écho d'un monde perdu", Element.Cryo, new Color(1.2f, 1.2f, 1.5f), new Color(0.92f, 0.94f, 1f), 1.2f, 1.9f, 1.3f, "boss_pale", "Écho d'un monde perdu"),
                rp + new Vector3(14f, 0f, -10f), 14, new[] { slimePale });
            AddWaypoint(g, "wp_pale", "Sentinelle du Rivage Pâle", G(P.x - 40f, P.z - 130f), 0f);
            Zone(g, P, 340f, "Terres Pâles", new Color(0.92f, 0.94f, 1f), 0.85f, 3f, new Color(1.15f, 1.15f, 1.2f), RegionWeather.Pale, 60f);
            // Ilio only waits here until the chapter is over (he then joins the camp)
            var gate = g.gameObject.AddComponent<ChapterGate>();
            gate.showFrom = 0;
            gate.hideFrom = 5;
            gate.targets = new[] { ilioRefuge };
        }

        // ================================================================ Chapitres VIII & X — La Tour du Sceau

        static void BuildTour()
        {
            var g = Group("Tour du Sceau");
            var T = new Vector3(1060f, 0f, 1020f);
            Flatten(T, 100f, 160f, 62f);
            Paint(T, 150f, Rock, 0.85f);
            Paint(T, 90f, Dirt, 0.5f);
            Clear(T, 140f);
            float y0 = 62f, top = y0 + 45f;
            var stone = Stone(new Color(0.74f, 0.76f, 0.82f));
            var trim = Stone(new Color(0.58f, 0.61f, 0.68f));
            var c = new Vector3(T.x, top, T.z);
            Sites["tour"] = new Vector3(T.x, y0, T.z);
            Sites["sceau_summit"] = c;

            float[] sizes = { 66f, 60f, 56f };
            for (int i = 0; i < 3; i++)
            {
                Block(g, new Vector3(T.x, y0 + 7.5f + i * 15f, T.z), new Vector3(sizes[i], 15f, sizes[i]), 0f, i == 1 ? trim : stone, 0f, 4f);
                for (int k = 0; k < 4; k++)
                {
                    float a = 45f + k * 90f;
                    var p = new Vector3(T.x, y0 + 15f + i * 15f + 2.5f, T.z) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * (sizes[i] * 0.5f * 1.3f - 3f);
                    Block(g, p, new Vector3(3f, 5f, 3f), a, trim, 0f, 2f);
                }
            }
            // Parapet around the summit, open to the west where the bridge lands
            float half = 28f;
            Block(g, c + new Vector3(0f, 0.6f, half - 0.4f), new Vector3(56f, 1.2f, 0.8f), 0f, trim, 0f, 2f);
            Block(g, c + new Vector3(0f, 0.6f, -half + 0.4f), new Vector3(56f, 1.2f, 0.8f), 0f, trim, 0f, 2f);
            Block(g, c + new Vector3(half - 0.4f, 0.6f, 0f), new Vector3(0.8f, 1.2f, 56f), 0f, trim, 0f, 2f);
            Block(g, c + new Vector3(-half + 0.4f, 0.6f, -17f), new Vector3(0.8f, 1.2f, 22f), 0f, trim, 0f, 2f);
            Block(g, c + new Vector3(-half + 0.4f, 0.6f, 17f), new Vector3(0.8f, 1.2f, 22f), 0f, trim, 0f, 2f);

            // The Heart of the Seal: a ring of light around a broken crystal
            var light = new Color(0.7f, 0.95f, 1f);
            var glow = Glow("CoeurSceau", light, 2.8f);
            for (int i = 0; i < 16; i++)
            {
                float a = i * 22.5f;
                var p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 7f;
                Block(g, p + Vector3.up * 0.15f, new Vector3(1.2f, 0.3f, 2.4f), a + 90f, glow, 0f, 2f, false);
            }
            for (int i = 0; i < 4; i++)
            {
                float a = 45f + i * 90f;
                var p = c + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 22f;
                Block(g, p + Vector3.up * 4f, new Vector3(1.4f, 8f, 1.4f), a, trim, 0f, 2f);
                PointLight(g, p + Vector3.up * 8.5f, light, 16f, 2.4f);
            }
            var seal = Prop(g, "seal_heart", c, light, "Toucher le Cœur du Sceau", "seal_heart", "c10_choice", "main10", 3);
            seal.glowColor = light;

            // Spiral ramp around the tower: 1.15 turns, from the south, up to the bridge on the summit
            int N = 100;
            float R0 = 58f, startA = -90f, turns = 0.75f; // south → east → north → ends on the west side, facing the parapet's opening
            float step = turns * 360f / N;
            for (int i = 0; i < N; i++)
            {
                float a0 = startA + i * step, a1 = a0 + step, am = (a0 + a1) * 0.5f;
                float h0 = y0 + 0.3f + 45f * i / N, h1 = y0 + 0.3f + 45f * (i + 1) / N;
                Vector3 p0 = new(T.x + Mathf.Cos(a0 * Mathf.Deg2Rad) * R0, h0, T.z + Mathf.Sin(a0 * Mathf.Deg2Rad) * R0);
                Vector3 p1 = new(T.x + Mathf.Cos(a1 * Mathf.Deg2Rad) * R0, h1, T.z + Mathf.Sin(a1 * Mathf.Deg2Rad) * R0);
                Vector3 mid = (p0 + p1) * 0.5f, dir = p1 - p0;
                float len = new Vector2(dir.x, dir.z).magnitude;
                float yaw = Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, pitch = -Mathf.Atan2(dir.y, len) * Mathf.Rad2Deg;
                Block(g, mid - Vector3.up * 0.5f, new Vector3(9f, 1f, len + 0.6f), yaw, trim, pitch, 3f);
                if (i % 3 == 0)
                {
                    float ground = HeightAt(mid.x, mid.z);
                    float height = mid.y - 1f - ground;
                    if (height > 0.5f) Block(g, new Vector3(mid.x, ground + height * 0.5f, mid.z), new Vector3(2.6f, height, 2.6f), yaw, stone, 0f, 3f);
                }
            }
            float aEnd = startA + turns * 360f;
            Vector3 end = new(T.x + Mathf.Cos(aEnd * Mathf.Deg2Rad) * R0, top, T.z + Mathf.Sin(aEnd * Mathf.Deg2Rad) * R0);
            Vector3 toCenter = new Vector3(T.x - end.x, 0f, T.z - end.z);
            float bridgeLen = toCenter.magnitude - 27f;
            Block(g, end + toCenter.normalized * (bridgeLen * 0.5f) - Vector3.up * 0.5f, new Vector3(9f, 1f, bridgeLen + 2f), Mathf.Atan2(toCenter.x, toCenter.z) * Mathf.Rad2Deg, trim, 0f, 3f);
            Sites["sceau_base"] = G(T.x, T.z - 72f);
            Marker("sceau_base", Sites["sceau_base"], g);
            Marker("sceau_summit", c, g);
            Camp(g, "camp_sceau", "Gardes de la spirale", G(T.x + 20f, T.z - 88f), new[] { golemPale, slimePale, slime[(int)Element.Cryo] }, 6, 10f, 15, true);
            Boss(g, "boss_kael", ContentBuilder.DuelistPrefab("Boss_Kael", "Kael", Element.Electro, 7200f, 190f, "boss_kael", "Kael — le frère d'Aelith", true),
                c + new Vector3(0f, 0f, 9f), 17);
            Boss(g, "boss_final", ContentBuilder.GolemPrefab(true, "Boss_Effacement", "L'Effacement", Element.Cryo, new Color(1.3f, 1.3f, 1.6f), new Color(0.95f, 0.96f, 1.05f), 1.45f, 2.4f, 1.5f, "boss_final", "L'Effacement — le visage de la Pâleur"),
                c + new Vector3(4f, 0f, 8f), 19, new[] { slimePale, golemPale });
            AddWaypoint(g, "wp_tour", "Sentinelle de la Tour du Sceau", G(T.x - 20f, T.z - 105f), 0f);
            Zone(g, new Vector3(T.x, y0, T.z), 220f, "Tour du Sceau", new Color(0.75f, 0.85f, 0.95f), 0.4f, 1.3f, new Color(1f, 1f, 1.08f), RegionWeather.Motes, 40f);
        }

        // ================================================================ Chapitre VI — Le Sanctuaire des Quatre Vents

        static void BuildSanctuary()
        {
            var g = Group("Sanctuaire des Quatre Vents");
            var S = new Vector3(1024f, 700f, -440f); // far above the sea, off the south coast
            float top = S.y + 0.75f;
            var marble = Stone(new Color(0.8f, 0.82f, 0.86f));
            var trim = Stone(new Color(0.66f, 0.72f, 0.76f));
            Block(g, S, new Vector3(72f, 1.5f, 72f), 0f, marble, 0f, 4f);
            var ring = Glow("SanctuaireAnneau", new Color(0.55f, 1f, 0.9f), 2.2f);
            Block(g, new Vector3(S.x, top + 0.03f, S.z), new Vector3(40f, 0.06f, 1.2f), 0f, ring, 0f, 3f, false);
            Block(g, new Vector3(S.x, top + 0.03f, S.z), new Vector3(1.2f, 0.06f, 40f), 0f, ring, 0f, 3f, false);
            foreach (float s in new[] { -35.4f, 35.4f })
            {
                Block(g, new Vector3(S.x, top + 0.7f, S.z + s), new Vector3(72f, 1.4f, 1.2f), 0f, trim, 0f, 3f);
                Block(g, new Vector3(S.x + s, top + 0.7f, S.z), new Vector3(1.2f, 1.4f, 72f), 0f, trim, 0f, 3f);
            }
            for (int i = 0; i < 12; i++)
            {
                float a = i * 30f;
                var p = new Vector3(S.x, top, S.z) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 29f;
                Block(g, p + Vector3.up * 4.5f, new Vector3(1.6f, 9f, 1.6f), a, trim, 0f, 2f);
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                orb.transform.SetParent(g, false);
                orb.transform.position = p + Vector3.up * 9.6f;
                orb.transform.localScale = Vector3.one * 0.8f;
                orb.GetComponent<Renderer>().sharedMaterial = ring;
                UnityEngine.Object.DestroyImmediate(orb.GetComponent<Collider>());
                PointLight(g, p + Vector3.up * 9.6f, new Color(0.6f, 1f, 0.9f), 18f, 2.2f);
            }
            Color[] fr = { Elements.Color(Element.Anemo), new Color(1f, 0.45f, 0.15f), Tide, Storm };
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var p = new Vector3(S.x, top + 3.6f, S.z) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 4.5f;
                var shard = Block(g, p, new Vector3(0.6f, 1.6f, 0.6f), a, Glow($"Fragment_{i}", fr[i], 3f), 0f, 1f, false);
                shard.transform.rotation = Quaternion.Euler(20f, a, 25f);
                PointLight(g, p, fr[i], 10f, 2f);
            }
            var mark = Marker("SanctuaryMark", new Vector3(S.x, top, S.z - 22f), g);
            Sites["sanctuaire"] = new Vector3(S.x, top, S.z);
            Sites["sanctuaire_mark"] = mark.transform.position;
            Color[] sc = { new Color(1f, 0.5f, 0.2f), Tide, Storm };
            string[] scenes = { "spirit_cendrel", "spirit_ysme", "spirit_valdrak" };
            for (int i = 0; i < 3; i++)
            {
                float a = 90f + i * 120f;
                var p = new Vector3(S.x, top, S.z) + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 18f;
                Prop(g, $"spirit_{i}", p, sc[i], "Écouter le Gardien", "spirit", scenes[i], "main6", 2);
            }
            Prop(g, "sanctuary_core", new Vector3(S.x, top, S.z), new Color(1f, 1f, 1f), "Toucher le Cœur du Sanctuaire", "sanctuary_core", "c6_core", "main6", 4);
            Boss(g, "boss_shade", ContentBuilder.DuelistPrefab("Boss_Ombre", "Ombre d'Aelith", Element.Anemo, 6000f, 150f, "boss_shade", "L'Ombre d'Aelith", false, new Color(0.3f, 0.28f, 0.5f)),
                new Vector3(S.x, top, S.z + 9f), 14);
            Zone(g, new Vector3(S.x, top, S.z), 95f, "Sanctuaire des Quatre Vents", new Color(0.7f, 0.95f, 0.92f), 0.7f, 1.0f, new Color(1.1f, 1.2f, 1.2f), RegionWeather.Motes, 70f);
        }

        // ================================================================ Chapitre IX — Le camp assiégé

        static void BuildSiege(Vector3 spawn)
        {
            var g = Group("Siège du camp");
            var camp = GameObject.Find("Camp de départ");
            Vector3 c = camp ? camp.transform.position : spawn;
            var altar = GameObject.Find("Autel des Vents");
            Vector3 center = altar ? altar.transform.position : c;
            int[] counts = { 5, 6, 7 };
            for (int i = 0; i < 3; i++)
            {
                float a = (40f + i * 120f) * Mathf.Deg2Rad;
                var p = G(center.x + Mathf.Cos(a) * 44f, center.z + Mathf.Sin(a) * 44f);
                Camp(g, $"camp_siege_{i}", "Vague de la Pâleur", p, new[] { slimePale, golemPale, slime[(int)Element.Cryo] }, counts[i], 8f, 15 + i, true);
            }
            string[] scenes = { "veillee_kaira", "veillee_nerys", "veillee_torvan", "veillee_elara" };
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                var p = G(center.x + Mathf.Cos(a) * 12f, center.z + Mathf.Sin(a) * 12f);
                Prop(g, $"brazier_{i}", p, new Color(1f, 0.7f, 0.35f), "Veiller auprès du brasero", "brazier", scenes[i], "main9", 4, true);
            }
            // The pale tide closes in on the camp once Chapter VIII is over
            var zoneHolder = Group("Marée blanche", g);
            Zone(zoneHolder, center, 230f, "Le camp assiégé", new Color(0.9f, 0.93f, 1f), 0.65f, 2.4f, new Color(1.1f, 1.1f, 1.15f), RegionWeather.Pale, 70f);
            var gate = g.gameObject.AddComponent<ChapterGate>();
            gate.showFrom = 8;
            gate.hideFrom = 99;
            gate.targets = new[] { zoneHolder.gameObject };
            zoneHolder.gameObject.SetActive(false);
            Sites["camp"] = center;
        }

        // ================================================================ Companions at the camp

        static void BuildCompanionsAndGates(Vector3 spawn)
        {
            var camp = GameObject.Find("Camp de départ");
            Vector3 c = camp ? camp.transform.position : spawn;
            var g = Group("Au camp");
            var ilio = Npc(g, "Bram", "ilio_camp", "Ilio", "Petit frère de Kaïra", G(c.x + 3.5f, c.z - 8f), G(c.x, c.z - 9.5f), "Idle", new Color(0.55f, 0.7f, 0.9f), 0.62f);
            var gate = g.gameObject.AddComponent<ChapterGate>();
            gate.showFrom = 5;
            gate.hideFrom = 99;
            gate.targets = new[] { ilio };
            ilio.SetActive(false);
        }
    }
}
