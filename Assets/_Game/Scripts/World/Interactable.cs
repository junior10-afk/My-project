using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LastWorld
{
    public abstract class Interactable : MonoBehaviour
    {
        public static readonly List<Interactable> All = new();
        public string id;
        public float range = 2.8f;

        public abstract string Prompt { get; }
        public virtual bool Available => true;
        public abstract void Interact();

        protected virtual void OnEnable()
        {
            All.Add(this);
            WorldMarkers.Register(id, transform);
        }

        protected virtual void OnDisable() => All.Remove(this);
    }
}

