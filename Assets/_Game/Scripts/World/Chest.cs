using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public class Chest : Interactable
    {
        public int tier;
        public Transform lid;
        public bool Opened { get; private set; }
        float openT;

        static readonly string[] TierNames = { "Coffre commun", "Coffre raffiné", "Coffre précieux", "Coffre luxueux" };

        public override string Prompt => $"Ouvrir — {TierNames[Mathf.Clamp(tier, 0, 3)]}";
        public override bool Available => !Opened;

        void Start()
        {
            if (SaveSystem.IsChestOpened(id)) SetOpened(false);
        }

        public override void Interact()
        {
            if (Opened) return;
            SetOpened(true);
            int t = Mathf.Clamp(tier, 0, 3);
            Inventory.Add("mora", (t + 1) * (t + 1) * 250 + Random.Range(0, 100));
            Inventory.Add("star_crystal", new[] { 0, 5, 10, 40 }[t]);
            if (t >= 1) Inventory.Add("xp_book", t);
            if (Random.value < 0.6f) Inventory.Add("apple", 1 + t);
            if (id == "ruin_chest" || id.EndsWith("_fragment")) Inventory.Add("seal_fragment", 1);
            if (id == "tower_chest") Inventory.Add("ancient_key", 1);
            PartyManager.Instance?.GrantXP(80 * (t + 1));
            CombatFX.Burst(transform.position + Vector3.up, new Color(1f, 0.85f, 0.45f), 1.5f, 40);
            AudioManager.Play("chest");
            QuestManager.ReportChest(id);
            SaveSystem.MarkChest(id);
        }

        void SetOpened(bool animate)
        {
            Opened = true;
            openT = animate ? 0f : 1f;
            if (lid && !animate) lid.localRotation = Quaternion.Euler(-110f, 0f, 0f);
        }

        void Update()
        {
            if (!Opened || !lid || openT >= 1f) return;
            openT = Mathf.Min(1f, openT + Time.deltaTime * 2f);
            lid.localRotation = Quaternion.Euler(-110f * Mathf.SmoothStep(0f, 1f, openT), 0f, 0f);
        }
    }
}

