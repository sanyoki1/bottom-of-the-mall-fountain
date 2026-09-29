// The hazards as characters: Officer Doug (mall security: uniform, hat, badge, whistle) walking his
// loop around the plaza, and Chad the rival diver (wetsuit, mask, snorkel, flippers, a sack for
// your coins) wading through the fountain. Both speak through the same bubbles as shoppers.
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class HazardsView
    {
        Person guard, rival;
        Transform sack, binoculars;
        float raise;                        // 0 = arms down, 1 = binoculars at his eyes
        Sim sim;
        ViewContext ctx;
        string guardLine, rivalLine;
        public System.Action<Transform, string, bool, Rarity> Speak;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        public void Init(Transform parent, ViewContext c)
        {
            ctx = c;
            sim = c.Sim;
            guard = Actors.MakePerson(parent, C(0x2A3A6A), C(0x1E2440), C(0xD8A07A), C(0x1E2440), false, 1.12f);
            guard.Root.name = "Officer Doug";
            var gk = new MeshKit();
            gk.Cylinder(new Vector3(0, 0.12f, 0), 0.19f, 0.08f, 12, C(0x1E2440));
            gk.Cylinder(new Vector3(0, 0.07f, 0.06f), 0.2f, 0.02f, 12, C(0x1A1A1A));
            gk.Box(new Vector3(0, 0.13f, 0.18f), new Vector3(0.06f, 0.05f, 0.01f), C(0xE8B83A));
            gk.Build("Cap", guard.Head, false);
            var bk = new MeshKit();
            bk.Ellipsoid(new Vector3(0, 0.88f, 0.1f), new Vector3(0.24f, 0.22f, 0.2f), 5, 10, C(0x2A3A6A));
            bk.Box(new Vector3(0.1f, 1.1f, 0.24f), new Vector3(0.06f, 0.07f, 0.01f), C(0xE8B83A));
            bk.Box(new Vector3(0, 0.66f, 0), new Vector3(0.48f, 0.06f, 0.3f), C(0x1A1A1A));
            bk.Box(new Vector3(-0.2f, 0.62f, 0.12f), new Vector3(0.08f, 0.14f, 0.06f), C(0x2A2A2A));
            bk.Build("Uniform", guard.Body, false);
            var wk = new MeshKit();
            wk.Box(new Vector3(0, -0.02f, 0.05f), new Vector3(0.03f, 0.03f, 0.08f), C(0xC8CCD2));
            wk.Build("Whistle", guard.Hand, false);
            // binoculars at his eyes while he looks (the statue check); glinting lenses so you see them from the water
            var nk = new MeshKit();
            var along = Quaternion.Euler(90, 0, 0);
            for (int side = -1; side <= 1; side += 2)
            {
                nk.Cylinder(new Vector3(side * 0.045f, 0.03f, 0.2f), along, 0.034f, 0.12f, 10, C(0x1A1A1A));
                nk.Cylinder(new Vector3(side * 0.045f, 0.03f, 0.262f), along, 0.026f, 0.006f, 10, C(0x9CD8FF, 0.9f));
            }
            nk.Box(new Vector3(0, 0.03f, 0.2f), new Vector3(0.05f, 0.03f, 0.06f), C(0x2A2A2A));
            binoculars = nk.Build("Binoculars", guard.Head, false).transform;
            binoculars.gameObject.SetActive(false);

            rival = Actors.MakePerson(parent, C(0x1A1A1E), C(0x1A1A1E), C(0xE0B08A), C(0x1A1A1E), false, 1.0f);
            rival.Root.name = "Chad the Rival Diver";
            var mk = new MeshKit();
            mk.Box(new Vector3(0, 0.03f, 0.15f), new Vector3(0.26f, 0.12f, 0.06f), new Color(0.55f, 0.85f, 1f, 0.35f));
            mk.Box(new Vector3(0, 0.03f, 0.12f), new Vector3(0.3f, 0.14f, 0.03f), C(0xFFD000));
            mk.Tube(new Vector3(0.14f, 0.0f, 0.05f), new Vector3(0.16f, 0.3f, 0.02f), 0.02f, 5, C(0xFF7A1A));
            mk.Build("Mask", rival.Head, false);
            var fk = new MeshKit();
            fk.Box(new Vector3(0, -0.58f, 0.12f), new Vector3(0.14f, 0.02f, 0.34f), C(0xFFD000));
            fk.Build("Flipper", rival.LegL, false);
            fk.Build("Flipper", rival.LegR, false);
            var sk = new MeshKit();
            sk.Ellipsoid(new Vector3(0, -0.1f, 0), new Vector3(0.14f, 0.18f, 0.12f), 5, 8, C(0xB89A5A));
            sk.Box(new Vector3(0, -0.08f, 0.115f), new Vector3(0.05f, 0.08f, 0.01f), C(0x2F7F3F));
            sack = sk.Build("Sack", rival.Hand, false).transform;
            var col = rival.Root.gameObject.AddComponent<CapsuleCollider>();
            col.center = new Vector3(0, 0.9f, 0);
            col.height = 1.8f;
            col.radius = 0.4f;
            col.isTrigger = true;
            rival.Root.gameObject.layer = WishView.Layer;
            var ct = rival.Root.gameObject.AddComponent<ClickTarget>();
            ct.Kind = "rival";
            rival.Root.gameObject.SetActive(false);
        }

        public Transform RivalHead => rival.Head;
        public Transform GuardHead => guard.Head;

        public void Update(float dt, float time)
        {
            var g = sim.Guard;
            guard.Root.position = new Vector3(g.X, 0, g.Z);
            guard.Root.rotation = Quaternion.Slerp(guard.Root.rotation, Quaternion.Euler(0, g.Heading, 0), dt * 6);
            Actors.Animate(guard, time, Mathf.Clamp01(g.Speed / 1.2f), 25);
            // the statue check: binoculars up during the tell, held at his eyes while he looks
            raise = Mathf.MoveTowards(raise, g.Watching ? 1 : 0, dt / (g.Watching ? Balance.GuardTell * 0.8f : 0.35f));
            if (binoculars.gameObject.activeSelf != raise > 0.6f) binoculars.gameObject.SetActive(raise > 0.6f);
            if (raise > 0.001f)
            {
                float k = Mathf.SmoothStep(0, 1, raise);
                guard.ArmR.localRotation = Quaternion.Slerp(guard.ArmR.localRotation, Quaternion.Euler(-128, 0, -28), k);
                guard.ArmL.localRotation = Quaternion.Slerp(guard.ArmL.localRotation, Quaternion.Euler(-128, 0, 28), k);
            }
            // a slow scan across the water while he looks (Animate never turns the head, so ease it back after)
            var headAim = g.State == GuardState.Looking ? Quaternion.Euler(0, Mathf.Sin(time * 1.3f) * 12, 0) : Quaternion.identity;
            guard.Head.localRotation = Quaternion.Slerp(guard.Head.localRotation, headAim, 1 - Mathf.Exp(-dt * 6));
            if (g.State == GuardState.Busted)
            {
                // arm up, whistle to the mouth, finger pointing at you
                guard.ArmR.localRotation = Quaternion.Euler(-150, 0, -10);
                guard.ArmL.localRotation = Quaternion.Euler(-80 + Mathf.Sin(time * 12) * 10, 0, 15);
            }
            if (g.Line != guardLine) { guardLine = g.Line; if (!string.IsNullOrEmpty(g.Line)) Speak?.Invoke(guard.Head, g.Line, false, Rarity.Common); }

            var r = sim.Rival;
            bool on = r.State != RivalState.Away;
            if (rival.Root.gameObject.activeSelf != on) rival.Root.gameObject.SetActive(on);
            if (on)
            {
                float inBasin = r.X * r.X + r.Z * r.Z < FountainView.RimInner * FountainView.RimInner ? 1 : 0;
                float y = inBasin > 0 ? ctx.Fountain.HeightAt(r.X, r.Z) : 0;
                rival.Root.position = new Vector3(r.X, y, r.Z);
                rival.Root.rotation = Quaternion.Slerp(rival.Root.rotation, Quaternion.Euler(0, r.Heading, 0), dt * 8);
                Actors.Animate(rival, time * (r.State == RivalState.Fleeing ? 1.6f : 1f), Mathf.Clamp01(r.Speed / 1.5f), r.State == RivalState.Fleeing ? 70 : 30);
                sack.localScale = Vector3.one * (0.6f + Mathf.Min(1.2f, r.Loot.Count * 0.05f));
                if (inBasin > 0 && Random.value < dt * 3) ctx.Fx.Ripple(new Vector3(r.X, ctx.Fountain.WaterY + 0.01f, r.Z), new Color(0.9f, 0.97f, 1f, 0.5f), 1.1f, 0.6f);
                if (r.Line != rivalLine) { rivalLine = r.Line; if (!string.IsNullOrEmpty(r.Line)) Speak?.Invoke(rival.Head, r.Line, false, Rarity.Common); }
            }
        }
    }
}
