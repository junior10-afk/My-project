using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public class Waypoint : Interactable
    {
        public static readonly List<Waypoint> AllWaypoints = new();
        public string displayName = "Sentinelle";
        public Renderer orb;
        public Light orbLight;
        public bool Activated { get; private set; }

        public override string Prompt => Activated ? $"Se reposer — {displayName}" : $"Activer — {displayName}";

        protected override void OnEnable()
        {
            base.OnEnable();
            AllWaypoints.Add(this);
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            AllWaypoints.Remove(this);
        }

        void Start()
        {
            if (SaveSystem.IsWaypointActive(id)) SetActive(false);
            else SetVisual(false);
        }

        public override void Interact()
        {
            if (!Activated)
            {
                SetActive(true);
                GameUI.Banner($"{displayName} activée", Elements.Color(Element.Anemo));
                AudioManager.Play("quest");
                SaveSystem.MarkWaypoint(id);
                QuestManager.ReportWaypoints(ActiveCount);
            }
            PartyManager.Instance.ReviveAndHealAll();
            PartyManager.Instance.RespawnPoint = SpawnPoint;
            PlayerController.Instance.RestoreStamina();
            GameUI.Toast("L'équipe est soignée.");
        }

        public Vector3 SpawnPoint => transform.position + transform.forward * 2.5f + Vector3.up * 0.2f;

        void SetActive(bool fx)
        {
            Activated = true;
            SetVisual(true);
            if (fx) CombatFX.Burst(transform.position + Vector3.up * 4.5f, Elements.Color(Element.Anemo), 3f, 60);
        }

        void SetVisual(bool on)
        {
            if (orbLight) orbLight.intensity = on ? 2.5f : 0.3f;
            if (orb)
            {
                var mpb = new MaterialPropertyBlock();
                orb.GetPropertyBlock(mpb);
                mpb.SetColor("_EmissionColor", on ? new Color(0.9f, 3.2f, 2.6f) : new Color(0.15f, 0.2f, 0.25f));
                orb.SetPropertyBlock(mpb);
            }
        }

        public static int ActiveCount => AllWaypoints.FindAll(w => w.Activated).Count;

        public static bool NearestInactive(out Vector3 pos)
        {
            pos = default;
            var p = PlayerController.Instance;
            if (!p) return false;
            float best = float.MaxValue;
            bool found = false;
            foreach (var w in AllWaypoints)
            {
                if (w.Activated) continue;
                float d = Vector3.Distance(w.transform.position, p.transform.position);
                if (d < best) { best = d; pos = w.transform.position; found = true; }
            }
            return found;
        }
    }
}

