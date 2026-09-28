// The fountain basin: rim, mosaic walls (which reveal faint bands of every stratum you dig
// through), the tiered centrepiece, and the crust heightfield that sinks with depth and
// dents where you click. Loose junk props sit on the crust and get swapped per stratum.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class FountainView
    {
        public const float RimInner = 8.0f, RimOuter = 9.4f, RimTop = 0.9f;
        public const float BasinFloor = -7.0f, CrustTop = 0.55f;
        const int Rings = 38, Segs = 104;
        const float InnerR = 0.72f, OuterR = 7.97f;

        public Transform Root { get; private set; }
        public float SurfaceY { get; private set; } = CrustTop;
        float targetY = CrustTop;
        bool cleared;
        float clearedBlend;

        Mesh crustMesh;
        Material crustMat;
        Vector3[] verts;
        Color[] cols;
        float[] bump, dent, radius, angle;
        bool dirty = true;
        MallDef mall;
        int stratum = -1;
        Color baseCol, speckCol, baseTarget, speckTarget;
        float coinAmount = 0.7f;

        readonly List<Prop> props = new List<Prop>();
        readonly System.Random rng = new System.Random(11);
        Transform propRoot;

        sealed class Prop
        {
            public Transform T;
            public float X, Z, Sink, Tilt, Yaw, Life, Fade;
            public bool Dying;
        }

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        public void Build(MallDef m, Transform parent)
        {
            mall = m;
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = new GameObject("Fountain").transform;
            Root.SetParent(parent, false);
            props.Clear();
            propRoot = new GameObject("Crust Props").transform;
            propRoot.SetParent(Root, false);
            BuildRim(m.Theme);
            BuildWall(m);
            BuildCenterpiece(m.Theme);
            BuildFloor();
            BuildCrust();
            BuildWorkLight(m.Theme);
            stratum = -1;
            dirty = true;
        }

        Light workLight;

        // A floodlight hung over the dig site so the crust stays readable as it sinks below the rim's shadow.
        void BuildWorkLight(ThemeDef th)
        {
            var go = new GameObject("Work Light");
            go.transform.SetParent(Root, false);
            workLight = go.AddComponent<Light>();
            workLight.type = LightType.Point;
            workLight.range = 24f;
            workLight.intensity = th.Night ? 1.5f : 1.05f;
            workLight.color = Color.Lerp(C(th.LightColor), Color.white, 0.4f);
            workLight.shadows = LightShadows.None;
            workLight.renderMode = LightRenderMode.ForcePixel;
            go.transform.localPosition = new Vector3(0, SurfaceY + 9f, -1.5f);
        }

        void BuildRim(ThemeDef th)
        {
            var k = new MeshKit();
            Color rim = C(th.Rim), rimTop = Color.Lerp(C(th.Rim), Color.white, 0.18f), trim = C(th.WallTrim);
            rimTop.a = 0;
            k.Wall(Vector3.zero, RimOuter, 0, RimTop, 96, rim, false);
            k.Push(new Vector3(0, RimTop, 0));
            k.Ring(Vector3.zero, RimInner - 0.05f, RimOuter + 0.08f, 96, rimTop);
            k.Pop();
            k.Wall(Vector3.zero, RimOuter + 0.08f, RimTop - 0.12f, RimTop, 96, trim, false);
            k.Wall(Vector3.zero, RimInner - 0.05f, RimTop - 0.2f, RimTop, 96, rim, true);
            // little brass coin-slot plaques around the rim
            for (int i = 0; i < 8; i++)
            {
                float a = (i * 45 + 22.5f) * Mathf.Deg2Rad;
                Vector3 p = new Vector3(Mathf.Cos(a) * (RimOuter + 0.02f), RimTop * 0.55f, Mathf.Sin(a) * (RimOuter + 0.02f));
                k.Push(p, Quaternion.LookRotation(new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))));
                k.Box(Vector3.zero, new Vector3(0.7f, 0.3f, 0.04f), C(0xC9A64A));
                k.Pop();
            }
            k.Build("Rim", Root);
        }

        void BuildWall(MallDef m)
        {
            var th = m.Theme;
            var k = new MeshKit();
            Color a = C(th.BasinTileA), b = C(th.BasinTileB);
            int seg = 96;
            float rowH = 0.32f;
            int rows = Mathf.CeilToInt((RimTop - 0.2f - BasinFloor) / rowH);
            for (int r = 0; r < rows; r++)
            {
                float y1 = RimTop - 0.2f - r * rowH, y0 = y1 - rowH;
                if (y0 < BasinFloor) y0 = BasinFloor;
                float depthFrac = Mathf.Clamp01((CrustTop - (y0 + y1) * 0.5f) / (CrustTop - BasinFloor));
                // which stratum lived at this height: tint the tiles with its ghost
                Color ghost = Color.clear;
                float ghostAmt = 0;
                if (depthFrac > 0)
                {
                    int s = 0;
                    for (int i = 0; i < m.Strata.Length; i++) if (depthFrac >= m.Strata[i].StartFrac) s = i;
                    ghost = C(m.Strata[s].Color);
                    ghostAmt = 0.45f;
                }
                for (int i = 0; i < seg; i++)
                {
                    bool alt = ((r / 2) + i) % 2 == 0;
                    bool band = r % 9 == 4;
                    Color c = band ? C(th.WallTrim) : (alt ? a : b);
                    float j = 0.9f + 0.12f * TexKit.Hash(i, r, 3);
                    c = new Color(c.r * j, c.g * j, c.b * j);
                    if (ghostAmt > 0) c = Color.Lerp(c, ghost * 0.85f, ghostAmt * (0.75f + 0.25f * TexKit.Hash(i, r, 9)));
                    // water line grime just under the old surface
                    if (y1 < CrustTop + 0.3f && y1 > CrustTop - 0.6f) c *= 0.72f;
                    c.a = 0;
                    float a0 = i * Mathf.PI * 2 / seg + 0.002f, a1 = (i + 1) * Mathf.PI * 2 / seg - 0.002f;
                    k.Wall(Vector3.zero, RimInner, y0 + 0.012f, y1 - 0.012f, 1, c, true, a0, a1);
                }
            }
            // grout backing
            k.Wall(Vector3.zero, RimInner + 0.01f, BasinFloor, RimTop, 64, new Color(0.35f, 0.34f, 0.32f, 0), true);
            k.Build("Basin Wall", Root);
        }

        void BuildCenterpiece(ThemeDef th)
        {
            var k = new MeshKit();
            Color stone = Color.Lerp(C(th.Rim), Color.white, 0.12f), trim = C(th.WallTrim), gunk = C(0x6B5A3A);
            stone.a = 0;
            k.Cylinder(new Vector3(0, (BasinFloor + 1.6f) / 2, 0), 0.55f, 1.6f - BasinFloor, 18, stone);
            for (float y = BasinFloor + 1f; y < 1.4f; y += 1.6f) k.Torus(new Vector3(0, y, 0), 0.57f, 0.06f, 18, 5, trim);
            // lower bowl
            k.Frustum(new Vector3(0, 1.72f, 0), 0.6f, 1.85f, 0.45f, 24, stone, true);
            k.Torus(new Vector3(0, 1.95f, 0), 1.85f, 0.09f, 28, 6, trim);
            k.Ellipsoid(new Vector3(0, 1.98f, 0), new Vector3(1.55f, 0.18f, 1.55f), 5, 16, gunk);
            k.Cylinder(new Vector3(0, 2.45f, 0), 0.34f, 1.0f, 14, stone);
            // upper bowl
            k.Frustum(new Vector3(0, 3.05f, 0), 0.38f, 1.05f, 0.3f, 20, stone, true);
            k.Torus(new Vector3(0, 3.2f, 0), 1.05f, 0.06f, 22, 5, trim);
            k.Ellipsoid(new Vector3(0, 3.22f, 0), new Vector3(0.85f, 0.12f, 0.85f), 4, 12, gunk);
            k.Cylinder(new Vector3(0, 3.45f, 0), 0.16f, 0.5f, 10, stone);
            // wishing star finial
            Color star = C(th.NeonB, 0.9f);
            k.Push(new Vector3(0, 4.0f, 0), Quaternion.identity);
            for (int i = 0; i < 5; i++)
            {
                var r = Quaternion.Euler(0, 0, i * 72);
                k.Push(Vector3.zero, r);
                k.Cone(Vector3.zero, 0.12f, 0.5f, 4, star);
                k.Pop();
            }
            k.Sphere(Vector3.zero, 0.16f, 5, 8, star);
            k.Pop();
            k.Build("Centerpiece", Root);
        }

        void BuildFloor()
        {
            var k = new MeshKit();
            k.Push(new Vector3(0, BasinFloor, 0));
            k.Ring(Vector3.zero, 0.55f, RimInner, 64, new Color(0.7f, 0.69f, 0.66f, 0));
            k.Ring(new Vector3(0, 0.01f, 0), 0.55f, 1.3f, 32, new Color(0.25f, 0.25f, 0.26f, 0));
            k.Pop();
            k.Build("Basin Floor", Root, false);
        }

        void BuildCrust()
        {
            int n = (Rings + 1) * (Segs + 1);
            verts = new Vector3[n];
            cols = new Color[n];
            bump = new float[n];
            dent = new float[n];
            radius = new float[n];
            angle = new float[n];
            var uv = new Vector2[n];
            var tris = new List<int>(Rings * Segs * 6);
            for (int i = 0; i <= Rings; i++)
            {
                float t = i / (float)Rings;
                float r = Mathf.Lerp(InnerR, OuterR, Mathf.Pow(t, 0.9f));
                for (int j = 0; j <= Segs; j++)
                {
                    float a = j / (float)Segs * Mathf.PI * 2;
                    int idx = i * (Segs + 1) + j;
                    radius[idx] = r;
                    angle[idx] = a;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    float nb = (Mathf.PerlinNoise(x * 0.23f + 13, z * 0.23f + 7) - 0.5f) * 0.55f
                             + (Mathf.PerlinNoise(x * 0.9f + 3, z * 0.9f + 9) - 0.5f) * 0.18f;
                    float mound = 0.22f * (1 - (r / OuterR) * (r / OuterR));
                    float edge = 0.38f * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6.3f, OuterR, r));
                    bump[idx] = nb + mound + edge;
                    uv[idx] = new Vector2(x, z);
                }
            }
            for (int i = 0; i < Rings; i++)
                for (int j = 0; j < Segs; j++)
                {
                    int a = i * (Segs + 1) + j, b = a + Segs + 1;
                    tris.Add(a); tris.Add(a + 1); tris.Add(b + 1);
                    tris.Add(a); tris.Add(b + 1); tris.Add(b);
                }
            crustMesh = new Mesh { name = "Crust" };
            crustMesh.MarkDynamic();
            UpdateVerts();
            crustMesh.vertices = verts;
            crustMesh.colors = cols;
            crustMesh.uv = uv;
            crustMesh.SetTriangles(tris, 0);
            crustMesh.RecalculateNormals();
            crustMesh.bounds = new Bounds(new Vector3(0, -3, 0), new Vector3(20, 12, 20));
            var go = new GameObject("Crust");
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = crustMesh;
            var mr = go.AddComponent<MeshRenderer>();
            crustMat = new Material(Mats.Crust);
            mr.sharedMaterial = crustMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
        }

        float BumpScale => 1f - clearedBlend * 0.92f;

        void UpdateVerts()
        {
            float bs = BumpScale;
            for (int i = 0; i < verts.Length; i++)
            {
                float r = radius[i], a = angle[i];
                float h = SurfaceY + bump[i] * bs + dent[i];
                h = Mathf.Max(h, BasinFloor + 0.02f);
                verts[i] = new Vector3(Mathf.Cos(a) * r, h, Mathf.Sin(a) * r);
                float fresh = Mathf.Clamp01(-dent[i] / 0.7f);
                float shade = 1f - fresh * 0.38f;
                cols[i] = new Color(shade, shade * 0.97f, shade * 0.94f, 1);
            }
        }

        public void SetDepth(double frac, bool isCleared, bool instant)
        {
            cleared = isCleared;
            float f = (float)frac;
            targetY = Mathf.Lerp(CrustTop, BasinFloor + 0.05f, f);
            if (instant) { SurfaceY = targetY; clearedBlend = cleared ? 1 : 0; dirty = true; }
        }

        public void SetStratum(MallDef m, int s, bool instant)
        {
            if (s == stratum && !instant) return;
            bool changed = stratum >= 0 && s != stratum;
            stratum = s;
            var st = m.Strata[Mathf.Clamp(s, 0, m.Strata.Length - 1)];
            baseTarget = C(st.Color);
            speckTarget = C(st.Speck);
            coinAmount = st.Loose ? 0.85f : 0.62f + 0.04f * s;
            if (instant || !changed) { baseCol = baseTarget; speckCol = speckTarget; }
            RespawnProps(m, s, changed);
        }

        /// <summary>Punch a crater at a world position. strength ~ 0.2 (tap) .. 1.5 (explosion).</summary>
        public void Dig(Vector3 p, float strength, float radiusWorld)
        {
            float rr = radiusWorld * radiusWorld;
            for (int i = 0; i < verts.Length; i++)
            {
                float dx = verts[i].x - p.x, dz = verts[i].z - p.z;
                float d2 = dx * dx + dz * dz;
                if (d2 > rr) continue;
                float fall = 1 - d2 / rr;
                fall *= fall;
                dent[i] = Mathf.Max(-1.3f, dent[i] - strength * fall);
            }
            dirty = true;
            foreach (var pr in props)
            {
                float dx = pr.X - p.x, dz = pr.Z - p.z;
                if (dx * dx + dz * dz < rr * 0.35f && !pr.Dying) { pr.Dying = true; pr.Life = 0.6f; }
            }
        }

        public string RendererDiagnostics()
        {
            var t = Root != null ? Root.Find("Crust") : null;
            if (t == null) return "no Crust child";
            var mr = t.GetComponent<MeshRenderer>();
            var mf = t.GetComponent<MeshFilter>();
            var mesh = mf != null ? mf.sharedMesh : null;
            string mv = mesh != null && mesh.vertexCount > 0 ? mesh.vertices[mesh.vertexCount / 2].ToString() : "none";
            return $"crustGO active={t.gameObject.activeInHierarchy} rEnabled={mr.enabled} visible={mr.isVisible} mat={(mr.sharedMaterial == crustMat)} " +
                   $"meshVerts={(mesh != null ? mesh.vertexCount : -1)} meshMid={mv} bounds={mr.bounds} childCount={Root.childCount} pos={t.position}";
        }

        public string Diagnostics()
        {
            var tex = crustMat != null ? crustMat.mainTexture : null;
            string sh = crustMat != null && crustMat.shader != null ? crustMat.shader.name : "null";
            return $"crust shader={sh} tex={(tex != null ? tex.name + " " + tex.width + "x" + tex.height : "NULL")} " +
                   $"concrete={(crustMat != null ? crustMat.GetFloat("_Concrete") : -1)} coins={(crustMat != null ? crustMat.GetFloat("_CoinAmount") : -1)} " +
                   $"clearedBlend={clearedBlend} cleared={cleared} surfaceY={SurfaceY} target={targetY} stratum={stratum} v0={(verts != null && verts.Length > 0 ? verts[verts.Length / 2].ToString() : "none")} " +
                   $"base={baseCol} speck={speckCol}";
        }

        public float HeightAt(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            if (r < InnerR) r = InnerR;
            if (r > OuterR) r = OuterR;
            float a = Mathf.Atan2(z, x);
            if (a < 0) a += Mathf.PI * 2;
            float fi = Mathf.Pow(Mathf.InverseLerp(InnerR, OuterR, r), 1 / 0.9f) * Rings;
            float fj = a / (Mathf.PI * 2) * Segs;
            int i0 = Mathf.Clamp(Mathf.FloorToInt(fi), 0, Rings - 1), j0 = Mathf.Clamp(Mathf.FloorToInt(fj), 0, Segs - 1);
            float ti = Mathf.Clamp01(fi - i0), tj = Mathf.Clamp01(fj - j0);
            float h00 = verts[i0 * (Segs + 1) + j0].y, h01 = verts[i0 * (Segs + 1) + j0 + 1].y;
            float h10 = verts[(i0 + 1) * (Segs + 1) + j0].y, h11 = verts[(i0 + 1) * (Segs + 1) + j0 + 1].y;
            return Mathf.Lerp(Mathf.Lerp(h00, h01, tj), Mathf.Lerp(h10, h11, tj), ti);
        }

        public bool Raycast(Ray ray, out Vector3 hit)
        {
            hit = Vector3.zero;
            // march along the ray from above the rim down past the floor
            float tStart = 0, tEnd = 200;
            if (ray.direction.y < -1e-3f)
            {
                tStart = (RimTop + 1.5f - ray.origin.y) / ray.direction.y;
                tEnd = (BasinFloor - 0.5f - ray.origin.y) / ray.direction.y;
                if (tStart < 0) tStart = 0;
            }
            float prevT = tStart;
            float prevDiff = float.MaxValue;
            const int steps = 90;
            for (int s = 0; s <= steps; s++)
            {
                float t = Mathf.Lerp(tStart, tEnd, s / (float)steps);
                Vector3 p = ray.origin + ray.direction * t;
                float r = Mathf.Sqrt(p.x * p.x + p.z * p.z);
                if (r > RimInner) { prevDiff = float.MaxValue; prevT = t; continue; }
                float diff = p.y - HeightAt(p.x, p.z);
                if (diff <= 0 && prevDiff != float.MaxValue)
                {
                    float lerp = prevDiff / (prevDiff - diff);
                    hit = ray.origin + ray.direction * Mathf.Lerp(prevT, t, lerp);
                    return Mathf.Sqrt(hit.x * hit.x + hit.z * hit.z) <= OuterR + 0.1f;
                }
                if (diff <= 0) { hit = p; return true; }
                prevDiff = diff;
                prevT = t;
            }
            return false;
        }

        public Vector3 RandomSurfacePoint(float minR = 1.6f, float maxR = 7.0f)
        {
            float a = Random.value * Mathf.PI * 2, r = Mathf.Lerp(minR, maxR, Mathf.Sqrt(Random.value));
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            return new Vector3(x, HeightAt(x, z), z);
        }

        public Vector3 SurfacePoint(float x, float z) => new Vector3(x, HeightAt(x, z), z);

        public void Update(float dt)
        {
            float prev = SurfaceY;
            SurfaceY = Mathf.Lerp(SurfaceY, targetY, 1 - Mathf.Exp(-dt * 2.5f));
            if (Mathf.Abs(SurfaceY - prev) > 1e-4f) dirty = true;
            float cb = Mathf.MoveTowards(clearedBlend, cleared ? 1 : 0, dt * 0.5f);
            if (cb != clearedBlend) { clearedBlend = cb; dirty = true; }

            float relax = Mathf.Exp(-dt * 0.8f);
            bool any = false;
            for (int i = 0; i < dent.Length; i++)
            {
                if (dent[i] < -1e-4f) { dent[i] *= relax; any = true; }
                else if (dent[i] != 0) { dent[i] = 0; any = true; }
            }
            if (any) dirty = true;

            if (workLight != null) workLight.transform.localPosition = new Vector3(0, SurfaceY + 9f, -1.5f);
            baseCol = Color.Lerp(baseCol, baseTarget, 1 - Mathf.Exp(-dt * 1.5f));
            speckCol = Color.Lerp(speckCol, speckTarget, 1 - Mathf.Exp(-dt * 1.5f));
            if (crustMat != null)
            {
                crustMat.SetColor("_BaseColor", baseCol);
                crustMat.SetColor("_SpeckColor", speckCol);
                crustMat.SetFloat("_CoinAmount", coinAmount * (1 - clearedBlend));
                crustMat.SetFloat("_Concrete", clearedBlend);
            }

            if (dirty)
            {
                UpdateVerts();
                crustMesh.vertices = verts;
                crustMesh.colors = cols;
                crustMesh.RecalculateNormals();
                dirty = false;
            }
            UpdateProps(dt);
        }

        // ── loose junk on the crust ─────────────────────────────────────────

        void RespawnProps(MallDef m, int s, bool animateOut)
        {
            foreach (var p in props) { p.Dying = true; p.Life = animateOut ? 0.8f : 0f; }
            if (cleared) return;
            int count = 14;
            for (int i = 0; i < count; i++)
            {
                var item = m.Items[rng.Next(m.Items.Length)];
                float a = (float)rng.NextDouble() * Mathf.PI * 2, r = Mathf.Lerp(1.8f, 7.2f, Mathf.Sqrt((float)rng.NextDouble()));
                var go = Loot.MakeItemMesh(item.Shape, MeshKit.Hex(item.Color), 1.9f, propRoot);
                var pr = new Prop
                {
                    T = go.transform, X = Mathf.Cos(a) * r, Z = Mathf.Sin(a) * r, Sink = 0.05f + (float)rng.NextDouble() * 0.12f,
                    Tilt = (float)rng.NextDouble() * 50 - 25, Yaw = (float)rng.NextDouble() * 360, Fade = 0
                };
                props.Add(pr);
            }
        }

        void UpdateProps(float dt)
        {
            for (int i = props.Count - 1; i >= 0; i--)
            {
                var p = props[i];
                if (p.T == null) { props.RemoveAt(i); continue; }
                if (p.Dying)
                {
                    p.Life -= dt;
                    float s = Mathf.Clamp01(p.Life / 0.6f);
                    p.T.localScale = Vector3.one * s;
                    if (p.Life <= 0) { Object.Destroy(p.T.gameObject); props.RemoveAt(i); continue; }
                }
                else
                {
                    p.Fade = Mathf.MoveTowards(p.Fade, 1, dt * 2);
                    p.T.localScale = Vector3.one * p.Fade;
                }
                float y = HeightAt(p.X, p.Z) - p.Sink;
                p.T.localPosition = new Vector3(p.X, y, p.Z);
                p.T.localRotation = Quaternion.Euler(p.Tilt, p.Yaw, p.Tilt * 0.5f);
            }
        }
    }
}
