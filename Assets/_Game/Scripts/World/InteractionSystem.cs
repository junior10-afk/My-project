using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public class InteractionSystem : MonoBehaviour
    {
        public static Interactable Current { get; private set; }

        void Update()
        {
            Current = null;
            if (GameState.InputBlocked) return;
            float best = float.MaxValue;
            foreach (var it in Interactable.All)
            {
                if (!it.Available) continue;
                Vector3 d = it.transform.position - transform.position;
                float dist = new Vector2(d.x, d.z).magnitude;
                if (dist > it.range || Mathf.Abs(d.y) > 4f) continue;
                float score = dist - Vector3.Dot(transform.forward, d.normalized) * 0.8f;
                if (score < best) { best = score; Current = it; }
            }
            var kb = Keyboard.current;
            var gp = Gamepad.current;
            if (Current && ((kb != null && kb.fKey.wasPressedThisFrame) || (gp != null && gp.buttonNorth.wasPressedThisFrame)))
                Current.Interact();
        }
    }
}

