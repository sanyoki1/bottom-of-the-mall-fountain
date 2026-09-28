// Procedural models + animation for every machine. Each visual shows up to MaxCopies
// physical copies (a row of tumblers, a flock of pigeons...) and animates faster while
// its pipeline stage is busy. Clicking a machine "whacks" it for a burst of output.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class ViewContext
    {
        public Sim Sim;
        public FountainView Fountain;
        public FX Fx;
        public float Time;
        public readonly float[] Activity = new float[3];   // smoothed 0..1 busy-ness per stage
        public MallDef Mall;
    }

    /// <summary>Tag on anything clickable in the world.</summary>
    public sealed class ClickTarget : MonoBehaviour
    {
        public string Kind;    // "machine", "vending", "wish", "golden", "rat"
        public string Id;
        public int Uid;
    }

    public abstract class MachineVisual
    {
        public MachineDef Def;
        public Transform Root;
        public int Shown;
        protected ViewContext ctx;
        protected System.Random rng = new System.Random(3);
        protected readonly List<Transform> copies = new List<Transform>();
        readonly List<float> popIn = new List<float>();
        float wobble;
        public Vector3 Anchor;          // where labels / effects go
        public abstract int MaxCopies { get; }
        protected static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);
        protected static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);
        public static Vector3 Polar(float deg, float r, float y = 0) => new Vector3(Mathf.Cos(deg * Mathf.Deg2Rad) * r, y, Mathf.Sin(deg * Mathf.Deg2Rad) * r);
        public static Quaternion Face(Vector3 pos) { var d = -pos; d.y = 0; return d.sqrMagnitude < 1e-4f ? Quaternion.identity : Quaternion.LookRotation(d.normalized); }

        public void Init(MachineDef def, Transform parent, ViewContext c)
        {
            Def = def;
            ctx = c;
            Root = new GameObject(def.Name).transform;
            Root.SetParent(parent, false);
            OnInit();
        }

        protected virtual void OnInit() { }

        public void SetCount(int count)
        {
            int want = Mathf.Min(count, MaxCopies);
            while (Shown < want)
            {
                var t = AddCopy(Shown);
                if (t != null)
                {
                    copies.Add(t);
                    popIn.Add(0);
                    AddCollider(t);
                }
                Shown++;
            }
            OnCountChanged(count);
        }

        protected virtual void OnCountChanged(int count) { }

        protected abstract Transform AddCopy(int index);

        protected virtual void AddCollider(Transform t)
        {
            var bounds = new Bounds(t.position, Vector3.zero);
            foreach (var r in t.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(r.bounds);
            var go = new GameObject("Click");
            go.transform.SetParent(t, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(bounds.center);
            var size = bounds.size;
            box.size = new Vector3(Mathf.Max(size.x, 1f), Mathf.Max(size.y, 1f), Mathf.Max(size.z, 1f));
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "machine";
            ct.Id = Def.Id;
        }

        public void Whack() { wobble = 1f; }

        public void Tick(float dt, float time, float activity)
        {
            for (int i = 0; i < copies.Count; i++)
            {
                if (popIn[i] < 1)
                {
                    popIn[i] = Mathf.Min(1, popIn[i] + dt * 2.5f);
                    float p = popIn[i];
                    float s = p < 1 ? 1 + Mathf.Sin(p * Mathf.PI) * 0.25f - (1 - p) * (1 - p) * 1f : 1;
                    copies[i].localScale = Vector3.one * Mathf.Max(0.01f, s);
                }
            }
            if (wobble > 0)
            {
                wobble = Mathf.Max(0, wobble - dt * 3f);
                float w = Mathf.Sin(wobble * 30) * wobble * 0.08f;
                Root.localScale = new Vector3(1 + w, 1 - w, 1 + w);
            }
            OnTick(dt, time, activity);
        }

        protected abstract void OnTick(float dt, float time, float activity);

        /// <summary>World position where processed loot visually lands for this machine.</summary>
        public virtual Vector3 Mouth => Anchor;

        public Vector3 RandomCopyPosition() => copies.Count > 0 ? copies[Random.Range(0, copies.Count)].position : (Anchor != Vector3.zero ? Anchor : Root.position);
        public Vector3 LastCopyPosition() => copies.Count > 0 ? copies[copies.Count - 1].position : Anchor;

        protected Transform NewCopy(string name, Vector3 pos, Quaternion rot)
        {
            var t = new GameObject(name).transform;
            t.SetParent(Root, false);
            t.localPosition = pos;
            t.localRotation = rot;
            t.localScale = Vector3.one * 0.01f;
            return t;
        }

        protected static GameObject Part(MeshKit k, string name, Transform parent, Vector3 localPos, bool shadows = true)
        {
            var go = k.Build(name, parent, shadows);
            go.transform.localPosition = localPos;
            return go;
        }
    }

    // ───────────────────────────── DIG ─────────────────────────────

    public sealed class PogoVisual : MachineVisual
    {
        public override int MaxCopies => 10;
        sealed class Rider { public Transform T; public Person P; public float Phase, X, Z, TX, TZ, Speed; public int Hops; }
        readonly List<Rider> riders = new List<Rider>();
        static readonly uint[] Shirts = { 0xFF7AA8, 0x39E5D0, 0xFFD34D, 0x7A6FA8, 0x5AC8FF, 0xFF9A3A };

        protected override Transform AddCopy(int index)
        {
            var t = NewCopy("Pogo " + index, Vector3.zero, Quaternion.identity);
            var k = new MeshKit();
            Color metal = C(0xC0C4CA), red = C(0xE8505B);
            k.Glossy(true);
            k.Tube(new Vector3(0, 0.15f, 0), new Vector3(0, 1.55f, 0), 0.035f, 6, metal);
            k.Glossy(false);
            k.Box(new Vector3(0, 1.55f, 0), new Vector3(0.6f, 0.05f, 0.05f), red);
            k.Box(new Vector3(0, 0.55f, 0), new Vector3(0.42f, 0.04f, 0.1f), red);
            for (int i = 0; i < 5; i++) k.Torus(new Vector3(0, 0.22f + i * 0.055f, 0), 0.06f, 0.012f, 8, 3, metal);
            // the metal detector plate at the bottom (it's a metal-detecting pogo stick)
            k.Cylinder(new Vector3(0, 0.07f, 0.02f), 0.18f, 0.03f, 12, C(0x3A3F48));
            k.Sphere(new Vector3(0, 0.1f, 0.2f), 0.035f, 3, 5, C(0x7CF45A, 1f));
            k.Build("Stick", t);
            var p = Actors.MakePerson(t, C(Shirts[index % Shirts.Length]), C(0x2A3A5A), C(0xE8B894), C(0xFFD34D), true, 0.95f);
            p.Root.localPosition = new Vector3(0, 0.36f, 0);
            p.ArmL.localRotation = Quaternion.Euler(-70, 0, 20);
            p.ArmR.localRotation = Quaternion.Euler(-70, 0, -20);
            var a = (float)rng.NextDouble() * Mathf.PI * 2;
            float r = Mathf.Lerp(2f, 6.8f, (float)rng.NextDouble());
            riders.Add(new Rider { T = t, P = p, Phase = (float)rng.NextDouble(), X = Mathf.Cos(a) * r, Z = Mathf.Sin(a) * r, TX = Mathf.Cos(a) * r, TZ = Mathf.Sin(a) * r, Speed = 0.8f + (float)rng.NextDouble() * 0.6f });
            return t;
        }

        protected override void AddCollider(Transform t) { }  // riders move; the clickable dig is the crust itself

        protected override void OnTick(float dt, float time, float activity)
        {
            if (ctx.Sim.MallCleared) activity = 0;
            foreach (var rd in riders)
            {
                float prev = rd.Phase;
                rd.Phase += dt * (1.4f + activity * 1.1f);
                if (Mathf.FloorToInt(rd.Phase) != Mathf.FloorToInt(prev) && activity > 0.05f)
                {
                    var ground = ctx.Fountain.SurfacePoint(rd.X, rd.Z);
                    ctx.Fountain.Dig(ground, 0.12f, 0.9f);
                    ctx.Fx.Dust(ground, ctx.Mall.Strata[ctx.Sim.Stratum].Color.ToColor() * 0.9f, 3, 0.5f, 0.4f, 0.8f);
                    if (++rd.Hops % 3 == 0) ctx.Fx.Glint(ground + Vector3.up * 0.1f, new Color(1f, 0.85f, 0.5f), 0.6f);
                    if (rd.Hops % 4 == 0)
                    {
                        float a = (float)rng.NextDouble() * Mathf.PI * 2, r = Mathf.Lerp(1.8f, 6.8f, Mathf.Sqrt((float)rng.NextDouble()));
                        rd.TX = Mathf.Cos(a) * r;
                        rd.TZ = Mathf.Sin(a) * r;
                    }
                }
                var to = new Vector2(rd.TX - rd.X, rd.TZ - rd.Z);
                if (to.magnitude > 0.05f)
                {
                    var step = to.normalized * Mathf.Min(to.magnitude, dt * rd.Speed * (0.4f + activity));
                    rd.X += step.x;
                    rd.Z += step.y;
                }
                float hop = Mathf.Abs(Mathf.Sin(rd.Phase * Mathf.PI)) * (0.35f + 0.55f * activity);
                float y = ctx.Fountain.HeightAt(rd.X, rd.Z);
                rd.T.localPosition = new Vector3(rd.X, y + hop, rd.Z);
                if (to.sqrMagnitude > 0.01f) rd.T.localRotation = Quaternion.Slerp(rd.T.localRotation, Quaternion.LookRotation(new Vector3(to.x, 0, to.y)) * Quaternion.Euler(8, 0, 0), dt * 4);
            }
        }
    }

    public sealed class WalkersVisual : MachineVisual
    {
        public override int MaxCopies => 12;
        readonly List<Person> people = new List<Person>();
        readonly List<float> offsets = new List<float>();
        float lap;
        static readonly uint[] Suits = { 0x5FB8A8, 0xB57CD8, 0xF28CB2, 0x6FA8E8, 0xE8C85A, 0xF2F2F2 };

        protected override Transform AddCopy(int index)
        {
            var t = NewCopy("Walker " + index, Vector3.zero, Quaternion.identity);
            var p = Actors.MakePerson(t, C(Suits[index % Suits.Length]), C(Suits[(index + 2) % Suits.Length]), C(0xE8C0A0), C(0x5FB8A8), false, 1.0f, true);
            people.Add(p);
            offsets.Add(index * 30f + (float)rng.NextDouble() * 8);
            return t;
        }

        protected override void AddCollider(Transform t) { }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (ctx.Sim.MallCleared) activity = 0;
            lap += dt * (6f + activity * 7f);
            for (int i = 0; i < people.Count; i++)
            {
                float ang = (lap + offsets[i]) * Mathf.Deg2Rad;
                float r = 6.2f + Mathf.Sin(ang * 3 + i) * 0.3f;
                float x = Mathf.Cos(ang) * r, z = Mathf.Sin(ang) * r;
                float y = ctx.Fountain.HeightAt(x, z);
                var root = copies[i];
                root.localPosition = new Vector3(x, y, z);
                root.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(ang), 0, Mathf.Cos(ang)));
                Actors.Animate(people[i], time * (0.9f + activity * 0.5f) + i, 0.3f + activity, 55f);
                if (activity > 0.1f && Random.value < dt * 0.6f)
                    ctx.Fx.Dust(new Vector3(x, y, z), ctx.Mall.Strata[ctx.Sim.Stratum].Color.ToColor(), 2, 0.4f, 0.3f, 0.5f);
            }
        }
    }

    public sealed class JackhammerVisual : MachineVisual
    {
        public override int MaxCopies => 3;
        static readonly float[] Angles = { 34, 146, 14 };
        sealed class Arm { public Transform Upper, Fore, Head; public float Ang, Phase; public Vector3 Base; }
        readonly List<Arm> arms = new List<Arm>();
        const float L1 = 4.6f, L2 = 5.4f;

        protected override Transform AddCopy(int index)
        {
            float ang = Angles[index % Angles.Length];
            Vector3 basePos = Polar(ang, 8.75f, FountainView.RimTop);
            var t = NewCopy("Jackhammer " + index, basePos, Quaternion.identity);
            Color yellow = C(0xF2C230), black = C(0x2A2A2A), steel = C(0xB8BCC4);
            var k = new MeshKit();
            k.Cylinder(new Vector3(0, 0.35f, 0), 0.75f, 0.7f, 16, yellow);
            for (int s = 0; s < 8; s++)
            {
                float a = s * 45 * Mathf.Deg2Rad;
                k.Box(new Vector3(Mathf.Cos(a) * 0.76f, 0.35f, Mathf.Sin(a) * 0.76f), new Vector3(0.18f, 0.72f, 0.18f), black);
            }
            k.Cylinder(new Vector3(0, 1.0f, 0), 0.4f, 0.8f, 12, steel);
            k.Sphere(new Vector3(0, 1.45f, 0), 0.42f, 6, 10, yellow);
            k.Box(new Vector3(0, 1.2f, -0.45f), new Vector3(0.3f, 0.2f, 0.05f), C(0xFF5A3A, 1f));
            k.Build("Base", t);
            var arm = new Arm { Ang = ang, Base = basePos + Vector3.up * 1.45f, Phase = (float)rng.NextDouble() };
            arm.Upper = Segment(t.parent, L1, 0.28f, yellow, "Upper");
            arm.Fore = Segment(t.parent, L2, 0.22f, yellow, "Fore");
            var h = new MeshKit();
            h.Box(new Vector3(0, 0.2f, 0), new Vector3(0.6f, 0.9f, 0.6f), C(0xE8A020));
            h.Box(new Vector3(0, 0.62f, 0), new Vector3(0.7f, 0.12f, 0.7f), black);
            h.Glossy(true);
            h.Cylinder(new Vector3(0, -0.55f, 0), 0.09f, 0.7f, 8, steel);
            h.Cone(new Vector3(0, -1.05f, 0), 0.09f, 0.18f, 8, steel);
            arm.Head = h.Build("Hammer", t.parent).transform;
            arms.Add(arm);
            Anchor = basePos + Vector3.up * 3;
            return t;
        }

        static Transform Segment(Transform parent, float len, float r, Color c, string name)
        {
            var k = new MeshKit();
            k.Push(new Vector3(0, 0, len / 2), Quaternion.Euler(90, 0, 0));
            k.Cylinder(Vector3.zero, r, len, 10, c);
            k.Pop();
            k.Sphere(Vector3.zero, r * 1.25f, 5, 8, C(0x2A2A2A));
            k.Tube(new Vector3(r, r, 0.3f), new Vector3(r, r, len - 0.3f), 0.05f, 5, C(0x333333));
            return k.Build(name, parent).transform;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (ctx.Sim.MallCleared) activity = 0;
            foreach (var a in arms)
            {
                float sweep = Mathf.Sin(time * 0.25f + a.Phase * 6) * 18f;
                float ang = (a.Ang + sweep) * Mathf.Deg2Rad;
                float reach = 4.2f + Mathf.Sin(time * 0.33f + a.Phase * 3) * 1.2f;
                float tx = Mathf.Cos(ang) * reach, tz = Mathf.Sin(ang) * reach;
                a.Phase += dt * (activity > 0.05f ? 7f + activity * 6f : 0.5f);
                float pound = activity > 0.05f ? Mathf.Abs(Mathf.Sin(a.Phase * Mathf.PI)) * 0.35f : 0.3f;
                Vector3 target = new Vector3(tx, ctx.Fountain.HeightAt(tx, tz) + 1.45f + pound, tz);
                Vector3 P = a.Base;
                Vector3 d = target - P;
                float dist = Mathf.Clamp(d.magnitude, 0.5f, L1 + L2 - 0.05f);
                Vector3 dir = d.normalized;
                float cosA = Mathf.Clamp((L1 * L1 + dist * dist - L2 * L2) / (2 * L1 * dist), -1, 1);
                float angA = Mathf.Acos(cosA);
                Vector3 side = Vector3.Cross(dir, Vector3.up).normalized;
                if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
                Vector3 elbowDir = Quaternion.AngleAxis(-angA * Mathf.Rad2Deg, side) * dir;
                Vector3 E = P + elbowDir * L1;
                Vector3 T = P + dir * dist;
                a.Upper.position = P;
                a.Upper.rotation = Quaternion.LookRotation(E - P);
                a.Fore.position = E;
                a.Fore.rotation = Quaternion.LookRotation(T - E);
                a.Head.position = T;
                a.Head.rotation = Quaternion.identity;
                int hit = Mathf.FloorToInt(a.Phase);
                if (activity > 0.05f && hit != Mathf.FloorToInt(a.Phase - dt * (7f + activity * 6f)))
                {
                    var ground = ctx.Fountain.SurfacePoint(tx, tz);
                    ctx.Fountain.Dig(ground, 0.22f, 1.4f);
                    ctx.Fx.Dust(ground, ctx.Mall.Strata[ctx.Sim.Stratum].Color.ToColor(), 4, 0.9f, 0.7f, 1.5f);
                    if (Random.value < 0.4f) ctx.Fx.Sparks(ground + Vector3.up * 0.2f, new Color(1f, 0.8f, 0.4f), 4, 3f);
                }
            }
        }
    }

    public sealed class ClawVisual : MachineVisual
    {
        public override int MaxCopies => 2;
        static readonly float[] Angles = { 18, 162 };
        sealed class Crane { public Transform Top, Trolley, Cable, Claw; public float Phase, Ang; public Vector3 Base; }
        readonly List<Crane> cranes = new List<Crane>();
        const float TowerH = 15f, BoomLen = 12.5f;

        protected override Transform AddCopy(int index)
        {
            float ang = Angles[index % Angles.Length];
            Vector3 basePos = Polar(ang, 12.6f);
            var t = NewCopy("Claw Crane " + index, basePos, Quaternion.identity);
            Color pink = C(0xFF4FD8), cyan = C(0x3FE8FF), dark = C(0x2A2340);
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.3f, 0), new Vector3(2.4f, 0.6f, 2.4f), dark);
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1 : 1) * 0.55f, z = (i < 2 ? -1 : 1) * 0.55f;
                k.Box(new Vector3(x, TowerH / 2, z), new Vector3(0.14f, TowerH, 0.14f), pink);
            }
            for (float y = 1.2f; y < TowerH; y += 1.4f)
            {
                k.Box(new Vector3(0, y, -0.55f), new Vector3(1.2f, 0.08f, 0.08f), C(0xFF4FD8, 0.6f));
                k.Box(new Vector3(0, y, 0.55f), new Vector3(1.2f, 0.08f, 0.08f), C(0xFF4FD8, 0.6f));
            }
            k.Build("Tower", t);
            var cr = new Crane { Ang = ang, Base = basePos, Phase = index * 1.7f };
            var top = new MeshKit();
            top.Box(new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.8f, 1.6f), dark);
            top.Box(new Vector3(0, 0.9f, BoomLen / 2), new Vector3(0.7f, 0.5f, BoomLen), cyan);
            top.Box(new Vector3(0, 0.9f, -2.2f), new Vector3(0.7f, 0.5f, 4.4f), cyan);
            top.Box(new Vector3(0, 0.5f, -3.8f), new Vector3(1.4f, 1.2f, 1.4f), C(0x444444));
            top.Box(new Vector3(0, 1.3f, 0.3f), new Vector3(1.0f, 0.8f, 1.0f), C(0x3FE8FF, 0.25f));
            for (float z = 1; z < BoomLen; z += 1.5f) top.Box(new Vector3(0, 1.18f, z), new Vector3(0.75f, 0.06f, 0.1f), C(0xFFE066, 0.8f));
            cr.Top = top.Build("Slew", t).transform;
            cr.Top.localPosition = new Vector3(0, TowerH, 0);
            var tr = new MeshKit();
            tr.Box(Vector3.zero, new Vector3(1.0f, 0.4f, 0.8f), C(0x444444));
            cr.Trolley = tr.Build("Trolley", cr.Top).transform;
            var cb = new MeshKit();
            cb.Box(new Vector3(0, -0.5f, 0), new Vector3(0.05f, 1f, 0.05f), C(0x222222));
            cr.Cable = cb.Build("Cable", cr.Top, false).transform;
            var cl = new MeshKit().Glossy(true);
            cl.Cylinder(new Vector3(0, 0.25f, 0), 0.45f, 0.5f, 12, C(0xC8CCD2));
            cl.Glossy(false);
            for (int p = 0; p < 3; p++)
            {
                float a = p * 120 * Mathf.Deg2Rad;
                Vector3 o = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                cl.Tube(o * 0.35f, o * 0.85f + Vector3.down * 0.6f, 0.07f, 6, C(0xC8CCD2));
                cl.Tube(o * 0.85f + Vector3.down * 0.6f, o * 0.5f + Vector3.down * 1.3f, 0.06f, 6, C(0xC8CCD2));
            }
            cl.Sphere(new Vector3(0, 0.6f, 0), 0.12f, 4, 6, C(0xFF4FD8, 1f));
            cr.Claw = cl.Build("Claw", cr.Top).transform;
            cranes.Add(cr);
            Anchor = basePos + Vector3.up * 6;
            return t;
        }

        protected override void AddCollider(Transform t)
        {
            var go = new GameObject("Click");
            go.transform.SetParent(t, false);
            var box = go.AddComponent<BoxCollider>();
            box.center = new Vector3(0, TowerH / 2, 0);
            box.size = new Vector3(2.5f, TowerH, 2.5f);
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "machine";
            ct.Id = Def.Id;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (ctx.Sim.MallCleared) activity = 0;
            foreach (var cr in cranes)
            {
                float speed = activity > 0.05f ? 0.35f + activity * 0.35f : 0.05f;
                float prev = cr.Phase;
                cr.Phase += dt * speed;
                float cyc = cr.Phase % 1f;
                // slew toward a spot over the basin, lower, grab, raise, swing back
                float slew = Mathf.Sin(cr.Phase * 0.9f) * 35f;
                Vector3 toCenter = -cr.Base;
                toCenter.y = 0;
                float baseYaw = Quaternion.LookRotation(toCenter).eulerAngles.y;
                cr.Top.localRotation = Quaternion.Euler(0, baseYaw + slew, 0);
                float trolleyZ = Mathf.Lerp(5.5f, 11.5f, 0.5f + 0.5f * Mathf.Sin(cr.Phase * 1.7f));
                cr.Trolley.localPosition = new Vector3(0, 0.55f, trolleyZ);
                Vector3 clawWorldXZ = cr.Top.TransformPoint(new Vector3(0, 0, trolleyZ));
                float ground = ctx.Fountain.HeightAt(clawWorldXZ.x, clawWorldXZ.z);
                float drop = cyc < 0.5f ? Mathf.SmoothStep(0, 1, cyc * 2) : Mathf.SmoothStep(1, 0, (cyc - 0.5f) * 2);
                float topY = TowerH - 2.5f;
                float clawY = Mathf.Lerp(topY, ground + 1.3f - TowerH, drop) ;
                float localY = Mathf.Lerp(-2.5f, ground + 1.3f - TowerH, drop);
                cr.Claw.localPosition = new Vector3(0, localY, trolleyZ);
                cr.Cable.localPosition = new Vector3(0, 0.4f, trolleyZ);
                cr.Cable.localScale = new Vector3(1, Mathf.Max(0.1f, -localY + 0.2f), 1);
                if (prev % 1f < 0.5f && cyc >= 0.5f && activity > 0.05f)
                {
                    var g = ctx.Fountain.SurfacePoint(clawWorldXZ.x, clawWorldXZ.z);
                    ctx.Fountain.Dig(g, 0.6f, 2.0f);
                    ctx.Fx.Dust(g, ctx.Mall.Strata[ctx.Sim.Stratum].Color.ToColor(), 8, 1.2f, 1f, 1.5f);
                    ctx.Fx.CoinShower(g, new[] { new Color(0.85f, 0.55f, 0.3f), new Color(0.8f, 0.8f, 0.85f) }, 6, 4f);
                }
            }
        }
    }

    public sealed class BorerVisual : MachineVisual
    {
        public override int MaxCopies => 3;
        sealed class Borer { public Transform T, Cutter; public float Ang, R, Dir; }
        readonly List<Borer> borers = new List<Borer>();

        protected override Transform AddCopy(int index)
        {
            var t = NewCopy("Borer " + index, Vector3.zero, Quaternion.identity);
            Color gold = C(0xE8B83A), dark = C(0x2A2420);
            var k = new MeshKit().Glossy(true);
            k.Box(new Vector3(0, 0.7f, -0.8f), new Vector3(1.8f, 1.2f, 2.4f), gold);
            k.Glossy(false);
            k.Box(new Vector3(0, 0.25f, -0.8f), new Vector3(2.1f, 0.5f, 2.6f), dark);
            k.Box(new Vector3(0, 1.4f, -1.4f), new Vector3(0.9f, 0.5f, 0.9f), C(0x3FE8FF, 0.4f));
            k.Build("Body", t);
            var c = new MeshKit().Glossy(true);
            c.Push(Vector3.zero, Quaternion.Euler(90, 0, 0));
            c.Cylinder(Vector3.zero, 1.05f, 0.35f, 18, gold);
            c.Glossy(false);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45 * Mathf.Deg2Rad;
                c.Cone(new Vector3(Mathf.Cos(a) * 0.75f, 0.18f, Mathf.Sin(a) * 0.75f), 0.12f, 0.35f, 5, C(0xE8F4FF, 0.3f));
            }
            c.Cone(new Vector3(0, 0.18f, 0), 0.25f, 0.6f, 8, C(0xE8F4FF, 0.3f));
            c.Pop();
            var cutter = c.Build("Cutter", t).transform;
            cutter.localPosition = new Vector3(0, 0.8f, 0.55f);
            borers.Add(new Borer { T = t, Cutter = cutter, Ang = (float)rng.NextDouble() * 360, R = 3.5f + index * 1.2f, Dir = index % 2 == 0 ? 1 : -1 });
            return t;
        }

        protected override void AddCollider(Transform t) { }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (ctx.Sim.MallCleared) activity = 0;
            foreach (var b in borers)
            {
                b.Ang += dt * b.Dir * (4f + activity * 10f);
                float a = b.Ang * Mathf.Deg2Rad;
                float x = Mathf.Cos(a) * b.R, z = Mathf.Sin(a) * b.R;
                float y = ctx.Fountain.HeightAt(x, z);
                b.T.localPosition = new Vector3(x, y - 0.1f, z);
                b.T.localRotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(a) * b.Dir, 0, Mathf.Cos(a) * b.Dir));
                b.Cutter.Rotate(0, 0, dt * (90 + activity * 600), Space.Self);
                if (activity > 0.05f && Random.value < dt * 5)
                {
                    Vector3 front = b.T.TransformPoint(new Vector3(0, 0.3f, 1.0f));
                    ctx.Fountain.Dig(front, 0.15f, 1.3f);
                    ctx.Fx.Sparks(front, new Color(1f, 0.85f, 0.45f), 3, 4f);
                    ctx.Fx.Dust(front, ctx.Mall.Strata[ctx.Sim.Stratum].Color.ToColor(), 2, 0.8f, 0.6f, 1f);
                }
            }
        }
    }

    // ───────────────────────────── WASH ─────────────────────────────

    public sealed class TumblerVisual : MachineVisual
    {
        public override int MaxCopies => 6;
        readonly List<Transform> drums = new List<Transform>();
        readonly List<Transform> cranks = new List<Transform>();
        protected override void OnInit() { Anchor = Polar(95, 12.8f, 2.2f); }

        protected override Transform AddCopy(int index)
        {
            float ang = 76 + index * 7.5f;
            float r = 12.6f + (index % 2) * 0.4f;
            Vector3 pos = Polar(ang, r);
            var t = NewCopy("Tumbler " + index, pos, Face(pos) * Quaternion.Euler(0, 90, 0));
            Color frame = C(0xB8BCC4), red = C(0xD8283A);
            var k = new MeshKit();
            k.Box(new Vector3(-0.8f, 0.6f, 0), new Vector3(0.14f, 1.2f, 0.9f), frame);
            k.Box(new Vector3(0.8f, 0.6f, 0), new Vector3(0.14f, 1.2f, 0.9f), frame);
            k.Box(new Vector3(0, 0.08f, 0), new Vector3(1.8f, 0.16f, 1.1f), C(0x555A62));
            // upside-down two-litre bottle feeding the drum
            k.Cylinder(new Vector3(0.1f, 2.3f, 0), 0.22f, 0.75f, 12, C(0x4A2A1A, 0.1f));
            k.Frustum(new Vector3(0.1f, 1.8f, 0), 0.07f, 0.22f, 0.28f, 12, C(0x4A2A1A, 0.1f));
            k.Box(new Vector3(0.1f, 2.35f, 0), new Vector3(0.46f, 0.25f, 0.46f), red);
            k.Tube(new Vector3(0.1f, 1.66f, 0), new Vector3(0.1f, 1.45f, 0), 0.05f, 6, C(0x333333));
            k.Build("Frame", t);
            var d = new MeshKit();
            d.Push(Vector3.zero, Quaternion.Euler(0, 0, 90));
            d.Cylinder(Vector3.zero, 0.55f, 1.4f, 14, C(0xE8E8EC));
            d.Glossy(true);
            d.Torus(new Vector3(0, 0.55f, 0), 0.55f, 0.05f, 14, 4, red);
            d.Torus(new Vector3(0, -0.55f, 0), 0.55f, 0.05f, 14, 4, red);
            d.Glossy(false);
            for (int s = 0; s < 6; s++)
            {
                float a = s * 60 * Mathf.Deg2Rad;
                d.Box(new Vector3(Mathf.Cos(a) * 0.56f, 0, Mathf.Sin(a) * 0.56f), new Vector3(0.08f, 1.3f, 0.08f), C(0xAA2020));
            }
            d.Pop();
            var drum = d.Build("Drum", t).transform;
            drum.localPosition = new Vector3(0, 1.0f, 0);
            drums.Add(drum);
            var c = new MeshKit();
            c.Box(new Vector3(0, 0.2f, 0), new Vector3(0.05f, 0.45f, 0.06f), C(0x333333));
            c.Push(new Vector3(0.12f, 0.42f, 0), Quaternion.Euler(0, 0, 90));
            c.Cylinder(Vector3.zero, 0.05f, 0.25f, 6, red);
            c.Pop();
            var crank = c.Build("Crank", t).transform;
            crank.localPosition = new Vector3(0.9f, 1.0f, 0);
            cranks.Add(crank);
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            float spin = dt * (15 + activity * 280);
            foreach (var d in drums) d.Rotate(spin, 0, 0, Space.Self);
            foreach (var c in cranks) c.Rotate(spin, 0, 0, Space.Self);
            if (activity > 0.1f && Random.value < dt * 3 * activity && drums.Count > 0)
            {
                var d = drums[Random.Range(0, drums.Count)];
                ctx.Fx.Bubbles(d.position + Vector3.up * 0.5f, new Color(0.9f, 0.7f, 0.5f), 3, 0.4f, 0.14f);
            }
        }
    }

    public sealed class DishwasherVisual : MachineVisual
    {
        public override int MaxCopies => 4;
        readonly List<Transform> windows = new List<Transform>();

        protected override Transform AddCopy(int index)
        {
            float ang = 74 + index * 11f;
            Vector3 pos = Polar(ang, 15.9f);
            var t = NewCopy("Dishwasher " + index, pos, Face(pos));
            Color steel = C(0xC8CCD2), dark = C(0x3A3F48);
            var k = new MeshKit().Glossy(true);
            k.Box(new Vector3(0, 1.3f, 0), new Vector3(2.4f, 2.6f, 1.9f), steel);
            k.Glossy(false);
            k.Box(new Vector3(0, 0.1f, 0), new Vector3(2.5f, 0.2f, 2.0f), dark);
            k.Box(new Vector3(0, 2.75f, 0), new Vector3(1.0f, 0.3f, 1.0f), dark);
            k.Tube(new Vector3(0.6f, 2.9f, 0.3f), new Vector3(0.6f, 3.8f, 0.3f), 0.12f, 8, steel);
            k.Box(new Vector3(-1.3f, 1.0f, 0), new Vector3(0.3f, 0.4f, 1.6f), C(0x4A4F58));
            k.Box(new Vector3(0.75f, 2.2f, 0.96f), new Vector3(0.5f, 0.25f, 0.04f), C(0x7CF45A, 0.9f));
            k.Build("Body", t);
            var w = new MeshKit();
            w.Box(Vector3.zero, new Vector3(1.4f, 1.0f, 0.04f), C(0x5AC8FF, 0.8f));
            var win = w.Build("Window", t, false).transform;
            win.localPosition = new Vector3(-0.2f, 1.4f, 0.97f);
            windows.Add(win);
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            for (int i = 0; i < windows.Count; i++)
                windows[i].localScale = new Vector3(1, 0.9f + 0.1f * Mathf.Sin(time * (3 + activity * 8) + i), 1);
            if (activity > 0.1f && copies.Count > 0 && Random.value < dt * 2.5f * activity)
            {
                var c = copies[Random.Range(0, copies.Count)];
                ctx.Fx.Dust(c.TransformPoint(new Vector3(0.6f, 3.9f, 0.3f)), new Color(0.95f, 0.95f, 1f), 2, 1.1f, 0.3f, 1.4f);
            }
        }
    }

    public sealed class AcidVisual : MachineVisual
    {
        static readonly float[] PumpAngles = { 64, 122, 57, 129 };
        static readonly float[] PumpRadii = { 12.9f, 12.9f, 15.2f, 15.2f };
        public override int MaxCopies => 4;
        Transform river;
        Material acidMat;

        protected override void OnInit()
        {
            acidMat = new Material(Mats.Acid);
        }

        protected override Transform AddCopy(int index)
        {
            if (river == null)
            {
                var k = new MeshKit();
                Color channel = C(0x3A4A3A);
                float a0 = 58 * Mathf.Deg2Rad, a1 = 122 * Mathf.Deg2Rad;
                k.Wall(Vector3.zero, 9.65f, 0, 0.45f, 40, channel, false, a0, a1);
                k.Wall(Vector3.zero, 10.45f, 0, 0.45f, 40, channel, true, a0, a1);
                k.Push(new Vector3(0, 0.45f, 0));
                k.Ring(Vector3.zero, 9.58f, 9.68f, 40, C(0xF2C230), a0, a1);
                k.Ring(Vector3.zero, 10.42f, 10.52f, 40, C(0xF2C230), a0, a1);
                k.Pop();
                k.Build("Channel", Root);
                var s = new MeshKit();
                s.Push(new Vector3(0, 0.33f, 0));
                s.Ring(Vector3.zero, 9.65f, 10.45f, 40, Color.white, a0, a1);
                s.Pop();
                var go = s.Build("Acid", Root, false);
                // uv along the arc for scrolling
                var mesh = go.GetComponent<MeshFilter>().sharedMesh;
                var v = mesh.vertices;
                var uv = new Vector2[v.Length];
                for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(Mathf.Atan2(v[i].z, v[i].x) * 3f, (new Vector2(v[i].x, v[i].z).magnitude - 9.65f) * 1.2f);
                mesh.uv = uv;
                go.GetComponent<MeshRenderer>().sharedMaterial = acidMat;
                river = go.transform;
            }
            // pump station
            float ang = PumpAngles[index % PumpAngles.Length];
            Vector3 pos = Polar(ang, PumpRadii[index % PumpRadii.Length]);
            var t = NewCopy("Acid Pump " + index, pos, Face(pos));
            var p = new MeshKit();
            p.Cylinder(new Vector3(0, 1.1f, 0), 0.55f, 2.2f, 14, C(0x9FA8A0));
            p.Box(new Vector3(0, 2.3f, 0), new Vector3(0.9f, 0.3f, 0.9f), C(0x2A2A2A));
            p.Cylinder(new Vector3(0, 1.4f, 0.56f), 0.18f, 0.5f, 10, C(0x7CF45A, 0.9f));
            p.Tube(new Vector3(0, 1.8f, 0.3f), new Vector3(0, 0.9f, 1.4f), 0.12f, 8, C(0x5A6A5A));
            p.Box(new Vector3(0, 0.5f, -0.58f), new Vector3(0.6f, 0.4f, 0.04f), C(0xF2C230));
            p.Build("Pump", t);
            Anchor = Polar(90, 10.0f, 1);
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (acidMat != null) acidMat.SetFloat("_Speed", 0.15f + activity * 0.6f);
            if (river != null && activity > 0.05f && Random.value < dt * 10 * activity)
            {
                float a = Random.Range(58f, 122f);
                ctx.Fx.Bubbles(Polar(a, 10.05f, 0.4f), new Color(0.5f, 1f, 0.4f), 2, 0.25f, 0.2f);
            }
        }
    }

    public sealed class CarwashVisual : MachineVisual
    {
        public override int MaxCopies => 3;
        readonly List<Transform> brushes = new List<Transform>();

        protected override Transform AddCopy(int index)
        {
            float ang = 80 + index * 12;
            Vector3 pos = Polar(ang, 19.3f);
            var t = NewCopy("Car Wash " + index, pos, Face(pos) * Quaternion.Euler(0, 90, 0));
            Color arch = C(0x2A2340), neon = C(0x3FE8FF, 0.9f), pink = C(0xFF3FD0, 0.9f);
            var k = new MeshKit();
            k.Box(new Vector3(-1.6f, 1.8f, 0), new Vector3(0.4f, 3.6f, 2.6f), arch);
            k.Box(new Vector3(1.6f, 1.8f, 0), new Vector3(0.4f, 3.6f, 2.6f), arch);
            k.Box(new Vector3(0, 3.8f, 0), new Vector3(3.6f, 0.5f, 2.6f), arch);
            k.Box(new Vector3(0, 3.8f, -1.32f), new Vector3(3.4f, 0.3f, 0.05f), index % 2 == 0 ? pink : neon);
            k.Box(new Vector3(-1.4f, 1.8f, -1.32f), new Vector3(0.06f, 3.2f, 0.05f), neon);
            k.Box(new Vector3(1.4f, 1.8f, -1.32f), new Vector3(0.06f, 3.2f, 0.05f), neon);
            k.Box(new Vector3(0, 0.05f, 0), new Vector3(3.2f, 0.1f, 2.6f), C(0x444450));
            k.Build("Arch", t);
            for (int s = -1; s <= 1; s += 2)
            {
                var b = new MeshKit();
                b.Cylinder(Vector3.zero, 0.5f, 3.0f, 10, s < 0 ? C(0xFF3FD0, 0.35f) : C(0x3FE8FF, 0.35f));
                for (int f = 0; f < 10; f++)
                {
                    float a = f * 36 * Mathf.Deg2Rad;
                    b.Box(new Vector3(Mathf.Cos(a) * 0.55f, 0, Mathf.Sin(a) * 0.55f), new Vector3(0.12f, 2.9f, 0.12f), C(0xFFFFFF, 0.2f));
                }
                var br = b.Build("Brush", t).transform;
                br.localPosition = new Vector3(s * 0.9f, 1.8f, 0);
                brushes.Add(br);
            }
            WorldBuilder.MakeText(t, "WASH", new Vector3(0, 3.85f, -1.4f), Quaternion.identity, 0.5f, Color.white, 2.2f);
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            for (int i = 0; i < brushes.Count; i++) brushes[i].Rotate(0, (i % 2 == 0 ? 1 : -1) * dt * (40 + activity * 700), 0, Space.Self);
            if (activity > 0.1f && brushes.Count > 0 && Random.value < dt * 5 * activity)
                ctx.Fx.Bubbles(brushes[Random.Range(0, brushes.Count)].position, new Color(1f, 0.6f, 0.95f), 3, 0.5f, 0.25f);
        }
    }

    public sealed class JacuzziVisual : MachineVisual
    {
        static readonly float[] JacuzziAngles = { 64, 118, 54 };
        public override int MaxCopies => 3;

        protected override Transform AddCopy(int index)
        {
            float ang = JacuzziAngles[index % JacuzziAngles.Length];
            Vector3 pos = Polar(ang, 21.2f);
            var t = NewCopy("Jacuzzi " + index, pos, Face(pos));
            Color gold = C(0xD9B45A), marble = C(0xF4F1EA);
            var k = new MeshKit();
            k.Cylinder(new Vector3(0, 0.45f, 0), 1.7f, 0.9f, 24, marble);
            k.Glossy(true);
            k.Torus(new Vector3(0, 0.92f, 0), 1.7f, 0.12f, 24, 5, gold);
            k.Glossy(false);
            k.Push(new Vector3(0, 0.85f, 0));
            k.Ring(Vector3.zero, 0, 1.6f, 24, C(0xF2D98A, 0.8f));
            k.Pop();
            // champagne bottle fountain in the middle
            k.Cylinder(new Vector3(0, 1.4f, 0), 0.18f, 0.9f, 10, C(0x1F3A2A));
            k.Frustum(new Vector3(0, 2.0f, 0), 0.18f, 0.06f, 0.3f, 10, C(0x1F3A2A));
            k.Box(new Vector3(0, 1.4f, 0.17f), new Vector3(0.22f, 0.3f, 0.02f), C(0xF2E8C8));
            k.Build("Tub", t);
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (copies.Count == 0) return;
            if (Random.value < dt * (2 + activity * 14))
            {
                var c = copies[Random.Range(0, copies.Count)];
                ctx.Fx.Bubbles(c.position + Vector3.up * 0.9f, new Color(1f, 0.92f, 0.6f), 3, 1.2f, 0.16f);
                if (activity > 0.2f && Random.value < 0.3f) ctx.Fx.Sparks(c.position + Vector3.up * 2.2f, new Color(1f, 0.95f, 0.7f), 3, 2.2f, 0.08f, 0.8f);
            }
        }
    }

    // ───────────────────────────── SORT ─────────────────────────────

    public sealed class PigeonVisual : MachineVisual
    {
        public override int MaxCopies => 4;
        readonly List<Pigeon> birds = new List<Pigeon>();
        readonly List<Transform> perches = new List<Transform>();
        int birdsShown;

        protected override Transform AddCopy(int index)
        {
            float ang = 246 + index * 8.5f;
            Vector3 pos = Polar(ang, 12.9f + (index % 2) * 0.3f);
            var t = NewCopy("Pigeon Perch " + index, pos, Face(pos) * Quaternion.Euler(0, 90, 0));
            Color wood = C(0x9A7048), table = C(0xC8B89A);
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.75f, 0), new Vector3(2.2f, 0.1f, 1.2f), table);
            for (int i = 0; i < 4; i++) k.Box(new Vector3((i % 2 == 0 ? -1 : 1) * 1.0f, 0.37f, (i < 2 ? -1 : 1) * 0.5f), new Vector3(0.08f, 0.74f, 0.08f), wood);
            k.Box(new Vector3(0, 0.85f, 0), new Vector3(2.0f, 0.1f, 0.06f), C(0xE8505B));
            k.Tube(new Vector3(-1.05f, 0.8f, 0), new Vector3(-1.05f, 2.1f, 0), 0.05f, 6, wood);
            k.Tube(new Vector3(1.05f, 0.8f, 0), new Vector3(1.05f, 2.1f, 0), 0.05f, 6, wood);
            k.Tube(new Vector3(-1.1f, 2.1f, 0), new Vector3(1.1f, 2.1f, 0), 0.045f, 6, wood);
            // pretzel crumbs payment bowl
            k.Cylinder(new Vector3(0.8f, 0.85f, 0.35f), 0.18f, 0.08f, 10, C(0xE8E8E8));
            k.Ellipsoid(new Vector3(0.8f, 0.9f, 0.35f), new Vector3(0.14f, 0.05f, 0.14f), 3, 8, C(0xC9955C));
            k.Build("Perch", t);
            perches.Add(t);
            return t;
        }

        protected override void OnCountChanged(int count)
        {
            int want = Mathf.Min(count * 2, perches.Count * 6);
            while (birdsShown < want && perches.Count > 0)
            {
                var perch = perches[(birdsShown / 6) % perches.Count];
                int slot = birdsShown % 6;
                var pg = Actors.MakePigeon(perch, 1.6f, rng);
                bool onTable = slot % 2 == 1;
                pg.Root.localPosition = onTable ? new Vector3(-0.8f + slot * 0.3f, 0.8f, (float)rng.NextDouble() * 0.6f - 0.3f)
                                                : new Vector3(-0.9f + slot * 0.36f, 2.12f, 0);
                pg.Root.localRotation = Quaternion.Euler(0, onTable ? (float)rng.NextDouble() * 360 : 180 + (float)rng.NextDouble() * 20 - 10, 0);
                birds.Add(pg);
                birdsShown++;
            }
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            foreach (var b in birds) Actors.AnimatePigeon(b, time, Mathf.Max(0.15f, activity));
        }
    }

    public sealed class CoinStarVisual : MachineVisual
    {
        public override int MaxCopies => 4;
        readonly List<Transform> sliders = new List<Transform>();

        protected override Transform AddCopy(int index)
        {
            float ang = 246 + index * 9f;
            Vector3 pos = Polar(ang, 15.9f);
            var t = NewCopy("Coin Star " + index, pos, Face(pos));
            Color green = C(0x1F9F5F), dark = C(0x2A2F36);
            var k = new MeshKit();
            k.Box(new Vector3(0, 1.1f, 0), new Vector3(1.3f, 2.2f, 1.0f), green);
            k.Box(new Vector3(0, 2.35f, 0), new Vector3(1.4f, 0.3f, 1.1f), C(0xFFE066, 0.8f));
            k.Box(new Vector3(0, 1.55f, 0.51f), new Vector3(0.8f, 0.5f, 0.04f), C(0x5AC8FF, 0.6f));
            k.Push(new Vector3(0, 1.05f, 0.75f), Quaternion.Euler(-25, 0, 0));
            k.Box(Vector3.zero, new Vector3(1.1f, 0.06f, 0.7f), C(0xC0C4CA));
            k.Pop();
            for (int i = 0; i < 4; i++) k.Box(new Vector3(-0.45f + i * 0.3f, 0.35f, 0.55f), new Vector3(0.22f, 0.4f, 0.15f), dark);
            k.Build("Kiosk", t);
            WorldBuilder.MakeText(t, "COIN STAR", new Vector3(0, 2.35f, 0.57f), Quaternion.Euler(0, 180, 0), 0.2f, C(0x1F3F2F), 1.2f);
            for (int c = 0; c < 3; c++)
            {
                var coin = Loot.MakeItemMesh(ItemShape.Coin, c == 0 ? C(0xC77B43) : C(0xD4D8DC), 1.4f, t);
                sliders.Add(coin.transform);
            }
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            for (int i = 0; i < sliders.Count; i++)
            {
                float p = (time * (0.3f + activity * 1.2f) + i * 0.33f) % 1f;
                sliders[i].localPosition = new Vector3(-0.4f + (i % 3) * 0.4f, Mathf.Lerp(1.25f, 0.85f, p), Mathf.Lerp(0.5f, 1.05f, p));
                sliders[i].localRotation = Quaternion.Euler(-25, time * 200 + i * 40, 0);
            }
        }
    }

    public sealed class LaserVisual : MachineVisual
    {
        public override int MaxCopies => 3;
        readonly List<Transform> beams = new List<Transform>();
        readonly List<Transform> grabbers = new List<Transform>();

        protected override Transform AddCopy(int index)
        {
            float ang = 206 + index * 9f;
            Vector3 pos = Polar(ang, 13.6f);
            var t = NewCopy("Laser Scanner " + index, pos, Face(pos) * Quaternion.Euler(0, 90, 0));
            Color frame = C(0xB8BEC8), belt = C(0x4A4F58);
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.45f, 0), new Vector3(3.0f, 0.9f, 1.0f), belt);
            k.Box(new Vector3(-1.5f, 1.5f, 0), new Vector3(0.16f, 3.0f, 0.3f), frame);
            k.Box(new Vector3(1.5f, 1.5f, 0), new Vector3(0.16f, 3.0f, 0.3f), frame);
            k.Box(new Vector3(0, 3.05f, 0), new Vector3(3.2f, 0.25f, 0.5f), frame);
            for (int c = 0; c < 3; c++) k.Box(new Vector3(-0.9f + c * 0.9f, 2.85f, 0), new Vector3(0.3f, 0.22f, 0.3f), C(0xFF3048, 0.9f));
            k.Build("Gantry", t);
            var belts = new MeshKit();
            belts.Box(new Vector3(0, 0.92f, 0), new Vector3(3.0f, 0.04f, 1.0f), Color.white);
            belts.Build("Belt", t).GetComponent<MeshRenderer>().sharedMaterial = Mats.Belt;
            for (int c = 0; c < 3; c++)
            {
                var b = new MeshKit();
                b.Box(new Vector3(0, -0.9f, 0), new Vector3(0.04f, 1.8f, 0.04f), new Color(1f, 0.2f, 0.25f, 1f));
                var beam = b.Build("Beam", t, false).transform;
                beam.localPosition = new Vector3(-0.9f + c * 0.9f, 2.75f, 0);
                beams.Add(beam);
            }
            var g = new MeshKit();
            g.Tube(Vector3.zero, new Vector3(0, -1.2f, 0), 0.07f, 6, C(0xC8CCD2));
            g.Box(new Vector3(0, -1.3f, 0), new Vector3(0.3f, 0.15f, 0.15f), C(0xF2C230));
            var grab = g.Build("Grabber", t).transform;
            grab.localPosition = new Vector3(0, 2.9f, 0.5f);
            grabbers.Add(grab);
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            for (int i = 0; i < beams.Count; i++)
            {
                float sweep = Mathf.Sin(time * (2f + activity * 5f) + i * 1.3f) * 35f;
                beams[i].localRotation = Quaternion.Euler(sweep, 0, 0);
                beams[i].gameObject.SetActive(activity > 0.03f || (time % 2f) < 1f);
            }
            for (int i = 0; i < grabbers.Count; i++)
            {
                float t = time * (0.8f + activity * 2.5f) + i;
                grabbers[i].localPosition = new Vector3(Mathf.Sin(t) * 1.1f, 2.9f, 0.4f);
                grabbers[i].localRotation = Quaternion.Euler(Mathf.Abs(Mathf.Sin(t * 2)) * 40 - 20, 0, 0);
            }
            if (activity > 0.2f && copies.Count > 0 && Random.value < dt * 3 * activity)
            {
                var c = copies[Random.Range(0, copies.Count)];
                ctx.Fx.Sparks(c.TransformPoint(new Vector3(Random.Range(-1f, 1f), 1.0f, 0)), new Color(1f, 0.25f, 0.3f), 5, 2.5f, 0.08f, 0.35f);
            }
        }
    }

    public sealed class PrizeBotVisual : MachineVisual
    {
        public override int MaxCopies => 4;
        readonly List<Transform> arms = new List<Transform>();

        protected override Transform AddCopy(int index)
        {
            float ang = 244 + index * 8.5f;
            Vector3 pos = Polar(ang, 19.3f);
            var t = NewCopy("Prize Bot " + index, pos, Face(pos));
            Color body = C(0xE8E8EC), red = C(0xE84F4F);
            var k = new MeshKit();
            k.Box(new Vector3(0, 0.55f, 0.9f), new Vector3(2.2f, 1.1f, 0.5f), C(0x3A2A6A));
            k.Box(new Vector3(0, 1.12f, 0.9f), new Vector3(2.3f, 0.06f, 0.6f), C(0xFFE066, 0.9f));
            for (int p = 0; p < 4; p++) k.Sphere(new Vector3(-0.8f + p * 0.55f, 1.35f, 0.95f), 0.2f, 5, 8, C(p % 2 == 0 ? 0xFF7AB8 : 0x7CF4A0));
            k.Box(new Vector3(0, 0.9f, 0), new Vector3(0.8f, 1.2f, 0.6f), body);
            k.Box(new Vector3(0, 1.75f, 0), new Vector3(0.6f, 0.5f, 0.5f), body);
            k.Box(new Vector3(0, 1.78f, 0.26f), new Vector3(0.45f, 0.14f, 0.02f), C(0x3FE8FF, 1f));
            k.Tube(new Vector3(0, 2.0f, 0), new Vector3(0, 2.35f, 0), 0.02f, 4, C(0x999999));
            k.Sphere(new Vector3(0, 2.38f, 0), 0.06f, 3, 5, red * 1f);
            k.Build("Robot", t);
            for (int s = -1; s <= 1; s += 2)
            {
                var a = new MeshKit();
                a.Tube(Vector3.zero, new Vector3(0, -0.7f, 0.25f), 0.07f, 6, body);
                a.Box(new Vector3(0, -0.78f, 0.3f), new Vector3(0.2f, 0.12f, 0.12f), red);
                var arm = a.Build("Arm", t).transform;
                arm.localPosition = new Vector3(s * 0.48f, 1.4f, 0);
                arms.Add(arm);
            }
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            for (int i = 0; i < arms.Count; i++)
            {
                float t = time * (1.2f + activity * 4f) + i * 1.1f;
                arms[i].localRotation = Quaternion.Euler(-40 - Mathf.Abs(Mathf.Sin(t)) * 70 * Mathf.Max(0.2f, activity), 0, 0);
            }
        }
    }

    public sealed class SieveVisual : MachineVisual
    {
        public override int MaxCopies => 3;
        readonly List<Transform> disks = new List<Transform>();

        protected override Transform AddCopy(int index)
        {
            float ang = 282 + index * 10f;
            Vector3 pos = Polar(ang, 19.6f);
            var t = NewCopy("Quantum Sieve " + index, pos, Face(pos));
            var k = new MeshKit();
            k.Cylinder(new Vector3(0, 0.3f, 0), 1.2f, 0.6f, 20, C(0x2A2F3A));
            k.Tube(new Vector3(-1f, 0.6f, 0), new Vector3(-1f, 3.2f, 0), 0.12f, 8, C(0x8A8FA0));
            k.Tube(new Vector3(1f, 0.6f, 0), new Vector3(1f, 3.2f, 0), 0.12f, 8, C(0x8A8FA0));
            k.Build("Base", t);
            var d = new MeshKit();
            d.Push(Vector3.zero, Quaternion.Euler(90, 0, 0));
            d.Torus(Vector3.zero, 1.2f, 0.1f, 24, 5, C(0xB57CFF, 0.9f));
            d.Torus(Vector3.zero, 0.8f, 0.05f, 20, 4, C(0x3FE8FF, 0.9f));
            for (int s = 0; s < 6; s++)
            {
                d.Push(Vector3.zero, Quaternion.Euler(0, s * 30, 0));
                d.Box(Vector3.zero, new Vector3(2.3f, 0.03f, 0.03f), C(0x9FF0FF, 0.7f));
                d.Pop();
            }
            d.Pop();
            var disk = d.Build("Lattice", t, false).transform;
            disk.localPosition = new Vector3(0, 2.2f, 0);
            disks.Add(disk);
            return t;
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            for (int i = 0; i < disks.Count; i++)
            {
                disks[i].Rotate(0, 0, dt * (20 + activity * 180) * (i % 2 == 0 ? 1 : -1), Space.Self);
                disks[i].localScale = Vector3.one * (1 + Mathf.Sin(time * 4 + i) * 0.04f * (0.3f + activity));
            }
            if (activity > 0.1f && disks.Count > 0 && Random.value < dt * 4 * activity)
                ctx.Fx.Glint(disks[Random.Range(0, disks.Count)].position + Random.insideUnitSphere * 0.8f, new Color(0.7f, 0.6f, 1f), 0.7f);
        }
    }

    // ───────────────────────────── WISHES & MEGAPROJECTS ─────────────────────────────

    public sealed class CompressorVisual : MachineVisual
    {
        public override int MaxCopies => 1;
        Transform piston, bricks;
        int brickCount;
        public Vector3 Intake { get; private set; }

        protected override Transform AddCopy(int index)
        {
            Vector3 pos = Polar(40, 16.2f);
            var t = NewCopy("Wish Compressor", pos, Face(pos));
            Color purple = C(0x7A3FC8), steel = C(0xC8CCD2);
            var k = new MeshKit();
            k.Cylinder(new Vector3(0, 1.6f, 0), 1.1f, 3.2f, 20, steel);
            k.Torus(new Vector3(0, 0.9f, 0), 1.12f, 0.08f, 20, 4, purple);
            k.Torus(new Vector3(0, 2.3f, 0), 1.12f, 0.08f, 20, 4, purple);
            k.Frustum(new Vector3(0, 3.8f, 0), 0.5f, 1.6f, 1.2f, 20, C(0x5A5F68));
            k.Box(new Vector3(0, 1.8f, 1.12f), new Vector3(0.9f, 0.6f, 0.05f), C(0xB57CFF, 1f));
            k.Box(new Vector3(0, 0.25f, 1.9f), new Vector3(1.6f, 0.2f, 1.2f), C(0x8A6A4A));
            k.Build("Body", t);
            var p = new MeshKit();
            p.Cylinder(Vector3.zero, 0.35f, 1.2f, 12, C(0xB57CFF, 0.5f));
            piston = p.Build("Piston", t).transform;
            piston.localPosition = new Vector3(0, 3.2f, 0);
            bricks = new GameObject("Bricks").transform;
            bricks.SetParent(t, false);
            bricks.localPosition = new Vector3(0, 0.35f, 1.9f);
            Intake = pos + Vector3.up * 4.5f;
            Anchor = Intake;
            return t;
        }

        protected override void OnCountChanged(int count)
        {
            int want = Mathf.Min(24, count * 2);
            while (brickCount < want && bricks != null)
            {
                var k = new MeshKit();
                k.Box(Vector3.zero, new Vector3(0.44f, 0.2f, 0.22f), C(0x9A5FE8, 0.35f));
                var b = k.Build("Brick", bricks).transform;
                int layer = brickCount / 6, slot = brickCount % 6;
                b.localPosition = new Vector3(-0.5f + (slot % 3) * 0.48f, layer * 0.21f + 0.1f, -0.25f + (slot / 3) * 0.5f);
                brickCount++;
            }
        }

        public void Pulse() { if (piston != null) piston.localScale = new Vector3(1.3f, 0.6f, 1.3f); }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (piston != null) piston.localScale = Vector3.Lerp(piston.localScale, Vector3.one, dt * 6);
        }
    }

    public sealed class CarouselVisual : MachineVisual
    {
        public override int MaxCopies => 1;
        Transform ring;
        readonly List<Transform> bags = new List<Transform>();
        int level;

        protected override Transform AddCopy(int index)
        {
            var t = NewCopy("Baggage Carousel", Vector3.zero, Quaternion.identity);
            var k = new MeshKit();
            k.Wall(Vector3.zero, 25.5f, 0, 0.7f, 96, C(0x8A8F96), true);
            k.Wall(Vector3.zero, 27.5f, 0, 0.7f, 96, C(0x8A8F96), false);
            k.Build("Rails", t);
            var s = new MeshKit();
            s.Push(new Vector3(0, 0.72f, 0));
            s.Ring(Vector3.zero, 25.5f, 27.5f, 96, Color.white);
            s.Pop();
            var go = s.Build("Plates", t, false);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            var v = mesh.vertices;
            var uv = new Vector2[v.Length];
            for (int i = 0; i < v.Length; i++) uv[i] = new Vector2(Mathf.Atan2(v[i].z, v[i].x) * 30f, new Vector2(v[i].x, v[i].z).magnitude);
            mesh.uv = uv;
            go.GetComponent<MeshRenderer>().sharedMaterial = Mats.ScrollCustom(TexKit.BeltStripes, new Color(0.7f, 0.72f, 0.78f), 0.4f, 0);
            ring = new GameObject("Bags").transform;
            ring.SetParent(t, false);
            return t;
        }

        protected override void AddCollider(Transform t)
        {
            var go = new GameObject("Click");
            go.transform.SetParent(t, false);
            go.transform.localPosition = Polar(-90, 26.5f, 0.7f);
            var box = go.AddComponent<BoxCollider>();
            box.size = new Vector3(6, 1.5f, 2.5f);
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "machine";
            ct.Id = Def.Id;
        }

        protected override void OnCountChanged(int count)
        {
            level = count;
            int want = Mathf.Min(40, 8 + count * 3);
            uint[] colors = { 0x2F6FD6, 0xE84F4F, 0x3A3A3A, 0xF2B233, 0x5FB8A8, 0xB57CD8, 0xE8E8E8 };
            while (bags.Count < want && ring != null)
            {
                var k = new MeshKit();
                Color c = C(colors[rng.Next(colors.Length)]);
                k.Box(new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.6f, 0.5f), c);
                k.Box(new Vector3(0, 0.7f, 0), new Vector3(0.3f, 0.08f, 0.08f), C(0x222222));
                var b = k.Build("Bag", ring).transform;
                float a = bags.Count * (360f / 40) * Mathf.Deg2Rad;
                b.localPosition = new Vector3(Mathf.Cos(a) * 26.5f, 0.74f, Mathf.Sin(a) * 26.5f);
                b.localRotation = Quaternion.Euler(0, -a * Mathf.Rad2Deg + (float)rng.NextDouble() * 30, 0);
                bags.Add(b);
            }
        }

        protected override void OnTick(float dt, float time, float activity)
        {
            if (ring != null) ring.Rotate(0, dt * (4 + level * 0.8f), 0, Space.Self);
        }
    }

    public sealed class SlotsVisual : MachineVisual
    {
        public override int MaxCopies => 1;
        readonly List<Transform> reels = new List<Transform>();
        Transform lever;
        float pull;

        protected override Transform AddCopy(int index)
        {
            Vector3 pos = new Vector3(0, 0, 24.8f);
            var t = NewCopy("Slot Machine of Fortune", pos, Quaternion.Euler(0, 180, 0));
            Color red = C(0xB0102A), gold = C(0xFFD34D);
            var k = new MeshKit();
            k.Box(new Vector3(0, 4, 0), new Vector3(9, 8, 3), red);
            k.Box(new Vector3(0, 8.4f, 0), new Vector3(9.4f, 0.8f, 3.2f), C(0xFFD34D, 0.9f));
            k.Box(new Vector3(0, 4.6f, -1.52f), new Vector3(7.4f, 3.0f, 0.1f), C(0x111111));
            k.Box(new Vector3(0, 1.4f, -1.6f), new Vector3(6f, 0.6f, 0.4f), gold);
            for (int i = 0; i < 20; i++)
            {
                float x = -4.3f + (i % 10) * 0.95f, y = i < 10 ? 7.7f : 0.3f;
                k.Sphere(new Vector3(x, y, -1.55f), 0.16f, 3, 6, C(i % 2 == 0 ? 0xFFE066 : 0xFF3048, 1f));
            }
            k.Build("Cabinet", t);
            WorldBuilder.MakeText(t, "FORTUNE", new Vector3(0, 8.4f, -1.65f), Quaternion.identity, 1.1f, C(0xB0102A), 1.3f, WorldBuilder.SignFontAlt);
            for (int r = 0; r < 3; r++)
            {
                var rk = new MeshKit();
                rk.Push(Vector3.zero, Quaternion.Euler(0, 0, 90));
                rk.Cylinder(Vector3.zero, 1.25f, 2.0f, 12, C(0xF8F4EC, 0.35f));
                rk.Pop();
                string[] sym = { "7", "$", "7" };
                var reel = rk.Build("Reel", t).transform;
                reel.localPosition = new Vector3(-2.3f + r * 2.3f, 4.6f, -0.2f);
                reels.Add(reel);
                WorldBuilder.MakeText(t, sym[r], new Vector3(-2.3f + r * 2.3f, 4.6f, -1.6f), Quaternion.identity, 1.4f, C(0xB0102A), 1.2f, WorldBuilder.SignFontAlt);
            }
            var l = new MeshKit();
            l.Tube(Vector3.zero, new Vector3(0, 3.2f, 0), 0.15f, 8, C(0xC8CCD2));
            l.Sphere(new Vector3(0, 3.3f, 0), 0.4f, 6, 10, C(0xFF3048, 0.4f));
            lever = l.Build("Lever", t).transform;
            lever.localPosition = new Vector3(4.9f, 3.5f, 0);
            return t;
        }

        public void Pull() { pull = 1; }

        protected override void OnTick(float dt, float time, float activity)
        {
            foreach (var r in reels) r.Rotate(dt * (60 + activity * 300), 0, 0, Space.Self);
            pull = Mathf.Max(0, pull - dt * 1.5f);
            if (lever != null) lever.localRotation = Quaternion.Euler(Mathf.Sin(pull * Mathf.PI) * 60, 0, 0);
        }
    }

    public sealed class WishEngineVisual : MachineVisual
    {
        public override int MaxCopies => 1;
        readonly List<Transform> rings = new List<Transform>();
        int level;

        protected override Transform AddCopy(int index)
        {
            var t = NewCopy("The Wish Engine", new Vector3(0, 11.5f, 0), Quaternion.identity);
            for (int i = 0; i < 3; i++)
            {
                var k = new MeshKit();
                float r = 3.2f + i * 1.3f;
                k.Torus(Vector3.zero, r, 0.14f, 48, 6, C(i == 1 ? 0x3FE0D0 : 0xFFD34D, 0.6f));
                for (int g = 0; g < 12; g++)
                {
                    float a = g * 30 * Mathf.Deg2Rad;
                    k.Box(new Vector3(Mathf.Cos(a) * r, 0, Mathf.Sin(a) * r), new Vector3(0.35f, 0.35f, 0.35f), C(0xC8A050));
                }
                var ring = k.Build("Ring", t, false).transform;
                ring.localRotation = Quaternion.Euler(i * 25, i * 40, 0);
                rings.Add(ring);
            }
            var core = new MeshKit();
            core.Sphere(Vector3.zero, 1.0f, 10, 16, C(0x9FF0FF, 1f));
            core.Build("Core", t, false);
            return t;
        }

        protected override void AddCollider(Transform t)
        {
            var go = new GameObject("Click");
            go.transform.SetParent(t, false);
            var sc = go.AddComponent<SphereCollider>();
            sc.radius = 2.2f;
            var ct = go.AddComponent<ClickTarget>();
            ct.Kind = "machine";
            ct.Id = Def.Id;
        }

        protected override void OnCountChanged(int count) { level = count; }

        protected override void OnTick(float dt, float time, float activity)
        {
            for (int i = 0; i < rings.Count; i++)
                rings[i].Rotate((i + 1) * 7 * dt, (12 + level) * dt * (i % 2 == 0 ? 1 : -1), 0, Space.Self);
            if (copies.Count > 0)
            {
                copies[0].localPosition = new Vector3(0, 11.5f + Mathf.Sin(time * 0.8f) * 0.4f, 0);
                if (Random.value < dt * 3) ctx.Fx.Glint(copies[0].position + Random.insideUnitSphere * 3f, new Color(0.6f, 1f, 1f), 1.0f);
            }
        }
    }

    public static class ColorExt
    {
        public static Color ToColor(this uint hex) => MeshKit.Hex(hex);
    }
}
