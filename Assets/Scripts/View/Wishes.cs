// True Wishes: glowing orbs that rise out of the water where a wishful toss landed, bob about
// for a few seconds, and can be caught by looking at one and pressing E. They sit on the Water
// layer as triggers, so they never block the player but the aim ray can still find them.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class WishView
    {
        public const int Layer = 4;   // Unity's built-in "Water" layer

        sealed class Orb
        {
            public ActiveWish W;
            public Transform T, Halo, Core;
            public Vector3 From, Hover;
            public float Seed, Out;
            public int Mode;   // 0 alive, 1 caught, 2 escaped
        }

        readonly Dictionary<int, Orb> orbs = new Dictionary<int, Orb>();
        readonly List<Orb> dying = new List<Orb>();
        Transform root;
        ViewContext ctx;
        Mesh coreMesh;
        readonly Material[] haloMats = new Material[5];
        readonly Material[] coreMats = new Material[5];
        Camera cam;

        public void Init(Transform parent, ViewContext c, Camera camera)
        {
            ctx = c;
            cam = camera;
            root = new GameObject("Wishes").transform;
            root.SetParent(parent, false);
            coreMesh = new MeshKit().Sphere(Vector3.zero, 0.16f, 10, 16, new Color(1, 1, 1, 1)).ToMesh("wish core");
            for (int i = 0; i < 5; i++)
            {
                haloMats[i] = Mats.NewGlow(TexKit.SoftDot, 0.85f + i * 0.22f, RarityColors.Orb[i]);
                coreMats[i] = new Material(Mats.Lit);
                coreMats[i].SetColor("_Color", Color.Lerp(RarityColors.Orb[i], Color.white, 0.15f));
                coreMats[i].SetFloat("_EmissionBoost", 1.1f + i * 0.25f);
            }
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
            var go = new GameObject("Wish " + w.Uid) { layer = Layer };
            go.transform.SetParent(root, false);
            int r = (int)w.Def.Rarity;
            float size = 1f + r * 0.12f;
            var core = new GameObject("Core");
            core.transform.SetParent(go.transform, false);
            core.AddComponent<MeshFilter>().sharedMesh = coreMesh;
            var cmr = core.AddComponent<MeshRenderer>();
            cmr.sharedMaterial = coreMats[r];
            cmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            core.transform.localScale = Vector3.one * size;
            var halo = new GameObject("Halo");
            halo.transform.SetParent(go.transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
            var hmr = halo.AddComponent<MeshRenderer>();
            hmr.sharedMaterial = haloMats[r];
            hmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            halo.transform.localScale = Vector3.one * (0.9f * size);
            var col = go.AddComponent<SphereCollider>();
            col.radius = 0.42f * size;
            col.isTrigger = true;
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "wish";
            ct.Uid = w.Uid;

            float water = ctx.Fountain.WaterY;
            var from = new Vector3(w.X, water, w.Z);
            // hover over the water, pulled a little toward the middle so they don't hide in the rim
            Vector3 flat = new Vector3(w.X, 0, w.Z);
            if (flat.magnitude > 6.2f) flat = flat.normalized * 6.2f;
            var hover = new Vector3(flat.x, water + 1.1f + Random.value * 1.0f, flat.z);
            var o = new Orb { W = w, T = go.transform, Halo = halo.transform, Core = core.transform, From = from, Hover = hover, Seed = Random.value * 10 };
            go.transform.position = from;
            orbs[w.Uid] = o;
            ctx.Fx.Sparks(from, RarityColors.Orb[r], 10 + r * 6, 3f, 0.12f, 0.6f);
        }

        public Vector3 PositionOf(int uid) => orbs.TryGetValue(uid, out var o) && o.T != null ? o.T.position : Vector3.zero;

        /// <summary>Where the last caught wish was (for the quote bubble).</summary>
        public Vector3 LastCaught { get; private set; }

        public void Caught(ActiveWish w)
        {
            if (!orbs.TryGetValue(w.Uid, out var o)) return;
            orbs.Remove(w.Uid);
            LastCaught = o.T.position;
            o.Mode = 1;
            o.Out = 0;
            Object.Destroy(o.T.GetComponent<SphereCollider>());
            int r = (int)w.Def.Rarity;
            ctx.Fx.Sparks(o.T.position, RarityColors.Orb[r], 18 + r * 8, 4f, 0.14f, 0.7f);
            ctx.Fx.Glint(o.T.position, Color.white, 1.2f + r * 0.3f, 2 + r);
            if (r >= 3) ctx.Fx.Confetti(o.T.position, 30 + r * 10, 5);
            dying.Add(o);
        }

        /// <summary>A wish nobody caught gets sucked into a Wish Compressor at dest.</summary>
        public void Compressed(ActiveWish w, Vector3 dest)
        {
            if (!orbs.TryGetValue(w.Uid, out var o)) return;
            orbs.Remove(w.Uid);
            o.Mode = 3;
            o.Out = 0;
            o.From = o.T.position;
            o.Hover = dest;
            Object.Destroy(o.T.GetComponent<SphereCollider>());
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

        public void Update(float dt, float time)
        {
            Quaternion face = cam != null ? cam.transform.rotation * Quaternion.Euler(-90, 0, 0) : Quaternion.identity;
            foreach (var o in orbs.Values)
            {
                if (o.T == null) continue;
                var w = o.W;
                float lifeT = (float)(w.Age / w.Life);
                float rise = Mathf.Clamp01((float)w.Age / 1.4f);
                rise = 1 - (1 - rise) * (1 - rise);
                Vector3 bob = new Vector3(Mathf.Sin(time * 0.9f + o.Seed) * 0.35f, Mathf.Sin(time * 1.7f + o.Seed) * 0.2f, Mathf.Cos(time * 0.7f + o.Seed) * 0.35f);
                Vector3 p = Vector3.Lerp(o.From, o.Hover, rise) + bob * rise;
                if (lifeT > 0.85f) p += Vector3.up * (lifeT - 0.85f) * 14f;
                o.T.position = p;
                float pulse = 1 + Mathf.Sin(time * 5 + o.Seed) * 0.08f;
                float fade = lifeT > 0.8f ? Mathf.Lerp(1, 0.55f, (lifeT - 0.8f) / 0.2f) : 1;
                o.Core.localScale = Vector3.one * (1f + (int)w.Def.Rarity * 0.12f) * pulse * fade;
                o.Halo.rotation = face;
                if ((int)w.Def.Rarity >= 2 && Random.value < dt * 3) ctx.Fx.Glint(p + Random.insideUnitSphere * 0.3f, RarityColors.Orb[(int)w.Def.Rarity], 0.4f);
            }
            for (int i = dying.Count - 1; i >= 0; i--)
            {
                var o = dying[i];
                if (o.T == null) { dying.RemoveAt(i); continue; }
                o.Out += dt;
                if (o.Mode == 1)
                {
                    float t = o.Out / 0.25f;
                    o.T.localScale = Vector3.one * Mathf.Max(0.01f, 1 + t * 1.4f);
                    if (t >= 1) { Object.Destroy(o.T.gameObject); dying.RemoveAt(i); continue; }
                }
                else if (o.Mode == 3)
                {
                    float t = Mathf.Clamp01(o.Out / 1.0f);
                    float e = t * t;
                    o.T.position = Vector3.Lerp(o.From, o.Hover, e) + Vector3.up * Mathf.Sin(t * Mathf.PI) * 2.5f;
                    o.T.localScale = Vector3.one * Mathf.Max(0.05f, 1 - e * 0.8f);
                    if (t >= 1) { ctx.Fx.Sparks(o.T.position, new Color(0.8f, 0.6f, 1f), 12, 3f, 0.1f, 0.5f); Object.Destroy(o.T.gameObject); dying.RemoveAt(i); continue; }
                }
                else
                {
                    float t = o.Out / 0.9f;
                    o.T.position += Vector3.up * dt * 6;
                    o.T.localScale = Vector3.one * Mathf.Max(0.01f, 1 - t);
                    if (t >= 1) { Object.Destroy(o.T.gameObject); dying.RemoveAt(i); continue; }
                }
                if (o.Halo != null) o.Halo.rotation = face;
            }
        }
    }
}
