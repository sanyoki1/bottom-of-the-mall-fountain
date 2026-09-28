// Procedural low-poly mesh builder. Every part carries a vertex colour; vertex alpha is an
// emission mask read by the WE/Lit shader (0 = matte, 1 = full glow), so one material draws
// a whole machine including its lights and screens.
using System.Collections.Generic;
using UnityEngine;

namespace WishExtractor.View
{
    public sealed class MeshKit
    {
        readonly List<Vector3> verts = new List<Vector3>(1024);
        readonly List<Vector3> norms = new List<Vector3>(1024);
        readonly List<Color> cols = new List<Color>(1024);
        readonly List<Vector2> uvs = new List<Vector2>(1024);
        readonly List<int>[] tris = { new List<int>(2048), new List<int>(256) };
        Matrix4x4 m = Matrix4x4.identity;
        readonly Stack<Matrix4x4> stack = new Stack<Matrix4x4>();
        int sub;

        public static bool Linear => QualitySettings.activeColorSpace == ColorSpace.Linear;

        /// <summary>0 = matte material, 1 = glossy material (two submeshes).</summary>
        public MeshKit Glossy(bool on) { sub = on ? 1 : 0; return this; }

        public MeshKit Push(Vector3 pos, Quaternion rot, Vector3 scale)
        {
            stack.Push(m);
            m = m * Matrix4x4.TRS(pos, rot, scale);
            return this;
        }
        public MeshKit Push(Vector3 pos) => Push(pos, Quaternion.identity, Vector3.one);
        public MeshKit Push(Vector3 pos, Quaternion rot) => Push(pos, rot, Vector3.one);
        public MeshKit Pop() { m = stack.Pop(); return this; }
        public int VertexCount => verts.Count;

        static Color Lin(Color c)
        {
            float a = c.a;
            var l = Linear ? c.linear : c;
            l.a = a;
            return l;
        }

        int V(Vector3 p, Vector3 n, Color c, Vector2 uv)
        {
            verts.Add(m.MultiplyPoint3x4(p));
            norms.Add(m.MultiplyVector(n).normalized);
            cols.Add(c);
            uvs.Add(uv);
            return verts.Count - 1;
        }

        void Tri(int a, int b, int c) { var t = tris[sub]; t.Add(a); t.Add(b); t.Add(c); }

        public MeshKit Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color col)
        {
            col = Lin(col);
            Vector3 n = Vector3.Cross(b - a, c - a).normalized;
            int i0 = V(a, n, col, new Vector2(0, 0)), i1 = V(b, n, col, new Vector2(0, 1));
            int i2 = V(c, n, col, new Vector2(1, 1)), i3 = V(d, n, col, new Vector2(1, 0));
            Tri(i0, i1, i2); Tri(i0, i2, i3);
            return this;
        }

        public MeshKit Box(Vector3 center, Vector3 size, Color col) => Box(center, size, Quaternion.identity, col);

        public MeshKit Box(Vector3 center, Vector3 size, Quaternion rot, Color col)
        {
            Push(center, rot);
            Vector3 h = size * 0.5f;
            Vector3 p000 = new Vector3(-h.x, -h.y, -h.z), p100 = new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = new Vector3(-h.x, h.y, -h.z), p110 = new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = new Vector3(-h.x, -h.y, h.z), p101 = new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = new Vector3(-h.x, h.y, h.z), p111 = new Vector3(h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, col); // -z
            Quad(p101, p111, p011, p001, col); // +z
            Quad(p001, p011, p010, p000, col); // -x
            Quad(p100, p110, p111, p101, col); // +x
            Quad(p010, p011, p111, p110, col); // +y
            Quad(p001, p000, p100, p101, col); // -y
            Pop();
            return this;
        }

        /// <summary>Box with only its top and sides (for things sitting on the floor).</summary>
        public MeshKit Slab(Vector3 center, Vector3 size, Color top, Color side)
        {
            Push(center);
            Vector3 h = size * 0.5f;
            Vector3 p000 = new Vector3(-h.x, -h.y, -h.z), p100 = new Vector3(h.x, -h.y, -h.z);
            Vector3 p010 = new Vector3(-h.x, h.y, -h.z), p110 = new Vector3(h.x, h.y, -h.z);
            Vector3 p001 = new Vector3(-h.x, -h.y, h.z), p101 = new Vector3(h.x, -h.y, h.z);
            Vector3 p011 = new Vector3(-h.x, h.y, h.z), p111 = new Vector3(h.x, h.y, h.z);
            Quad(p000, p010, p110, p100, side);
            Quad(p101, p111, p011, p001, side);
            Quad(p001, p011, p010, p000, side);
            Quad(p100, p110, p111, p101, side);
            Quad(p010, p011, p111, p110, top);
            Pop();
            return this;
        }

        /// <summary>Vertical cylinder (axis = local Y) centred on center.</summary>
        public MeshKit Cylinder(Vector3 center, float radius, float height, int seg, Color col, bool caps = true, bool smooth = true)
            => Frustum(center, radius, radius, height, seg, col, caps, smooth);

        public MeshKit Cylinder(Vector3 center, Quaternion rot, float radius, float height, int seg, Color col, bool caps = true)
        {
            Push(center, rot);
            Frustum(Vector3.zero, radius, radius, height, seg, col, caps, true);
            Pop();
            return this;
        }

        public MeshKit Frustum(Vector3 center, float rBottom, float rTop, float height, int seg, Color col, bool caps = true, bool smooth = true)
        {
            Color c = Lin(col);
            float y0 = center.y - height * 0.5f, y1 = center.y + height * 0.5f;
            float slope = (rBottom - rTop) / Mathf.Max(0.0001f, height);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2 / seg, a1 = (i + 1) * Mathf.PI * 2 / seg;
                Vector3 d0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)), d1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1));
                Vector3 n0 = (d0 + Vector3.up * slope).normalized, n1 = (d1 + Vector3.up * slope).normalized;
                if (!smooth) { n0 = n1 = ((d0 + d1) * 0.5f + Vector3.up * slope).normalized; }
                Vector3 b0 = center + d0 * rBottom, b1 = center + d1 * rBottom;
                Vector3 t0 = center + d0 * rTop, t1 = center + d1 * rTop;
                b0.y = b1.y = y0; t0.y = t1.y = y1;
                int i0 = V(b0, n0, c, new Vector2(i / (float)seg, 0)), i1 = V(t0, n0, c, new Vector2(i / (float)seg, 1));
                int i2 = V(t1, n1, c, new Vector2((i + 1) / (float)seg, 1)), i3 = V(b1, n1, c, new Vector2((i + 1) / (float)seg, 0));
                Tri(i0, i1, i2); Tri(i0, i2, i3);
                if (caps)
                {
                    if (rTop > 0.0001f)
                    {
                        int ct = V(new Vector3(center.x, y1, center.z), Vector3.up, c, new Vector2(0.5f, 0.5f));
                        int ta = V(t0, Vector3.up, c, Vector2.zero), tb = V(t1, Vector3.up, c, Vector2.zero);
                        Tri(ct, tb, ta);
                    }
                    if (rBottom > 0.0001f)
                    {
                        int cb = V(new Vector3(center.x, y0, center.z), Vector3.down, c, new Vector2(0.5f, 0.5f));
                        int ba = V(b0, Vector3.down, c, Vector2.zero), bb = V(b1, Vector3.down, c, Vector2.zero);
                        Tri(cb, ba, bb);
                    }
                }
            }
            return this;
        }

        public MeshKit Cone(Vector3 baseCenter, float radius, float height, int seg, Color col)
            => Frustum(baseCenter + Vector3.up * height * 0.5f, radius, 0.0001f, height, seg, col, true, false);

        public MeshKit Sphere(Vector3 center, float radius, int lat, int lon, Color col) => Ellipsoid(center, Vector3.one * radius, lat, lon, col);

        public MeshKit Ellipsoid(Vector3 center, Vector3 radii, int lat, int lon, Color col)
        {
            Color c = Lin(col);
            int start = verts.Count;
            for (int y = 0; y <= lat; y++)
            {
                float v = y / (float)lat, th = v * Mathf.PI;
                for (int x = 0; x <= lon; x++)
                {
                    float u = x / (float)lon, ph = u * Mathf.PI * 2;
                    Vector3 n = new Vector3(Mathf.Sin(th) * Mathf.Cos(ph), Mathf.Cos(th), Mathf.Sin(th) * Mathf.Sin(ph));
                    Vector3 p = center + Vector3.Scale(n, radii);
                    V(p, new Vector3(n.x / radii.x, n.y / radii.y, n.z / radii.z), c, new Vector2(u, v));
                }
            }
            for (int y = 0; y < lat; y++)
                for (int x = 0; x < lon; x++)
                {
                    int a = start + y * (lon + 1) + x, b = a + lon + 1;
                    Tri(a, a + 1, b + 1); Tri(a, b + 1, b);
                }
            return this;
        }

        /// <summary>Flat ring (annulus) in the XZ plane, facing up.</summary>
        public MeshKit Ring(Vector3 center, float rIn, float rOut, int seg, Color col, float a0 = 0, float a1 = Mathf.PI * 2)
        {
            Color c = Lin(col);
            for (int i = 0; i < seg; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)seg), t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)seg);
                Vector3 d0 = new Vector3(Mathf.Cos(t0), 0, Mathf.Sin(t0)), d1 = new Vector3(Mathf.Cos(t1), 0, Mathf.Sin(t1));
                int i0 = V(center + d0 * rIn, Vector3.up, c, Vector2.zero), i1 = V(center + d0 * rOut, Vector3.up, c, Vector2.right);
                int i2 = V(center + d1 * rOut, Vector3.up, c, Vector2.one), i3 = V(center + d1 * rIn, Vector3.up, c, Vector2.up);
                Tri(i0, i3, i2); Tri(i0, i2, i1);
            }
            return this;
        }

        /// <summary>Upright wall along a circle (normals point inward if inward=true).</summary>
        public MeshKit Wall(Vector3 center, float radius, float y0, float y1, int seg, Color col, bool inward, float a0 = 0, float a1 = Mathf.PI * 2)
        {
            Color c = Lin(col);
            for (int i = 0; i < seg; i++)
            {
                float t0 = Mathf.Lerp(a0, a1, i / (float)seg), t1 = Mathf.Lerp(a0, a1, (i + 1) / (float)seg);
                Vector3 d0 = new Vector3(Mathf.Cos(t0), 0, Mathf.Sin(t0)), d1 = new Vector3(Mathf.Cos(t1), 0, Mathf.Sin(t1));
                Vector3 n0 = inward ? -d0 : d0, n1 = inward ? -d1 : d1;
                Vector3 b0 = center + d0 * radius, b1 = center + d1 * radius;
                b0.y = b1.y = y0;
                Vector3 t0p = b0, t1p = b1;
                t0p.y = t1p.y = y1;
                int i0 = V(b0, n0, c, Vector2.zero), i1 = V(t0p, n0, c, Vector2.up), i2 = V(t1p, n1, c, Vector2.one), i3 = V(b1, n1, c, Vector2.right);
                if (inward) { Tri(i0, i2, i1); Tri(i0, i3, i2); }
                else { Tri(i0, i1, i2); Tri(i0, i2, i3); }
            }
            return this;
        }

        public MeshKit Torus(Vector3 center, float r, float tube, int seg, int tubeSeg, Color col)
        {
            Color c = Lin(col);
            int start = verts.Count;
            for (int i = 0; i <= seg; i++)
            {
                float u = i / (float)seg * Mathf.PI * 2;
                Vector3 dir = new Vector3(Mathf.Cos(u), 0, Mathf.Sin(u));
                for (int j = 0; j <= tubeSeg; j++)
                {
                    float v = j / (float)tubeSeg * Mathf.PI * 2;
                    Vector3 n = dir * Mathf.Cos(v) + Vector3.up * Mathf.Sin(v);
                    V(center + dir * r + n * tube, n, c, new Vector2(i / (float)seg, j / (float)tubeSeg));
                }
            }
            for (int i = 0; i < seg; i++)
                for (int j = 0; j < tubeSeg; j++)
                {
                    int a = start + i * (tubeSeg + 1) + j, b = a + tubeSeg + 1;
                    Tri(a, b + 1, b); Tri(a, a + 1, b + 1);
                }
            return this;
        }

        /// <summary>A tube between two points (for poles, hoses, arms).</summary>
        public MeshKit Tube(Vector3 a, Vector3 b, float radius, int seg, Color col, bool caps = true)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 1e-4f) return this;
            var rot = Quaternion.FromToRotation(Vector3.up, d / len);
            Push((a + b) * 0.5f, rot);
            Frustum(Vector3.zero, radius, radius, len, seg, col, caps, true);
            Pop();
            return this;
        }

        /// <summary>Rounded-ish box: a box with bevelled vertical edges via an octagonal prism.</summary>
        public MeshKit Capsule(Vector3 center, float radius, float height, Color col, int seg = 10)
        {
            float body = Mathf.Max(0, height - radius * 2);
            Cylinder(center, radius, body, seg, col, false);
            Ellipsoid(center + Vector3.up * body * 0.5f, Vector3.one * radius, 5, seg, col);
            Ellipsoid(center - Vector3.up * body * 0.5f, Vector3.one * radius, 5, seg, col);
            return this;
        }

        public Mesh ToMesh(string name = "mesh")
        {
            var mesh = new Mesh { name = name };
            if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(verts);
            mesh.SetNormals(norms);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            bool hasGloss = tris[1].Count > 0;
            mesh.subMeshCount = hasGloss ? 2 : 1;
            mesh.SetTriangles(tris[0], 0);
            if (hasGloss) mesh.SetTriangles(tris[1], 1);
            mesh.RecalculateBounds();
            return mesh;
        }

        public bool HasGloss => tris[1].Count > 0;

        /// <summary>Build a GameObject with this mesh and the standard matte/glossy materials.</summary>
        public GameObject Build(string name, Transform parent, bool shadows = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = ToMesh(name);
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterials = HasGloss ? new[] { Mats.Lit, Mats.Gloss } : new[] { Mats.Lit };
            mr.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = true;
            return go;
        }

        public static Color Hex(uint rgb, float emission = 0)
        {
            return new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, emission);
        }

        public static Color Glow(Color c, float emission) { c.a = emission; return c; }
    }
}
