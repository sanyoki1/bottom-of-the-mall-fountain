// Draws the shoppers from Sim.Shoppers: low-poly people with an archetype outfit and prop, a walk
// cycle, and a wind-up-and-throw arm animation with the item visible in their hand.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class CrowdView
    {
        sealed class Npc
        {
            public Shopper S;
            public Person P;
            public Transform HandL, Held;
            public int HeldType = -1;
            public float Seed, Throw;
            public string LastLine;
        }

        readonly Dictionary<int, Npc> npcs = new Dictionary<int, Npc>();
        readonly List<int> gone = new List<int>();
        Transform root;
        Sim sim;
        /// <summary>A shopper started saying something: head transform, text, is-a-wish, rarity.</summary>
        public System.Action<Transform, string, bool, Rarity> Speak;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        public void Init(Transform parent, Sim s)
        {
            sim = s;
            root = new GameObject("Crowd").transform;
            root.SetParent(parent, false);
        }

        public void Clear()
        {
            foreach (var n in npcs.Values) if (n.P.Root != null) Object.Destroy(n.P.Root.gameObject);
            npcs.Clear();
        }

        public int Count => npcs.Count;

        /// <summary>Head position of a shopper (for bubbles), or null if they're gone.</summary>
        public Transform HeadOf(int uid) => npcs.TryGetValue(uid, out var n) ? n.P.Head : null;

        Npc Make(Shopper s)
        {
            var d = s.Def;
            var p = Actors.MakePerson(root, C(d.Shirt), C(d.Pants), C(d.Skin), C(d.Hat), false, d.Scale, d.Headband);
            p.Root.name = d.Name;
            var n = new Npc { S = s, P = p, Seed = (s.Uid * 0.37f) % 10f };
            n.HandL = new GameObject("HandL").transform;
            n.HandL.SetParent(p.ArmL, false);
            n.HandL.localPosition = new Vector3(0, -0.52f, 0.02f);
            n.Held = new GameObject("Held").transform;
            n.Held.SetParent(p.Hand, false);
            AddProp(n, d.Prop);
            return n;
        }

        void AddProp(Npc n, string prop)
        {
            if (string.IsNullOrEmpty(prop)) return;
            var k = new MeshKit();
            Transform at = n.HandL;
            switch (prop)
            {
                case "phone":
                    k.Box(new Vector3(0, 0, 0.06f), new Vector3(0.07f, 0.13f, 0.012f), C(0x1A1A1E));
                    k.Box(new Vector3(0, 0, 0.067f), new Vector3(0.06f, 0.11f, 0.002f), C(0x6AC8FF, 0.7f));
                    break;
                case "briefcase":
                    k.Box(new Vector3(0, -0.14f, 0), new Vector3(0.1f, 0.3f, 0.42f), C(0x4A3222));
                    k.Box(new Vector3(0, 0.02f, 0), new Vector3(0.04f, 0.04f, 0.12f), C(0xC9A64A));
                    break;
                case "cane":
                    k.Tube(new Vector3(0, 0, 0.05f), new Vector3(0, -0.85f, 0.18f), 0.015f, 5, C(0x6A4A2A));
                    k.Torus(new Vector3(0, 0.03f, 0.08f), 0.04f, 0.012f, 8, 3, C(0x6A4A2A));
                    break;
                case "balloon":
                    k.Tube(Vector3.zero, new Vector3(0.05f, 1.1f, 0.05f), 0.003f, 3, C(0xEEEEEE));
                    k.Glossy(true);
                    k.Ellipsoid(new Vector3(0.05f, 1.3f, 0.05f), new Vector3(0.17f, 0.21f, 0.17f), 6, 10, C(0xE8453A));
                    break;
                case "rose":
                    k.Tube(Vector3.zero, new Vector3(0, 0.05f, 0.28f), 0.006f, 4, C(0x2F7F3F));
                    k.Sphere(new Vector3(0, 0.05f, 0.3f), 0.035f, 4, 6, C(0xC8102E));
                    break;
                case "camera":
                    at = n.P.Body;
                    k.Box(new Vector3(0, 1.0f, 0.24f), new Vector3(0.14f, 0.09f, 0.06f), C(0x2A2A2A));
                    k.Cylinder(new Vector3(0, 1.0f, 0.29f), Quaternion.Euler(90, 0, 0), 0.03f, 0.05f, 8, C(0x111111));
                    break;
                case "selfie":
                    k.Tube(Vector3.zero, new Vector3(0, 0.55f, 0.35f), 0.01f, 4, C(0x2A2A2A));
                    k.Box(new Vector3(0, 0.58f, 0.37f), new Vector3(0.08f, 0.14f, 0.012f), C(0x1A1A1E));
                    k.Torus(new Vector3(0, 0.58f, 0.36f), 0.09f, 0.008f, 12, 3, C(0xFFF4E0, 1f));
                    break;
                case "tophat":
                    at = n.P.Head;
                    k.Cylinder(new Vector3(0, 0.25f, 0), 0.13f, 0.24f, 12, C(0x111111));
                    k.Cylinder(new Vector3(0, 0.14f, 0), 0.21f, 0.02f, 14, C(0x111111));
                    k.Cylinder(new Vector3(0, 0.17f, 0), 0.135f, 0.04f, 12, C(0x8A1A1A));
                    k.Torus(new Vector3(0.06f, 0.03f, 0.155f), 0.03f, 0.005f, 10, 3, C(0xE8B83A));
                    break;
                case "dumbbell":
                    k.Tube(new Vector3(-0.12f, 0, 0), new Vector3(0.12f, 0, 0), 0.015f, 5, C(0x8A8A8A));
                    k.Cylinder(new Vector3(-0.13f, 0, 0), Quaternion.Euler(0, 0, 90), 0.07f, 0.06f, 10, C(0x222222));
                    k.Cylinder(new Vector3(0.13f, 0, 0), Quaternion.Euler(0, 0, 90), 0.07f, 0.06f, 10, C(0x222222));
                    break;
            }
            if (k.VertexCount > 0) k.Build("Prop", at, false);
        }

        void SetHeld(Npc n, int type)
        {
            if (type == n.HeldType) return;
            n.HeldType = type;
            foreach (Transform c in n.Held) Object.Destroy(c.gameObject);
            if (type < 0) return;
            var t = Content.Items[type];
            var go = Loot.MakeItemMesh(t.Shape, MeshKit.Hex(t.Color), t.Scale * 1.1f, n.Held);
            go.transform.localRotation = Quaternion.Euler(80, 0, 0);
        }

        public void Update(float dt, float time)
        {
            foreach (var s in sim.Shoppers)
            {
                if (!npcs.TryGetValue(s.Uid, out var n)) { n = Make(s); npcs[s.Uid] = n; }
                var p = n.P;
                p.Root.position = new Vector3(s.X, 0, s.Z);
                p.Root.rotation = Quaternion.Euler(0, s.Heading, 0);
                float moving = Mathf.Clamp01(s.Speed / 1.4f);
                Actors.Animate(p, time + n.Seed, moving);

                // the throw: wind back, then whip the arm forward; the item leaves the hand at release
                if (s.State == ShopperState.WindUp)
                {
                    float t = Mathf.Clamp01(s.Timer / (float)Balance.WindUp);
                    float ang = t < 0.7f ? Mathf.Lerp(0, 65, t / 0.7f) : Mathf.Lerp(65, -120, (t - 0.7f) / 0.3f);
                    p.ArmR.localRotation = Quaternion.Euler(ang, 0, -8);
                    SetHeld(n, s.PendingType);
                    n.Throw = 1;
                }
                else
                {
                    SetHeld(n, -1);
                    if (n.Throw > 0)
                    {
                        n.Throw = Mathf.Max(0, n.Throw - dt * 2.5f);
                        p.ArmR.localRotation = Quaternion.Euler(Mathf.Lerp(0, -120, n.Throw), 0, -8);
                    }
                }
                // keep the prop arm a little raised (phones get looked at, balloons get held up)
                if (s.Def.Prop == "phone" || s.Def.Prop == "selfie") p.ArmL.localRotation = Quaternion.Euler(-70 - (s.Def.Prop == "selfie" ? 40 : 0), 20, 10);
                else if (s.Def.Prop == "balloon") p.ArmL.localRotation = Quaternion.Euler(-40, 0, 20);

                if (s.Line != n.LastLine)
                {
                    n.LastLine = s.Line;
                    if (!string.IsNullOrEmpty(s.Line)) Speak?.Invoke(p.Head, s.Line, s.LineIsWish, s.LineRarity);
                }
            }
            gone.Clear();
            foreach (var kv in npcs)
            {
                bool alive = false;
                foreach (var s in sim.Shoppers) if (s.Uid == kv.Key) { alive = true; break; }
                if (!alive) gone.Add(kv.Key);
            }
            foreach (int u in gone)
            {
                Object.Destroy(npcs[u].P.Root.gameObject);
                npcs.Remove(u);
            }
        }
    }
}
