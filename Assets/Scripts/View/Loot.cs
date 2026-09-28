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
