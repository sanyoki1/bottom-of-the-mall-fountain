// The fountain basin: rim, mosaic walls (which reveal faint bands of every stratum you dig
// through), the tiered centrepiece, the crust heightfield that sinks with depth, the shallow
// water on top of it, and the colliders you walk on in first person.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class FountainView
    {
        public const float RimInner = 8.0f, RimOuter = 9.4f, RimTop = 0.9f;
        public const float BasinFloor = -7.0f, CrustTop = 0.55f;
        public const float WaterDepth = 0.34f;
        const float EdgeBank = 0.16f;   // sediment banked against the wall (low enough that water mostly covers it)
        const int Rings = 38, Segs = 104;
        const int ColRings = 12, ColSegs = 40;
        const float InnerR = 0.72f, OuterR = 7.97f;

        public Transform Root { get; private set; }
        public float SurfaceY { get; private set; } = CrustTop;
        /// <summary>Height of the water surface: a shallow layer that follows the crust down.</summary>
        public float WaterY => Mathf.Min(SurfaceY + WaterDepth, RimTop - 0.12f);
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
        Transform water;
        MeshCollider crustCollider;
        Mesh crustColMesh;
        float colliderY = float.NaN;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        public void Build(MallDef m, Transform parent)
        {
            mall = m;
            theme = m.Theme;
            Grime = 1;
            hasJets = hasLights = false;
            jets = null;
            lightRing = null;
            lightMat = null;
            if (Root != null) Object.Destroy(Root.gameObject);
            Root = new GameObject("Fountain").transform;
            Root.SetParent(parent, false);
            BuildRim(m.Theme);
            BuildWall(m);
            BuildCenterpiece(m.Theme);
            BuildFloor();
            BuildCrust();
            BuildWater(m.Theme);
            BuildColliders(m.Theme);
            BuildWorkLight(m.Theme);
            stratum = -1;
            dirty = true;
            colliderY = float.NaN;
        }

        void BuildWater(ThemeDef th)
        {
            // planar UVs in world metres + tangents, so the ripple normals line up with the world
            const int seg = 96;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>();
            float r0 = 0.5f, r1 = RimInner - 0.01f;
            for (int i = 0; i <= seg; i++)
            {
                float a = i * Mathf.PI * 2 / seg;
                var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v.Add(d * r0); v.Add(d * r1);
                uv.Add(new Vector2(d.x * r0, d.z * r0)); uv.Add(new Vector2(d.x * r1, d.z * r1));
            }
            for (int i = 0; i < seg; i++)
            {
                int a = i * 2, b = a + 2;
                tri.Add(a); tri.Add(b); tri.Add(a + 1);
                tri.Add(a + 1); tri.Add(b); tri.Add(b + 1);
            }
            var mesh = new Mesh { name = "Water" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            var n = new Vector3[v.Count];
            for (int i = 0; i < n.Length; i++) n[i] = Vector3.up;
            mesh.normals = n;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            var go = new GameObject("Water");
            go.transform.SetParent(Root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            Color tint = Color.Lerp(C(th.BasinTileA), new Color(0.35f, 0.7f, 0.8f), 0.6f);
            waterMat = Mats.NewFountainWater(tint);
            mr.sharedMaterial = waterMat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            water = go.transform;
            water.localPosition = new Vector3(0, WaterY, 0);
            TintWater();
        }

        /// <summary>
        /// Walkable physics: a ring of boxes for the rim and basin wall, stepping stones outside it,
        /// the centrepiece mesh, and a coarse crust heightfield (rebuilt as the crust sinks).
        /// </summary>
        void BuildColliders(ThemeDef th)
        {
            var root = new GameObject("Colliders").transform;
            root.SetParent(Root, false);
            const int n = 48;
            float mid = (RimInner + RimOuter) / 2, thick = RimOuter - RimInner;
            float chord = 2 * RimOuter * Mathf.Tan(Mathf.PI / n) + 0.05f;
            float h = RimTop - (BasinFloor - 1);
            for (int i = 0; i < n; i++)
            {
                float a = i * 360f / n;
                var go = new GameObject("Rim " + i);
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad) * mid, RimTop - h / 2, Mathf.Sin(a * Mathf.Deg2Rad) * mid);
                go.transform.localRotation = Quaternion.Euler(0, 90 - a, 0);
                var bc = go.AddComponent<BoxCollider>();
                bc.size = new Vector3(chord, h, thick);
            }

            // stepping stones outside the rim
            var k = new MeshKit();
            Color stone = Color.Lerp(C(th.Rim), new Color(0.55f, 0.53f, 0.5f), 0.5f);
            float[] angles = { -90, -30, -150, 30, 150, 90 };
            foreach (float a in angles)
            {
                Vector3 p = new Vector3(Mathf.Cos(a * Mathf.Deg2Rad), 0, Mathf.Sin(a * Mathf.Deg2Rad)) * (RimOuter + 0.55f);
                var rot = Quaternion.Euler(0, 90 - a, 0);
                k.Push(p, rot);
                k.Box(new Vector3(0, 0.22f, 0), new Vector3(1.3f, 0.44f, 0.9f), stone);
                k.Box(new Vector3(0, 0.445f, 0), new Vector3(1.2f, 0.01f, 0.8f), Color.Lerp(stone, Color.white, 0.2f));
                k.Pop();
                var go = new GameObject("Step");
                go.transform.SetParent(root, false);
                go.transform.localPosition = p + new Vector3(0, 0.22f, 0);
                go.transform.localRotation = rot;
                go.AddComponent<BoxCollider>().size = new Vector3(1.3f, 0.44f, 0.9f);
            }
            k.Build("Stepping Stones", Root);

            var cgo = new GameObject("Crust Collider");
            cgo.transform.SetParent(root, false);
            crustColMesh = new Mesh { name = "Crust Collider" };
            crustCollider = cgo.AddComponent<MeshCollider>();
        }

        void RebuildCrustCollider()
        {
            float bs = BumpScale;
            var v = new Vector3[(ColRings + 1) * (ColSegs + 1)];
            var tri = new List<int>(ColRings * ColSegs * 6);
            for (int i = 0; i <= ColRings; i++)
            {
                float r = Mathf.Lerp(0.3f, RimInner + 0.1f, i / (float)ColRings);
                for (int j = 0; j <= ColSegs; j++)
                {
                    float a = j / (float)ColSegs * Mathf.PI * 2;
                    float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
                    float y = Mathf.Max(SurfaceY + BumpAt(x, z) * bs, BasinFloor + 0.02f);
                    v[i * (ColSegs + 1) + j] = new Vector3(x, y, z);
                }
            }
            for (int i = 0; i < ColRings; i++)
                for (int j = 0; j < ColSegs; j++)
                {
                    int a = i * (ColSegs + 1) + j, b = a + ColSegs + 1;
                    tri.Add(a); tri.Add(a + 1); tri.Add(b + 1);
                    tri.Add(a); tri.Add(b + 1); tri.Add(b);
                }
            crustColMesh.Clear();
            crustColMesh.vertices = v;
            crustColMesh.SetTriangles(tri, 0);
            crustColMesh.RecalculateBounds();
            crustCollider.sharedMesh = null;
            crustCollider.sharedMesh = crustColMesh;
            colliderY = SurfaceY;
        }

        /// <summary>The same noise the render crust uses, without click dents.</summary>
        static float BumpAt(float x, float z)
        {
            float r = Mathf.Sqrt(x * x + z * z);
            float nb = (Mathf.PerlinNoise(x * 0.23f + 13, z * 0.23f + 7) - 0.5f) * 0.55f
                     + (Mathf.PerlinNoise(x * 0.9f + 3, z * 0.9f + 9) - 0.5f) * 0.18f;
            float rc = Mathf.Min(r, OuterR);
            float mound = 0.22f * (1 - (rc / OuterR) * (rc / OuterR));
            float edge = EdgeBank * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6.3f, OuterR, rc));
            return nb + mound + edge;
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
            Color rim = Grimy(C(th.Rim)), rimTop = Grimy(Color.Lerp(C(th.Rim), Color.white, 0.18f)), trim = Grimy(C(th.WallTrim));
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

        // ── beautification ──────────────────────────────────────────────────

        /// <summary>1 = forty years of algae; 0 = scrubbed.</summary>
        public float Grime { get; private set; } = 1f;
        bool hasJets, hasLights;
        ParticleSystem jets;
        Transform lightRing;
        Material lightMat;
        Material waterMat;
        ThemeDef theme;

        Color Grimy(Color c)
        {
            var g = Color.Lerp(c, new Color(0.34f, 0.38f, 0.24f), 0.45f * Grime) * (1 - 0.2f * Grime);
            g.a = c.a;
            return g;
        }

        /// <summary>Show the fountain upgrades the player has bought. Cheap to call every frame.</summary>
        public void SetBeauty(bool scrubbed, bool withJets, bool withLights)
        {
            float g = scrubbed ? 0 : 1;
            if (g != Grime)
            {
                Grime = g;
                foreach (var n in new[] { "Rim", "Basin Wall", "Centerpiece" })
                {
                    var t = Root.Find(n);
                    if (t != null) Object.Destroy(t.gameObject);
                }
                BuildRim(theme);
                BuildWall(mall);
                BuildCenterpiece(theme);
                TintWater();
            }
            if (withJets != hasJets) { hasJets = withJets; if (withJets) BuildJets(); else if (jets != null) Object.Destroy(jets.gameObject); }
            if (withLights != hasLights) { hasLights = withLights; if (withLights) BuildLights(); else if (lightRing != null) Object.Destroy(lightRing.gameObject); }
        }

        void TintWater()
        {
            if (waterMat == null) return;
            Color clear = Color.Lerp(C(theme.BasinTileA), new Color(0.35f, 0.7f, 0.8f), 0.6f);
            waterMat.SetColor("_Color", Color.Lerp(clear, new Color(0.42f, 0.5f, 0.3f), Grime * 0.7f));
            waterMat.SetFloat("_Alpha", Mathf.Lerp(0.16f, 0.3f, Grime));
        }

        void BuildJets()
        {
            var go = new GameObject("Water Jets");
            go.transform.SetParent(Root, false);
            jets = go.AddComponent<ParticleSystem>();
            jets.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = jets.main;
            main.loop = true;
            main.startLifetime = 1.3f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.6f, 3.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.gravityModifier = 1.1f;
            main.maxParticles = 1500;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startColor = new Color(0.85f, 0.95f, 1f, 0.8f);
            var em = jets.emission;
            em.rateOverTime = 260;
            var shape = jets.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 28;
            shape.radius = 0.12f;
            go.transform.localPosition = new Vector3(0, 4.25f, 0);
            go.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var col = jets.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
                         new[] { new GradientAlphaKey(0.9f, 0), new GradientAlphaKey(0.7f, 0.7f), new GradientAlphaKey(0, 1) });
            col.color = new ParticleSystem.MinMaxGradient(grad);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = Mats.NewGlow(TexKit.SoftDot, 0.9f, new Color(0.8f, 0.92f, 1f));
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // a second ring of arcs spilling from the lower bowl
            var sub = Object.Instantiate(go, Root);
            sub.name = "Bowl Spill";
            sub.transform.localPosition = new Vector3(0, 2.0f, 0);
            var sp = sub.GetComponent<ParticleSystem>();
            var sm = sp.main;
            sm.startSpeed = new ParticleSystem.MinMaxCurve(1.4f, 2.0f);
            var ss = sp.shape;
            ss.shapeType = ParticleSystemShapeType.Circle;
            ss.radius = 1.8f;
            ss.radiusThickness = 0;
            ss.rotation = Vector3.zero;
            sub.transform.localRotation = Quaternion.Euler(-90, 0, 0);
            var se = sp.emission;
            se.rateOverTime = 320;
            sub.transform.SetParent(go.transform, true);
            jets.Play(true);
        }

        void BuildLights()
        {
            lightRing = new GameObject("Fountain Lights").transform;
            lightRing.SetParent(water, false);
            var k = new MeshKit();
            const int n = 24;
            for (int i = 0; i < n; i++)
            {
                float a = i * Mathf.PI * 2 / n;
                float hue = i / (float)n;
                Color c = Color.HSVToRGB(hue, 0.7f, 1f);
                c.a = 1f;
                var p = new Vector3(Mathf.Cos(a) * (RimInner - 0.06f), -0.14f, Mathf.Sin(a) * (RimInner - 0.06f));
                k.Push(p, Quaternion.LookRotation(-new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a))));
                k.Box(Vector3.zero, new Vector3(0.34f, 0.12f, 0.05f), c);
                k.Pop();
            }
            var go = k.Build("LEDs", lightRing, false);
            lightMat = new Material(Mats.Lit);
            lightMat.SetFloat("_EmissionBoost", 2.2f);
            go.GetComponent<MeshRenderer>().sharedMaterial = lightMat;
            // glow pools on the water above each LED
            var glowMat = Mats.NewGlow(TexKit.SoftDot, 0.7f, Color.white);
            for (int i = 0; i < n; i += 2)
            {
                float a = i * Mathf.PI * 2 / n;
                var q = new GameObject("Glow");
                q.transform.SetParent(lightRing, false);
                q.transform.localPosition = new Vector3(Mathf.Cos(a) * (RimInner - 0.8f), 0.02f, Mathf.Sin(a) * (RimInner - 0.8f));
                q.transform.localScale = Vector3.one * 2.4f;
                q.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
                var mr = q.AddComponent<MeshRenderer>();
                var m = new Material(glowMat);
                m.SetColor("_Color", Color.HSVToRGB(i / (float)n, 0.6f, 1f));
                mr.sharedMaterial = m;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
        }

        void AnimateBeauty(float time)
        {
            if (lightMat != null)
            {
                // a slow colour chase: tint the whole ring through the rainbow over the vertex colours
                float h = (time * 0.05f) % 1f;
                lightMat.SetColor("_Color", Color.Lerp(Color.white, Color.HSVToRGB(h, 0.5f, 1f), 0.5f));
            }
        }

        void BuildWall(MallDef m)
        {
            var th = m.Theme;
            var k = new MeshKit();
            Color a = Grimy(C(th.BasinTileA)), b = Grimy(C(th.BasinTileB));
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
            Color stone = Grimy(Color.Lerp(C(th.Rim), Color.white, 0.12f)), trim = Grimy(C(th.WallTrim));
            Color gunk = Grime > 0.5f ? C(0x6B5A3A) : Color.Lerp(C(th.BasinTileA), new Color(0.5f, 0.8f, 0.9f), 0.6f);
            gunk.a = 0;
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
            var go = k.Build("Centerpiece", Root);
            // you walk around (and under the bowls of) this in first person
            go.AddComponent<MeshCollider>().sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
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
                    float edge = EdgeBank * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(6.3f, OuterR, r));
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
            // no painted coins in first person: every coin you see is a real, pick-up-able item
            coinAmount = 0f;
            if (instant || !changed) { baseCol = baseTarget; speckCol = speckTarget; }
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
            if (water != null) water.localPosition = new Vector3(0, WaterY, 0);
            AnimateBeauty(Time.time);
            if (crustCollider != null && (float.IsNaN(colliderY) || Mathf.Abs(SurfaceY - colliderY) > 0.04f)) RebuildCrustCollider();
        }

        /// <summary>True when a point (feet) is standing in the fountain water.</summary>
        public bool InWater(Vector3 p) => p.x * p.x + p.z * p.z < RimInner * RimInner && p.y < WaterY - 0.02f;

        /// <summary>True when a point is inside the basin (horizontally).</summary>
        public static bool InBasin(Vector3 p) => p.x * p.x + p.z * p.z < RimInner * RimInner;
    }
}
