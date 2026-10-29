using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// SpeedTree-style procedural vegetation built from real Poly Haven photo textures:
    /// conifers (fir twig cards on drooping whorled branches), broadleaf trees (composited leaf-cluster
    /// cards on forked limbs) and grass clumps. Every tree gets 3 LODs, spherical normals for soft canopy
    /// lighting and vertex colours (R wind weight, G phase, B ambient occlusion) for the foliage shader.
    /// </summary>
    public static class TreeFactory
    {
        const string Foliage = ArtLibrary.PolyHaven + "/Foliage";
        const string GenDir = "Assets/_Game/Generated/Vegetation";
        const string PrefabDir = "Assets/_Game/Prefabs/Vegetation";

        // Sub-rectangles (UV, bottom-left origin) of the source atlases: xMin, yMin, width, height.
        static readonly Rect[] FirTwigs =
        {
            Rect.MinMaxRect(0.185f, 0.69f, 0.43f, 0.95f),
            Rect.MinMaxRect(0.654f, 0.63f, 0.94f, 0.96f),
            Rect.MinMaxRect(0.30f, 0.23f, 0.645f, 0.61f),
            Rect.MinMaxRect(0.635f, 0.17f, 0.957f, 0.55f),
        };

        static readonly Rect[] Leaves =
        {
            Rect.MinMaxRect(0.016f, 0.60f, 0.146f, 0.98f),
            Rect.MinMaxRect(0.166f, 0.609f, 0.336f, 0.977f),
            Rect.MinMaxRect(0.361f, 0.639f, 0.479f, 0.971f),
            Rect.MinMaxRect(0.518f, 0.639f, 0.648f, 0.957f),
            Rect.MinMaxRect(0.693f, 0.586f, 0.816f, 0.98f),
            Rect.MinMaxRect(0.016f, 0.023f, 0.18f, 0.375f),
            Rect.MinMaxRect(0.215f, 0.004f, 0.355f, 0.424f),
            Rect.MinMaxRect(0.42f, 0.014f, 0.5625f, 0.404f),
        };

        static readonly Rect[] GrassClumps =
        {
            Rect.MinMaxRect(0.205f, 0.121f, 0.488f, 0.268f),
            Rect.MinMaxRect(0.586f, 0.14f, 0.80f, 0.248f),
            Rect.MinMaxRect(0.03f, 0.014f, 0.195f, 0.121f),
            Rect.MinMaxRect(0.234f, 0.014f, 0.479f, 0.121f),
            Rect.MinMaxRect(0.576f, 0.014f, 0.781f, 0.131f),
        };

        static System.Random rng;
        static float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        public class Result
        {
            public GameObject[] conifers;
            public GameObject[] broadleaves;
            public GameObject grass;
            public GameObject grassTall;
        }

        public static Result BuildAll(int seed)
        {
            rng = new System.Random(seed);
            ArtLibrary.EnsureFolder(GenDir);
            ArtLibrary.EnsureFolder(PrefabDir);

            // --- Materials
            var firDiff = ArtLibrary.Find($"{Foliage}/fir_tree_01", "twig_diff");
            var firAlpha = ArtLibrary.Find($"{Foliage}/fir_tree_01", "twig_alpha");
            var firNor = ArtLibrary.Find($"{Foliage}/fir_tree_01", "twig_nor_gl");
            var firAtlas = ArtLibrary.Pack("FirTwig_AlbedoAlpha", 2048,
                ArtLibrary.Channel.From(firDiff, 0), ArtLibrary.Channel.From(firDiff, 1), ArtLibrary.Channel.From(firDiff, 2),
                ArtLibrary.Channel.From(firAlpha, 0), srgb: true);
            var firLeafMat = ArtLibrary.FoliageMaterial("Fir_Needles", firAtlas, firNor, new Color(0.8f, 0.9f, 0.82f),
                cutoff: 0.35f, windStrength: 0.35f, flutter: 0.03f, translucency: 0.5f, smoothness: 0.15f);
            firLeafMat.SetFloat("_BackfaceFlip", 0f);

            var firBarkMat = ArtLibrary.FoliageMaterial("Fir_Bark",
                ArtLibrary.Find($"{Foliage}/fir_tree_01", "bark_diff"), ArtLibrary.Find($"{Foliage}/fir_tree_01", "bark_nor_gl"),
                Color.white, windStrength: 0.35f, flutter: 0f, translucency: 0f, smoothness: 0.1f, alphaClip: false);

            var leafCluster = BuildLeafClusterTexture();
            var leafMat = ArtLibrary.FoliageMaterial("Broadleaf_Leaves", leafCluster, null, new Color(0.85f, 0.95f, 0.8f),
                cutoff: 0.4f, windStrength: 0.45f, flutter: 0.06f, translucency: 1.1f, smoothness: 0.3f);
            leafMat.SetFloat("_BackfaceFlip", 0f);

            var oakBark = $"{ArtLibrary.PolyHaven}/Textures/bark_brown_02";
            var broadBarkMat = ArtLibrary.FoliageMaterial("Broadleaf_Bark",
                ArtLibrary.Find(oakBark, "_diff"), ArtLibrary.Find(oakBark, "_nor_gl"), Color.white,
                windStrength: 0.45f, flutter: 0f, translucency: 0f, smoothness: 0.1f, alphaClip: false);
            broadBarkMat.SetTextureScale("_BaseMap", new Vector2(1f, 1f));

            var grassDiff = ArtLibrary.Find($"{Foliage}/grass_medium_01", "_diff");
            var grassAlpha = ArtLibrary.Find($"{Foliage}/grass_medium_01", "_alpha");
            var grassAtlas = ArtLibrary.Pack("Grass_AlbedoAlpha", 2048,
                ArtLibrary.Channel.From(grassDiff, 0), ArtLibrary.Channel.From(grassDiff, 1), ArtLibrary.Channel.From(grassDiff, 2),
                ArtLibrary.Channel.From(grassAlpha, 0), srgb: true);
            var grassMat = ArtLibrary.FoliageMaterial("Grass", grassAtlas, null, new Color(0.95f, 1.05f, 0.8f),
                cutoff: 0.3f, windStrength: 0.18f, flutter: 0.02f, translucency: 0.9f, smoothness: 0.1f);
            grassMat.SetFloat("_BackfaceFlip", 0f);
            grassMat.SetFloat("_ColorVariation", 0.35f);

            var result = new Result
            {
                conifers = new GameObject[4],
                broadleaves = new GameObject[4],
            };
            for (int i = 0; i < result.conifers.Length; i++)
                result.conifers[i] = SaveTree($"Fir_{i}", lod => BuildConifer(seed * 31 + i, lod), firBarkMat, firLeafMat, 0.35f);
            for (int i = 0; i < result.broadleaves.Length; i++)
                result.broadleaves[i] = SaveTree($"Broadleaf_{i}", lod => BuildBroadleaf(seed * 57 + i, lod), broadBarkMat, leafMat, 0.4f);

            result.grass = SaveSimple("GrassClump", BuildGrassClump(seed, 6, 0.55f, 0.85f), grassMat);
            result.grassTall = SaveSimple("GrassClumpTall", BuildGrassClump(seed + 1, 8, 0.8f, 1.3f), grassMat);
            return result;
        }

        // ================================================================ Mesh builder

        class MeshBuilder
        {
            public readonly List<Vector3> v = new();
            public readonly List<Vector3> n = new();
            public readonly List<Vector2> uv = new();
            public readonly List<Color> c = new();
            public readonly List<int>[] t = { new(), new() };

            public void Tube(IList<Vector3> pts, IList<float> radii, int sides, IList<float> wind, float phase, float uvPerMeter, float ao = 1f)
            {
                int start = v.Count;
                float len = 0f;
                for (int i = 0; i < pts.Count; i++)
                {
                    if (i > 0) len += Vector3.Distance(pts[i], pts[i - 1]);
                    Vector3 tangent = (pts[Mathf.Min(i + 1, pts.Count - 1)] - pts[Mathf.Max(i - 1, 0)]).normalized;
                    Vector3 refUp = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                    Vector3 side = Vector3.Cross(tangent, refUp).normalized;
                    Vector3 bin = Vector3.Cross(side, tangent).normalized;
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = (float)k / sides * Mathf.PI * 2f;
                        Vector3 dir = Mathf.Cos(a) * side + Mathf.Sin(a) * bin;
                        v.Add(pts[i] + dir * radii[i]);
                        n.Add(dir);
                        uv.Add(new Vector2((float)k / sides, len * uvPerMeter));
                        c.Add(new Color(wind[i], phase, ao, 1f));
                    }
                }
                for (int i = 0; i < pts.Count - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int b0 = start + i * (sides + 1) + k, b1 = b0 + 1;
                    int t0 = b0 + sides + 1, t1 = t0 + 1;
                    t[0].AddRange(new[] { t0, t1, b0, t1, b1, b0 });
                }
            }

            /// <summary>Quad from basePos extending along 'up' by length, centred on 'right'.</summary>
            public void Card(Vector3 basePos, Vector3 up, Vector3 right, float length, float width, Rect r,
                Vector3 bendFrom, float bend, float windBase, float windTip, float phase, float aoBase, float aoTip)
            {
                Vector3 face = Vector3.Cross(up, right).normalized;
                Vector3 mid = basePos + up * (length * 0.5f);
                Vector3 outward = (mid - bendFrom).normalized;
                if (Vector3.Dot(face, outward) < 0f) face = -face;
                int s = v.Count;
                Vector3[] corners =
                {
                    basePos - right * (width * 0.5f),
                    basePos + right * (width * 0.5f),
                    basePos + right * (width * 0.5f) + up * length,
                    basePos - right * (width * 0.5f) + up * length,
                };
                Vector2[] uvs = { new(r.xMin, r.yMin), new(r.xMax, r.yMin), new(r.xMax, r.yMax), new(r.xMin, r.yMax) };
                for (int i = 0; i < 4; i++)
                {
                    bool tip = i >= 2;
                    Vector3 sph = (corners[i] - bendFrom).normalized;
                    v.Add(corners[i]);
                    n.Add(Vector3.Slerp(face, sph, bend).normalized);
                    uv.Add(uvs[i]);
                    c.Add(new Color(tip ? windTip : windBase, phase, tip ? aoTip : aoBase, 1f));
                }
                t[1].AddRange(new[] { s, s + 3, s + 2, s, s + 2, s + 1 });
            }

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(v);
                m.SetNormals(n);
                m.SetUVs(0, uv);
                m.SetColors(c);
                if (t[0].Count == 0 || t[1].Count == 0)
                {
                    m.subMeshCount = 1;
                    m.SetTriangles(t[0].Count > 0 ? t[0] : t[1], 0);
                }
                else
                {
                    m.subMeshCount = 2;
                    m.SetTriangles(t[0], 0);
                    m.SetTriangles(t[1], 1);
                }
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }

        // ================================================================ Conifer

        static Mesh BuildConifer(int seed, int lod)
        {
            var r = new System.Random(seed);
            float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var mb = new MeshBuilder();

            float height = Rnd(13f, 21f);
            float trunkR = height * Rnd(0.018f, 0.024f);
            float lean = Rnd(-0.03f, 0.03f);

            var trunkPts = new List<Vector3>();
            var trunkRad = new List<float>();
            var trunkWind = new List<float>();
            int segs = lod == 0 ? 8 : 4;
            for (int i = 0; i <= segs; i++)
            {
                float t = (float)i / segs;
                trunkPts.Add(new Vector3(Mathf.Sin(t * 3f + seed) * 0.08f + lean * t * height, t * height, Mathf.Cos(t * 2f + seed) * 0.08f));
                trunkRad.Add(trunkR * Mathf.Pow(1f - t, 0.8f) + 0.02f);
                trunkWind.Add(0.12f * t * t);
            }
            mb.Tube(trunkPts, trunkRad, lod == 0 ? 10 : lod == 1 ? 6 : 4, trunkWind, 0f, 0.35f, 0.8f);

            float y = height * Rnd(0.1f, 0.16f);
            int whorl = 0;
            while (y < height * 0.96f)
            {
                float t = y / height;
                Vector3 axis = Vector3.Lerp(trunkPts[0], trunkPts[segs], t);
                axis.y = y;
                int count = r.Next(5, 8);
                float angle0 = Rnd(0f, 360f);
                bool keep = lod < 2 || whorl % 2 == 0;
                for (int b = 0; b < count && keep; b++)
                {
                    float a = (angle0 + b * 360f / count + Rnd(-15f, 15f)) * Mathf.Deg2Rad;
                    float len = (0.45f + (1f - t) * height * 0.24f) * Rnd(0.8f, 1.15f);
                    float pitch = Mathf.Lerp(28f, -12f, 1f - t) * Mathf.Deg2Rad;
                    var dir = new Vector3(Mathf.Cos(a) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Sin(a) * Mathf.Cos(pitch));
                    float phase = (float)r.NextDouble();
                    float w0 = 0.12f * t * t + 0.1f;

                    var pts = new List<Vector3>();
                    var rad = new List<float>();
                    var wnd = new List<float>();
                    for (int k = 0; k <= 3; k++)
                    {
                        float f = k / 3f;
                        pts.Add(axis + dir * (len * f) + Vector3.down * (len * 0.14f * f * f));
                        rad.Add(Mathf.Lerp(0.025f + len * 0.012f, 0.006f, f));
                        wnd.Add(w0 + f * (0.35f + len * 0.06f));
                    }
                    if (lod == 0 && len > 0.9f) mb.Tube(pts, rad, 4, wnd, phase, 1f, 0.6f);

                    int cards = Mathf.Max(1, Mathf.RoundToInt(len / (lod == 0 ? 0.42f : 0.8f)));
                    for (int j = 0; j < cards; j++)
                    {
                        float f = (j + 0.5f) / cards;
                        Vector3 p = PointOn(pts, f);
                        Vector3 along = (PointOn(pts, Mathf.Min(1f, f + 0.1f)) - PointOn(pts, Mathf.Max(0f, f - 0.1f))).normalized;
                        Vector3 right = Vector3.Cross(Vector3.up, along).normalized;
                        if (right.sqrMagnitude < 0.01f) right = Vector3.right;
                        float cl = Mathf.Min(2.6f, 0.85f + len * 0.42f) * (lod == 0 ? 1f : 1.3f) * Rnd(0.85f, 1.15f);
                        var rect = FirTwigs[r.Next(FirTwigs.Length)];
                        float aspect = rect.width / rect.height;
                        Vector3 bendFrom = new(axis.x, p.y - 1.5f, axis.z);
                        float ao0 = Mathf.Lerp(0.35f, 0.9f, f) * Mathf.Lerp(0.75f, 1f, t);
                        float ao1 = Mathf.Min(1f, ao0 + 0.15f);
                        float wb = w0 + f * 0.4f, wt = wb + 0.25f;
                        Vector3 basePos = p - along * (cl * 0.25f);

                        // Flat layer card, slightly rolled
                        Vector3 rollRight = Quaternion.AngleAxis(Rnd(-20f, 20f), along) * right;
                        mb.Card(basePos, along, rollRight, cl, cl * aspect, rect, bendFrom, 0.7f, wb, wt, phase, ao0, ao1);
                        if (lod < 2)
                        {
                            Vector3 crossRight = Quaternion.AngleAxis(Rnd(50f, 70f), along) * right;
                            mb.Card(basePos, along, crossRight, cl * 0.9f, cl * 0.9f * aspect, FirTwigs[r.Next(FirTwigs.Length)],
                                bendFrom, 0.7f, wb, wt, phase, ao0 * 0.9f, ao1);
                        }
                        if (lod == 0)
                        {
                            // Drooping sub-layer gives the dense, layered look of real firs
                            Vector3 droop = (along + Vector3.down * 0.5f).normalized;
                            Vector3 dRight = Quaternion.AngleAxis(Rnd(-60f, -40f), along) * right;
                            mb.Card(basePos + Vector3.down * 0.1f, droop, dRight, cl * 0.8f, cl * 0.8f * aspect, FirTwigs[r.Next(FirTwigs.Length)],
                                bendFrom, 0.7f, wb, wt, phase, ao0 * 0.8f, ao1 * 0.9f);
                        }
                    }
                }
                y += Mathf.Lerp(0.34f, 0.5f, (float)r.NextDouble());
                whorl++;
            }

            // Leader at the very top
            Vector3 top = trunkPts[segs];
            for (int k = 0; k < (lod == 0 ? 3 : 2); k++)
            {
                Vector3 up = Quaternion.Euler(Rnd(-12f, 12f), 0f, Rnd(-12f, 12f)) * Vector3.up;
                Vector3 right = Quaternion.AngleAxis(k * 60f, Vector3.up) * Vector3.right;
                mb.Card(top - Vector3.up * 1.4f, up, right, 1.8f, 1.2f, FirTwigs[0], top - Vector3.up * 3f, 0.5f, 0.3f, 0.6f, 0.3f, 0.8f, 1f);
            }
            return mb.Build($"Fir_{seed}_LOD{lod}");
        }

        // ================================================================ Broadleaf

        static Mesh BuildBroadleaf(int seed, int lod)
        {
            var r = new System.Random(seed);
            float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var mb = new MeshBuilder();

            float trunkH = Rnd(3f, 4.8f);
            float trunkR = Rnd(0.28f, 0.42f);
            var lean = new Vector3(Rnd(-0.4f, 0.4f), 0f, Rnd(-0.4f, 0.4f));

            var tp = new List<Vector3>();
            var tr = new List<float>();
            var tw = new List<float>();
            for (int i = 0; i <= 4; i++)
            {
                float t = i / 4f;
                tp.Add(lean * t * t + Vector3.up * (trunkH * t));
                tr.Add(Mathf.Lerp(trunkR * 1.25f, trunkR * 0.75f, t));
                tw.Add(0.05f * t);
            }
            mb.Tube(tp, tr, lod == 0 ? 10 : 6, tw, 0f, 0.5f, 0.8f);
            Vector3 crown = tp[4];

            var clusters = new List<(Vector3 pos, float wind, float phase)>();
            int limbs = r.Next(4, 7);
            float limb0 = Rnd(0f, 360f);
            for (int l = 0; l < limbs; l++)
            {
                float yaw = (limb0 + l * 360f / limbs + Rnd(-20f, 20f)) * Mathf.Deg2Rad;
                float spread = Rnd(30f, 58f) * Mathf.Deg2Rad;
                var dir = new Vector3(Mathf.Cos(yaw) * Mathf.Sin(spread), Mathf.Cos(spread), Mathf.Sin(yaw) * Mathf.Sin(spread));
                float len = Rnd(3.2f, 5.5f);
                float phase = (float)r.NextDouble();
                Vector3 start = crown - Vector3.up * Rnd(0f, 0.8f);

                var lp = new List<Vector3>();
                var lr = new List<float>();
                var lw = new List<float>();
                for (int k = 0; k <= 4; k++)
                {
                    float f = k / 4f;
                    Vector3 bendUp = Vector3.up * (len * 0.15f * f * f);
                    lp.Add(start + dir * (len * f) + bendUp);
                    lr.Add(Mathf.Lerp(trunkR * 0.6f, 0.05f, f));
                    lw.Add(0.08f + f * 0.3f);
                }
                if (lod < 2) mb.Tube(lp, lr, lod == 0 ? 6 : 4, lw, phase, 0.6f, 0.75f);
                for (int k = 0; k < 4; k++)
                    clusters.Add((PointOn(lp, Mathf.Lerp(0.45f, 1f, k / 3f)) + RandomInSphere(r) * 0.6f, 0.3f + k * 0.06f, phase));

                int subs = r.Next(3, 6);
                for (int s = 0; s < subs; s++)
                {
                    float f0 = Rnd(0.35f, 0.95f);
                    Vector3 basePt = PointOn(lp, f0);
                    Vector3 sdir = (Quaternion.AngleAxis(Rnd(-60f, 60f), Vector3.up) * dir + Vector3.up * Rnd(0.1f, 0.6f)).normalized;
                    float slen = Rnd(1.4f, 2.8f);
                    var sp = new List<Vector3>();
                    var sr = new List<float>();
                    var sw = new List<float>();
                    for (int k = 0; k <= 2; k++)
                    {
                        float f = k / 2f;
                        sp.Add(basePt + sdir * (slen * f) + Vector3.down * (0.2f * f * f));
                        sr.Add(Mathf.Lerp(0.05f, 0.015f, f));
                        sw.Add(0.08f + f0 * 0.3f + f * 0.3f);
                    }
                    if (lod == 0) mb.Tube(sp, sr, 4, sw, phase, 1f, 0.7f);
                    int per = lod == 0 ? 4 : 2;
                    for (int c = 0; c < per; c++)
                        clusters.Add((PointOn(sp, Mathf.Lerp(0.35f, 1f, (float)c / Mathf.Max(1, per - 1))) + RandomInSphere(r) * 0.35f,
                            0.5f + f0 * 0.3f, phase));
                }
            }

            Vector3 center = Vector3.zero;
            foreach (var cl in clusters) center += cl.pos;
            center /= clusters.Count;
            float canopyR = 0.01f;
            foreach (var cl in clusters) canopyR = Mathf.Max(canopyR, Vector3.Distance(cl.pos, center));

            // Fill the crown volume: extra clusters on an ellipsoid shell and inside it,
            // so the silhouette reads as a full canopy rather than tufts at branch tips.
            var radii = new Vector3(canopyR * 0.95f, canopyR * 0.7f, canopyR * 0.95f);
            int shell = lod == 0 ? 70 : lod == 1 ? 45 : 30;
            for (int s = 0; s < shell; s++)
            {
                Vector3 d = RandomInSphere(r).normalized;
                if (d.y < -0.45f) d.y = -d.y * 0.5f; // keep the underside open
                float depth = s % 3 == 0 ? Rnd(0.4f, 0.75f) : Rnd(0.8f, 1.05f);
                var p = center + Vector3.Scale(d, radii) * depth + Vector3.up * canopyR * 0.15f;
                clusters.Add((p, 0.55f + depth * 0.35f, (float)r.NextDouble()));
            }
            Vector3 bendFrom = center - Vector3.up * canopyR * 0.4f;

            for (int i = 0; i < clusters.Count; i++)
            {
                if (lod == 2 && i % 2 == 1) continue;
                var cl = clusters[i];
                Vector3 outward = (cl.pos - center).normalized;
                Vector3 up = (outward + Vector3.up * 0.6f).normalized;
                float size = Rnd(2.1f, 3.1f) * (lod == 0 ? 1f : lod == 1 ? 1.25f : 1.6f);
                int cards = lod == 0 ? 3 : lod == 1 ? 2 : 1;
                float rot0 = Rnd(0f, 180f);
                float depth = Mathf.Clamp01(Vector3.Distance(cl.pos, center) / canopyR);
                float ao0 = Mathf.Lerp(0.35f, 0.85f, depth);
                for (int c = 0; c < cards; c++)
                {
                    Vector3 basis = Mathf.Abs(Vector3.Dot(up, Vector3.up)) > 0.95f ? Vector3.right : Vector3.Cross(Vector3.up, up).normalized;
                    Vector3 right = Quaternion.AngleAxis(rot0 + c * 180f / cards, up) * basis;
                    mb.Card(cl.pos - up * (size * 0.3f), up, right, size, size, new Rect(0, 0, 1, 1),
                        bendFrom, 0.8f, cl.wind, cl.wind + 0.3f, cl.phase, ao0, Mathf.Min(1f, ao0 + 0.2f));
                }
            }
            return mb.Build($"Broadleaf_{seed}_LOD{lod}");
        }

        // ================================================================ Grass

        static Mesh BuildGrassClump(int seed, int cards, float minH, float maxH)
        {
            var r = new System.Random(seed);
            float Rnd(float a, float b) => a + (float)r.NextDouble() * (b - a);
            var mb = new MeshBuilder();
            for (int i = 0; i < cards; i++)
            {
                var rect = GrassClumps[r.Next(GrassClumps.Length)];
                float aspect = rect.width / rect.height;
                float h = Rnd(minH, maxH) * 0.5f;
                float w = h * aspect;
                float yaw = Rnd(0f, 360f);
                Vector3 offset = Quaternion.Euler(0f, Rnd(0f, 360f), 0f) * Vector3.forward * Rnd(0f, 0.25f);
                Vector3 right = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3 up = Quaternion.AngleAxis(Rnd(-12f, 12f), right) * Vector3.up;
                mb.Card(offset - Vector3.up * 0.03f, up, right, h, w, rect, Vector3.down * 2f, 0.85f, 0f, 1f, (float)r.NextDouble(), 0.45f, 1f);
            }
            return mb.Build($"Grass_{seed}");
        }

        // ================================================================ Leaf cluster atlas

        /// <summary>
        /// Composites individual scanned leaves around a twig into a single 1024² cluster texture,
        /// like the leaf atlases authored for SpeedTree.
        /// </summary>
        static Texture2D BuildLeafClusterTexture()
        {
            string path = $"{GenDir}/LeafCluster.png";
            if (System.IO.File.Exists(path)) return AssetDatabase.LoadAssetAtPath<Texture2D>(path);

            var diffTex = ArtLibrary.Find($"{Foliage}/island_tree_01", "leaves_diff");
            var alphaTex = ArtLibrary.Find($"{Foliage}/island_tree_01", "leaves_alpha");
            const int S = 2048, C = 1024;
            var diff = ArtLibrary.Read(diffTex, S, true);
            var alpha = ArtLibrary.Read(alphaTex, S, false);

            var canvas = new Color[C * C];
            var fill = new Color(0.22f, 0.32f, 0.12f, 0f);
            for (int i = 0; i < canvas.Length; i++) canvas[i] = fill;

            var cr = new System.Random(7);
            float Rnd(float a, float b) => a + (float)cr.NextDouble() * (b - a);
            var twig = new Color(0.32f, 0.24f, 0.14f, 1f);
            var root = new Vector2(C * 0.5f, C * 0.02f);
            var tipMain = new Vector2(C * 0.5f, C * 0.8f);
            DrawLine(canvas, C, root, tipMain, 9f, 3f, twig);

            var leaves = new List<(Vector2 basePt, float angle, float len, int rect)>();
            for (int i = 0; i < 46; i++)
            {
                float t = Rnd(0.12f, 1f);
                var onStem = Vector2.Lerp(root, tipMain, t);
                float side = i % 2 == 0 ? 1f : -1f;
                float spread = Rnd(15f, 75f) * side;
                float angle = (90f - spread) * Mathf.Deg2Rad;
                var twigDir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float twigLen = Rnd(0f, C * 0.18f) * (1.1f - t);
                var leafBase = onStem + twigDir * twigLen;
                if (twigLen > 4f) DrawLine(canvas, C, onStem, leafBase, 3.5f, 1.5f, twig);
                float leafAngle = angle + Rnd(-0.35f, 0.35f);
                leaves.Add((leafBase, leafAngle, Rnd(C * 0.17f, C * 0.27f) * (1.15f - t * 0.35f), cr.Next(Leaves.Length)));
            }
            // Painter's order: lower leaves first so upper ones overlap them.
            leaves.Sort((a, b) => a.basePt.y.CompareTo(b.basePt.y));
            foreach (var l in leaves)
            {
                var rect = Leaves[l.rect];
                float aspect = rect.width / rect.height;
                float shade = Rnd(0.78f, 1.08f);
                StampLeaf(canvas, C, diff, alpha, S, rect, l.basePt, l.angle, l.len, l.len * aspect, shade);
            }

            var tex = new Texture2D(C, C, TextureFormat.RGBA32, true, false);
            tex.SetPixels(canvas);
            tex.Apply();
            System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.alphaIsTransparency = true;
            imp.mipmapEnabled = true;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static void StampLeaf(Color[] canvas, int C, Color[] diff, Color[] alpha, int S, Rect src, Vector2 basePt,
            float angle, float length, float width, float shade)
        {
            var d = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            var p = new Vector2(-d.y, d.x);
            Vector2[] corners = { basePt - p * width * 0.5f, basePt + p * width * 0.5f, basePt + d * length - p * width * 0.5f, basePt + d * length + p * width * 0.5f };
            int x0 = C, y0 = C, x1 = 0, y1 = 0;
            foreach (var c in corners)
            {
                x0 = Mathf.Min(x0, (int)c.x); y0 = Mathf.Min(y0, (int)c.y);
                x1 = Mathf.Max(x1, (int)c.x + 1); y1 = Mathf.Max(y1, (int)c.y + 1);
            }
            x0 = Mathf.Clamp(x0, 0, C - 1); y0 = Mathf.Clamp(y0, 0, C - 1);
            x1 = Mathf.Clamp(x1, 0, C - 1); y1 = Mathf.Clamp(y1, 0, C - 1);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var rel = new Vector2(x, y) - basePt;
                float a = Vector2.Dot(rel, d) / length;
                float b = Vector2.Dot(rel, p) / width + 0.5f;
                if (a < 0f || a > 1f || b < 0f || b > 1f) continue;
                float su = src.xMin + b * src.width;
                float sv = src.yMin + a * src.height;
                int si = Mathf.Clamp((int)(sv * S), 0, S - 1) * S + Mathf.Clamp((int)(su * S), 0, S - 1);
                float sa = alpha[si].r;
                if (sa <= 0.01f) continue;
                Color sc = diff[si] * shade;
                int ci = y * C + x;
                Color dst = canvas[ci];
                canvas[ci] = new Color(
                    Mathf.Lerp(dst.r, sc.r, sa), Mathf.Lerp(dst.g, sc.g, sa), Mathf.Lerp(dst.b, sc.b, sa),
                    sa + dst.a * (1f - sa));
            }
        }

        static void DrawLine(Color[] canvas, int C, Vector2 a, Vector2 b, float w0, float w1, Color col)
        {
            int x0 = Mathf.Clamp((int)(Mathf.Min(a.x, b.x) - w0), 0, C - 1), x1 = Mathf.Clamp((int)(Mathf.Max(a.x, b.x) + w0), 0, C - 1);
            int y0 = Mathf.Clamp((int)(Mathf.Min(a.y, b.y) - w0), 0, C - 1), y1 = Mathf.Clamp((int)(Mathf.Max(a.y, b.y) + w0), 0, C - 1);
            Vector2 ab = b - a;
            float len2 = Mathf.Max(ab.sqrMagnitude, 1e-4f);
            for (int y = y0; y <= y1; y++)
            for (int x = x0; x <= x1; x++)
            {
                var pt = new Vector2(x, y);
                float t = Mathf.Clamp01(Vector2.Dot(pt - a, ab) / len2);
                float dist = Vector2.Distance(pt, a + ab * t);
                float w = Mathf.Lerp(w0, w1, t) * 0.5f;
                float cov = Mathf.Clamp01(w - dist + 0.5f);
                if (cov <= 0f) continue;
                int ci = y * C + x;
                Color dst = canvas[ci];
                canvas[ci] = new Color(Mathf.Lerp(dst.r, col.r, cov), Mathf.Lerp(dst.g, col.g, cov), Mathf.Lerp(dst.b, col.b, cov), Mathf.Max(dst.a, cov));
            }
        }

        // ================================================================ Prefab helpers

        static GameObject SaveTree(string name, System.Func<int, Mesh> build, Material bark, Material leaves, float colliderRadius)
        {
            var root = new GameObject(name);
            var lods = new LOD[3];
            float[] transitions = { 0.3f, 0.09f, 0.008f };
            Bounds bounds = default;
            for (int l = 0; l < 3; l++)
            {
                var mesh = SaveMesh(build(l), $"{GenDir}/{name}_LOD{l}.asset");
                var child = new GameObject($"LOD{l}");
                child.transform.SetParent(root.transform, false);
                child.AddComponent<MeshFilter>().sharedMesh = mesh;
                var mr = child.AddComponent<MeshRenderer>();
                mr.sharedMaterials = mesh.subMeshCount > 1 ? new[] { bark, leaves } : new[] { bark };
                lods[l] = new LOD(transitions[l], new Renderer[] { mr });
                if (l == 0) bounds = mesh.bounds;
            }
            var lodGroup = root.AddComponent<LODGroup>();
            lodGroup.SetLODs(lods);
            lodGroup.RecalculateBounds();
            var col = root.AddComponent<CapsuleCollider>();
            col.radius = colliderRadius;
            col.height = Mathf.Min(bounds.size.y, 6f);
            col.center = new Vector3(0f, col.height * 0.5f, 0f);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(root);
            return prefab;
        }

        static GameObject SaveSimple(string name, Mesh mesh, Material mat)
        {
            mesh = SaveMesh(mesh, $"{GenDir}/{name}.asset");
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{PrefabDir}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        static Mesh SaveMesh(Mesh mesh, string path)
        {
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing)
            {
                EditorUtility.CopySerialized(mesh, existing);
                Object.DestroyImmediate(mesh);
                return existing;
            }
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        static Vector3 PointOn(IList<Vector3> pts, float f)
        {
            float x = Mathf.Clamp01(f) * (pts.Count - 1);
            int i = Mathf.Min((int)x, pts.Count - 2);
            return Vector3.Lerp(pts[i], pts[i + 1], x - i);
        }

        static Vector3 RandomInSphere(System.Random r)
        {
            var v = new Vector3((float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1, (float)r.NextDouble() * 2 - 1);
            return v.sqrMagnitude > 1f ? v.normalized : v;
        }
    }
}
