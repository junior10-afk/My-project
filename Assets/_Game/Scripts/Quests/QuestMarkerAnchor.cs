using UnityEngine;

namespace LastWorld
{
    /// <summary>Registers this transform as a named quest / map marker at runtime.</summary>
    public class QuestMarkerAnchor : MonoBehaviour
    {
        public string id;

        void OnEnable() => WorldMarkers.Register(id, transform);
    }
}
