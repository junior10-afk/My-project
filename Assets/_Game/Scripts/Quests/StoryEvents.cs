using System.Linq;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Story beats of the whole game (Prologue, Chapter I; later chapters live in ChapterQuests): chains cinematics (CinematicDirector), story
    /// dialogues (DialogueDB) and banners at the right moments of the main quest.
    /// </summary>
    public static class StoryEvents
    {
        static readonly Color Gold = new(1f, 0.85f, 0.45f);

        public static void NewGame()
        {
            CinematicDirector.PlaySequence(new[] { "CIN_00_Prologue", "CIN_01_Reveil" }, () =>
            {
                GameUI.Banner("Prairies de Vent-Clair", Color.white);
                TalkTo("elara");
            });
        }

        public static void AltarTouched()
        {
            CinematicDirector.Play("CIN_02_Resonance", () =>
            {
                GameUI.Banner("Résonance du vent éveillée", Elements.Color(Element.Anemo));
                DialogueSystem.Show(DialogueDB.AfterVision(), null);
            });
        }

        public static void Recruited(string member) => DialogueSystem.Show(DialogueDB.Recruit(member), null);

        public static void BossDefeated()
        {
            // Aim the first shot at where the Guardian actually fell (its corpse stays for the cinematic).
            var boss = Object.FindObjectsByType<EnemyBase>().FirstOrDefault(e => e.killTag == "boss");
            var aim = CinematicDirector.Instance ? CinematicDirector.Instance.transform.Find("CIN_04_PremierVent/Gardien") : null;
            if (boss && aim) aim.position = boss.transform.position + Vector3.up * 1.5f;
            CinematicDirector.Play("CIN_04_PremierVent", () => GameUI.Banner("Le Vent-Clair est libéré", Elements.Color(Element.Anemo)));
        }

        public static void ChapterEnd()
        {
            CinematicDirector.Play("CIN_05_IlTeRessemblait", () => GameUI.Banner("Chapitre I terminé", Gold));
        }

        /// <summary>Plays a cinematic of the chapters (skipped silently if it was not built), then calls <paramref name="then"/>.</summary>
        public static void Play(string id, System.Action then = null) => CinematicDirector.Play(id, then);

        public static void ChapterDone(int chapter, string title)
        {
            AudioManager.Play("quest");
            GameUI.Banner($"Chapitre {Roman(chapter)} terminé — {title}", Gold);
            var next = QuestManager.Instance ? QuestManager.Instance.CurrentChapter : null;
            if (next != null) GameUI.Toast($"Nouveau chapitre : {next.title}. Retrouve Elara au camp.");
            SaveSystem.Save();
        }

        /// <summary>The three endings of Chapter X: 1 = the Seal restored, 2 = the world left to fade, 3 = the Third Way.</summary>
        public static void Ending(int which)
        {
            if (GameUI.Instance) GameUI.Instance.CloseDialogue(); // also reports the Heart of the Seal to the quest
            string id = which switch { 1 => "CIN_19_Fin_Sceau", 2 => "CIN_20_Fin_Pale", _ => "CIN_21_Fin_Vents" };
            string name = which switch { 1 => "Le Sceau restauré", 2 => "L'Extinction douce", _ => "La Troisième Voie" };
            CinematicDirector.Play(id, () =>
            {
                GameUI.Banner($"Fin — {name}", Gold);
                SaveSystem.Save();
            });
        }

        static string Roman(int n) => n switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", 6 => "VI", 7 => "VII", 8 => "VIII", 9 => "IX", _ => "X" };

        static void TalkTo(string id)
        {
            var npc = Object.FindObjectsByType<NPC>().FirstOrDefault(n => n.id == id);
            if (npc) npc.Interact();
        }
    }
}
