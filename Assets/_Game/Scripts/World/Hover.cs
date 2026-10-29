using UnityEngine;

namespace LastWorld
{
    /// <summary>Gentle floating and spinning, used on glowing orbs.</summary>
    public class Hover : MonoBehaviour
    {
        public float amplitude = 0.25f;
        public float frequency = 0.4f;
        public float spinSpeed = 30f;

        Vector3 start;

        void Start() => start = transform.localPosition;

        void Update()
        {
            transform.localPosition = start + Vector3.up * (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) * amplitude);
            transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        }
    }
}
