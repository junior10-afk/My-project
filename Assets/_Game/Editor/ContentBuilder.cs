using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Adds the gameplay layer to the generated world scene: the 4-character party (MPFB models,
    /// swords, combat, interaction), game systems (UI, quests, inventory, save, audio, FX), NPCs,
    /// interactive altar/waypoints, chests, wind orbs, enemy prefabs and camps, the boss arena and
    /// the world map texture. Menu: Last World/Build Gameplay Content.
    /// </summary>
    public static class ContentBuilder
    {
        const string PrefabDir = "Assets/_Game/Prefabs/Gameplay";
        const string MatDir = "Assets/_Game/Materials/Gameplay";
        const string GenDir = "Assets/_Game/Generated";
        const float SeaLevel = 0.08f * 420f;

        static System.Random rng;
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        [MenuItem("Last World/Build Gameplay Content")]
        public static void Build()
        {
            rng = new System.Random(777);
            ArtLibrary.EnsureFolder(PrefabDir);
            ArtLibrary.EnsureFolder(MatDir);
            CharacterImporter.ImportAll();

            var terrain = Object.FindAnyObjectByType<Terrain>();
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (!terrain || !player)
            {
                Debug.LogError("[ContentBuilder] Open the World scene first (terrain/player missing).");
                return;
            }

            Cleanup();
            var systems = BuildSystems();
            BuildParty(player);
            var content = new GameObject("Gameplay").transform;
            BuildAltarAndWaypoints();
            BuildNPCs(terrain, content);
            BuildCampProps(terrain, content);
            var chestPrefab = ChestPrefab();
            BuildChests(terrain, content, chestPrefab);
            BuildOrbs(terrain, content);
            BuildEnemies(terrain, content, player.transform.position);
            var ui = systems.GetComponent<GameUI>();
            ui.mapTexture = RenderMap(terrain);
            ui.worldMin = new Vector2(terrain.transform.position.x, terrain.transform.position.z);
            ui.worldSize = new Vector2(terrain.terrainData.size.x, terrain.terrainData.size.z);
            CinematicBuilder.Build();

            var scene = player.gameObject.scene;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[ContentBuilder] Gameplay content built.");
        }

        static void Cleanup()
        {
            foreach (var name in new[] { "HUD", "Systèmes", "Gameplay" })
            {
                var go = GameObject.Find(name);
                if (go) Object.DestroyImmediate(go);
            }
        }

        // ================================================================ Systems

        static GameObject BuildSystems()
        {
            var go = new GameObject("Systèmes");
            go.AddComponent<GameState>();
            go.AddComponent<SaveSystem>();
            go.AddComponent<Inventory>();
            go.AddComponent<QuestManager>();
            go.AddComponent<AudioManager>();
            var fx = go.AddComponent<CombatFX>();
            fx.additive = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/FX_Fire.mat");
            fx.alphaBlend = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Materials/FX_Smoke.mat");
            go.AddComponent<DamageNumbers>();
            go.AddComponent<GameUI>();
            return go;
        }

        // ================================================================ Party

        static void BuildParty(PlayerController player)
        {
            var visual = player.visual;
            for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);
            foreach (var old in player.GetComponents<CapeCloth>()) Object.DestroyImmediate(old);
            int layer = LayerMask.NameToLayer("Player");

            var defs = new[]
            {
                new PartyMember { name = "Aelis", title = "Voyageur tombé du ciel", element = Element.Anemo, skill = SkillKind.AnemoVortex, burst = BurstKind.Tornado,
                    skillName = "Tourbillon", burstName = "Œil de la Tempête", baseHP = 1150f, baseATK = 115f, critRate = 0.2f, critDamage = 0.7f, skillCooldown = 5f, burstEnergy = 60f, unlocked = true },
                new PartyMember { name = "Kaïra", title = "Lame de braise", element = Element.Pyro, skill = SkillKind.PyroSlash, burst = BurstKind.Inferno,
                    skillName = "Taille ardente", burstName = "Brasier écarlate", baseHP = 1050f, baseATK = 135f, critRate = 0.25f, critDamage = 0.8f, skillCooldown = 7f, burstEnergy = 70f, unlocked = false },
                new PartyMember { name = "Nerys", title = "Érudite des marées", element = Element.Hydro, skill = SkillKind.HydroWave, burst = BurstKind.Rain,
                    skillName = "Vague apaisante", burstName = "Pluie de grâce", baseHP = 1250f, baseATK = 95f, critRate = 0.15f, critDamage = 0.6f, skillCooldown = 9f, burstEnergy = 60f, unlocked = false },
                new PartyMember { name = "Torvan", title = "Veilleur du tonnerre", element = Element.Electro, skill = SkillKind.ElectroStrike, burst = BurstKind.Thunderstorm,
                    skillName = "Foudre jumelle", burstName = "Courroux céleste", baseHP = 1400f, baseATK = 120f, critRate = 0.2f, critDamage = 0.7f, skillCooldown = 6f, burstEnergy = 80f, unlocked = false },
            };
            string[] models = { "Hero", "Kaira", "Nerys", "Torvan" };
            var weapons = new GameObject[defs.Length];

            for (int i = 0; i < defs.Length; i++)
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterImporter.PrefabDir}/{models[i]}.prefab");
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
                inst.transform.localPosition = new Vector3(0f, -1f, 0f);
                inst.transform.localRotation = Quaternion.identity;
                if (layer >= 0) foreach (var t in inst.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                defs[i].model = inst;
                inst.SetActive(i == 0);

                var anim = inst.GetComponent<Animator>();
                var hand = anim ? anim.GetBoneTransform(HumanBodyBones.RightHand) : null;
                if (hand)
                {
                    weapons[i] = Sword(defs[i].element);
                    weapons[i].transform.SetParent(hand, false);
                    weapons[i].transform.localPosition = Vector3.zero;
                    // Orient the blade along the character's forward when the arm hangs down (adjusted at runtime by the pose).
                    weapons[i].transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward) * Quaternion.Euler(0f, 0f, 0f);
                    weapons[i].transform.position = hand.position;
                    if (layer >= 0) foreach (var t in weapons[i].GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
                    weapons[i].SetActive(false);
                }
            }

            var animator = player.GetComponent<ProceduralAnimator>() ?? player.gameObject.AddComponent<ProceduralAnimator>();
            animator.controller = player;
            animator.rig = defs[0].model.GetComponent<Animator>();
            player.visualBob = false;

            var party = player.GetComponent<PartyManager>() ?? player.gameObject.AddComponent<PartyManager>();
            party.members = defs;
            var combat = player.GetComponent<PlayerCombat>() ?? player.gameObject.AddComponent<PlayerCombat>();
            combat.weapons = weapons;
            if (!player.GetComponent<InteractionSystem>()) player.gameObject.AddComponent<InteractionSystem>();
            EditorUtility.SetDirty(player.gameObject);
        }

        static GameObject Sword(Element element)
        {
            var steel = Mat("Sword_Steel", new Color(0.82f, 0.85f, 0.9f), 0.9f, 0.95f);
            var gold = Mat("Sword_Gold", new Color(0.95f, 0.75f, 0.35f), 0.8f, 0.9f);
            var grip = Mat("Sword_Grip", new Color(0.2f, 0.12f, 0.08f), 0.3f, 0f);
            var gem = Mat($"Sword_Gem_{element}", Elements.Color(element), 0.9f, 0f, Elements.Color(element) * 3f);

            var root = new GameObject($"Épée ({Elements.Name(element)})");
            // Local +Z is the blade direction.
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.52f), new Vector3(0.05f, 0.012f, 0.9f), steel);
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.99f), new Vector3(0.035f, 0.01f, 0.06f), steel, new Vector3(0f, 45f, 0f));
            Part(root.transform, PrimitiveType.Cube, new Vector3(0f, 0f, 0.06f), new Vector3(0.22f, 0.03f, 0.035f), gold);
            Part(root.transform, PrimitiveType.Cylinder, new Vector3(0f, 0f, -0.05f), new Vector3(0.03f, 0.08f, 0.03f), grip, new Vector3(90f, 0f, 0f));
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, -0.14f), Vector3.one * 0.045f, gold);
            Part(root.transform, PrimitiveType.Sphere, new Vector3(0f, 0f, 0.06f), Vector3.one * 0.03f, gem);
            return root;
        }

        // ================================================================ Altar, waypoints, NPCs

        static readonly string[] WaypointNames =
        {
            "Sentinelle du Vallon", "Sentinelle des Brumes", "Sentinelle du Lac d'Argent", "Sentinelle des Pins",
            "Sentinelle de la Falaise", "Sentinelle du Gué", "Sentinelle des Ruines", "Sentinelle du Col",
        };

        static void BuildAltarAndWaypoints()
        {
            var altar = GameObject.Find("Autel des Vents");
            if (altar)
            {
                var a = altar.GetComponent<Altar>() ?? altar.AddComponent<Altar>();
                a.id = "altar";
                a.range = 6.5f;
            }
            var group = GameObject.Find("Points de téléportation");
            if (!group) return;
            int i = 0;
            foreach (Transform wpT in group.transform)
            {
                var wp = wpT.GetComponent<Waypoint>() ?? wpT.gameObject.AddComponent<Waypoint>();
                wp.id = $"wp_{i}";
                wp.displayName = WaypointNames[i % WaypointNames.Length];
                wp.range = 4.5f;
                var orb = wpT.Find("Orbe");
                if (orb)
                {
                    wp.orb = orb.GetComponent<Renderer>();
                    wp.orbLight = orb.GetComponentInChildren<Light>();
                }
                i++;
            }
        }

        static void BuildNPCs(Terrain terrain, Transform parent)
        {
            var altar = GameObject.Find("Autel des Vents");
            var camp = GameObject.Find("Camp de départ");
            Vector3 basePos = camp ? camp.transform.position : Vector3.zero;
            Vector3 elaraPos = altar ? altar.transform.position + new Vector3(4.5f, 0f, -3.5f) : basePos;
            Spawn("Elara", "elara", "Elara", "Gardienne de l'Autel", Ground(terrain, elaraPos), basePos + Vector3.back * 6f, parent);
            Spawn("Bram", "bram", "Bram", "Marchand", Ground(terrain, basePos + new Vector3(-1f, 0f, -9.5f)), basePos, parent);
        }

        /// <summary>Dresses the starting camp with Poly Haven CC0 props: Bram's stall, the campfire circle, tents and altar.</summary>
        static void BuildCampProps(Terrain terrain, Transform parent)
        {
            var camp = GameObject.Find("Camp de départ");
            if (!camp) return;
            Vector3 c = camp.transform.position;
            var group = new GameObject("Décor du camp").transform;
            group.SetParent(parent, false);

            GameObject Prop(string id, Vector3 offset, float yaw, bool colliders = true, float y = 0f)
            {
                var prefab = ArtLibrary.ModelPrefab(id, false, 0.01f, colliders);
                if (!prefab) return null;
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                go.transform.position = Ground(terrain, c + offset) + Vector3.up * y;
                go.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return go;
            }
            void Lamp(GameObject lantern, float height)
            {
                if (!lantern) return;
                var l = new GameObject("Lumière").AddComponent<Light>();
                l.transform.SetParent(lantern.transform, false);
                l.transform.localPosition = Vector3.up * height;
                l.type = LightType.Point;
                l.color = new Color(1f, 0.7f, 0.4f);
                l.intensity = 1.6f;
                l.range = 6f;
                l.shadows = LightShadows.None;
            }

            // Bram's stall: table between him and the camp, stock behind him
            var table = Prop("WoodenTable_01", new Vector3(-1f, 0f, -8f), 0f);
            float top = table ? ArtLibrary.PrefabSize(table).y : 0.75f;
            Lamp(Prop("Lantern_01", new Vector3(-1.6f, 0f, -8f), 20f, false, top), 0.25f);
            Prop("wicker_basket_01", new Vector3(-0.4f, 0f, -8.05f), -15f, false, top);
            var crate = Prop("wooden_crate_01", new Vector3(-2.7f, 0f, -11f), 12f);
            if (crate) Prop("wooden_crate_02", new Vector3(-2.7f, 0f, -11f), 40f, true, ArtLibrary.PrefabSize(crate).y);
            Prop("wooden_barrels_01", new Vector3(0.9f, 0f, -11.3f), -20f);
            Prop("wine_barrel_01", new Vector3(-3.8f, 0f, -9.4f), 75f);

            // Campfire circle (fire at c + (-7, -2))
            Vector3 fire = new(-7f, 0f, -2f);
            Prop("wooden_stool_02", fire + new Vector3(2f, 0f, 1.3f), 30f);
            Prop("wooden_stool_02", fire + new Vector3(-1.4f, 0f, 2.2f), -50f);
            Prop("wooden_bucket_01", fire + new Vector3(2.3f, 0f, -1.5f), 0f);
            Prop("wooden_axe", fire + new Vector3(-2.6f, 0f, -0.6f), 65f, false);

            // Tents at (9,-7), (11,3), (-11,7)
            Prop("Barrel_01", new Vector3(7.2f, 0f, -5.4f), 0f);
            Prop("wicker_basket_02", new Vector3(9.4f, 0f, 1.3f), 30f, false);
            Prop("kite_shield", new Vector3(-9.4f, 0f, 5.2f), 120f, false);
            Lamp(Prop("wooden_lantern_01", new Vector3(-9.6f, 0f, 8.6f), 0f, false), 0.3f);

            // Altar (c + (0, 14), 8 m plinth): offering jars beside the steps
            Prop("ceramic_pot", new Vector3(-3f, 0f, 9.4f), 0f);
            Prop("ceramic_pot", new Vector3(3f, 0f, 9.4f), 140f);
        }

        static void Spawn(string model, string id, string displayName, string title, Vector3 pos, Vector3 lookAt, Transform parent)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterImporter.PrefabDir}/{model}.prefab");
            if (!prefab) return;
            var root = new GameObject(displayName);
            root.transform.SetParent(parent, false);
            root.transform.position = pos;
            Vector3 d = lookAt - pos;
            d.y = 0f;
            if (d.sqrMagnitude > 0.01f) root.transform.rotation = Quaternion.LookRotation(d);
            var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
            inst.transform.localPosition = Vector3.zero;
            inst.transform.localRotation = Quaternion.identity;
            var npc = root.AddComponent<NPC>();
            npc.id = id;
            npc.displayName = displayName;
            npc.title = title;
            npc.range = 3f;
            var anim = root.AddComponent<ProceduralAnimator>();
            anim.isNPC = true;
            anim.rig = inst.GetComponent<Animator>();
            var col = root.AddComponent<CapsuleCollider>();
            col.height = 1.8f;
            col.radius = 0.3f;
            col.center = new Vector3(0f, 0.9f, 0f);
        }

        // ================================================================ Chests & orbs

        static GameObject ChestPrefab()
        {
            var wood = ArtLibrary.SurfaceMaterial("weathered_planks");
            var gold = Mat("Chest_Gold", new Color(0.95f, 0.75f, 0.35f), 0.8f, 0.9f);
            var root = new GameObject("Coffre");
            Box(root.transform, new Vector3(0f, 0.3f, 0f), new Vector3(1f, 0.6f, 0.65f), wood);
            var lid = new GameObject("Couvercle").transform;
            lid.SetParent(root.transform, false);
            lid.localPosition = new Vector3(0f, 0.6f, -0.32f);
            Box(lid, new Vector3(0f, 0.11f, 0.32f), new Vector3(1.04f, 0.22f, 0.68f), wood);
            Box(lid, new Vector3(0f, 0.11f, 0.665f), new Vector3(0.16f, 0.2f, 0.03f), gold);
            foreach (float x in new[] { -0.4f, 0.4f })
            {
                Box(root.transform, new Vector3(x, 0.3f, 0f), new Vector3(0.06f, 0.62f, 0.67f), gold);
                Box(lid, new Vector3(x, 0.11f, 0.32f), new Vector3(0.06f, 0.23f, 0.69f), gold);
            }
            var col = root.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.4f, 0f);
            col.size = new Vector3(1f, 0.8f, 0.65f);
            var chest = root.AddComponent<Chest>();
            chest.lid = lid;
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/Chest.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void PlaceChest(GameObject prefab, Transform parent, string id, int tier, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            go.name = $"Coffre {id}";
            var c = go.GetComponent<Chest>();
            c.id = id;
            c.tier = tier;
            if (tier >= 2) AddLight(go.transform, new Vector3(0f, 1.2f, 0f), new Color(1f, 0.85f, 0.5f), 5f, 1.5f);
        }

        static void BuildChests(Terrain terrain, Transform parent, GameObject prefab)
        {
            var group = new GameObject("Coffres").transform;
            group.SetParent(parent, false);

            // Story chests
            var ruins = GameObject.Find("Ruines anciennes");
            Transform firstRuin = null;
            if (ruins && ruins.transform.childCount > 0)
            {
                var camp = GameObject.Find("Camp de départ");
                Vector3 c = camp ? camp.transform.position : Vector3.zero;
                firstRuin = ruins.transform.Cast<Transform>().OrderBy(t => Vector3.Distance(t.position, c)).First();
                PlaceChest(prefab, group, "ruin_chest", 2, Ground(terrain, firstRuin.position), R(0f, 360f));
                int k = 0;
                foreach (Transform ruin in ruins.transform)
                    if (ruin != firstRuin) PlaceChest(prefab, group, $"ruin_{k++}", 1, Ground(terrain, ruin.position + new Vector3(R(-3f, 3f), 0f, R(-3f, 3f))), R(0f, 360f));
            }
            var oldTowerChest = GameObject.Find("Coffre ancien");
            if (oldTowerChest) Object.DestroyImmediate(oldTowerChest);
            var towerTop = GameObject.Find("Tour en ruine/Sommet");
            if (towerTop) PlaceChest(prefab, group, "tower_chest", 3, towerTop.transform.position + Vector3.up * 0.25f, 180f);

            // Scattered chests
            var data = terrain.terrainData;
            int placed = 0;
            for (int guard = 0; placed < 26 && guard < 5000; guard++)
            {
                float u = R(0.12f, 0.88f), v = R(0.12f, 0.88f);
                float h = data.GetInterpolatedHeight(u, v);
                if (h < SeaLevel + 2f || data.GetSteepness(u, v) > 20f) continue;
                var pos = new Vector3(u * data.size.x, h, v * data.size.z) + terrain.transform.position;
                int tier = rng.NextDouble() < 0.6 ? 0 : rng.NextDouble() < 0.75 ? 1 : 2;
                if (h > 200f) tier = Mathf.Max(tier, 2);
                PlaceChest(prefab, group, $"chest_{placed}", tier, pos, R(0f, 360f));
                placed++;
            }
        }

        static void BuildOrbs(Terrain terrain, Transform parent)
        {
            var group = new GameObject("Orbes des Vents").transform;
            group.SetParent(parent, false);
            var glow = Mat("WindOrb", Elements.Color(Element.Anemo), 0.9f, 0f, new Color(0.9f, 3.2f, 2.6f));
            var data = terrain.terrainData;
            int placed = 0;
            for (int guard = 0; placed < 32 && guard < 8000; guard++)
            {
                float u = R(0.1f, 0.9f), v = R(0.1f, 0.9f);
                float h = data.GetInterpolatedHeight(u, v);
                if (h < SeaLevel + 3f) continue;
                bool high = h > 110f || data.GetSteepness(u, v) > 35f;
                if (!high && rng.NextDouble() > 0.35) continue;
                var pos = new Vector3(u * data.size.x, h + (high ? 1.6f : R(2.5f, 6f)), v * data.size.z) + terrain.transform.position;
                var orb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Object.DestroyImmediate(orb.GetComponent<Collider>());
                orb.name = $"Orbe {placed}";
                orb.transform.SetParent(group, false);
                orb.transform.position = pos;
                orb.transform.localScale = Vector3.one * 0.45f;
                orb.GetComponent<Renderer>().sharedMaterial = glow;
                AddLight(orb.transform, Vector3.zero, Elements.Color(Element.Anemo), 5f, 2f);
                orb.AddComponent<WindOrb>().id = $"orb_{placed}";
                placed++;
            }
        }

        // ================================================================ Enemies

        static void BuildEnemies(Terrain terrain, Transform parent, Vector3 spawn)
        {
            var slimes = new Dictionary<Element, GameObject>();
            foreach (var e in new[] { Element.Anemo, Element.Pyro, Element.Hydro, Element.Electro, Element.Cryo })
                slimes[e] = SlimePrefab(e);
            var golem = GolemPrefab(false);
            var boss = GolemPrefab(true);

            var group = new GameObject("Camps ennemis").transform;
            group.SetParent(parent, false);
            var data = terrain.terrainData;

            // Tutorial camp east of the starting camp
            var tut = Camp(group, "camp_tutorial", "Slimes du pré", Ground(terrain, spawn + new Vector3(40f, 0f, 12f)),
                new[] { slimes[Element.Anemo], slimes[Element.Hydro] }, 3, 5f, 2);
            WorldMarkers.Register("camp_tutorial", tut.transform);
            var marker = new GameObject("Marqueur").AddComponent<QuestMarkerAnchor>();
            marker.transform.SetParent(tut.transform, false);
            marker.id = "camp_tutorial";

            // Boss arena: largest ruin closest to the tower
            var tower = GameObject.Find("Tour en ruine");
            var ruins = GameObject.Find("Ruines anciennes");
            Vector3 arena = tower ? tower.transform.position : spawn;
            if (ruins && tower && ruins.transform.childCount > 0)
                arena = ruins.transform.Cast<Transform>().OrderBy(t => Vector3.Distance(t.position, tower.transform.position)).First().position;
            var bossCamp = Camp(group, "boss_arena", "Gardien Ancien", Ground(terrain, arena), new[] { boss }, 1, 0.5f, 8);
            bossCamp.respawn = false;
            bossCamp.extraPrefabs = new[] { slimes[Element.Pyro], slimes[Element.Electro], slimes[Element.Cryo] };
            var bm = new GameObject("Marqueur").AddComponent<QuestMarkerAnchor>();
            bm.transform.SetParent(bossCamp.transform, false);
            bm.id = "boss";

            // Wild camps scaled by distance from spawn
            var taken = new List<Vector3> { spawn, tut.transform.position, bossCamp.transform.position };
            int camps = 0;
            for (int guard = 0; camps < 16 && guard < 6000; guard++)
            {
                float u = R(0.1f, 0.9f), v = R(0.1f, 0.9f);
                float h = data.GetInterpolatedHeight(u, v);
                if (h < SeaLevel + 3f || h > 260f || data.GetSteepness(u, v) > 18f) continue;
                var pos = new Vector3(u * data.size.x, h, v * data.size.z) + terrain.transform.position;
                if (taken.Any(t => Vector3.Distance(t, pos) < 140f)) continue;
                taken.Add(pos);
                float dist = Vector3.Distance(pos, spawn);
                int level = Mathf.Clamp(3 + Mathf.RoundToInt(dist / 120f), 3, 20);
                bool golemCamp = h > 120f || rng.NextDouble() < 0.25;
                var els = new[] { Element.Pyro, Element.Hydro, Element.Electro, Element.Cryo, Element.Anemo };
                var e1 = els[rng.Next(els.Length)];
                var e2 = els[rng.Next(els.Length)];
                var set = golemCamp ? new[] { golem, slimes[e1] } : new[] { slimes[e1], slimes[e2] };
                Camp(group, $"camp_{camps}", golemCamp ? "Gardiens de pierre" : "Slimes", pos, set, golemCamp ? 2 : rng.Next(3, 6), 6f, level);
                camps++;
            }
        }

        static EnemySpawner Camp(Transform parent, string id, string label, Vector3 pos, GameObject[] prefabs, int count, float radius, int level)
        {
            var go = new GameObject($"Camp — {label} (niv. {level})");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;
            var s = go.AddComponent<EnemySpawner>();
            s.campId = id;
            s.count = count;
            s.radius = radius;
            // Per-camp level-scaled copies are created at runtime by scaling stats in LevelScaler.
            s.prefabs = prefabs;
            go.AddComponent<CampLevel>().level = level;
            return s;
        }

        static GameObject SlimePrefab(Element element)
        {
            string path = $"{PrefabDir}/Slime_{element}.prefab";
            var col = Elements.Color(element);
            var body = Mat($"Slime_{element}", Color.Lerp(col, Color.white, 0.15f), 0.92f, 0f, col * 0.8f);
            var eye = Mat("Slime_Eye", new Color(0.05f, 0.05f, 0.08f), 0.95f, 0f);
            var shine = Mat("Slime_Shine", Color.white, 1f, 0f, Color.white * 1.5f);

            var root = new GameObject($"Slime {Elements.Name(element)}");
            var bodyT = new GameObject("Corps").transform;
            bodyT.SetParent(root.transform, false);
            var blob = Part(bodyT, PrimitiveType.Sphere, new Vector3(0f, 0.45f, 0f), new Vector3(1f, 0.85f, 1f), body);
            Part(bodyT, PrimitiveType.Sphere, new Vector3(-0.17f, 0.58f, 0.42f), new Vector3(0.12f, 0.17f, 0.08f), eye);
            Part(bodyT, PrimitiveType.Sphere, new Vector3(0.17f, 0.58f, 0.42f), new Vector3(0.12f, 0.17f, 0.08f), eye);
            Part(bodyT, PrimitiveType.Sphere, new Vector3(-0.15f, 0.62f, 0.46f), Vector3.one * 0.04f, shine);
            Part(bodyT, PrimitiveType.Sphere, new Vector3(0.19f, 0.62f, 0.46f), Vector3.one * 0.04f, shine);
            AddLight(bodyT, new Vector3(0f, 0.5f, 0f), col, 4f, 1.2f);

            var sphere = root.AddComponent<SphereCollider>();
            sphere.center = new Vector3(0f, 0.45f, 0f);
            sphere.radius = 0.5f;
            var h = root.AddComponent<Health>();
            h.maxHealth = 380f;
            h.defense = 0.3f;
            h.maxPoise = 25f;
            h.displayName = $"Slime {Elements.Name(element)}";
            h.barOffset = new Vector3(0f, 1.35f, 0f);
            h.innateAura = element == Element.Anemo ? Element.Physical : element;
            var e = root.AddComponent<EnemyBase>();
            e.kind = EnemyKind.Slime;
            e.attackElement = element;
            e.moveSpeed = 2.6f;
            e.attackRange = 2f;
            e.attackDamage = 70f;
            e.windup = 0.75f;
            e.heavyChance = 0.25f;
            e.xpReward = 50;
            e.moraReward = 35;
            e.dropItem = "slime_gel";
            e.killTag = "slime";
            e.body = bodyT;
            e.glow = blob.GetComponent<Renderer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject GolemPrefab(bool boss)
        {
            string path = $"{PrefabDir}/{(boss ? "Boss_GardienAncien" : "Golem")}.prefab";
            float s = boss ? 2.3f : 1f;
            Element element = boss ? Element.Geo : Element.Physical;
            var coreCol = boss ? Elements.Color(Element.Geo) : new Color(1f, 0.55f, 0.25f);
            var core = Mat(boss ? "Golem_CoreBoss" : "Golem_Core", coreCol, 0.9f, 0f, coreCol * 3f);
            var boulder = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/PolyHaven/boulder_01.prefab");
            var stone = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/PolyHaven/stone_01.prefab");
            var arm = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Prefabs/PolyHaven/rock_09.prefab");

            var root = new GameObject(boss ? "Gardien Ancien" : "Gardien de pierre");
            var bodyT = new GameObject("Corps").transform;
            bodyT.SetParent(root.transform, false);
            bodyT.localPosition = new Vector3(0f, 1.1f * s, 0f);
            RockPart(boulder, bodyT, Vector3.zero, new Vector3(1.3f, 1.4f, 1.1f) * s, Quaternion.Euler(0f, 30f, 0f));
            var headT = new GameObject("Tête").transform;
            headT.SetParent(bodyT, false);
            headT.localPosition = new Vector3(0f, 0.95f * s, 0.1f * s);
            RockPart(stone, headT, Vector3.zero, Vector3.one * 0.6f * s, Quaternion.identity);
            var glow = Part(headT, PrimitiveType.Sphere, new Vector3(0f, 0.05f * s, 0.3f * s), Vector3.one * 0.18f * s, core);
            Part(bodyT, PrimitiveType.Sphere, new Vector3(0f, 0.1f * s, 0.55f * s), Vector3.one * 0.32f * s, core);
            AddLight(bodyT, new Vector3(0f, 0.2f * s, 0.9f * s), coreCol, 6f * s, 2f);

            Transform Arm(float side)
            {
                var pivot = new GameObject(side > 0 ? "Bras D" : "Bras G").transform;
                pivot.SetParent(bodyT, false);
                pivot.localPosition = new Vector3(0.85f * side * s, 0.5f * s, 0f);
                RockPart(arm, pivot, new Vector3(0.1f * side * s, -0.6f * s, 0f), new Vector3(0.6f, 1.3f, 0.6f) * s, Quaternion.Euler(0f, 0f, 90f));
                RockPart(stone, pivot, new Vector3(0.12f * side * s, -1.25f * s, 0.05f * s), Vector3.one * 0.55f * s, Quaternion.identity);
                return pivot;
            }
            var armL = Arm(-1f);
            var armR = Arm(1f);
            RockPart(stone, root.transform, new Vector3(-0.4f * s, 0.3f * s, 0f), Vector3.one * 0.6f * s, Quaternion.identity);
            RockPart(stone, root.transform, new Vector3(0.4f * s, 0.3f * s, 0f), Vector3.one * 0.6f * s, Quaternion.identity);
            if (boss)
                for (int i = 0; i < 5; i++)
                    RockPart(arm, headT, new Vector3((i - 2) * 0.12f * s, 0.35f * s, -0.05f * s), new Vector3(0.12f, 0.5f, 0.12f) * s, Quaternion.Euler(0f, 0f, (i - 2) * 12f));

            var capsule = root.AddComponent<CapsuleCollider>();
            capsule.center = new Vector3(0f, 1.2f * s, 0f);
            capsule.radius = 0.8f * s;
            capsule.height = 2.6f * s;
            var h = root.AddComponent<Health>();
            h.maxHealth = boss ? 4000f : 1300f;
            h.defense = boss ? 0.5f : 0.45f;
            h.maxPoise = boss ? 220f : 90f;
            h.displayName = boss ? "Gardien Ancien" : "Gardien de pierre";
            h.barOffset = new Vector3(0f, 3.1f * s, 0f);
            var e = root.AddComponent<EnemyBase>();
            e.kind = boss ? EnemyKind.Boss : EnemyKind.Golem;
            e.attackElement = element;
            e.moveSpeed = boss ? 2.4f : 1.9f;
            e.attackRange = boss ? 5f : 2.8f;
            e.attackDamage = boss ? 230f : 150f;
            e.attackCooldown = boss ? 2.4f : 3f;
            e.windup = boss ? 1.1f : 1.0f;
            e.heavyChance = boss ? 0.55f : 0.5f;
            e.aggroRange = boss ? 22f : 14f;
            e.leashRange = boss ? 30f : 40f;
            e.xpReward = boss ? 3000 : 260;
            e.moraReward = boss ? 4000 : 180;
            e.dropItem = "golem_core";
            e.killTag = boss ? "boss" : "golem";
            e.body = bodyT;
            e.head = headT;
            e.armL = armL;
            e.armR = armR;
            e.glow = glow.GetComponent<Renderer>();

            var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        static void RockPart(GameObject prefab, Transform parent, Vector3 localPos, Vector3 targetSize, Quaternion rot)
        {
            if (!prefab) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            PrefabUtility.UnpackPrefabInstance(go, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var size = ArtLibrary.PrefabSize(prefab);
            go.transform.localPosition = localPos;
            go.transform.localRotation = rot;
            go.transform.localScale = new Vector3(targetSize.x / Mathf.Max(0.01f, size.x), targetSize.y / Mathf.Max(0.01f, size.y), targetSize.z / Mathf.Max(0.01f, size.z));
        }

        // ================================================================ Map

        static Texture2D RenderMap(Terrain terrain)
        {
            var size = terrain.terrainData.size;
            var center = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            var camGO = new GameObject("MapCam");
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = size.z * 0.5f;
            cam.aspect = 1f;
            cam.transform.position = center + Vector3.up * 900f;
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = 2000f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.25f, 0.35f);
            var data = camGO.AddComponent<UniversalAdditionalCameraData>();
            data.renderPostProcessing = false;
            data.renderShadows = false;
            var rt = new RenderTexture(2048, 2048, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            RenderTexture.active = rt;
            var tex = new Texture2D(2048, 2048, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 2048, 2048), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGO);
            string path = $"{GenDir}/WorldMap.png";
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.mipmapEnabled = false;
            imp.wrapMode = TextureWrapMode.Clamp;
            imp.maxTextureSize = 2048;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        // ================================================================ Helpers

        static Material Mat(string name, Color color, float smooth, float metal, Color? emission = null)
        {
            var m = ArtLibrary.LoadOrCreateMaterial($"{MatDir}/{name}.mat", "Universal Render Pipeline/Lit");
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            return m;
        }

        static GameObject Part(Transform parent, PrimitiveType type, Vector3 pos, Vector3 scale, Material mat, Vector3? euler = null)
        {
            var g = GameObject.CreatePrimitive(type);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.transform.SetParent(parent, false);
            g.transform.localPosition = pos;
            g.transform.localRotation = Quaternion.Euler(euler ?? Vector3.zero);
            g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = mat;
            return g;
        }

        static void Box(Transform parent, Vector3 pos, Vector3 size, Material mat) => Part(parent, PrimitiveType.Cube, pos, size, mat);

        static void AddLight(Transform parent, Vector3 pos, Color color, float range, float intensity)
        {
            var l = new GameObject("Lumière").AddComponent<Light>();
            l.transform.SetParent(parent, false);
            l.transform.localPosition = pos;
            l.type = LightType.Point;
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = LightShadows.None;
        }

        static Vector3 Ground(Terrain t, Vector3 p)
        {
            p.y = t.SampleHeight(p) + t.transform.position.y;
            return p;
        }
    }
}
