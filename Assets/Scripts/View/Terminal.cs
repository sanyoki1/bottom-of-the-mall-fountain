// The Maintenance Terminal: a 1995 laptop on a folding table, a cold coffee, an extension cord
// and a sticky note with the password on it. Look at it and press E to open the tech tree.
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class TerminalProp
    {
        public Transform Root { get; private set; }
        TextMesh screen;
        float blink;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        public void Build(Transform parent)
        {
            Root = new GameObject("Maintenance Terminal").transform;
            Root.SetParent(parent, false);
            Root.position = new Vector3(Layout.TerminalX, 0, Layout.TerminalZ);
            Vector3 look = new Vector3(0, 0, -11) - Root.position;
            Root.rotation = Quaternion.LookRotation(look.normalized);

            var k = new MeshKit();
            Color top = C(0xD8D2C0), leg = C(0x5A5E64), plastic = C(0x2A2C30), beige = C(0xC8BFA8);
            // folding table (front edge is +z, toward the player)
            k.Box(new Vector3(0, 0.74f, 0), new Vector3(1.5f, 0.04f, 0.76f), top);
            k.Box(new Vector3(0, 0.71f, 0.37f), new Vector3(1.5f, 0.04f, 0.02f), leg);
            foreach (float x in new[] { -0.68f, 0.68f })
            {
                k.Tube(new Vector3(x, 0.72f, 0.3f), new Vector3(x, 0, 0.12f), 0.018f, 6, leg);
                k.Tube(new Vector3(x, 0.72f, -0.3f), new Vector3(x, 0, -0.12f), 0.018f, 6, leg);
            }
            // chunky laptop: base + open lid
            k.Box(new Vector3(0, 0.785f, 0.05f), new Vector3(0.5f, 0.05f, 0.36f), beige);
            k.Box(new Vector3(0, 0.812f, 0.08f), new Vector3(0.42f, 0.004f, 0.18f), C(0x3A3A3A));
            k.Push(new Vector3(0, 0.81f, -0.13f), Quaternion.Euler(-12, 0, 0));
            k.Box(new Vector3(0, 0.18f, 0), new Vector3(0.5f, 0.36f, 0.03f), beige);
            k.Box(new Vector3(0, 0.185f, 0.016f), new Vector3(0.42f, 0.3f, 0.004f), C(0x0B2616, 0.4f));
            k.Pop();
            // coffee mug, sticky note, extension cord, a spare coin roll
            k.Cylinder(new Vector3(0.52f, 0.81f, 0.12f), 0.045f, 0.1f, 12, C(0xE8453A));
            k.Cylinder(new Vector3(0.52f, 0.855f, 0.12f), 0.038f, 0.01f, 12, C(0x3A2210));
            k.Box(new Vector3(-0.36f, 0.763f, 0.16f), new Vector3(0.12f, 0.004f, 0.12f), C(0xFFE45A));
            k.Box(new Vector3(-0.58f, 0.78f, -0.18f), new Vector3(0.16f, 0.05f, 0.08f), C(0xE8E4DA));
            k.Tube(new Vector3(-0.58f, 0.76f, -0.22f), new Vector3(-0.72f, 0.02f, -0.5f), 0.012f, 5, C(0xE8A020));
            k.Tube(new Vector3(-0.72f, 0.02f, -0.5f), new Vector3(-1.4f, 0.01f, -1.2f), 0.012f, 5, C(0xE8A020));
            k.Build("Table", Root);
            // a hand-lettered sign taped to the table front
            var sk = new MeshKit();
            sk.Box(new Vector3(0, 0.55f, 0.39f), new Vector3(0.7f, 0.22f, 0.005f), C(0xF4F0E6));
            sk.Build("Sign", Root, false);

            var face = Quaternion.Euler(0, 180, 0);
            WorldBuilder.MakeText(Root, "MAINTENANCE\nTERMINAL", new Vector3(0, 0.55f, 0.395f), face, 0.05f, C(0x1D1D1F), 0.75f, WorldBuilder.SignFontAlt);
            WorldBuilder.MakeText(Root, "pw: fountain1", new Vector3(-0.36f, 0.767f, 0.16f), Quaternion.Euler(90, 180, 0), 0.016f, C(0x1D1D1F), 0.7f);
            var lidRot = Quaternion.Euler(-12, 180, 0);
            Vector3 lid = new Vector3(0, 0.81f, -0.13f) + Quaternion.Euler(-12, 0, 0) * new Vector3(0, 0.2f, 0.02f);
            screen = WorldBuilder.MakeText(Root, "MAINT-OS 95\n> READY_", lid, lidRot, 0.036f, C(0x6CFF9A), 2.0f, WorldBuilder.SignFont);

            var col = Root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 0.6f, 0);
            col.size = new Vector3(1.6f, 1.3f, 0.85f);
            var ct = Root.gameObject.AddComponent<ClickTarget>();
            ct.Kind = "terminal";
        }

        public void Update(float dt, Sim sim)
        {
            blink += dt;
            if (screen == null) return;
            bool cursor = ((int)(blink * 2)) % 2 == 0;
            bool anything = false;
            for (int i = 0; i < Content.Techs.Length && !anything; i++) anything = sim.CanBuyTech(i);
            screen.text = "MAINT-OS 95\n" + (anything ? "> UPGRADES READY" : "> READY") + (cursor ? "_" : " ");
        }
    }
}
