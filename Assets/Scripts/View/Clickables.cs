// World clickables: floating True Wishes, Golden Pennies glinting in the crust, and the
// mall rat that scurries along the rim with stolen loot.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public static class RarityColors
    {
        public static readonly Color[] Orb =
        {
            new Color(0.75f, 0.9f, 1f),     // common: pale blue
            new Color(0.45f, 1f, 0.6f),     // uncommon: mint
            new Color(0.35f, 0.65f, 1f),    // rare: blue
            new Color(0.78f, 0.45f, 1f),    // epic: violet
            new Color(1f, 0.82f, 0.25f),    // legendary: gold
        };
        public static readonly string[] Names = { "Common", "Uncommon", "Rare", "Epic", "Legendary" };
    }

    public sealed class WishOrbs
    {
        sealed class Orb
        {
            public ActiveWish W;
            public Transform T, Halo, Core;
            public Vector3 From, Hover, Drift;
            public float Seed, Out;
            public bool Leaving;
            public int Mode;   // 0 alive, 1 caught, 2 escaped, 3 compressed
            public Vector3 Dest;
        }

        readonly Dictionary<int, Orb> orbs = new Dictionary<int, Orb>();
        readonly List<Orb> dying = new List<Orb>();
        Transform root;
        ViewContext ctx;
        Mesh coreMesh;
        readonly Material[] haloMats = new Material[5];
        public System.Func<Vector3> SourcePoint;   // where wishes emerge (wash machines)
        public System.Func<Vector3> CompressorIntake;
        Camera cam;

        public void Init(Transform parent, ViewContext c, Camera camera)
        {
            ctx = c;
            cam = camera;
            root = new GameObject("Wishes").transform;
            root.SetParent(parent, false);
            coreMesh = new MeshKit().Sphere(Vector3.zero, 0.34f, 10, 16, new Color(1, 1, 1, 1)).ToMesh("wish core");
            for (int i = 0; i < 5; i++) haloMats[i] = Mats.NewGlow(TexKit.SoftDot, 0.85f + i * 0.22f, RarityColors.Orb[i]);
        }

        public void Clear()
        {
            foreach (var o in orbs.Values) if (o.T != null) Object.Destroy(o.T.gameObject);
            foreach (var o in dying) if (o.T != null) Object.Destroy(o.T.gameObject);
            orbs.Clear();
            dying.Clear();
        }

        public void Spawn(ActiveWish w)
        {
            var go = new GameObject("Wish " + w.Uid);
            go.transform.SetParent(root, false);
            int r = (int)w.Def.Rarity;
            float size = 1f + r * 0.12f;
            var core = new GameObject("Core");
            core.transform.SetParent(go.transform, false);
            core.AddComponent<MeshFilter>().sharedMesh = coreMesh;
            var cmr = core.AddComponent<MeshRenderer>();
            var mat = new Material(Mats.Lit);
            mat.SetColor("_Color", Color.Lerp(RarityColors.Orb[r], Color.white, 0.12f));
            mat.SetFloat("_EmissionBoost", 1.05f + r * 0.25f);
            cmr.sharedMaterial = mat;
            cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core.transform.localScale = Vector3.one * size;
            var halo = new GameObject("Halo");
            halo.transform.SetParent(go.transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
            var hmr = halo.AddComponent<MeshRenderer>();
            hmr.sharedMaterial = haloMats[r];
            hmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.transform.localScale = Vector3.one * (1.7f * size);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 1.0f * size;
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "wish";
            ct.Uid = w.Uid;

            Vector3 from = SourcePoint != null ? SourcePoint() : new Vector3(0, FountainView.RimTop, 0);
            float a = Random.value * Mathf.PI * 2, rr = Mathf.Sqrt(Random.value) * 6.5f;
            Vector3 hover = new Vector3(Mathf.Cos(a) * rr, Mathf.Max(ctx.Fountain.SurfaceY, 0) + 3.2f + Random.value * 3.2f, Mathf.Sin(a) * rr - 1.0f);
            var o = new Orb { W = w, T = go.transform, Halo = halo.transform, Core = core.transform, From = from, Hover = hover, Seed = Random.value * 10, Drift = Random.insideUnitSphere * 0.6f };
            go.transform.position = from;
            orbs[w.Uid] = o;
        }

        public Vector3 PositionOf(int uid) => orbs.TryGetValue(uid, out var o) && o.T != null ? o.T.position : Vector3.zero;

        public void Caught(ActiveWish w)
        {
            if (!orbs.TryGetValue(w.Uid, out var o)) return;
            orbs.Remove(w.Uid);
            o.Mode = 1;
            o.Out = 0;
            Object.Destroy(o.T.GetComponent<SphereCollider>());
            int r = (int)w.Def.Rarity;
            ctx.Fx.Sparks(o.T.position, RarityColors.Orb[r], 18 + r * 8, 5f, 0.18f, 0.7f);
            ctx.Fx.Glint(o.T.position, Color.white, 2.2f + r * 0.4f, 2 + r);
            if (r >= 3) ctx.Fx.Confetti(o.T.position, 30 + r * 10, 6);
            dying.Add(o);
        }

        public void Escaped(ActiveWish w)
        {
            if (!orbs.TryGetValue(w.Uid, out var o)) return;
            orbs.Remove(w.Uid);
            o.Mode = 2;
            o.Out = 0;
            Object.Destroy(o.T.GetComponent<SphereCollider>());
            dying.Add(o);
        }

        public void Compressed(ActiveWish w)
        {
            if (!orbs.TryGetValue(w.Uid, out var o)) return;
            orbs.Remove(w.Uid);
            o.Mode = 3;
            o.Out = 0;
            o.From = o.T.position;
            o.Dest = CompressorIntake != null ? CompressorIntake() : o.T.position + Vector3.up * 5;
            Object.Destroy(o.T.GetComponent<SphereCollider>());
            dying.Add(o);
        }

        public void Update(float dt, float time)
        {
            Quaternion face = cam != null ? cam.transform.rotation * Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
            foreach (var o in orbs.Values)
            {
                if (o.T == null) continue;
                var w = o.W;
                float lifeT = (float)(w.Age / w.Life);
                float rise = Mathf.Clamp01((float)w.Age / 1.6f);
                rise = 1 - (1 - rise) * (1 - rise);
                Vector3 bob = new Vector3(Mathf.Sin(time * 0.9f + o.Seed) * 0.5f, Mathf.Sin(time * 1.7f + o.Seed) * 0.35f, Mathf.Cos(time * 0.7f + o.Seed) * 0.5f);
                Vector3 p = Vector3.Lerp(o.From, o.Hover + o.Drift * (float)w.Age * 0.3f, rise) + bob * rise;
                if (lifeT > 0.85f) p += Vector3.up * (lifeT - 0.85f) * 20f;
                o.T.position = p;
                float pulse = 1 + Mathf.Sin(time * 5 + o.Seed) * 0.08f;
                float fade = lifeT > 0.8f ? Mathf.Lerp(1, 0.55f, (lifeT - 0.8f) / 0.2f) : 1;
                o.Core.localScale = Vector3.one * (1f + (int)w.Def.Rarity * 0.12f) * pulse * fade;
                o.Halo.rotation = face;
                if ((int)w.Def.Rarity >= 2 && Random.value < dt * 3) ctx.Fx.Glint(p + Random.insideUnitSphere * 0.5f, RarityColors.Orb[(int)w.Def.Rarity], 0.5f);
            }
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                var o = dying[i];
                if (o.T == null) { dying.RemoveAt(i); continue; }
                o.Out += dt;
                if (o.Mode == 1)
                {
                    float t = o.Out / 0.25f;
                    o.T.localScale = Vector3.one * (1 + t * 1.4f);
                    if (t >= 1) { Object.Destroy(o.T.gameObject); dying.RemoveAt(i); }
                }
                else if (o.Mode == 2)
                {
                    float t = o.Out / 0.9f;
                    o.T.position += Vector3.up * dt * 7;
                    o.T.localScale = Vector3.one * Mathf.Max(0.01f, 1 - t);
                    if (t >= 1) { Object.Destroy(o.T.gameObject); dying.RemoveAt(i); }
                }
                else
                {
                    float t = Mathf.Clamp01(o.Out / 0.9f);
                    float e = t * t;
                    o.T.position = Vector3.Lerp(o.From, o.Dest, e) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2;
                    o.T.localScale = Vector3.one * Mathf.Lerp(1, 0.2f, e);
                    if (t >= 1) { Object.Destroy(o.T.gameObject); dying.RemoveAt(i); }
                }
                if (o.Halo != null) o.Halo.rotation = face;
            }
        }
    }

    public sealed class GoldenPennies
    {
        sealed class Penny { public ActiveGolden G; public Transform T, Coin, Halo; }
        readonly Dictionary<int, Penny> pennies = new Dictionary<int, Penny>();
        Transform root;
        ViewContext ctx;
        Mesh coinMesh;
        Material haloMat;
        Camera cam;

        public void Init(Transform parent, ViewContext c, Camera camera)
        {
            ctx = c;
            cam = camera;
            root = new GameObject("Golden Pennies").transform;
            root.SetParent(parent, false);
            var k = new MeshKit().Glossy(true);
            k.Cylinder(Vector3.zero, 0.55f, 0.1f, 24, new Color(1f, 0.78f, 0.25f, 0.45f));
            k.Torus(new Vector3(0, 0.05f, 0), 0.48f, 0.035f, 24, 4, new Color(1f, 0.9f, 0.5f, 0.6f));
            k.Torus(new Vector3(0, -0.05f, 0), 0.48f, 0.035f, 24, 4, new Color(1f, 0.9f, 0.5f, 0.6f));
            k.Box(new Vector3(0, 0.055f, 0), new Vector3(0.12f, 0.02f, 0.5f), new Color(1f, 0.95f, 0.7f, 0.8f));
            k.Box(new Vector3(0, -0.055f, 0), new Vector3(0.12f, 0.02f, 0.5f), new Color(1f, 0.95f, 0.7f, 0.8f));
            coinMesh = k.ToMesh("golden penny");
            haloMat = Mats.NewGlow(TexKit.SoftDot, 2.4f, new Color(1f, 0.82f, 0.3f));
        }

        public void Clear()
        {
            foreach (var p in pennies.Values) if (p.T != null) Object.Destroy(p.T.gameObject);
            pennies.Clear();
        }

        public void Spawn(ActiveGolden g)
        {
            var go = new GameObject("Golden Penny " + g.Uid);
            go.transform.SetParent(root, false);
            var coin = new GameObject("Coin");
            coin.transform.SetParent(go.transform, false);
            coin.AddComponent<MeshFilter>().sharedMesh = coinMesh;
            coin.AddComponent<MeshRenderer>().sharedMaterials = new[] { Mats.Lit, Mats.Gloss };
            var halo = new GameObject("Halo");
            halo.transform.SetParent(go.transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
            var hmr = halo.AddComponent<MeshRenderer>();
            hmr.sharedMaterial = haloMat;
            hmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.transform.localScale = Vector3.one * 3.2f;
            var col = go.AddComponent<SphereCollider>();
            col.radius = 1.3f;
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "golden";
            ct.Uid = g.Uid;
            pennies[g.Uid] = new Penny { G = g, T = go.transform, Coin = coin.transform, Halo = halo.transform };
            ctx.Fx.Glint(Pos(g), new Color(1f, 0.9f, 0.5f), 2.5f, 3, 0.5f);
        }

        Vector3 Pos(ActiveGolden g)
        {
            float x = (float)g.X * 6.8f, z = (float)g.Z * 6.8f - 0.5f;
            return new Vector3(x, ctx.Fountain.HeightAt(x, z) + 0.75f, z);
        }

        public Vector3 PositionOf(int uid) => pennies.TryGetValue(uid, out var p) ? p.T.position : Vector3.zero;

        public void Remove(int uid, bool claimed)
        {
            if (!pennies.TryGetValue(uid, out var p)) return;
            pennies.Remove(uid);
            if (claimed)
            {
                ctx.Fx.Confetti(p.T.position, 60, 8);
                ctx.Fx.CoinShower(p.T.position, new[] { new Color(1f, 0.8f, 0.3f), new Color(1f, 0.9f, 0.55f) }, 30, 7, 1.4f);
                ctx.Fx.Glint(p.T.position, Color.white, 4f, 4, 0.6f);
            }
            Object.Destroy(p.T.gameObject);
        }

        public void Update(float dt, float time)
        {
            Quaternion face = cam != null ? cam.transform.rotation * Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
            foreach (var p in pennies.Values)
            {
                float appear = Mathf.Clamp01((float)p.G.Age / 0.5f);
                float leave = Mathf.Clamp01((float)(p.G.Life - p.G.Age) / 1.5f);
                p.T.position = Pos(p.G) + Vector3.up * Mathf.Sin(time * 2.2f) * 0.18f;
                p.Coin.localRotation = Quaternion.Euler(90, time * 160, 0);
                float blink = leave < 1 ? (Mathf.Sin(time * 25) > 0 ? 1 : 0.3f) : 1;
                p.T.localScale = Vector3.one * appear * (0.6f + 0.4f * leave) * blink;
                p.Halo.rotation = face;
                if (Random.value < dt * 4) ctx.Fx.Glint(p.T.position + Random.insideUnitSphere * 0.7f, new Color(1f, 0.92f, 0.6f), 0.8f);
            }
        }
    }

    public sealed class RatView
    {
        Transform rat;
        ActiveRat data;
        ViewContext ctx;
        Transform parent;

        public void Init(Transform p, ViewContext c) { parent = p; ctx = c; }

        public void Spawn(ActiveRat r)
        {
            Remove(false);
            data = r;
            rat = Actors.MakeRat(parent);
            var col = rat.gameObject.AddComponent<SphereCollider>();
            col.radius = 0.9f;
            col.center = new Vector3(0, 0.3f, 0);
            var ct = rat.gameObject.AddComponent<ClickTarget>();
            ct.Kind = "rat";
            ct.Uid = r.Uid;
        }

        public Vector3 Position => rat != null ? rat.position : Vector3.zero;

        public void Remove(bool caught)
        {
            if (rat == null) return;
            if (caught)
            {
                ctx.Fx.Dust(rat.position, new Color(0.6f, 0.55f, 0.5f), 10, 0.9f, 0.8f, 1.5f);
                ctx.Fx.Glint(rat.position + Vector3.up * 0.5f, new Color(1f, 0.9f, 0.5f), 2f, 3);
            }
            Object.Destroy(rat.gameObject);
            rat = null;
            data = null;
        }

        public void Update(float dt, float time, ActiveRat live)
        {
            if (rat == null) return;
            if (live == null || live.Uid != data.Uid) { Remove(false); return; }
            float t = (float)(data.Age / data.Life);
            float ang = (float)data.StartAngle + data.Dir * t * Mathf.PI * 1.25f;
            float r = 8.7f;
            Vector3 pos = new Vector3(Mathf.Cos(ang) * r, FountainView.RimTop + Mathf.Abs(Mathf.Sin(time * 22)) * 0.08f, Mathf.Sin(ang) * r);
            rat.position = pos;
            Vector3 tangent = new Vector3(-Mathf.Sin(ang), 0, Mathf.Cos(ang)) * data.Dir;
            rat.rotation = Quaternion.LookRotation(tangent);
            float appear = Mathf.Clamp01(t * 8) * Mathf.Clamp01((1 - t) * 8);
            rat.localScale = Vector3.one * 1.6f * appear;
            if (Random.value < dt * 6) ctx.Fx.Dust(pos, new Color(0.7f, 0.65f, 0.6f), 1, 0.35f, 0.2f, 0.3f);
        }
    }

    public sealed class WorkerView
    {
        Person p;
        Transform tool;
        int toolIndex = -1;
        Vector3 pos, target;
        float swing, hop, celebrate;
        ViewContext ctx;
        Vector3 facing = Vector3.forward;
        public Transform Root => p?.Root;

        public void Init(Transform parent, ViewContext c)
        {
            ctx = c;
            // vertex alpha is an emission mask in WE/Lit, so every colour here is explicitly matte (a = 0)
            p = Actors.MakePerson(parent, new Color(1f, 0.55f, 0.1f, 0), new Color(0.2f, 0.3f, 0.5f, 0), new Color(0.9f, 0.72f, 0.56f, 0), new Color(1f, 0.85f, 0.1f, 0), true, 1.15f);
            p.Root.name = "Worker";
            pos = target = new Vector3(0.5f, 0, -4.8f);
            // reflective vest stripes (slightly emissive)
            var k = new MeshKit();
            k.Box(new Vector3(0, 1.05f, 0.22f), new Vector3(0.44f, 0.07f, 0.04f), new Color(0.9f, 0.95f, 0.9f, 0.25f));
            k.Box(new Vector3(0, 0.88f, 0.22f), new Vector3(0.44f, 0.07f, 0.04f), new Color(0.9f, 0.95f, 0.9f, 0.25f));
            k.Build("Vest", p.Body);
        }

        public void SetTool(int index)
        {
            if (index == toolIndex) return;
            toolIndex = index;
            if (tool != null) Object.Destroy(tool.gameObject);
            tool = Actors.MakeTool(index, p.Hand).transform;
            ctx.Fx.Glint(p.Hand.position, Color.white, 1.5f, 2);
        }

        public void DigAt(Vector3 point)
        {
            Vector3 flat = new Vector3(point.x, 0, point.z);
            Vector3 home = new Vector3(pos.x, 0, pos.z);
            // step toward the click but stop an arm's length short
            Vector3 dir = flat - home;
            if (dir.magnitude > 2.2f) target = flat - dir.normalized * 1.3f;
            if (dir.sqrMagnitude > 0.01f) facing = dir.normalized;
            float tr = new Vector2(target.x, target.z).magnitude;
            if (tr < 1.6f) target = new Vector3(target.x, 0, target.z).normalized * 1.6f;
            if (tr > 7.1f) target = new Vector3(target.x, 0, target.z).normalized * 7.1f;
            swing = 1f;
            hop = 1f;
        }

        public void Celebrate() { celebrate = 3f; }

        public void Update(float dt, float time)
        {
            if (p == null) return;
            Vector3 flatPos = new Vector3(pos.x, 0, pos.z);
            Vector3 to = new Vector3(target.x, 0, target.z) - flatPos;
            float moving = 0;
            if (to.magnitude > 0.05f)
            {
                float step = Mathf.Min(to.magnitude, dt * 7f);
                flatPos += to.normalized * step;
                moving = 1;
            }
            pos = flatPos;
            float ground = ctx.Fountain.HeightAt(pos.x, pos.z);
            hop = Mathf.Max(0, hop - dt * 4);
            celebrate = Mathf.Max(0, celebrate - dt);
            float jump = Mathf.Sin(hop * Mathf.PI) * 0.35f + (celebrate > 0 ? Mathf.Abs(Mathf.Sin(time * 8)) * 0.6f : 0);
            p.Root.position = new Vector3(pos.x, ground + jump, pos.z);
            p.Root.rotation = Quaternion.Slerp(p.Root.rotation, Quaternion.LookRotation(facing), dt * 12);
            Actors.Animate(p, time, moving);
            swing = Mathf.Max(0, swing - dt * 4.5f);
            if (swing > 0)
            {
                float s = swing > 0.5f ? Mathf.Lerp(40, -150, (swing - 0.5f) * 2) : Mathf.Lerp(-10, 40, swing * 2);
                p.ArmR.localRotation = Quaternion.Euler(s, 0, -6);
                p.ArmL.localRotation = Quaternion.Euler(s * 0.7f, 0, 6);
            }
            else if (celebrate > 0)
            {
                p.ArmR.localRotation = Quaternion.Euler(0, 0, -150 + Mathf.Sin(time * 10) * 20);
                p.ArmL.localRotation = Quaternion.Euler(0, 0, 150 - Mathf.Sin(time * 10) * 20);
            }
        }
    }
}
