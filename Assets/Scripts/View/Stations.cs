// Fixed structures around the fountain: the greasy vending machine (sell point), the raw
// hopper, the washed-loot tray, the pocket pile, and decorative conveyors that carry
// loot between stages once you own machines for them.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class Stations
    {
        public Transform Root;
        public Transform Vending, VendingPanel;
        Transform hopperPile, trayPile, pocketPile;
        public Vector3 HopperTop, TrayTop, PocketTop, VendingSlot;
        float vendShake, vendFlash;
        Material vendMat;
        readonly List<Belt> belts = new List<Belt>();
        readonly List<BeltItem> items = new List<BeltItem>();
        readonly Stack<BeltItem> itemPool = new Stack<BeltItem>();
        ViewContext ctx;

        sealed class Belt
        {
            public GameObject Go;
            public float A0, A1, R;
            public float Accum;
            public int Stage;   // 1 = wash feed, 2 = wash->tray, 3 = tray->sort
        }

        sealed class BeltItem
        {
            public Transform T;
            public Belt B;
            public float P;
        }

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        public void Build(Transform parent, ViewContext c)
        {
            ctx = c;
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = new GameObject("Stations").transform;
            Root.SetParent(parent, false);
            belts.Clear();
            items.Clear();
            itemPool.Clear();
            BuildVending();
            BuildHopper();
            BuildTray();
            BuildPocket();
            AddBelt(1, 53, 130, 11.3f);
            AddBelt(2, 132, 233, 11.3f);
            AddBelt(3, 239, 280, 11.3f);
        }

        void BuildVending()
        {
            Vector3 pos = MachineVisual.Polar(293, 13.8f);
            Vending = new GameObject("Greasy Vending Machine").transform;
            Vending.SetParent(Root, false);
            Vending.localPosition = pos;
            // turned toward the default camera so its lit snack window reads from the start
            Vending.localRotation = Quaternion.LookRotation(new Vector3(-0.25f, 0, -1f));
            var k = new MeshKit();
            Color body = C(0xD8283A), dark = C(0x2A2A2A);
            k.Box(new Vector3(0, 1.45f, 0), new Vector3(1.9f, 2.9f, 1.2f), body);
            k.Box(new Vector3(0, 3.05f, 0), new Vector3(2.0f, 0.3f, 1.3f), dark);
            k.Box(new Vector3(0.62f, 1.6f, 0.61f), new Vector3(0.5f, 1.4f, 0.04f), C(0x2A2A2A));
            k.Box(new Vector3(0.62f, 2.0f, 0.64f), new Vector3(0.18f, 0.08f, 0.03f), C(0xFFD34D, 1f));
            k.Box(new Vector3(0.62f, 1.55f, 0.64f), new Vector3(0.32f, 0.3f, 0.03f), C(0x7CF45A, 0.8f));
            k.Box(new Vector3(-0.25f, 0.35f, 0.61f), new Vector3(1.1f, 0.35f, 0.05f), dark);
            // grease stains
            k.Box(new Vector3(-0.6f, 0.9f, 0.605f), new Vector3(0.3f, 0.2f, 0.01f), C(0x8A5A2A));
            k.Box(new Vector3(0.1f, 2.6f, 0.605f), new Vector3(0.25f, 0.15f, 0.01f), C(0x8A5A2A));
            k.Build("Cabinet", Vending);
            // snack window (flashes on sale)
            var w = new MeshKit();
            w.Box(Vector3.zero, new Vector3(1.05f, 1.75f, 0.04f), C(0xBFE8FF, 0.25f));
            string[] snackCols = { "FF7AA8", "FFD34D", "39E5D0", "FF9A3A", "7CF45A" };
            for (int row = 0; row < 5; row++)
                for (int col = 0; col < 4; col++)
                {
                    uint hex = System.Convert.ToUInt32(snackCols[(row + col) % snackCols.Length], 16);
                    w.Box(new Vector3(-0.38f + col * 0.25f, 0.68f - row * 0.34f, 0.03f), new Vector3(0.18f, 0.22f, 0.04f), C(hex, 0.15f));
                }
            var panel = w.Build("Window", Vending, false);
            panel.transform.localPosition = new Vector3(-0.25f, 1.75f, 0.62f);
            VendingPanel = panel.transform;
            vendMat = new Material(Mats.Lit);
            panel.GetComponent<MeshRenderer>().sharedMaterial = vendMat;
            WorldBuilder.MakeText(Vending, "SELL", new Vector3(0, 3.06f, 0.67f), Quaternion.Euler(0, 180, 0), 0.34f, C(0xFFE066), 2.2f);
            var col2 = new GameObject("Click");
            col2.transform.SetParent(Vending, false);
            var box = col2.AddComponent<BoxCollider>();
            box.center = new Vector3(0, 1.5f, 0);
            box.size = new Vector3(2.1f, 3.2f, 1.4f);
            var ct = col2.AddComponent<ClickTarget>();
            ct.Kind = "vending";
            VendingSlot = Vending.TransformPoint(new Vector3(0.62f, 2.0f, 0.7f));
        }

        Transform Pile(string name, Vector3 pos, Color c, Transform parent)
        {
            var k = new MeshKit();
            k.Ellipsoid(Vector3.zero, new Vector3(1f, 0.55f, 1f), 6, 14, c);
            for (int i = 0; i < 9; i++)
            {
                float a = i * 40 * Mathf.Deg2Rad;
                k.Ellipsoid(new Vector3(Mathf.Cos(a) * 0.55f, 0.25f, Mathf.Sin(a) * 0.55f), new Vector3(0.35f, 0.3f, 0.35f), 4, 8, c * (0.85f + 0.1f * (i % 3)));
            }
            var go = k.Build(name, parent);
            go.transform.localPosition = pos;
            go.transform.localScale = Vector3.zero;
            return go.transform;
        }

        void BuildHopper()
        {
            Vector3 pos = MachineVisual.Polar(50, 11.4f);
            var t = new GameObject("Hopper").transform;
            t.SetParent(Root, false);
            t.localPosition = pos;
            t.localRotation = MachineVisual.Face(pos);
            var k = new MeshKit();
            Color steel = C(0x8A8F96), stripe = C(0xF2C230);
            k.Box(new Vector3(0, 0.08f, 0), new Vector3(2.4f, 0.16f, 2.4f), steel * 0.8f);
            k.Box(new Vector3(-1.15f, 0.55f, 0), new Vector3(0.1f, 1.1f, 2.4f), steel);
            k.Box(new Vector3(1.15f, 0.55f, 0), new Vector3(0.1f, 1.1f, 2.4f), steel);
            k.Box(new Vector3(0, 0.55f, -1.15f), new Vector3(2.4f, 1.1f, 0.1f), steel);
            k.Box(new Vector3(0, 0.55f, 1.15f), new Vector3(2.4f, 1.1f, 0.1f), steel);
            k.Box(new Vector3(0, 1.1f, 1.2f), new Vector3(2.4f, 0.12f, 0.02f), stripe);
            k.Build("Bin", t);
            WorldBuilder.MakeText(t, "RAW GUNK", new Vector3(0, 0.6f, 1.22f), Quaternion.Euler(0, 180, 0), 0.22f, C(0xF2C230), 1.2f);
            hopperPile = Pile("Raw Pile", new Vector3(0, 0.2f, 0), C(0x6B4A2A), t);
            HopperTop = t.TransformPoint(new Vector3(0, 1.0f, 0));
        }

        void BuildTray()
        {
            Vector3 pos = MachineVisual.Polar(236, 11.4f);
            var t = new GameObject("Sorting Tray").transform;
            t.SetParent(Root, false);
            t.localPosition = pos;
            t.localRotation = MachineVisual.Face(pos);
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.7f, 0), new Vector3(2.6f, 0.1f, 1.8f), C(0xC0C4CA));
            k.Box(new Vector3(0, 0.85f, -0.9f), new Vector3(2.6f, 0.3f, 0.06f), C(0xC0C4CA));
            k.Box(new Vector3(0, 0.85f, 0.9f), new Vector3(2.6f, 0.3f, 0.06f), C(0xC0C4CA));
            for (int i = 0; i < 4; i++) k.Box(new Vector3((i % 2 == 0 ? -1.2f : 1.2f), 0.35f, (i < 2 ? -0.8f : 0.8f)), new Vector3(0.08f, 0.7f, 0.08f), C(0x555A62));
            k.Build("Table", t);
            WorldBuilder.MakeText(t, "WASHED", new Vector3(0, 0.88f, 0.94f), Quaternion.Euler(0, 180, 0), 0.2f, C(0x39E5D0), 1.2f);
            trayPile = Pile("Washed Pile", new Vector3(0, 0.75f, 0), C(0xB8B8A8), t);
            TrayTop = t.TransformPoint(new Vector3(0, 1.3f, 0));
        }

        void BuildPocket()
        {
            Vector3 pos = MachineVisual.Polar(283, 11.5f);
            var t = new GameObject("Pocket").transform;
            t.SetParent(Root, false);
            t.localPosition = pos;
            var k = new MeshKit();
            k.Cylinder(new Vector3(0, 0.05f, 0), 1.3f, 0.1f, 20, C(0x5A5F68));
            k.Build("Mat", t);
            pocketPile = Pile("Coin Pile", new Vector3(0, 0.1f, 0), C(0xD9A04A), t);
            PocketTop = t.TransformPoint(new Vector3(0, 0.8f, 0));
        }

        void AddBelt(int stage, float a0Deg, float a1Deg, float r)
        {
            var k = new MeshKit();
            float a0 = a0Deg * Mathf.Deg2Rad, a1 = a1Deg * Mathf.Deg2Rad;
            float lo = Mathf.Min(a0, a1), hi = Mathf.Max(a0, a1);
            int seg = Mathf.Max(4, Mathf.RoundToInt(Mathf.Abs(a1Deg - a0Deg) / 3));
            k.Wall(Vector3.zero, r - 0.45f, 0, 0.5f, seg, C(0x555A62), true, lo, hi);
            k.Wall(Vector3.zero, r + 0.45f, 0, 0.5f, seg, C(0x555A62), false, lo, hi);
            k.Push(new Vector3(0, 0.5f, 0));
            k.Ring(Vector3.zero, r - 0.5f, r - 0.4f, seg, C(0xF2C230), lo, hi);
            k.Ring(Vector3.zero, r + 0.4f, r + 0.5f, seg, C(0xF2C230), lo, hi);
            k.Pop();
            var frame = k.Build("Belt Frame", Root);
            var s = new MeshKit();
            s.Push(new Vector3(0, 0.46f, 0));
            s.Ring(Vector3.zero, r - 0.4f, r + 0.4f, seg, Color.white, lo, hi);
            s.Pop();
            var surf = s.Build("Belt", frame.transform, false);
            var mesh = surf.GetComponent<MeshFilter>().sharedMesh;
            var v = mesh.vertices;
            var uv = new Vector2[v.Length];
            float dir = a1 > a0 ? -1 : 1;
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(Mathf.Atan2(v[i].z, v[i].x) * r * 0.5f * dir, new Vector2(v[i].x, v[i].z).magnitude);
            mesh.uv = uv;
            surf.GetComponent<MeshRenderer>().sharedMaterial = Mats.Belt;
            frame.SetActive(false);
            belts.Add(new Belt { Go = frame, A0 = a0, A1 = a1, R = r, Stage = stage });
        }

        public void SetBeltsVisible(bool wash, bool sort)
        {
            foreach (var b in belts) b.Go.SetActive(b.Stage == 3 ? sort : wash);
        }

        static float PileScale(double count)
        {
            if (count < 1) return 0;
            return Mathf.Clamp(0.25f + (float)System.Math.Log10(count + 1) * 0.13f, 0.25f, 1.35f);
        }

        public void FlashSale(bool big)
        {
            vendShake = big ? 1f : 0.6f;
            vendFlash = 1;
        }

        public void Update(float dt, float time, Sim sim, float washAct, float sortAct)
        {
            float k = 1 - Mathf.Exp(-dt * 5);
            hopperPile.localScale = Vector3.Lerp(hopperPile.localScale, Vector3.one * PileScale(sim.S.hopperCount), k);
            trayPile.localScale = Vector3.Lerp(trayPile.localScale, Vector3.one * PileScale(sim.S.trayCount), k);
            pocketPile.localScale = Vector3.Lerp(pocketPile.localScale, Vector3.one * PileScale(sim.S.pocketCount), k);

            if (vendShake > 0)
            {
                vendShake = Mathf.Max(0, vendShake - dt * 2.5f);
                Vending.localScale = new Vector3(1 + Mathf.Sin(vendShake * 40) * vendShake * 0.04f, 1 - Mathf.Sin(vendShake * 40) * vendShake * 0.03f, 1);
            }
            vendFlash = Mathf.Max(0, vendFlash - dt * 2f);
            float pulse = 0.35f + 0.1f * Mathf.Sin(time * 3);
            if (vendMat != null) vendMat.SetFloat("_Flash", vendFlash * 0.8f + (sim.S.pocketValue > 0 && !sim.AutoSell ? pulse * 0.3f : 0));

            // belt items
            foreach (var b in belts)
            {
                if (!b.Go.activeSelf) continue;
                float act = b.Stage == 3 ? sortAct : washAct;
                b.Accum += dt * act * 2.2f;
                while (b.Accum >= 1)
                {
                    b.Accum -= 1;
                    var it = itemPool.Count > 0 ? itemPool.Pop() : NewItem();
                    it.B = b;
                    it.P = 0;
                    it.T.gameObject.SetActive(true);
                    items.Add(it);
                }
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                var it = items[i];
                float arcLen = Mathf.Abs(it.B.A1 - it.B.A0) * it.B.R;
                it.P += dt * 1.8f / Mathf.Max(1, arcLen);
                if (it.P >= 1 || !it.B.Go.activeSelf)
                {
                    it.T.gameObject.SetActive(false);
                    items.RemoveAt(i);
                    itemPool.Push(it);
                    continue;
                }
                float a = Mathf.Lerp(it.B.A0, it.B.A1, it.P);
                it.T.localPosition = new Vector3(Mathf.Cos(a) * it.B.R, 0.58f, Mathf.Sin(a) * it.B.R);
                it.T.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0);
            }
        }

        BeltItem NewItem()
        {
            var shape = Random.value < 0.6f ? ItemShape.Coin : Random.value < 0.5f ? ItemShape.Wad : ItemShape.Cube;
            Color c = shape == ItemShape.Coin ? (Random.value < 0.5f ? C(0xC77B43) : C(0xC9CDD2)) : C(0x8A6A4A);
            var go = Loot.MakeItemMesh(shape, c, 2.2f, Root);
            return new BeltItem { T = go.transform };
        }
    }
}
