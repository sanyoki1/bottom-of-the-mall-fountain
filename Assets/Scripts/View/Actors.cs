// Little low-poly characters: people (worker, pogo riders, mall walkers, robots), pigeons, the mall rat,
// and the tool models the worker carries.
using UnityEngine;

namespace WishExtractor.View
{
    public sealed class Person
    {
        public Transform Root, Body, Head, ArmL, ArmR, LegL, LegR, Hand;
    }

    public sealed class Pigeon
    {
        public Transform Root, Head;
        public float Phase;
    }

    public static class Actors
    {
        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);
        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        public static Person MakePerson(Transform parent, Color shirt, Color pants, Color skin, Color hat, bool hardHat, float scale = 1f, bool headband = false)
        {
            var p = new Person();
            p.Root = new GameObject("Person").transform;
            p.Root.SetParent(parent, false);
            p.Root.localScale = Vector3.one * scale;

            var body = new MeshKit();
            body.Capsule(new Vector3(0, 0.95f, 0), 0.23f, 0.78f, shirt);
            body.Box(new Vector3(0, 0.62f, 0), new Vector3(0.4f, 0.16f, 0.26f), pants);
            p.Body = body.Build("Body", p.Root).transform;

            var head = new MeshKit();
            head.Sphere(Vector3.zero, 0.17f, 6, 10, skin);
            head.Box(new Vector3(0.07f, 0.03f, 0.15f), new Vector3(0.03f, 0.04f, 0.02f), C(0x222222));
            head.Box(new Vector3(-0.07f, 0.03f, 0.15f), new Vector3(0.03f, 0.04f, 0.02f), C(0x222222));
            if (hardHat)
            {
                head.Ellipsoid(new Vector3(0, 0.08f, 0), new Vector3(0.2f, 0.15f, 0.2f), 5, 10, hat);
                head.Cylinder(new Vector3(0, 0.07f, 0.02f), 0.24f, 0.025f, 12, hat);
            }
            else if (headband)
            {
                head.Torus(new Vector3(0, 0.07f, 0), 0.17f, 0.03f, 12, 4, hat);
                head.Ellipsoid(new Vector3(0, 0.12f, -0.02f), new Vector3(0.17f, 0.1f, 0.17f), 4, 10, C(0xE8E8E8));
            }
            else head.Ellipsoid(new Vector3(0, 0.07f, -0.02f), new Vector3(0.18f, 0.13f, 0.18f), 4, 10, hat);
            p.Head = head.Build("Head", p.Root).transform;
            p.Head.localPosition = new Vector3(0, 1.5f, 0);

            p.ArmL = Limb(p.Root, new Vector3(-0.29f, 1.24f, 0), 0.065f, 0.52f, shirt, skin, "ArmL");
            p.ArmR = Limb(p.Root, new Vector3(0.29f, 1.24f, 0), 0.065f, 0.52f, shirt, skin, "ArmR");
            p.LegL = Limb(p.Root, new Vector3(-0.11f, 0.58f, 0), 0.085f, 0.56f, pants, C(0x2A2A2A), "LegL");
            p.LegR = Limb(p.Root, new Vector3(0.11f, 0.58f, 0), 0.085f, 0.56f, pants, C(0x2A2A2A), "LegR");
            p.Hand = new GameObject("Hand").transform;
            p.Hand.SetParent(p.ArmR, false);
            p.Hand.localPosition = new Vector3(0, -0.52f, 0.02f);
            return p;
        }

        static Transform Limb(Transform parent, Vector3 pivot, float r, float len, Color c, Color end, string name)
        {
            var k = new MeshKit();
            k.Capsule(new Vector3(0, -len * 0.5f, 0), r, len, c, 6);
            k.Sphere(new Vector3(0, -len, 0), r * 1.1f, 4, 6, end);
            var t = k.Build(name, parent).transform;
            t.localPosition = pivot;
            return t;
        }

        /// <summary>Walk / idle cycle. speed 0 = idle breathing.</summary>
        public static void Animate(Person p, float time, float speed, float armSwing = 35f)
        {
            float s = Mathf.Sin(time * 9f) * Mathf.Clamp01(speed);
            p.LegL.localRotation = Quaternion.Euler(s * 30, 0, 0);
            p.LegR.localRotation = Quaternion.Euler(-s * 30, 0, 0);
            p.ArmL.localRotation = Quaternion.Euler(-s * armSwing, 0, 4);
            p.ArmR.localRotation = Quaternion.Euler(s * armSwing, 0, -4);
            float bob = Mathf.Abs(Mathf.Sin(time * 9f)) * 0.05f * Mathf.Clamp01(speed) + Mathf.Sin(time * 2f) * 0.01f;
            p.Body.localPosition = new Vector3(0, bob, 0);
            p.Head.localPosition = new Vector3(0, 1.5f + bob, 0);
        }

        public static Pigeon MakePigeon(Transform parent, float scale, System.Random rng)
        {
            var pg = new Pigeon { Phase = (float)rng.NextDouble() * 10 };
            pg.Root = new GameObject("Pigeon").transform;
            pg.Root.SetParent(parent, false);
            pg.Root.localScale = Vector3.one * scale;
            var k = new MeshKit();
            Color grey = Color.Lerp(C(0x8A8F9A), C(0x6E6A78), (float)rng.NextDouble());
            k.Ellipsoid(new Vector3(0, 0.16f, 0), new Vector3(0.13f, 0.12f, 0.2f), 5, 10, grey);
            k.Ellipsoid(new Vector3(0, 0.17f, -0.03f), new Vector3(0.14f, 0.08f, 0.16f), 4, 8, grey * 0.85f);
            k.Box(new Vector3(0, 0.15f, -0.24f), new Vector3(0.12f, 0.03f, 0.12f), C(0x3A3A44));
            k.Tube(new Vector3(0.04f, 0.06f, 0.02f), new Vector3(0.04f, 0f, 0.03f), 0.01f, 4, C(0xE8705A));
            k.Tube(new Vector3(-0.04f, 0.06f, 0.02f), new Vector3(-0.04f, 0f, 0.03f), 0.01f, 4, C(0xE8705A));
            k.Build("Body", pg.Root);
            var h = new MeshKit();
            h.Sphere(Vector3.zero, 0.075f, 5, 8, grey * 0.9f);
            h.Torus(new Vector3(0, -0.05f, 0), 0.06f, 0.02f, 8, 3, C(0x3FA88A));
            h.Push(new Vector3(0, -0.005f, 0.06f), Quaternion.Euler(90, 0, 0));
            h.Cone(Vector3.zero, 0.02f, 0.06f, 5, C(0xE8A05A));
            h.Pop();
            h.Box(new Vector3(0.05f, 0.02f, 0.03f), new Vector3(0.015f, 0.02f, 0.02f), C(0xFF7A30));
            h.Box(new Vector3(-0.05f, 0.02f, 0.03f), new Vector3(0.015f, 0.02f, 0.02f), C(0xFF7A30));
            pg.Head = h.Build("Head", pg.Root).transform;
            pg.Head.localPosition = new Vector3(0, 0.3f, 0.16f);
            return pg;
        }

        public static void AnimatePigeon(Pigeon pg, float time, float activity)
        {
            float t = time * (1.2f + activity * 2.5f) + pg.Phase;
            float peck = Mathf.Max(0, Mathf.Sin(t * 3f)) * activity;
            pg.Head.localPosition = new Vector3(0, 0.3f - peck * 0.14f, 0.16f + peck * 0.06f);
            pg.Head.localRotation = Quaternion.Euler(peck * 50, Mathf.Sin(t * 0.7f) * 25, 0);
        }

        public static Transform MakeRat(Transform parent)
        {
            var root = new GameObject("Mall Rat").transform;
            root.SetParent(parent, false);
            var k = new MeshKit();
            Color fur = C(0x7A7068), pink = C(0xF2A0B0);
            k.Ellipsoid(new Vector3(0, 0.22f, 0), new Vector3(0.2f, 0.16f, 0.34f), 6, 10, fur);
            k.Ellipsoid(new Vector3(0, 0.28f, 0.34f), new Vector3(0.13f, 0.12f, 0.17f), 5, 8, fur);
            k.Ellipsoid(new Vector3(0.09f, 0.4f, 0.3f), new Vector3(0.07f, 0.08f, 0.02f), 4, 6, pink);
            k.Ellipsoid(new Vector3(-0.09f, 0.4f, 0.3f), new Vector3(0.07f, 0.08f, 0.02f), 4, 6, pink);
            k.Sphere(new Vector3(0, 0.27f, 0.5f), 0.03f, 3, 5, pink);
            k.Box(new Vector3(0.06f, 0.32f, 0.44f), new Vector3(0.025f, 0.025f, 0.02f), C(0x111111));
            k.Box(new Vector3(-0.06f, 0.32f, 0.44f), new Vector3(0.025f, 0.025f, 0.02f), C(0x111111));
            k.Tube(new Vector3(0, 0.2f, -0.32f), new Vector3(0, 0.12f, -0.7f), 0.02f, 4, pink);
            k.Tube(new Vector3(0, 0.12f, -0.7f), new Vector3(0.1f, 0.2f, -0.95f), 0.015f, 4, pink);
            // stolen loot: a shiny gold coin held in the mouth
            k.Glossy(true);
            k.Push(new Vector3(0, 0.24f, 0.55f), Quaternion.Euler(90, 0, 0));
            k.Cylinder(Vector3.zero, 0.1f, 0.02f, 12, C(0xF2C14E, 0.5f));
            k.Pop();
            k.Build("Body", root);
            root.localScale = Vector3.one * 1.6f;
            return root;
        }

        /// <summary>Tool mesh held by the worker; index follows Content.Tools.</summary>
        public static GameObject MakeTool(int index, Transform hand)
        {
            var k = new MeshKit();
            Color wood = C(0x8A5A34), steel = C(0xC8CCD2), dark = C(0x333333);
            switch (index)
            {
                case 0:
                    k.Tube(new Vector3(0, 0, 0), new Vector3(0, -0.7f, 0.1f), 0.008f, 4, C(0xEEEEEE));
                    k.Ellipsoid(new Vector3(0, -0.75f, 0.1f), new Vector3(0.07f, 0.06f, 0.07f), 5, 8, C(0xF28DB2));
                    break;
                case 1:
                    k.Box(new Vector3(0, -0.05f, 0.05f), new Vector3(0.05f, 0.12f, 0.05f), wood);
                    k.Box(new Vector3(0, -0.1f, 0.28f), new Vector3(0.015f, 0.05f, 0.34f), steel);
                    break;
                case 2:
                    k.Tube(new Vector3(0, 0, 0), new Vector3(0, 0, 0.55f), 0.025f, 6, C(0xFFD000));
                    k.Box(new Vector3(0, -0.02f, 0.68f), new Vector3(0.22f, 0.03f, 0.26f), C(0xFFB000));
                    break;
                case 3:
                    k.Tube(new Vector3(0, 0, -0.05f), new Vector3(0, 0, 0.18f), 0.03f, 6, C(0x2F8F4F));
                    k.Box(new Vector3(0, -0.01f, 0.32f), new Vector3(0.1f, 0.015f, 0.26f), steel);
                    break;
                case 4:
                    k.Tube(new Vector3(0, 0, -0.1f), new Vector3(0, 0, 0.8f), 0.03f, 6, C(0xD8283A));
                    k.Tube(new Vector3(0, 0, 0.8f), new Vector3(0, -0.12f, 0.92f), 0.03f, 6, C(0xD8283A));
                    break;
                case 5:
                    k.Cylinder(new Vector3(-0.35f, 0.1f, -0.2f), 0.2f, 0.45f, 12, C(0xF2C230));
                    k.Tube(new Vector3(-0.3f, 0.3f, -0.1f), new Vector3(0, 0, 0.3f), 0.04f, 6, dark);
                    k.Tube(new Vector3(0, 0, 0.3f), new Vector3(0, -0.2f, 0.8f), 0.05f, 6, dark);
                    break;
                case 6:
                    k.Box(new Vector3(0, 0.02f, 0.05f), new Vector3(0.22f, 0.24f, 0.36f), C(0xFF7A1A));
                    k.Tube(new Vector3(0, -0.02f, 0.2f), new Vector3(0, -0.3f, 0.95f), 0.07f, 8, C(0xFF9A3A));
                    break;
                case 7:
                    k.Box(new Vector3(0, 0.1f, 0.1f), new Vector3(0.18f, 0.3f, 0.18f), C(0xF2C230));
                    k.Tube(new Vector3(0, -0.05f, 0.1f), new Vector3(0, -0.6f, 0.15f), 0.035f, 6, steel);
                    k.Box(new Vector3(0, 0.3f, 0.1f), new Vector3(0.4f, 0.04f, 0.05f), dark);
                    break;
                case 8:
                    k.Box(new Vector3(0, 0, 0.1f), new Vector3(0.12f, 0.14f, 0.35f), C(0x3A3F48));
                    k.Tube(new Vector3(0, 0, 0.28f), new Vector3(0, -0.05f, 0.6f), 0.03f, 6, steel);
                    k.Sphere(new Vector3(0, -0.06f, 0.63f), 0.05f, 4, 6, C(0x5AD8FF, 1f));
                    break;
                case 9:
                    k.Glossy(true);
                    k.Sphere(new Vector3(0, -0.02f, 0.05f), 0.14f, 6, 10, C(0xE8B83A, 0.2f));
                    for (int i = 0; i < 4; i++) k.Tube(new Vector3(-0.08f + i * 0.055f, 0.04f, 0.12f), new Vector3(-0.09f + i * 0.06f, 0.06f, 0.3f), 0.025f, 5, C(0xE8B83A, 0.2f));
                    k.Sphere(new Vector3(0, 0.05f, 0.14f), 0.04f, 4, 6, C(0xB57CFF, 1f));
                    break;
                default:
                    k.Glossy(true);
                    k.Tube(new Vector3(0, 0, -0.4f), new Vector3(0, 0, 1.0f), 0.03f, 6, C(0xE8B83A, 0.15f));
                    k.Box(new Vector3(0, 0, 1.0f), new Vector3(0.36f, 0.04f, 0.04f), C(0xE8B83A, 0.15f));
                    for (int i = -1; i <= 1; i++) k.Cone(new Vector3(i * 0.17f, 0, 1.0f), 0.03f, 0.25f, 5, C(0x5FE8F0, 0.8f));
                    break;
            }
            var go = k.Build("Tool", hand, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.Euler(80, 0, 0);
            return go;
        }
    }
}
