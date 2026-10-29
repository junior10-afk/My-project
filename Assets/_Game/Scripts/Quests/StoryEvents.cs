using System.Linq;
using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Story beats of the Prologue and Chapter I: chains cinematics (CinematicDirector), story
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
            CinematicDirector.Play("CIN_04_PremierVent", () => GameUI.Banner("Le Vent-Clair est libéré", Elements.Color(Element.Anemo)));
        }

        public static void ChapterEnd()
        {
            CinematicDirector.Play("CIN_05_IlTeRessemblait", () => GameUI.Banner("Chapitre I terminé", Gold));
        }

        static void TalkTo(string id)
        {
            var npc = Object.FindObjectsByType<NPC>().FirstOrDefault(n => n.id == id);
            if (npc) npc.Interact();
        }
    }
}
