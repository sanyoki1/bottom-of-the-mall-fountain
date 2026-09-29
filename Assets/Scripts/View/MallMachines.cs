// Models and moving parts for the four mall-only machines (rules in Core/SimMallMachines.cs): Galleria
// Aurelia's Champagne Cork Cannon, Skyport's Baggage Claim Carousel and its cargo drones, the Lucky Lagoon's
// Slot-Machine Sorter and Eternity Plaza's Old Well. FactoryView builds the static model through Model (so
// the build ghost gets it too) and calls Animate every frame for the moving parts.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class MallMachines
    {
        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);
        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        const float BarrelPitch = -32f;               // the cannon aims up and out over the water
        static readonly Vector3 BarrelPos = new Vector3(0, 0.98f, -0.2f);
        const float ReelR = 0.22f;
        // the eight symbols round each reel, starting at the window: 7, cherries, BAR, lemon, 7, cherries, bell, BAR
        static readonly uint[] ReelSymbols = { 0xFFC83A, 0xD8283A, 0x1A1A1E, 0xFFE040, 0xFFC83A, 0xD8283A, 0xE8B83A, 0x1A1A1E };
        static readonly int[] ResultSymbol = { 3, 1, 2, 0, 0 };   // SlotResult → symbol shown on the middle reel

        public static bool Handles(BuildDef d) => d.Intake == "cannon" || d.Intake == "well" || d.IsCarousel || d.Process == "slots";

        // ───────────────────────────── models ─────────────────────────────

        /// <summary>The static model (plus named moving parts) for a mall machine, facing local +z. False if d isn't one.</summary>
        public static bool Model(BuildDef d, Transform t, MeshKit k)
        {
            switch (d.Id)
            {
                case "dig_cannon": Cannon(t, k); Voice(t, new Vector3(0, 1.7f, -0.6f)); return true;
                case "carousel": Carousel(t, k); Voice(t, new Vector3(-1.32f, 3.3f, -1.32f)); return true;
                case "proc_slots": Slots(t, k); Voice(t, new Vector3(0, 3.0f, 0)); return true;
                case "wishing_well": Well(t, k); Voice(t, new Vector3(0, 2.3f, 0)); return true;
            }
            return false;
        }

        /// <summary>An empty where the machine's speech bubbles (the sommelier, Elvis, the well) hang.</summary>
        static void Voice(Transform t, Vector3 at)
        {
            var v = new GameObject("Voice").transform;
            v.SetParent(t, false);
            v.localPosition = at;
        }

        static void Cannon(Transform t, MeshKit k)
        {
            Color marble = C(0xEFE9DD), gold = C(0xC9A64A), goldHi = C(0xE8C040), velvet = C(0x8A1A2A), silver = C(0xD8DCE0);
            // a marble plinth with a gold rim, and a gilded carriage
            k.Box(new Vector3(0, 0.15f, 0), new Vector3(1.9f, 0.3f, 1.9f), marble);
            k.Box(new Vector3(0, 0.31f, 0), new Vector3(1.94f, 0.04f, 1.94f), gold);
            k.Box(new Vector3(-0.42f, 0.62f, -0.25f), new Vector3(0.12f, 0.6f, 0.95f), gold);
            k.Box(new Vector3(0.42f, 0.62f, -0.25f), new Vector3(0.12f, 0.6f, 0.95f), gold);
            k.Box(new Vector3(0, 0.4f, -0.25f), new Vector3(0.84f, 0.12f, 0.8f), C(0x6A4A2E));
            for (int side = -1; side <= 1; side += 2)
            {
                k.Push(new Vector3(side * 0.6f, 0.62f, -0.25f), Quaternion.Euler(0, 0, 90));
                k.Torus(Vector3.zero, 0.3f, 0.045f, 20, 6, goldHi);
                for (int i = 0; i < 6; i++)
                {
                    float a = i * Mathf.PI / 3;
                    k.Box(new Vector3(Mathf.Cos(a) * 0.15f, 0, Mathf.Sin(a) * 0.15f), new Vector3(0.3f, 0.03f, 0.03f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0), gold);
                }
                k.Cylinder(Vector3.zero, 0.06f, 0.1f, 10, goldHi);
                k.Pop();
            }
            // an ice bucket with the next bottle, and a velvet rope so nobody stands in front of it
            k.Frustum(new Vector3(0.66f, 0.49f, -0.66f), 0.15f, 0.19f, 0.34f, 12, silver);
            k.Cylinder(new Vector3(0.66f, 0.74f, -0.66f), 0.065f, 0.34f, 10, C(0x1F4F2F));
            k.Cylinder(new Vector3(0.66f, 0.95f, -0.66f), 0.035f, 0.1f, 8, goldHi);
            for (int side = -1; side <= 1; side += 2)
            {
                k.Cylinder(new Vector3(side * 0.82f, 0.62f, 0.82f), 0.035f, 0.62f, 8, gold);
                k.Sphere(new Vector3(side * 0.82f, 0.95f, 0.82f), 0.055f, 4, 8, goldHi);
            }
            k.Tube(new Vector3(-0.82f, 0.86f, 0.82f), new Vector3(0, 0.74f, 0.86f), 0.025f, 6, velvet);
            k.Tube(new Vector3(0, 0.74f, 0.86f), new Vector3(0.82f, 0.86f, 0.82f), 0.025f, 6, velvet);
            k.Box(new Vector3(0, 0.36f, 0.9f), new Vector3(0.5f, 0.1f, 0.05f), goldHi);

            // the barrel: a magnum on its side, aimed up at the water (it recoils; the cork flies)
            var barrel = new GameObject("Barrel").transform;
            barrel.SetParent(t, false);
            barrel.localPosition = BarrelPos;
            barrel.localRotation = Quaternion.Euler(BarrelPitch, 0, 0);
            var bk = new MeshKit();
            var along = Quaternion.Euler(90, 0, 0);   // cylinders run along +z
            bk.Glossy(true);
            bk.Cylinder(new Vector3(0, 0, 0.05f), along, 0.3f, 1.1f, 20, C(0x1F4F2F));
            bk.Push(new Vector3(0, 0, 0.72f), along);
            bk.Frustum(Vector3.zero, 0.3f, 0.13f, 0.24f, 20, C(0x1F4F2F));
            bk.Pop();
            bk.Glossy(false);
            bk.Cylinder(new Vector3(0, 0, 0.02f), along, 0.305f, 0.46f, 20, C(0xF4F0E6));
            bk.Cylinder(new Vector3(0, 0, 0.02f), along, 0.31f, 0.1f, 20, C(0xB0182A));
            bk.Cylinder(new Vector3(0, 0, 0.96f), along, 0.125f, 0.3f, 14, C(0xE8C040));
            bk.Cylinder(new Vector3(0, 0, -0.52f), along, 0.26f, 0.04f, 20, C(0x173A22));
            bk.Build("Bottle", barrel, true);
            var ck = new MeshKit();
            ck.Cylinder(Vector3.zero, along, 0.1f, 0.14f, 12, C(0xC8A870));
            ck.Cylinder(new Vector3(0, 0, 0.08f), along, 0.12f, 0.03f, 12, C(0xB89060));
            var cork = ck.Build("Cork", barrel, false);
            cork.transform.localPosition = new Vector3(0, 0, 1.17f);
        }

        static void Carousel(Transform t, MeshKit k)
        {
            Color steel = C(0xB8BEC4), steelLt = C(0xD4D8DC), rubber = C(0x2A2C30), dark = C(0x1D1F22);
            // a stainless drum with a rubber bumper, a sloped island in the middle with its flap curtain,
            // and a chute out the back (local +z) where the line starts
            k.Cylinder(new Vector3(0, 0.25f, 0), 1.42f, 0.5f, 32, steel);
            k.Torus(new Vector3(0, 0.5f, 0), 1.42f, 0.045f, 32, 4, rubber);
            k.Frustum(new Vector3(0, 0.78f, 0), 0.6f, 0.4f, 0.56f, 24, steelLt);
            k.Box(new Vector3(0, 0.82f, -0.52f), new Vector3(0.52f, 0.42f, 0.12f), dark);
            for (int i = 0; i < 6; i++) k.Box(new Vector3(-0.2f + i * 0.08f, 0.82f, -0.59f), new Vector3(0.07f, 0.36f, 0.02f), rubber);
            k.Box(new Vector3(0, 0.3f, 1.3f), new Vector3(0.8f, 0.26f, 0.4f), steel);
            k.Box(new Vector3(0, 0.44f, 1.3f), new Vector3(0.62f, 0.02f, 0.36f), rubber);
            // the flight-information sign on a pole
            k.Tube(new Vector3(-1.32f, 0.1f, -1.32f), new Vector3(-1.32f, 2.5f, -1.32f), 0.045f, 8, dark);
            k.Box(new Vector3(-1.32f, 2.72f, -1.32f), new Vector3(1.1f, 0.42f, 0.14f), dark);
            k.Box(new Vector3(-1.32f, 2.72f, -1.24f), new Vector3(1.0f, 0.34f, 0.01f), C(0x2A1A08, 0.25f));
            k.Box(new Vector3(-1.32f, 2.72f, -1.40f), new Vector3(1.0f, 0.34f, 0.01f), C(0x2A1A08, 0.25f));
            // yellow floor markings: STAND BEHIND THE LINE
            k.Ring(new Vector3(0, 0.505f, 0), 1.44f, 1.5f, 32, C(0xF2B233));

            // the belt of rubber slats that goes round (with a few bags already on it)
            var plate = new GameObject("Plate").transform;
            plate.SetParent(t, false);
            plate.localPosition = new Vector3(0, 0.5f, 0);
            var pk = new MeshKit();
            pk.Ring(new Vector3(0, 0.02f, 0), 0.6f, 1.38f, 32, rubber);
            for (int i = 0; i < 24; i++)
            {
                float a = i * Mathf.PI * 2 / 24;
                pk.Box(new Vector3(Mathf.Cos(a) * 0.99f, 0.03f, Mathf.Sin(a) * 0.99f), new Vector3(0.76f, 0.012f, 0.03f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0), dark);
            }
            pk.Build("Slats", plate, false);
            var rng = new System.Random(4);
            uint[] bagColors = { 0x2F4F8A, 0xD8283A, 0x3A3A3A, 0x6A4FA8, 0xE87A2A, 0x2E8C7A, 0xF2B233, 0x8A5A34 };
            for (int i = 0; i < 12; i++)
            {
                var bag = new MeshKit();
                float w = 0.26f + (float)rng.NextDouble() * 0.12f, h = 0.16f + (float)rng.NextDouble() * 0.08f;
                Color col = C(bagColors[i % bagColors.Length]);
                bag.Box(new Vector3(0, h * 0.5f, 0), new Vector3(w, h, 0.22f), col);
                bag.Box(new Vector3(0, h + 0.02f, 0), new Vector3(w * 0.4f, 0.03f, 0.04f), dark);
                if (i % 3 == 0) bag.Box(new Vector3(w * 0.3f, h * 0.6f, 0.115f), new Vector3(0.06f, 0.1f, 0.01f), C(0xF2B233));
                var bg = bag.Build("Bag" + i, plate, false);
                float a = i * Mathf.PI * 2 / 12 + 0.1f;
                bg.transform.localPosition = new Vector3(Mathf.Cos(a) * 1.0f, 0.03f, Mathf.Sin(a) * 1.0f);
                bg.transform.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + 90 + rng.Next(-20, 20), 0);
            }
        }

        static void Slots(Transform t, MeshKit k)
        {
            Color carpet = C(0x5A0A18), red = C(0xD8283A), gold = C(0xFFC83A), goldDk = C(0xC9A64A), chrome = C(0xD8DCE0), dark = C(0x14060A);
            k.Box(new Vector3(0, 0.1f, 0), new Vector3(1.9f, 0.2f, 1.9f), carpet);
            // the cabinet: reels on both long sides (±x), so a line shows its luck to either aisle
            k.Box(new Vector3(0, 1.15f, 0), new Vector3(1.3f, 1.9f, 1.6f), red);
            k.Box(new Vector3(0, 0.23f, 0), new Vector3(1.34f, 0.06f, 1.64f), goldDk);
            k.Box(new Vector3(0, 2.1f, 0), new Vector3(1.34f, 0.06f, 1.64f), goldDk);
            k.Cylinder(new Vector3(0, 2.12f, 0), Quaternion.Euler(90, 0, 0), 0.62f, 1.6f, 20, gold);
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 0.66f;
                k.Box(new Vector3(x, 1.45f, 0), new Vector3(0.03f, 0.62f, 1.2f), chrome);
                k.Box(new Vector3(x + side * 0.005f, 1.45f, 0), new Vector3(0.02f, 0.5f, 1.08f), dark);
                k.Box(new Vector3(x, 0.95f, 0), new Vector3(0.03f, 0.26f, 1.0f), C(0x3FA0FF, 0.6f));
                k.Box(new Vector3(x + side * 0.06f, 0.55f, 0), new Vector3(0.12f, 0.08f, 0.7f), chrome);
                k.Box(new Vector3(x, 1.86f, 0), new Vector3(0.03f, 0.12f, 1.3f), C(0xFFF0A0, 0.9f));
                // marquee bulbs round the dome's ends
                for (int i = 0; i < 9; i++)
                {
                    float a = Mathf.PI * i / 8;
                    k.Sphere(new Vector3(Mathf.Cos(a) * 0.62f, 2.12f + Mathf.Sin(a) * 0.62f, side * 0.81f), 0.045f, 3, 6, C(0xFFF0A0, 1f));
                }
            }
            // the reels: three on each side, one axle each way (+z), symbols round the rim
            for (int side = -1; side <= 1; side += 2)
                for (int r = 0; r < 3; r++)
                {
                    var reel = new GameObject($"Reel{(side < 0 ? 0 : 3) + r}").transform;
                    reel.SetParent(t, false);
                    reel.localPosition = new Vector3(side * (0.64f - ReelR), 1.45f, -0.34f + r * 0.34f);
                    var rk = new MeshKit();
                    rk.Cylinder(Vector3.zero, Quaternion.Euler(90, 0, 0), ReelR - 0.01f, 0.26f, 16, C(0xF4F0E6));
                    for (int s = 0; s < ReelSymbols.Length; s++)
                    {
                        float a = s * Mathf.PI * 2 / ReelSymbols.Length;
                        rk.Box(new Vector3(Mathf.Cos(a) * ReelR, Mathf.Sin(a) * ReelR, 0), new Vector3(0.02f, 0.1f, 0.14f), Quaternion.Euler(0, 0, a * Mathf.Rad2Deg), C(ReelSymbols[s], s % 4 == 0 ? 0.35f : 0));
                    }
                    rk.Build("Reel", reel, false);
                    // the -x reels face the other way: their window is at 180°
                    if (side < 0) reel.localRotation = Quaternion.Euler(0, 180, 0);
                }
            // the lever (at the input end) and a siren light on top for the jackpot
            var lever = new GameObject("Lever").transform;
            lever.SetParent(t, false);
            lever.localPosition = new Vector3(0.5f, 1.55f, -0.82f);
            var lk = new MeshKit();
            lk.Cylinder(Vector3.zero, Quaternion.Euler(0, 0, 90), 0.07f, 0.16f, 10, chrome);
            lk.Tube(new Vector3(0.06f, 0, 0), new Vector3(0.06f, 0.62f, -0.12f), 0.025f, 8, chrome);
            lk.Sphere(new Vector3(0.06f, 0.66f, -0.13f), 0.08f, 5, 8, C(0xD8283A, 0.15f));
            lk.Build("Arm", lever, false);
            var beacon = new GameObject("Beacon").transform;
            beacon.SetParent(t, false);
            beacon.localPosition = new Vector3(0, 2.78f, 0);
            var ek = new MeshKit();
            ek.Cylinder(new Vector3(0, -0.05f, 0), 0.1f, 0.06f, 12, chrome);
            ek.Cylinder(new Vector3(0, 0.08f, 0), 0.08f, 0.2f, 12, C(0xFF3048, 0.9f));
            ek.Box(new Vector3(0.05f, 0.08f, 0), new Vector3(0.08f, 0.16f, 0.05f), C(0xFFE0E0, 1f));
            ek.Build("Light", beacon, false);
        }

        static void Well(Transform t, MeshKit k)
        {
            Color stone = C(0x8A8F7A), stone2 = C(0x9A9A8A), moss = C(0x5F7A45), timber = C(0x6A4A2E), roof = C(0x4A2E1E), iron = C(0x3A3A3A);
            k.Cylinder(new Vector3(0, 0.05f, 0), 0.96f, 0.1f, 18, C(0x7A7F72));
            // two courses of old stones, a darker shaft inside, and the glowing water far below
            for (int course = 0; course < 3; course++)
                for (int i = 0; i < 14; i++)
                {
                    float a = (i + course * 0.5f) * Mathf.PI * 2 / 14;
                    Color c = (i * 7 + course * 3) % 5 == 0 ? moss : (i + course) % 2 == 0 ? stone : stone2;
                    k.Box(new Vector3(Mathf.Cos(a) * 0.72f, 0.23f + course * 0.24f, Mathf.Sin(a) * 0.72f), new Vector3(0.3f, 0.22f, 0.22f), Quaternion.Euler(0, -a * Mathf.Rad2Deg, 0), c);
                }
            k.Wall(Vector3.zero, 0.58f, 0.1f, 0.82f, 20, C(0x2A2F2A), true);
            k.Cylinder(new Vector3(0, 0.4f, 0), 0.58f, 0.02f, 20, C(0x5FE8F0, 1f));
            // a timber frame with a little shingled roof, and a bronze plaque that has seen some things
            for (int side = -1; side <= 1; side += 2)
                k.Box(new Vector3(side * 0.82f, 1.02f, 0), new Vector3(0.12f, 1.84f, 0.12f), timber);
            k.Box(new Vector3(0, 1.92f, 0), new Vector3(1.9f, 0.1f, 0.1f), timber);
            k.Box(new Vector3(-0.42f, 2.12f, 0), new Vector3(1.0f, 0.06f, 1.2f), Quaternion.Euler(0, 0, 28), roof);
            k.Box(new Vector3(0.42f, 2.12f, 0), new Vector3(1.0f, 0.06f, 1.2f), Quaternion.Euler(0, 0, -28), roof);
            k.Box(new Vector3(0, 0.46f, 0.84f), new Vector3(0.34f, 0.18f, 0.03f), C(0xB08D57));
            k.Box(new Vector3(0, 0.46f, 0.855f), new Vector3(0.28f, 0.03f, 0.01f), C(0x6A4A2E));

            // the windlass (a log with a crank), the rope and the bucket, and a glow over the water
            var windlass = new GameObject("Windlass").transform;
            windlass.SetParent(t, false);
            windlass.localPosition = new Vector3(0, 1.38f, 0);
            var wk = new MeshKit();
            wk.Cylinder(Vector3.zero, Quaternion.Euler(0, 0, 90), 0.09f, 1.56f, 10, C(0x8A6A4A));
            wk.Tube(new Vector3(0.8f, 0, 0), new Vector3(0.8f, 0.24f, 0), 0.025f, 6, iron);
            wk.Tube(new Vector3(0.8f, 0.24f, 0), new Vector3(0.95f, 0.24f, 0), 0.025f, 6, iron);
            wk.Build("Log", windlass, true);
            var rope = new GameObject("Rope").transform;
            rope.SetParent(t, false);
            rope.localPosition = new Vector3(0, 1.3f, 0);
            var rk = new MeshKit();
            rk.Tube(Vector3.zero, Vector3.down, 0.012f, 5, C(0xC8B08A));
            rk.Build("Line", rope, false);
            var bucket = new GameObject("Bucket").transform;
            bucket.SetParent(t, false);
            bucket.localPosition = new Vector3(0, 0.95f, 0);
            var uk = new MeshKit();
            uk.Frustum(new Vector3(0, -0.12f, 0), 0.13f, 0.16f, 0.24f, 12, C(0x7A5A3A));
            uk.Torus(new Vector3(0, -0.04f, 0), 0.155f, 0.012f, 12, 4, iron);
            uk.Tube(new Vector3(-0.15f, 0, 0), new Vector3(0, 0.1f, 0), 0.01f, 4, iron);
            uk.Tube(new Vector3(0, 0.1f, 0), new Vector3(0.15f, 0, 0), 0.01f, 4, iron);
            uk.Build("Bucket", bucket, true);
            var glow = new GameObject("Glow").transform;
            glow.SetParent(t, false);
            glow.localPosition = new Vector3(0, 0.44f, 0);
            var gk = new MeshKit();
            gk.Cylinder(Vector3.zero, 0.5f, 0.02f, 20, C(0x9FF0FF, 1f));
            gk.Build("Shine", glow, false);
        }

        // ───────────────────────────── moving parts ─────────────────────────────

        sealed class DroneRig
        {
            public Transform T, Bag;
            public Transform[] Rotors;
            public Vector3 Last;
        }

        sealed class Rig
        {
            public Transform Barrel, Cork, Plate, Lever, Beacon, Windlass, Bucket, Rope, Glow, Voice;
            public Transform[] Reels, Bags;
            public readonly List<DroneRig> Drones = new List<DroneRig>();
            public TextMesh[] Sign;
            public float Kick, Jackpot, Pull, PlateAngle, SignClock, WindAngle, CorkHidden;
            public readonly float[] ReelAngle = new float[6];
            public int ShownSeven = -1;
        }

        readonly Dictionary<int, Rig> rigs = new Dictionary<int, Rig>();
        Transform root;
        ViewContext ctx;
        Mesh droneBody, droneRotor, droneBag, corkMesh;

        public void Init(Transform factoryRoot, ViewContext c)
        {
            root = factoryRoot;
            ctx = c;
            var sim = c.Sim;
            sim.OnCannonBlast += (b, n) => { if (rigs.TryGetValue(b.Uid, out var r)) Fire(b, r); };
            sim.OnJackpot += (b, v) => { if (rigs.TryGetValue(b.Uid, out var r)) Jackpot(b, r); };
            sim.OnWellGranted += (b, w, s) => { if (rigs.TryGetValue(b.Uid, out var r)) { r.Pull = 1; Splash(b, r); } };
        }

        /// <summary>Grab the named moving parts of a freshly built machine.</summary>
        public void Attach(Building b, Transform t)
        {
            var r = new Rig
            {
                Barrel = t.Find("Barrel"), Plate = t.Find("Plate"), Lever = t.Find("Lever"), Beacon = t.Find("Beacon"),
                Windlass = t.Find("Windlass"), Bucket = t.Find("Bucket"), Rope = t.Find("Rope"), Glow = t.Find("Glow"),
                Voice = t.Find("Voice"),
            };
            if (r.Barrel != null) r.Cork = r.Barrel.Find("Cork");
            if (b.Def.Process == "slots")
            {
                r.Reels = new Transform[6];
                for (int i = 0; i < 6; i++) r.Reels[i] = t.Find("Reel" + i);
            }
            if (r.Plate != null)
            {
                r.Bags = new Transform[12];
                for (int i = 0; i < 12; i++) r.Bags[i] = r.Plate.Find("Bag" + i);
                // the flight board; TextMesh stays off the build ghost, so it's made here, not in Model.
                // (TextMesh reads from -z; turn it round for the board's +z face)
                var face = Quaternion.Euler(0, 180, 0);
                r.Sign = new[]
                {
                    WorldBuilder.MakeText(t, "CAROUSEL 4", new Vector3(-1.32f, 2.72f, -1.225f), face, 0.13f, C(0xFFB020), 1.3f),
                    WorldBuilder.MakeText(t, "CAROUSEL 4", new Vector3(-1.32f, 2.72f, -1.415f), Quaternion.identity, 0.13f, C(0xFFB020), 1.3f),
                };
            }
            rigs[b.Uid] = r;
        }

        /// <summary>Where a machine's speech bubble hangs (null if it doesn't talk).</summary>
        public Transform Voice(int uid) => rigs.TryGetValue(uid, out var r) ? r.Voice : null;

        public void Detach(int uid)
        {
            if (!rigs.TryGetValue(uid, out var r)) return;
            foreach (var d in r.Drones) if (d.T != null) Object.Destroy(d.T.gameObject);
            rigs.Remove(uid);
        }

        public void Clear()
        {
            foreach (var r in rigs.Values) foreach (var d in r.Drones) if (d.T != null) Object.Destroy(d.T.gameObject);
            rigs.Clear();
        }

        public void Animate(Building b, Transform t, float dt, float time)
        {
            if (!rigs.TryGetValue(b.Uid, out var r)) return;
            if (r.Barrel != null) AnimateCannon(r, dt);
            else if (r.Plate != null) AnimateCarousel(b, t, r, dt, time);
            else if (r.Reels != null) AnimateSlots(b, r, dt, time);
            else if (r.Windlass != null) AnimateWell(r, dt, time);
        }

        // ── the cannon ──

        void Fire(Building b, Rig r)
        {
            r.Kick = 1;
            r.CorkHidden = 0.9f;
            var muzzle = r.Barrel.TransformPoint(new Vector3(0, 0, 1.25f));
            var impact = new Vector3(b.LastPickX, ctx.Fountain.WaterY, b.LastPickZ);
            ctx.Fx.Dust(muzzle, new Color(1f, 0.98f, 0.9f), 6, 0.7f, 0.35f, 0.9f);
            ctx.Fx.Sparks(muzzle, new Color(1f, 0.9f, 0.55f), 12, 3.5f, 0.08f, 0.45f);
            if (corkMesh == null) corkMesh = Loot.Tinted(ItemShape.Wad, C(0xC8A870));
            ctx.Fx.Fly(muzzle, () => impact, corkMesh, 1.6f, 0.55f, 1.2f, () =>
            {
                // a champagne geyser where the cork lands
                ctx.Fx.Sparks(impact, new Color(1f, 0.95f, 0.75f), 26, 5f, 0.12f, 0.8f);
                ctx.Fx.Bubbles(impact, new Color(1f, 0.98f, 0.9f), 18, 0.9f, 0.2f);
                ctx.Fx.Ripple(impact + Vector3.up * 0.01f, new Color(1f, 0.97f, 0.85f, 0.7f), 3.4f, 0.9f);
                ctx.Fx.Dust(impact, MeshKit.Hex(ctx.Sim.CurStratum.Color), 6, 1.3f, 1.0f, 1.6f);
            });
        }

        void AnimateCannon(Rig r, float dt)
        {
            r.Kick = Mathf.Max(0, r.Kick - dt * 2.2f);
            var axis = Quaternion.Euler(BarrelPitch, 0, 0) * Vector3.forward;
            r.Barrel.localPosition = BarrelPos - axis * (r.Kick * r.Kick * 0.3f);
            r.CorkHidden -= dt;
            if (r.Cork != null && r.Cork.gameObject.activeSelf != r.CorkHidden <= 0) r.Cork.gameObject.SetActive(r.CorkHidden <= 0);
        }

        // ── the baggage carousel and its drones ──

        DroneRig MakeDrone(int seed)
        {
            if (droneBody == null)
            {
                var k = new MeshKit();
                k.Box(Vector3.zero, new Vector3(0.36f, 0.12f, 0.36f), C(0xF2B233));
                k.Box(new Vector3(0, 0.08f, 0), new Vector3(0.2f, 0.06f, 0.2f), C(0x2A2C30));
                k.Box(new Vector3(0, 0, 0.185f), new Vector3(0.1f, 0.03f, 0.01f), C(0x6CFF9A, 1f));
                for (int i = 0; i < 4; i++)
                {
                    float a = (i + 0.5f) * Mathf.PI / 2;
                    k.Tube(Vector3.zero, new Vector3(Mathf.Cos(a) * 0.34f, 0.02f, Mathf.Sin(a) * 0.34f), 0.02f, 5, C(0x2A2C30));
                    k.Cylinder(new Vector3(Mathf.Cos(a) * 0.34f, 0.04f, Mathf.Sin(a) * 0.34f), 0.03f, 0.06f, 6, C(0x2A2C30));
                }
                k.Tube(new Vector3(-0.1f, -0.05f, 0), new Vector3(-0.1f, -0.26f, 0), 0.008f, 4, C(0x2A2C30));
                k.Tube(new Vector3(0.1f, -0.05f, 0), new Vector3(0.1f, -0.26f, 0), 0.008f, 4, C(0x2A2C30));
                droneBody = k.ToMesh("drone");
                var rk = new MeshKit();
                rk.Cylinder(Vector3.zero, 0.15f, 0.008f, 12, C(0x8A8F96));
                rk.Box(Vector3.zero, new Vector3(0.3f, 0.012f, 0.03f), C(0x2A2C30));
                droneRotor = rk.ToMesh("rotor");
                var bk = new MeshKit();
                bk.Box(new Vector3(0, -0.4f, 0), new Vector3(0.34f, 0.24f, 0.2f), C(0x2F4F8A));
                bk.Box(new Vector3(0, -0.27f, 0), new Vector3(0.12f, 0.03f, 0.04f), C(0x1D1F22));
                bk.Box(new Vector3(0.1f, -0.4f, 0.105f), new Vector3(0.06f, 0.1f, 0.01f), C(0xF2B233));
                droneBag = bk.ToMesh("drone bag");
            }
            var go = new GameObject("Baggage Drone");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = droneBody;
            go.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            var d = new DroneRig { T = go.transform, Rotors = new Transform[4] };
            for (int i = 0; i < 4; i++)
            {
                var ro = new GameObject("Rotor");
                ro.transform.SetParent(go.transform, false);
                float a = (i + 0.5f) * Mathf.PI / 2;
                ro.transform.localPosition = new Vector3(Mathf.Cos(a) * 0.34f, 0.08f, Mathf.Sin(a) * 0.34f);
                ro.AddComponent<MeshFilter>().sharedMesh = droneRotor;
                var mr = ro.AddComponent<MeshRenderer>();
                mr.sharedMaterial = Mats.Lit;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                d.Rotors[i] = ro.transform;
            }
            var bag = new GameObject("Bag");
            bag.transform.SetParent(go.transform, false);
            bag.AddComponent<MeshFilter>().sharedMesh = droneBag;
            bag.AddComponent<MeshRenderer>().sharedMaterial = Mats.Lit;
            d.Bag = bag.transform;
            return d;
        }

        void AnimateCarousel(Building b, Transform t, Rig r, float dt, float time)
        {
            r.PlateAngle += dt * (8 + 30 * b.Activity);
            r.Plate.localRotation = Quaternion.Euler(0, r.PlateAngle, 0);
            int bags = Mathf.Clamp(Mathf.CeilToInt(b.BufCount / 60f), 0, 12);
            for (int i = 0; i < r.Bags.Length; i++)
                if (r.Bags[i] != null && r.Bags[i].gameObject.activeSelf != i < bags) r.Bags[i].gameObject.SetActive(i < bags);
            // the gate changes now and then, as gates do
            r.SignClock += dt;
            string gate = r.SignClock % 26f > 22f ? "CAROUSEL 7" : "CAROUSEL 4";
            foreach (var s in r.Sign) if (s != null && s.text != gate) s.text = gate;

            var sim = ctx.Sim;
            while (r.Drones.Count < b.Drones.Count) r.Drones.Add(MakeDrone(r.Drones.Count));
            var (hx, hz) = b.Center;
            for (int i = 0; i < r.Drones.Count; i++)
            {
                var dr = r.Drones[i];
                if (i >= b.Drones.Count) { dr.T.gameObject.SetActive(false); continue; }
                var d = b.Drones[i];
                dr.T.gameObject.SetActive(true);
                float cruise = 4.1f + (i % 4) * 0.35f;
                float y;
                switch (d.State)
                {
                    case DroneState.Docked: y = 1.45f + i * 0.28f + Mathf.Sin(time * 3 + i) * 0.04f; break;
                    case DroneState.Loading: y = 2.2f; break;
                    case DroneState.Unloading: y = 1.6f; break;
                    default:
                    {
                        // climb out, cruise, drop in: height follows the distance to whichever end is nearer
                        float end = float.MaxValue;
                        var tb = sim.FindBuilding(d.Target);
                        if (tb != null) { var (tx, tz) = tb.Center; end = Mathf.Min(end, Vector2.Distance(new Vector2(d.X, d.Z), new Vector2(tx, tz))); }
                        end = Mathf.Min(end, Vector2.Distance(new Vector2(d.X, d.Z), new Vector2(hx, hz)));
                        y = Mathf.Lerp(1.8f, cruise, Mathf.SmoothStep(0, 1, end / 3.5f));
                        break;
                    }
                }
                var p = new Vector3(d.X + (d.State == DroneState.Docked ? Mathf.Cos(i * 2.1f) * 0.5f : 0), y, d.Z + (d.State == DroneState.Docked ? Mathf.Sin(i * 2.1f) * 0.5f : 0));
                var v = (p - dr.Last) / Mathf.Max(1e-4f, dt);
                dr.Last = p;
                dr.T.position = p;
                var flat = new Vector3(v.x, 0, v.z);
                if (flat.sqrMagnitude > 0.2f)
                {
                    // lean into the flight
                    var yaw = Quaternion.LookRotation(flat);
                    dr.T.rotation = Quaternion.Slerp(dr.T.rotation, yaw * Quaternion.Euler(Mathf.Clamp(flat.magnitude * 2.2f, 0, 18), 0, 0), dt * 6);
                }
                else dr.T.rotation = Quaternion.Slerp(dr.T.rotation, Quaternion.Euler(0, dr.T.eulerAngles.y, 0), dt * 4);
                foreach (var ro in dr.Rotors) ro.localRotation = Quaternion.Euler(0, time * 1800 + i * 40, 0);
                bool loaded = d.CargoCount > 0;
                if (dr.Bag.gameObject.activeSelf != loaded) dr.Bag.gameObject.SetActive(loaded);
            }
        }

        // ── the slot machine ──

        void Jackpot(Building b, Rig r)
        {
            r.Jackpot = 3.2f;
            var top = FactoryView.WorldCenter(b) + Vector3.up * 2.9f;
            ctx.Fx.Confetti(top, 60, 7);
            ctx.Fx.CoinShower(top, new[] { new Color(1f, 0.78f, 0.23f), new Color(1f, 0.9f, 0.5f) }, 40, 6f, 1.2f);
            ctx.Fx.Glint(top, new Color(1f, 0.95f, 0.6f), 2.2f, 4, 0.6f);
        }

        void AnimateSlots(Building b, Rig r, float dt, float time)
        {
            r.Jackpot = Mathf.Max(0, r.Jackpot - dt);
            float act = b.Activity;
            for (int i = 0; i < 6; i++)
            {
                if (r.Reels[i] == null) continue;
                float stop;
                if (r.Jackpot > 0) stop = 0;                                     // 7 7 7, for everyone to see
                else if (act > 0.25f) { r.ReelAngle[i] += dt * (700 + i * 90); r.Reels[i].localRotation = ReelRotation(i, r.ReelAngle[i]); continue; }
                else stop = ResultSymbol[Mathf.Clamp(b.SpinResult, 0, 4)] + (i % 3 == 1 ? 0 : (i * 3) % 8);
                // ease the reel onto its symbol (the window is at angle 0)
                float target = -stop * 360f / ReelSymbols.Length;
                r.ReelAngle[i] = Mathf.LerpAngle(r.ReelAngle[i], target, 1 - Mathf.Exp(-dt * 10));
                r.Reels[i].localRotation = ReelRotation(i, r.ReelAngle[i]);
            }
            if (r.Lever != null) r.Lever.localRotation = Quaternion.Euler(act > 0.25f ? Mathf.Abs(Mathf.Sin(time * 2.6f)) * 55 : 0, 0, 0);
            if (r.Beacon != null)
            {
                r.Beacon.localRotation = Quaternion.Euler(0, time * (r.Jackpot > 0 ? 720 : 40), 0);
                float s = r.Jackpot > 0 ? 1.3f + Mathf.Abs(Mathf.Sin(time * 14)) * 0.5f : 1f;
                r.Beacon.localScale = new Vector3(s, s, s);
            }
            if (r.Jackpot > 0 && Random.value < dt * 8)
            {
                var c = FactoryView.WorldCenter(b);
                ctx.Fx.Glint(c + new Vector3(Random.Range(-0.7f, 0.7f), 2.1f + Random.value * 0.7f, Random.Range(-0.8f, 0.8f)), new Color(1f, 0.9f, 0.5f), 0.8f);
            }
        }

        static Quaternion ReelRotation(int i, float angle) => (i < 3 ? Quaternion.Euler(0, 180, 0) : Quaternion.identity) * Quaternion.Euler(0, 0, angle);

        // ── the well ──

        void Splash(Building b, Rig r)
        {
            var top = FactoryView.WorldCenter(b) + Vector3.up * 0.5f;
            ctx.Fx.Sparks(top, new Color(0.6f, 0.95f, 1f), 16, 2.5f, 0.1f, 0.7f);
            ctx.Fx.Glint(top + Vector3.up * 0.3f, new Color(0.7f, 1f, 1f), 1.6f, 3, 0.3f);
            ctx.Fx.Bubbles(top, new Color(0.7f, 1f, 1f), 10, 0.4f, 0.15f);
        }

        void AnimateWell(Rig r, float dt, float time)
        {
            r.Pull = Mathf.Max(0, r.Pull - dt / 1.8f);
            float down = Mathf.Sin((1 - r.Pull) * Mathf.PI) * (r.Pull > 0 ? 1 : 0);
            if (r.Pull > 0) r.WindAngle += dt * 360 * (r.Pull > 0.5f ? 1 : -1);
            r.Windlass.localRotation = Quaternion.Euler(r.WindAngle, 0, 0);
            float by = Mathf.Lerp(0.95f, 0.5f, down);
            r.Bucket.localPosition = new Vector3(0, by, 0);
            r.Rope.localScale = new Vector3(1, Mathf.Max(0.01f, 1.3f - by), 1);
            float g = 1 + Mathf.Sin(time * 1.3f) * 0.04f + r.Pull * 0.35f;
            r.Glow.localScale = new Vector3(g, 1, g);
        }
    }
}
