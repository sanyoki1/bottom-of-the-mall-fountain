// The fountain's beautification props beyond the first three (those live in FountainView):
// the MAKE A WISH neon, the cherub, koi, rock speakers, the lucky penny dispenser, the influencer
// photo spot, the golden statue, the light show, the certification plaque and the wormhole.
// Each appears (with a puff of confetti) when its tech is bought.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class FountainDecor
    {
        Transform root;
        ViewContext ctx;
        readonly Dictionary<string, Transform> built = new Dictionary<string, Transform>();
        readonly List<(Transform t, float phase, float radius, float speed)> koi = new List<(Transform, float, float, float)>();
        readonly List<Transform> beams = new List<Transform>();
        readonly List<Material> beamMats = new List<Material>();
        Transform portal, portalSwirl;
        float noteAcc;

        static readonly string[] Ids =
        {
            "fountain_neon", "fountain_cherub", "fountain_koi", "fountain_music", "fountain_dispenser",
            "fountain_photo", "fountain_golden", "fountain_show", "fountain_certified", "fountain_wormhole",
        };

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);
        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        public void Build(Transform parent, ViewContext c)
        {
            ctx = c;
            root = new GameObject("Fountain Decor").transform;
            root.SetParent(parent, false);
            built.Clear();
            koi.Clear();
            beams.Clear();
            beamMats.Clear();
            portal = portalSwirl = null;
        }

        public void Sync(Sim sim, bool fanfare)
        {
            foreach (var id in Ids)
            {
                bool own = sim.TechLevel(id) > 0;
                if (own && !built.ContainsKey(id))
                {
                    var t = Make(id);
                    built[id] = t;
                    if (fanfare && t != null) ctx.Fx.Confetti(t.position + Vector3.up * 1.5f, 60, 6);
                }
            }
        }

        Transform Make(string id)
        {
            var go = new GameObject(id);
            go.transform.SetParent(root, false);
            var t = go.transform;
            var th = ctx.Mall.Theme;
            var k = new MeshKit();
            switch (id)
            {
                case "fountain_neon":
                {
                    t.position = new Vector3(0, 0, 12.2f);
                    t.rotation = Quaternion.Euler(0, 180, 0);
                    k.Tube(new Vector3(-1.6f, 0, 0), new Vector3(-1.6f, 3.2f, 0), 0.05f, 8, C(0x3A3A3A));
                    k.Tube(new Vector3(1.6f, 0, 0), new Vector3(1.6f, 3.2f, 0), 0.05f, 8, C(0x3A3A3A));
                    k.Box(new Vector3(0, 3.0f, 0.02f), new Vector3(3.6f, 1.1f, 0.06f), C(0x1A1418));
                    k.Box(new Vector3(0, 2.46f, -0.02f), new Vector3(3.3f, 0.05f, 0.05f), C(0xFF4FA8, 1f));
                    k.Box(new Vector3(0, 3.54f, -0.02f), new Vector3(3.3f, 0.05f, 0.05f), C(0xFF4FA8, 1f));
                    k.Build("Frame", t);
                    WorldBuilder.MakeText(t, "MAKE A WISH", new Vector3(0, 3.02f, -0.05f), Quaternion.identity, 0.62f, C(0xFF7AC8), 3.2f, WorldBuilder.SignFontAlt);
                    WorldBuilder.MakeText(t, "MAKE A WISH", new Vector3(0, 3.02f, 0.09f), Quaternion.Euler(0, 180, 0), 0.62f, C(0xFF7AC8), 3.2f, WorldBuilder.SignFontAlt);
                    return t;
                }
                case "fountain_cherub":
                {
                    t.position = new Vector3(0, 3.28f, 0.1f);
                    Color stone = C(0xE8E0D0);
                    k.Ellipsoid(new Vector3(0, 0.22f, 0), new Vector3(0.16f, 0.2f, 0.14f), 6, 10, stone);
                    k.Sphere(new Vector3(0, 0.5f, 0), 0.13f, 6, 10, stone);
                    k.Ellipsoid(new Vector3(-0.17f, 0.36f, -0.08f), new Vector3(0.14f, 0.1f, 0.03f), 4, 8, stone);
                    k.Ellipsoid(new Vector3(0.17f, 0.36f, -0.08f), new Vector3(0.14f, 0.1f, 0.03f), 4, 8, stone);
                    k.Tube(new Vector3(0.12f, 0.3f, 0.05f), new Vector3(0.22f, 0.45f, 0.18f), 0.03f, 5, stone);
                    k.Torus(new Vector3(0.25f, 0.48f, 0.2f), 0.08f, 0.012f, 10, 3, C(0xC9A64A));
                    k.Torus(new Vector3(0, 0.66f, 0), 0.08f, 0.01f, 12, 3, C(0xFFE08A, 0.8f));
                    k.Build("Cherub", t);
                    return t;
                }
                case "fountain_koi":
                {
                    for (int i = 0; i < 12; i++)
                    {
                        var f = Loot.MakeItemMesh(ItemShape.Fish, i % 3 == 0 ? new Color(0.95f, 0.95f, 0.92f) : new Color(1f, 0.45f, 0.1f), 2.4f, t);
                        koi.Add((f.transform, i * 0.52f, 2.4f + (i % 4) * 1.2f, 0.25f + (i % 3) * 0.08f));
                    }
                    return t;
                }
                case "fountain_music":
                {
                    foreach (float a in new[] { 45f, 135f, 225f, 315f })
                    {
                        var p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)) * 9.9f;
                        k.Push(p, Quaternion.LookRotation(-p.normalized));
                        k.Ellipsoid(new Vector3(0, 0.3f, 0), new Vector3(0.45f, 0.32f, 0.35f), 5, 9, C(0x8A8478));
                        k.Cylinder(new Vector3(0, 0.34f, 0.3f), Quaternion.Euler(90, 0, 0), 0.14f, 0.04f, 12, C(0x2A2A2A));
                        k.Pop();
                    }
                    k.Build("Rock Speakers", t);
                    return t;
                }
                case "fountain_dispenser":
                {
                    t.position = new Vector3(-10.9f, 0, 4.5f);
                    t.rotation = Quaternion.LookRotation(-t.position.normalized);
                    var dc = t.gameObject.AddComponent<BoxCollider>();
                    dc.center = new Vector3(0, 0.75f, 0);
                    dc.size = new Vector3(0.6f, 1.5f, 0.6f);
                    k.Box(new Vector3(0, 0.5f, 0), new Vector3(0.5f, 1.0f, 0.5f), C(0xD8283A));
                    k.Glossy(true);
                    k.Sphere(new Vector3(0, 1.28f, 0), 0.3f, 10, 14, new Color(0.85f, 0.95f, 1f, 0.25f));
                    k.Glossy(false);
                    k.Box(new Vector3(0, 0.62f, 0.26f), new Vector3(0.14f, 0.14f, 0.04f), C(0xC9CDD2));
                    k.Box(new Vector3(0, 0.35f, 0.26f), new Vector3(0.18f, 0.1f, 0.06f), C(0x2A2A2A));
                    k.Build("Dispenser", t);
                    for (int i = 0; i < 14; i++)
                    {
                        var c = Loot.MakeItemMesh(ItemShape.Coin, new Color(0.78f, 0.48f, 0.26f), 0.9f, t);
                        c.transform.localPosition = new Vector3(Random.Range(-0.15f, 0.15f), 1.1f + Random.Range(0f, 0.2f), Random.Range(-0.15f, 0.15f));
                        c.transform.localRotation = Random.rotation;
                    }
                    WorldBuilder.MakeText(t, "LUCKY\nPENNIES", new Vector3(0, 0.85f, 0.26f), Quaternion.Euler(0, 180, 0), 0.07f, Color.white, 1.2f, WorldBuilder.SignFontAlt);
                    WorldBuilder.MakeText(t, "1¢ = 1¢", new Vector3(0, 0.18f, 0.26f), Quaternion.Euler(0, 180, 0), 0.06f, C(0xFFE08A), 1.2f);
                    return t;
                }
                case "fountain_photo":
                {
                    t.position = new Vector3(11.6f, 0, 5.2f);
                    t.rotation = Quaternion.LookRotation(-t.position.normalized);
                    var pc = t.gameObject.AddComponent<BoxCollider>();
                    pc.center = new Vector3(0, 1.3f, -0.6f);
                    pc.size = new Vector3(2.6f, 2.6f, 0.2f);
                    // flower wall
                    k.Box(new Vector3(0, 1.3f, -0.6f), new Vector3(2.6f, 2.6f, 0.1f), C(0xF4E0E8));
                    var rng = new System.Random(5);
                    for (int i = 0; i < 70; i++)
                    {
                        int col = i % 3 == 0 ? 0xFF7AA8 : i % 3 == 1 ? 0xFFFFFF : 0xFFB3C8;
                        k.Sphere(new Vector3((float)rng.NextDouble() * 2.4f - 1.2f, 0.1f + (float)rng.NextDouble() * 2.4f, -0.54f), 0.09f, 3, 5, C(col));
                    }
                    // ring light on a tripod
                    k.Tube(new Vector3(0, 0, 1.3f), new Vector3(0, 1.5f, 1.3f), 0.02f, 5, C(0x2A2A2A));
                    k.Push(new Vector3(0, 1.6f, 1.3f), Quaternion.Euler(90, 0, 0));
                    k.Torus(Vector3.zero, 0.3f, 0.035f, 24, 5, C(0xFFF6E8, 1f));
                    k.Pop();
                    k.Build("Photo Spot", t);
                    WorldBuilder.MakeText(t, "#WISHWALL", new Vector3(0, 2.4f, -0.66f), Quaternion.Euler(0, 180, 0), 0.26f, C(0xFF4FA8), 2.0f, WorldBuilder.SignFontAlt);
                    return t;
                }
                case "fountain_golden":
                {
                    t.position = new Vector3(-4.2f, 0, -11.4f);
                    t.rotation = Quaternion.LookRotation(new Vector3(0.2f, 0, -1f));
                    t.gameObject.AddComponent<BoxCollider>().center = new Vector3(0, 0.5f, 0);
                    k.Box(new Vector3(0, 0.5f, 0), new Vector3(1.0f, 1.0f, 1.0f), C(0xE8E0D0));
                    k.Box(new Vector3(0, 1.02f, 0), new Vector3(1.1f, 0.06f, 1.1f), C(0xC9A64A));
                    k.Build("Pedestal", t);
                    var p = Actors.MakePerson(t, C(0xF2C230), C(0xE8B83A), C(0xF2C230), C(0xE8B83A), false, 1.25f);
                    p.Root.localPosition = new Vector3(0, 1.05f, 0);
                    p.ArmR.localRotation = Quaternion.Euler(-150, 0, -10);
                    foreach (var r in p.Root.GetComponentsInChildren<MeshRenderer>()) r.sharedMaterials = new[] { Mats.Gloss };
                    WorldBuilder.MakeText(t, "THE FOUNDER", new Vector3(0, 0.6f, 0.51f), Quaternion.Euler(0, 180, 0), 0.13f, C(0x6A5020), 0.8f, WorldBuilder.SignFontAlt);
                    return t;
                }
                case "fountain_show":
                {
                    for (int i = 0; i < 6; i++)
                    {
                        float a = i * 60 * Mathf.Deg2Rad;
                        var b = new GameObject("Beam");
                        b.transform.SetParent(t, false);
                        b.transform.position = new Vector3(Mathf.Cos(a) * 11f, 17.5f, Mathf.Sin(a) * 11f);
                        var bk = new MeshKit();
                        bk.Cylinder(new Vector3(0, -8f, 0), 0.12f, 16f, 8, Color.white, false);
                        var bgo = bk.Build("Ray", b.transform, false);
                        var m = Mats.NewGlow(TexKit.SoftDot, 0.35f, Color.HSVToRGB(i / 6f, 0.8f, 1f));
                        bgo.GetComponent<MeshRenderer>().sharedMaterial = m;
                        beams.Add(b.transform);
                        beamMats.Add(m);
                        var hk = new MeshKit();
                        hk.Box(Vector3.zero, new Vector3(0.4f, 0.3f, 0.4f), C(0x2A2A2A));
                        hk.Build("Head", b.transform, false);
                    }
                    return t;
                }
                case "fountain_certified":
                {
                    var outward = new Vector3(Mathf.Cos(-115 * Mathf.Deg2Rad), 0, Mathf.Sin(-115 * Mathf.Deg2Rad));
                    t.position = outward * (FountainView.RimOuter + 0.03f) + Vector3.up * 0.45f;
                    t.rotation = Quaternion.LookRotation(-outward);
                    k.Box(Vector3.zero, new Vector3(1.4f, 0.55f, 0.04f), C(0xC9A64A));
                    k.Box(new Vector3(0, 0, -0.022f), new Vector3(1.3f, 0.45f, 0.01f), C(0x6A5020));
                    k.Cylinder(new Vector3(0.62f, -0.2f, -0.04f), Quaternion.Euler(90, 0, 0), 0.12f, 0.02f, 14, C(0xD8283A));
                    k.Build("Plaque", t);
                    WorldBuilder.MakeText(t, "OFFICIAL\nWISHING FOUNTAIN", new Vector3(-0.08f, 0.03f, -0.03f), Quaternion.identity, 0.085f, C(0xFFE8A0), 1.4f, WorldBuilder.SignFontAlt);
                    return t;
                }
                case "fountain_wormhole":
                {
                    t.position = new Vector3(0, 7.2f, 0);
                    portal = t;
                    k.Torus(Vector3.zero, 1.6f, 0.12f, 36, 8, C(0x9A5CF7, 1f));
                    k.Torus(Vector3.zero, 1.35f, 0.05f, 36, 6, C(0x5AE8FF, 1f));
                    k.Build("Ring", t, false);
                    var sw = new GameObject("Swirl");
                    sw.transform.SetParent(t, false);
                    sw.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
                    var mr = sw.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = Mats.NewGlow(TexKit.Ring, 1.6f, new Color(0.6f, 0.4f, 1f));
                    sw.transform.localScale = Vector3.one * 3.1f;
                    portalSwirl = sw.transform;
                    return t;
                }
            }
            Object.Destroy(go);
            return null;
        }

        public void Update(float dt, float time)
        {
            float water = ctx.Fountain.WaterY;
            foreach (var f in koi)
            {
                float a = f.phase + time * f.speed;
                var p = new Vector3(Mathf.Cos(a) * f.radius, water - 0.12f, Mathf.Sin(a) * f.radius);
                f.t.position = p;
                f.t.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a))) * Quaternion.Euler(0, 0, Mathf.Sin(time * 6 + f.phase) * 12);
            }
            for (int i = 0; i < beams.Count; i++)
            {
                var b = beams[i];
                b.rotation = Quaternion.Euler(Mathf.Sin(time * 0.7f + i) * 25, time * 20 + i * 60, Mathf.Cos(time * 0.5f + i) * 25);
                beamMats[i].SetColor("_Color", Color.HSVToRGB((time * 0.08f + i / 6f) % 1f, 0.8f, 1f));
            }
            if (portal != null)
            {
                portal.rotation = Quaternion.Euler(90 + Mathf.Sin(time * 0.4f) * 8, time * 15, 0);
                if (portalSwirl != null) portalSwirl.localRotation = Quaternion.Euler(0, -time * 120, 0);
            }
            if (built.ContainsKey("fountain_music"))
            {
                noteAcc += dt * 1.5f;
                while (noteAcc >= 1)
                {
                    noteAcc -= 1;
                    float a = Random.Range(0, 4) * 90 + 45;
                    var p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)) * 9.9f + Vector3.up * 0.8f;
                    ctx.Fx.Glint(p, new Color(1f, 0.85f, 0.5f), 0.5f, 1, 0.3f);
                }
            }
        }

        /// <summary>Where wormhole tosses come out.</summary>
        public Vector3 PortalPos => portal != null ? portal.position : new Vector3(0, 7, 0);
    }
}
