using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public class NPC : Interactable
    {
        public string displayName = "Villageois";
        public string title = "";
        public Transform head;

        public override string Prompt => $"Parler — {displayName}";

        public override void Interact()
        {
            var lines = DialogueDB.For(id);
            DialogueSystem.Show(lines, () => QuestManager.ReportTalk(id));
            var p = PlayerController.Instance;
            if (p)
            {
                Vector3 d = p.transform.position - transform.position;
                d.y = 0f;
                if (d.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(d);
                p.FaceTowards(-d);
            }
        }
    }
}

