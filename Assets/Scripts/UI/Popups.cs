// Transient UI, kept to one thing at a time where it can be: world-anchored floating numbers (quick
// pickups share one running total), a single toast at a time (a dark pill, one line, a second only
// when it matters), a centre banner for big moments, the quote card when you catch a True Wish, and
// speech bubbles over nearby heads (three at most).
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
        Floater tally;

        // toasts: one showing, a few waiting
        sealed class ToastCard { public RectTransform Rt; public CanvasGroup G; public float Age, Life; }
        ToastCard toast;
        readonly Queue<(string title, string detail, Color color, string glyph, float life)> toastQueue = new Queue<(string, string, Color, string, float)>();
        const int ToastsWaiting = 3;

        // banner
        RectTransform banner;
        CanvasGroup bannerGroup;
        Text bannerKicker, bannerTitle, bannerSub;
        float bannerAge = 99, bannerLife;
        readonly Queue<(string kicker, string title, string sub, Color color, float life)> bannerQueue = new Queue<(string, string, string, Color, float)>();

        // wish quote cards
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

        public void Float(Vector3 world, string text, Color color, float scale) => Spawn(world, text, color, scale);

        Floater Spawn(Vector3 world, string text, Color color, float scale)
        {
            if (floaters.Count > 40) return null;
            var f = floaterPool.Count > 0 ? floaterPool.Pop() : NewFloater();
            f.Rt.gameObject.SetActive(true);
            f.World = world + Random.insideUnitSphere * 0.2f;
            f.T.text = text;
            f.Col = color;
            f.T.color = color;
            f.T.fontSize = Mathf.RoundToInt(22 * scale);
            f.Age = 0;
            f.Life = 1.15f;
            f.Scale = scale;
            floaters.Add(f);
            return f;
        }

        /// <summary>A running total for quick pickups: a fresh one floats up, the ones after it update its text.</summary>
        public void Tally(Vector3 world, string text, Color color, bool fresh)
        {
            if (!fresh && tally != null && floaters.Contains(tally) && tally.Age < 0.8f)
            {
                tally.T.text = text;
                tally.Age = Mathf.Min(tally.Age, 0.25f);
                return;
            }
            tally = Spawn(world, text, color, 0.8f);
        }

        Floater NewFloater()
        {
            var t = UIKit.Label(root, "Float", "", 22, Color.white, TextAnchor.MiddleCenter, UIKit.Bold);
            t.rectTransform.sizeDelta = new Vector2(400, 60);
            var o = t.gameObject.AddComponent<Outline>();
            o.effectColor = new Color(0, 0, 0, 0.55f);
            o.effectDistance = new Vector2(1.4f, -1.4f);
            return new Floater { Rt = t.rectTransform, T = t, O = o };
        }

        // ── toasts ────────────────────────────────────────────────────────

        /// <summary>A short note at the right edge. Keep the title to a few words; the detail (optional) to one line.</summary>
        public void Toast(string title, string detail, Color color, string glyph = "★", float life = 3.2f)
        {
            // never a backlog: if the notes pile up, the oldest waiting one goes
            while (toastQueue.Count >= ToastsWaiting) toastQueue.Dequeue();
            toastQueue.Enqueue((title, detail, color, glyph, life));
        }

        void SpawnToast((string title, string detail, Color color, string glyph, float life) d)
        {
            const float padL = 46, padR = 18, maxW = 440;
            var rt = UIKit.Rect(root, "Toast");
            rt.anchorMin = rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(1, 1);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            g.alpha = 0;
            var bg = UIKit.Image(rt, "Bg", UIKit.Rounded, new Color(0.07f, 0.08f, 0.1f, 0.66f), 18);
            bg.rectTransform.Stretch();
            var dot = UIKit.Dot(rt, "Icon", d.color, 24);
            var gl = UIKit.Label(dot.rectTransform, "G", d.glyph, 14, Color.white, TextAnchor.MiddleCenter, UIKit.Symbol);
            gl.rectTransform.Stretch();
            var t = UIKit.Label(rt, "Title", d.title, 16, Color.white, TextAnchor.MiddleLeft, UIKit.Semibold);
            float w = Mathf.Min(maxW, t.preferredWidth), h = 42;
            if (!string.IsNullOrEmpty(d.detail))
            {
                var s = UIKit.Label(rt, "Detail", d.detail, 13, new Color(1, 1, 1, 0.7f), TextAnchor.UpperLeft, UIKit.Regular, true);
                w = Mathf.Max(w, Mathf.Min(maxW, s.preferredWidth));
                s.rectTransform.TL(padL, 32, w, 20);
                h = 32 + s.preferredHeight + 10;
                s.rectTransform.sizeDelta = new Vector2(w, h - 38);
            }
            t.rectTransform.TL(padL, 10, w, 22);
            dot.rectTransform.TL(13, 9, 24, 24);
            rt.sizeDelta = new Vector2(padL + w + padR, h);
            toast = new ToastCard { Rt = rt, G = g, Life = d.life };
        }

        // ── banner ────────────────────────────────────────────────────────

        void BuildBanner()
        {
            banner = UIKit.Rect(root, "Banner").Place(new Vector2(0.5f, 0.5f), new Vector2(0, 250), new Vector2(1000, 132));
            bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            bannerGroup.blocksRaycasts = false;
            bannerGroup.alpha = 0;
            bannerKicker = UIKit.Label(banner, "Kicker", "", 15, Color.white, TextAnchor.MiddleCenter, UIKit.Semibold);
            bannerKicker.rectTransform.TL(0, 0, 1000, 22);
            bannerTitle = UIKit.Label(banner, "Title", "", 42, Color.white, TextAnchor.MiddleCenter, UIKit.Bold);
            bannerTitle.rectTransform.TL(0, 22, 1000, 56);
            bannerSub = UIKit.Label(banner, "Sub", "", 17, new Color(1, 1, 1, 0.85f), TextAnchor.UpperCenter, UIKit.Regular, true);
            bannerSub.rectTransform.TL(170, 80, 660, 50);
            foreach (var t in new[] { bannerKicker, bannerTitle, bannerSub })
            {
                var sh = t.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.55f);
                sh.effectDistance = new Vector2(1.5f, -2.5f);
                var o = t.gameObject.AddComponent<Outline>();
                o.effectColor = new Color(0, 0, 0, 0.2f);
            }
        }

        public void Banner(string kicker, string title, string sub, Color color, float life = 3.6f)
        {
            bannerQueue.Enqueue((kicker, title, sub, color, Mathf.Min(life, 4.2f)));
        }

        /// <summary>Drop any banner that is showing or queued (e.g. when a new mall starts).</summary>
        public void ClearBanners()
        {
            bannerQueue.Clear();
            bannerAge = bannerLife = 99;
            bannerGroup.alpha = 0;
        }

        // ── wish quote cards ──────────────────────────────────────────────

        public void WishQuote(Vector3 world, WishDef w, double value, bool first)
        {
            while (bubbles.Count >= 2) { Object.Destroy(bubbles[0].Rt.gameObject); bubbles.RemoveAt(0); }
            const float W = 360;
            var rt = UIKit.Card(root, "Wish", new Color(1, 1, 1, 0.94f), 18, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            var col = Pal.Rarity[(int)w.Rarity];
            var kick = UIKit.Label(rt, "Kicker", (first ? "NEW  ·  " : "") + View.RarityColors.Names[(int)w.Rarity].ToUpperInvariant() + " WISH", 11, col, TextAnchor.UpperLeft, UIKit.Bold);
            kick.rectTransform.TL(16, 11, 220, 16);
            var val = UIKit.Label(rt, "Value", "+" + Fmt.Money(value), 13, Pal.Green, TextAnchor.UpperRight, UIKit.Bold);
            val.rectTransform.TL(W - 176, 9, 160, 18);
            var q = UIKit.Label(rt, "Quote", "“" + w.Text + "”", 15, Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold, true);
            q.fontStyle = FontStyle.Italic;
            q.rectTransform.TL(16, 30, W - 32, 20);
            float h = 30 + q.preferredHeight + 14;
            q.rectTransform.sizeDelta = new Vector2(W - 32, h - 36);
            rt.sizeDelta = new Vector2(W, h);
            bubbles.Add(new Bubble { Rt = rt, G = g, World = world, Life = 3.6f });
        }

        // ── speech bubbles over nearby heads ─────────────────────────────────

        sealed class Speech { public RectTransform Rt; public CanvasGroup G; public Transform Anchor; public Vector3 Last; public float Age, Life; }
        readonly List<Speech> speech = new List<Speech>();
        const int MaxSpeech = 3;
        const float SpeechRange = 18f;

        public void Say(Transform anchor, string text, bool wish, Rarity rarity)
        {
            if (anchor == null) return;
            if ((anchor.position - cam.transform.position).sqrMagnitude > SpeechRange * SpeechRange) return;
            for (int i = speech.Count - 1; i >= 0; i--)
                if (speech[i].Anchor == anchor) { Object.Destroy(speech[i].Rt.gameObject); speech.RemoveAt(i); }
            while (speech.Count >= MaxSpeech) { Object.Destroy(speech[0].Rt.gameObject); speech.RemoveAt(0); }
            var col = wish ? Pal.Rarity[(int)rarity] : Pal.Ink;
            var rt = UIKit.Card(root, "Speech", new Color(1, 1, 1, 0.92f), 14, false, false);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0);
            var g = rt.gameObject.AddComponent<CanvasGroup>();
            g.blocksRaycasts = false;
            var t = UIKit.Label(rt, "Text", text, wish ? 15 : 14, wish ? Pal.Ink : Pal.Ink2, TextAnchor.MiddleCenter, wish ? UIKit.Semibold : UIKit.Regular, true);
            if (wish) t.fontStyle = FontStyle.Italic;
            float w = Mathf.Clamp(t.preferredWidth + 26, 100, 320);
            t.rectTransform.TL(13, wish ? 20 : 7, w - 26, 10);
            t.rectTransform.sizeDelta = new Vector2(w - 26, 0);
            float h = t.preferredHeight + (wish ? 30 : 15);
            t.rectTransform.sizeDelta = new Vector2(w - 26, h - (wish ? 27 : 13));
            rt.sizeDelta = new Vector2(w, h);
            if (wish)
            {
                var k = UIKit.Label(rt, "Kicker", "✦ " + View.RarityColors.Names[(int)rarity].ToUpperInvariant() + " WISH", 10, col, TextAnchor.UpperCenter, UIKit.Bold);
                k.rectTransform.TL(0, 5, w, 13);
            }
            // a little tail
            var tail = UIKit.Image(rt, "Tail", UIKit.Rounded, new Color(1, 1, 1, 0.92f), 4);
            tail.rectTransform.anchorMin = tail.rectTransform.anchorMax = new Vector2(0.5f, 0);
            tail.rectTransform.sizeDelta = new Vector2(12, 12);
            tail.rectTransform.anchoredPosition = new Vector2(0, -2);
            tail.rectTransform.localRotation = Quaternion.Euler(0, 0, 45);
            tail.transform.SetAsFirstSibling();
            speech.Add(new Speech { Rt = rt, G = g, Anchor = anchor, Last = anchor.position, Life = wish ? 4f : 2.8f });
        }

        void UpdateSpeech(float dt)
        {
            for (int i = speech.Count - 1; i >= 0; i--)
            {
                var s = speech[i];
                s.Age += dt;
                if (s.Anchor != null) s.Last = s.Anchor.position;
                if (s.Age >= s.Life) { Object.Destroy(s.Rt.gameObject); speech.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(s.Last + Vector3.up * 0.45f);
                if (sp.z < 0.5f) { s.G.alpha = 0; continue; }
                s.Rt.anchoredPosition = ToCanvas(sp);
                float dist = sp.z;
                s.Rt.localScale = Vector3.one * Mathf.Clamp(6f / dist, 0.55f, 1f);
                s.G.alpha = Mathf.Clamp01(s.Age / 0.15f) * Mathf.Clamp01((s.Life - s.Age) / 0.4f);
            }
        }

        // ── update ────────────────────────────────────────────────────────

        public void Update(float dt)
        {
            UpdateSpeech(dt);
            for (int i = floaters.Count - 1; i >= 0; i--)
            {
                var f = floaters[i];
                f.Age += dt;
                float t = f.Age / f.Life;
                if (t >= 1) { f.Rt.gameObject.SetActive(false); floaters.RemoveAt(i); floaterPool.Push(f); continue; }
                Vector3 sp = cam.WorldToScreenPoint(f.World + Vector3.up * t * 1.6f);
                if (sp.z < 0) { f.Rt.gameObject.SetActive(false); continue; }
                f.Rt.gameObject.SetActive(true);
                f.Rt.anchoredPosition = ToCanvas(sp);
                float pop = t < 0.15f ? Mathf.Lerp(0.6f, 1.12f, t / 0.15f) : Mathf.Lerp(1.12f, 1f, Mathf.Clamp01((t - 0.15f) / 0.2f));
                f.Rt.localScale = Vector3.one * pop;
                var c = f.Col;
                c.a = t > 0.65f ? 1 - (t - 0.65f) / 0.35f : 1;
                f.T.color = c;
                var oc = f.O.effectColor;
                oc.a = 0.55f * c.a;
                f.O.effectColor = oc;
            }

            // one toast at a time, at the right edge under the depth readout; it hurries along when others wait
            if (toast == null && toastQueue.Count > 0) SpawnToast(toastQueue.Dequeue());
            if (toast != null)
            {
                toast.Age += dt * (toastQueue.Count > 0 ? 1.6f : 1f);
                float a = Mathf.Clamp01(toast.Age / 0.25f) * Mathf.Clamp01((toast.Life - toast.Age) / 0.35f);
                toast.G.alpha = a;
                toast.Rt.anchoredPosition = new Vector2(-28 + (1 - Mathf.Clamp01(toast.Age / 0.25f)) * 40, -136);
                if (toast.Age >= toast.Life) { Object.Destroy(toast.Rt.gameObject); toast = null; }
            }

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
            banner.localScale = Vector3.one * (bannerAge < 0.35f ? Mathf.Lerp(0.9f, 1f, bannerAge / 0.35f) : 1f);

            // wish cards follow the spot where the wish was caught
            for (int i = bubbles.Count - 1; i >= 0; i--)
            {
                var b = bubbles[i];
                b.Age += dt;
                if (b.Age >= b.Life) { Object.Destroy(b.Rt.gameObject); bubbles.RemoveAt(i); continue; }
                Vector3 sp = cam.WorldToScreenPoint(b.World);
                var p = ToCanvas(sp) + new Vector2(0, 50 + b.Age * 10 + i * 6);
                var half = root.rect.size * 0.5f;
                p.x = Mathf.Clamp(p.x, -half.x + 200, half.x - 200);
                p.y = Mathf.Clamp(p.y, -half.y + 180, half.y - 200);
                b.Rt.anchoredPosition = p;
                b.G.alpha = Mathf.Clamp01(b.Age / 0.2f) * Mathf.Clamp01((b.Life - b.Age) / 0.5f);
            }
        }
    }
}
