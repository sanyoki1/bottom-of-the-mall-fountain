// First-person view-model: your right hand (reaching, grabbing, feeding the kiosk) and whatever
// you carry things in, from bare fingers to a mop bucket to a shopping cart.
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class Hands
    {
        Transform root, arm, hand, held, container, fill;
        int heldType = -1, carryTier = -1;
        float grab, push, sway, heldPop;
        Vector3 swayOffset;
        Quaternion lastCamRot;
        Camera cam;
        public Transform Root => root;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        public void Build(Camera camera)
        {
            cam = camera;
            root = new GameObject("Hands").transform;
            root.SetParent(cam.transform, false);
            root.localPosition = Vector3.zero;

            arm = new GameObject("Arm").transform;
            arm.SetParent(root, false);
            arm.localScale = Vector3.one * 0.8f;
            var k = new MeshKit();
            Color sleeve = C(0x2F5E9E), cuff = C(0xF2B233), skin = C(0xE0B08A);
            // forearm along +z, hand at the far end
            k.Push(Vector3.zero, Quaternion.Euler(90, 0, 0));
            k.Cylinder(new Vector3(0, -0.12f, 0), 0.045f, 0.3f, 10, sleeve);
            k.Cylinder(new Vector3(0, 0.035f, 0), 0.048f, 0.03f, 10, cuff);
            k.Pop();
            var ak = k.Build("Sleeve", arm, false);
            SetLayerNoShadow(ak);

            hand = new GameObject("Hand").transform;
            hand.SetParent(arm, false);
            hand.localPosition = new Vector3(0, 0, 0.1f);
            var hk = new MeshKit();
            hk.Box(new Vector3(0, 0, 0.04f), new Vector3(0.075f, 0.03f, 0.08f), skin);
            // capsules stand along y, so tip each finger forward (+z)
            for (int i = 0; i < 4; i++)
            {
                hk.Push(new Vector3(-0.027f + i * 0.018f, 0.0f, 0.1f), Quaternion.Euler(90 + i * 4, 0, 0));
                hk.Capsule(Vector3.zero, 0.009f, 0.055f - Mathf.Abs(i - 1.5f) * 0.006f, skin, 6);
                hk.Pop();
            }
            hk.Push(new Vector3(0.045f, 0.0f, 0.06f), Quaternion.Euler(90, -35, 0));
            hk.Capsule(Vector3.zero, 0.011f, 0.05f, skin, 6);
            hk.Pop();
            var hgo = hk.Build("Palm", hand, false);
            SetLayerNoShadow(hgo);

            held = new GameObject("Held").transform;
            held.SetParent(hand, false);
            held.localPosition = new Vector3(0.0f, 0.03f, 0.1f);
            lastCamRot = cam.transform.rotation;
        }

        static void SetLayerNoShadow(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<MeshRenderer>())
            {
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
        }

        /// <summary>Show the top item of what you carry in your fingers (bare hands only).</summary>
        public void SetHeld(int type)
        {
            if (type == heldType) return;
            heldType = type;
            foreach (Transform c in held) Object.Destroy(c.gameObject);
            if (type < 0) return;
            var t = Content.Items[type];
            var go = Loot.MakeItemMesh(t.Shape, MeshKit.Hex(t.Color), Mathf.Min(1.2f, t.Scale * 1.05f), held);
            go.transform.localRotation = Quaternion.Euler(70, 0, 0);
            SetLayerNoShadow(go);
            heldPop = 1;
        }

        /// <summary>The carry container (cup, pail, bucket...) held in the left of the view.</summary>
        public void SetCarryTier(int tier)
        {
            if (tier == carryTier) return;
            carryTier = tier;
            if (container != null) Object.Destroy(container.gameObject);
            container = null;
            fill = null;
            if (tier <= 0) return;
            container = new GameObject("Container").transform;
            container.SetParent(root, false);
            var k = new MeshKit();
            var f = new MeshKit();
            switch (tier)
            {
                case 1: // paper cup
                    k.Frustum(new Vector3(0, 0.06f, 0), 0.035f, 0.048f, 0.12f, 14, C(0xF4F0E6), false);
                    k.Frustum(new Vector3(0, 0.06f, 0), 0.0355f, 0.0485f, 0.04f, 14, C(0xD8283A), false);
                    k.Ring(new Vector3(0, 0f, 0), 0, 0.035f, 14, C(0xF4F0E6));
                    f.Cylinder(Vector3.zero, 0.044f, 0.01f, 14, C(0xC77B43));
                    break;
                case 2: // sand pail
                    k.Frustum(new Vector3(0, 0.08f, 0), 0.07f, 0.09f, 0.16f, 16, C(0xE8453A), false);
                    k.Ring(Vector3.zero, 0, 0.07f, 16, C(0xE8453A));
                    k.Torus(new Vector3(0, 0.12f, 0), 0.092f, 0.004f, 16, 3, C(0xFFD23A));
                    f.Cylinder(Vector3.zero, 0.085f, 0.01f, 16, C(0xC77B43));
                    break;
                case 3: // mop bucket
                    k.Frustum(new Vector3(0, 0.12f, 0), 0.13f, 0.15f, 0.24f, 18, C(0xFFD000), false);
                    k.Ring(Vector3.zero, 0, 0.13f, 18, C(0xFFD000));
                    k.Torus(new Vector3(0, 0.235f, 0), 0.15f, 0.008f, 18, 3, C(0xE0B400));
                    f.Cylinder(Vector3.zero, 0.14f, 0.01f, 18, C(0xC77B43));
                    break;
                case 4: // fanny pack of holding
                    k.Ellipsoid(new Vector3(0, 0.06f, 0), new Vector3(0.16f, 0.08f, 0.07f), 6, 12, C(0x7A3FC8));
                    k.Box(new Vector3(0, 0.1f, 0.05f), new Vector3(0.22f, 0.012f, 0.02f), C(0x39E5D0));
                    f.Box(Vector3.zero, new Vector3(0.001f, 0.001f, 0.001f), C(0xC77B43));
                    break;
                case 5: // wheelbarrow: tray ahead, wheel at the front, handles coming up to your hands
                {
                    Color green = C(0x2F7F4F), steel = C(0x8A8F96), rubber = C(0x1A1A1A);
                    k.Push(new Vector3(0, 0, 0.15f), Quaternion.Euler(0, 45, 0));
                    k.Frustum(new Vector3(0, 0.1f, 0), 0.3f, 0.42f, 0.2f, 4, green, false, false);
                    k.Ring(Vector3.zero, 0, 0.3f, 4, green);
                    k.Pop();
                    k.Cylinder(new Vector3(0, -0.12f, 0.62f), Quaternion.Euler(0, 0, 90), 0.12f, 0.07f, 14, rubber);
                    k.Tube(new Vector3(-0.28f, -0.05f, -0.45f), new Vector3(-0.06f, -0.08f, 0.62f), 0.018f, 6, steel);
                    k.Tube(new Vector3(0.28f, -0.05f, -0.45f), new Vector3(0.06f, -0.08f, 0.62f), 0.018f, 6, steel);
                    f.Ellipsoid(Vector3.zero, new Vector3(0.26f, 0.08f, 0.26f), 4, 10, C(0xC77B43));
                    break;
                }
                case 6: // shopping cart: a wire basket, a child seat, one wonky wheel
                {
                    Color wire = C(0xC8CCD2), red = C(0xD8283A);
                    for (int i = 0; i <= 6; i++)
                    {
                        float x = -0.3f + i * 0.1f;
                        k.Box(new Vector3(x, 0.12f, 0.1f), new Vector3(0.008f, 0.26f, 0.008f), wire);
                        k.Box(new Vector3(x, -0.01f, 0.1f), new Vector3(0.008f, 0.008f, 0.8f), wire);
                    }
                    for (int j = 0; j <= 5; j++)
                    {
                        float z = -0.3f + j * 0.16f;
                        k.Box(new Vector3(-0.3f, 0.12f, z), new Vector3(0.008f, 0.26f, 0.008f), wire);
                        k.Box(new Vector3(0.3f, 0.12f, z), new Vector3(0.008f, 0.26f, 0.008f), wire);
                    }
                    k.Box(new Vector3(-0.3f, 0.25f, 0.1f), new Vector3(0.015f, 0.015f, 0.82f), wire);
                    k.Box(new Vector3(0.3f, 0.25f, 0.1f), new Vector3(0.015f, 0.015f, 0.82f), wire);
                    k.Box(new Vector3(0, 0.25f, 0.5f), new Vector3(0.62f, 0.015f, 0.015f), wire);
                    k.Box(new Vector3(0, 0.3f, -0.36f), new Vector3(0.66f, 0.04f, 0.04f), red);
                    k.Box(new Vector3(0, 0.2f, -0.2f), new Vector3(0.5f, 0.012f, 0.2f), red);
                    f.Ellipsoid(Vector3.zero, new Vector3(0.28f, 0.1f, 0.36f), 4, 10, C(0xC77B43));
                    break;
                }
                case 7: // ride-on floor scrubber: dashboard, steering wheel, hopper
                {
                    Color blue = C(0x2E6FD6), dark = C(0x222428);
                    k.Box(new Vector3(0, -0.1f, 0.35f), new Vector3(0.9f, 0.3f, 0.6f), blue);
                    k.Box(new Vector3(0, 0.07f, 0.12f), new Vector3(0.5f, 0.06f, 0.2f), dark);
                    k.Box(new Vector3(0.15f, 0.1f, 0.14f), new Vector3(0.06f, 0.03f, 0.04f), C(0x6CFF9A, 0.9f));
                    k.Box(new Vector3(-0.1f, 0.1f, 0.14f), new Vector3(0.05f, 0.03f, 0.04f), C(0xFF4040, 0.9f));
                    k.Push(new Vector3(0, 0.12f, 0.0f), Quaternion.Euler(-55, 0, 0));
                    k.Torus(Vector3.zero, 0.16f, 0.015f, 18, 4, dark);
                    k.Box(Vector3.zero, new Vector3(0.3f, 0.02f, 0.02f), dark);
                    k.Pop();
                    k.Frustum(new Vector3(0, 0.08f, 0.5f), 0.2f, 0.3f, 0.16f, 12, C(0x3A3A3A), false);
                    f.Ellipsoid(Vector3.zero, new Vector3(0.25f, 0.08f, 0.25f), 4, 10, C(0xC77B43));
                    break;
                }
                case 8: // shop-vac backpack: all you see is the nozzle and the hose
                {
                    Color hose = C(0x3A3A3A), vac = C(0xE8C020);
                    k.Tube(new Vector3(0.1f, -0.3f, -0.1f), new Vector3(0.05f, -0.02f, 0.2f), 0.035f, 8, hose);
                    k.Tube(new Vector3(0.05f, -0.02f, 0.2f), new Vector3(0.02f, -0.1f, 0.62f), 0.035f, 8, vac);
                    k.Frustum(new Vector3(0.02f, -0.12f, 0.68f), 0.06f, 0.035f, 0.08f, 10, vac);
                    f.Box(Vector3.zero, new Vector3(0.001f, 0.001f, 0.001f), C(0xC77B43));
                    break;
                }
                default: // industrial hopper suit: a funnel on your chest and hazard stripes
                {
                    k.Frustum(new Vector3(0, 0.0f, 0.25f), 0.12f, 0.36f, 0.3f, 16, C(0xE8B000), false);
                    for (int i = 0; i < 6; i++)
                        k.Box(new Vector3(-0.25f + i * 0.1f, 0.16f, 0.52f), new Vector3(0.05f, 0.02f, 0.02f), C(0x1A1A1A));
                    f.Ellipsoid(Vector3.zero, new Vector3(0.28f, 0.1f, 0.28f), 4, 10, C(0xC77B43));
                    break;
                }
            }
            var go = k.Build("Body", container, false);
            SetLayerNoShadow(go);
            fill = f.Build("Fill", container, false).transform;
            SetLayerNoShadow(fill.gameObject);
            container.localScale = Vector3.one * (tier == 8 ? 0.45f : tier >= 5 ? 1f : tier == 3 ? 0.62f : 0.8f);
            container.localPosition = ContainerHome;
        }

        Vector3 ContainerHome => carryTier == 8 ? new Vector3(-0.3f, -0.3f, 0.3f) : carryTier >= 5 ? new Vector3(-0.05f, -0.8f, 1.15f) : new Vector3(-0.26f, -0.32f, 0.52f);

        // ── the grab tool (or dig tool) in your right hand ─────────────────────

        Transform tool;
        int toolTier = -1;
        float swing;

        public void Swing() { swing = 1; }

        /// <summary>Hold a dig tool instead of a grab tool (tier into Content.DigTools).</summary>
        public void SetDigTool(int tier)
        {
            int key = 100 + tier;
            if (key == toolTier) return;
            toolTier = key;
            if (tool != null) Object.Destroy(tool.gameObject);
            tool = new GameObject("Dig Tool").transform;
            tool.SetParent(hand, false);
            var k = new MeshKit();
            Color wood = C(0x8A5A34), steel = C(0xB8BEC4), dark = C(0x2A2A2A);
            switch (tier)
            {
                case 0: // nothing: an empty hand, making a digging motion, hopefully
                    break;
                case 1: // sandbox shovel
                    k.Tube(new Vector3(0, 0, -0.02f), new Vector3(0, -0.02f, 0.45f), 0.015f, 6, C(0xFFD000));
                    k.Box(new Vector3(0, -0.04f, 0.55f), new Vector3(0.12f, 0.02f, 0.16f), C(0xFFB000));
                    break;
                case 2: // snow shovel
                    k.Tube(new Vector3(0, 0, -0.05f), new Vector3(0, -0.04f, 0.8f), 0.018f, 6, dark);
                    k.Box(new Vector3(0, -0.06f, 0.95f), new Vector3(0.4f, 0.03f, 0.3f), C(0x3A7BD5));
                    break;
                case 3: // pickaxe
                    k.Tube(new Vector3(0, 0, -0.05f), new Vector3(0, 0, 0.7f), 0.02f, 6, wood);
                    k.Push(new Vector3(0, 0, 0.72f), Quaternion.Euler(0, 0, 0));
                    k.Tube(new Vector3(0, 0.22f, 0), new Vector3(0, -0.22f, 0.05f), 0.025f, 6, steel);
                    k.Pop();
                    break;
                case 4: // jackhammer
                    k.Box(new Vector3(0, 0, 0.12f), new Vector3(0.14f, 0.14f, 0.35f), C(0xE8C020));
                    k.Tube(new Vector3(0, 0, 0.3f), new Vector3(0, -0.02f, 0.75f), 0.025f, 6, steel);
                    k.Box(new Vector3(0, 0.08f, -0.05f), new Vector3(0.3f, 0.03f, 0.03f), dark);
                    break;
                default: // handheld borer
                    k.Cylinder(new Vector3(0, 0, 0.2f), Quaternion.Euler(90, 0, 0), 0.12f, 0.4f, 14, C(0x8A8F96));
                    k.Cone(new Vector3(0, 0, 0.4f), 0.12f, 0.25f, 14, steel);
                    k.Push(new Vector3(0, 0, 0.4f), Quaternion.Euler(90, 0, 0));
                    k.Cone(Vector3.zero, 0.12f, 0.25f, 14, steel);
                    k.Pop();
                    break;
            }
            if (k.VertexCount > 0) SetLayerNoShadow(k.Build("Model", tool, false));
        }

        public void SetTool(int tier)
        {
            if (tier == toolTier) return;
            toolTier = tier;
            if (tool != null) Object.Destroy(tool.gameObject);
            tool = null;
            if (tier <= 0) return;
            tool = new GameObject("Tool").transform;
            tool.SetParent(hand, false);
            var k = new MeshKit();
            Color alu = C(0xB8BEC4), yellow = C(0xFFD000), dark = C(0x2A2A2A);
            switch (tier)
            {
                case 1: // litter grabber
                    k.Tube(new Vector3(0, 0, 0.02f), new Vector3(0, -0.02f, 0.75f), 0.01f, 6, alu);
                    k.Box(new Vector3(0, -0.03f, 0.04f), new Vector3(0.03f, 0.06f, 0.05f), yellow);
                    k.Box(new Vector3(0.018f, -0.03f, 0.78f), new Vector3(0.008f, 0.02f, 0.07f), dark);
                    k.Box(new Vector3(-0.018f, -0.03f, 0.78f), new Vector3(0.008f, 0.02f, 0.07f), dark);
                    break;
                case 2: // pool skimmer net
                    k.Tube(new Vector3(0, 0, -0.05f), new Vector3(0, -0.05f, 0.95f), 0.012f, 6, C(0x3A7BD5));
                    k.Push(new Vector3(0, -0.07f, 1.08f), Quaternion.Euler(-70, 0, 0));
                    k.Torus(Vector3.zero, 0.13f, 0.008f, 16, 3, alu);
                    k.Push(Vector3.zero, Quaternion.Euler(180, 0, 0));
                    k.Cone(Vector3.zero, 0.13f, 0.12f, 12, new Color(0.85f, 0.9f, 0.95f, 0));
                    k.Pop();
                    k.Pop();
                    break;
                case 3: // coin rake
                    k.Tube(new Vector3(0, 0, -0.05f), new Vector3(0, -0.05f, 0.95f), 0.013f, 6, C(0x8A5A34));
                    k.Box(new Vector3(0, -0.06f, 0.97f), new Vector3(0.36f, 0.03f, 0.03f), alu);
                    for (int i = 0; i < 9; i++) k.Box(new Vector3(-0.16f + i * 0.04f, -0.1f, 0.99f), new Vector3(0.008f, 0.07f, 0.008f), alu);
                    break;
                case 4: // detector magnet
                    k.Tube(new Vector3(0, 0, -0.05f), new Vector3(0, -0.06f, 0.85f), 0.012f, 6, dark);
                    k.Cylinder(new Vector3(0, -0.08f, 0.9f), Quaternion.Euler(-20, 0, 0), 0.11f, 0.02f, 16, C(0x3A3F48));
                    k.Box(new Vector3(0, 0.03f, 0.25f), new Vector3(0.06f, 0.05f, 0.12f), C(0x3A3F48));
                    k.Box(new Vector3(0, 0.06f, 0.25f), new Vector3(0.03f, 0.01f, 0.05f), C(0x5AD8FF, 1f));
                    k.Torus(new Vector3(0, -0.1f, 0.92f), 0.06f, 0.018f, 12, 4, C(0xD8283A));
                    break;
                case 5: // reverse leaf blower
                    k.Box(new Vector3(0, 0.01f, 0.08f), new Vector3(0.09f, 0.11f, 0.18f), C(0xFF7A1A));
                    k.Tube(new Vector3(0, -0.02f, 0.18f), new Vector3(0, -0.12f, 0.95f), 0.04f, 10, C(0xFF9A3A));
                    k.Frustum(new Vector3(0, -0.13f, 0.98f), 0.05f, 0.07f, 0.05f, 10, dark);
                    break;
                default: // industrial magnet glove
                    k.Box(new Vector3(0, 0, 0.05f), new Vector3(0.11f, 0.06f, 0.16f), C(0x3A3F48));
                    for (int i = 0; i < 3; i++) k.Torus(new Vector3(0, 0, 0.0f + i * 0.05f), 0.06f, 0.01f, 12, 3, C(0xE8A020, 0.6f));
                    k.Sphere(new Vector3(0, 0, 0.14f), 0.035f, 5, 8, C(0x7AE8FF, 1f));
                    break;
            }
            var go = k.Build("Model", tool, false);
            SetLayerNoShadow(go);
        }

        public void SetFill(float frac)
        {
            if (fill == null || carryTier <= 0) return;
            frac = Mathf.Clamp01(frac);
            fill.gameObject.SetActive(frac > 0.001f);
            float h = carryTier == 1 ? 0.12f : carryTier == 2 ? 0.16f : carryTier == 3 ? 0.24f : 0.12f;
            fill.localPosition = new Vector3(0, Mathf.Lerp(0.005f, h * 0.9f, frac), 0);
            float s = carryTier >= 5 ? Mathf.Lerp(0.3f, 1.3f, frac) : 1;
            fill.localScale = new Vector3(s, carryTier >= 5 ? s : 1, s);
        }

        public void Grab() { grab = 1; }
        public void Push() { push = 1; }

        public void Update(float dt, float bobPhase, float bobAmount)
        {
            if (root == null) return;
            // weapon-style sway from camera rotation
            var q = cam.transform.rotation;
            var delta = Quaternion.Inverse(lastCamRot) * q;
            lastCamRot = q;
            Vector3 e = delta.eulerAngles;
            float yawD = Mathf.DeltaAngle(0, e.y), pitchD = Mathf.DeltaAngle(0, e.x);
            swayOffset = Vector3.Lerp(swayOffset, new Vector3(-yawD * 0.004f, pitchD * 0.004f, 0), 1 - Mathf.Exp(-dt * 10));
            swayOffset = Vector3.ClampMagnitude(swayOffset, 0.05f);
            Vector3 bob = new Vector3(Mathf.Sin(bobPhase) * 0.012f, -Mathf.Abs(Mathf.Sin(bobPhase)) * 0.014f, 0) * bobAmount;

            grab = Mathf.Max(0, grab - dt * 5f);
            push = Mathf.Max(0, push - dt * 3f);
            swing = Mathf.Max(0, swing - dt * 4f);
            float sw = swing > 0.6f ? (1 - swing) / 0.4f : swing / 0.6f;   // quick raise, heavy drop
            heldPop = Mathf.Max(0, heldPop - dt * 4f);
            float g = Mathf.Sin(grab * Mathf.PI);
            float p = Mathf.Sin(push * Mathf.PI);
            Vector3 basePos = new Vector3(0.25f, -0.27f, 0.4f);
            Vector3 reach = new Vector3(-0.1f, -0.08f, 0.22f) * g + new Vector3(-0.08f, 0.04f, 0.2f) * p;
            arm.localPosition = basePos + bob + swayOffset + reach + new Vector3(-0.05f, 0.1f, -0.05f) * sw;
            arm.localRotation = Quaternion.Euler(8 + g * 25 - p * 10 - sw * 55 + (swing > 0 && swing < 0.6f ? 30 * (1 - swing / 0.6f) : 0), -12 - g * 8, -8);
            held.localScale = Vector3.one * (1 + heldPop * 0.3f);
            if (container != null && carryTier >= 5 && carryTier != 8)
            {
                // barrows, carts and scrubbers sit on the floor in front of you and follow your heading, not your gaze
                Vector3 fwd = cam.transform.forward;
                fwd.y = 0;
                if (fwd.sqrMagnitude < 1e-4f) fwd = cam.transform.up;
                fwd.y = 0;
                fwd.Normalize();
                container.position = cam.transform.position + fwd * 1.05f + Vector3.down * (1.25f - bob.y) + cam.transform.right * swayOffset.x * 0.5f;
                container.rotation = Quaternion.LookRotation(fwd);
            }
            else if (container != null)
            {
                container.localPosition = ContainerHome + bob * 1.3f + swayOffset * 1.2f + new Vector3(0.04f, 0.03f, 0) * g;
                container.localRotation = Quaternion.Euler(-8 + g * 10, 10, 0);
            }
        }
    }
}
