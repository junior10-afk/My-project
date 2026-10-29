using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace LastWorld.EditorTools
{
    /// <summary>
    /// Builds the hero entirely in code: a 22-bone Unity-Humanoid skeleton (T-pose), a skinned body
    /// modelled from lofted cross-sections (torso, limbs, boots), a stylised face (eyes, brows, nose,
    /// mouth, ears), anime-style hair clumps and braid, an adventurer outfit (tunic with skirt, trousers,
    /// leather gloves/boots/belt, gold trims, scarf) and a cloth-simulated cape. Produces a Humanoid
    /// Avatar so mocap clips can be retargeted later; ProceduralAnimator animates it meanwhile.
    /// Menu: Last World/Rebuild Player Character.
    /// </summary>
    public static class CharacterFactory
    {
        const string Dir = "Assets/_Game/Generated/Character";
        const string MatDir = "Assets/_Game/Materials/Character";
        const string PrefabPath = "Assets/_Game/Prefabs/Hero.prefab";

        static readonly string[] BoneNames =
        {
            "Hips", "Spine", "Chest", "UpperChest", "Neck", "Head",
            "LeftShoulder", "LeftUpperArm", "LeftLowerArm", "LeftHand",
            "RightShoulder", "RightUpperArm", "RightLowerArm", "RightHand",
            "LeftUpperLeg", "LeftLowerLeg", "LeftFoot", "LeftToes",
            "RightUpperLeg", "RightLowerLeg", "RightFoot", "RightToes",
        };

        static readonly int[] Parents = { -1, 0, 1, 2, 3, 4, 3, 6, 7, 8, 3, 10, 11, 12, 0, 14, 15, 16, 0, 18, 19, 20 };

        static readonly Vector3[] Pos =
        {
            new(0f, 0.95f, 0f), new(0f, 1.05f, 0f), new(0f, 1.2f, 0f), new(0f, 1.33f, 0f), new(0f, 1.48f, 0f), new(0f, 1.58f, 0f),
            new(0.04f, 1.42f, 0f), new(0.16f, 1.43f, 0f), new(0.43f, 1.43f, 0f), new(0.66f, 1.43f, 0f),
            new(-0.04f, 1.42f, 0f), new(-0.16f, 1.43f, 0f), new(-0.43f, 1.43f, 0f), new(-0.66f, 1.43f, 0f),
            new(0.09f, 0.93f, 0f), new(0.09f, 0.51f, 0f), new(0.09f, 0.09f, 0f), new(0.09f, 0.02f, 0.12f),
            new(-0.09f, 0.93f, 0f), new(-0.09f, 0.51f, 0f), new(-0.09f, 0.09f, 0f), new(-0.09f, 0.02f, 0.12f),
        };

        const int H = 0, SP = 1, CH = 2, UC = 3, NK = 4, HD = 5;
        static int UA(int side) => side > 0 ? 7 : 11;
        static int LA(int side) => side > 0 ? 8 : 12;
        static int HA(int side) => side > 0 ? 9 : 13;
        static int UL(int side) => side > 0 ? 14 : 18;
        static int LL(int side) => side > 0 ? 15 : 19;
        static int FT(int side) => side > 0 ? 16 : 20;
        static int TO(int side) => side > 0 ? 17 : 21;

        enum M { Skin, Tunic, Pants, Leather, Hair, EyeWhite, Iris, Dark, Gold, Scarf, Count }

        // ================================================================ Entry points

        [MenuItem("Last World/Rebuild Player Character")]
        public static void RebuildInScene()
        {
            var player = Object.FindAnyObjectByType<PlayerController>();
            if (!player)
            {
                Debug.LogError("[CharacterFactory] No PlayerController in the open scene.");
                return;
            }
            Attach(player, LayerMask.NameToLayer("Player"));
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            EditorSceneManager.SaveScene(player.gameObject.scene);
            Debug.Log("[CharacterFactory] Hero rebuilt and attached to the player.");
        }

        /// <summary>Replaces the player's visual with a freshly built hero and wires the animator.</summary>
        public static void Attach(PlayerController player, int layer)
        {
            var visual = player.visual;
            for (int i = visual.childCount - 1; i >= 0; i--) Object.DestroyImmediate(visual.GetChild(i).gameObject);

            var prefab = BuildPrefab();
            var hero = (GameObject)PrefabUtility.InstantiatePrefab(prefab, visual);
            hero.transform.localPosition = new Vector3(0f, -1f, 0f);
            hero.transform.localRotation = Quaternion.identity;
            if (layer >= 0)
                foreach (var t in hero.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;

            var anim = player.GetComponent<ProceduralAnimator>();
            if (!anim) anim = player.gameObject.AddComponent<ProceduralAnimator>();
            anim.controller = player;
            anim.rig = hero.GetComponent<Animator>();
            player.visualBob = false;
            EditorUtility.SetDirty(player);
            EditorUtility.SetDirty(anim);
        }

        public static GameObject BuildPrefab()
        {
            ArtLibrary.EnsureFolder(Dir);
            ArtLibrary.EnsureFolder(MatDir);
            var root = Build();
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ================================================================ Build

        static GameObject Build()
        {
            var root = new GameObject("Hero");
            var bones = new Transform[BoneNames.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                var t = new GameObject(BoneNames[i]).transform;
                t.SetParent(Parents[i] < 0 ? root.transform : bones[Parents[i]], false);
                t.position = Pos[i];
                t.rotation = Quaternion.identity;
                bones[i] = t;
            }

            var mats = Materials();
            var sk = new Skin((int)M.Count);
            BuildTorso(sk);
            BuildHead(sk);
            BuildHair(sk);
            BuildArms(sk);
            BuildLegs(sk);

            var bindposes = bones.Select(b => b.worldToLocalMatrix * root.transform.localToWorldMatrix).ToArray();
            var bodyMesh = Save(sk.ToMesh("HeroBody", bindposes), $"{Dir}/HeroBody.asset");
            var body = new GameObject("Corps");
            body.transform.SetParent(root.transform, false);
            var smr = body.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = bodyMesh;
            smr.bones = bones;
            smr.rootBone = bones[H];
            smr.sharedMaterials = mats;
            smr.updateWhenOffscreen = true;

            BuildCape(root, bones, bindposes, mats[(int)M.Scarf]);

            var avatar = BuildAvatar(root);
            var animator = root.AddComponent<Animator>();
            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.enabled = false; // ProceduralAnimator drives the bones; the avatar is kept for future mocap clips
            return root;
        }

        // ================================================================ Body parts

        static void BuildTorso(Skin sk)
        {
            // y, half-width, half-depth, boneA, boneB, weightB
            var rings = new (float y, float rx, float rz, int a, int b, float w)[]
            {
                (0.76f, 0.205f, 0.16f, H, H, 0f),
                (0.84f, 0.185f, 0.142f, H, H, 0f),
                (0.90f, 0.168f, 0.122f, H, H, 0f),
                (0.96f, 0.162f, 0.115f, H, H, 0f),
                (1.03f, 0.146f, 0.102f, H, SP, 0.5f),
                (1.10f, 0.136f, 0.096f, SP, SP, 0f),
                (1.20f, 0.149f, 0.104f, SP, CH, 0.5f),
                (1.30f, 0.168f, 0.112f, CH, CH, 0f),
                (1.38f, 0.178f, 0.108f, CH, UC, 0.6f),
                (1.44f, 0.158f, 0.094f, UC, UC, 0f),
                (1.48f, 0.075f, 0.065f, UC, NK, 0.4f),
            };
            sk.Loft(rings.Select(r => new Vector3(0f, r.y, r.y > 1.25f && r.y < 1.42f ? 0.012f : 0f)).ToArray(),
                rings.Select(r => new Vector2(r.rx, r.rz)).ToArray(),
                rings.Select(r => r.a).ToArray(), rings.Select(r => r.b).ToArray(), rings.Select(r => r.w).ToArray(),
                24, (int)M.Tunic, false, true, 3f);

            // Gold hem trim and leather belt with buckle
            Band(sk, 0.765f, 0.785f, 0.21f, 0.164f, H, (int)M.Gold);
            Band(sk, 0.962f, 1.0f, 0.172f, 0.124f, H, (int)M.Leather);
            sk.Ellipsoid(new Vector3(0f, 0.981f, 0.124f), new Vector3(0.03f, 0.022f, 0.01f), 10, 6, (int)M.Gold, H);

            // Neck
            sk.Loft(new[] { new Vector3(0f, 1.45f, 0f), new Vector3(0f, 1.55f, 0.005f), new Vector3(0f, 1.63f, 0.008f) },
                new[] { new Vector2(0.052f, 0.05f), new Vector2(0.047f, 0.046f), new Vector2(0.045f, 0.045f) },
                new[] { UC, NK, NK }, new[] { NK, NK, HD }, new[] { 0.5f, 0f, 0.7f }, 14, (int)M.Skin, false, false, 3f);

            // Scarf wrapped around the neck
            const int n = 18;
            var centers = new Vector3[n + 1];
            var radii = new Vector2[n + 1];
            var ba = new int[n + 1];
            var bb = new int[n + 1];
            var bw = new float[n + 1];
            for (int i = 0; i <= n; i++)
            {
                float a = (float)i / n * Mathf.PI * 2f;
                centers[i] = new Vector3(Mathf.Cos(a) * 0.088f, 1.475f + Mathf.Sin(a) * 0.006f, Mathf.Sin(a) * 0.082f);
                radii[i] = new Vector2(0.032f, 0.026f);
                ba[i] = UC;
                bb[i] = NK;
                bw[i] = 0.3f;
            }
            sk.Loft(centers, radii, ba, bb, bw, 10, (int)M.Scarf, false, false, 4f);
        }

        static void Band(Skin sk, float y0, float y1, float rx, float rz, int bone, int sub)
        {
            sk.Loft(new[] { new Vector3(0f, y0, 0f), new Vector3(0f, y1, 0f) },
                new[] { new Vector2(rx, rz), new Vector2(rx * 0.98f, rz * 0.98f) },
                new[] { bone, bone }, new[] { bone, bone }, new[] { 0f, 0f }, 24, sub, false, false, 3f);
        }

        static void BuildHead(Skin sk)
        {
            var c = new Vector3(0f, 1.675f, 0.008f);
            var r = new Vector3(0.092f, 0.112f, 0.1f);
            sk.Ellipsoid(c, r, 28, 20, (int)M.Skin, HD, deform: (dir, p) =>
            {
                if (dir.y < 0f)
                {
                    // Narrow jaw and chin
                    float k = -dir.y;
                    p.x = c.x + (p.x - c.x) * Mathf.Lerp(1f, 0.7f, k);
                    if (dir.z > 0f) p.z += 0.01f * k * dir.z;
                }
                if (dir.z < -0.2f) p.z -= 0.008f * (-dir.z); // fuller back of the skull
                return p;
            });

            foreach (int s in new[] { 1, -1 })
            {
                sk.Ellipsoid(new Vector3(0.091f * s, 1.672f, -0.002f), new Vector3(0.012f, 0.026f, 0.016f), 10, 8, (int)M.Skin, HD);
                // Eyes: sclera, iris, pupil
                sk.Ellipsoid(new Vector3(0.036f * s, 1.682f, 0.096f), new Vector3(0.021f, 0.017f, 0.008f), 14, 10, (int)M.EyeWhite, HD);
                sk.Ellipsoid(new Vector3(0.036f * s, 1.681f, 0.1f), new Vector3(0.0118f, 0.0148f, 0.006f), 14, 10, (int)M.Iris, HD);
                sk.Ellipsoid(new Vector3(0.036f * s, 1.681f, 0.1045f), new Vector3(0.005f, 0.007f, 0.003f), 8, 6, (int)M.Dark, HD);
                // Eyebrows
                sk.Ellipsoid(new Vector3(0.038f * s, 1.708f, 0.095f), new Vector3(0.021f, 0.0035f, 0.005f), 10, 6, (int)M.Hair, HD);
            }
            sk.Ellipsoid(new Vector3(0f, 1.655f, 0.102f), new Vector3(0.0105f, 0.02f, 0.013f), 10, 8, (int)M.Skin, HD);
            sk.Ellipsoid(new Vector3(0f, 1.622f, 0.096f), new Vector3(0.014f, 0.0028f, 0.004f), 10, 6, (int)M.Dark, HD);
        }

        static void BuildHair(Skin sk)
        {
            var c = new Vector3(0f, 1.682f, 0.004f);
            var r = new Vector3(0.101f, 0.123f, 0.11f);
            sk.Ellipsoid(c, r, 28, 20, (int)M.Hair, HD, keep: d =>
                d.z > 0.5f ? d.y > 0.42f : d.y > 0f || (d.z < -0.1f && d.y > -0.65f) || (Mathf.Abs(d.x) > 0.72f && d.y > -0.4f && d.z < 0.3f));

            Vector3 OnScalp(Vector3 dir) => c + Vector3.Scale(dir.normalized, r) * 0.97f;

            // Bangs over the forehead
            for (int i = 0; i < 7; i++)
            {
                float x = Mathf.Lerp(-0.55f, 0.55f, i / 6f);
                var root = OnScalp(new Vector3(x, 0.72f, 0.55f));
                var dir = new Vector3(x * 0.5f, -0.55f, 0.75f).normalized;
                float len = 0.055f + Mathf.Abs(x) * 0.08f + (i % 2) * 0.012f; // short in the middle so the eyes stay visible
                sk.Strand(root, dir, len, 0.03f, 0.9f, (int)M.Hair, HD);
            }
            // Side locks framing the face
            foreach (int s in new[] { 1, -1 })
            {
                sk.Strand(OnScalp(new Vector3(0.85f * s, 0.25f, 0.3f)), new Vector3(0.12f * s, -1f, 0.2f).normalized, 0.21f, 0.03f, 0.15f, (int)M.Hair, HD);
                sk.Strand(OnScalp(new Vector3(0.9f * s, 0.1f, -0.1f)), new Vector3(0.25f * s, -1f, -0.1f).normalized, 0.16f, 0.032f, 0.2f, (int)M.Hair, HD);
            }
            // Back layers
            for (int i = 0; i < 9; i++)
            {
                float x = Mathf.Lerp(-0.75f, 0.75f, i / 8f);
                float y = i % 2 == 0 ? 0.05f : -0.2f;
                var root = OnScalp(new Vector3(x, y, -0.75f));
                var dir = new Vector3(x * 0.4f, -0.65f, -0.5f).normalized;
                sk.Strand(root, dir, 0.18f + (i % 3) * 0.04f, 0.036f, 0.5f, (int)M.Hair, HD);
            }
            // Crown clumps swept back
            for (int i = 0; i < 7; i++)
            {
                float a = Mathf.Lerp(-0.6f, 0.6f, i / 6f);
                var root = OnScalp(new Vector3(a, 0.95f, 0.15f - Mathf.Abs(a) * 0.3f));
                var dir = new Vector3(a * 0.6f, 0.35f, -0.85f).normalized;
                sk.Strand(root, dir, 0.15f, 0.038f, 0.65f, (int)M.Hair, HD);
            }
            // Long braid down the back
            var braidRoot = new Vector3(0f, 1.63f, -0.1f);
            var pts = new Vector3[9];
            var rad = new Vector2[9];
            var ba = new int[9];
            var bb = new int[9];
            var bw = new float[9];
            for (int i = 0; i < pts.Length; i++)
            {
                float t = i / 8f;
                pts[i] = braidRoot + new Vector3(Mathf.Sin(t * 9f) * 0.008f, -0.42f * t, -0.04f * t - 0.03f * t * t);
                float bulge = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(t * 14f));
                rad[i] = Vector2.one * Mathf.Lerp(0.03f, 0.014f, t) * bulge;
                ba[i] = HD;
                bb[i] = UC;
                bw[i] = Mathf.Clamp01(t * 1.2f);
            }
            sk.Loft(pts, rad, ba, bb, bw, 8, (int)M.Hair, true, true, 3f);
            sk.Ellipsoid(pts[pts.Length - 1] + Vector3.down * 0.01f, new Vector3(0.018f, 0.012f, 0.018f), 8, 6, (int)M.Gold, UC);
        }

        static void BuildArms(Skin sk)
        {
            foreach (int s in new[] { 1, -1 })
            {
                int ua = UA(s), la = LA(s), ha = HA(s);
                // Shoulder cap (tunic)
                sk.Ellipsoid(new Vector3(0.165f * s, 1.425f, 0f), new Vector3(0.072f, 0.066f, 0.07f), 14, 10, (int)M.Tunic, UC, ua, 0.7f);

                // Sleeve
                var sleeve = new (float x, float rx, float rz, int a, int b, float w)[]
                {
                    (0.15f, 0.058f, 0.056f, UC, ua, 0.6f),
                    (0.26f, 0.053f, 0.051f, ua, ua, 0f),
                    (0.36f, 0.048f, 0.047f, ua, ua, 0f),
                    (0.41f, 0.052f, 0.051f, ua, la, 0.3f),
                    (0.445f, 0.05f, 0.049f, ua, la, 0.6f),
                };
                sk.Loft(sleeve.Select(p => new Vector3(p.x * s, 1.43f, 0f)).ToArray(), sleeve.Select(p => new Vector2(p.rx, p.rz)).ToArray(),
                    sleeve.Select(p => p.a).ToArray(), sleeve.Select(p => p.b).ToArray(), sleeve.Select(p => p.w).ToArray(),
                    14, (int)M.Tunic, false, false, 3f);

                // Leather glove / bracer
                var fore = new (float x, float rx, float rz, int a, int b, float w)[]
                {
                    (0.42f, 0.042f, 0.041f, ua, la, 0.6f),
                    (0.5f, 0.045f, 0.04f, la, la, 0f),
                    (0.6f, 0.037f, 0.032f, la, la, 0f),
                    (0.665f, 0.031f, 0.027f, la, ha, 0.5f),
                };
                sk.Loft(fore.Select(p => new Vector3(p.x * s, 1.43f, 0f)).ToArray(), fore.Select(p => new Vector2(p.rx, p.rz)).ToArray(),
                    fore.Select(p => p.a).ToArray(), fore.Select(p => p.b).ToArray(), fore.Select(p => p.w).ToArray(),
                    14, (int)M.Leather, false, false, 3f);

                // Hand and thumb
                sk.Ellipsoid(new Vector3(0.722f * s, 1.428f, 0.004f), new Vector3(0.056f, 0.022f, 0.042f), 14, 10, (int)M.Skin, ha);
                sk.Ellipsoid(new Vector3(0.69f * s, 1.424f, 0.04f), new Vector3(0.026f, 0.012f, 0.013f), 10, 8, (int)M.Skin, ha);
            }
        }

        static void BuildLegs(Skin sk)
        {
            foreach (int s in new[] { 1, -1 })
            {
                int ul = UL(s), ll = LL(s), ft = FT(s), to = TO(s);
                float x = 0.088f * s;
                var thigh = new (float y, float rx, float rz, int a, int b, float w)[]
                {
                    (0.97f, 0.066f, 0.085f, H, ul, 0.6f),
                    (0.86f, 0.078f, 0.086f, ul, ul, 0f),
                    (0.71f, 0.073f, 0.077f, ul, ul, 0f),
                    (0.57f, 0.059f, 0.062f, ul, ul, 0f),
                    (0.5f, 0.056f, 0.058f, ul, ll, 0.5f),
                    (0.45f, 0.055f, 0.057f, ll, ll, 0f),
                };
                sk.Loft(thigh.Select(p => new Vector3(x, p.y, 0f)).ToArray(), thigh.Select(p => new Vector2(p.rx, p.rz)).ToArray(),
                    thigh.Select(p => p.a).ToArray(), thigh.Select(p => p.b).ToArray(), thigh.Select(p => p.w).ToArray(),
                    16, (int)M.Pants, false, false, 3f);

                var boot = new (float y, float rx, float rz, float z, int a, int b, float w)[]
                {
                    (0.53f, 0.07f, 0.072f, 0.005f, ul, ll, 0.5f),
                    (0.49f, 0.066f, 0.069f, 0.005f, ul, ll, 0.6f),
                    (0.42f, 0.06f, 0.064f, 0f, ll, ll, 0f),
                    (0.32f, 0.058f, 0.063f, -0.006f, ll, ll, 0f),
                    (0.2f, 0.048f, 0.05f, 0f, ll, ll, 0f),
                    (0.12f, 0.046f, 0.048f, 0f, ll, ft, 0.5f),
                    (0.075f, 0.049f, 0.052f, 0f, ft, ft, 0f),
                };
                sk.Loft(boot.Select(p => new Vector3(x, p.y, p.z)).ToArray(), boot.Select(p => new Vector2(p.rx, p.rz)).ToArray(),
                    boot.Select(p => p.a).ToArray(), boot.Select(p => p.b).ToArray(), boot.Select(p => p.w).ToArray(),
                    16, (int)M.Leather, true, false, 3f);

                // Shoe running heel to toe
                var shoe = new (Vector3 c, Vector2 r, int a, int b, float w)[]
                {
                    (new Vector3(x, 0.055f, -0.065f), new Vector2(0.043f, 0.045f), ft, ft, 0f),
                    (new Vector3(x, 0.05f, 0.0f), new Vector2(0.05f, 0.05f), ft, ft, 0f),
                    (new Vector3(x, 0.042f, 0.08f), new Vector2(0.049f, 0.04f), ft, to, 0.4f),
                    (new Vector3(x, 0.036f, 0.145f), new Vector2(0.043f, 0.032f), to, to, 0f),
                    (new Vector3(x, 0.034f, 0.18f), new Vector2(0.03f, 0.024f), to, to, 0f),
                };
                sk.Loft(shoe.Select(p => p.c).ToArray(), shoe.Select(p => p.r).ToArray(),
                    shoe.Select(p => p.a).ToArray(), shoe.Select(p => p.b).ToArray(), shoe.Select(p => p.w).ToArray(),
                    14, (int)M.Leather, true, true, 3f);
                // Gold knee guard
                sk.Ellipsoid(new Vector3(x, 0.515f, 0.062f), new Vector3(0.036f, 0.03f, 0.01f), 12, 8, (int)M.Gold, ll, ul, 0.4f);
            }
        }

        static void BuildCape(GameObject root, Transform[] bones, Matrix4x4[] bindposes, Material mat)
        {
            const int cols = 10, rows = 16;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var w = new List<BoneWeight>();
            var t = new List<int>();
            for (int r = 0; r <= rows; r++)
            {
                float fy = (float)r / rows;
                float y = Mathf.Lerp(1.44f, 0.6f, fy);
                float half = Mathf.Lerp(0.16f, 0.3f, fy);
                for (int c = 0; c <= cols; c++)
                {
                    float fx = (float)c / cols * 2f - 1f;
                    float z = Mathf.Lerp(-0.112f, -0.2f, fy) - 0.035f * (1f - fx * fx) * (0.4f + fy);
                    v.Add(new Vector3(fx * half, y, z));
                    uv.Add(new Vector2((float)c / cols, 1f - fy));
                    w.Add(new BoneWeight { boneIndex0 = UC, weight0 = 1f });
                }
            }
            for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                int i = r * (cols + 1) + c;
                t.AddRange(new[] { i, i + 1, i + cols + 2, i, i + cols + 2, i + cols + 1 });
            }
            var mesh = new Mesh { name = "HeroCape" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.boneWeights = w.ToArray();
            mesh.bindposes = bindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            mesh = Save(mesh, $"{Dir}/HeroCape.asset");

            var cape = new GameObject("Cape");
            cape.transform.SetParent(root.transform, false);
            var smr = cape.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones;
            smr.rootBone = bones[UC];
            smr.sharedMaterial = mat;
            smr.updateWhenOffscreen = true;

            var body = Collider(bones[CH], new Vector3(0f, -0.08f, 0f), 0.17f, 0.62f);
            var thighL = Collider(bones[UL(1)], new Vector3(0f, -0.2f, 0f), 0.095f, 0.5f);
            var thighR = Collider(bones[UL(-1)], new Vector3(0f, -0.2f, 0f), 0.095f, 0.5f);

            var cloth = cape.AddComponent<Cloth>();
            cloth.stretchingStiffness = 0.9f;
            cloth.bendingStiffness = 0.25f;
            cloth.damping = 0.15f;
            cloth.useGravity = true;
            cloth.worldVelocityScale = 0.6f;
            cloth.worldAccelerationScale = 0.8f;
            cloth.friction = 0.3f;
            cloth.capsuleColliders = new[] { body, thighL, thighR };
            cape.AddComponent<CapeCloth>();
        }

        static CapsuleCollider Collider(Transform bone, Vector3 center, float radius, float height)
        {
            var go = new GameObject("ClothCollider");
            go.transform.SetParent(bone, false);
            var c = go.AddComponent<CapsuleCollider>();
            c.center = center;
            c.radius = radius;
            c.height = height;
            c.direction = 1;
            c.isTrigger = true;
            return c;
        }

        // ================================================================ Avatar & materials

        static Avatar BuildAvatar(GameObject root)
        {
            var transforms = root.GetComponentsInChildren<Transform>(true).Where(t => t == root.transform || BoneNames.Contains(t.name)).ToArray();
            var desc = new HumanDescription
            {
                human = BoneNames.Select(n => new HumanBone { boneName = n, humanName = n, limit = new HumanLimit { useDefaultValues = true } }).ToArray(),
                skeleton = transforms.Select(t => new SkeletonBone { name = t.name, position = t.localPosition, rotation = t.localRotation, scale = t.localScale }).ToArray(),
                upperArmTwist = 0.5f,
                lowerArmTwist = 0.5f,
                upperLegTwist = 0.5f,
                lowerLegTwist = 0.5f,
                armStretch = 0.05f,
                legStretch = 0.05f,
                feetSpacing = 0f,
                hasTranslationDoF = false,
            };
            var avatar = AvatarBuilder.BuildHumanAvatar(root, desc);
            avatar.name = "HeroAvatar";
            if (!avatar.isValid) Debug.LogWarning("[CharacterFactory] Avatar is not a valid humanoid.");
            return Save(avatar, $"{Dir}/HeroAvatar.asset");
        }

        static Material[] Materials()
        {
            var fabricNormal = ArtLibrary.Find($"{ArtLibrary.PolyHaven}/Textures/fabric_pattern_05", "_nor_gl");
            Material Make(string name, Color color, float smooth, float metal = 0f, Texture2D normal = null, float tiling = 1f, Color? emission = null, bool twoSided = false)
            {
                var m = ArtLibrary.LoadOrCreateMaterial($"{MatDir}/{name}.mat", "Universal Render Pipeline/Lit");
                m.SetColor("_BaseColor", color);
                m.SetFloat("_Smoothness", smooth);
                m.SetFloat("_Metallic", metal);
                if (normal)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.SetFloat("_BumpScale", 0.6f);
                    m.SetTextureScale("_BaseMap", Vector2.one * tiling);
                    m.EnableKeyword("_NORMALMAP");
                }
                if (emission.HasValue)
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", emission.Value);
                }
                if (twoSided)
                {
                    m.SetFloat("_Cull", 0f);
                    m.doubleSidedGI = true;
                }
                EditorUtility.SetDirty(m);
                return m;
            }

            var mats = new Material[(int)M.Count];
            mats[(int)M.Skin] = Make("Hero_Skin", new Color(1f, 0.83f, 0.72f), 0.38f);
            mats[(int)M.Tunic] = Make("Hero_Tunic", new Color(0.93f, 0.91f, 0.86f), 0.2f, 0f, fabricNormal, 3f);
            mats[(int)M.Pants] = Make("Hero_Pants", new Color(0.13f, 0.15f, 0.2f), 0.2f, 0f, fabricNormal, 4f);
            mats[(int)M.Leather] = Make("Hero_Leather", new Color(0.28f, 0.17f, 0.1f), 0.45f);
            mats[(int)M.Hair] = Make("Hero_Hair", new Color(0.96f, 0.8f, 0.46f), 0.55f);
            mats[(int)M.EyeWhite] = Make("Hero_EyeWhite", new Color(0.97f, 0.96f, 0.95f), 0.85f);
            mats[(int)M.Iris] = Make("Hero_Iris", new Color(0.1f, 0.62f, 0.6f), 0.9f, 0f, null, 1f, new Color(0.05f, 0.35f, 0.33f));
            mats[(int)M.Dark] = Make("Hero_Dark", new Color(0.12f, 0.06f, 0.06f), 0.4f);
            mats[(int)M.Gold] = Make("Hero_Gold", new Color(0.95f, 0.75f, 0.35f), 0.75f, 0.9f);
            mats[(int)M.Scarf] = Make("Hero_Scarf", new Color(0.12f, 0.55f, 0.55f), 0.25f, 0f, fabricNormal, 3f, null, true);
            return mats;
        }

        static T Save<T>(T obj, string path) where T : Object
        {
            if (AssetDatabase.LoadMainAssetAtPath(path)) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(obj, path);
            return obj;
        }

        // ================================================================ Skinned mesh builder

        class Skin
        {
            readonly List<Vector3> v = new();
            readonly List<Vector2> uv = new();
            readonly List<BoneWeight> w = new();
            readonly List<int>[] sub;

            public Skin(int subs)
            {
                sub = new List<int>[subs];
                for (int i = 0; i < subs; i++) sub[i] = new List<int>();
            }

            int Vert(Vector3 p, Vector2 t, int b0, int b1, float w1)
            {
                v.Add(p);
                uv.Add(t);
                var bw = new BoneWeight { boneIndex0 = b0, weight0 = 1f };
                if (b1 >= 0 && b1 != b0 && w1 > 0f)
                {
                    float w0 = 1f - w1;
                    if (w1 > w0) bw = new BoneWeight { boneIndex0 = b1, weight0 = w1, boneIndex1 = b0, weight1 = w0 };
                    else bw = new BoneWeight { boneIndex0 = b0, weight0 = w0, boneIndex1 = b1, weight1 = w1 };
                }
                w.Add(bw);
                return v.Count - 1;
            }

            void Tri(int a, int b, int c, int s, Vector3 outward)
            {
                var n = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
                if (Vector3.Dot(n, outward) < 0f) (b, c) = (c, b);
                sub[s].Add(a);
                sub[s].Add(b);
                sub[s].Add(c);
            }

            public void Loft(Vector3[] c, Vector2[] r, int[] boneA, int[] boneB, float[] wB, int sides, int s,
                bool capStart, bool capEnd, float uvScale)
            {
                int n = c.Length;
                int start = v.Count;
                float len = 0f;
                var tangents = new Vector3[n];
                for (int i = 0; i < n; i++)
                {
                    if (i > 0) len += Vector3.Distance(c[i], c[i - 1]);
                    Vector3 tangent = (c[Mathf.Min(i + 1, n - 1)] - c[Mathf.Max(i - 1, 0)]).normalized;
                    tangents[i] = tangent;
                    Vector3 refUp = Mathf.Abs(Vector3.Dot(tangent, Vector3.up)) > 0.95f ? Vector3.forward : Vector3.up;
                    Vector3 side = Vector3.Cross(tangent, refUp).normalized;
                    Vector3 bin = Vector3.Cross(side, tangent).normalized;
                    for (int k = 0; k <= sides; k++)
                    {
                        float a = (float)k / sides * Mathf.PI * 2f;
                        Vector3 p = c[i] + side * (Mathf.Cos(a) * r[i].x) + bin * (Mathf.Sin(a) * r[i].y);
                        Vert(p, new Vector2((float)k / sides, len * uvScale), boneA[i], boneB[i], wB[i]);
                    }
                }
                for (int i = 0; i < n - 1; i++)
                for (int k = 0; k < sides; k++)
                {
                    int a = start + i * (sides + 1) + k, b = a + 1, d = a + sides + 1, e = d + 1;
                    Vector3 outward = (v[a] + v[e]) * 0.5f - (c[i] + c[i + 1]) * 0.5f;
                    Tri(a, b, e, s, outward);
                    Tri(a, e, d, s, outward);
                }
                if (capStart) Cap(start, sides, c[0] - tangents[0] * Mathf.Min(r[0].x, r[0].y) * 0.4f, -tangents[0], boneA[0], boneB[0], wB[0], s);
                if (capEnd) Cap(start + (n - 1) * (sides + 1), sides, c[n - 1] + tangents[n - 1] * Mathf.Min(r[n - 1].x, r[n - 1].y) * 0.4f,
                    tangents[n - 1], boneA[n - 1], boneB[n - 1], wB[n - 1], s);
            }

            void Cap(int ring, int sides, Vector3 center, Vector3 dir, int b0, int b1, float w1, int s)
            {
                int cv = Vert(center, new Vector2(0.5f, 0.5f), b0, b1, w1);
                for (int k = 0; k < sides; k++) Tri(cv, ring + k, ring + k + 1, s, dir);
            }

            public void Ellipsoid(Vector3 center, Vector3 radii, int lon, int lat, int s, int b0, int b1 = -1, float w1 = 0f,
                System.Func<Vector3, bool> keep = null, System.Func<Vector3, Vector3, Vector3> deform = null)
            {
                int start = v.Count;
                var dirs = new List<Vector3>();
                for (int y = 0; y <= lat; y++)
                {
                    float theta = (float)y / lat * Mathf.PI;
                    for (int x = 0; x <= lon; x++)
                    {
                        float phi = (float)x / lon * Mathf.PI * 2f;
                        var dir = new Vector3(Mathf.Sin(theta) * Mathf.Cos(phi), Mathf.Cos(theta), Mathf.Sin(theta) * Mathf.Sin(phi));
                        var p = center + Vector3.Scale(dir, radii);
                        if (deform != null) p = deform(dir, p);
                        Vert(p, new Vector2((float)x / lon, (float)y / lat), b0, b1, w1);
                        dirs.Add(dir);
                    }
                }
                for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int i = y * (lon + 1) + x;
                    int a = start + i, b = a + 1, d = a + lon + 1, e = d + 1;
                    if (keep != null && !(keep(dirs[i]) && keep(dirs[i + 1]) && keep(dirs[i + lon + 1]) && keep(dirs[i + lon + 2]))) continue;
                    Vector3 outward = (v[a] + v[e]) * 0.5f - center;
                    Tri(a, b, e, s, outward);
                    Tri(a, e, d, s, outward);
                }
            }

            public void Strand(Vector3 root, Vector3 dir, float length, float width, float gravity, int s, int bone)
            {
                const int segs = 5;
                var c = new Vector3[segs + 1];
                var r = new Vector2[segs + 1];
                var ba = new int[segs + 1];
                var bb = new int[segs + 1];
                var bw = new float[segs + 1];
                for (int i = 0; i <= segs; i++)
                {
                    float t = (float)i / segs;
                    c[i] = root + dir * (length * t) + Vector3.down * (gravity * length * t * t);
                    float taper = 1f - t * 0.93f;
                    r[i] = new Vector2(width * taper, width * 0.5f * taper);
                    ba[i] = bone;
                    bb[i] = bone;
                }
                Loft(c, r, ba, bb, bw, 6, s, true, true, 3f);
            }

            public Mesh ToMesh(string name, Matrix4x4[] bindposes)
            {
                var m = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.subMeshCount = sub.Length;
                for (int i = 0; i < sub.Length; i++) m.SetTriangles(sub[i], i);
                m.boneWeights = w.ToArray();
                m.bindposes = bindposes;
                m.RecalculateNormals();
                m.RecalculateTangents();
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
