using UnityEngine;

namespace LastWorld
{
    /// <summary>
    /// Pins the top edge of a cloth cape to the shoulders and feeds it the world wind so it flutters.
    /// </summary>
    [RequireComponent(typeof(Cloth))]
    public class CapeCloth : MonoBehaviour
    {
        public float pinnedBand = 0.04f;
        public float maxSwing = 0.8f;
        public float windForce = 3f;

        Cloth cloth;
        WindController wind;

        void Start()
        {
            cloth = GetComponent<Cloth>();
            wind = FindAnyObjectByType<WindController>();

            var verts = cloth.vertices;
            float top = float.MinValue;
            foreach (var v in verts) top = Mathf.Max(top, v.y);
            var coeff = cloth.coefficients;
            for (int i = 0; i < coeff.Length && i < verts.Length; i++)
            {
                float d = top - verts[i].y;
                coeff[i].maxDistance = d < pinnedBand ? 0f : Mathf.Min(maxSwing, d * 1.3f);
                coeff[i].collisionSphereDistance = 0.02f;
            }
            cloth.coefficients = coeff;
        }

        void Update()
        {
            if (!wind) return;
            Vector3 w = wind.CurrentDirection * (wind.CurrentStrength * windForce);
            cloth.externalAcceleration = w;
            cloth.randomAcceleration = w * 0.6f + Vector3.one * 0.5f;
        }
    }
}
