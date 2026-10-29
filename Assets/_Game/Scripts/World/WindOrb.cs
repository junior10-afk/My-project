using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public class WindOrb : MonoBehaviour
    {
        public string id;
        Vector3 start;

        void Start()
        {
            start = transform.position;
            if (SaveSystem.IsOrbCollected(id)) gameObject.SetActive(false);
        }

        void Update()
        {
            transform.position = start + Vector3.up * (Mathf.Sin(Time.time * 1.5f) * 0.25f);
            transform.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
            var p = PlayerController.Instance;
            if (p && Vector3.Distance(p.transform.position + Vector3.up, transform.position) < 1.6f)
            {
                CombatFX.Burst(transform.position, Elements.Color(Element.Anemo), 1.5f, 30);
                AudioManager.Play("pickup", 1.2f);
                SaveSystem.MarkOrb(id);
                Inventory.Add("wind_orb", 1);
                GameUI.Toast($"Orbe des Vents ({Inventory.Count("wind_orb")})");
                gameObject.SetActive(false);
            }
        }
    }
}

