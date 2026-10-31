using System.Linq;
using UnityEngine;

namespace LastWorld
{
    /// <summary>Helpers for automated play-tests (called from editor scripts): jump in the story, walk to markers, play a step.</summary>
    public static class DevTools
    {
        public static void Jump(string questId, int step) => QuestManager.Instance?.DebugJump(questId, step);

        /// <summary>Teleports the hero next to a quest marker; returns false if the marker does not exist.</summary>
        public static bool Goto(string markerId, float back = 4f)
        {
            var p = PlayerController.Instance;
            if (p == null || !WorldMarkers.TryGet(markerId, out var pos)) return false;
            p.Teleport(pos + new Vector3(0f, 2f, -back));
            return true;
        }

        public static string State()
        {
            var qm = QuestManager.Instance;
            var q = qm ? qm.CurrentChapter : null;
            return q == null ? "no chapter" : $"{q.id} step {q.current}/{q.steps.Count}: {q.Step?.text} [{q.Step?.type} → {q.Step?.target}] progress {q.progress}";
        }

        public static void Kill(EnemyBase e) => e.Health.TakeDamage(new DamageInfo { amount = 1e9f, element = Element.Physical, point = e.transform.position });

        /// <summary>Plays the current step of the current chapter the way a player would complete it; returns what it did.</summary>
        public static string AutoStep()
        {
            var qm = QuestManager.Instance;
            var q = qm ? qm.CurrentChapter : null;
            var s = q?.Step;
            if (s == null) return "no step";
            string log = State();
            switch (s.type)
            {
                case StepType.Talk: QuestManager.ReportTalk(s.target); break;
                case StepType.Reach:
                    if (!Goto(s.markerId)) log += "  !! marker missing";
                    break;
                case StepType.Camp:
                {
                    var sp = EnemySpawner.Find(s.target);
                    if (!sp) { log += "  !! spawner missing"; break; }
                    sp.EnsureSpawned();
                    var foes = Object.FindObjectsByType<EnemyBase>().Where(e => e.GetComponentInParent<EnemySpawner>() == sp).ToList();
                    log += $"  ({foes.Count} foes)";
                    foreach (var e in foes) Kill(e);
                    if (foes.Count == 0) QuestManager.ReportCampCleared(s.target);
                    break;
                }
                case StepType.Boss:
                {
                    var sp = EnemySpawner.Find(s.target);
                    if (!sp) { log += "  !! boss spawner missing"; break; }
                    var foes = Object.FindObjectsByType<EnemyBase>().Where(e => e.killTag == s.target).ToList();
                    if (foes.Count == 0) { sp.EnsureSpawned(); foes = Object.FindObjectsByType<EnemyBase>().Where(e => e.killTag == s.target).ToList(); }
                    log += $"  ({foes.Count} boss, level {foes.FirstOrDefault()?.Health.level}, hp {foes.FirstOrDefault()?.Health.maxHealth:0})";
                    foreach (var e in foes) Kill(e);
                    break;
                }
                case StepType.Interact:
                    for (int i = 0; i < s.required; i++) QuestManager.ReportInteract(s.target);
                    break;
                case StepType.OpenChest: QuestManager.ReportChest(s.target); break;
                default: log += "  !! unsupported step type"; break;
            }
            return log;
        }
    }
}
