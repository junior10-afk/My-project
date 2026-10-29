using UnityEngine;

namespace LastWorld
{
    /// <summary>Fire-like flicker on a light's intensity.</summary>
    [RequireComponent(typeof(Light))]
    public class LightFlicker : MonoBehaviour
    {
        public float amount = 0.3f;
        public float speed = 6f;

        Light lamp;
        float baseIntensity;
        float seed;

        void Start()
        {
            lamp = GetComponent<Light>();
            baseIntensity = lamp.intensity;
            seed = Random.value * 100f;
        }

        void Update()
        {
            float n = Mathf.PerlinNoise(seed, Time.time * speed);
            lamp.intensity = baseIntensity * (1f - amount * 0.5f + n * amount);
        }
    }
}
