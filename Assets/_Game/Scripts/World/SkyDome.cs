using UnityEngine;

namespace LastWorld
{
    /// <summary>Keeps the cloud dome centred on the main camera so the sky never gets closer.</summary>
    [ExecuteAlways]
    public class SkyDome : MonoBehaviour
    {
        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam) transform.position = cam.transform.position;
        }
    }
}
