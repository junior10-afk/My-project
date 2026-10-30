using System.Linq;
using Unity.Cinemachine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Builds the Prologue / Chapter I cinematics (see STORY.md) into the World scene: one child of
    /// "Cinématiques" per cinematic, each with a PlayableDirector + Timeline asset (Cinemachine shot
    /// track, subtitles, fades, actions), its shot cameras, marks and actors. The Timelines stay
    /// fully editable in the Timeline window afterwards (enable the CinemachineBrain on the main
    /// camera to preview). Run after Build Gameplay Content (it needs Elara and the boss arena).
    /// </summary>
    public static class CinematicBuilder
    {
        const string Dir = "Assets/_Game/Cinematics";
        const string MatDir = "Assets/_Game/Materials/Cinematics";
        const string RootName = "Cinématiques";
        const float SeaLevel = 0.08f * 420f;
        const string Voice = "Voix lointaine";

        static Terrain terrain;
        static NoiseSettings handheld;

        [MenuItem("Last World/Build Cinematics")]
        public static void BuildMenu()
        {
            if (Build())
            {
                var scene = EditorSceneManager.GetActiveScene();
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
        }

        /// <summary>Renders key frames of every cinematic into one contact-sheet PNG (Assets/../Previews).</summary>
        [MenuItem("Last World/Preview Cinematics (contact sheet)")]
        public static void PreviewMenu() => Debug.Log($"[CinematicBuilder] Contact sheet: {Preview("Previews/cinematics_sheet.png", 4)}");

        public static string Preview(string file, int framesPerCinematic)
        {
            var root = GameObject.Find(RootName);
            var cam = Camera.main;
            var brain = cam ? cam.GetComponent<CinemachineBrain>() : null;
            if (!root || !brain) return "no cinematics in scene";
            bool wasEnabled = brain.enabled;
            var method = brain.UpdateMethod;
            cam.transform.GetPositionAndRotation(out var p0, out var r0);
            float fov0 = cam.fieldOfView;
            brain.enabled = true;
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.ManualUpdate;

            var directors = root.GetComponentsInChildren<PlayableDirector>(true);
            const int w = 480, h = 270;
            var sheet = new Texture2D(w * framesPerCinematic, h * directors.Length, TextureFormat.RGB24, false);
            var rt = new RenderTexture(w, h, 24);
            var read = new Texture2D(w, h, TextureFormat.RGB24, false);
            for (int i = 0; i < directors.Length; i++)
            {
                var d = directors[i];
                var go = d.gameObject;
                go.SetActive(true);
                var actors = go.transform.Cast<Transform>().Where(t => t.name == "Kael" || t.name.StartsWith("Fragment")).ToList();
                actors.ForEach(a => a.gameObject.SetActive(true));
                d.RebuildGraph();
                double len = d.duration;
                for (int j = 0; j < framesPerCinematic; j++)
                {
                    d.time = len * (j + 0.5) / framesPerCinematic;
                    d.Evaluate();
                    foreach (var m in go.GetComponentsInChildren<TimelineMover>()) m.SendMessage("LateUpdate");
                    brain.ManualUpdate();
                    brain.ManualUpdate();
                    cam.targetTexture = rt;
                    cam.Render();
                    cam.targetTexture = null;
                    RenderTexture.active = rt;
                    read.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    RenderTexture.active = null;
                    sheet.SetPixels(j * w, (directors.Length - 1 - i) * h, w, h, read.GetPixels());
                }
                d.Stop();
                actors.ForEach(a => a.gameObject.SetActive(false));
                go.SetActive(false);
            }
            brain.UpdateMethod = method;
            brain.enabled = wasEnabled;
            cam.transform.SetPositionAndRotation(p0, r0);
            cam.fieldOfView = fov0;
            string full = System.IO.Path.GetFullPath(System.IO.Path.Combine(Application.dataPath, "..", file));
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            System.IO.File.WriteAllBytes(full, sheet.EncodeToPNG());
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(read);
            Object.DestroyImmediate(sheet);
            return full;
        }

        public static bool Build()
        {
            terrain = Object.FindAnyObjectByType<Terrain>();
            var altar = GameObject.Find("Autel des Vents");
            var elara = GameObject.Find("Gameplay/Elara");
            var towerTop = GameObject.Find("Tour en ruine/Sommet");
            var arenaCamp = Object.FindObjectsByType<EnemySpawner>().FirstOrDefault(s => s.campId == "boss_arena");
            if (!terrain || !altar || !elara || !towerTop || !arenaCamp)
            {
                Debug.LogError("[CinematicBuilder] World scene with gameplay content required (terrain, altar, Elara, tower, boss arena).");
                return false;
            }

            ArtLibrary.EnsureFolder(Dir);
            ArtLibrary.EnsureFolder(MatDir);
            handheld = AssetDatabase.LoadAssetAtPath<NoiseSettings>("Packages/com.unity.cinemachine/Presets/Noise/Handheld_normal_mild.asset");

            var old = GameObject.Find(RootName);
            if (old) Object.DestroyImmediate(old);
            var root = new GameObject(RootName);
            root.AddComponent<CinematicDirector>();

            var cam = Camera.main;
            if (cam && !cam.GetComponent<CinemachineBrain>())
            {
                var brain = cam.gameObject.AddComponent<CinemachineBrain>();
                brain.enabled = false; // enabled by CinematicDirector while a cinematic plays
            }

            Vector3 altarPos = altar.transform.position;
            Vector3 elaraPos = elara.transform.position;
            Vector3 arena = arenaCamp.transform.position;
            // The boss arena is a ruin deep in the forest: open a clearing so the fight and its cinematics breathe.
            ClearTrees(arena, 34f, 14f);

            Prologue(root.transform, altarPos);
            Reveil(root.transform, altarPos, elaraPos);
            Resonance(root.transform, altarPos, towerTop.transform.position);
            Gardien(root.transform, arena);
            PremierVent(root.transform, arena);
            Epilogue(root.transform);

            AssetDatabase.SaveAssets();
            Debug.Log("[CinematicBuilder] 6 cinematics built.");
            return true;
        }

        // ================================================================ CIN_00 — La chute

        static void Prologue(Transform parent, Vector3 altar)
        {
            var c = Begin(parent, "CIN_00_Prologue");
            Vector3 impact = Ground(altar + new Vector3(1.5f, 0f, -6f)) + Vector3.up; // where Aelis wakes up in CIN_01
            Vector3 starFrom = altar + new Vector3(-90f, 170f, 150f);
            Star(c, "Etoile", starFrom, impact, 10.5f, 19f);
            var horizon = Target(c, "Horizon", altar + new Vector3(260f, 30f, 640f));
            // Static framings: the star crosses the frame (tracking a fast object smears it with motion blur)
            // Wide view from 110 m: horizon + the whole fall inside the DOF's sharp range (< 350 m)
            var skyPath = Target(c, "Trajectoire", altar + new Vector3(-20f, 55f, 40f));
            var landing = Target(c, "Point de chute", impact + Vector3.up * 2.5f);
            Mark(c, "Impact", impact, 0f);

            Act(c, 0f, "player:hide", true);
            Fade(c, 0f, 2f, Color.black, 1f, 1f);
            Fade(c, 2f, 3f, Color.black, 1f, 0f);
            Shot(c, "Plan aérien", 0f, 11f, altar + new Vector3(-260f, 230f, -380f), altar + new Vector3(-190f, 205f, -310f), horizon, 50f);
            Sub(c, 1f, 5.5f, Voice, "Il y avait autrefois mille mondes. Ils se sont éteints comme des bougies, sans un bruit.");
            Sub(c, 7f, 4f, Voice, "Il n'en reste qu'un.");

            Shot(c, "L'étoile", 11f, 5f, Ground(impact + new Vector3(35f, 0f, -105f)) + Vector3.up * 6f, Ground(impact + new Vector3(32f, 0f, -99f)) + Vector3.up * 6f, skyPath, 50f, 0f, false);
            Sub(c, 12f, 5.5f, Voice, "Si tu entends ma voix… alors le Sceau est brisé.");
            Shot(c, "L'impact", 16f, 7f, impact + new Vector3(7f, 1.6f, -9f), impact + new Vector3(6f, 1.4f, -8f), landing, 45f);
            Sub(c, 18f, 3.5f, Voice, "Pardonne-moi. Réveille-toi… Aelis.");

            Act(c, 19f, "burst:Impact:Anemo");
            Act(c, 19f, "sound:burst");
            Fade(c, 19.2f, 1.3f, Color.white, 0f, 1f);
            Fade(c, 20.5f, 2.5f, Color.white, 1f, 1f);
            Act(c, 20f, "hide:Etoile", true);
            End(c);
        }

        // ================================================================ CIN_01 — Réveil

        static void Reveil(Transform parent, Vector3 altar, Vector3 elara)
        {
            var c = Begin(parent, "CIN_01_Reveil");
            Vector3 p = Ground(altar + new Vector3(1.5f, 0f, -6f)); // just off the altar's 8 m plinth
            Vector3 fwd = Flat(elara - p);
            Vector3 right = Vector3.Cross(Vector3.up, fwd);
            Mark(c, "PlayerMark", p, Yaw(fwd));
            var head = Target(c, "Tête Aelis", p + Vector3.up * 1.62f);
            var elaraHead = Target(c, "Tête Elara", elara + Vector3.up * 1.6f);
            var altarTop = Target(c, "Autel", altar + Vector3.up * 4f);

            Act(c, 0f, "place:PlayerMark", true);
            Act(c, 0f, "player:show", true);
            Act(c, 0.05f, "anim:player:LayToIdle:3.6"); // Aelis gets up from the grass
            Act(c, 0.05f, "npcface:elara", true);
            Fade(c, 0f, 3f, Color.white, 1f, 0f);
            var body = Target(c, "Corps", p + Vector3.up * 0.4f);
            var torso = Target(c, "Torse", p + Vector3.up * 1.0f); // she rises: the framing follows her up
            Shot(c, "Au sol", 0f, 1.6f, p + fwd * 1.7f + right * 0.9f + Vector3.up * 1.6f, p + fwd * 1.6f + right * 0.8f + Vector3.up * 1.7f, body, 40f);
            Shot(c, "Se relève", 1.6f, 1.6f, p + fwd * 2.3f + right * 1.1f + Vector3.up * 1.3f, p + fwd * 2.1f + right * 0.9f + Vector3.up * 1.45f, torso, 45f);
            Shot(c, "Visage", 3.2f, 2.3f, p + fwd * 1.0f + Vector3.up * 1.62f, p + fwd * 1.4f + Vector3.up * 1.68f, head, 35f);
            Sub(c, 1.5f, 3f, "Elara", "Doucement… N'essaie pas de te lever trop vite.");
            Shot(c, "Elara", 5.5f, 3.5f, p - fwd * 1.2f + right * 0.5f + Vector3.up * 1.75f, p - fwd * 1.1f + right * 0.45f + Vector3.up * 1.72f, elaraHead, 32f);
            Sub(c, 5.8f, 3.0f, "Elara", "Le vent t'a déposé ici cette nuit. Bienvenue sur Aerendal.");
            Shot(c, "Les Prairies", 9f, 6f, p + new Vector3(-4f, 2f, -6f), p + new Vector3(-18f, 22f, -40f), altarTop, 55f, 0f, false);
            End(c);
        }

        // ================================================================ CIN_02 — Résonance

        static void Resonance(Transform parent, Vector3 altar, Vector3 tower)
        {
            var c = Begin(parent, "CIN_02_Resonance");
            // On the plinth, facing the pillar — clear of the 5.6 m step (at 3.0 m the capsule caught its edge)
            Vector3 p = Ground(altar + new Vector3(0f, 0f, -3.5f));
            Mark(c, "AltarMark", p, 0f);
            tower = Ground(tower + new Vector3(-2.2f, 0f, -1.8f)); // beside the chest at the top
            Mark(c, "AltarFX", altar + Vector3.up * 3f, 0f);
            var altarTarget = Target(c, "Autel", altar + Vector3.up * 2.5f);
            var head = Target(c, "Tête Aelis", p + Vector3.up * 1.62f);

            var kael = Kael(c, tower, 250f, true);
            var kaelHead = Target(c, "Tête Kael", tower + Vector3.up * 1.62f);
            Mark(c, "KaelFX", tower + Vector3.up * 2f, 0f);
            var sky = Target(c, "Ciel", tower + Vector3.up * 25f);

            // Vision: the four fragments scatter to the four regions
            var dirs = new[] { new Vector3(1f, 0.15f, 0.2f), new Vector3(-0.3f, 0.2f, 1f), new Vector3(-1f, 0.1f, -0.4f), new Vector3(0.4f, 0.12f, -1f) };
            var elems = new[] { Element.Anemo, Element.Pyro, Element.Hydro, Element.Electro };
            for (int i = 0; i < 4; i++)
                Fragment(c, $"Fragment {i}", elems[i], tower + Vector3.up * 3f, tower + Vector3.up * 3f + dirs[i].normalized * 380f, 10f, 14f, false);

            Act(c, 0f, "place:AltarMark", true);
            var contact = Target(c, "Contact", p + new Vector3(0f, 1.3f, 1f)); // between Aelis and the pillar
            Shot(c, "La main", 0f, 4f, p + new Vector3(-2.6f, 1.5f, 0.2f), p + new Vector3(-2.2f, 1.5f, 0.45f), contact, 42f);
            Act(c, 0.3f, "anim:player:Interact:2.6");
            Act(c, 1.5f, "burst:AltarFX:Anemo");
            Act(c, 1.5f, "sound:skill");
            Fade(c, 3.2f, 0.8f, Color.white, 0f, 1f);

            Act(c, 4f, "show:Kael");
            Fade(c, 4f, 1f, Color.white, 1f, 0f);
            Shot(c, "Le traître", 4f, 6f, tower + new Vector3(6f, 2f, -4f), tower + new Vector3(-5f, 3f, -6f), kaelHead, 40f);
            Sub(c, 5f, 4f, "???", "Pardonne-moi, petite sœur. Je ne te laisserai pas pourrir là-dedans une éternité de plus.");
            Act(c, 4.2f, "anim:Kael:SwordIdle:4.2");
            Act(c, 8.6f, "anim:Kael:Attack4:1.4"); // the blow that shatters the Seal
            Act(c, 9.5f, "burst:KaelFX:Electro");
            Act(c, 9.5f, "sound:slam");
            for (int i = 0; i < 4; i++) Act(c, 9.8f, $"show:Fragment {i}");
            Shot(c, "Le Sceau se brise", 10f, 5f, tower + new Vector3(-30f, 25f, -40f), tower + new Vector3(-42f, 40f, -62f), sky, 62f, 0f, false);
            Fade(c, 14f, 1f, Color.white, 0f, 1f);
            Act(c, 15f, "hide:Kael", true);

            Fade(c, 15.2f, 1.6f, Color.white, 1f, 0f);
            Shot(c, "Retour", 15f, 5f, p + new Vector3(0.9f, 1.65f, 1.0f), p + new Vector3(0.7f, 1.66f, 0.85f), head, 34f);
            Sub(c, 16.5f, 3f, "Aelis", "Qu'est-ce que… c'était ?");
            _ = kael;
            End(c);
        }

        // ================================================================ CIN_03 — Le Gardien Ancien

        static void Gardien(Transform parent, Vector3 arena)
        {
            var c = Begin(parent, "CIN_03_Gardien");
            var center = Target(c, "Arène", arena + Vector3.up * 3f);
            var high = Target(c, "Gardien", arena + Vector3.up * 4f);
            Mark(c, "ArenaFX", arena + Vector3.up * 1f, 0f);

            Shot(c, "Le sol tremble", 0f, 4f, Ground(arena + new Vector3(14f, 0f, -14f)) + Vector3.up * 1.2f, Ground(arena + new Vector3(12f, 0f, -12f)) + Vector3.up * 1f, center, 45f);
            Act(c, 0.5f, "sound:slam");
            Act(c, 1.5f, "burst:ArenaFX:Anemo");
            Act(c, 3f, "sound:slam");
            Shot(c, "Le réveil", 4f, 4.5f, arena + new Vector3(6f, 2f, -6f), arena + new Vector3(9f, 9f, -9f), high, 45f);
            Act(c, 5f, "burst:ArenaFX:Anemo");
            Act(c, 5f, "sound:burst");
            Sub(c, 4.5f, 3.5f, "Gardien Ancien", "INTRUS… NUL NE FOULERA… LA TOUR…");
            Shot(c, "Face à face", 8.5f, 3.5f, Ground(arena + new Vector3(-20f, 0f, -20f)) + Vector3.up * 6f, Ground(arena + new Vector3(-19f, 0f, -19f)) + Vector3.up * 5.5f, center, 50f);
            Act(c, 8.6f, "banner:Gardien Ancien — Sentinelle de la Tour");
            End(c);

            var trigger = new GameObject("Déclencheur — Gardien Ancien").AddComponent<CinematicTrigger>();
            trigger.transform.SetParent(parent, false);
            trigger.transform.position = arena;
            trigger.cinematicId = "CIN_03_Gardien";
            trigger.step = 7;
            trigger.radius = 35f;
        }

        // ================================================================ CIN_04 — Le premier vent

        static void PremierVent(Transform parent, Vector3 arena)
        {
            var c = Begin(parent, "CIN_04_PremierVent");
            Vector3 p = Ground(arena + new Vector3(0f, 0f, -6f));
            Mark(c, "PlayerMark", p, 0f);
            var golem = Target(c, "Gardien", arena + Vector3.up * 1.5f);
            var frag = Fragment(c, "Fragment", Element.Anemo, Ground(arena) + Vector3.up * 0.5f, Ground(arena) + Vector3.up * 5f, 4.5f, 8f, true);
            var plain = Target(c, "Plaine", Ground(arena + new Vector3(0f, 0f, 160f)) + Vector3.up * 10f);

            // Kael watches from the edge of the clearing, across the arena (canopy hides anything farther)
            Vector3 cliff = Ground(arena + Flat(arena - p) * 28f);
            Kael(c, cliff, Yaw(Flat(arena - cliff)));
            var kaelHead = Target(c, "Tête Kael", cliff + Vector3.up * 1.62f);
            Mark(c, "KaelFX", cliff + Vector3.up * 1f, 0f);

            Act(c, 0f, "place:PlayerMark", true);
            Shot(c, "Dernier souffle", 0f, 5f, p + new Vector3(4f, 1.6f, -3f), p + new Vector3(3.2f, 1.5f, -2.2f), golem, 40f);
            Sub(c, 0.8f, 3.7f, "Gardien Ancien", "…Aelith ? Tu es… revenue…");
            Act(c, 4.5f, "show:Fragment");
            Act(c, 5.2f, "anim:player:Interact:2.8"); // Aelis reaches for the rising fragment
            Act(c, 8.3f, "anim:player:Skill:1.4");    // ...and the wind answers
            Shot(c, "Le fragment", 5f, 4.3f, p + new Vector3(-3f, 2f, 0f), p + new Vector3(-2.4f, 2.6f, 0.8f), frag, 40f);
            Act(c, 8.5f, "burst:Fragment:Anemo");
            Act(c, 8.5f, "sound:burst");
            Fade(c, 8.6f, 0.7f, Color.white, 0f, 1f);
            Act(c, 9f, "hide:Fragment", true);
            Fade(c, 9.3f, 1.2f, Color.white, 1f, 0f);
            Shot(c, "Le vent revient", 9.3f, 6.7f, arena + new Vector3(-30f, 12f, -40f), arena + new Vector3(30f, 18f, -50f), plain, 58f, 0f, false);
            Sub(c, 10.5f, 3.5f, "Aelis", "Le vent… Il chante.");
            Act(c, 15.5f, "show:Kael");
            Shot(c, "Sur la falaise", 16f, 5f, p + Vector3.up * 2.5f - Flat(cliff - p) * 2f, p + Vector3.up * 2.4f - Flat(cliff - p) * 1.6f, kaelHead, 14f);
            Sub(c, 16.5f, 2.5f, "Aelis", "…Lui !");
            Act(c, 19.2f, "anim:Kael:Skill:0.9");
            Act(c, 19.5f, "burst:KaelFX:Anemo");
            Act(c, 19.7f, "hide:Kael", true);
            End(c);
        }

        // ================================================================ CIN_05 — Il te ressemblait

        static void Epilogue(Transform parent)
        {
            var c = Begin(parent, "CIN_05_IlTeRessemblait");
            Vector3 edge = ShorePoint(new Vector3(-1f, 0f, -0.3f).normalized);
            Vector3 center = new(terrain.terrainData.size.x * 0.5f, 0f, terrain.terrainData.size.z * 0.5f);
            Vector3 outDir = Flat(edge - center);
            Vector3 right = Vector3.Cross(Vector3.up, outDir);
            Kael(c, edge, Yaw(outDir));
            var head = Target(c, "Tête Kael", edge + Vector3.up * 1.62f);
            var horizon = Target(c, "Horizon pâle", edge + outDir * 220f + Vector3.up * 12f);

            Act(c, 0f, "show:Kael", true);
            Fade(c, 0f, 2f, Color.black, 1f, 0f);
            Shot(c, "Au bord du monde", 0f, 7f, edge - outDir * 6f + Vector3.up * 2.5f, edge - outDir * 4f + Vector3.up * 2f, horizon, 45f);
            Sub(c, 1.5f, 4.5f, "Kael", "Tu as toujours été têtue, petite sœur.");
            Shot(c, "Gros plan Kael", 7f, 6f, edge + outDir * 1.4f + right * 0.4f + Vector3.up * 1.7f, edge + outDir * 1.1f + right * 0.3f + Vector3.up * 1.68f, head, 30f);
            Sub(c, 7.5f, 5f, "Kael", "Cette fois, je ne te laisserai pas te sacrifier. Même si ce monde doit s'éteindre.");
            Shot(c, "Le dernier monde", 13f, 9f, edge + outDir * 30f + Vector3.up * 15f, edge + outDir * 60f + Vector3.up * 40f, head, 55f, 0f, false);
            Fade(c, 16f, 2f, Color.black, 0f, 1f);
            Fade(c, 18f, 4f, Color.black, 1f, 1f);
            Sub(c, 18.3f, 3.5f, "", "Chapitre II — Les Cendres de Cendrelune   ·   à venir");
            End(c);
        }

        // ================================================================ Timeline helpers

        class Cine
        {
            public Transform root, cams;
            public PlayableDirector director;
            public TimelineAsset timeline;
            public CinemachineTrack camTrack;
            public SubtitleTrack subTrack;
            public FadeTrack fadeTrack;
            public CinematicActionTrack actTrack;
        }

        static Cine Begin(Transform parent, string id)
        {
            string path = $"{Dir}/{id}.playable";
            AssetDatabase.DeleteAsset(path);
            var tl = ScriptableObject.CreateInstance<TimelineAsset>();
            AssetDatabase.CreateAsset(tl, path);

            var go = new GameObject(id);
            go.transform.SetParent(parent, false);
            var dir = go.AddComponent<PlayableDirector>();
            dir.playableAsset = tl;
            dir.playOnAwake = false;
            dir.extrapolationMode = DirectorWrapMode.None;

            var cams = new GameObject("Caméras").transform;
            cams.SetParent(go.transform, false);

            var c = new Cine
            {
                root = go.transform, cams = cams, director = dir, timeline = tl,
                camTrack = tl.CreateTrack<CinemachineTrack>(null, "Caméras"),
                subTrack = tl.CreateTrack<SubtitleTrack>(null, "Sous-titres"),
                fadeTrack = tl.CreateTrack<FadeTrack>(null, "Fondus"),
                actTrack = tl.CreateTrack<CinematicActionTrack>(null, "Actions"),
            };
            var brain = Camera.main ? Camera.main.GetComponent<CinemachineBrain>() : null;
            if (brain) dir.SetGenericBinding(c.camTrack, brain);
            return c;
        }

        static void End(Cine c)
        {
            EditorUtility.SetDirty(c.timeline);
            c.root.gameObject.SetActive(false);
        }

        /// <param name="blendIn">Cross-blend from the previous shot (seconds, overlapping it).</param>
        /// <param name="noise">Subtle handheld shake (off for wide crane moves).</param>
        static CinemachineCamera Shot(Cine c, string name, float start, float duration, Vector3 from, Vector3 to, Transform lookAt, float fov, float blendIn = 0f, bool noise = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(c.cams, false);
            go.transform.position = from;
            var vcam = go.AddComponent<CinemachineCamera>();
            vcam.Lens.FieldOfView = fov;
            vcam.Lens.NearClipPlane = 0.05f;
            vcam.Lens.FarClipPlane = 4000f;
            if (lookAt)
            {
                from = Unoccluded(lookAt.position, from);
                to = Unoccluded(lookAt.position, to);
                go.transform.position = from;
                go.AddComponent<CinemachineHardLookAt>();
                vcam.LookAt = lookAt;
                go.transform.rotation = Quaternion.LookRotation(lookAt.position - from);
            }
            if (noise && handheld)
            {
                var perlin = go.AddComponent<CinemachineBasicMultiChannelPerlin>();
                perlin.NoiseProfile = handheld;
                perlin.AmplitudeGain = 0.35f;
                perlin.FrequencyGain = 0.6f;
            }
            if (from != to) Mover(c, go, start, start + duration, from, to);

            var clip = c.camTrack.CreateClip<CinemachineShot>();
            clip.displayName = name;
            clip.start = start - blendIn;
            clip.duration = duration + blendIn;
            if (blendIn > 0f) clip.blendInDuration = blendIn;
            var shot = (CinemachineShot)clip.asset;
            shot.DisplayName = name;
            shot.VirtualCamera.exposedName = GUID.Generate().ToString();
            c.director.SetReferenceValue(shot.VirtualCamera.exposedName, vcam);
            return vcam;
        }

        static void Sub(Cine c, float start, float duration, string speaker, string text)
        {
            var clip = c.subTrack.CreateClip<SubtitleClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = text.Length > 24 ? text[..24] + "…" : text;
            var a = (SubtitleClip)clip.asset;
            a.speaker = speaker;
            a.text = text;
        }

        static void Fade(Cine c, float start, float duration, Color color, float from, float to)
        {
            var clip = c.fadeTrack.CreateClip<FadeClip>();
            clip.start = start;
            clip.duration = duration;
            clip.displayName = to > from ? "Fondu sortant" : to < from ? "Fondu entrant" : "Écran";
            var a = (FadeClip)clip.asset;
            a.color = color;
            a.from = from;
            a.to = to;
        }

        static void Act(Cine c, float time, string action, bool essential = false)
        {
            var clip = c.actTrack.CreateClip<CinematicActionClip>();
            clip.start = time;
            clip.duration = 0.2f;
            clip.displayName = action;
            var a = (CinematicActionClip)clip.asset;
            a.action = action;
            a.essential = essential;
        }

        static void Mover(Cine c, GameObject go, float start, float end, Vector3 from, Vector3 to, bool ease = true)
        {
            var m = go.AddComponent<TimelineMover>();
            m.director = c.director;
            m.start = start;
            m.end = end;
            m.fromPosition = from;
            m.toPosition = to;
            m.easeIn = m.easeOut = ease;
        }

        // ================================================================ Scene helpers

        static Transform Target(Cine c, string name, Vector3 pos)
        {
            var t = new GameObject(name).transform;
            t.SetParent(c.root, false);
            t.position = pos;
            return t;
        }

        static Transform Mark(Cine c, string name, Vector3 pos, float yaw)
        {
            var t = Target(c, name, pos);
            t.rotation = Quaternion.Euler(0f, yaw, 0f);
            return t;
        }

        static Transform Star(Cine c, string name, Vector3 from, Vector3 to, float start, float end)
        {
            var go = Glow(c, name, Elements.Color(Element.Anemo), 1.3f, 25f);
            go.transform.position = from;
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.7f;
            trail.widthMultiplier = 0.6f;
            trail.widthCurve = AnimationCurve.Linear(0f, 1f, 1f, 0f);
            trail.sharedMaterial = GlowMat("Trail", new Color(0.6f, 1f, 0.95f));
            trail.minVertexDistance = 0.5f;
            Mover(c, go, start, end, from, to, false);
            go.GetComponent<TimelineMover>().easeIn = true; // accelerates as it falls
            return go.transform;
        }

        static Transform Fragment(Cine c, string name, Element e, Vector3 from, Vector3 to, float start, float end, bool ease)
        {
            var go = Glow(c, name, Elements.Color(e), 0.45f, 6f);
            go.transform.position = from;
            go.transform.localScale = new Vector3(0.35f, 0.7f, 0.35f);
            go.transform.rotation = Quaternion.Euler(0f, 45f, 0f);
            Mover(c, go, start, end, from, to, ease);
            go.SetActive(false);
            return go.transform;
        }

        static GameObject Glow(Cine c, string name, Color color, float size, float lightRange)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(c.root, false);
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().sharedMaterial = GlowMat($"Glow_{ColorUtility.ToHtmlStringRGB(color)}", color);
            var light = new GameObject("Lumière").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 6f;
            light.range = lightRange;
            return go;
        }

        static Material GlowMat(string name, Color color)
        {
            var m = ArtLibrary.LoadOrCreateMaterial($"{MatDir}/{name}.mat", "Universal Render Pipeline/Unlit");
            m.SetColor("_BaseColor", color * 2.5f);
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Kael: the hero's model, dark-clothed and silver-haired — the same face, five centuries older at heart.</summary>
        static GameObject Kael(Cine c, Vector3 pos, float yaw, bool armed = false)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterImporter.PrefabDir}/Hero.prefab");
            var root = new GameObject("Kael");
            root.transform.SetParent(c.root, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (prefab)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                    r.sharedMaterials = r.sharedMaterials.Select(KaelMaterial).ToArray();
                var rig = inst.GetComponent<Animator>();
                rig.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimationSetup.ControllerPath);
                var anim = root.AddComponent<NpcAnimator>();
                anim.rig = rig;
                anim.idleState = "FoldArms";
                anim.speakerName = "Kael";
                // His storm-violet blade, gripped like the heroes' (bind pose: blade along the thumb)
                var hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand && armed)
                {
                    var sword = ContentBuilder.Sword(Element.Electro);
                    sword.transform.SetParent(hand, false);
                    var body = inst.transform;
                    sword.transform.SetPositionAndRotation(hand.position + body.right * 0.07f - body.up * 0.03f, Quaternion.LookRotation(body.forward, body.up));
                }
            }
            root.SetActive(false);
            return root;
        }

        static Material KaelMaterial(Material src)
        {
            if (!src) return src;
            string n = src.name.ToLowerInvariant();
            Color? tint = n.Contains("short") || n.Contains("hair") ? new Color(0.92f, 0.92f, 0.98f)  // silver hair
                : n.Contains("tunic") ? new Color(0.16f, 0.13f, 0.22f)
                : n.Contains("pants") ? new Color(0.12f, 0.11f, 0.14f)
                : n.Contains("boots") ? new Color(0.1f, 0.08f, 0.08f)
                : null;
            if (tint == null) return src;
            string path = $"{MatDir}/Kael_{src.name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!m)
            {
                m = new Material(src);
                AssetDatabase.CreateAsset(m, path);
            }
            else m.CopyPropertiesFromMaterial(src);
            string prop = m.HasProperty("_BaseColor") ? "_BaseColor" : "_Color";
            m.SetColor(prop, tint.Value);
            if ((n.Contains("short") || n.Contains("hair")) && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") is Texture2D hairTex)
                m.SetTexture("_BaseMap", SilverTexture(hairTex));
            EditorUtility.SetDirty(m);
            return m;
        }

        /// <summary>Desaturated, brightened copy of a hair texture (keeps the strand alpha) — tinting alone can't lighten dark hair.</summary>
        static Texture2D SilverTexture(Texture2D src)
        {
            string srcPath = AssetDatabase.GetAssetPath(src);
            string path = $"{MatDir}/Kael_{src.name}_silver.png";
            if (!AssetDatabase.LoadAssetAtPath<Texture2D>(path))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(System.IO.File.ReadAllBytes(srcPath));
                var px = tex.GetPixels32();
                for (int i = 0; i < px.Length; i++)
                {
                    float l = (0.3f * px[i].r + 0.59f * px[i].g + 0.11f * px[i].b) / 255f;
                    float v = Mathf.Clamp01(0.55f + l * 1.6f);
                    px[i] = new Color32((byte)(v * 232), (byte)(v * 236), (byte)(v * 245), px[i].a);
                }
                tex.SetPixels32(px);
                System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
                AssetDatabase.ImportAsset(path);
                if (AssetImporter.GetAtPath(path) is TextureImporter ti)
                {
                    ti.alphaIsTransparency = true;
                    ti.SaveAndReimport();
                }
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>Ground height under p: the terrain, or a solid structure standing on it (altar plinth, tower top…).</summary>
        static Vector3 Ground(Vector3 p)
        {
            float terrainY = terrain.SampleHeight(p) + terrain.transform.position.y;
            p.y = terrainY;
            var origin = new Vector3(p.x, Mathf.Max(terrainY, p.y) + 40f, p.z);
            foreach (var hit in Physics.RaycastAll(origin, Vector3.down, 80f, ~0, QueryTriggerInteraction.Ignore).OrderBy(h => h.distance))
            {
                if (hit.collider is TerrainCollider) break;
                if (hit.collider is CapsuleCollider) continue; // tree trunks, characters
                if (hit.normal.y > 0.7f) { p.y = hit.point.y; break; }
            }
            return p;
        }

        /// <summary>Pulls a camera position toward its subject if something solid blocks the view.</summary>
        static Vector3 Unoccluded(Vector3 subject, Vector3 cam)
        {
            Vector3 d = cam - subject;
            float len = d.magnitude;
            if (len < 0.5f) return cam;
            var hits = Physics.SphereCastAll(subject, 0.3f, d / len, len, ~0, QueryTriggerInteraction.Ignore)
                .Where(h => h.distance > 0.6f && !h.collider.GetComponentInParent<PlayerController>() && !h.collider.GetComponentInParent<NPC>())
                .OrderBy(h => h.distance).ToArray();
            return hits.Length == 0 ? cam : subject + d / len * Mathf.Max(0.8f, hits[0].distance - 0.4f);
        }

        /// <summary>Removes terrain trees (and, closer in, undergrowth) around a point.</summary>
        static void ClearTrees(Vector3 center, float treeRadius, float floorRadius)
        {
            var data = terrain.terrainData;
            var size = data.size;
            var protos = data.treePrototypes;
            var kept = data.treeInstances.Where(ti =>
            {
                Vector3 wp = Vector3.Scale(ti.position, size) + terrain.transform.position;
                float dist = Vector2.Distance(new Vector2(wp.x, wp.z), new Vector2(center.x, center.z));
                var prefab = protos[ti.prototypeIndex].prefab;
                bool big = prefab && prefab.GetComponentInChildren<Collider>();
                return dist > (big ? treeRadius : floorRadius);
            }).ToArray();
            int removed = data.treeInstanceCount - kept.Length;
            if (removed == 0) return;
            data.SetTreeInstances(kept, false);
            terrain.Flush();
            var tc = terrain.GetComponent<TerrainCollider>();
            if (tc) { tc.enabled = false; tc.enabled = true; }
            EditorUtility.SetDirty(data);
            Debug.Log($"[CinematicBuilder] Clearing: {removed} trees removed around {center}.");
        }

        static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 1e-4f ? v.normalized : Vector3.forward;
        }

        static float Yaw(Vector3 dir) => Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg;

        /// <summary>Last land point above sea level walking from the island centre along <paramref name="dir"/>.</summary>
        static Vector3 ShorePoint(Vector3 dir)
        {
            var size = terrain.terrainData.size;
            Vector3 center = terrain.transform.position + new Vector3(size.x * 0.5f, 0f, size.z * 0.5f);
            Vector3 last = Ground(center);
            for (float d = 0f; d < size.x * 0.5f; d += 4f)
            {
                var p = Ground(center + dir * d);
                if (p.y < SeaLevel + 1.5f) break;
                last = p;
            }
            return last;
        }
    }
}
