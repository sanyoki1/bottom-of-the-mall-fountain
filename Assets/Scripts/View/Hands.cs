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
                default: // big stuff: a heaped tray you push along
                    k.Box(new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.1f, 0.34f), tier == 5 ? C(0x2F7F4F) : tier == 6 ? C(0xC8CCD2) : C(0x2E6FD6));
                    k.Box(new Vector3(0, 0.1f, 0), new Vector3(0.46f, 0.02f, 0.3f), C(0x2A2A2A));
                    f.Ellipsoid(Vector3.zero, new Vector3(0.2f, 0.06f, 0.13f), 4, 10, C(0xC77B43));
                    break;
            }
            var go = k.Build("Body", container, false);
            SetLayerNoShadow(go);
            fill = f.Build("Fill", container, false).transform;
            SetLayerNoShadow(fill.gameObject);
            container.localScale = Vector3.one * (tier >= 5 ? 1f : tier == 3 ? 0.62f : 0.8f);
            container.localPosition = ContainerHome;
        }

        Vector3 ContainerHome => carryTier >= 5 ? new Vector3(-0.05f, -0.5f, 0.8f) : new Vector3(-0.26f, -0.32f, 0.52f);

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
            heldPop = Mathf.Max(0, heldPop - dt * 4f);
            float g = Mathf.Sin(grab * Mathf.PI);
            float p = Mathf.Sin(push * Mathf.PI);
            Vector3 basePos = new Vector3(0.25f, -0.27f, 0.4f);
            Vector3 reach = new Vector3(-0.1f, -0.08f, 0.22f) * g + new Vector3(-0.08f, 0.04f, 0.2f) * p;
            arm.localPosition = basePos + bob + swayOffset + reach;
            arm.localRotation = Quaternion.Euler(8 + g * 25 - p * 10, -12 - g * 8, -8);
            held.localScale = Vector3.one * (1 + heldPop * 0.3f);
            if (container != null)
            {
                container.localPosition = ContainerHome + bob * 1.3f + swayOffset * 1.2f + new Vector3(0.04f, 0.03f, 0) * g;
                container.localRotation = Quaternion.Euler(-8 + g * 10, 10, 0);
            }
        }
    }
}
