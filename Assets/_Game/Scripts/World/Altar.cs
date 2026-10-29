using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public class Altar : Interactable
    {
        public override string Prompt => "Prier à l'Autel des Vents";

        public override void Interact()
        {
            PartyManager.Instance.ReviveAndHealAll();
            PlayerController.Instance.RestoreStamina();
            CombatFX.Burst(transform.position + Vector3.up * 7f, Elements.Color(Element.Anemo), 4f, 80);
            CombatFX.Ring(transform.position, 10f, Elements.Color(Element.Anemo), 0.8f);
            AudioManager.Play("quest");
            GameUI.Toast("L'Autel restaure l'équipe.");
            QuestManager.ReportInteract("altar");
        }
    }
}

