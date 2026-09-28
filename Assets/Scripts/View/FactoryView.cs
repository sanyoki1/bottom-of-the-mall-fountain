// Draws the factory from Sim.Buildings: a procedural model per machine (with a collider tagged
// "building" so you can aim at it), conveyor belts with a scrolling surface, the items riding on
// them (instanced through ItemRenderer), and the moving parts: the skimmer bot paddling around
// the fountain, the pump's hose, the claw crane's arm.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class FactoryView
    {
        sealed class Node
        {
            public Building B;
            public Transform T, Spin, Bot, Arm, Claw, Light;
            public int Rot = -1;
            public int PickSerial;
            public float Puff;
            public readonly List<Pigeon> Pigeons = new List<Pigeon>();
        }

        readonly Dictionary<int, Node> nodes = new Dictionary<int, Node>();
        readonly List<int> gone = new List<int>();
        Transform root;
        Sim sim;
        ViewContext ctx;
        static Mesh beltFrame, beltSurface;
        Material beltMat;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);
        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        public void Init(Transform parent, ViewContext c)
        {
            ctx = c;
            sim = c.Sim;
            root = new GameObject("Factory").transform;
            root.SetParent(parent, false);
            beltMat = new Material(Mats.Belt);
        }

        public void Clear()
        {
            foreach (var n in nodes.Values) if (n.T != null) Object.Destroy(n.T.gameObject);
            nodes.Clear();
        }

        public static Vector3 WorldCenter(Building b)
        {
            var (x, z) = b.Center;
            return new Vector3(x, 0, z);
        }

        // ───────────────────────────── models ─────────────────────────────

        static void EnsureBeltMeshes()
        {
            if (beltFrame != null) return;
            var k = new MeshKit();
            Color steel = C(0x8A8F96), dark = C(0x2A2C30), yellow = C(0xF2B233);
            k.Box(new Vector3(0, 0.08f, 0), new Vector3(0.96f, 0.16f, 1f), dark);
            k.Box(new Vector3(-0.46f, 0.2f, 0), new Vector3(0.06f, 0.1f, 1f), steel);
            k.Box(new Vector3(0.46f, 0.2f, 0), new Vector3(0.06f, 0.1f, 1f), steel);
            k.Box(new Vector3(-0.46f, 0.255f, 0), new Vector3(0.065f, 0.01f, 1f), yellow);
            k.Box(new Vector3(0.46f, 0.255f, 0), new Vector3(0.065f, 0.01f, 1f), yellow);
            // an arrow on the side rails so you can read the direction
            k.Box(new Vector3(0.5f, 0.2f, 0.25f), new Vector3(0.02f, 0.04f, 0.2f), C(0xFFFFFF, 0.4f));
            k.Box(new Vector3(-0.5f, 0.2f, 0.25f), new Vector3(0.02f, 0.04f, 0.2f), C(0xFFFFFF, 0.4f));
            beltFrame = k.ToMesh("belt frame");
            beltSurface = new Mesh { name = "belt surface" };
            beltSurface.vertices = new[] { new Vector3(-0.42f, 0.17f, -0.5f), new Vector3(-0.42f, 0.17f, 0.5f), new Vector3(0.42f, 0.17f, 0.5f), new Vector3(0.42f, 0.17f, -0.5f) };
            // u runs backward along the belt, so the WE/Scroll shader's +u scroll reads as forward motion
            beltSurface.uv = new[] { new Vector2(1, 0), new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1) };
            beltSurface.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            beltSurface.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            beltSurface.RecalculateNormals();
            beltSurface.RecalculateBounds();
        }

        /// <summary>Model for a buildable, centred on its footprint, facing local +z. Used for real buildings and the build ghost.</summary>
        public static GameObject MakeModel(BuildDef d, Transform parent, Material beltMaterial = null)
        {
            var go = new GameObject(d.Name);
            go.transform.SetParent(parent, false);
            var t = go.transform;
            var k = new MeshKit();
            Color main = C(d.Color), dark = C(0x2A2C30), steel = C(0xB8BEC4);
            float w = d.W, dd = d.D;
            switch (d.Id)
            {
                case "belt":
                {
                    EnsureBeltMeshes();
                    var f = new GameObject("Frame");
                    f.transform.SetParent(t, false);
                    f.AddComponent<MeshFilter>().sharedMesh = beltFrame;
                    f.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
                    var s = new GameObject("Surface");
                    s.transform.SetParent(t, false);
                    s.AddComponent<MeshFilter>().sharedMesh = beltSurface;
                    var mr = s.AddComponent<MeshRenderer>();
                    mr.sharedMaterial = beltMaterial != null ? beltMaterial : Mats.Belt;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    return go;
                }
                case "splitter":
                    k.Box(new Vector3(0, 0.2f, 0), new Vector3(0.96f, 0.4f, 0.96f), main);
                    k.Box(new Vector3(0, 0.41f, 0.25f), new Vector3(0.1f, 0.02f, 0.4f), C(0xFFFFFF, 0.6f));
                    k.Box(new Vector3(0.25f, 0.41f, 0), new Vector3(0.4f, 0.02f, 0.1f), C(0xFFFFFF, 0.6f));
                    k.Box(new Vector3(-0.25f, 0.41f, 0), new Vector3(0.4f, 0.02f, 0.1f), C(0xFFFFFF, 0.6f));
                    break;
                case "gen_hamster":
                {
                    k.Box(new Vector3(0, 0.06f, 0), new Vector3(0.9f, 0.12f, 0.9f), C(0x8A5A34));
                    k.Box(new Vector3(-0.34f, 0.5f, 0), new Vector3(0.06f, 0.8f, 0.08f), steel);
                    k.Box(new Vector3(0.34f, 0.5f, 0), new Vector3(0.06f, 0.8f, 0.08f), steel);
                    k.Box(new Vector3(0.3f, 0.18f, 0.35f), new Vector3(0.2f, 0.12f, 0.15f), C(0xE8C020));
                    k.Box(new Vector3(0.3f, 0.25f, 0.43f), new Vector3(0.06f, 0.03f, 0.01f), C(0x6CFF9A, 1f));
                    k.Build("Stand", t);
                    var wheel = new MeshKit();
                    wheel.Push(Vector3.zero, Quaternion.Euler(0, 0, 90));
                    wheel.Torus(new Vector3(0, -0.28f, 0), 0.4f, 0.025f, 24, 4, C(0x39E5D0));
                    wheel.Torus(new Vector3(0, 0.28f, 0), 0.4f, 0.025f, 24, 4, C(0x39E5D0));
                    for (int i = 0; i < 16; i++)
                    {
                        float a = i * Mathf.PI * 2 / 16;
                        wheel.Box(new Vector3(Mathf.Cos(a) * 0.4f, 0, Mathf.Sin(a) * 0.4f), new Vector3(0.02f, 0.58f, 0.02f), steel);
                    }
                    wheel.Pop();
                    var wg = wheel.Build("Wheel", t, false);
                    wg.transform.localPosition = new Vector3(0, 0.55f, 0);
                    var ham = new MeshKit();
                    ham.Ellipsoid(new Vector3(0, 0.2f, 0), new Vector3(0.09f, 0.08f, 0.13f), 5, 8, C(0xE0B080));
                    ham.Sphere(new Vector3(0, 0.23f, 0.12f), 0.06f, 4, 6, C(0xE0B080));
                    ham.Box(new Vector3(0.03f, 0.26f, 0.17f), new Vector3(0.015f, 0.015f, 0.01f), C(0x111111));
                    ham.Box(new Vector3(-0.03f, 0.26f, 0.17f), new Vector3(0.015f, 0.015f, 0.01f), C(0x111111));
                    var hg = ham.Build("Gerald", t, false);
                    hg.transform.localPosition = new Vector3(0, 0.15f, 0);
                    break;
                }
                case "gen_diesel":
                    k.Box(new Vector3(0, 0.1f, 0), new Vector3(1.9f, 0.2f, 1.9f), dark);
                    k.Box(new Vector3(0, 0.7f, 0), new Vector3(1.6f, 1.0f, 1.3f), main);
                    k.Box(new Vector3(0, 0.9f, 0.66f), new Vector3(0.6f, 0.4f, 0.04f), dark);
                    k.Box(new Vector3(-0.15f, 0.95f, 0.69f), new Vector3(0.1f, 0.06f, 0.02f), C(0x6CFF9A, 1f));
                    k.Box(new Vector3(0.1f, 0.95f, 0.69f), new Vector3(0.1f, 0.06f, 0.02f), C(0xFF4040, 1f));
                    k.Cylinder(new Vector3(0.55f, 1.5f, -0.35f), 0.08f, 0.8f, 10, steel);
                    for (int i = 0; i < 5; i++) k.Box(new Vector3(-0.8f, 0.5f + i * 0.08f, 0), new Vector3(0.02f, 0.04f, 1.1f), dark);
                    break;
                case "gen_fryer":
                    k.Box(new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1.0f, 1.6f), steel);
                    k.Box(new Vector3(0, 1.0f, 0), new Vector3(1.5f, 0.02f, 1.3f), C(0xE8A020, 0.5f));
                    k.Box(new Vector3(0.3f, 1.25f, 0), new Vector3(0.6f, 0.3f, 0.5f), C(0xC8CCD2));
                    k.Tube(new Vector3(0.3f, 1.4f, 0), new Vector3(0.3f, 1.8f, -0.4f), 0.02f, 5, dark);
                    k.Box(new Vector3(0, 0.6f, 0.81f), new Vector3(1.2f, 0.3f, 0.02f), C(0xE87A2A));
                    break;
                case "gen_solar":
                    k.Box(new Vector3(0, 0.5f, 0), new Vector3(1.6f, 1.0f, 1.6f), C(0xE8E4DA));
                    k.Box(new Vector3(0, 1.02f, 0), new Vector3(1.4f, 0.04f, 1.4f), main);
                    k.Cylinder(new Vector3(0, 9.5f, 0), 0.35f, 17f, 16, new Color(1f, 0.95f, 0.75f, 0.6f), false);
                    break;
                case "intake_skimmer":
                    k.Box(new Vector3(0, 0.12f, 0), new Vector3(0.9f, 0.24f, 1.9f), dark);
                    k.Box(new Vector3(0, 0.35f, -0.5f), new Vector3(0.7f, 0.3f, 0.6f), main);
                    k.Box(new Vector3(0, 0.52f, -0.5f), new Vector3(0.3f, 0.05f, 0.2f), C(0x6CFF9A, 0.9f));
                    k.Box(new Vector3(0, 0.26f, 0.6f), new Vector3(0.6f, 0.04f, 0.7f), steel);
                    break;
                case "intake_pump":
                    k.Box(new Vector3(0, 0.1f, 0), new Vector3(1.9f, 0.2f, 1.9f), dark);
                    k.Cylinder(new Vector3(0, 0.75f, -0.2f), 0.55f, 1.1f, 16, main);
                    k.Cylinder(new Vector3(0, 1.4f, -0.2f), 0.3f, 0.3f, 12, steel);
                    k.Box(new Vector3(0.6f, 0.5f, -0.6f), new Vector3(0.4f, 0.5f, 0.4f), C(0x3A3F48));
                    k.Box(new Vector3(0.6f, 0.6f, -0.39f), new Vector3(0.2f, 0.08f, 0.02f), C(0x6CFF9A, 1f));
                    break;
                case "intake_claw":
                    k.Box(new Vector3(0, 0.4f, -0.3f), new Vector3(1.4f, 0.8f, 1.2f), main);
                    k.Box(new Vector3(0, 1.4f, -0.3f), new Vector3(1.3f, 1.2f, 1.1f), new Color(0.8f, 0.95f, 1f, 0.15f));
                    k.Box(new Vector3(0, 2.1f, -0.3f), new Vector3(1.4f, 0.3f, 1.2f), C(0xFFD34D, 0.5f));
                    k.Box(new Vector3(0, 1.0f, -0.2f), new Vector3(1.1f, 0.3f, 0.9f), C(0xC77B43));
                    k.Box(new Vector3(0, 2.4f, 0.6f), new Vector3(0.2f, 0.2f, 1.2f), steel);
                    break;
                case "dig_rig":
                    k.Box(new Vector3(0, 0.15f, 0), new Vector3(1.9f, 0.3f, 1.9f), dark);
                    k.Box(new Vector3(0, 0.7f, -0.3f), new Vector3(1.2f, 0.8f, 1.0f), main);
                    k.Box(new Vector3(0, 1.2f, -0.3f), new Vector3(0.6f, 0.2f, 0.6f), C(0x3A3A3A));
                    for (int i = 0; i < 6; i++) k.Box(new Vector3(-0.55f + i * 0.22f, 0.32f, 0.92f), new Vector3(0.1f, 0.03f, 0.06f), i % 2 == 0 ? C(0x1A1A1A) : C(0xFFD000));
                    break;
                case "dig_borer":
                    k.Box(new Vector3(0, 0.2f, 0), new Vector3(1.9f, 0.4f, 2.9f), dark);
                    k.Cylinder(new Vector3(0, 1.0f, -0.2f), Quaternion.Euler(90, 0, 0), 0.75f, 2.0f, 18, main);
                    k.Box(new Vector3(0, 1.8f, -0.8f), new Vector3(0.9f, 0.5f, 0.9f), C(0xE8C020));
                    k.Box(new Vector3(0.2f, 1.9f, -0.34f), new Vector3(0.3f, 0.15f, 0.02f), C(0x6CFF9A, 1f));
                    break;
                case "proc_tumbler":
                    k.Box(new Vector3(0, 0.55f, 0), new Vector3(1.8f, 1.1f, 1.8f), C(0xE8E4DA));
                    k.Cylinder(new Vector3(0, 0.65f, 0.91f), Quaternion.Euler(90, 0, 0), 0.5f, 0.04f, 20, C(0x2A2C30));
                    k.Cylinder(new Vector3(0, 0.65f, 0.935f), Quaternion.Euler(90, 0, 0), 0.4f, 0.02f, 20, new Color(0.55f, 0.75f, 0.35f, 0.35f));
                    k.Box(new Vector3(0.6f, 1.15f, 0.5f), new Vector3(0.4f, 0.1f, 0.3f), main);
                    k.Box(new Vector3(-0.5f, 1.12f, -0.4f), new Vector3(0.3f, 0.06f, 0.3f), C(0xD8283A));
                    break;
                case "proc_pigeons":
                    k.Box(new Vector3(0, 0.3f, 0), new Vector3(1.8f, 0.6f, 1.8f), C(0x8A5A34));
                    k.Box(new Vector3(0, 0.61f, 0), new Vector3(1.6f, 0.02f, 1.6f), C(0xC8C0A8));
                    k.Tube(new Vector3(-0.8f, 0.6f, -0.8f), new Vector3(-0.8f, 1.6f, -0.8f), 0.04f, 6, steel);
                    k.Tube(new Vector3(0.8f, 0.6f, -0.8f), new Vector3(0.8f, 1.6f, -0.8f), 0.04f, 6, steel);
                    k.Tube(new Vector3(-0.8f, 1.6f, -0.8f), new Vector3(0.8f, 1.6f, -0.8f), 0.04f, 6, steel);
                    break;
                case "proc_sorter":
                    k.Box(new Vector3(0, 0.6f, 0), new Vector3(1.8f, 1.2f, 1.6f), main);
                    for (int i = 0; i < 5; i++) k.Box(new Vector3(-0.64f + i * 0.32f, 0.35f, 0.81f), new Vector3(0.24f, 0.4f, 0.02f), C(i % 2 == 0 ? 0xC77B43 : 0xD4D8DC));
                    k.Box(new Vector3(0, 1.0f, 0.81f), new Vector3(0.7f, 0.2f, 0.02f), C(0x6CFF9A, 0.8f));
                    k.Frustum(new Vector3(0, 1.4f, -0.3f), 0.2f, 0.5f, 0.4f, 12, steel);
                    break;
                case "proc_roller":
                case "proc_bagger":
                    k.Box(new Vector3(0, 0.5f, 0), new Vector3(1.8f, 1.0f, 1.8f), main);
                    k.Cylinder(new Vector3(0, 1.1f, 0), Quaternion.Euler(0, 0, 90), 0.25f, 1.4f, 14, C(0xF4F0E6));
                    k.Box(new Vector3(0, 0.6f, 0.91f), new Vector3(1.0f, 0.3f, 0.02f), dark);
                    break;
                case "proc_pallet":
                    k.Box(new Vector3(0, 0.2f, 0), new Vector3(2.8f, 0.4f, 2.8f), C(0x8A5A34));
                    k.Box(new Vector3(0, 1.3f, 0), new Vector3(0.2f, 2.2f, 0.2f), steel);
                    k.Box(new Vector3(0, 2.4f, 0), new Vector3(2.6f, 0.15f, 0.2f), steel);
                    k.Box(new Vector3(0, 0.9f, 0.3f), new Vector3(1.4f, 1.0f, 1.4f), new Color(0.85f, 0.9f, 0.95f, 0.1f));
                    break;
                case "proc_melter":
                    k.Frustum(new Vector3(0, 0.6f, 0), 0.9f, 0.7f, 1.2f, 16, C(0x3A3A3A));
                    k.Cylinder(new Vector3(0, 1.21f, 0), 0.6f, 0.02f, 16, C(0xFF8A20, 1f));
                    k.Box(new Vector3(0, 0.4f, 0.85f), new Vector3(0.5f, 0.3f, 0.2f), main);
                    break;
                case "proc_compressor":
                    k.Box(new Vector3(0, 0.3f, 0), new Vector3(1.8f, 0.6f, 1.8f), C(0x3A3F48));
                    k.Cylinder(new Vector3(0, 1.0f, 0), 0.55f, 0.9f, 18, main);
                    k.Box(new Vector3(0, 1.55f, 0), new Vector3(1.3f, 0.2f, 1.3f), C(0x2A2C30));
                    k.Sphere(new Vector3(0, 1.85f, 0), 0.25f, 6, 10, C(0xC8A0FF, 1f));
                    break;
                case "hopper":
                case "hopper2":
                {
                    Color body = main, trim = d.Id == "hopper" ? C(0xFFD34D) : C(0xD8283A);
                    k.Box(new Vector3(0, 0.45f, 0), new Vector3(1.6f, 0.9f, 1.6f), body);
                    k.Push(Vector3.zero, Quaternion.Euler(0, 45, 0));
                    k.Frustum(new Vector3(0, 1.2f, 0), 0.55f, 1.05f, 0.6f, 4, body, false, false);
                    k.Pop();
                    k.Box(new Vector3(0, 0.92f, 0), new Vector3(1.64f, 0.06f, 1.64f), trim);
                    k.Box(new Vector3(0, 0.6f, 0.81f), new Vector3(0.9f, 0.3f, 0.02f), dark);
                    k.Box(new Vector3(0, 0.6f, 0.82f), new Vector3(0.7f, 0.18f, 0.01f), C(0x6CFF9A, 0.8f));
                    if (d.Id == "hopper2")
                        for (int i = 0; i < 5; i++) k.Box(new Vector3(-0.64f + i * 0.32f, 0.45f, 0.815f), new Vector3(0.06f, 0.8f, 0.02f), C(0x3A3F48));
                    break;
                }
                default:
                    k.Box(new Vector3(0, 0.5f, 0), new Vector3(w * 0.9f, 1f, dd * 0.9f), main);
                    break;
            }
            if (k.VertexCount > 0) k.Build("Body", t);
            return go;
        }

        // ───────────────────────────── sync + animate ─────────────────────────────

        Node Make(Building b)
        {
            var n = new Node { B = b };
            var go = MakeModel(b.Def, root, b.Def.IsBelt ? beltMat : null);
            n.T = go.transform;
            n.Spin = n.T.Find("Wheel");
            var col = go.AddComponent<BoxCollider>();
            float h = b.Def.IsBelt ? 0.26f : b.Def.Id == "gen_solar" ? 1.1f : 1.4f;
            col.center = new Vector3(0, h / 2, 0);
            col.size = new Vector3(b.Def.W * 0.98f, h, b.Def.D * 0.98f);
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "building";
            ct.Uid = b.Uid;
            if (b.Def.Intake == "skimmer")
            {
                var bk = new MeshKit();
                bk.Cylinder(Vector3.zero, 0.32f, 0.12f, 18, C(0x2A2C30));
                bk.Torus(new Vector3(0, 0.02f, 0), 0.33f, 0.025f, 18, 4, C(0x39E5D0));
                bk.Box(new Vector3(0, 0.07f, 0.24f), new Vector3(0.12f, 0.03f, 0.03f), C(0x6CFF9A, 1f));
                bk.Sphere(new Vector3(0, 0.1f, 0), 0.08f, 4, 8, C(0x39E5D0));
                n.Bot = bk.Build("Skimmer Bot", root, false).transform;
            }
            if (b.Def.Id == "proc_pigeons")
            {
                var rng = new System.Random(b.Uid);
                for (int i = 0; i < 6; i++)
                {
                    var pg = Actors.MakePigeon(n.T, 1.2f, rng);
                    pg.Root.localPosition = new Vector3(-0.6f + (i % 3) * 0.6f, 0.62f, -0.4f + (i / 3) * 0.7f);
                    pg.Root.localRotation = Quaternion.Euler(0, rng.Next(360), 0);
                    n.Pigeons.Add(pg);
                }
            }
            if (b.Def.Intake == "dig")
            {
                n.Arm = new GameObject("Arm").transform;
                n.Arm.SetParent(root, false);
                var ak = new MeshKit();
                ak.Cylinder(new Vector3(0, 0.5f, 0), 0.12f, 1f, 10, C(0xE8C020));
                ak.Build("Segment", n.Arm, false);
                var ck = new MeshKit();
                if (b.Def.Id == "dig_rig")
                {
                    ck.Box(new Vector3(0, 0.35f, 0), new Vector3(0.3f, 0.4f, 0.3f), C(0xE8C020));
                    ck.Tube(new Vector3(0, 0.15f, 0), new Vector3(0, -0.35f, 0), 0.04f, 6, C(0xB8BEC4));
                }
                else
                {
                    ck.Cylinder(new Vector3(0, 0.3f, 0), 0.5f, 0.4f, 16, C(0x8A8F96));
                    ck.Push(new Vector3(0, 0.1f, 0), Quaternion.Euler(180, 0, 0));
                    ck.Cone(Vector3.zero, 0.5f, 0.5f, 16, C(0xB8BEC4));
                    ck.Pop();
                }
                n.Claw = ck.Build("Head", root, false).transform;
            }
            if (b.Def.Intake == "pump" || b.Def.Intake == "claw")
            {
                n.Arm = new GameObject("Arm").transform;
                n.Arm.SetParent(root, false);
                var ak = new MeshKit();
                ak.Cylinder(new Vector3(0, 0.5f, 0), 0.1f, 1f, 10, b.Def.Intake == "pump" ? C(0x3A3A3A) : C(0xB8BEC4));
                ak.Build("Segment", n.Arm, false);
                var ck = new MeshKit();
                if (b.Def.Intake == "pump") ck.Frustum(new Vector3(0, 0.1f, 0), 0.25f, 0.1f, 0.2f, 12, C(0x2E6FD6));
                else for (int i = 0; i < 3; i++)
                    {
                        ck.Push(Vector3.zero, Quaternion.Euler(0, i * 120, 0));
                        ck.Tube(new Vector3(0, 0.3f, 0), new Vector3(0, 0, 0.18f), 0.03f, 5, C(0xB8BEC4));
                        ck.Tube(new Vector3(0, 0, 0.18f), new Vector3(0, -0.15f, 0.1f), 0.025f, 5, C(0xB8BEC4));
                        ck.Pop();
                    }
                n.Claw = ck.Build("Head", root, false).transform;
            }
            return n;
        }

        public Transform BuildingTransform(int buildingUid) => nodes.TryGetValue(buildingUid, out var n) ? n.T : null;

        public void Update(float dt, float time)
        {
            beltMat.SetFloat("_Speed", 1.2f * (1 << sim.BeltTier));
            foreach (var b in sim.Buildings)
            {
                if (!nodes.TryGetValue(b.Uid, out var n)) { n = Make(b); nodes[b.Uid] = n; }
                if (n.Rot != b.Rot)
                {
                    n.Rot = b.Rot;
                    n.T.position = WorldCenter(b);
                    n.T.rotation = Quaternion.Euler(0, b.Rot * 90, 0);
                }
                float act = b.Activity;
                if (n.Spin != null) n.Spin.localRotation = Quaternion.Euler(time * 360 * (0.6f + act), 0, 0);
                if (b.Def.Id == "gen_diesel" || b.Def.Id == "gen_fryer")
                {
                    n.Puff -= dt;
                    if (n.Puff <= 0)
                    {
                        n.Puff = b.Def.Id == "gen_diesel" ? 0.35f : 0.8f;
                        var top = n.T.position + n.T.rotation * (b.Def.Id == "gen_diesel" ? new Vector3(0.55f, 1.95f, -0.35f) : new Vector3(0.3f, 1.85f, -0.4f));
                        ctx.Fx.Dust(top, b.Def.Id == "gen_diesel" ? new Color(0.3f, 0.3f, 0.32f) : new Color(0.9f, 0.85f, 0.7f), 1, 0.5f, 0.2f, 1.2f);
                    }
                    n.T.localScale = new Vector3(1, 1 + Mathf.Sin(time * 40) * 0.006f, 1);
                }
                if (n.Bot != null)
                {
                    float water = ctx.Fountain.WaterY;
                    var target = new Vector3(b.BotX, water - 0.04f, b.BotZ);
                    var prev = n.Bot.position;
                    n.Bot.position = target;
                    var dir = target - prev;
                    dir.y = 0;
                    if (dir.sqrMagnitude > 1e-6f) n.Bot.rotation = Quaternion.Slerp(n.Bot.rotation, Quaternion.LookRotation(dir), dt * 8);
                    if (b.Activity > 0.5f && Random.value < dt * 4) ctx.Fx.Ripple(target, new Color(0.9f, 0.97f, 1f, 0.5f), 0.9f, 0.6f);
                }
                if (n.Arm != null)
                {
                    // pump hose / crane arm from the machine's top over the rim to where it's working
                    var start = n.T.position + Vector3.up * (b.Def.Intake == "pump" ? 1.5f : b.Def.Intake == "dig" ? 1.3f : 2.4f);
                    Vector3 end;
                    if (b.Def.Intake == "pump")
                    {
                        var (sx, sz) = sim.SuctionPoint(b);
                        end = new Vector3(sx, ctx.Fountain.HeightAt(sx, sz) + 0.1f, sz);
                    }
                    else if (b.Def.Intake == "dig")
                    {
                        var (sx, sz) = sim.SuctionPoint(b);
                        float bounce = act > 0.5f ? Mathf.Abs(Mathf.Sin(time * (b.Def.Id == "dig_rig" ? 30 : 8))) * (b.Def.Id == "dig_rig" ? 0.12f : 0.05f) : 0;
                        end = new Vector3(sx, ctx.Fountain.HeightAt(sx, sz) + 0.35f + bounce, sz);
                        if (act > 0.5f && Random.value < dt * 6) ctx.Fx.Dust(end + Vector3.down * 0.3f, MeshKit.Hex(sim.CurStratum.Color), 2, 0.7f, 0.5f, 1f);
                        if (b.Def.Id == "dig_borer") n.Claw.rotation = Quaternion.Euler(0, time * 400, 0);
                    }
                    else
                    {
                        var goal = b.PickSerial > 0 ? new Vector3(b.LastPickX, ctx.Fountain.HeightAt(b.LastPickX, b.LastPickZ) + 0.3f, b.LastPickZ) : n.T.position + n.T.forward * 4 + Vector3.up;
                        end = n.Claw.position == Vector3.zero ? goal : Vector3.Lerp(n.Claw.position, goal, 1 - Mathf.Exp(-dt * 3));
                    }
                    var mid = (start + end) * 0.5f + Vector3.up * 1.2f;
                    // two straight segments via the arch point
                    PlaceSegment(n.Arm, start, mid, end);
                    n.Claw.position = end;
                    if (b.Def.Id != "dig_borer") n.Claw.rotation = Quaternion.identity;
                }
                foreach (var pg in n.Pigeons) Actors.AnimatePigeon(pg, time, act);
                if (b.Def.Id == "proc_tumbler" || b.Def.Id == "proc_roller" || b.Def.Id == "proc_bagger")
                    n.T.localScale = new Vector3(1, 1 + Mathf.Sin(time * 25) * 0.01f * act, 1);
                if (b.PickSerial != n.PickSerial)
                {
                    n.PickSerial = b.PickSerial;
                    var p = new Vector3(b.LastPickX, ctx.Fountain.WaterY, b.LastPickZ);
                    ctx.Fx.Sparks(p, new Color(0.85f, 0.95f, 1f), 5, 2f, 0.06f, 0.4f);
                    if (b.Def.Intake != "skimmer" && b.Def.Intake != "dig")
                        ctx.Fx.Fly(p, () => n.T != null ? n.T.position + Vector3.up * 1.2f : p, Loot.Tinted(ItemShape.Coin, new Color(0.78f, 0.48f, 0.26f)), 1f, 0.5f, 1.5f);
                }
            }
            gone.Clear();
            foreach (var kv in nodes) if (sim.FindBuilding(kv.Key) == null) gone.Add(kv.Key);
            foreach (int u in gone)
            {
                var n = nodes[u];
                if (n.T != null)
                {
                    ctx.Fx.Dust(n.T.position + Vector3.up * 0.4f, new Color(0.8f, 0.78f, 0.72f), 6, 1f, 0.8f, 1f);
                    Object.Destroy(n.T.gameObject);
                }
                if (n.Bot != null) Object.Destroy(n.Bot.gameObject);
                if (n.Arm != null) Object.Destroy(n.Arm.gameObject);
                if (n.Claw != null) Object.Destroy(n.Claw.gameObject);
                nodes.Remove(u);
            }
        }

        static void PlaceSegment(Transform arm, Vector3 a, Vector3 mid, Vector3 b)
        {
            // the arm has one child segment per leg; make a second leg on demand
            while (arm.childCount < 2) Object.Instantiate(arm.GetChild(0).gameObject, arm);
            Leg(arm.GetChild(0), a, mid);
            Leg(arm.GetChild(1), mid, b);
        }

        static void Leg(Transform seg, Vector3 a, Vector3 b)
        {
            var d = b - a;
            float len = Mathf.Max(0.01f, d.magnitude);
            seg.position = a;
            seg.rotation = Quaternion.FromToRotation(Vector3.up, d / len);
            seg.localScale = new Vector3(1, len, 1);
        }

        /// <summary>Queue every belt item for drawing (called while ItemRenderer builds its batches).</summary>
        public void DrawBeltItems(ItemRenderer items)
        {
            foreach (var b in sim.Buildings)
            {
                if (!b.Def.IsBelt || b.Items.Count == 0) continue;
                var c = WorldCenter(b);
                var f = new Vector3(b.Fx, 0, b.Fz);
                foreach (var it in b.Items)
                {
                    var p = c + f * (it.Pos - 0.5f) + Vector3.up * 0.19f;
                    items.AddExtra(it.Type, p, Quaternion.Euler(0, b.Rot * 90 + (it.Type * 37) % 60 - 30, 0));
                }
            }
        }
    }
}
