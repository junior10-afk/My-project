using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// The main quests of Chapters II to X (v0.2 → v1.0). Each chapter unlocks when the previous one is done and
    /// starts with a briefing by Elara at the camp. World objects (markers, camps, bosses, chests, story props)
    /// are placed by <c>ChapterBuilder</c>; their ids are the <c>target</c> / <c>markerId</c> values used here.
    /// </summary>
    public static class ChapterQuests
    {
        static readonly Color Gold = new(1f, 0.85f, 0.45f);

        public static void Add(QuestManager qm)
        {
            qm.quests.Insert(1, Cendrelune());
            qm.quests.Insert(2, Ysmere());
            qm.quests.Insert(3, Valdrak());
            qm.quests.Insert(4, TerresPales());
            qm.quests.Insert(5, Sanctuaire());
            qm.quests.Insert(6, Veilles());
            qm.quests.Insert(7, TourDuSceau());
            qm.quests.Insert(8, DernierSoir());
            qm.quests.Insert(9, DernierMonde());
        }

        /// <summary>Chapter VII awakens each companion's wind as their trial is won.</summary>
        public static bool IsAwakened(string member)
        {
            var qm = QuestManager.Instance;
            var q = qm ? qm.quests.Find(x => x.id == "main7") : null;
            if (q == null) return false;
            if (q.done) return member is "Kaïra" or "Nerys" or "Torvan";
            return member switch
            {
                "Kaïra" => q.current > 2,
                "Nerys" => q.current > 4,
                "Torvan" => q.current > 6,
                _ => false,
            };
        }

        // ---------------------------------------------------------------- Step helpers

        static QuestStep TalkTo(string text, string npc, string scene, Action done = null) =>
            new() { text = text, type = StepType.Talk, target = npc, markerId = npc, lines = () => ChapterDialogue.Scene(scene), onComplete = done };

        static QuestStep ReachMark(string text, string marker, Action done = null, float radius = 30f) =>
            new() { text = text, type = StepType.Reach, target = marker, markerId = marker, radius = radius, onComplete = done };

        static QuestStep Clear(string text, string camp, Action done = null) =>
            new() { text = text, type = StepType.Camp, target = camp, markerId = camp, onComplete = done };

        static QuestStep Touch(string text, string reportId, string marker, int count, Action done = null) =>
            new() { text = text, type = StepType.Interact, target = reportId, markerId = marker, required = count, onComplete = done };

        static QuestStep Defeat(string text, string boss, Action done = null) =>
            new() { text = text, type = StepType.Boss, target = boss, markerId = boss, onComplete = done };

        static QuestStep Chest(string text, string chest, Action done = null) =>
            new() { text = text, type = StepType.OpenChest, target = chest, markerId = chest, onComplete = done };

        static void Spawn(string camp) => EnemySpawner.Find(camp)?.EnsureSpawned();

        static void Say(string scene, Action then = null) => DialogueSystem.Show(ChapterDialogue.Scene(scene), then);

        static void Done(int chapter, string title, Action extra = null)
        {
            Inventory.Add("star_crystal", 600 + chapter * 150);
            Inventory.Add("xp_book", 4 + chapter);
            PartyManager.Instance?.GrantXP(1500 * chapter);
            extra?.Invoke();
            StoryEvents.ChapterDone(chapter, title);
        }

        // ---------------------------------------------------------------- Chapter II — Cendrelune (Pyro)

        static Quest Cendrelune() => new()
        {
            id = "main2", requires = "main1", main = true,
            title = "Chapitre II — Les Cendres de Cendrelune",
            summary = "Le fragment de braise est tombé aux Hauts de Cendrelune, le village natal de Kaïra. Kael le cherche aussi.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c2_brief"),
                ReachMark("Gagner les Hauts de Cendrelune", "cendrelune", () => Say("c2_village"), 45f),
                TalkTo("Parler à Maren, la dernière habitante", "maren", "c2_maren", () => Spawn("camp_cendre")),
                Clear("Chasser les Brûlés du chemin de la Forge", "camp_cendre", () => Say("c2_camp_done")),
                ReachMark("Monter à la Forge-Volcan", "forge", () => { Spawn("boss_pyro"); StoryEvents.Play("CIN_06_Forge"); }, 40f),
                Defeat("Vaincre le Colosse de Braise", "boss_pyro", () => Say("c2_boss_done")),
                Chest("Ouvrir le coffre de la Forge", "forge_fragment", () => StoryEvents.Play("CIN_07_VentBraise", () => Say("c2_end"))),
            },
            onFinish = () => Done(2, "Les Cendres de Cendrelune"),
        };

        // ---------------------------------------------------------------- Chapter III — Ysmère (Hydro)

        static Quest Ysmere() => new()
        {
            id = "main3", requires = "main2", main = true,
            title = "Chapitre III — La Mémoire des Marées",
            summary = "Le vent d'eau dort sous la lagune d'Ysmère. Nerys espère y retrouver les archives qui disent la vérité sur les Gardiens.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c3_brief"),
                ReachMark("Gagner la lagune d'Ysmère", "ysmere", () => Say("c3_lagoon"), 50f),
                TalkTo("Retrouver Sael dans les Archives", "sael", "c3_sael1", () => Spawn("camp_marees")),
                Clear("Repousser les créatures de la marée", "camp_marees", () => Say("c3_camp_done")),
                Touch("Réveiller les Pierres de Mémoire", "memory_stone", "mem_0", 3),
                TalkTo("Consulter le Livre des Marées avec Sael", "sael", "c3_sael2", () =>
                {
                    Spawn("boss_hydro");
                    StoryEvents.Play("CIN_08_LivreMarees", () => Say("c3_truth"));
                }),
                Defeat("Vaincre la Sentinelle du Bassin", "boss_hydro", () => Say("c3_boss_done")),
                Chest("Ouvrir le coffre du Bassin", "lagoon_fragment", () => StoryEvents.Play("CIN_09_VentMarees", () => Say("c3_end"))),
            },
            onFinish = () => Done(3, "La Mémoire des Marées"),
        };

        // ---------------------------------------------------------------- Chapter IV — Valdrak (Electro)

        static Quest Valdrak() => new()
        {
            id = "main4", requires = "main3", main = true,
            title = "Chapitre IV — Le Serment d'Orage",
            summary = "Le dernier fragment repose dans la citadelle de l'Ordre de la Veille, sur les pics de Valdrak. Torvan doit y affronter son serment.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c4_brief"),
                ReachMark("Gravir le col de Valdrak", "valdrak", () => Say("c4_valdrak"), 50f),
                TalkTo("Retrouver Orrin, le dernier Veilleur", "orrin", "c4_orrin1"),
                Touch("Allumer les trois flambeaux de la Veille", "vigil_flame", "vigil_0", 3, () => Spawn("camp_orage")),
                Clear("Vaincre les gardiens de la citadelle", "camp_orage", () => Say("c4_camp_done")),
                ReachMark("Franchir la porte de la citadelle", "citadel", () =>
                {
                    Spawn("boss_electro");
                    StoryEvents.Play("CIN_10_Serment", () => Say("c4_after_serment"));
                }, 35f),
                Defeat("Vaincre le Colosse d'Orage", "boss_electro", () => Say("c4_boss_done")),
                Chest("Ouvrir le coffre de la Veille", "storm_fragment", () => StoryEvents.Play("CIN_11_VentOrage", () => Say("c4_end"))),
            },
            onFinish = () => Done(4, "Le Serment d'Orage"),
        };

        // ---------------------------------------------------------------- Chapter V — Les Terres Pâles

        static Quest TerresPales() => new()
        {
            id = "main5", requires = "main4", main = true,
            title = "Chapitre V — Les Terres Pâles",
            summary = "La Pâleur avance sur le rivage nord. Des voix d'enfants s'y font entendre : Kaïra ne peut pas ne pas y aller.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c5_brief"),
                ReachMark("Atteindre les Terres Pâles", "terres_pales", () => { Spawn("camp_pale"); Say("c5_pale"); }, 55f),
                Clear("Disperser les échos de la Pâleur", "camp_pale", () => Say("c5_camp_done")),
                ReachMark("Suivre la lueur de la lanterne", "refuge", () => StoryEvents.Play("CIN_12_Refuge"), 18f),
                TalkTo("Parler à l'enfant de la lanterne", "ilio", "c5_ilio"),
                Touch("Lire le carnet du « monsieur triste »", "kael_journal", "kael_journal", 1, () => Spawn("boss_pale")),
                Defeat("Repousser l'Écho d'un monde perdu", "boss_pale", () => Say("c5_boss_done")),
            },
            onFinish = () => Done(5, "Les Terres Pâles"),
        };

        // ---------------------------------------------------------------- Chapter VI — Le Sanctuaire des Quatre Vents

        static Quest Sanctuaire() => new()
        {
            id = "main6", requires = "main5", main = true,
            title = "Chapitre VI — La Mémoire d'Aelith",
            summary = "Les quatre fragments, réunis sur l'Autel, ouvrent le Sanctuaire des Quatre Vents où dorment les Gardiens Premiers.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c6_brief"),
                new QuestStep { text = "Poser les quatre fragments sur l'Autel des Vents", type = StepType.Interact, target = "altar", markerId = "altar",
                    onComplete = () => StoryEvents.Play("CIN_13_Sanctuaire") },
                Touch("Écouter les trois Gardiens Premiers", "spirit", "spirit_0", 3, () => Say("c6_shade", () => Spawn("boss_shade"))),
                Defeat("Affronter l'Ombre d'Aelith", "boss_shade", () => Say("c6_shade_done")),
                Touch("Toucher le Cœur du Sanctuaire", "sanctuary_core", "sanctuary_core", 1, () => StoryEvents.Play("CIN_14_Retour", () => Say("c6_end"))),
            },
            onFinish = () => Done(6, "La Mémoire d'Aelith"),
        };

        // ---------------------------------------------------------------- Chapter VII — Les Quatre Veilles

        static Quest Veilles() => new()
        {
            id = "main7", requires = "main6", main = true,
            title = "Chapitre VII — Les Quatre Veilles",
            summary = "Chaque compagnon doit éveiller le vent qu'il porte en affrontant sa veille : braise, marée, orage.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c7_brief"),
                ReachMark("Veille de braise — gagner le cercle de feu", "veille_pyro", () => Spawn("boss_v1"), 22f),
                Defeat("Vaincre le Brasier-Veilleur", "boss_v1", () => Say("c7_kaira")),
                ReachMark("Veille de marée — gagner la crique d'Ysmère", "veille_hydro", () => Spawn("boss_v2"), 22f),
                Defeat("Vaincre le Veilleur des Marées", "boss_v2", () => Say("c7_nerys")),
                ReachMark("Veille d'orage — gagner la crête de Valdrak", "veille_electro", () => Spawn("boss_v3"), 22f),
                Defeat("Vaincre le Veilleur d'Orage", "boss_v3", () => Say("c7_torvan")),
                TalkTo("Retourner auprès d'Elara", "elara", "c7_end"),
            },
            onFinish = () => Done(7, "Les Quatre Veilles", () => GameUI.Banner("Les quatre vents sont éveillés", Gold)),
        };

        // ---------------------------------------------------------------- Chapter VIII — La Tour du Sceau

        static Quest TourDuSceau() => new()
        {
            id = "main8", requires = "main7", main = true,
            title = "Chapitre VIII — La Tour du Sceau",
            summary = "Au centre de l'île se dresse la Tour du Sceau. Kael y attend sa sœur.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c8_brief"),
                ReachMark("Gagner le pied de la Tour du Sceau", "sceau_base", () => { Spawn("camp_sceau"); Say("c8_base"); }, 45f),
                Clear("Franchir les gardes de la spirale", "camp_sceau", () => Say("c8_camp_done")),
                ReachMark("Atteindre le sommet de la Tour", "sceau_summit", () =>
                {
                    Spawn("boss_kael");
                    StoryEvents.Play("CIN_15_FaceAFace");
                }, 22f),
                Defeat("Vaincre Kael", "boss_kael", () => StoryEvents.Play("CIN_16_KaelVaincu", () => Say("c8_end"))),
            },
            onFinish = () => Done(8, "La Tour du Sceau"),
        };

        // ---------------------------------------------------------------- Chapter IX — La Veille du Dernier Jour

        static Quest DernierSoir() => new()
        {
            id = "main9", requires = "main8", main = true,
            title = "Chapitre IX — La Veille du Dernier Jour",
            summary = "La Pâleur sera sur le camp à l'aube. Il reste une nuit pour défendre l'Autel et dire ce qui doit l'être.",
            steps =
            {
                TalkTo("Écouter Elara au camp", "elara", "c9_brief", () => Spawn("camp_siege_0")),
                Clear("Repousser la première vague", "camp_siege_0", () => { Say("c9_wave_done"); Spawn("camp_siege_1"); }),
                Clear("Repousser la deuxième vague", "camp_siege_1", () => Spawn("camp_siege_2")),
                Clear("Repousser la dernière vague", "camp_siege_2"),
                Touch("Passer la veillée auprès des braseros", "brazier", "brazier_0", 4),
                TalkTo("Parler à Bram", "bram", "c9_bram"),
                TalkTo("Parler à Ilio", "ilio_camp", "c9_ilio", () => StoryEvents.Play("CIN_17_Veillee")),
            },
            onFinish = () => Done(9, "La Veille du Dernier Jour"),
        };

        // ---------------------------------------------------------------- Chapter X — Le Dernier Monde

        static Quest DernierMonde() => new()
        {
            id = "main10", requires = "main9", main = true,
            title = "Chapitre X — Le Dernier Monde",
            summary = "À l'aube, la Pâleur se referme sur Aerendal. Au sommet de la Tour du Sceau, tout se décide.",
            steps =
            {
                TalkTo("Recevoir la bénédiction d'Elara", "elara", "c10_brief"),
                ReachMark("Retourner au sommet de la Tour du Sceau", "sceau_summit", () =>
                {
                    Spawn("boss_final");
                    StoryEvents.Play("CIN_18_Effacement");
                }, 22f),
                Defeat("Affronter l'Effacement", "boss_final", () => Say("c10_boss_done")),
                Touch("Faire ton choix au Cœur du Sceau", "seal_heart", "seal_heart", 1),
            },
            onFinish = () =>
            {
                Inventory.Add("star_crystal", 5000);
                Inventory.Add("xp_book", 20);
            },
        };
    }
}
