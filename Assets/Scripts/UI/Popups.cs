// Transient UI: world-anchored floating numbers, stacked toasts, big centre banners, and the
// speech-bubble quotes that appear when you catch a True Wish.
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WishExtractor.Core;

namespace WishExtractor.UI
{
    public sealed class Popups
    {
        RectTransform root;
        Canvas canvas;
        Camera cam;

        // floating numbers
        sealed class Floater { public RectTransform Rt; public Text T; public Outline O; public Vector3 World; public float Age, Life, Scale; public Color Col; }
        readonly List<Floater> floaters = new List<Floater>();
        readonly Stack<Floater> floaterPool = new Stack<Floater>();

        // toasts
        sealed class ToastCard { public RectTransform Rt; public CanvasGroup G; public float Age, Life; public float Y; }
        readonly List<ToastCard> toasts = new List<ToastCard>();
        readonly Queue<(string title, string detail, Color color, string glyph, float life)> toastQueue = new Queue<(string, string, Color, string, float)>();

        // banner
        RectTransform banner;
        CanvasGroup bannerGroup;
        Text bannerKicker, bannerTitle, bannerSub;
        float bannerAge = 99, bannerLife;
        readonly Queue<(string kicker, string title, string sub, Color color, float life)> bannerQueue = new Queue<(string, string, string, Color, float)>();

        // wish bubbles
        sealed class Bubble { public RectTransform Rt; public CanvasGroup G; public Vector3 World; public float Age, Life; }
        readonly List<Bubble> bubbles = new List<Bubble>();

        public void Build(Canvas c, Camera camera)
        {
            canvas = c;
            cam = camera;
            root = UIKit.Rect(c.transform, "Popups").Stretch();
            BuildBanner();
        }

        Vector2 ToCanvas(Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, null, out var local);
            return local;
        }

        // ── floating numbers ──────────────────────────────────────────────

        public void Float(Vector3 world, string text, Color color, float scale)
        {
            if (floaters.Count > 60) return;
            var f = floaterPool.Count > 0 ? floaterPool.Pop() : NewFloater();
            f.Rt.gameObject.SetActive(true);
            f.World = world + Random.insideUnitSphere * 0.25f;
            f.T.text = text;
            f.Col = color;
            f.T.color = color;
            f.T.fontSize = Mathf.RoundToInt(26 * scale);
            f.Age = 0;
            f.Life = 1.15f;
            f.Scale = scale;
            floaters.Add(f);
        }

        Floater NewFloater()
        {
            var t = UIKit.Label(root, "Float", "", 26, Color.white, TextAnchor.MiddleCenter, UIKit.Bold);
            t.rectTransform.sizeDelta = new Vector2(400, 60);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.55f);
            o.effectDistance = new Vector2(1.6f, -1.6f);
            return new Floater { Rt = t.rectTransform, T = t, O = o };
        }

        // ── toasts ────────────────────────────────────────────────────────

        public void Toast(string title, string detail, Color color, string glyph = "★", float life = 3.6f)
        {
            toastQueue.Enqueue((title, detail, color, glyph, life));
        }

        void SpawnToast((string title, string detail, Color color, string glyph, float life) d)
        {
            var rt = UIKit.Card(root, "Toast", Pal.GlassStrong, 20);
            rt.anchorMin = rt.anchorMax = new Vector2(0, 0);
            rt.pivot = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(480, 66);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            var dot = UIKit.Dot(rt, "Icon", d.color, 40);
            dot.rectTransform.TL(13, 13, 40, 40);
            var gl = UIKit.Label(dot.rectTransform, "G", d.glyph, 22, Color.white, TextAnchor.MiddleCenter, UIKit.Symbol);
            gl.rectTransform.Stretch();
            var t = UIKit.Label(rt, "Title", d.title, 18, Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold);
            t.rectTransform.TL(64, 9, 406, 24);
            var s = UIKit.Label(rt, "Detail", d.detail, 14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            s.rectTransform.TL(64, 33, 406, 30);
            toasts.Add(new ToastCard { Rt = rt, G = g, Life = d.life, Y = 120 });
        }

        // ── banner ────────────────────────────────────────────────────────

        void BuildBanner()
        {
            banner = UIKit.Rect(root, "Banner").Place(new Vector2(0.5f, 0.5f), new Vector2(-40, 170), new Vector2(1100, 170));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.alpha = 0;
            bannerKicker = UIKit.Label(banner, "Kicker", "", 20, Color.white, TextAnchor.MiddleCenter, UIKit.Semibold);
            bannerKicker.rectTransform.TL(0, 0, 1100, 30);
            bannerTitle = UIKit.Label(banner, "Title", "", 58, Color.white, TextAnchor.MiddleCenter, UIKit.Bold);
            bannerTitle.rectTransform.TL(0, 30, 1100, 76);
            bannerSub = UIKit.Label(banner, "Sub", "", 20, Color.white, TextAnchor.UpperCenter, UIKit.Regular, true);
            bannerSub.rectTransform.TL(150, 108, 800, 60);
            foreach (var t in new[] { bannerKicker, bannerTitle, bannerSub })
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.55f);
                sh.effectDistance = new Vector2(2, -3);
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.25f);
            }
        }

        public void Banner(string kicker, string title, string sub, Color color, float life = 4f)
        {
            bannerQueue.Enqueue((kicker, title, sub, color, life));
        }

        /// <summary>Drop any banner that is showing or queued (e.g. when a new mall starts).</summary>
        public void ClearBanners()
        {
            bannerQueue.Clear();
            bannerAge = bannerLife = 99;
            bannerGroup.alpha = 0;
        }

        // ── wish bubbles ──────────────────────────────────────────────────

        public void WishQuote(Vector3 world, WishDef w, double value, bool first)
        {
            while (bubbles.Count >= 3) { Object.Destroy(bubbles[0].Rt.gameObject); bubbles.RemoveAt(0); }
            var rt = UIKit.Card(root, "Wish", new Color(1, 1, 1, 0.95f), 20);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0);
            rt.sizeDelta = new Vector2(430, 104);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            var col = Pal.Rarity[(int)w.Rarity];
            var kick = UIKit.Label(rt, "Kicker", (first ? "NEW WISH  ·  " : "") + View.RarityColors.Names[(int)w.Rarity].ToUpperInvariant() + " WISH", 12, col, TextAnchor.UpperLeft, UIKit.Semibold);
            kick.rectTransform.TL(18, 12, 300, 18);
            var val = UIKit.Label(rt, "Value", "+" + Fmt.Money(value), 15, Pal.Green, TextAnchor.UpperRight, UIKit.Bold);
            val.rectTransform.TL(230, 10, 182, 22);
            var q = UIKit.Label(rt, "Quote", "“" + w.Text + "”", 17, Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold, true);
            q.rectTransform.TL(18, 34, 394, 64);
            q.fontStyle = FontStyle.Italic;
            bubbles.Add(new Bubble { Rt = rt, G = g, World = world, Life = 4.2f });
        }

        // ── update ────────────────────────────────────────────────────────

        public void Update(float dt)
        {
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.Age += dt;
                float t = f.Age / f.Life;
                if (t >= 1) { f.Rt.gameObject.SetActive(false); floaters.RemoveAt(i); floaterPool.Push(f); continue; }
                Vector3 sp = cam.WorldToScreenPoint(f.World + Vector3.up * t * 1.8f);
                if (sp.z < 0) { f.Rt.gameObject.SetActive(false); continue; }
                f.Rt.gameObject.SetActive(true);
                f.Rt.anchoredPosition = ToCanvas(sp);
                float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.15f, t / 0.15f) : Mathf.Lerp(1.15f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
                f.Rt.localScale = Vector3.one * pop;
                var c = f.Col;
                c.a = t > 0.65f ? 1 - (t - 0.65f) / 0.35f : 1;
                f.T.color = c;
                var oc = f.O.effectColor;
                oc.a = 0.55f * c.a;
                f.O.effectColor = oc;
            }

            // toasts: keep up to 4, stack under the depth card
            while (toastQueue.Count > 0 && toasts.Count < 3) SpawnToast(toastQueue.Dequeue());
            float y = 164;
            for (int i = 0; i < toasts.Count; i++)
            {
                var t = toasts[i];
                t.Age += dt;
                float a = Mathf.Clamp01(t.Age / 0.25f) * Mathf.Clamp01((t.Life - t.Age) / 0.4f);
                t.G.alpha = a;
                t.Y = Mathf.Lerp(t.Y, y, 1 - Mathf.Exp(-dt * 12));
                t.Rt.anchoredPosition = new Vector2(20 - (1 - Mathf.Clamp01(t.Age / 0.25f)) * 30, t.Y);
                y += 74;
            }
            for (int i = toasts.Count - 1; i >= 0; i--)
                if (toasts[i].Age >= toasts[i].Life) { Object.Destroy(toasts[i].Rt.gameObject); toasts.RemoveAt(i); }

            // banner
            bannerAge += dt;
            if (bannerAge >= bannerLife && bannerQueue.Count > 0)
            {
                var b = bannerQueue.Dequeue();
                bannerKicker.text = b.kicker.ToUpperInvariant();
                bannerKicker.color = Color.Lerp(b.color, Color.white, 0.35f);
                bannerTitle.text = b.title;
                bannerSub.text = b.sub;
                bannerAge = 0;
                bannerLife = b.life;
            }
            float ba = bannerAge < bannerLife ? Mathf.Clamp01(bannerAge / 0.35f) * Mathf.Clamp01((bannerLife - bannerAge) / 0.6f) : 0;
            bannerGroup.alpha = ba;
            banner.localScale = Vector3.one * (bannerAge < 0.35f ? Mathf.Lerp(0.85f, 1f, bannerAge / 0.35f) : 1f);

            // wish bubbles follow the spot where the wish was caught
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                b.Age += dt;
                if (b.Age >= b.Life) { Object.Destroy(b.Rt.gameObject); bubbles.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(b.World);
                var p = ToCanvas(sp) + new Vector2(0, 50 + b.Age * 10 + i * 6);
                var half = root.rect.size * 0.5f;
                p.x = Mathf.Clamp(p.x, -half.x + 470, half.x - 800);
                p.y = Mathf.Clamp(p.y, -half.y + 180, half.y - 280);
                b.Rt.anchoredPosition = p;
                b.G.alpha = Mathf.Clamp01(b.Age / 0.2f) * Mathf.Clamp01((b.Life - b.Age) / 0.5f);
            }
        }
    }
}
