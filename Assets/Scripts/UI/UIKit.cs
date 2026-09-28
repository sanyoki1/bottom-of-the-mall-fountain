// uGUI helpers: procedural rounded/shadow sprites, OS fonts, and small builders for panels,
// labels, buttons, bars and scroll lists. The look is a light "glass" UI: translucent white
// cards, soft shadows, one teal accent, gold for money.
using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WishExtractor.UI
{
    public static class Pal
    {
        public static readonly Color Glass = new Color(1f, 1f, 1f, 0.86f);
        public static readonly Color GlassStrong = new Color(1f, 1f, 1f, 0.95f);
        public static readonly Color GlassDim = new Color(0.97f, 0.97f, 0.99f, 0.78f);
        public static readonly Color Ink = new Color32(0x1D, 0x1D, 0x1F, 0xFF);
        public static readonly Color Ink2 = new Color32(0x6E, 0x6E, 0x73, 0xFF);
        public static readonly Color Ink3 = new Color32(0xA1, 0xA1, 0xA6, 0xFF);
        public static readonly Color Hair = new Color(0, 0, 0, 0.08f);
        public static readonly Color Accent = new Color32(0x0F, 0xB5, 0xA8, 0xFF);
        public static readonly Color AccentDark = new Color32(0x0A, 0x8C, 0x82, 0xFF);
        public static readonly Color Pink = new Color32(0xFF, 0x5E, 0x87, 0xFF);
        public static readonly Color Gold = new Color32(0xE8, 0xA8, 0x00, 0xFF);
        public static readonly Color GoldInk = new Color32(0xA8, 0x74, 0x00, 0xFF);
        public static readonly Color Green = new Color32(0x28, 0xB4, 0x63, 0xFF);
        public static readonly Color Red = new Color32(0xFF, 0x45, 0x3A, 0xFF);
        public static readonly Color Purple = new Color32(0x8E, 0x5C, 0xF7, 0xFF);
        public static readonly Color Blue = new Color32(0x0A, 0x84, 0xFF, 0xFF);
        public static readonly Color Disabled = new Color32(0xD8, 0xD8, 0xDE, 0xFF);
        public static readonly Color[] Stage = { new Color32(0xE8, 0x8A, 0x2E, 0xFF), new Color32(0x2E, 0x9C, 0xE8, 0xFF), new Color32(0x8E, 0x5C, 0xF7, 0xFF), new Color32(0x28, 0xB4, 0x63, 0xFF) };
        public static readonly Color[] Rarity =
        {
            new Color32(0x8E, 0x8E, 0x93, 0xFF), new Color32(0x28, 0xB4, 0x63, 0xFF), new Color32(0x0A, 0x84, 0xFF, 0xFF),
            new Color32(0x8E, 0x5C, 0xF7, 0xFF), new Color32(0xE8, 0xA8, 0x00, 0xFF)
        };
        public static Color A(Color c, float a) { c.a = a; return c; }
    }

    public static class UIKit
    {
        static Font regular, semibold, bold, symbol;
        static Sprite rounded, shadow, circle, pill, softRing;

        public static Font Regular => regular ??= MakeFont(new[] { "Segoe UI", "Arial" });
        public static Font Semibold => semibold ??= MakeFont(new[] { "Segoe UI Semibold", "Segoe UI", "Arial" });
        public static Font Bold => bold ??= MakeFont(new[] { "Segoe UI Bold", "Segoe UI Black", "Segoe UI", "Arial" });
        public static Font Symbol => symbol ??= MakeFont(new[] { "Segoe UI Symbol", "Segoe UI", "Arial" });
        static Font mono;
        public static Font Mono => mono ??= MakeFont(new[] { "Consolas", "Courier New", "Lucida Console" });

        static Font MakeFont(string[] names)
        {
            try
            {
                var installed = Font.GetOSInstalledFontNames();
                foreach (var n in names)
                    if (Array.IndexOf(installed, n) >= 0) return Font.CreateDynamicFontFromOSFont(n, 32);
            }
            catch { }
            return Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        }

        // ── procedural sprites ─────────────────────────────────────────────

        /// <summary>Rounded rectangle, 9-sliced. Corner radius in the texture is 32 px.</summary>
        public static Sprite Rounded
        {
            get
            {
                if (rounded != null) return rounded;
                const int n = 96, r = 32;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        float a = Mathf.Clamp01(r - d + 0.5f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                rounded = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return rounded;
            }
        }

        public static Sprite Shadow
        {
            get
            {
                if (shadow != null) return shadow;
                const int n = 128, r = 48;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float cx = Mathf.Clamp(x + 0.5f, r, n - r), cy = Mathf.Clamp(y + 0.5f, r, n - r);
                        float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        float a = Mathf.Clamp01(1 - d / r);
                        a = a * a * (3 - 2 * a);
                        px[y * n + x] = new Color32(0, 0, 0, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                shadow = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100, 0, SpriteMeshType.FullRect, new Vector4(r, r, r, r));
                return shadow;
            }
        }

        public static Sprite Circle
        {
            get
            {
                if (circle != null) return circle;
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - n / 2f) * (x + 0.5f - n / 2f) + (y + 0.5f - n / 2f) * (y + 0.5f - n / 2f));
                        float a = Mathf.Clamp01(n / 2f - d);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                circle = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
                return circle;
            }
        }

        public static Sprite SoftRing
        {
            get
            {
                if (softRing != null) return softRing;
                const int n = 128;
                var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                var px = new Color32[n * n];
                for (int y = 0; y < n; y++)
                    for (int x = 0; x < n; x++)
                    {
                        float d = Mathf.Sqrt((x + 0.5f - n / 2f) * (x + 0.5f - n / 2f) + (y + 0.5f - n / 2f) * (y + 0.5f - n / 2f)) / (n / 2f);
                        float a = Mathf.Clamp01(1 - Mathf.Abs(d - 0.8f) / 0.2f);
                        px[y * n + x] = new Color32(255, 255, 255, (byte)(a * a * 255));
                    }
                t.SetPixels32(px);
                t.Apply();
                softRing = Sprite.Create(t, new Rect(0, 0, n, n), new Vector2(0.5f, 0.5f), 100);
                return softRing;
            }
        }

        // ── canvas ─────────────────────────────────────────────────────────

        public static Canvas CreateCanvas(string name, int order)
        {
            var go = new GameObject(name);
            var c = go.AddComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = order;
            c.pixelPerfect = false;
            var s = go.AddComponent<CanvasScaler>();
            s.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            s.referenceResolution = new Vector2(1920, 1080);
            s.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            s.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            return c;
        }

        public static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem");
            go.AddComponent<EventSystem>();
            go.AddComponent<StandaloneInputModule>();
        }

        // ── rect helpers ───────────────────────────────────────────────────

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Place a rect by anchor (0..1) with pixel offset and size (pivot = anchor).</summary>
        public static RectTransform Place(this RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = anchor;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return rt;
        }

        public static RectTransform Stretch(this RectTransform rt, float left = 0, float top = 0, float right = 0, float bottom = 0)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
            return rt;
        }

        public static RectTransform Pos(this RectTransform rt, float x, float y) { rt.anchoredPosition = new Vector2(x, y); return rt; }

        /// <summary>Top-left anchored rect with pixel position (y down) and size.</summary>
        public static RectTransform TL(this RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
            rt.pivot = new Vector2(0, 1);
            rt.anchoredPosition = new Vector2(x, -y);
            rt.sizeDelta = new Vector2(w, h);
            return rt;
        }

        /// <summary>Stretch horizontally inside the parent at a y offset from the top.</summary>
        public static RectTransform Row(this RectTransform rt, float y, float h, float padL = 0, float padR = 0)
        {
            rt.anchorMin = new Vector2(0, 1);
            rt.anchorMax = new Vector2(1, 1);
            rt.pivot = new Vector2(0.5f, 1);
            rt.anchoredPosition = new Vector2((padL - padR) / 2, -y);
            rt.sizeDelta = new Vector2(-(padL + padR), h);
            return rt;
        }

        // ── widgets ────────────────────────────────────────────────────────

        public static Image Image(Transform parent, string name, Sprite sprite, Color color, float radius = 0)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = sprite;
            img.color = color;
            img.raycastTarget = false;
            if (sprite == Rounded || sprite == Shadow)
            {
                img.type = UnityEngine.UI.Image.Type.Sliced;
                img.pixelsPerUnitMultiplier = sprite == Rounded ? Mathf.Max(0.01f, 32f / Mathf.Max(1, radius)) : Mathf.Max(0.01f, 48f / Mathf.Max(1, radius));
            }
            return img;
        }

        /// <summary>A glass card with a soft shadow. Returns the card's RectTransform.</summary>
        public static RectTransform Card(Transform parent, string name, Color? color = null, float radius = 22, bool shadowOn = true, bool blocks = true)
        {
            var holder = Rect(parent, name);
            if (shadowOn)
            {
                var sh = Image(holder, "Shadow", Shadow, new Color(0, 0, 0, 0.16f), radius + 18);
                sh.rectTransform.Stretch(-18, -10, -18, -26);
            }
            var bg = Image(holder, "Bg", Rounded, color ?? Pal.Glass, radius);
            bg.rectTransform.Stretch();
            bg.raycastTarget = blocks;
            if (shadowOn)
            {
                var hl = Image(holder, "Rim", Rounded, new Color(1, 1, 1, 0.55f), radius);
                hl.rectTransform.Stretch(0, 0, 0, 0);
                hl.fillCenter = false;
            }
            return holder;
        }

        public static Text Label(Transform parent, string name, string text, int size, Color color, TextAnchor anchor = TextAnchor.MiddleLeft, Font font = null, bool wrap = false)
        {
            var rt = Rect(parent, name);
            var t = rt.gameObject.AddComponent<Text>();
            t.font = font ?? Regular;
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = anchor;
            t.horizontalOverflow = wrap ? HorizontalWrapMode.Wrap : HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            t.supportRichText = true;
            t.lineSpacing = 1.05f;
            return t;
        }

        public sealed class Btn
        {
            public Button Button;
            public Image Bg;
            public Text Label;
            public RectTransform Rt;
            public Color Normal, Hover, Off;
            public bool Interactable
            {
                get => Button.interactable;
                set { Button.interactable = value; Bg.color = value ? Normal : Off; }
            }
            public void SetColors(Color normal, Color off) { Normal = normal; Off = off; Bg.color = Button.interactable ? normal : off; }
        }

        public static Btn Button(Transform parent, string name, string text, Color bg, Color fg, int size, Action onClick, float radius = 999, Font font = null)
        {
            var rt = Rect(parent, name);
            var img = rt.gameObject.AddComponent<Image>();
            img.sprite = Rounded;
            img.type = UnityEngine.UI.Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = radius >= 999 ? 1.4f : Mathf.Max(0.01f, 32f / radius);
            img.color = bg;
            var b = rt.gameObject.AddComponent<Button>();
            b.targetGraphic = img;
            var cb = b.colors;
            cb.normalColor = Color.white;
            cb.highlightedColor = new Color(0.94f, 0.94f, 0.94f);
            cb.pressedColor = new Color(0.82f, 0.82f, 0.82f);
            cb.selectedColor = Color.white;
            cb.disabledColor = Color.white;
            cb.fadeDuration = 0.08f;
            b.colors = cb;
            b.navigation = new Navigation { mode = Navigation.Mode.None };
            var label = Label(rt, "Label", text, size, fg, TextAnchor.MiddleCenter, font ?? Semibold);
            label.rectTransform.Stretch(6, 0, 6, 0);
            if (onClick != null) b.onClick.AddListener(() => onClick());
            return new Btn { Button = b, Bg = img, Label = label, Rt = rt, Normal = bg, Off = Pal.Disabled };
        }

        public sealed class Bar
        {
            public RectTransform Rt, FillRt;
            public Image Back, Fill;
            float value = -1;
            public void Set(float v)
            {
                v = Mathf.Clamp01(v);
                if (Mathf.Abs(v - value) < 0.0005f) return;
                value = v;
                FillRt.anchorMax = new Vector2(Mathf.Max(0.001f, v), 1);
                Fill.enabled = v > 0.001f;
            }
        }

        public static Bar ProgressBar(Transform parent, string name, Color back, Color fill, float radius = 999)
        {
            var rt = Rect(parent, name);
            var bg = rt.gameObject.AddComponent<Image>();
            bg.sprite = Rounded;
            bg.type = UnityEngine.UI.Image.Type.Sliced;
            bg.pixelsPerUnitMultiplier = radius >= 999 ? 3f : Mathf.Max(0.01f, 32f / radius);
            bg.color = back;
            bg.raycastTarget = false;
            var fr = Rect(rt, "Fill");
            fr.anchorMin = Vector2.zero;
            fr.anchorMax = new Vector2(0, 1);
            fr.offsetMin = fr.offsetMax = Vector2.zero;
            var f = fr.gameObject.AddComponent<Image>();
            f.sprite = Rounded;
            f.type = UnityEngine.UI.Image.Type.Sliced;
            f.pixelsPerUnitMultiplier = bg.pixelsPerUnitMultiplier;
            f.color = fill;
            f.raycastTarget = false;
            var bar = new Bar { Rt = rt, Back = bg, Fill = f, FillRt = fr };
            bar.Set(0);
            return bar;
        }

        public static Image Dot(Transform parent, string name, Color color, float size)
        {
            var img = Image(parent, name, Circle, color);
            img.rectTransform.sizeDelta = new Vector2(size, size);
            return img;
        }

        /// <summary>Vertical scroll list. Returns the content rect (children laid out top to bottom).</summary>
        public static RectTransform ScrollList(Transform parent, string name, out ScrollRect scroll, float spacing = 10, int padding = 6)
        {
            var view = Rect(parent, name);
            var mask = view.gameObject.AddComponent<RectMask2D>();
            mask.padding = Vector4.zero;
            var hit = view.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            scroll = view.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 38;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            var content = Rect(view, "Content");
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.anchoredPosition = Vector2.zero;
            content.sizeDelta = Vector2.zero;
            var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = spacing;
            vlg.padding = new RectOffset(padding, padding, padding, padding + 8);
            vlg.childControlHeight = true;
            vlg.childControlWidth = true;
            vlg.childForceExpandHeight = false;
            vlg.childForceExpandWidth = true;
            var fit = content.gameObject.AddComponent<ContentSizeFitter>();
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = content;
            scroll.viewport = view;
            return content;
        }

        public static LayoutElement Layout(GameObject go, float prefHeight, float minHeight = -1)
        {
            var le = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            le.preferredHeight = prefHeight;
            if (minHeight >= 0) le.minHeight = minHeight;
            return le;
        }

        public static void SetAlpha(Graphic g, float a) { var c = g.color; c.a = a; g.color = c; }

        public static string Hex(Color c) => ColorUtility.ToHtmlStringRGB(c);
    }
}
