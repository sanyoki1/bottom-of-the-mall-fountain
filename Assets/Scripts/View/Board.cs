// The "Fountain Improvement Plan": a clipboard on an easel by the rim, listing the next
// beautification job and its price. Look at it and press E to approve (buy) the job.
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class FountainBoard
    {
        public Transform Root { get; private set; }
        TextMesh title, job, price, detail, done;
        float refresh, flash;
        Sim sim;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        public void Build(Transform parent, Sim s)
        {
            sim = s;
            Root = new GameObject("Fountain Improvement Plan").transform;
            Root.SetParent(parent, false);
            Root.position = new Vector3(Layout.BoardX, 0, Layout.BoardZ);
            // face the path between the entrance and the fountain
            Vector3 look = new Vector3(-1.5f, 0, -16f) - Root.position;
            Root.rotation = Quaternion.LookRotation(look.normalized);

            var k = new MeshKit();
            Color wood = C(0x8A5A34), board = C(0xF4F0E6), clip = C(0xB8BEC4), cork = C(0xC8A070);
            // easel legs
            k.Tube(new Vector3(-0.45f, 0, 0.25f), new Vector3(-0.3f, 1.9f, 0.02f), 0.03f, 6, wood);
            k.Tube(new Vector3(0.45f, 0, 0.25f), new Vector3(0.3f, 1.9f, 0.02f), 0.03f, 6, wood);
            k.Tube(new Vector3(0, 0, -0.45f), new Vector3(0, 1.8f, -0.02f), 0.03f, 6, wood);
            k.Box(new Vector3(0, 0.62f, 0.2f), new Vector3(0.95f, 0.05f, 0.1f), wood);
            // cork board with a clipped work order
            k.Push(new Vector3(0, 1.22f, 0.12f), Quaternion.Euler(-12, 0, 0));
            k.Box(Vector3.zero, new Vector3(0.9f, 1.1f, 0.04f), cork);
            k.Box(new Vector3(0, -0.02f, 0.025f), new Vector3(0.74f, 0.96f, 0.01f), board);
            k.Box(new Vector3(0, 0.46f, 0.035f), new Vector3(0.24f, 0.06f, 0.03f), clip);
            k.Box(new Vector3(0, 0.33f, 0.031f), new Vector3(0.66f, 0.004f, 0.002f), C(0x2E9C95));
            k.Box(new Vector3(-0.2f, -0.33f, 0.031f), new Vector3(0.26f, 0.08f, 0.002f), C(0x28B463, 0.2f));
            k.Pop();
            // a little hard hat hanging off the corner, for authenticity
            k.Ellipsoid(new Vector3(0.42f, 1.78f, 0.1f), new Vector3(0.14f, 0.1f, 0.14f), 5, 10, C(0xFFD000));
            k.Build("Easel", Root);

            var face = Quaternion.Euler(-12, 180, 0);
            Vector3 P(float x, float y) => new Vector3(0, 1.22f, 0.12f) + Quaternion.Euler(-12, 0, 0) * new Vector3(x, y, 0.036f);
            title = WorldBuilder.MakeText(Root, "FOUNTAIN IMPROVEMENT PLAN", P(0, 0.4f), face, 0.052f, C(0x1D1D1F), 0.75f, WorldBuilder.SignFontAlt);
            job = WorldBuilder.MakeText(Root, "", P(0, 0.2f), face, 0.07f, C(0x1D1D1F), 0.75f);
            detail = WorldBuilder.MakeText(Root, "", P(0, 0.0f), face, 0.036f, C(0x4A4A4E), 0.7f);
            price = WorldBuilder.MakeText(Root, "", P(0.14f, -0.33f), face, 0.06f, Color.white, 0.8f);
            done = WorldBuilder.MakeText(Root, "", P(-0.2f, -0.33f), face, 0.04f, C(0xFFFFFF), 1f);

            var col = Root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.1f, 0.05f);
            col.size = new Vector3(1.0f, 2.1f, 0.7f);
            var ct = Root.gameObject.AddComponent<ClickTarget>();
            ct.Kind = "board";
            Refresh();
        }

        public void Flash() { flash = 1; Refresh(); }

        void Refresh()
        {
            int i = sim.NextFountainTech();
            done.text = $"{sim.FountainUpgradesOwned} DONE";
            if (i < 0)
            {
                job.text = "ALL JOBS DONE";
                detail.text = "Check the Maintenance Terminal\nfor more.";
                price.text = "";
                return;
            }
            var t = Content.Techs[i];
            job.text = t.Name.ToUpperInvariant();
            detail.text = Wrap(t.Desc, 34) + $"\n\nWISHABILITY +{t.Value:0}";
            price.text = Fmt.Money(sim.TechCost(i));
            price.color = sim.CanAfford(i) ? new Color(0.1f, 0.45f, 0.35f) : new Color(0.75f, 0.2f, 0.2f);
        }

        static string Wrap(string s, int width)
        {
            var words = s.Split(' ');
            var sb = new System.Text.StringBuilder();
            int line = 0, lines = 0;
            foreach (var w in words)
            {
                if (line + w.Length > width && line > 0)
                {
                    if (++lines >= 3) { sb.Append('…'); return sb.ToString(); }
                    sb.Append('\n');
                    line = 0;
                }
                else if (line > 0) { sb.Append(' '); line++; }
                sb.Append(w);
                line += w.Length;
            }
            return sb.ToString();
        }

        public void Update(float dt)
        {
            refresh -= dt;
            if (refresh <= 0) { refresh = 0.3f; Refresh(); }
            flash = Mathf.Max(0, flash - dt * 2);
            if (Root != null) Root.localScale = Vector3.one * (1 + Mathf.Sin(flash * Mathf.PI) * 0.05f);
        }
    }
}
