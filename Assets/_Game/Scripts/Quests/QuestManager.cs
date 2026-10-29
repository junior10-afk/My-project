using System;
using System.Collections.Generic;
using UnityEngine;

namespace LastWorld
{
    public enum StepType { Talk, Interact, Kill, Camp, Waypoints, OpenChest, Item, Boss }

    public class QuestStep
    {
        public string text;
        public StepType type;
        public string target;
        public int required = 1;
        public string markerId;
        public Action onComplete;
    }

    public class Quest
    {
        public string id, title, summary;
        public bool main;
        public List<QuestStep> steps = new();
        public int current;
        public int progress;
        public bool done;
        public Action onFinish;
        public QuestStep Step => done || current >= steps.Count ? null : steps[current];
    }

    /// <summary>Static registry of world positions used by quest markers and the map.</summary>
    public static class WorldMarkers
    {
        static readonly Dictionary<string, Transform> markers = new();
        public static void Register(string id, Transform t) { if (!string.IsNullOrEmpty(id)) markers[id] = t; }
        public static bool TryGet(string id, out Vector3 pos)
        {
            pos = default;
            if (string.IsNullOrEmpty(id) || !markers.TryGetValue(id, out var t) || !t) return false;
            pos = t.position;
            return true;
        }
    }

    /// <summary>
    /// Story and side quests (Chapter I « Les Vents Oubliés »), progress tracking from world events,
    /// party recruitment and rewards.
    /// </summary>
    public class QuestManager : MonoBehaviour
    {
        public static QuestManager Instance { get; private set; }
        public readonly List<Quest> quests = new();
        public Quest Tracked { get; private set; }
        public static event Action Changed;

        int activatedWaypoints;

        void Awake()
        {
            Instance = this;
            BuildQuests();
            Tracked = quests[0];
        }

        void BuildQuests()
        {
            var party = (Func<PartyManager>)(() => PartyManager.Instance);

            quests.Add(new Quest
            {
                id = "main1",
                title = "Chapitre I — Les Vents Oubliés",
                summary = "Tu es tombé du ciel au pied de l'Autel des Vents, sans souvenirs. Aerendal est le dernier monde : depuis que le Sceau Céleste s'est brisé, ses vents se sont tus et la Pâleur ronge l'horizon.",
                main = true,
                steps =
                {
                    new QuestStep { text = "Parler à Elara, la Gardienne de l'Autel", type = StepType.Talk, target = "elara", markerId = "elara" },
                    new QuestStep { text = "Toucher l'Autel des Vents", type = StepType.Interact, target = "altar", markerId = "altar",
                        onComplete = () =>
                        {
                            EnemySpawner.Find("camp_tutorial")?.EnsureSpawned();
                            StoryEvents.AltarTouched();
                        } },
                    new QuestStep { text = "Repousser les slimes près du camp", type = StepType.Camp, target = "camp_tutorial", markerId = "camp_tutorial",
                        onComplete = () => Recruit("Kaïra") },
                    new QuestStep { text = "Réveiller des Sentinelles", type = StepType.Waypoints, required = 3, markerId = "nearest_waypoint",
                        onComplete = () => Recruit("Nerys") },
                    new QuestStep { text = "Retrouver l'éclat tombé dans les ruines anciennes", type = StepType.OpenChest, target = "ruin_chest", markerId = "ruin_chest",
                        onComplete = () => Recruit("Torvan") },
                    new QuestStep { text = "Rapporter le fragment à Elara", type = StepType.Talk, target = "elara", markerId = "elara",
                        onComplete = () => EnemySpawner.Find("boss_arena")?.EnsureSpawned() },
                    new QuestStep { text = "Gravir la Tour des Vents et ouvrir le coffre ancien", type = StepType.OpenChest, target = "tower_chest", markerId = "tower_chest",
                        onComplete = () => EnemySpawner.Find("boss_arena")?.EnsureSpawned() },
                    new QuestStep { text = "Délivrer le Gardien Ancien", type = StepType.Boss, target = "boss", markerId = "boss",
                        onComplete = StoryEvents.BossDefeated },
                    new QuestStep { text = "Retourner auprès d'Elara", type = StepType.Talk, target = "elara", markerId = "elara" },
                },
                onFinish = () =>
                {
                    Inventory.Add("star_crystal", 1600);
                    Inventory.Add("xp_book", 10);
                    StoryEvents.ChapterEnd();
                },
            });

            void Recruit(string member)
            {
                party()?.Unlock(member);
                StoryEvents.Recruited(member);
            }

            quests.Add(new Quest
            {
                id = "orbs",
                title = "Les Âmes du Vent",
                summary = "Des Orbes des Vents flottent sur les hauteurs d'Aerendal. Elara dit que l'Autel en récompense la collecte.",
                steps = { new QuestStep { text = "Collecter des Orbes des Vents", type = StepType.Item, target = "wind_orb", required = 10 } },
                onFinish = () =>
                {
                    var pc = PlayerController.Instance;
                    if (pc) pc.SetMaxStamina(pc.MaxStamina + 40f);
                    Inventory.Add("star_crystal", 400);
                    GameUI.Banner("Endurance maximale augmentée !", Elements.Color(Element.Anemo));
                },
            });

            quests.Add(new Quest
            {
                id = "hunter",
                title = "Chasseur de monstres",
                summary = "Bram, le marchand du camp, paie bien pour chaque monstre éliminé dans la région.",
                steps = { new QuestStep { text = "Vaincre des monstres", type = StepType.Kill, target = "*", required = 20 } },
                onFinish = () =>
                {
                    Inventory.Add("mora", 5000);
                    Inventory.Add("xp_book", 5);
                },
            });
        }

        public static string StepProgress(Quest q)
        {
            var s = q.Step;
            if (s == null) return "Terminée";
            return s.required > 1 ? $"{s.text} ({Mathf.Min(q.progress, s.required)}/{s.required})" : s.text;
        }

        public static bool TryGetMarker(Quest q, out Vector3 pos)
        {
            pos = default;
            var s = q?.Step;
            if (s == null) return false;
            if (s.markerId == "nearest_waypoint") return Waypoint.NearestInactive(out pos);
            return WorldMarkers.TryGet(s.markerId, out pos);
        }

        public void Track(Quest q)
        {
            Tracked = q;
            Changed?.Invoke();
        }

        void Report(StepType type, string target, int amount = 1)
        {
            foreach (var q in quests)
            {
                var s = q.Step;
                if (s == null || s.type != type) continue;
                if (s.target != "*" && s.target != target && type != StepType.Waypoints && type != StepType.Item) continue;
                if (type == StepType.Item && s.target != target) continue;
                q.progress = type == StepType.Item ? Inventory.Count(target) : q.progress + amount;
                if (type == StepType.Waypoints) q.progress = activatedWaypoints;
                if (q.progress >= s.required) Advance(q);
                else Changed?.Invoke();
            }
        }

        void Advance(Quest q)
        {
            var s = q.Step;
            q.current++;
            q.progress = 0;
            s.onComplete?.Invoke();
            AudioManager.Play("quest");
            if (q.current >= q.steps.Count)
            {
                q.done = true;
                q.onFinish?.Invoke();
                GameUI.Toast($"Quête terminée : {q.title}");
                if (Tracked == q) Tracked = quests.Find(x => !x.done);
            }
            else
            {
                GameUI.Toast($"Nouvel objectif : {q.Step.text}");
                // Some steps may already be satisfied (e.g. waypoints activated earlier)
                if (q.Step.type == StepType.Waypoints && activatedWaypoints >= q.Step.required) Advance(q);
                if (q.Step != null && q.Step.type == StepType.Item && Inventory.Count(q.Step.target) >= q.Step.required) Advance(q);
            }
            SaveSystem.Save();
            Changed?.Invoke();
        }

        public bool IsAtStep(string questId, int step) => quests.Find(x => x.id == questId) is { } q && !q.done && q.current == step;

        public static void ReportTalk(string npc) => Instance?.Report(StepType.Talk, npc);
        public static void ReportInteract(string id) => Instance?.Report(StepType.Interact, id);
        public static void ReportKill(string tag)
        {
            Instance?.Report(StepType.Kill, tag);
            if (tag == "boss") Instance?.Report(StepType.Boss, "boss");
        }
        public static void ReportCampCleared(string camp) => Instance?.Report(StepType.Camp, camp);
        public static void ReportChest(string chest) => Instance?.Report(StepType.OpenChest, chest);
        public static void ReportItem(string item) => Instance?.Report(StepType.Item, item, 0);

        public static void ReportWaypoints(int activated)
        {
            if (!Instance) return;
            Instance.activatedWaypoints = activated;
            Instance.Report(StepType.Waypoints, "", 0);
        }

        // ---------------------------------------------------------------- Save support

        [Serializable]
        public class QuestState
        {
            public string id;
            public int current, progress;
            public bool done;
        }

        public List<QuestState> Snapshot()
        {
            var list = new List<QuestState>();
            foreach (var q in quests) list.Add(new QuestState { id = q.id, current = q.current, progress = q.progress, done = q.done });
            return list;
        }

        public void Restore(List<QuestState> states)
        {
            foreach (var st in states)
            {
                var q = quests.Find(x => x.id == st.id);
                if (q == null) continue;
                q.current = st.current;
                q.progress = st.progress;
                q.done = st.done;
            }
            Tracked = quests.Find(x => !x.done) ?? quests[0];
            Changed?.Invoke();
        }
    }
}
