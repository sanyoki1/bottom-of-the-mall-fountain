// Builds the mall atrium around the fountain from the current mall's ThemeDef:
// terrazzo floor, storefronts with neon signs, balcony, escalator, planters and
// a few signature props per mall. Rebuilt whenever the player signs a new contract.
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class WorldBuilder
    {
        public Transform Root { get; private set; }
        public Light Sun { get; private set; }
        public Color FogColor { get; private set; }
        readonly List<Material> textMats = new List<Material>();
        readonly List<Transform> spinners = new List<Transform>();
        readonly List<Transform> swayers = new List<Transform>();
        static Font signFont, signFontAlt;
        System.Random rng;

        public const float HallMinX = -36, HallMaxX = 36, HallMinZ = -30, HallMaxZ = 32;
        const float BalconyY = 6.8f, BalconyZ = 26f, EscalatorX = -29.5f;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        static Color C(int hex, float glow = 0) => MeshKit.Hex((uint)hex, glow);

        public static Font SignFont
        {
            get
            {
                if (signFont == null)
                {
                    signFont = Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift SemiBold", "Bahnschrift", "Arial Black", "Arial" }, 64);
                    Font.textureRebuilt += OnFontRebuilt;
                }
                return signFont;
            }
        }

        public static Font SignFontAlt
        {
            get
            {
                if (signFontAlt == null) signFontAlt = Font.CreateDynamicFontFromOSFont(new[] { "Impact", "Arial Black", "Bahnschrift", "Arial" }, 64);
                return signFontAlt;
            }
        }

        static readonly List<(Font font, Material mat)> fontMats = new List<(Font, Material)>();

        static void OnFontRebuilt(Font f)
        {
            foreach (var fm in fontMats)
                if (fm.font == f && fm.mat != null) fm.mat.mainTexture = f.material.mainTexture;
        }

        public static TextMesh MakeText(Transform parent, string text, Vector3 pos, Quaternion rot, float size, Color color, float intensity, Font font = null, int layerOrder = 0)
        {
            font = font ?? SignFont;
            var go = new GameObject("Text " + text);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var tm = go.AddComponent<TextMesh>();
            tm.font = font;
            tm.text = text;
            tm.fontSize = 64;
            tm.characterSize = size / 10f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = Color.white;
            var mr = go.GetComponent<MeshRenderer>();
            var mat = Mats.NewText(font, color, intensity);
            fontMats.Add((font, mat));
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.sortingOrder = layerOrder;
            return tm;
        }

        public void Build(MallDef mall, Transform parent, int seed)
        {
            if (Root != null) Object.Destroy(Root.gameObject);
            textMats.Clear();
            spinners.Clear();
            swayers.Clear();
            rng = new System.Random(seed);
            Root = new GameObject("Mall: " + mall.Name).transform;
            Root.SetParent(parent, false);
            var th = mall.Theme;

            colRoot = new GameObject("Colliders").transform;
            colRoot.SetParent(Root, false);
            BuildLighting(th);
            BuildFloor(th);
            BuildBackWall(th, mall);
            BuildSideWalls(th);
            BuildSouthWall(th, mall);
            BuildCeiling(th);
            BuildBalcony(th, mall);
            BuildPlanters(th);
            BuildEscalator(th);
            BuildBenches(th);
            BuildSignature(mall);
            BuildHallColliders();
        }

        // ── colliders ───────────────────────────────────────────────────────

        Transform colRoot;

        public BoxCollider ColBox(Vector3 center, Vector3 size, Quaternion rot, string name = "Box")
        {
            var go = new GameObject(name);
            go.transform.SetParent(colRoot, false);
            go.transform.localPosition = center;
            go.transform.localRotation = rot;
            var bc = go.AddComponent<BoxCollider>();
            bc.size = size;
            return bc;
        }

        public BoxCollider ColBox(Vector3 center, Vector3 size) => ColBox(center, size, Quaternion.identity);

        void ColCapsule(Vector3 center, float radius, float height)
        {
            var go = new GameObject("Capsule");
            go.transform.SetParent(colRoot, false);
            go.transform.localPosition = center;
            var cc = go.AddComponent<CapsuleCollider>();
            cc.radius = radius;
            cc.height = height;
        }

        /// <summary>Round things (planters) as two boxes rotated 45° apart.</summary>
        void ColRound(Vector3 center, float radius, float height)
        {
            float side = radius * 1.8f;
            ColBox(center, new Vector3(side, height, side), Quaternion.identity, "Round");
            ColBox(center, new Vector3(side, height, side), Quaternion.Euler(0, 45, 0), "Round");
        }

        void BuildHallColliders()
        {
            // floor: an annulus mesh, so it never caps the basin once the crust sinks below y = 0
            var k = new MeshKit();
            k.Ring(Vector3.zero, FountainView.RimOuter - 0.1f, 60f, 64, Color.white);
            var fgo = new GameObject("Floor Collider");
            fgo.transform.SetParent(colRoot, false);
            fgo.AddComponent<MeshCollider>().sharedMesh = k.ToMesh("floor collider");
            float w = HallMaxX - HallMinX, d = HallMaxZ - HallMinZ;
            ColBox(new Vector3(0, 9, HallMaxZ + 0.5f), new Vector3(w + 4, 20, 1), Quaternion.identity, "Back Wall");
            ColBox(new Vector3(0, 9, HallMinZ - 0.5f), new Vector3(w + 4, 20, 1), Quaternion.identity, "South Wall");
            ColBox(new Vector3(HallMinX - 0.5f, 9, 1), new Vector3(1, 20, d + 4), Quaternion.identity, "West Wall");
            ColBox(new Vector3(HallMaxX + 0.5f, 9, 1), new Vector3(1, 20, d + 4), Quaternion.identity, "East Wall");
            ColBox(new Vector3(0, CeilingY + 0.3f, 1), new Vector3(w + 4, 0.6f, d + 4), Quaternion.identity, "Ceiling");
        }

        // ── south wall (behind the player's spawn) with the mall entrance ──────

        const float CeilingY = 18f;

        void BuildSouthWall(ThemeDef th, MallDef mall)
        {
            var k = new MeshKit();
            Color wall = C(th.Wall), trim = C(th.WallTrim);
            var face = Quaternion.Euler(0, 180, 0);
            k.Box(new Vector3(0, 9, HallMinZ - 0.5f), new Vector3(HallMaxX - HallMinX + 2, 18, 1), wall);
            k.Box(new Vector3(0, BalconyY - 0.3f, HallMinZ + 0.05f), new Vector3(HallMaxX - HallMinX, 0.5f, 0.12f), trim);
            float[] xs = { -27f, -14f, 14f, 27f };
            for (int i = 0; i < xs.Length; i++)
            {
                int si = (i * 2 + 1) % th.Signs.Length;
                Storefront(k, Root, new Vector3(xs[i], 0, HallMinZ + 0.2f), face, 11.4f, 5.6f, th.Signs[si], C(th.SignColors[si]), th, mall.Id == "crestview" && i == 2);
            }
            // entrance: frame, four glass doors glowing with daylight, a welcome mat
            Color frame = Color.Lerp(trim, Color.black, 0.3f);
            frame.a = 0;
            Color day = th.Night ? new Color(0.25f, 0.3f, 0.55f, 0.55f) : new Color(0.85f, 0.93f, 1f, 0.7f);
            float ez = HallMinZ + 0.15f;
            k.Box(new Vector3(0, 3.1f, ez), new Vector3(9.6f, 0.4f, 0.4f), frame);
            k.Box(new Vector3(-4.7f, 1.5f, ez), new Vector3(0.3f, 3.0f, 0.4f), frame);
            k.Box(new Vector3(4.7f, 1.5f, ez), new Vector3(0.3f, 3.0f, 0.4f), frame);
            for (int i = 0; i < 4; i++)
            {
                float x = -3.45f + i * 2.3f;
                k.Box(new Vector3(x, 1.45f, ez + 0.02f), new Vector3(2.1f, 2.85f, 0.06f), day);
                k.Box(new Vector3(x + (i % 2 == 0 ? 0.8f : -0.8f), 1.2f, ez + 0.12f), new Vector3(0.06f, 0.6f, 0.06f), C(0xC8CCD2));
                k.Box(new Vector3(x, 1.45f, ez + 0.08f), new Vector3(0.08f, 2.85f, 0.04f), frame);
            }
            k.Box(new Vector3(0, 0.006f, HallMinZ + 2.2f), new Vector3(6f, 0.012f, 3f), new Color(0.2f, 0.2f, 0.22f, 0));
            k.Box(new Vector3(0, 3.9f, ez + 0.05f), new Vector3(9.8f, 1.2f, 0.25f), Color.Lerp(wall, Color.black, th.Night ? 0.7f : 0.4f));
            Color exitGlow = C(0xFF3030, 0.9f);
            k.Box(new Vector3(3.6f, 3.35f, ez + 0.3f), new Vector3(0.7f, 0.28f, 0.1f), exitGlow);
            k.Build("South Wall", Root, false);
            MakeText(Root, mall.Name.ToUpperInvariant(), new Vector3(0, 3.95f, ez + 0.2f), face, 0.55f, C(th.NeonA), 1.6f + th.NeonStrength * 0.6f, SignFontAlt);
            MakeText(Root, "EXIT", new Vector3(3.6f, 3.35f, ez + 0.37f), face, 0.18f, Color.white, 1.8f, SignFont);
            MakeText(Root, "WELCOME", new Vector3(0, 0.02f, HallMinZ + 2.2f), Quaternion.Euler(90, 180, 0), 0.45f, new Color(0.7f, 0.7f, 0.72f), 0.6f, SignFont);
        }

        void BuildCeiling(ThemeDef th)
        {
            var k = new MeshKit();
            Color ceil = C(th.Ceiling), beam = Color.Lerp(C(th.Ceiling), Color.black, 0.25f);
            beam.a = 0;
            float w = HallMaxX - HallMinX, d = HallMaxZ - HallMinZ, cz = (HallMinZ + HallMaxZ) / 2;
            const float sky = 13f;   // skylight half-size, centred on the fountain
            // ceiling slab around a square skylight opening
            k.Box(new Vector3(0, CeilingY + 0.2f, (HallMaxZ + sky) / 2 + 0.5f), new Vector3(w, 0.4f, HallMaxZ - sky - 1), ceil);
            k.Box(new Vector3(0, CeilingY + 0.2f, (HallMinZ - sky) / 2 + 0.5f), new Vector3(w, 0.4f, -HallMinZ - sky + 1), ceil);
            k.Box(new Vector3((HallMinX - sky) / 2, CeilingY + 0.2f, 1), new Vector3(-HallMinX - sky, 0.4f, sky * 2), ceil);
            k.Box(new Vector3((HallMaxX + sky) / 2, CeilingY + 0.2f, 1), new Vector3(HallMaxX - sky, 0.4f, sky * 2), ceil);
            // coffer beams across the solid part
            for (float x = HallMinX + 6; x < HallMaxX; x += 12) k.Box(new Vector3(x, CeilingY - 0.25f, cz), new Vector3(0.5f, 0.5f, d), beam);
            // skylight: glowing glass panes in a steel grid, a little recessed
            Color glass = th.Night ? new Color(0.12f, 0.15f, 0.32f, 0.35f) : new Color(0.78f, 0.9f, 1f, 0.55f);
            k.Box(new Vector3(0, CeilingY + 1.2f, 1), new Vector3(sky * 2, 0.1f, sky * 2), glass);
            Color steel = new Color(0.35f, 0.37f, 0.4f, 0);
            for (int i = 0; i <= 6; i++)
            {
                float t = -sky + i * sky / 3;
                k.Box(new Vector3(t, CeilingY + 1.1f, 1), new Vector3(0.2f, 0.2f, sky * 2), steel);
                k.Box(new Vector3(0, CeilingY + 1.1f, 1 + t), new Vector3(sky * 2, 0.2f, 0.2f), steel);
            }
            // light well walls
            k.Box(new Vector3(0, CeilingY + 0.7f, 1 + sky), new Vector3(sky * 2, 1.0f, 0.2f), ceil);
            k.Box(new Vector3(0, CeilingY + 0.7f, 1 - sky), new Vector3(sky * 2, 1.0f, 0.2f), ceil);
            k.Box(new Vector3(sky, CeilingY + 0.7f, 1), new Vector3(0.2f, 1.0f, sky * 2), ceil);
            k.Box(new Vector3(-sky, CeilingY + 0.7f, 1), new Vector3(0.2f, 1.0f, sky * 2), ceil);
            // shadows off so the sun still lights the hall through the "glass"
            k.Build("Ceiling", Root, false);
        }

        void BuildLighting(ThemeDef th)
        {
            if (Sun == null)
            {
                var go = new GameObject("Sun");
                Sun = go.AddComponent<Light>();
                Sun.type = LightType.Directional;
                Sun.shadows = LightShadows.Soft;
                Sun.shadowStrength = 0.72f;
                Sun.shadowBias = 0.04f;
                Sun.shadowNormalBias = 0.35f;
            }
            Sun.color = C(th.LightColor);
            Sun.intensity = th.LightIntensity;
            Sun.transform.rotation = Quaternion.Euler(th.SunPitch, th.SunYaw, 0);

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            float amb = th.Night ? 0.72f : 0.8f;
            RenderSettings.ambientSkyColor = C(th.AmbientSky) * amb;
            RenderSettings.ambientEquatorColor = C(th.AmbientEquator) * amb;
            RenderSettings.ambientGroundColor = C(th.AmbientGround) * amb;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            FogColor = C(th.FogColor);
            RenderSettings.fogColor = FogColor;
            RenderSettings.fogDensity = th.FogDensity;
        }

        void BuildFloor(ThemeDef th)
        {
            var k = new MeshKit();
            Color a = C(th.FloorA), b = C(th.FloorB);
            Color grout = Color.Lerp(a, b, 0.5f) * 0.72f;
            grout.a = 0;
            // grout base: an annulus, so it never caps the basin opening once the crust sinks below floor level
            k.Push(new Vector3(0, -0.01f, 0));
            k.Ring(Vector3.zero, FountainView.RimOuter - 0.05f, 52f, 96, grout);
            k.Pop();
            const float tile = 4f, gap = 0.12f;
            for (float x = HallMinX; x < HallMaxX; x += tile)
                for (float z = HallMinZ; z < HallMaxZ; z += tile)
                {
                    float cx = x + tile / 2, cz = z + tile / 2;
                    float dist = Mathf.Sqrt(cx * cx + cz * cz);
                    if (dist < 12.6f) continue; // plaza ring handles the middle
                    bool checker = (Mathf.FloorToInt(x / tile) + Mathf.FloorToInt(z / tile)) % 2 == 0;
                    Color c = checker ? a : b;
                    float j = 0.94f + 0.08f * (float)rng.NextDouble();
                    c = new Color(c.r * j, c.g * j, c.b * j, 0);
                    k.Quad(new Vector3(x + gap, 0, z + gap), new Vector3(x + gap, 0, z + tile - gap),
                           new Vector3(x + tile - gap, 0, z + tile - gap), new Vector3(x + tile - gap, 0, z + gap), c);
                }
            // plaza: radial rings of accent tiles around the fountain
            Color acc = C(th.Accent);
            Color light = Color.Lerp(a, Color.white, 0.25f);
            int seg = 48;
            for (int ring = 0; ring < 3; ring++)
            {
                float r0 = 9.5f + ring * 1.05f, r1 = r0 + 1.0f;
                for (int i = 0; i < seg; i++)
                {
                    float a0 = i * Mathf.PI * 2 / seg + 0.004f, a1 = (i + 1) * Mathf.PI * 2 / seg - 0.004f;
                    Color c = ring == 1 ? (i % 2 == 0 ? acc : light) : (ring == 0 ? Color.Lerp(b, Color.white, 0.1f) : Color.Lerp(a, b, 0.4f));
                    c.a = 0;
                    k.Push(new Vector3(0, 0.005f, 0));
                    k.Ring(Vector3.zero, r0 + 0.03f, r1 - 0.03f, 1, c, a0, a1);
                    k.Pop();
                }
            }
            k.Build("Floor", Root, false).GetComponent<MeshRenderer>().receiveShadows = true;
        }

        void Storefront(MeshKit k, Transform textParent, Vector3 center, Quaternion rot, float width, float height, string sign, Color neon, ThemeDef th, bool vacant)
        {
            k.Push(center, rot);
            Color trim = C(th.WallTrim), wall = C(th.Wall);
            Color glass = vacant ? new Color(0.55f, 0.43f, 0.3f, 0) : Color.Lerp(new Color(0.12f, 0.16f, 0.2f), C(th.NeonA), 0.12f);
            glass.a = vacant ? 0 : 0.18f;
            float signY = height - 0.9f;
            // frame
            k.Box(new Vector3(0, height / 2, 0.15f), new Vector3(width, height, 0.3f), wall);
            k.Box(new Vector3(0, (signY - 0.35f) / 2, -0.05f), new Vector3(width - 1.2f, signY - 0.35f, 0.12f), glass);
            // mullions
            for (int i = 1; i < 4; i++)
                k.Box(new Vector3(-width / 2 + 0.6f + i * (width - 1.2f) / 4, (signY - 0.35f) / 2, -0.13f), new Vector3(0.1f, signY - 0.35f, 0.06f), trim);
            // sign band + neon underline
            Color band = Color.Lerp(wall, Color.black, th.Night ? 0.7f : 0.35f);
            band.a = 0;
            k.Box(new Vector3(0, signY + 0.25f, -0.12f), new Vector3(width - 0.6f, 1.1f, 0.18f), band);
            Color n = neon;
            n.a = vacant ? 0 : 0.85f * th.NeonStrength;
            k.Box(new Vector3(0, signY - 0.33f, -0.24f), new Vector3(width - 0.9f, 0.07f, 0.06f), n);
            // kick plate
            k.Box(new Vector3(0, 0.2f, -0.12f), new Vector3(width - 1.2f, 0.4f, 0.16f), trim);
            k.Pop();
            if (!string.IsNullOrEmpty(sign))
            {
                Vector3 p = center + rot * new Vector3(0, signY + 0.28f, -0.25f);
                var t = MakeText(textParent, sign, p, rot, 0.62f, vacant ? new Color(0.7f, 0.66f, 0.6f) : neon, vacant ? 0.9f : 1.6f + th.NeonStrength * 0.8f, SignFont);
                // keep long names inside the band
                float maxW = width - 1.2f;
                var bounds = t.GetComponent<MeshRenderer>().bounds;
                float wWorld = Mathf.Max(bounds.size.x, bounds.size.z);
                if (wWorld > maxW && wWorld > 0.01f) t.characterSize *= maxW / wWorld;
            }
        }

        void BuildBackWall(ThemeDef th, MallDef mall)
        {
            var k = new MeshKit();
            Color wall = C(th.Wall);
            k.Box(new Vector3(0, 9, HallMaxZ + 0.5f), new Vector3(HallMaxX - HallMinX + 2, 18, 1), wall);
            int n = 5;
            float width = (HallMaxX - HallMinX - 6) / n;
            for (int i = 0; i < n; i++)
            {
                float x = HallMinX + 3 + width * (i + 0.5f);
                int si = i % th.Signs.Length;
                Storefront(k, Root, new Vector3(x, 0, HallMaxZ - 0.2f), Quaternion.identity, width - 0.6f, 5.6f, th.Signs[si], C(th.SignColors[si]), th, false);
                // upper level
                int ui = (i + 5) % th.Signs.Length;
                bool vacant = mall.Id == "crestview" ? i % 2 == 1 : i == 3;
                Storefront(k, Root, new Vector3(x, BalconyY, HallMaxZ - 0.2f), Quaternion.identity, width - 0.6f, 5.2f, vacant ? "SPACE AVAILABLE" : th.Signs[ui], C(th.SignColors[ui]), th, vacant);
            }
            k.Build("Back Wall", Root);
        }

        void BuildSideWalls(ThemeDef th)
        {
            var k = new MeshKit();
            Color wall = C(th.Wall);
            k.Box(new Vector3(HallMinX - 0.5f, 9, 1), new Vector3(1, 18, HallMaxZ - HallMinZ), wall);
            k.Box(new Vector3(HallMaxX + 0.5f, 9, 1), new Vector3(1, 18, HallMaxZ - HallMinZ), wall);
            for (int i = 0; i < 3; i++)
            {
                float z = HallMinZ + 8 + i * 17;
                int si = (i * 3 + 2) % th.Signs.Length;
                Storefront(k, Root, new Vector3(HallMinX + 0.2f, 0, z), Quaternion.Euler(0, -90, 0), 14f, 5.6f, th.Signs[si], C(th.SignColors[si]), th, false);
                int sj = (i * 3 + 4) % th.Signs.Length;
                Storefront(k, Root, new Vector3(HallMaxX - 0.2f, 0, z), Quaternion.Euler(0, 90, 0), 14f, 5.6f, th.Signs[sj], C(th.SignColors[sj]), th, false);
            }
            k.Build("Side Walls", Root);
        }

        void BuildBalcony(ThemeDef th, MallDef mall)
        {
            var k = new MeshKit();
            Color slab = C(th.Pillar), trim = C(th.WallTrim), pillar = C(th.Pillar);
            float depth = HallMaxZ - BalconyZ;
            k.Box(new Vector3(0, BalconyY - 0.3f, BalconyZ + depth / 2), new Vector3(HallMaxX - HallMinX, 0.6f, depth), slab);
            Color rail = C(th.NeonA);
            rail.a = 0.55f * th.NeonStrength;
            k.Box(new Vector3(0, BalconyY + 1.05f, BalconyZ - 0.05f), new Vector3(HallMaxX - HallMinX, 0.08f, 0.12f), rail);
            Color glass = Color.Lerp(C(th.Pillar), new Color(0.6f, 0.8f, 0.9f), 0.5f);
            glass.a = 0.05f;
            k.Box(new Vector3(0, BalconyY + 0.5f, BalconyZ), new Vector3(HallMaxX - HallMinX, 1.0f, 0.05f), glass);
            Color band = trim;
            band.a = 0;
            k.Box(new Vector3(0, BalconyY - 0.3f, BalconyZ - 0.02f), new Vector3(HallMaxX - HallMinX, 0.62f, 0.1f), band);
            for (int i = -2; i <= 2; i++)
            {
                float x = i * 13.5f;
                k.Cylinder(new Vector3(x, 8.5f, BalconyZ + 0.6f), 0.55f, 17f, 16, pillar);
                k.Cylinder(new Vector3(x, 0.25f, BalconyZ + 0.6f), 0.75f, 0.5f, 16, trim);
                k.Cylinder(new Vector3(x, BalconyY - 0.7f, BalconyZ + 0.6f), 0.72f, 0.3f, 16, trim);
            }
            k.Build("Balcony", Root);
            ColBox(new Vector3(0, BalconyY - 0.3f, BalconyZ + depth / 2), new Vector3(HallMaxX - HallMinX, 0.6f, depth), Quaternion.identity, "Balcony");
            // front rail, with a gap where the escalator arrives
            float gapA = EscalatorX - 1.4f, gapB = EscalatorX + 1.4f;
            ColBox(new Vector3((HallMinX + gapA) / 2, BalconyY + 0.55f, BalconyZ), new Vector3(gapA - HallMinX, 1.1f, 0.2f), Quaternion.identity, "Rail");
            ColBox(new Vector3((gapB + HallMaxX) / 2, BalconyY + 0.55f, BalconyZ), new Vector3(HallMaxX - gapB, 1.1f, 0.2f), Quaternion.identity, "Rail");
            for (int i = -2; i <= 2; i++) ColCapsule(new Vector3(i * 13.5f, 8.5f, BalconyZ + 0.6f), 0.6f, 17f);
            // big hanging banner with the mall name
            var bn = new MeshKit();
            Color bannerCol = C(th.Accent);
            bannerCol.a = 0;
            bn.Box(new Vector3(0, BalconyY + 2.9f, BalconyZ - 0.4f), new Vector3(14f, 2.2f, 0.1f), bannerCol);
            bn.Build("Banner", Root, false);
            MakeText(Root, mall.Name.ToUpperInvariant(), new Vector3(0, BalconyY + 3.1f, BalconyZ - 0.47f), Quaternion.identity, 0.9f, Color.white, 1.15f, SignFontAlt);
            MakeText(Root, mall.Tagline.Split('—')[0].Trim(), new Vector3(0, BalconyY + 2.35f, BalconyZ - 0.47f), Quaternion.identity, 0.34f, new Color(1, 1, 1, 0.9f), 1.0f, SignFont);
        }

        void Palm(MeshKit k, Vector3 basePos, float height, Color trunk, Color leaf, float lean)
        {
            int segs = 7;
            Vector3 p = basePos;
            Vector3 dir = Quaternion.Euler(lean, (float)rng.NextDouble() * 360, 0) * Vector3.up;
            for (int i = 0; i < segs; i++)
            {
                float r0 = Mathf.Lerp(0.34f, 0.2f, i / (float)segs);
                Vector3 next = p + dir * (height / segs);
                Color tc = i % 2 == 0 ? trunk : trunk * 0.88f;
                tc.a = 0;
                k.Tube(p, next, r0, 8, tc, false);
                p = next;
            }
            k.Sphere(p, 0.3f, 4, 8, trunk * 0.8f);
            for (int f = 0; f < 8; f++)
            {
                float ang = f * 45 + (float)rng.NextDouble() * 20;
                var rot = Quaternion.Euler(0, ang, 0);
                Vector3 d = rot * Vector3.forward;
                Vector3 mid = p + d * 1.2f + Vector3.up * 0.25f;
                Vector3 tip = p + d * 2.4f + Vector3.down * 0.8f;
                Color lc = f % 2 == 0 ? leaf : leaf * 0.85f;
                lc.a = 0;
                k.Push(Vector3.zero, Quaternion.identity);
                k.Tube(p, mid, 0.05f, 5, lc);
                k.Tube(mid, tip, 0.04f, 5, lc);
                k.Push((p + mid) * 0.5f, Quaternion.LookRotation(mid - p) * Quaternion.Euler(0, 0, 90));
                k.Ellipsoid(Vector3.zero, new Vector3(0.45f, 0.04f, 0.75f), 3, 8, lc);
                k.Pop();
                k.Push((mid + tip) * 0.5f, Quaternion.LookRotation(tip - mid) * Quaternion.Euler(0, 0, 90));
                k.Ellipsoid(Vector3.zero, new Vector3(0.35f, 0.035f, 0.7f), 3, 8, lc);
                k.Pop();
                k.Pop();
            }
        }

        void BuildPlanters(ThemeDef th)
        {
            var k = new MeshKit();
            Color rim = C(th.Rim), trim = C(th.WallTrim), soil = new Color(0.25f, 0.18f, 0.12f, 0);
            Color trunk = new Color(0.52f, 0.38f, 0.24f, 0);
            Color leaf = C(th.Plant);
            float[] angles = { 38, 142, 218, 322 };
            foreach (float ang in angles)
            {
                float r = ang > 180 ? 21f : 19.5f;
                Vector3 c = new Vector3(Mathf.Cos(ang * Mathf.Deg2Rad) * r, 0, Mathf.Sin(ang * Mathf.Deg2Rad) * r * 0.9f + 1);
                k.Cylinder(c + Vector3.up * 0.45f, 1.9f, 0.9f, 20, rim);
                k.Cylinder(c + Vector3.up * 0.92f, 2.0f, 0.08f, 20, trim);
                k.Cylinder(c + Vector3.up * 0.93f, 1.7f, 0.04f, 20, soil);
                Palm(k, c + Vector3.up * 0.9f, 5.5f + (float)rng.NextDouble() * 1.5f, trunk, leaf, 6 + (float)rng.NextDouble() * 6);
                ColRound(c + Vector3.up * 0.48f, 2.0f, 0.96f);
            }
            k.Build("Planters", Root);
        }

        void BuildEscalator(ThemeDef th)
        {
            var k = new MeshKit();
            Color steel = new Color(0.62f, 0.64f, 0.68f, 0), dark = new Color(0.18f, 0.18f, 0.2f, 0);
            Color glow = C(th.NeonB);
            glow.a = 0.7f * th.NeonStrength;
            Vector3 bottom = new Vector3(EscalatorX, 0, -2f), top = new Vector3(EscalatorX, BalconyY, BalconyZ - 1f);
            Vector3 d = top - bottom;
            float len = d.magnitude;
            var rot = Quaternion.LookRotation(d.normalized, Vector3.up);
            // walkable ramp up to the balcony, a little longer than the steps so it meets the slab
            {
                Vector3 cb = new Vector3(EscalatorX, -0.25f, -2.6f), ct = new Vector3(EscalatorX, BalconyY - 0.25f, BalconyZ + 0.6f);
                Vector3 cd = ct - cb;
                ColBox((cb + ct) / 2, new Vector3(2.3f, 0.5f, cd.magnitude), Quaternion.LookRotation(cd.normalized, Vector3.up), "Escalator");
                ColBox((bottom + top) / 2 + rot * new Vector3(-1.25f, 0.6f, 0), new Vector3(0.15f, 1.3f, len), rot, "Escalator Rail");
                ColBox((bottom + top) / 2 + rot * new Vector3(1.25f, 0.6f, 0), new Vector3(0.15f, 1.3f, len), rot, "Escalator Rail");
            }
            k.Push((bottom + top) * 0.5f, rot);
            k.Box(new Vector3(0, -0.3f, 0), new Vector3(2.4f, 0.6f, len), dark);
            k.Box(new Vector3(-1.25f, 0.4f, 0), new Vector3(0.15f, 1.2f, len), steel);
            k.Box(new Vector3(1.25f, 0.4f, 0), new Vector3(0.15f, 1.2f, len), steel);
            k.Box(new Vector3(-1.25f, 1.02f, 0), new Vector3(0.18f, 0.06f, len), glow);
            k.Box(new Vector3(1.25f, 1.02f, 0), new Vector3(0.18f, 0.06f, len), glow);
            k.Pop();
            int steps = 26;
            for (int i = 0; i < steps; i++)
            {
                Vector3 p = Vector3.Lerp(bottom, top, (i + 0.5f) / steps);
                k.Box(p + Vector3.up * 0.05f, new Vector3(2.2f, 0.12f, 0.5f), i % 2 == 0 ? steel : steel * 0.85f);
            }
            k.Build("Escalator", Root);
        }

        void BuildBenches(ThemeDef th)
        {
            var k = new MeshKit();
            Color wood = Color.Lerp(C(th.Accent), new Color(0.5f, 0.35f, 0.22f), 0.6f);
            wood.a = 0;
            Color metal = new Color(0.3f, 0.3f, 0.33f, 0);
            Vector3[] spots = { new Vector3(-17, 0, 10), new Vector3(17, 0, 10), new Vector3(-23, 0, -9), new Vector3(23, 0, -9) };
            foreach (var s in spots)
            {
                float ang = Mathf.Atan2(-s.x, -s.z) * Mathf.Rad2Deg;
                k.Push(s, Quaternion.Euler(0, ang, 0));
                k.Box(new Vector3(0, 0.48f, 0), new Vector3(2.4f, 0.1f, 0.7f), wood);
                k.Box(new Vector3(0, 0.9f, 0.32f), new Vector3(2.4f, 0.5f, 0.08f), wood);
                k.Box(new Vector3(-1f, 0.24f, 0), new Vector3(0.08f, 0.48f, 0.6f), metal);
                k.Box(new Vector3(1f, 0.24f, 0), new Vector3(0.08f, 0.48f, 0.6f), metal);
                k.Pop();
                ColBox(s + new Vector3(0, 0.55f, 0), new Vector3(2.4f, 1.1f, 0.75f), Quaternion.Euler(0, ang, 0), "Bench");
            }
            k.Build("Benches", Root);
        }

        // ── signature props per mall ─────────────────────────────────────────

        void BuildSignature(MallDef mall)
        {
            var th = mall.Theme;
            var k = new MeshKit();
            switch (mall.Id)
            {
                case "crestview":
                    // coin-op kiddie ride and a wet floor sign
                    k.Push(new Vector3(24, 0, 3), Quaternion.Euler(0, -120, 0));
                    k.Box(new Vector3(0, 0.3f, 0), new Vector3(1.6f, 0.6f, 1.1f), C(0x2E9C95));
                    k.Ellipsoid(new Vector3(0, 1.1f, 0), new Vector3(0.35f, 0.4f, 0.8f), 6, 10, C(0xF2E8D0));
                    k.Ellipsoid(new Vector3(0, 1.55f, 0.7f), new Vector3(0.22f, 0.3f, 0.35f), 6, 10, C(0xF2E8D0));
                    k.Box(new Vector3(0, 1.6f, 0.95f), new Vector3(0.1f, 0.12f, 0.1f), C(0x222222));
                    k.Tube(new Vector3(0, 0.6f, 0), new Vector3(0, 2.6f, 0), 0.05f, 6, C(0xC8C8C8));
                    k.Box(new Vector3(0, 0.9f, -0.62f), new Vector3(0.5f, 0.4f, 0.1f), C(0xFFD34D, 0.6f));
                    k.Pop();
                    ColBox(new Vector3(24, 0.8f, 3), new Vector3(1.6f, 1.6f, 1.6f), Quaternion.Euler(0, -120, 0), "Kiddie Ride");
                    k.Push(new Vector3(-12, 0, -12), Quaternion.Euler(0, 20, 0));
                    k.Box(new Vector3(0, 0.55f, 0.18f), new Vector3(0.7f, 1.1f, 0.04f), C(0xFFD000));
                    k.Box(new Vector3(0, 0.55f, -0.18f), new Vector3(0.7f, 1.1f, 0.04f), C(0xFFD000));
                    k.Pop();
                    MakeText(Root, "40% LEASED!", new Vector3(20, BalconyY + 3.0f, BalconyZ - 0.45f), Quaternion.identity, 0.5f, C(0xFF7AA8), 1.4f, SignFontAlt);
                    MakeText(Root, "GRAND RE-OPENING SOON*", new Vector3(-20, BalconyY + 3.0f, BalconyZ - 0.45f), Quaternion.identity, 0.34f, C(0x39E5D0), 1.4f, SignFont);
                    break;

                case "neongalaxy":
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 p = new Vector3(-31, 0, -14 + i * 2.2f);
                        k.Push(p, Quaternion.Euler(0, 90, 0));
                        Color body = i % 2 == 0 ? C(0x2A1F5E) : C(0x5E1F4A);
                        k.Box(new Vector3(0, 1.1f, 0), new Vector3(1.4f, 2.2f, 1.1f), body);
                        k.Box(new Vector3(0, 1.45f, -0.52f), new Vector3(1.1f, 0.8f, 0.05f), C(i % 3 == 0 ? 0x3FE8FF : i % 3 == 1 ? 0xFF3FD0 : 0x7CF45A, 0.9f));
                        k.Box(new Vector3(0, 2.1f, -0.5f), new Vector3(1.3f, 0.25f, 0.1f), C(0xFFE066, 0.9f));
                        k.Box(new Vector3(0, 0.95f, -0.62f), new Vector3(1.2f, 0.12f, 0.35f), C(0x222222));
                        k.Pop();
                        ColBox(p + new Vector3(0, 1.1f, 0), new Vector3(1.1f, 2.2f, 1.4f), Quaternion.identity, "Arcade");
                    }
                    MakeText(Root, "ARCADE ZONE", new Vector3(-31, 3.2f, -8.5f), Quaternion.Euler(0, -90, 0), 0.7f, C(0xFF3FD0), 2.6f, SignFontAlt);
                    {
                        var ball = new MeshKit().Glossy(true);
                        ball.Sphere(Vector3.zero, 1.1f, 10, 16, new Color(0.85f, 0.85f, 0.95f, 0.25f));
                        var go = ball.Build("Disco Ball", Root, false);
                        go.transform.localPosition = new Vector3(0, 14.5f, 6);
                        spinners.Add(go.transform);
                        k.Tube(new Vector3(0, 15.5f, 6), new Vector3(0, 20, 6), 0.03f, 4, C(0x999999));
                    }
                    break;

                case "aurelia":
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var ch = new MeshKit().Glossy(true);
                        ch.Torus(Vector3.zero, 1.8f, 0.09f, 24, 6, C(0xD9B45A));
                        ch.Torus(new Vector3(0, -0.8f, 0), 1.1f, 0.07f, 20, 6, C(0xD9B45A));
                        ch.Tube(Vector3.zero, new Vector3(0, 6, 0), 0.04f, 4, C(0xD9B45A));
                        for (int b = 0; b < 12; b++)
                        {
                            float a = b * 30 * Mathf.Deg2Rad;
                            ch.Sphere(new Vector3(Mathf.Cos(a) * 1.8f, 0.18f, Mathf.Sin(a) * 1.8f), 0.13f, 4, 6, C(0xFFF1C0, 1f));
                        }
                        ch.Sphere(new Vector3(0, -1.2f, 0), 0.3f, 6, 10, C(0xE8F4FF, 0.6f));
                        var go = ch.Build("Chandelier", Root, false);
                        go.transform.localPosition = new Vector3(i * 13, 13.5f, 8);
                        swayers.Add(go.transform);
                    }
                    k.Box(new Vector3(0, 0.01f, 21), new Vector3(4, 0.02f, 10), C(0x8A2A3A));
                    break;

                case "skyport":
                    {
                        Vector3 bp = new Vector3(0, BalconyY + 6.2f, HallMaxZ - 0.35f);
                        k.Box(bp, new Vector3(18, 3.6f, 0.2f), C(0x15181C));
                        MakeText(Root, "DEPARTURES", bp + new Vector3(0, 1.35f, -0.2f), Quaternion.identity, 0.45f, C(0xFFC83A), 2.0f, SignFont);
                        string[] rows = { "GATE 23  FOUNTAIN CITY   DELAYED", "C14  NEW MALL        BOARDING", "C07  THE FOOD COURT  DELAYED", "C23  BARE CONCRETE   ON TIME?" };
                        for (int r = 0; r < rows.Length; r++)
                            MakeText(Root, rows[r], bp + new Vector3(0, 0.6f - r * 0.62f, -0.2f), Quaternion.identity, 0.3f, r == 3 ? C(0x7CF45A) : C(0xFFE08A), 1.8f, SignFont);
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        k.Push(new Vector3(26 - i * 1.6f, 0, -14 + i * 0.4f), Quaternion.Euler(0, 15 * i, 0));
                        k.Box(new Vector3(0, 0.35f, 0), new Vector3(1.2f, 0.08f, 0.8f), C(0x8A8F96));
                        k.Box(new Vector3(0, 0.8f, 0), new Vector3(0.8f, 0.8f, 0.5f), C(i == 0 ? 0x2F6FD6 : i == 1 ? 0xE84F4F : 0x3A3A3A));
                        k.Pop();
                    }
                    break;

                case "luckylagoon":
                    for (int side = -1; side <= 1; side += 2)
                        for (int i = 0; i < 5; i++)
                        {
                            Vector3 p = new Vector3(side * 31, 0, -12 + i * 2.1f);
                            k.Push(p, Quaternion.Euler(0, side * 90, 0));
                            k.Box(new Vector3(0, 1.0f, 0), new Vector3(1.3f, 2.0f, 1.0f), C(0x7A1022));
                            k.Box(new Vector3(0, 1.35f, -0.48f), new Vector3(1.0f, 0.55f, 0.05f), C(i % 2 == 0 ? 0xFFD34D : 0xFF3048, 0.9f));
                            k.Box(new Vector3(0, 2.15f, -0.2f), new Vector3(1.2f, 0.35f, 0.7f), C(0xFFD34D, 0.7f));
                            k.Tube(new Vector3(0.7f, 1.0f, 0), new Vector3(0.75f, 1.8f, 0), 0.04f, 6, C(0xC0C0C0));
                            k.Sphere(new Vector3(0.75f, 1.85f, 0), 0.1f, 4, 6, C(0xFF3048, 0.5f));
                            k.Pop();
                            ColBox(p + new Vector3(0, 1f, 0), new Vector3(1.0f, 2.0f, 1.3f), Quaternion.identity, "Slot");
                        }
                    {
                        var ch = new MeshKit().Glossy(true);
                        ch.Torus(Vector3.zero, 2.4f, 0.12f, 28, 6, C(0xD4AF37));
                        ch.Torus(new Vector3(0, -1f, 0), 1.5f, 0.1f, 24, 6, C(0xD4AF37));
                        ch.Tube(Vector3.zero, new Vector3(0, 6, 0), 0.05f, 4, C(0xD4AF37));
                        for (int b = 0; b < 16; b++)
                        {
                            float a = b * 22.5f * Mathf.Deg2Rad;
                            ch.Sphere(new Vector3(Mathf.Cos(a) * 2.4f, 0.2f, Mathf.Sin(a) * 2.4f), 0.15f, 4, 6, C(0xFFE0A0, 1f));
                        }
                        var go = ch.Build("Chandelier", Root, false);
                        go.transform.localPosition = new Vector3(0, 14, 10);
                        swayers.Add(go.transform);
                    }
                    break;

                case "eternity":
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = new Vector3(-24 + i * 16, BalconyY + 4.8f, BalconyZ - 0.5f);
                        var st = new MeshKit();
                        for (int s = 0; s < 8; s++)
                        {
                            var r = Quaternion.Euler(0, 0, s * 22.5f);
                            st.Push(Vector3.zero, r);
                            st.Box(new Vector3(0, 0, 0), new Vector3(0.08f, 2.2f, 0.08f), C(s % 2 == 0 ? 0xF08A3C : 0x3FB8AF, 0.3f));
                            st.Pop();
                        }
                        st.Sphere(Vector3.zero, 0.25f, 5, 8, C(0xFFE6C0, 0.8f));
                        var go = st.Build("Starburst", Root, false);
                        go.transform.localPosition = p;
                        spinners.Add(go.transform);
                    }
                    k.Push(new Vector3(25, 0, 2), Quaternion.Euler(0, -30, 0));
                    k.Cylinder(new Vector3(0, 0.25f, 0), 1.2f, 0.5f, 16, C(0x3FB8AF));
                    k.Cylinder(new Vector3(0, 1.6f, 0), 0.45f, 2.2f, 12, C(0xE8E0CC));
                    k.Cone(new Vector3(0, 2.7f, 0), 0.45f, 0.9f, 12, C(0xF08A3C));
                    for (int f = 0; f < 3; f++)
                    {
                        var r = Quaternion.Euler(0, f * 120, 0);
                        k.Push(Vector3.zero, r);
                        k.Box(new Vector3(0, 0.8f, 0.55f), new Vector3(0.08f, 0.9f, 0.5f), C(0xF08A3C));
                        k.Pop();
                    }
                    k.Box(new Vector3(0, 1.8f, -0.42f), new Vector3(0.3f, 0.3f, 0.05f), C(0x9FF0FF, 0.7f));
                    k.Pop();
                    break;
            }
            if (k.VertexCount > 0) k.Build("Signature Props", Root);
        }

        public void Animate(float time, float dt)
        {
            foreach (var s in spinners) if (s != null) s.Rotate(0, 0, dt * 25f, Space.Self);
            for (int i = 0; i < swayers.Count; i++)
                if (swayers[i] != null) swayers[i].localRotation = Quaternion.Euler(Mathf.Sin(time * 0.6f + i) * 1.2f, time * 4f, Mathf.Cos(time * 0.5f + i) * 1.2f);
        }
    }
}
