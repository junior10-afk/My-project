using System.Linq;
using UnityEditor;
using UnityEngine;

namespace LastWorld.EditorTools
{
    /// <summary>The cinematics of Chapters II to X and the three endings (CIN_06 … CIN_21).</summary>
    public static partial class CinematicBuilder
    {
        // ---------------------------------------------------------------- Helpers

        static Vector3 O(Vector3 origin, float yaw, float right, float up, float fwd) => origin + Quaternion.Euler(0f, yaw, 0f) * new Vector3(right, up, fwd);

        static Transform Eye(Cine c, string name, Vector3 feet, float height = 1.62f) => Target(c, name, feet + Vector3.up * height);

        /// <summary>The hero, placed on a mark at the start of a cinematic.</summary>
        static void HeroAt(Cine c, Vector3 pos, float yaw)
        {
            Mark(c, "PlayerMark", pos, yaw);
            Act(c, 0f, "place:PlayerMark", true);
            Act(c, 0f, "player:show", true);
        }

        /// <summary>A character of the cast for one cinematic (hidden until a "show:Name" action), driven by mocap.</summary>
        static GameObject Actor(Cine c, string name, string model, Vector3 pos, float yaw, string idle = "Idle", Element? sword = null, Color? outfit = null, float scale = 1f)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{CharacterImporter.PrefabDir}/{model}.prefab");
            var root = new GameObject(name);
            root.transform.SetParent(c.root, false);
            root.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, yaw, 0f));
            if (prefab)
            {
                var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.transform);
                inst.transform.localPosition = Vector3.zero;
                inst.transform.localRotation = Quaternion.identity;
                inst.transform.localScale = Vector3.one * scale;
                if (outfit.HasValue)
                    foreach (var r in inst.GetComponentsInChildren<Renderer>(true))
                        r.sharedMaterials = r.sharedMaterials.Select(m => m && (m.name.ToLowerInvariant().Contains("tunic") || m.name.ToLowerInvariant().Contains("robe") || m.name.ToLowerInvariant().Contains("pants"))
                            ? ContentBuilder.Tinted(m, outfit.Value) : m).ToArray();
                var rig = inst.GetComponent<Animator>();
                rig.runtimeAnimatorController = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimationSetup.ControllerPath);
                var anim = root.AddComponent<NpcAnimator>();
                anim.rig = rig;
                anim.idleState = idle;
                anim.speakerName = name;
                var hand = rig.GetBoneTransform(HumanBodyBones.RightHand);
                if (sword.HasValue && hand)
                {
                    var s = ContentBuilder.Sword(sword.Value);
                    s.transform.SetParent(hand, false);
                    s.transform.SetPositionAndRotation(hand.position + inst.transform.right * 0.07f - inst.transform.up * 0.03f, Quaternion.LookRotation(inst.transform.forward, inst.transform.up));
                    s.SetActive(true);
                }
            }
            root.SetActive(false);
            return root;
        }

        /// <summary>Camera on a subject: starts at <paramref name="from"/>, drifts by <paramref name="drift"/>.</summary>
        static void Cam(Cine c, string name, float start, float duration, Vector3 subject, Vector3 from, Vector3 drift, float fov = 38f, float blend = 0f, bool noise = true)
            => Shot(c, name, start, duration, from, from + drift, Target(c, $"Cible {name}", subject), fov, blend, noise);

        static void Cam(Cine c, string name, float start, float duration, Transform subject, Vector3 from, Vector3 drift, float fov = 38f, float blend = 0f, bool noise = true)
            => Shot(c, name, start, duration, from, from + drift, subject, fov, blend, noise);

        static void Credits(Cine c, float t)
        {
            string[] lines =
            {
                "LE DERNIER MONDE",
                "Chapitres I à X — l'histoire d'Aelis, de Kaïra, de Nerys, de Torvan et de Kael",
                "Terrains, personnages, animations : Poly Haven (CC0), MakeHuman / MPFB, Quaternius (CC0)",
                "Moteur : Unity — URP, Cinemachine, Timeline",
                "Merci d'avoir joué.",
            };
            for (int i = 0; i < lines.Length; i++) Sub(c, t + i * 4.2f, 4f, "", lines[i]);
        }

        static void BuildChapters(Transform parent, Vector3 altar)
        {
            if (!ChapterBuilder.Sites.ContainsKey("forge"))
            {
                Debug.LogWarning("[CinematicBuilder] Chapter sites missing (run Build Gameplay Content): chapter cinematics skipped.");
                return;
            }
            Forge(parent);
            VentBraise(parent);
            LivreMarees(parent);
            VentMarees(parent);
            Serment(parent);
            VentOrage(parent);
            Refuge(parent);
            Sanctuaire(parent, altar);
            Retour(parent, altar);
            FaceAFace(parent);
            KaelVaincu(parent);
            Veillee(parent, altar);
            Effacement(parent);
            FinSceau(parent);
            FinPale(parent);
            FinVents(parent);
            Debug.Log("[CinematicBuilder] 16 chapter cinematics built.");
        }

        // ================================================================ CIN_06 — Kael à la Forge

        static void Forge(Transform parent)
        {
            var c = Begin(parent, "CIN_06_Forge");
            Vector3 cf = ChapterBuilder.Sites["forge"];
            Vector3 anvil = cf + new Vector3(0f, 1.4f, 12f);
            Vector3 hero = cf + new Vector3(-2.5f, 0f, -17f);
            HeroAt(c, hero, 0f);
            Actor(c, "Kaïra", "Kaira", cf + new Vector3(2.5f, 0f, -18f), 0f);
            Act(c, 0f, "show:Kaïra", true);
            Kael(c, cf + new Vector3(0f, 0f, 10f), 180f, true);
            Act(c, 0f, "show:Kael", true);
            Fragment(c, "Fragment", Element.Pyro, anvil + Vector3.up * 0.9f, anvil + Vector3.up * 0.9f, 0f, 1f, false);
            Act(c, 0f, "show:Fragment", true);
            Mark(c, "ColosseFX", cf + new Vector3(0f, 2.5f, -4f), 0f);
            Mark(c, "KaelFX", cf + new Vector3(0f, 1.2f, 10f), 0f);
            var kaelHead = Eye(c, "Tête Kael", cf + new Vector3(0f, 0f, 10f));
            var kairaHead = Eye(c, "Tête Kaïra", cf + new Vector3(2.5f, 0f, -18f));
            var heroHead = Eye(c, "Tête Aelis", hero);

            Fade(c, 0f, 1.5f, Color.black, 1f, 0f);
            Cam(c, "Le cratère", 0f, 6.5f, anvil, cf + new Vector3(-9f, 5f, -31f), new Vector3(4f, -0.8f, 4f), 40f);
            Sub(c, 1.2f, 3.8f, "Kael", "Tu es lente, petite sœur. Cinq siècles, et tu arrives encore en retard.");
            Shot(c, "Aelis", 6.5f, 3.8f, O(hero, 0f, 0.8f, 1.65f, 2.3f), O(hero, 0f, 0.6f, 1.68f, 2.0f), heroHead, 34f);
            Sub(c, 6.8f, 3f, "Aelis", "Kael. Pose ce fragment.");
            Shot(c, "Kaïra", 10.3f, 3.4f, O(cf + new Vector3(2.5f, 0f, -18f), 0f, -0.8f, 1.65f, 2.3f), O(cf + new Vector3(2.5f, 0f, -18f), 0f, -0.6f, 1.65f, 2.0f), kairaHead, 34f);
            Sub(c, 10.5f, 3f, "Kaïra", "Pose ça, ou je te le fais avaler !");
            Shot(c, "Kael", 13.7f, 8.5f, O(cf + new Vector3(0f, 0f, 10f), 180f, 0.7f, 1.7f, 2.4f), O(cf + new Vector3(0f, 0f, 10f), 180f, 0.3f, 1.66f, 1.7f), kaelHead, 32f);
            Sub(c, 14f, 3.8f, "Kael", "Un monde qui ne survit qu'en enchaînant ses dieux mérite-t-il d'être sauvé ?");
            Sub(c, 18f, 3.6f, "Kael", "Ce monde me doit cinq siècles. Qu'il se débrouille avec celui-ci.");
            Act(c, 20.2f, "anim:Kael:Attack4:1.0");
            Act(c, 21f, "burst:KaelFX:Electro");
            Act(c, 21f, "sound:slam");
            Act(c, 21.1f, "burst:ColosseFX:Pyro");
            Act(c, 21.3f, "hide:Kael", true);
            Cam(c, "Le Colosse", 22.2f, 5f, cf + new Vector3(0f, 3.4f, -4f), O(hero, 0f, 0f, 2.1f, -5.5f), new Vector3(0f, 0.6f, 2.5f), 42f);
            End(c);
        }

        // ================================================================ CIN_07 — Le vent de braise

        static void VentBraise(Transform parent)
        {
            var c = Begin(parent, "CIN_07_VentBraise");
            Vector3 cf = ChapterBuilder.Sites["forge"];
            Vector3 village = ChapterBuilder.Sites["cendrelune"];
            Vector3 chest = cf + new Vector3(0f, 1.4f, 12f);
            Vector3 hero = cf + new Vector3(-1f, 0f, 6f);
            HeroAt(c, hero, 0f);
            Actor(c, "Kaïra", "Kaira", cf + new Vector3(2f, 0f, 5f), 0f);
            Act(c, 0f, "show:Kaïra", true);
            Fragment(c, "Fragment", Element.Pyro, chest + Vector3.up * 0.8f, cf + new Vector3(0f, 22f, 12f), 1.5f, 8f, true);
            Mark(c, "FlameFX", cf + new Vector3(0f, 22f, 12f), 0f);
            Mark(c, "VillageFX", village + Vector3.up * 3f, 0f);
            var kairaHead = Eye(c, "Tête Kaïra", cf + new Vector3(2f, 0f, 5f));
            var village_t = Target(c, "Village", village + Vector3.up * 6f);
            var up = Target(c, "Ciel", cf + new Vector3(0f, 22f, 12f));

            Fade(c, 0f, 1.5f, Color.black, 1f, 0f);
            Act(c, 1.3f, "show:Fragment");
            Act(c, 1.4f, "anim:player:Interact:2.6");
            Cam(c, "Le fragment monte", 0f, 8f, up, cf + new Vector3(-6f, 2f, 0f), new Vector3(1f, 3.5f, 1f), 48f);
            Act(c, 8.2f, "burst:FlameFX:Pyro");
            Act(c, 8.2f, "sound:burst");
            Fade(c, 8.4f, 0.7f, Color.white, 0f, 1f);
            Act(c, 8.9f, "hide:Fragment", true);
            Fade(c, 9.1f, 1.3f, Color.white, 1f, 0f);
            Shot(c, "Cendrelune respire", 9.1f, 7.5f, cf + new Vector3(-6f, 14f, -26f), village + new Vector3(10f, 28f, -60f), village_t, 52f, 0f, false);
            Act(c, 10.5f, "burst:VillageFX:Pyro");
            Sub(c, 9.6f, 3.6f, "Kaïra", "Tu sens ça ? La chaleur… elle revient.");
            Sub(c, 13.4f, 3.2f, Voice, "…Grande sœur ?");
            Shot(c, "Kaïra écoute", 16.7f, 5.2f, O(cf + new Vector3(2f, 0f, 5f), 0f, -0.9f, 1.64f, 2.1f), O(cf + new Vector3(2f, 0f, 5f), 0f, -0.6f, 1.64f, 1.7f), kairaHead, 33f);
            Sub(c, 17f, 2.5f, "Kaïra", "Ilio… Il est vivant.");
            Sub(c, 19.6f, 2.6f, "Aelis", "Alors on le retrouvera.");
            Act(c, 17f, "banner:Le Vent-de-Braise est libéré");
            End(c);
        }

        // ================================================================ CIN_08 — Le Livre des Marées

        static void LivreMarees(Transform parent)
        {
            var c = Begin(parent, "CIN_08_LivreMarees");
            Vector3 isl = ChapterBuilder.Sites["archives"];
            Vector3 lectern = isl + new Vector3(0f, 0f, 6f);
            Vector3 hero = lectern + new Vector3(0f, 0f, -1.6f);
            HeroAt(c, hero, 0f);
            Vector3 vis = isl + new Vector3(-8f, 0.3f, -10f);
            Actor(c, "Aelith", "Hero", vis, 0f, "Idle", null, new Color(0.9f, 0.95f, 1f));
            Actor(c, "Kael jeune", "Hero", vis + new Vector3(2.4f, 0f, 0f), 270f, "FoldArms");
            var bookT = Target(c, "Livre", lectern + Vector3.up * 1.7f);
            var aelithHead = Eye(c, "Tête Aelith", vis);
            var kaelHead = Eye(c, "Tête Kael jeune", vis + new Vector3(2.4f, 0f, 0f));
            var heroHead = Eye(c, "Tête Aelis", hero);

            Fade(c, 0f, 1.2f, Color.black, 1f, 0f);
            Cam(c, "Le Livre", 0f, 5f, lectern + Vector3.up * 1.8f, O(lectern, 0f, 0.5f, 2.3f, -1.6f), new Vector3(0f, -0.3f, 0.7f), 36f);
            Act(c, 1.2f, "anim:player:Interact:2.8");
            Fade(c, 3.8f, 1f, Color.white, 0f, 1f);
            Act(c, 4.6f, "show:Aelith", true);
            Act(c, 4.6f, "show:Kael jeune", true);
            Fade(c, 4.8f, 1.2f, Color.white, 1f, 0.28f);            // the memory: bathed in a pale light
            Fade(c, 6f, 17f, new Color(0.95f, 0.85f, 0.65f), 0.28f, 0.28f);
            Shot(c, "La mémoire", 4.8f, 6f, isl + new Vector3(-22f, 4f, -22f), isl + new Vector3(-20f, 3.2f, -19f), Target(c, "Les jumeaux", vis + new Vector3(1.2f, 1.5f, 0f)), 44f);
            Sub(c, 5.2f, 4.4f, "Sael", "Il y a cinq cents ans, Aelith et Kael veillaient sur le dernier crépuscule.");
            Shot(c, "Kael", 10.8f, 5.5f, O(vis + new Vector3(2.4f, 0f, 0f), 270f, 0.7f, 1.66f, 2.1f), O(vis + new Vector3(2.4f, 0f, 0f), 270f, 0.4f, 1.66f, 1.6f), kaelHead, 33f);
            Sub(c, 11f, 4.2f, "Kael jeune", "Prends ma vie à la place de la tienne. Je suis le premier-né.");
            Shot(c, "Aelith", 16.3f, 6f, O(vis, 0f, 0.7f, 1.66f, 2.2f), O(vis, 0f, 0.4f, 1.66f, 1.7f), aelithHead, 33f);
            Sub(c, 16.5f, 4.3f, "Aelith", "Tu n'es pas un Gardien, Kael. Tu es mon frère. C'est pour cela que c'est à moi d'y aller.");
            Sub(c, 21f, 3.2f, "Kael jeune", "Alors je te reprendrai à ce monde. Je te le jure.");
            Sub(c, 24.5f, 3f, "Aelith", "Sois patient, mon frère. Je serai là.");
            Fade(c, 26.5f, 1.2f, Color.white, 0.28f, 1f);
            Act(c, 27.6f, "hide:Aelith", true);
            Act(c, 27.6f, "hide:Kael jeune", true);
            Fade(c, 27.8f, 1.5f, Color.white, 1f, 0f);
            Cam(c, "Aelis", 27.8f, 4.5f, hero + Vector3.up * 1.62f, O(hero, 0f, 0.5f, 1.65f, 2.3f), new Vector3(0f, 0f, -0.5f), 34f);
            Sub(c, 28.4f, 3.5f, "Aelis", "…Je m'appelais Aelith.");
            End(c);
        }

        // ================================================================ CIN_09 — Le vent des marées

        static void VentMarees(Transform parent)
        {
            var c = Begin(parent, "CIN_09_VentMarees");
            Vector3 isl = ChapterBuilder.Sites["archives"];
            Vector3 chest = isl + new Vector3(0f, 0.35f, 12f);
            Vector3 hero = isl + new Vector3(0f, 0.3f, 6f);
            HeroAt(c, hero, 0f);
            Actor(c, "Nerys", "Nerys", isl + new Vector3(2.5f, 0.3f, 5f), 0f);
            Act(c, 0f, "show:Nerys", true);
            Fragment(c, "Fragment", Element.Hydro, chest + Vector3.up * 0.8f, isl + new Vector3(0f, 24f, 12f), 1.5f, 8f, true);
            Mark(c, "WaterFX", isl + new Vector3(0f, 24f, 12f), 0f);
            Mark(c, "LagoonFX", isl + new Vector3(0f, 2f, 40f), 0f);
            var up = Target(c, "Ciel", isl + new Vector3(0f, 24f, 12f));
            var lagoon = Target(c, "Lagune", isl + new Vector3(30f, 2f, 0f));
            var nerysHead = Eye(c, "Tête Nerys", isl + new Vector3(2.5f, 0.3f, 5f));

            Fade(c, 0f, 1.5f, Color.black, 1f, 0f);
            Act(c, 1.3f, "show:Fragment");
            Act(c, 1.4f, "anim:player:Interact:2.6");
            Cam(c, "Le fragment monte", 0f, 8f, up, isl + new Vector3(-7f, 2f, 3f), new Vector3(1f, 3f, 2f), 48f);
            Act(c, 8.2f, "burst:WaterFX:Hydro");
            Act(c, 8.2f, "sound:burst");
            Fade(c, 8.4f, 0.7f, Color.white, 0f, 1f);
            Act(c, 8.9f, "hide:Fragment", true);
            Fade(c, 9.1f, 1.3f, Color.white, 1f, 0f);
            Shot(c, "La lagune", 9.1f, 7.5f, isl + new Vector3(-30f, 12f, -34f), isl + new Vector3(10f, 18f, -62f), lagoon, 55f, 0f, false);
            Act(c, 10.5f, "burst:LagoonFX:Hydro");
            Sub(c, 9.6f, 3.8f, "Nerys", "On dirait qu'elle pleure de soulagement.");
            Shot(c, "Nerys", 16.7f, 5.2f, O(isl + new Vector3(2.5f, 0.3f, 5f), 0f, -0.8f, 1.65f, 2.1f), O(isl + new Vector3(2.5f, 0.3f, 5f), 0f, -0.5f, 1.65f, 1.7f), nerysHead, 33f);
            Sub(c, 17f, 4.2f, "Nerys", "Pour une fois… je n'ai rien à ajouter.");
            Act(c, 17f, "banner:Le Vent-des-Marées est libéré");
            End(c);
        }

        // ================================================================ CIN_10 — Le Serment (Kael vs la Veille)

        static void Serment(Transform parent)
        {
            var c = Begin(parent, "CIN_10_Serment");
            Vector3 gate = ChapterBuilder.Sites["citadel"];
            Vector3 hero = gate + new Vector3(-9f, 0f, 1f);
            HeroAt(c, hero, 90f);
            Actor(c, "Torvan", "Torvan", gate + new Vector3(-7.5f, 0f, -2.5f), 90f);
            Actor(c, "Kaïra", "Kaira", gate + new Vector3(-9.5f, 0f, -3.5f), 90f);
            Act(c, 0f, "show:Torvan", true);
            Act(c, 0f, "show:Kaïra", true);
            Vector3 kp = gate + new Vector3(8f, 0f, 0f);
            Kael(c, kp, 270f, true);
            Act(c, 0f, "show:Kael", true);
            Mark(c, "KaelFX", kp + Vector3.up * 1.2f, 0f);
            Mark(c, "TorvanFX", gate + new Vector3(-3f, 1.2f, -2.5f), 0f);
            var kaelHead = Eye(c, "Tête Kael", kp);
            var torvanHead = Eye(c, "Tête Torvan", gate + new Vector3(-7.5f, 0f, -2.5f));
            var mid = Target(c, "Entre eux", gate + new Vector3(-1f, 1.5f, 0f));

            Fade(c, 0f, 1.3f, Color.black, 1f, 0f);
            Cam(c, "La porte", 0f, 6f, mid, gate + new Vector3(-4f, 3.5f, 22f), new Vector3(-1f, -0.5f, -4f), 44f);
            Sub(c, 1f, 3.8f, "Kael", "Torvan. Tu as vieilli. Pas moi : c'est ma malédiction.");
            Sub(c, 4.9f, 3.2f, "Torvan", "Tu avais prêté serment à la Veille, garçon.");
            Shot(c, "Kael", 6f, 6.5f, O(kp, 270f, 0.6f, 1.68f, 2.3f), O(kp, 270f, 0.3f, 1.66f, 1.8f), kaelHead, 32f);
            Sub(c, 8.2f, 3.4f, "Kael", "J'ai prêté serment à ma sœur. C'est tout ce qui compte.");
            Sub(c, 11.7f, 4f, "Kael", "Un monde qui ne survit qu'en enchaînant ses dieux, Aelis… mérite-t-il d'être sauvé ?");
            Sub(c, 15.9f, 3.8f, "Aelis", "Je ne sais pas encore. Mais on ne décide pas ça seul.");
            Cam(c, "Face à face", 15.6f, 4.5f, mid, gate + new Vector3(-1f, 2.6f, 24f), new Vector3(0f, 0.3f, -3f), 44f);
            Sub(c, 19.9f, 2.4f, "Kael", "Tu parles comme elle.");
            Act(c, 21.2f, "anim:Kael:Attack4:1.0");
            Act(c, 21.8f, "burst:KaelFX:Electro");
            Act(c, 21.8f, "sound:slam");
            Act(c, 21.9f, "anim:Torvan:Parry:0.6");
            Act(c, 22f, "burst:TorvanFX:Electro");
            Shot(c, "Torvan", 21.4f, 4f, O(gate + new Vector3(-7.5f, 0f, -2.5f), 90f, 0.8f, 1.7f, 2.2f), O(gate + new Vector3(-7.5f, 0f, -2.5f), 90f, 0.5f, 1.68f, 1.8f), torvanHead, 34f);
            Sub(c, 22.6f, 3f, "Torvan", "Hmpf. Pas un pas de plus.");
            Sub(c, 25.8f, 4f, "Kael", "Pas aujourd'hui. Venez au Cœur du Sceau quand vous aurez les quatre. Je vous y attendrai.");
            Act(c, 29f, "burst:KaelFX:Electro");
            Act(c, 29.1f, "hide:Kael", true);
            Cam(c, "Il est parti", 28.8f, 4.5f, mid, gate + new Vector3(-4f, 2.6f, 22f), new Vector3(0f, 0.5f, -1f), 44f);
            End(c);
        }

        // ================================================================ CIN_11 — Le vent d'orage

        static void VentOrage(Transform parent)
        {
            var c = Begin(parent, "CIN_11_VentOrage");
            Vector3 v = ChapterBuilder.Sites["valdrak"];
            Vector3 chest = v + new Vector3(26f, 0.3f, -6f);
            Vector3 hero = v + new Vector3(18f, 0f, -4f);
            HeroAt(c, hero, 90f);
            Actor(c, "Torvan", "Torvan", v + new Vector3(18f, 0f, -8f), 90f);
            Act(c, 0f, "show:Torvan", true);
            Vector3 sky = v + new Vector3(18f, 26f, -4f);
            Fragment(c, "Fragment", Element.Electro, chest + Vector3.up * 0.9f, sky, 1.5f, 6.5f, true);
            Fragment(c, "Fragment Braise", Element.Pyro, sky + new Vector3(-9f, -3f, 0f), sky + new Vector3(-1.2f, 0f, 0f), 6.5f, 11f, true);
            Fragment(c, "Fragment Marée", Element.Hydro, sky + new Vector3(0f, -3f, 9f), sky + new Vector3(0f, 0f, 1.2f), 6.5f, 11f, true);
            Fragment(c, "Fragment Vent", Element.Anemo, sky + new Vector3(0f, -3f, -9f), sky + new Vector3(0f, 0f, -1.2f), 6.5f, 11f, true);
            Mark(c, "UnionFX", sky, 0f);
            var up = Target(c, "Ciel", sky);
            var torvanHead = Eye(c, "Tête Torvan", v + new Vector3(18f, 0f, -8f));

            Fade(c, 0f, 1.5f, Color.black, 1f, 0f);
            Act(c, 1.3f, "show:Fragment");
            Act(c, 1.4f, "anim:player:Interact:2.6");
            Cam(c, "Le fragment monte", 0f, 6.5f, up, v + new Vector3(10f, 2f, -10f), new Vector3(1f, 4f, 1f), 50f);
            Act(c, 6.5f, "show:Fragment Braise");
            Act(c, 6.5f, "show:Fragment Marée");
            Act(c, 6.5f, "show:Fragment Vent");
            Cam(c, "Les quatre fragments", 6.5f, 6f, up, v + new Vector3(8f, 10f, -14f), new Vector3(1f, 4f, 3f), 55f, 0.6f);
            Sub(c, 7f, 3.6f, "Aelis", "Ils chantent… tous les quatre. Ensemble.");
            Act(c, 11.8f, "burst:UnionFX:Anemo");
            Act(c, 11.8f, "sound:burst");
            Fade(c, 12f, 0.7f, Color.white, 0f, 1f);
            Act(c, 12.5f, "hide:Fragment", true);
            Act(c, 12.5f, "hide:Fragment Braise", true);
            Act(c, 12.5f, "hide:Fragment Marée", true);
            Act(c, 12.5f, "hide:Fragment Vent", true);
            Fade(c, 12.7f, 1.3f, Color.white, 1f, 0f);
            Shot(c, "Torvan", 12.7f, 5.5f, O(v + new Vector3(18f, 0f, -8f), 90f, 0.9f, 1.7f, 2.3f), O(v + new Vector3(18f, 0f, -8f), 90f, 0.6f, 1.68f, 1.8f), torvanHead, 34f);
            Sub(c, 13f, 4f, "Torvan", "Hmpf. Quatre vents libres. Je n'aurais jamais cru voir ça de mon vivant.");
            Cam(c, "Vers le centre", 18.2f, 5.5f, v + new Vector3(-250f, -60f, -40f), v + new Vector3(10f, 6f, -2f), new Vector3(-2f, 1f, 0f), 50f, 0.5f, false);
            Sub(c, 18.6f, 4.4f, "Aelis", "Ils pointent tous vers le centre de l'île.");
            Act(c, 18.4f, "banner:Les quatre vents sont libres");
            End(c);
        }

        // ================================================================ CIN_12 — Le refuge (Kaïra et Ilio)

        static void Refuge(Transform parent)
        {
            var c = Begin(parent, "CIN_12_Refuge");
            Vector3 rp = ChapterBuilder.Sites["refuge"];
            Vector3 ilio = rp + new Vector3(1.8f, 0f, 0.8f);
            Vector3 hero = rp + new Vector3(-2.5f, 0f, -6f);
            HeroAt(c, hero, 0f);
            Vector3 kp = rp + new Vector3(0.4f, 0f, -3.8f);
            Actor(c, "Kaïra", "Kaira", kp, 15f);
            Act(c, 0f, "show:Kaïra", true);
            Act(c, 0.1f, "npcface:ilio");
            var kairaHead = Eye(c, "Tête Kaïra", kp);
            var ilioHead = Eye(c, "Tête Ilio", ilio, 1.0f);
            var lantern = Target(c, "Lanterne", rp + Vector3.up * 3.3f);

            Fade(c, 0f, 1.6f, Color.white, 1f, 0f);
            Cam(c, "La lanterne", 0f, 5f, lantern, rp + new Vector3(-12f, 2f, -18f), new Vector3(4f, 1f, 6f), 40f);
            Sub(c, 1.5f, 3f, "Ilio", "Qui est là ? Si c'est encore un rêve, vous êtes très réalistes.");
            Shot(c, "Kaïra", 5f, 4.2f, O(kp, 15f, 0.8f, 1.65f, 2.2f), O(kp, 15f, 0.5f, 1.62f, 1.7f), kairaHead, 33f);
            Act(c, 5.5f, "anim:Kaïra:Crouch:3");
            Sub(c, 5.4f, 3.4f, "Kaïra", "…Ilio.");
            Shot(c, "Ilio", 9.2f, 4.5f, O(ilio, 200f, -0.6f, 1.2f, 1.5f), O(ilio, 200f, -0.4f, 1.1f, 1.2f), ilioHead, 32f);
            Sub(c, 9.5f, 3.2f, "Ilio", "Tu es en retard, grande sœur.");
            Sub(c, 12.8f, 3f, "Kaïra", "Je sais. Je suis désolée. Je suis tellement désolée.");
            Cam(c, "Réunis", 13.7f, 4.5f, ilio + Vector3.up * 1f, rp + new Vector3(-6f, 1.8f, -8f), new Vector3(2.5f, 0.2f, 3f), 42f);
            End(c);
        }

        // ================================================================ CIN_13 — Le Sanctuaire des Quatre Vents

        static void Sanctuaire(Transform parent, Vector3 altar)
        {
            var c = Begin(parent, "CIN_13_Sanctuaire");
            Vector3 hero = Ground(altar + new Vector3(0f, 0f, -9f));
            HeroAt(c, hero, 0f);
            Vector3 sky = ChapterBuilder.Sites["sanctuaire"];
            Vector3 skyMark = ChapterBuilder.Sites["sanctuaire_mark"];
            Mark(c, "SkyMark", skyMark, 0f);
            Color[] cols = { Elements.Color(Element.Anemo), new Color(1f, 0.45f, 0.15f), new Color(0.35f, 0.75f, 1f), new Color(0.7f, 0.55f, 1f) };
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f + 45f;
                var from = altar + Vector3.up * 12f + Quaternion.Euler(0f, a, 0f) * Vector3.forward * 9f;
                Fragment(c, $"Fragment {i}", (Element)new[] { Element.Anemo, Element.Pyro, Element.Hydro, Element.Electro }[i], from, altar + Vector3.up * 8f, 0.5f + i * 0.4f, 6f, true);
                Act(c, 0.4f, $"show:Fragment {i}");
            }
            Mark(c, "AltarFX", altar + Vector3.up * 8f, 0f);
            var altarT = Target(c, "Autel", altar + Vector3.up * 6f);
            var skyT = Target(c, "Sanctuaire", sky + Vector3.up * 5f);

            Fade(c, 0f, 1.4f, Color.black, 1f, 0f);
            Act(c, 0.6f, "anim:player:Interact:3.0");
            Cam(c, "L'Autel", 0f, 7f, altarT, hero + new Vector3(-9f, 3.5f, -12f), new Vector3(3f, 2f, 3f), 46f);
            Sub(c, 1.5f, 4f, "Elara", "Que le vent te garde, Aelis. Va. Je veille.");
            Act(c, 6.4f, "burst:AltarFX:Anemo");
            Act(c, 6.4f, "sound:burst");
            Fade(c, 6.6f, 1f, Color.white, 0f, 1f);
            for (int i = 0; i < 4; i++) Act(c, 7.5f, $"hide:Fragment {i}");
            Act(c, 7.6f, "place:SkyMark", true);
            Act(c, 7.7f, "respawn:SkyMark", true);
            Fade(c, 8f, 1.6f, Color.white, 1f, 0f);
            Shot(c, "Le Sanctuaire", 8f, 8f, sky + new Vector3(-45f, 22f, -60f), sky + new Vector3(30f, 10f, -48f), skyT, 50f, 0f, false);
            Sub(c, 9f, 4f, Voice, "Bienvenue, petite du vent. Nous t'attendions.");
            Act(c, 9f, "banner:Sanctuaire des Quatre Vents");
            End(c);
        }

        // ================================================================ CIN_14 — Le retour

        static void Retour(Transform parent, Vector3 altar)
        {
            var c = Begin(parent, "CIN_14_Retour");
            Vector3 hero = Ground(altar + new Vector3(1.5f, 0f, -6f));
            Mark(c, "PlayerMark", hero, 0f);
            Vector3 sky = ChapterBuilder.Sites["sanctuaire"];
            Act(c, 0f, "time:6.4", true);
            Fade(c, 0f, 1f, Color.white, 1f, 1f);
            Act(c, 0.6f, "place:PlayerMark", true);
            Act(c, 0.7f, "player:show", true);
            Act(c, 0.7f, "respawn:PlayerMark", true);
            Act(c, 0.8f, "anim:player:LayToIdle:3.6");
            Fade(c, 1f, 2f, Color.white, 1f, 0f);
            var head = Eye(c, "Tête Aelis", hero);
            Cam(c, "À l'aube", 0.8f, 4f, hero + Vector3.up * 0.5f, hero + new Vector3(2.2f, 1.6f, 2.6f), new Vector3(-0.6f, 0.1f, -0.4f), 38f);
            Sub(c, 1.5f, 3.4f, "Aelith", "Je suis là. Je ne suis pas partie.");
            Shot(c, "Elle sourit", 4.8f, 4.5f, O(hero, 0f, 0.7f, 1.66f, 2.3f), O(hero, 0f, 0.4f, 1.66f, 1.8f), head, 34f);
            Sub(c, 5.2f, 3.6f, "Aelis", "Aelith… Aelis… Peu importe. Nous sommes deux, maintenant.");
            Shot(c, "Le jour se lève", 9.3f, 5f, altar + new Vector3(-26f, 8f, -34f), altar + new Vector3(-18f, 14f, -50f), Target(c, "Horizon", altar + new Vector3(60f, 14f, 200f)), 55f, 0f, false);
            End(c);
        }

        // ================================================================ CIN_15 — Face à face (Kael, sommet de la Tour)

        static void FaceAFace(Transform parent)
        {
            var c = Begin(parent, "CIN_15_FaceAFace");
            Vector3 s = ChapterBuilder.Sites["sceau_summit"];
            Vector3 hero = s + new Vector3(0f, 0f, -12f);
            HeroAt(c, hero, 0f);
            Actor(c, "Kaïra", "Kaira", s + new Vector3(-4f, 0f, -14f), 0f);
            Actor(c, "Nerys", "Nerys", s + new Vector3(4f, 0f, -14f), 0f);
            Actor(c, "Torvan", "Torvan", s + new Vector3(0f, 0f, -16f), 0f);
            foreach (var n in new[] { "Kaïra", "Nerys", "Torvan" }) Act(c, 0f, $"show:{n}", true);
            Vector3 kp = s + new Vector3(0f, 0f, 9f);
            var kaelHead = Eye(c, "Tête Kael", kp);
            var heroHead = Eye(c, "Tête Aelis", hero);
            var between = Target(c, "Entre eux", s + new Vector3(0f, 1.6f, -1f));

            Fade(c, 0f, 1.5f, Color.black, 1f, 0f);
            Cam(c, "Le sommet", 0f, 6f, between, s + new Vector3(-14f, 5f, -22f), new Vector3(4f, 0f, 5f), 42f);
            Sub(c, 1.2f, 3.4f, "Kael", "Tu es venue, petite sœur.");
            Sub(c, 4.8f, 3.4f, "Aelis", "Je m'appelle Aelis. Mais oui. Je suis venue.");
            Shot(c, "Kael", 6.2f, 8f, O(kp, 180f, 0.8f, 1.7f, 2.6f), O(kp, 180f, 0.3f, 1.66f, 1.9f), kaelHead, 32f);
            Sub(c, 6.6f, 4.6f, "Kael", "Tu ne te souviens pas de la veille de ton sacrifice. Moi, je me souviens de chaque heure.");
            Sub(c, 11.4f, 3.4f, "Kael", "Je ne t'ai pas sauvée pour te voir t'enchaîner à nouveau.");
            Shot(c, "Aelis", 14.4f, 7f, O(hero, 0f, 0.8f, 1.66f, 2.4f), O(hero, 0f, 0.4f, 1.66f, 1.9f), heroHead, 33f);
            Sub(c, 14.8f, 3.4f, "Aelis", "Si je ne le fais pas, Aerendal s'éteint.");
            Sub(c, 18.4f, 3f, "Kael", "Alors qu'il s'éteigne. Mille autres mondes l'ont fait avant lui.");
            Sub(c, 21.6f, 4f, "Aelis", "Il y a une troisième voie. Elle est derrière moi : ils sont quatre, et ils ne sont pas des dieux.");
            Cam(c, "Les quatre", 21.4f, 4.5f, s + new Vector3(0f, 1.6f, -15f), s + new Vector3(-6f, 2.2f, -4f), new Vector3(1.5f, 0f, 0.5f), 42f, 0.5f);
            Sub(c, 25.8f, 3.2f, "Kael", "Alors prouve-le. Tire l'épée.");
            Cam(c, "Il dégaine", 25.6f, 4.5f, kp + Vector3.up * 1.5f, O(kp, 180f, 3.2f, 1.6f, 6f), new Vector3(-0.6f, 0f, -1f), 36f, 0.4f);
            End(c);
        }

        // ================================================================ CIN_16 — Kael vaincu

        static void KaelVaincu(Transform parent)
        {
            var c = Begin(parent, "CIN_16_KaelVaincu");
            Vector3 s = ChapterBuilder.Sites["sceau_summit"];
            Vector3 hero = s + new Vector3(0f, 0f, -2f);
            HeroAt(c, hero, 0f);
            Act(c, 0f, "clearboss:boss_kael", true);
            Vector3 kp = s + new Vector3(0f, 0f, 3.5f);
            Kael(c, kp, 180f, false);
            var kael = c.root.Find("Kael");
            if (kael) kael.GetComponent<NpcAnimator>().idleState = "Crouch";
            Act(c, 0f, "show:Kael", true);
            Mark(c, "KaelFX", kp + Vector3.up * 1.2f, 0f);
            var kaelHead = Eye(c, "Tête Kael", kp, 1.0f);
            var heroHead = Eye(c, "Tête Aelis", hero);
            var horizon = Target(c, "Horizon", s + new Vector3(0f, 6f, 400f));

            Fade(c, 0f, 1f, Color.black, 1f, 0f);
            Shot(c, "Kael à genoux", 0f, 6.5f, O(kp, 180f, 1.2f, 1.5f, 3f), O(kp, 180f, 0.7f, 1.3f, 2.2f), kaelHead, 34f);
            Sub(c, 1f, 3.2f, "Kael", "…Tu te bats comme elle.");
            Sub(c, 4.4f, 2.6f, "Aelis", "Je me bats comme moi.");
            Shot(c, "Aelis", 6.5f, 6f, O(hero, 0f, 0.7f, 1.66f, 2.3f), O(hero, 0f, 0.4f, 1.66f, 1.8f), heroHead, 33f);
            Sub(c, 6.8f, 3.4f, "Kael", "Oui. C'est bien ce qui me terrifie.");
            Sub(c, 10.4f, 3.4f, "Aelis", "Viens avec nous. Ne reste pas seul une fois de plus.");
            Cam(c, "Kael", 12.7f, 6f, kp + Vector3.up * 1.1f, O(kp, 180f, 0.8f, 1.4f, 2.4f), new Vector3(0f, 0.1f, 0.4f), 33f);
            Sub(c, 13f, 4.4f, "Kael", "Pas encore. Je dois voir de mes yeux que ta troisième voie n'est pas une jolie histoire.");
            Act(c, 17.8f, "burst:KaelFX:Electro");
            Act(c, 17.9f, "hide:Kael", true);
            Fade(c, 18f, 0.5f, Color.white, 0f, 0.5f);
            Fade(c, 18.5f, 1f, Color.white, 0.5f, 0f);
            Shot(c, "Le mur blanc", 18.6f, 7.5f, s + new Vector3(-6f, 9f, 4f), s + new Vector3(-2f, 12f, 8f), horizon, 52f, 0f, false);
            Sub(c, 19.2f, 4.2f, "Nerys", "La Pâleur a franchi le dôme…");
            Act(c, 19.2f, "banner:La Pâleur a franchi le dôme");
            End(c);
        }

        // ================================================================ CIN_17 — La veillée

        static void Veillee(Transform parent, Vector3 altar)
        {
            var c = Begin(parent, "CIN_17_Veillee");
            Vector3 fire = Ground(altar + new Vector3(0f, 0f, -16f));
            Act(c, 0f, "time:23.2", true);
            Vector3 P(float a, float r) => fire + Quaternion.Euler(0f, a, 0f) * Vector3.forward * r;
            Vector3 hero = P(200f, 3.4f);
            HeroAt(c, hero, 20f);
            Actor(c, "Kaïra", "Kaira", P(260f, 3.4f), 80f, "Idle");
            Actor(c, "Nerys", "Nerys", P(320f, 3.4f), 140f, "Idle");
            Actor(c, "Torvan", "Torvan", P(20f, 3.4f), 200f, "FoldArms");
            Actor(c, "Elara", "Elara", P(80f, 3.4f), 260f, "Idle");
            foreach (var n in new[] { "Kaïra", "Nerys", "Torvan", "Elara" }) Act(c, 0f, $"show:{n}", true);
            Vector3 kp = P(150f, 9f);
            Kael(c, kp, 330f, false);
            // the fire
            var flame = Glow(c, "Feu", new Color(1f, 0.6f, 0.2f), 0.9f, 14f);
            flame.transform.position = fire + Vector3.up * 0.6f;
            flame.transform.localScale = new Vector3(0.7f, 1f, 0.7f);
            var fireT = Target(c, "Le feu", fire + Vector3.up * 1f);
            Mark(c, "DawnFX", fire + Vector3.up * 1f, 0f);
            Vector3 kc = Vector3.zero;

            Fade(c, 0f, 2f, Color.black, 1f, 0f);
            Cam(c, "Autour du feu", 0f, 8.5f, fireT.position, fire + new Vector3(-6f, 3.2f, -9f), new Vector3(2f, -0.6f, 2f), 44f);
            Sub(c, 1.5f, 3.6f, "Kaïra", "Allez, un toast ! Même si c'est de l'eau.");
            Sub(c, 5.4f, 3.4f, "Nerys", "Je note tout. Mentalement. Pour plus tard.");
            Shot(c, "Torvan", 8.8f, 4.5f, O(P(20f, 3.4f), 200f, 0.8f, 1.7f, 2.2f), O(P(20f, 3.4f), 200f, 0.5f, 1.68f, 1.8f), Eye(c, "Tête Torvan", P(20f, 3.4f)), 34f);
            Sub(c, 9.1f, 2.4f, "Torvan", "Hmpf.");
            Sub(c, 11.7f, 3.4f, "Elara", "C'est le premier soir où je n'ai pas peur de demain.");
            Act(c, 13f, "show:Kael", true);
            Cam(c, "Quelqu'un approche", 13.3f, 5.5f, kp + Vector3.up * 1.5f, fire + new Vector3(-5f, 2.2f, -4f), new Vector3(1f, 0.3f, 1f), 40f);
            Sub(c, 13.8f, 3.4f, "Kael", "Je ne viens pas me battre.");
            Sub(c, 17.4f, 3f, "Torvan", "Hmpf. Assieds-toi.");
            Sub(c, 20.6f, 3f, "Kael", "…Merci.");
            Shot(c, "Aelis", 23.6f, 5.5f, O(hero, 20f, 0.8f, 1.66f, 2.3f), O(hero, 20f, 0.4f, 1.66f, 1.8f), Eye(c, "Tête Aelis", hero), 33f);
            Sub(c, 23.9f, 4.2f, "Aelis", "Demain, on décide tous ensemble. Ni toi seul, ni moi seule.");
            Fade(c, 29f, 2f, Color.black, 0f, 1f);
            Act(c, 31f, "time:6.0", true);
            Fade(c, 31.2f, 2f, Color.black, 1f, 0f);
            Cam(c, "L'aube", 31.2f, 5f, fire + new Vector3(0f, 8f, 40f), fire + new Vector3(-12f, 3f, -20f), new Vector3(3f, 2f, 3f), 50f, 0f, false);
            Act(c, 31.5f, "banner:L'aube du dernier jour");
            End(c);
        }

        // ================================================================ CIN_18 — L'Effacement

        static void Effacement(Transform parent)
        {
            var c = Begin(parent, "CIN_18_Effacement");
            Vector3 s = ChapterBuilder.Sites["sceau_summit"];
            Vector3 hero = s + new Vector3(0f, 0f, -14f);
            HeroAt(c, hero, 0f);
            Actor(c, "Kaïra", "Kaira", s + new Vector3(-4f, 0f, -15f), 0f);
            Actor(c, "Nerys", "Nerys", s + new Vector3(4f, 0f, -15f), 0f);
            Actor(c, "Torvan", "Torvan", s + new Vector3(0f, 0f, -17f), 0f);
            Kael(c, s + new Vector3(-7f, 0f, -13f), 0f, true);
            foreach (var n in new[] { "Kaïra", "Nerys", "Torvan", "Kael" }) Act(c, 0f, $"show:{n}", true);
            Act(c, 0f, "time:6.3", true);
            var wall = Target(c, "Mur blanc", s + new Vector3(0f, 10f, 300f));
            var heart = Target(c, "Cœur du Sceau", s + new Vector3(0f, 2f, 8f));
            var heroHead = Eye(c, "Tête Aelis", hero);

            Fade(c, 0f, 1.6f, Color.white, 1f, 0f);
            Shot(c, "Le mur blanc", 0f, 6.5f, s + new Vector3(0f, 7f, -10f), s + new Vector3(0f, 9f, -6f), wall, 55f, 0f, false);
            Sub(c, 1.4f, 3.6f, Voice, "…Tout… s'efface…");
            Sub(c, 5f, 3f, Voice, "…Tout… finit… ici…");
            Cam(c, "Le Cœur", 6.5f, 5f, s + new Vector3(0f, 3f, 8f), s + new Vector3(-8f, 3f, -6f), new Vector3(2f, 0.2f, 1f), 46f);
            Act(c, 7f, "burst:Cœur:Anemo");
            Mark(c, "Cœur", s + new Vector3(0f, 2f, 8f), 0f);
            Sub(c, 7.4f, 4f, "Kael", "Il vient. L'oubli qui a mangé mille mondes. Il a pris un visage pour nous.");
            Shot(c, "Aelis", 11.6f, 6f, O(hero, 0f, 0.8f, 1.66f, 2.3f), O(hero, 0f, 0.4f, 1.66f, 1.8f), heroHead, 33f);
            Sub(c, 11.9f, 3.4f, "Aelis", "Alors regardons-le en face. Ensemble.");
            Sub(c, 15.4f, 2.8f, "Kaïra", "Allez ! On y va !");
            Cam(c, "Ils lui font face", 17.4f, 4.5f, s + new Vector3(0f, 5f, 8f), s + new Vector3(-3f, 2f, -10f), new Vector3(0f, 0.6f, 3f), 48f, 0.5f);
            End(c);
        }

        // ================================================================ CIN_19 / 20 / 21 — Les trois fins

        static void EndingStage(Cine c, out Vector3 s, out Vector3 hero)
        {
            s = ChapterBuilder.Sites["sceau_summit"];
            hero = s + new Vector3(0f, 0f, -6f);
            Act(c, 0f, "time:6.8", true);
            Mark(c, "PlayerMark", hero, 0f);
            Act(c, 0f, "place:PlayerMark", true);
            Act(c, 0f, "player:show", true);
            Act(c, 0f, "clearboss:boss_final", true);
            Actor(c, "Kaïra", "Kaira", s + new Vector3(-4f, 0f, -8f), 0f);
            Actor(c, "Nerys", "Nerys", s + new Vector3(4f, 0f, -8f), 0f);
            Actor(c, "Torvan", "Torvan", s + new Vector3(0f, 0f, -10f), 0f);
            foreach (var n in new[] { "Kaïra", "Nerys", "Torvan" }) Act(c, 0f, $"show:{n}", true);
            Kael(c, s + new Vector3(-7f, 0f, -5f), 0f, false);
            Act(c, 0f, "show:Kael", true);
        }

        static void FinSceau(Transform parent)
        {
            var c = Begin(parent, "CIN_19_Fin_Sceau");
            EndingStage(c, out var s, out var hero);
            Mark(c, "HeartFX", s + new Vector3(0f, 2f, 0f), 0f);
            var heart = Target(c, "Cœur", s + new Vector3(0f, 2f, 0f));
            var heroHead = Eye(c, "Tête Aelis", hero);
            Fade(c, 0f, 1.5f, Color.white, 1f, 0f);
            Shot(c, "Adieux", 0f, 8f, O(hero, 0f, 0.9f, 1.66f, 2.4f), O(hero, 0f, 0.5f, 1.66f, 1.9f), heroHead, 33f);
            Sub(c, 1f, 3.8f, "Aelis", "Je sais ce que ça coûte. Je le fais quand même.");
            Sub(c, 4.9f, 3.2f, "Kaïra", "Aelis… non. Allez, dis-moi qu'il y a autre chose !");
            Sub(c, 8.2f, 3f, "Aelis", "Merci. Pour chaque jour.");
            Cam(c, "Le Cœur", 8f, 6f, heart.position, s + new Vector3(-9f, 3.5f, -9f), new Vector3(2f, 0.5f, 2f), 42f, 0.5f);
            Act(c, 9.5f, "anim:player:Burst:1.5");
            Act(c, 10.5f, "burst:HeartFX:Anemo");
            Sub(c, 11.5f, 3.4f, "Kael", "NON ! Aelith !");
            Fade(c, 13f, 2.2f, Color.white, 0f, 1f);
            Act(c, 15.4f, "hide:Kael", true);
            Shot(c, "Aerendal sauvé", 15.4f, 9f, s + new Vector3(-70f, 45f, -80f), s + new Vector3(80f, 70f, -150f), Target(c, "L'île", s + new Vector3(0f, -45f, 120f)), 55f, 0f, false);
            Fade(c, 15.4f, 2f, Color.white, 1f, 0f);
            Sub(c, 16.5f, 4.6f, "", "Aerendal survécut. Le Sceau tient, et ses vents se sont tus de nouveau.");
            Sub(c, 21.4f, 4.6f, "", "Aelith dort, et le monde l'oublie peu à peu. Kael, lui, n'oublie rien.");
            Fade(c, 25f, 1.5f, Color.black, 0f, 1f);
            Credits(c, 27f);
            Fade(c, 27f, 22f, Color.black, 1f, 1f);
            End(c);
        }

        static void FinPale(Transform parent)
        {
            var c = Begin(parent, "CIN_20_Fin_Pale");
            EndingStage(c, out var s, out var hero);
            var wall = Target(c, "Horizon", s + new Vector3(0f, 8f, 320f));
            var heroHead = Eye(c, "Tête Aelis", hero);
            Fade(c, 0f, 1.5f, Color.white, 1f, 0f);
            Shot(c, "Aelis", 0f, 8f, O(hero, 0f, 0.9f, 1.66f, 2.4f), O(hero, 0f, 0.5f, 1.66f, 1.9f), heroHead, 33f);
            Sub(c, 1f, 4f, "Aelis", "Je ne veux pas enchaîner personne. Ni moi, ni eux. Ni toi, Kael.");
            Sub(c, 5.2f, 3.6f, "Kael", "Alors c'est la fin. Je suis fatigué, petite sœur. Je suis heureux.");
            Sub(c, 8.9f, 3.2f, "Torvan", "Hmpf. Pas la pire des fins.");
            Cam(c, "Ensemble", 8.4f, 7f, s + new Vector3(0f, 1.2f, -5f), s + new Vector3(-8f, 3f, -16f), new Vector3(3f, 0.5f, 5f), 44f, 0.5f);
            Shot(c, "La marée blanche", 15.4f, 9f, s + new Vector3(-6f, 6f, 6f), s + new Vector3(-4f, 8f, 10f), wall, 55f, 0f, false);
            Fade(c, 17f, 6f, Color.white, 0f, 1f);
            Sub(c, 17.5f, 4.6f, "", "La Pâleur vint sans bruit. Il n'y eut ni cri, ni peur, ni douleur.");
            Sub(c, 22.4f, 4.6f, "", "Seulement des amis, côte à côte, qui regardaient la lumière s'éteindre doucement.");
            Fade(c, 27f, 1.5f, Color.black, 0f, 1f);
            Credits(c, 28f);
            Fade(c, 28f, 22f, Color.black, 1f, 1f);
            End(c);
        }

        static void FinVents(Transform parent)
        {
            var c = Begin(parent, "CIN_21_Fin_Vents");
            EndingStage(c, out var s, out var hero);
            Mark(c, "FxAnemo", s + new Vector3(0f, 2f, -6f), 0f);
            Mark(c, "FxPyro", s + new Vector3(-4f, 2f, -8f), 0f);
            Mark(c, "FxHydro", s + new Vector3(4f, 2f, -8f), 0f);
            Mark(c, "FxElectro", s + new Vector3(0f, 2f, -10f), 0f);
            Mark(c, "FxHeart", s + new Vector3(0f, 6f, 0f), 0f);
            var heroHead = Eye(c, "Tête Aelis", hero);
            var heart = Target(c, "Cœur", s + new Vector3(0f, 3f, 0f));
            var sky = Target(c, "Ciel", s + new Vector3(0f, 40f, 40f));
            Fade(c, 0f, 1.5f, Color.white, 1f, 0f);
            Shot(c, "Les quatre", 0f, 7f, O(hero, 0f, 0.9f, 1.66f, 2.6f), O(hero, 0f, 0.5f, 1.7f, 2.0f), heroHead, 36f);
            Sub(c, 1f, 3.8f, "Aelis", "Pas un sacrifice. Une promesse. Chacun porte un vent, et personne ne porte tout.");
            Sub(c, 5f, 3f, "Kaïra", "Allez ! Moi, je prends le feu.");
            Act(c, 6f, "anim:Kaïra:Skill:1");
            Act(c, 6.2f, "burst:FxPyro:Pyro");
            Sub(c, 8.2f, 3f, "Nerys", "Et moi la marée. Sans rien dire. Enfin presque.");
            Act(c, 9f, "burst:FxHydro:Hydro");
            Sub(c, 11.3f, 2.8f, "Torvan", "Hmpf. L'orage, donc.");
            Act(c, 12f, "burst:FxElectro:Electro");
            Sub(c, 14.2f, 3f, "Aelis", "Et le souffle est à moi.");
            Act(c, 14.8f, "anim:player:Burst:1.5");
            Act(c, 15.4f, "burst:FxAnemo:Anemo");
            Cam(c, "Le Cœur s'ouvre", 14f, 6f, heart.position, s + new Vector3(-9f, 3.5f, -10f), new Vector3(3f, 1.5f, 3f), 44f, 0.5f);
            Act(c, 17f, "burst:FxHeart:Anemo");
            Act(c, 17.3f, "burst:FxHeart:Pyro");
            Act(c, 17.6f, "burst:FxHeart:Hydro");
            Act(c, 17.9f, "burst:FxHeart:Electro");
            Act(c, 17.5f, "sound:burst");
            Fade(c, 17.5f, 2f, Color.white, 0f, 1f);
            Fade(c, 19.6f, 2f, Color.white, 1f, 0f);
            Sub(c, 18.5f, 3.6f, "Kael", "Je sens le temps passer sur moi. Enfin.");
            Shot(c, "La Pâleur recule", 19.6f, 9f, s + new Vector3(-50f, 30f, -70f), s + new Vector3(60f, 50f, -120f), sky, 58f, 0f, false);
            Sub(c, 21f, 4.6f, "", "Les quatre vents revinrent, et avec eux les couleurs, les voix, les noms.");
            Sub(c, 25.8f, 4.8f, "", "Aelith s'appela de nouveau Aelis. Kael vieillit, enfin, comme tout le monde. Aerendal vécut.");
            Fade(c, 31f, 1.5f, Color.black, 0f, 1f);
            Credits(c, 33f);
            Fade(c, 33f, 22f, Color.black, 1f, 1f);
            End(c);
        }
    }
}
