// The COIN-O-MATIC 3000: a grimy coin-counting kiosk in the custodial corner. You feed it what you
// carry; the screen counts up, the beacon spins and a receipt with a joke curls out of the slot.
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class Kiosk
    {
        public static readonly Vector3 Spot = new Vector3(-6.5f, 0, -15f);
        public Transform Root { get; private set; }
        public Vector3 Front => Root.position + Root.forward * 0.9f + Vector3.up * 1.1f;
        public Vector3 Funnel => Root.TransformPoint(new Vector3(0, 1.08f, 0.55f));
        TextMesh screen, screenSub;
        Transform receipt, beacon, beaconGlow;
        float countT = -1, countDur, receiptT = -1, idleT, flash;
        double countTo;
        Material bodyFlash;

        static Color C(uint hex, float glow = 0) => MeshKit.Hex(hex, glow);

        public void Build(Transform parent)
        {
            Root = new GameObject("COIN-O-MATIC 3000").transform;
            Root.SetParent(parent, false);
            Root.position = Spot;
            Vector3 look = new Vector3(0, 0, -11) - Spot;
            Root.rotation = Quaternion.LookRotation(look.normalized);

            var k = new MeshKit();
            Color body = C(0x2E8C7A), dark = C(0x1D5A4F), steel = C(0xB8BEC4), black = C(0x16181A);
            // cabinet with a grimy lower half
            k.Box(new Vector3(0, 0.95f, 0), new Vector3(1.2f, 1.9f, 0.85f), body);
            k.Box(new Vector3(0, 0.3f, 0.005f), new Vector3(1.21f, 0.6f, 0.86f), dark);
            k.Box(new Vector3(0, 0.04f, 0), new Vector3(1.26f, 0.08f, 0.9f), black);
            // header sign
            k.Box(new Vector3(0, 2.12f, 0.02f), new Vector3(1.34f, 0.46f, 0.92f), C(0xFFD34D, 0.16f));
            k.Box(new Vector3(0, 1.88f, 0.02f), new Vector3(1.36f, 0.04f, 0.94f), C(0xD8283A));
            k.Box(new Vector3(0, 2.36f, 0.02f), new Vector3(1.36f, 0.04f, 0.94f), C(0xD8283A));
            // screen bezel + screen
            k.Box(new Vector3(0, 1.5f, 0.43f), new Vector3(0.72f, 0.44f, 0.04f), black);
            k.Box(new Vector3(0, 1.5f, 0.452f), new Vector3(0.62f, 0.34f, 0.01f), C(0x0B2616, 0.35f));
            // coin funnel tray
            k.Push(new Vector3(0, 1.05f, 0.52f), Quaternion.Euler(18, 0, 0));
            k.Box(Vector3.zero, new Vector3(0.8f, 0.05f, 0.34f), steel);
            k.Box(new Vector3(-0.41f, 0.06f, 0), new Vector3(0.03f, 0.14f, 0.34f), steel);
            k.Box(new Vector3(0.41f, 0.06f, 0), new Vector3(0.03f, 0.14f, 0.34f), steel);
            k.Box(new Vector3(0, 0.06f, 0.17f), new Vector3(0.84f, 0.14f, 0.03f), steel);
            k.Pop();
            k.Box(new Vector3(0, 1.0f, 0.43f), new Vector3(0.3f, 0.06f, 0.03f), black);
            // receipt slot and coin return
            k.Box(new Vector3(0.38f, 1.24f, 0.43f), new Vector3(0.24f, 0.03f, 0.03f), black);
            k.Box(new Vector3(0, 0.42f, 0.44f), new Vector3(0.4f, 0.22f, 0.04f), steel);
            k.Box(new Vector3(0, 0.4f, 0.46f), new Vector3(0.32f, 0.14f, 0.02f), black);
            // stickers and a taped note
            k.Box(new Vector3(-0.38f, 1.24f, 0.428f), new Vector3(0.26f, 0.12f, 0.005f), C(0xF4F0E0));
            k.Box(new Vector3(0.36f, 0.72f, 0.428f), new Vector3(0.2f, 0.2f, 0.005f), C(0xFFE45A));
            k.Box(new Vector3(-0.3f, 0.72f, 0.428f), new Vector3(0.34f, 0.1f, 0.005f), C(0xFF7AA8));
            // beacon on top
            k.Cylinder(new Vector3(0, 2.4f, 0), 0.09f, 0.06f, 10, black);
            k.Build("Cabinet", Root);

            var bk = new MeshKit();
            bk.Cylinder(Vector3.zero, 0.08f, 0.16f, 10, C(0xFF9A2A, 0.8f));
            bk.Box(new Vector3(0.05f, 0, 0), new Vector3(0.04f, 0.12f, 0.1f), C(0xFFF2B0, 1f));
            beacon = bk.Build("Beacon", Root, false).transform;
            beacon.localPosition = new Vector3(0, 2.51f, 0);
            beaconGlow = new GameObject("Beacon Glow").transform;
            beaconGlow.SetParent(beacon, false);
            beaconGlow.gameObject.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
            var gmr = beaconGlow.gameObject.AddComponent<MeshRenderer>();
            gmr.sharedMaterial = Mats.NewGlow(TexKit.SoftDot, 2f, new Color(1f, 0.6f, 0.2f));
            gmr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            beaconGlow.localScale = Vector3.one * 0.01f;
            beaconGlow.gameObject.SetActive(false);

            // text faces -z by default; the kiosk's front is +z
            var face = Quaternion.Euler(0, 180, 0);
            WorldBuilder.MakeText(Root, "COIN-O-MATIC", new Vector3(0, 2.2f, 0.485f), face, 0.26f, C(0xD8283A), 1.0f, WorldBuilder.SignFontAlt);
            WorldBuilder.MakeText(Root, "3000", new Vector3(0, 2.0f, 0.485f), face, 0.17f, C(0x1D1D1F), 0.8f, WorldBuilder.SignFontAlt);
            WorldBuilder.MakeText(Root, "WE COUNT.\nYOU WISH.", new Vector3(-0.38f, 1.24f, 0.433f), face, 0.065f, C(0x1D1D1F), 0.7f);
            WorldBuilder.MakeText(Root, "NO\nREFUNDS", new Vector3(0.36f, 0.72f, 0.433f), face, 0.065f, C(0xD8283A), 0.8f);
            WorldBuilder.MakeText(Root, "9% FEE", new Vector3(-0.3f, 0.72f, 0.433f), face, 0.085f, C(0x1D1D1F), 0.7f);
            WorldBuilder.MakeText(Root, "INSERT COINS", new Vector3(0, 1.1f, 0.47f), face, 0.05f, C(0xC8CCD2), 0.9f);
            screen = WorldBuilder.MakeText(Root, "INSERT COINS", new Vector3(0, 1.55f, 0.46f), face, 0.075f, C(0x6CFF9A), 2.2f);
            screenSub = WorldBuilder.MakeText(Root, "ANY COINS", new Vector3(0, 1.43f, 0.46f), face, 0.042f, C(0x6CFF9A), 1.6f);

            // receipt paper, scaled out of the slot when printing
            var rk = new MeshKit();
            rk.Box(new Vector3(0, -0.5f, 0), new Vector3(0.18f, 1f, 0.004f), C(0xF7F4EA));
            rk.Box(new Vector3(0, -0.2f, 0.003f), new Vector3(0.12f, 0.02f, 0.002f), C(0x4A4A4A));
            rk.Box(new Vector3(0, -0.35f, 0.003f), new Vector3(0.1f, 0.02f, 0.002f), C(0x4A4A4A));
            rk.Box(new Vector3(0, -0.6f, 0.003f), new Vector3(0.14f, 0.02f, 0.002f), C(0x4A4A4A));
            receipt = rk.Build("Receipt", Root, false).transform;
            receipt.localPosition = new Vector3(0.38f, 1.235f, 0.455f);
            receipt.localRotation = Quaternion.Euler(-12, 0, 0);
            // never scale a lit mesh to exactly zero: the singular normal matrix makes NaN pixels that bloom smears into black squares
            receipt.localScale = new Vector3(1, 0.01f, 1);
            receipt.gameObject.SetActive(false);

            var col = Root.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0, 1.25f, 0);
            col.size = new Vector3(1.36f, 2.5f, 1.0f);
            var ct = Root.gameObject.AddComponent<ClickTarget>();
            ct.Kind = "kiosk";
        }

        /// <summary>Play the counting animation for a deposit worth amount.</summary>
        public void Count(double amount, int items)
        {
            countTo = amount;
            countDur = Mathf.Clamp(0.35f + items * 0.04f, 0.4f, 1.6f);
            countT = 0;
            receiptT = -1;
            flash = 1;
        }

        public void Say(string line)
        {
            screen.text = "HELLO?";
            screenSub.text = Wrap(line, 22);
            idleT = 3.5f;
        }

        static string Wrap(string s, int width)
        {
            var words = s.Split(' ');
            var sb = new System.Text.StringBuilder();
            int line = 0;
            foreach (var w in words)
            {
                if (line + w.Length > width && line > 0) { sb.Append('\n'); line = 0; }
                else if (line > 0) { sb.Append(' '); line++; }
                sb.Append(w);
                line += w.Length;
            }
            return sb.ToString();
        }

        public void Update(float dt, float time)
        {
            flash = Mathf.Max(0, flash - dt * 1.5f);
            if (countT >= 0)
            {
                countT += dt;
                float t = Mathf.Clamp01(countT / countDur);
                screen.text = Fmt.Money(countTo * t);
                screenSub.text = t < 1 ? "COUNTING" + new string('.', 1 + (int)(time * 6) % 3) : "THANK YOU!";
                beacon.localRotation = Quaternion.Euler(0, time * 720, 0);
                beaconGlow.gameObject.SetActive(true);
                beaconGlow.localScale = Vector3.one * (0.5f + Mathf.Abs(Mathf.Sin(time * 12)) * 0.5f);
                if (t >= 1 && receiptT < 0) receiptT = 0;
                if (countT > countDur + 2.5f) { countT = -1; idleT = 0; }
            }
            else
            {
                beaconGlow.localScale = Vector3.MoveTowards(beaconGlow.localScale, Vector3.one * 0.01f, dt * 3);
                if (beaconGlow.localScale.x <= 0.02f) beaconGlow.gameObject.SetActive(false);
                if (idleT > 0) idleT -= dt;
                else
                {
                    screen.text = ((int)(time / 2.5f)) % 2 == 0 ? "INSERT COINS" : "COIN-O-MATIC";
                    screenSub.text = ((int)(time / 2.5f)) % 2 == 0 ? "ANY COINS" : "COUNTING SINCE 1991";
                }
            }
            if (receiptT >= 0)
            {
                receiptT += dt;
                float grow = Mathf.Clamp01(receiptT / 0.6f) * 0.28f;
                float fall = receiptT > 3f ? Mathf.Clamp01((receiptT - 3f) / 0.4f) : 0;
                receipt.gameObject.SetActive(true);
                receipt.localScale = new Vector3(1, Mathf.Max(0.01f, grow * (1 - fall)), 1);
                receipt.localRotation = Quaternion.Euler(-12 - Mathf.Sin(receiptT * 7) * 4 * (1 - fall), 0, 0);
                if (receiptT > 3.5f) { receiptT = -1; receipt.gameObject.SetActive(false); }
            }
        }
    }
}
