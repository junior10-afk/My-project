using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Publishes a global wind vector (_LW_Wind: xyz = direction, w = gust strength) read by the
    /// foliage and cloud shaders. Direction slowly wanders and strength gusts using Perlin noise.
    /// </summary>
    [ExecuteAlways]
    public class WindController : MonoBehaviour
    {
        public Vector3 direction = new(1f, 0f, 0.35f);
        public float baseStrength = 1f;
        public float gustStrength = 0.8f;
        public float gustFrequency = 0.12f;
        public float directionWander = 35f;

        static readonly int WindId = Shader.PropertyToID("_LW_Wind");

        public Vector3 CurrentDirection { get; private set; }
        public float CurrentStrength { get; private set; }

        void OnEnable() => Apply(0f);

        void Update() => Apply(Application.isPlaying ? Time.time : (float)UnityEditor_TimeSinceStartup());

        void Apply(float t)
        {
            float gust = baseStrength + (Mathf.PerlinNoise(t * gustFrequency, 0.37f) - 0.35f) * gustStrength;
            float yaw = (Mathf.PerlinNoise(0.13f, t * 0.015f) - 0.5f) * directionWander;
            CurrentDirection = Quaternion.Euler(0f, yaw, 0f) * direction.normalized;
            CurrentStrength = Mathf.Max(0.2f, gust);
            Shader.SetGlobalVector(WindId, new Vector4(CurrentDirection.x, CurrentDirection.y, CurrentDirection.z, CurrentStrength));
        }

        static double UnityEditor_TimeSinceStartup()
        {
#if UNITY_EDITOR
            return UnityEditor.EditorApplication.timeSinceStartup;
#else
            return Time.realtimeSinceStartup;
#endif
        }
    }
}
