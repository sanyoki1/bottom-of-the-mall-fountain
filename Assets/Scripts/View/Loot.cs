// Small loot meshes (coins, gum wads, straws, paper, gems, chips, cassettes) shared by
// crust props, flying loot and particle showers. White-ish base colours so tints work.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public static class Loot
    {
        static readonly Dictionary<ItemShape, Mesh> baseMeshes = new Dictionary<ItemShape, Mesh>();
        static readonly Dictionary<long, Mesh> tinted = new Dictionary<long, Mesh>();
        static Material litNoEmit, glossNoEmit;

        public static Material LitNoEmit
        {
            get
            {
                if (litNoEmit == null)
                {
                    litNoEmit = new Material(Mats.Lit) { name = "WE Lit (no emit)" };
                    litNoEmit.SetFloat("_EmissionBoost", 0);
                    litNoEmit.SetFloat("_Glossiness", 0.45f);
                    litNoEmit.SetFloat("_Metallic", 0.4f);
                }
                return litNoEmit;
            }
        }

        public static Material GlossNoEmit
        {
            get
            {
                if (glossNoEmit == null)
                {
                    glossNoEmit = new Material(Mats.Gloss) { name = "WE Gloss (no emit)" };
                    glossNoEmit.SetFloat("_EmissionBoost", 0);
                }
                return glossNoEmit;
            }
        }

        /// <summary>Untinted mesh for a shape (white), for particle systems that tint by vertex colour.</summary>
        public static Mesh Base(ItemShape shape)
        {
            if (baseMeshes.TryGetValue(shape, out var m)) return m;
            m = BuildShape(shape, Color.white).ToMesh("loot " + shape);
            baseMeshes[shape] = m;
            return m;
        }

        public static Mesh Tinted(ItemShape shape, Color c)
        {
            long key = ((long)shape << 32) | (long)(uint)((int)(c.r * 255) << 16 | (int)(c.g * 255) << 8 | (int)(c.b * 255));
            if (tinted.TryGetValue(key, out var m)) return m;
            m = BuildShape(shape, c).ToMesh("loot " + shape);
            tinted[key] = m;
            return m;
        }

        static MeshKit BuildShape(ItemShape shape, Color c)
        {
            var k = new MeshKit();
            Color dark = c * 0.78f;
            dark.a = 0;
            c.a = 0;
            switch (shape)
            {
                case ItemShape.Coin:
                    k.Glossy(true);
                    k.Cylinder(Vector3.zero, 0.12f, 0.022f, 14, c);
                    k.Torus(new Vector3(0, 0.011f, 0), 0.105f, 0.008f, 14, 3, dark);
                    break;
                case ItemShape.Chip:
                    k.Cylinder(Vector3.zero, 0.13f, 0.03f, 16, c);
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60 * Mathf.Deg2Rad;
                        k.Box(new Vector3(Mathf.Cos(a) * 0.115f, 0, Mathf.Sin(a) * 0.115f), new Vector3(0.035f, 0.034f, 0.035f), new Color(0.95f, 0.95f, 0.95f, 0));
                    }
                    break;
                case ItemShape.Wad:
                    k.Ellipsoid(Vector3.zero, new Vector3(0.1f, 0.075f, 0.09f), 5, 8, c);
                    break;
                case ItemShape.Stick:
                    k.Push(Vector3.zero, Quaternion.Euler(0, 0, 90));
                    k.Cylinder(Vector3.zero, 0.022f, 0.34f, 6, c);
                    k.Pop();
                    break;
                case ItemShape.Paper:
                    k.Box(Vector3.zero, new Vector3(0.28f, 0.012f, 0.2f), c);
                    k.Box(new Vector3(0.03f, 0.008f, 0), new Vector3(0.12f, 0.004f, 0.02f), dark);
                    break;
                case ItemShape.Gem:
                    k.Glossy(true);
                    k.Torus(Vector3.zero, 0.075f, 0.02f, 12, 4, new Color(0.92f, 0.78f, 0.35f, 0));
                    k.Push(new Vector3(0, 0.075f, 0), Quaternion.identity);
                    k.Cone(Vector3.zero, 0.05f, 0.06f, 6, new Color(c.r, c.g, c.b, 0.35f));
                    k.Push(Vector3.zero, Quaternion.Euler(180, 0, 0));
                    k.Cone(Vector3.zero, 0.05f, 0.04f, 6, new Color(c.r, c.g, c.b, 0.35f));
                    k.Pop();
                    k.Pop();
                    break;
                case ItemShape.Cube:
                    k.Box(Vector3.zero, new Vector3(0.2f, 0.06f, 0.13f), c);
                    k.Box(new Vector3(0, 0.031f, 0), new Vector3(0.12f, 0.004f, 0.06f), dark);
                    break;
                case ItemShape.Chunk:
                    k.Ellipsoid(new Vector3(0, 0.02f, 0), new Vector3(0.16f, 0.09f, 0.13f), 4, 6, c);
                    k.Ellipsoid(new Vector3(0.07f, 0.05f, 0.03f), new Vector3(0.08f, 0.06f, 0.07f), 3, 5, dark);
                    k.Box(new Vector3(-0.05f, 0.08f, -0.02f), new Vector3(0.04f, 0.01f, 0.04f), new Color(0.85f, 0.55f, 0.3f, 0));
                    break;
                case ItemShape.Roll:
                    k.Push(Vector3.zero, Quaternion.Euler(0, 0, 90));
                    k.Cylinder(Vector3.zero, 0.07f, 0.3f, 12, c);
                    k.Cylinder(Vector3.zero, 0.072f, 0.16f, 12, new Color(0.95f, 0.9f, 0.8f, 0), false);
                    k.Pop();
                    break;
                case ItemShape.Bag:
                    k.Ellipsoid(new Vector3(0, 0.1f, 0), new Vector3(0.17f, 0.15f, 0.15f), 6, 10, c);
                    k.Cylinder(new Vector3(0, 0.26f, 0), 0.05f, 0.08f, 8, dark);
                    k.Box(new Vector3(0, 0.12f, 0.148f), new Vector3(0.06f, 0.1f, 0.01f), new Color(0.15f, 0.45f, 0.2f, 0));
                    break;
                case ItemShape.Bar:
                    k.Glossy(true);
                    k.Frustum(Vector3.zero, 0.1f, 0.08f, 0.06f, 4, c, true, false);
                    break;
                case ItemShape.Brick:
                    k.Box(Vector3.zero, new Vector3(0.2f, 0.08f, 0.1f), new Color(c.r, c.g, c.b, 0.35f));
                    k.Box(new Vector3(0, 0.041f, 0), new Vector3(0.14f, 0.004f, 0.05f), new Color(1f, 0.9f, 1f, 0.8f));
                    break;
                case ItemShape.Keys:
                    k.Glossy(true);
                    k.Torus(Vector3.zero, 0.04f, 0.006f, 10, 3, c);
                    k.Box(new Vector3(0.07f, 0, 0.02f), new Vector3(0.09f, 0.006f, 0.022f), c);
                    k.Box(new Vector3(0.05f, 0, -0.04f), new Vector3(0.08f, 0.006f, 0.02f), c);
                    k.Box(new Vector3(-0.07f, 0, 0), new Vector3(0.06f, 0.02f, 0.035f), new Color(0.15f, 0.15f, 0.17f, 0));
                    break;
                case ItemShape.Teeth:
                    k.Torus(Vector3.zero, 0.055f, 0.02f, 12, 4, new Color(0.95f, 0.55f, 0.6f, 0));
                    for (int i = 0; i < 8; i++)
                    {
                        float a = (i * 22.5f + 10) * Mathf.Deg2Rad;
                        k.Box(new Vector3(Mathf.Cos(a) * 0.058f, 0.02f, Mathf.Sin(a) * 0.058f), new Vector3(0.018f, 0.022f, 0.018f), c);
                    }
                    break;
                case ItemShape.Phone:
                    k.Box(Vector3.zero, new Vector3(0.08f, 0.012f, 0.16f), c);
                    k.Box(new Vector3(0, 0.007f, 0), new Vector3(0.07f, 0.003f, 0.145f), new Color(0.35f, 0.75f, 1f, 0.7f));
                    break;
                case ItemShape.Ball:
                    k.Glossy(true);
                    k.Sphere(new Vector3(0, 0.11f, 0), 0.11f, 8, 12, c);
                    k.Sphere(new Vector3(0.03f, 0.2f, 0.05f), 0.012f, 3, 5, new Color(0.05f, 0.05f, 0.05f, 0));
                    k.Sphere(new Vector3(-0.02f, 0.21f, 0.05f), 0.012f, 3, 5, new Color(0.05f, 0.05f, 0.05f, 0));
                    break;
                case ItemShape.Toaster:
                    k.Glossy(true);
                    k.Box(new Vector3(0, 0.07f, 0), new Vector3(0.22f, 0.14f, 0.12f), c);
                    k.Box(new Vector3(0, 0.141f, 0.025f), new Vector3(0.16f, 0.004f, 0.02f), new Color(0.1f, 0.1f, 0.1f, 0));
                    k.Box(new Vector3(0, 0.141f, -0.025f), new Vector3(0.16f, 0.004f, 0.02f), new Color(0.1f, 0.1f, 0.1f, 0));
                    k.Box(new Vector3(0.12f, 0.09f, 0), new Vector3(0.02f, 0.02f, 0.03f), new Color(0.1f, 0.1f, 0.1f, 0));
                    break;
                case ItemShape.Fish:
                    k.Ellipsoid(new Vector3(0, 0.03f, 0), new Vector3(0.05f, 0.035f, 0.1f), 5, 8, new Color(c.r, c.g, c.b, 0.15f));
                    k.Push(new Vector3(0, 0.03f, -0.11f), Quaternion.Euler(-90, 0, 0));
                    k.Cone(Vector3.zero, 0.04f, 0.05f, 4, new Color(c.r, c.g, c.b, 0.15f));
                    k.Pop();
                    break;
                case ItemShape.Trophy:
                    k.Glossy(true);
                    k.Box(new Vector3(0, 0.02f, 0), new Vector3(0.1f, 0.04f, 0.1f), new Color(0.2f, 0.15f, 0.12f, 0));
                    k.Cylinder(new Vector3(0, 0.08f, 0), 0.012f, 0.08f, 6, c);
                    k.Frustum(new Vector3(0, 0.15f, 0), 0.02f, 0.06f, 0.07f, 10, c);
                    k.Sphere(new Vector3(0, 0.22f, 0), 0.03f, 4, 6, c);
                    break;
                case ItemShape.Shoe:
                    k.Box(new Vector3(0, 0.02f, 0), new Vector3(0.07f, 0.03f, 0.15f), new Color(0.95f, 0.95f, 0.95f, 0));
                    k.Ellipsoid(new Vector3(0, 0.05f, -0.01f), new Vector3(0.04f, 0.035f, 0.07f), 4, 8, c);
                    k.Box(new Vector3(0, 0.005f, 0.05f), new Vector3(0.06f, 0.01f, 0.03f), new Color(1f, 0.3f, 0.3f, 0.9f));
                    break;
                case ItemShape.Wallet:
                    k.Box(new Vector3(0, 0.012f, 0), new Vector3(0.11f, 0.024f, 0.09f), c);
                    k.Box(new Vector3(0.01f, 0.026f, 0.005f), new Vector3(0.08f, 0.004f, 0.06f), new Color(0.45f, 0.7f, 0.45f, 0));
                    break;
                case ItemShape.Duck:
                    k.Ellipsoid(new Vector3(0, 0.05f, 0), new Vector3(0.07f, 0.05f, 0.09f), 5, 8, c);
                    k.Sphere(new Vector3(0, 0.12f, 0.05f), 0.04f, 5, 8, c);
                    k.Push(new Vector3(0, 0.115f, 0.09f), Quaternion.Euler(90, 0, 0));
                    k.Cone(Vector3.zero, 0.018f, 0.03f, 5, new Color(1f, 0.5f, 0.1f, 0));
                    k.Pop();
                    break;
                case ItemShape.Vending:
                    k.Box(new Vector3(0, 0.9f, 0), new Vector3(0.9f, 1.8f, 0.75f), c);
                    k.Box(new Vector3(-0.1f, 1.0f, 0.38f), new Vector3(0.55f, 1.2f, 0.02f), new Color(0.6f, 0.85f, 1f, 0.45f));
                    k.Box(new Vector3(0.33f, 1.0f, 0.38f), new Vector3(0.14f, 0.5f, 0.02f), new Color(0.12f, 0.12f, 0.14f, 0));
                    k.Box(new Vector3(0, 0.2f, 0.38f), new Vector3(0.6f, 0.12f, 0.03f), new Color(0.1f, 0.1f, 0.1f, 0));
                    break;
                case ItemShape.Diamond:
                    k.Glossy(true);
                    k.Frustum(new Vector3(0, 0.035f, 0), 0.05f, 0.03f, 0.02f, 8, new Color(c.r, c.g, c.b, 0.5f), true, false);
                    k.Push(new Vector3(0, 0.025f, 0), Quaternion.Euler(180, 0, 0));
                    k.Cone(Vector3.zero, 0.05f, 0.06f, 8, new Color(c.r, c.g, c.b, 0.5f));
                    k.Pop();
                    break;
            }
            return k;
        }

        public static GameObject MakeItemMesh(ItemShape shape, Color c, float scale, Transform parent)
        {
            var go = new GameObject("loot");
            go.transform.SetParent(parent, false);
            go.transform.localScale = Vector3.one * scale;
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = Tinted(shape, c);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = mf.sharedMesh.subMeshCount > 1 ? new[] { Mats.Lit, Mats.Gloss } : new[] { Mats.Lit };
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
