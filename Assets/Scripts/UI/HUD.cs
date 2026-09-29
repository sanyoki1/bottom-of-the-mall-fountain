// First-person HUD, kept quiet. Everything sits straight on the picture (no cards; a soft shade along the
// top and bottom edges keeps it legible), and most of it only shows while it matters:
// - top-left: cash, with a brief "+$" when it grows (tokens and pennies once you have any);
// - top-centre: the current goal in one line (its hint fades after a few seconds; a tick when it's done);
// - top-right: depth, while digging or when it changes, and the mall event while it runs;
// - bottom-left: how full your hands are (the container's name only when it changes);
// - bottom-centre: the hotbar, once you have more than one tool (it dims when idle);
// - centre: crosshair, a short prompt, a one-line message, and Officer Doug's eye while he looks.
using UnityEngine;
using UnityEngine.UI;
using WishExtractor.Core;
using WishExtractor.View;

namespace WishExtractor.UI
{
    public sealed class HUD
    {
        static readonly Color Soft = new Color(1, 1, 1, 0.72f);
        static readonly Color Money = new Color32(0xFF, 0xD8, 0x6B, 0xFF);
        static readonly Color Good = new Color32(0x8E, 0xF0, 0xA8, 0xFF);
        static readonly Color Bad = new Color32(0xFF, 0x8F, 0xA8, 0xFF);
        static readonly Color GoalInk = new Color32(0x7C, 0xE8, 0xDC, 0xFF);
        static readonly Color Amber = new Color32(0xFF, 0xB8, 0x3A, 0xFF);
        static string Hex(Color c) => UIKit.Hex(c);

        Sim sim;
        RectTransform root;
        public RectTransform Root => root;
        public int Slot = 1;
        /// <summary>The camera, so the HUD can point at Officer Doug when he's out of view.</summary>
        public Transform CameraT;

        // crosshair + prompt + message
        Image dot, ring;
        Text prompt, message, keysHint;
        float ringScale = 1, messageAge = 99;

        // wallet
        Text cashText, tokenText, deltaText, powerText;
        double shownCash, lastCash, delta;
        float deltaAge = 99;

        // goal
        Text goalText, goalHint;
        int shownGoal = -1;
        string doneText;
        float goalAge, doneAge = 99;

        // depth + event
        RectTransform depthRt;
        CanvasGroup depthGroup;
        Text depthText, stratumText, eventText;
        UIKit.Bar depthBar;
        double lastDug = -1;
        float depthSeen = -99, depthAlpha;

        // carry
        Text carryName, carryCount;
        UIKit.Bar carryBar;
        int shownTier = -1;
        float tierAge = 99;

        // hotbar
        RectTransform hotbar;
        CanvasGroup hotbarGroup;
        readonly Image[] slotBg = new Image[3];
        readonly Text[] slotKey = new Text[3], slotName = new Text[3];
        readonly bool[] has = new bool[3];
        int lastSlot = 1;
        float slotAge = 99;

        // receipt
        RectTransform receipt;
        CanvasGroup receiptGroup;
        Text receiptTotal, receiptJoke;
        float receiptAge = 99, lastReceipt = -99;

        // Officer Doug's statue check
        RectTransform eyeRoot, eyeArrow;
        CanvasGroup eyeGroup;
        Image eyeWhite, iris, eyeRing, vignette;
        Text eyeLabel;
        float eyeAlpha, eyeOpen = 0.1f, vigAlpha;

        public void Build(Canvas canvas, Sim s)
        {
            sim = s;
            root = UIKit.Rect(canvas.transform, "HUD").Stretch();
            BuildEdges();
            BuildEye();
            BuildCrosshair();
            BuildWallet();
            BuildGoal();
            BuildDepth();
            BuildCarry();
            BuildHotbar();
            BuildReceipt();
            message = Label("Message", "", 20, Color.white, TextAnchor.MiddleCenter, UIKit.Semibold);
            message.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -132), new Vector2(1100, 34));
            keysHint = Label("Keys", "", 14, Soft, TextAnchor.LowerRight, UIKit.Semibold);
            keysHint.rectTransform.Place(new Vector2(1, 0), new Vector2(-28, 24), new Vector2(760, 22));
            shownCash = lastCash = sim.S.cash;
        }

        Text Label(string name, string text, int size, Color color, TextAnchor anchor, Font font, bool wrap = false, RectTransform parent = null)
        {
            var t = UIKit.Label(parent ?? root, name, text, size, color, anchor, font, wrap);
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.55f);
            sh.effectDistance = new Vector2(1.2f, -1.6f);
            return t;
        }

        /// <summary>A soft dark shade along the top and bottom edges, so white text reads over a bright mall.</summary>
        void BuildEdges()
        {
            var top = UIKit.Image(root, "ShadeTop", UIKit.Gradient, new Color(0, 0, 0, 0.26f));
            top.rectTransform.anchorMin = new Vector2(0, 1);
            top.rectTransform.anchorMax = new Vector2(1, 1);
            top.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            top.rectTransform.sizeDelta = new Vector2(0, 150);
            top.rectTransform.anchoredPosition = new Vector2(0, -75);
            top.rectTransform.localScale = new Vector3(1, -1, 1);   // flipped in place: the dark edge at the top
            var bottom = UIKit.Image(root, "ShadeBottom", UIKit.Gradient, new Color(0, 0, 0, 0.3f));
            bottom.rectTransform.anchorMin = new Vector2(0, 0);
            bottom.rectTransform.anchorMax = new Vector2(1, 0);
            bottom.rectTransform.pivot = new Vector2(0.5f, 0);
            bottom.rectTransform.sizeDelta = new Vector2(0, 170);
            bottom.rectTransform.anchoredPosition = Vector2.zero;
        }

        void BuildCrosshair()
        {
            ring = UIKit.Image(root, "Ring", UIKit.SoftRing, new Color(1, 1, 1, 0.85f));
            ring.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30, 30));
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dot = UIKit.Image(root, "Dot", UIKit.Circle, new Color(1, 1, 1, 0.9f));
            dot.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(5, 5));
            dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var dsh = dot.gameObject.AddComponent<Shadow>();
            dsh.effectColor = new Color(0, 0, 0, 0.5f);
            prompt = Label("Prompt", "", 18, Color.white, TextAnchor.UpperCenter, UIKit.Semibold);
            prompt.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(900, 30));
            prompt.rectTransform.pivot = new Vector2(0.5f, 1);
        }

        void BuildWallet()
        {
            cashText = Label("Cash", "$0.00", 34, Color.white, TextAnchor.UpperLeft, UIKit.Bold);
            cashText.rectTransform.TL(28, 18, 420, 44);
            deltaText = Label("Delta", "", 18, Good, TextAnchor.UpperLeft, UIKit.Bold);
            deltaText.rectTransform.TL(200, 32, 260, 24);
            tokenText = Label("Tokens", "", 16, new Color(0.84f, 0.78f, 1f), TextAnchor.UpperLeft, UIKit.Semibold);
            tokenText.rectTransform.TL(30, 62, 420, 22);
            powerText = Label("Power", "", 15, Soft, TextAnchor.UpperLeft, UIKit.Semibold);
            powerText.rectTransform.TL(30, 86, 520, 22);
        }

        void BuildGoal()
        {
            goalText = Label("Goal", "", 20, Color.white, TextAnchor.UpperCenter, UIKit.Semibold);
            goalText.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -22), new Vector2(1000, 28));
            goalHint = Label("GoalHint", "", 15, Soft, TextAnchor.UpperCenter, UIKit.Regular, true);
            goalHint.rectTransform.Place(new Vector2(0.5f, 1), new Vector2(0, -52), new Vector2(760, 44));
        }

        void BuildDepth()
        {
            depthRt = UIKit.Rect(root, "Depth").Place(new Vector2(1, 1), new Vector2(-28, -20), new Vector2(360, 64));
            depthGroup = depthRt.gameObject.AddComponent<CanvasGroup>();
            depthGroup.blocksRaycasts = false;
            depthGroup.alpha = 0;
            depthText = Label("Depth", "", 22, Color.white, TextAnchor.UpperRight, UIKit.Bold, false, depthRt);
            depthText.rectTransform.TL(0, 0, 360, 28);
            stratumText = Label("Stratum", "", 14, Soft, TextAnchor.UpperRight, UIKit.Semibold, false, depthRt);
            stratumText.rectTransform.TL(0, 30, 360, 18);
            depthBar = UIKit.ProgressBar(depthRt, "Bar", new Color(1, 1, 1, 0.2f), Money);
            depthBar.Rt.TL(200, 54, 160, 4);
            eventText = Label("Event", "", 15, new Color32(0xFF, 0x9E, 0xC4, 0xFF), TextAnchor.UpperRight, UIKit.Bold);
            eventText.rectTransform.Place(new Vector2(1, 1), new Vector2(-28, -92), new Vector2(460, 22));
        }

        void BuildCarry()
        {
            carryName = Label("CarryName", "", 13, Soft, TextAnchor.LowerLeft, UIKit.Semibold);
            carryName.rectTransform.Place(new Vector2(0, 0), new Vector2(30, 78), new Vector2(360, 18));
            carryCount = Label("CarryCount", "", 26, Color.white, TextAnchor.LowerLeft, UIKit.Bold);
            carryCount.rectTransform.Place(new Vector2(0, 0), new Vector2(28, 40), new Vector2(360, 34));
            carryBar = UIKit.ProgressBar(root, "CarryBar", new Color(1, 1, 1, 0.22f), Pal.Accent);
            carryBar.Rt.Place(new Vector2(0, 0), new Vector2(30, 28), new Vector2(180, 5));
        }

        const float SlotW = 112, SlotH = 32, SlotGap = 8;

        void BuildHotbar()
        {
            hotbar = UIKit.Rect(root, "Hotbar").Place(new Vector2(0.5f, 0), new Vector2(0, 26), new Vector2(3 * SlotW + 2 * SlotGap, SlotH));
            hotbarGroup = hotbar.gameObject.AddComponent<CanvasGroup>();
            hotbarGroup.blocksRaycasts = false;
            for (int i = 0; i < 3; i++)
            {
                var bg = UIKit.Image(hotbar, "Slot" + (i + 1), UIKit.Rounded, new Color(0, 0, 0, 0.32f), SlotH / 2);
                bg.rectTransform.TL(i * (SlotW + SlotGap), 0, SlotW, SlotH);
                slotBg[i] = bg;
                slotKey[i] = UIKit.Label(bg.rectTransform, "Key", (i + 1).ToString(), 13, Soft, TextAnchor.MiddleLeft, UIKit.Bold);
                slotKey[i].rectTransform.TL(14, 0, 16, SlotH);
                slotName[i] = UIKit.Label(bg.rectTransform, "Name", "", 14, Color.white, TextAnchor.MiddleLeft, UIKit.Semibold);
                slotName[i].rectTransform.TL(32, 0, SlotW - 40, SlotH);
            }
        }

        void BuildReceipt()
        {
            receipt = UIKit.Card(root, "Receipt", new Color(0.99f, 0.98f, 0.95f, 0.94f), 6, false).Place(new Vector2(1, 0.5f), new Vector2(-28, 40), new Vector2(236, 118));
            receiptGroup = receipt.gameObject.AddComponent<CanvasGroup>();
            receiptGroup.blocksRaycasts = false;
            receiptGroup.alpha = 0;
            var mono = UIKit.Mono;
            var head = UIKit.Label(receipt, "Head", "COIN-O-MATIC 3000", 12, Pal.Ink2, TextAnchor.UpperCenter, mono);
            head.rectTransform.TL(0, 10, 236, 16);
            receiptTotal = UIKit.Label(receipt, "Total", "", 16, Pal.Ink, TextAnchor.UpperCenter, mono);
            receiptTotal.rectTransform.TL(0, 30, 236, 22);
            receiptJoke = UIKit.Label(receipt, "Joke", "", 12, Pal.Ink2, TextAnchor.UpperCenter, mono, true);
            receiptJoke.rectTransform.TL(14, 58, 208, 54);
            receiptJoke.fontStyle = FontStyle.Italic;
        }

        void BuildEye()
        {
            vignette = UIKit.Image(root, "Vignette", UIKit.Vignette, new Color(1f, 0.62f, 0.15f, 0));
            vignette.rectTransform.Stretch();
            eyeRoot = UIKit.Rect(root, "Doug Eye").Place(new Vector2(0.5f, 0.5f), new Vector2(0, 104), new Vector2(120, 120));
            eyeRoot.pivot = new Vector2(0.5f, 0.5f);
            eyeGroup = eyeRoot.gameObject.AddComponent<CanvasGroup>();
            eyeGroup.blocksRaycasts = false;
            eyeGroup.alpha = 0;
            eyeRing = UIKit.Image(eyeRoot, "Ring", UIKit.SoftRing, Amber);
            eyeRing.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(98, 98));
            eyeRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            eyeRing.type = Image.Type.Filled;
            eyeRing.fillMethod = Image.FillMethod.Radial360;
            eyeRing.fillOrigin = (int)Image.Origin360.Top;
            eyeRing.fillClockwise = false;
            eyeWhite = UIKit.Image(eyeRoot, "Eye", UIKit.EyeLens, new Color(1, 1, 1, 0.95f));
            eyeWhite.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64, 36));
            eyeWhite.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            iris = UIKit.Image(eyeWhite.rectTransform, "Iris", UIKit.Circle, new Color(0.12f, 0.12f, 0.14f));
            iris.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22, 22));
            iris.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var arrow = UIKit.Image(eyeRoot, "Pointer", UIKit.Triangle, Amber);
            eyeArrow = arrow.rectTransform;
            eyeArrow.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(16, 14));
            eyeArrow.pivot = new Vector2(0.5f, 0.5f);
            eyeLabel = Label("Freeze", "", 18, Amber, TextAnchor.UpperCenter, UIKit.Bold, false, eyeRoot);
            eyeLabel.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -56), new Vector2(300, 24));
            eyeLabel.rectTransform.pivot = new Vector2(0.5f, 1);
        }

        // ───────────────────────────── events ─────────────────────────────

        /// <summary>A receipt slip for a deposit (at most one every half minute; the kiosk's "+$" floats up every time).</summary>
        public void ShowReceipt(double cash, int items, string joke)
        {
            if (Time.time - lastReceipt < 30 && sim.S.deposits > 1) return;
            lastReceipt = Time.time;
            receiptTotal.text = $"<b>TOTAL {Fmt.Money(cash)}</b>";
            receiptJoke.text = joke;
            float h = 58 + Mathf.Min(54, receiptJoke.preferredHeight) + 12;
            receipt.sizeDelta = new Vector2(236, h);
            receiptAge = 0;
        }

        public void ShowMessage(string text)
        {
            message.text = text;
            messageAge = 0;
        }

        // ───────────────────────────── per-frame ─────────────────────────────

        public void Refresh(float dt, Target target, bool wading, bool frozen, BuildMode build, bool digMode)
        {
            RefreshWallet(dt, build);
            RefreshGoal(dt);
            RefreshDepth(dt, digMode);
            RefreshCarry(dt);
            RefreshHotbar(dt, build, digMode);
            RefreshEye(dt, frozen);

            // crosshair + prompt
            bool on = target.Kind != TargetKind.None && !frozen;
            ringScale = Mathf.Lerp(ringScale, on ? 1f : 0.55f, 1 - Mathf.Exp(-dt * 14));
            ring.rectTransform.localScale = Vector3.one * ringScale;
            ring.color = on ? Pal.A(Color.Lerp(Pal.Accent, Color.white, 0.35f), 0.95f) : new Color(1, 1, 1, 0.35f);
            dot.enabled = !frozen;
            ring.enabled = !frozen;
            prompt.text = frozen ? "" : build.Active ? BuildPrompt(build) : digMode && target.Kind == TargetKind.None ? $"<color=#{Hex(Soft)}>Aim at the crust</color>" : PromptFor(target);

            // receipt + message
            receiptAge += dt;
            receiptGroup.alpha = receiptAge < 4f ? Mathf.Clamp01(receiptAge / 0.2f) * Mathf.Clamp01((4f - receiptAge) / 0.6f) : 0;
            receipt.anchoredPosition = new Vector2(-28 + (1 - Mathf.Clamp01(receiptAge / 0.25f)) * 40, 40);
            messageAge += dt;
            message.color = new Color(1, 1, 1, messageAge < 2.6f ? Mathf.Clamp01((2.6f - messageAge) / 0.5f) : 0);

            // the controls: until your first deposit (and in build mode, which has its own keys)
            keysHint.text = frozen ? "" : build.Active ? "Click build  ·  R rotate  ·  Tab catalogue  ·  X demolish  ·  1 done"
                : sim.S.deposits == 0 ? "WASD move  ·  E grab (hold to sweep)  ·  Shift sprint  ·  Esc menu" : "";
        }

        void RefreshWallet(float dt, BuildMode build)
        {
            shownCash = System.Math.Abs(sim.S.cash - shownCash) < 0.005 ? sim.S.cash : shownCash + (sim.S.cash - shownCash) * (1 - Mathf.Exp(-dt * 10));
            cashText.text = Fmt.Money(shownCash);
            // a brief "+$" beside the cash when it grows (a quick run of deposits adds up)
            double gain = sim.S.cash - lastCash;
            lastCash = sim.S.cash;
            if (gain > 1e-9)
            {
                delta = deltaAge < 1.8f ? delta + gain : gain;
                deltaAge = 0;
            }
            deltaAge += dt;
            deltaText.text = deltaAge < 1.8f ? "+" + Fmt.Money(delta) : "";
            deltaText.color = Pal.A(Good, Mathf.Clamp01((1.8f - deltaAge) / 0.5f));
            deltaText.rectTransform.anchoredPosition = new Vector2(28 + cashText.preferredWidth + 12, -32);
            string tokens = sim.S.wishTokens > 0 ? $"✦ {Fmt.Num(sim.S.wishTokens)}" : "";
            if (sim.S.luckyPennies > 0) tokens += (tokens.Length > 0 ? "    " : "") + $"¢ {Fmt.Num(sim.S.luckyPennies)}";
            tokenText.text = tokens;
            // power only matters while building, or when it runs short
            bool shortPower = sim.Buildings.Count > 0 && sim.PowerRatio < 0.999;
            powerText.rectTransform.anchoredPosition = new Vector2(30, tokens.Length > 0 ? -86 : -64);
            powerText.text = build.Active && (sim.Buildings.Count > 0 || sim.PowerUse > 0)
                ? $"⚡ {sim.PowerGen:0.#} / {sim.PowerUse:0.#} kW" + (shortPower ? $"  <color=#{Hex(Bad)}>{sim.PowerRatio * 100:0}%</color>" : "")
                : shortPower ? $"<color=#{Hex(Bad)}>⚡ {sim.PowerRatio * 100:0}% power</color>" : "";
        }

        void RefreshGoal(float dt)
        {
            var o = sim.CurrentObjective;
            if (sim.S.objective != shownGoal)
            {
                // a finished goal ticks green for a moment before the next one fades in
                if (shownGoal >= 0 && sim.S.objective > shownGoal && shownGoal < Content.Objectives.Length) { doneText = Content.Objectives[shownGoal].Text; doneAge = 0; }
                shownGoal = sim.S.objective;
                goalAge = 0;
            }
            doneAge += dt;
            if (doneAge < 1.6f)
            {
                goalText.text = "✓  " + doneText;
                goalText.color = Pal.A(Good, Mathf.Clamp01((1.6f - doneAge) / 0.3f));
                goalHint.color = Pal.A(Soft, 0);
                return;
            }
            goalAge += dt;
            goalText.text = o != null ? $"<color=#{Hex(GoalInk)}>●</color>  {o.Text}" : "";
            goalText.color = Pal.A(Color.white, Mathf.Clamp01(goalAge / 0.4f));
            goalHint.text = o != null ? o.Hint : "";
            // the hint is a first-look thing: it fades after a few seconds
            goalHint.color = Pal.A(Soft, 0.72f * Mathf.Clamp01(goalAge / 0.4f) * Mathf.Clamp01((9f - goalAge) / 1f));
        }

        void RefreshDepth(float dt, bool digMode)
        {
            var mall = sim.Mall;
            if (sim.S.dug != lastDug) { if (lastDug >= 0) depthSeen = Time.time; lastDug = sim.S.dug; }
            bool show = digMode || sim.MallCleared || Time.time - depthSeen < 6f;
            depthAlpha = Mathf.MoveTowards(depthAlpha, show ? 1 : 0, dt * 3);
            depthGroup.alpha = depthAlpha;
            if (depthAlpha > 0.001f)
            {
                var st = sim.CurStratum;
                depthText.text = sim.MallCleared ? "Bare concrete" : $"{Fmt.Feet(sim.DepthFeet)} / {Fmt.Feet(mall.DepthFeet)}";
                stratumText.text = sim.MallCleared ? mall.TreasureName : st.Name;
                depthBar.Set((float)sim.DepthFrac);
                depthBar.Fill.color = MeshKit.Hex(st.Color) + new Color(0.15f, 0.15f, 0.15f, 1);
            }
            eventText.rectTransform.anchoredPosition = new Vector2(-28, depthAlpha > 0.5f ? -92 : -22);
            eventText.text = sim.EventActive ? $"{mall.Event.Name.ToUpperInvariant()}  ·  {sim.EventRemaining:0}s" : "";
        }

        void RefreshCarry(float dt)
        {
            var c = sim.CarryDef;
            if (sim.CarryTier != shownTier) { shownTier = sim.CarryTier; tierAge = 0; }
            tierAge += dt;
            float frac = sim.CarryUsed / (float)Mathf.Max(1, sim.CarryCapacity);
            bool full = frac >= 1;
            carryName.text = full ? "FULL" : c.Name.ToUpperInvariant();
            carryName.color = full ? Bad : Pal.A(Soft, 0.72f * Mathf.Clamp01((4f - tierAge) / 0.8f));
            carryCount.text = $"{Fmt.Int(sim.CarryUsed)} / {Fmt.Int(sim.CarryCapacity)}";
            carryCount.color = full ? Bad : Color.white;
            carryBar.Set(frac);
            carryBar.Fill.color = full ? Pal.Pink : Pal.Accent;
        }

        void RefreshHotbar(float dt, BuildMode build, bool digMode)
        {
            Slot = build.Active ? 3 : digMode ? 2 : 1;
            if (Slot != lastSlot) { lastSlot = Slot; slotAge = 0; }
            slotAge += dt;
            has[0] = true;
            has[1] = sim.DigTier > 0;
            has[2] = build.AnyUnlocked;
            int n = 0;
            foreach (bool h in has) if (h) n++;
            // one tool is no choice at all: the hotbar appears with the second
            hotbar.gameObject.SetActive(n > 1);
            if (n <= 1) return;
            float x = -(n * SlotW + (n - 1) * SlotGap) / 2 + (3 * SlotW + 2 * SlotGap) / 2;
            for (int i = 0; i < 3; i++)
            {
                slotBg[i].gameObject.SetActive(has[i]);
                if (!has[i]) continue;
                bool sel = i + 1 == Slot;
                slotBg[i].rectTransform.anchoredPosition = new Vector2(x, 0);
                x += SlotW + SlotGap;
                slotBg[i].color = sel ? Pal.A(Pal.Accent, 0.8f) : new Color(0, 0, 0, 0.32f);
                slotName[i].text = i == 0 ? "Grab" : i == 1 ? "Dig" : build.Demolish ? "Demolish" : "Build";
                slotName[i].color = sel ? Color.white : Soft;
                slotKey[i].color = sel ? Color.white : Pal.A(Soft, 0.5f);
            }
            // dim when you haven't switched for a while
            hotbarGroup.alpha = Mathf.MoveTowards(hotbarGroup.alpha, slotAge < 3f ? 1f : 0.45f, dt * 2);
        }

        void RefreshEye(float dt, bool frozen)
        {
            var g = sim.Guard;
            bool busted = g.State == GuardState.Busted && g.Timer < 1.4f;
            eyeAlpha = Mathf.MoveTowards(eyeAlpha, (g.Watching || busted) && !frozen ? 1 : 0, dt * 5);
            eyeGroup.alpha = eyeAlpha;
            // half open and twitching during the tell, wide open while he looks
            float open = g.State == GuardState.Suspicious ? 0.35f + 0.12f * Mathf.Abs(Mathf.Sin(Time.time * 9)) : g.State == GuardState.Looking || busted ? 1f : 0.08f;
            eyeOpen = Mathf.Lerp(eyeOpen, open, 1 - Mathf.Exp(-dt * 14));
            eyeWhite.rectTransform.localScale = new Vector3(1, Mathf.Max(0.06f, eyeOpen), 1);
            // the pupil reddens as his suspicion grows; it shrinks while he can't see you (hidden, or out of the water)
            iris.color = busted ? Pal.Red : Color.Lerp(new Color(0.12f, 0.12f, 0.14f), Pal.Red, g.Suspicion);
            iris.rectTransform.localScale = Vector3.one * (g.State != GuardState.Looking || g.Sees ? 1f : 0.55f);
            eyeRing.fillAmount = g.State == GuardState.Looking ? 1 - g.LookProgress : g.State == GuardState.Suspicious ? 1 : 0;
            eyeRing.color = busted ? Pal.Red : Pal.A(Amber, g.State == GuardState.Suspicious ? 0.5f : 0.95f);
            eyeLabel.text = busted ? "" : g.Watching && sim.S.guardLooks <= 3 ? "FREEZE" : "";
            eyeLabel.color = Amber;
            // point at him when he's out of view
            bool point = false;
            if (CameraT != null && eyeAlpha > 0.01f)
            {
                var f = CameraT.forward;
                var to = new Vector3(g.X - CameraT.position.x, 0, g.Z - CameraT.position.z);
                f.y = 0;
                float bearing = Vector3.SignedAngle(f, to, Vector3.up);
                point = Mathf.Abs(bearing) > 40f;
                float a = bearing * Mathf.Deg2Rad;
                eyeArrow.anchoredPosition = new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * 60f;
                eyeArrow.localRotation = Quaternion.Euler(0, 0, -bearing);
            }
            eyeArrow.gameObject.SetActive(point);
            // amber edges while he looks, a red flash when he catches you
            float vig = frozen ? 0 : busted ? 0.5f * Mathf.Clamp01(1 - g.Timer / 1.4f) : g.State == GuardState.Looking ? 0.22f + 0.35f * g.Suspicion : g.State == GuardState.Suspicious ? 0.12f : 0;
            vigAlpha = Mathf.Lerp(vigAlpha, vig, 1 - Mathf.Exp(-dt * 8));
            var vc = busted ? new Color(1f, 0.2f, 0.25f) : new Color(1f, 0.62f, 0.15f);
            vc.a = vigAlpha;
            vignette.color = vc;
        }

        // ───────────────────────────── prompts ─────────────────────────────

        string BuildPrompt(BuildMode b)
        {
            if (b.Demolish)
            {
                var hb = b.HoverBuilding >= 0 ? sim.FindBuilding(b.HoverBuilding) : null;
                return hb != null
                    ? $"<color=#{Hex(Bad)}><b>Click</b>  Demolish {hb.Def.Name}</color>  <color=#{Hex(Soft)}>+{Fmt.Money(hb.Def.Cost * sim.Scale)}</color>"
                    : $"<color=#{Hex(Bad)}>Demolish</color>  <color=#{Hex(Soft)}>· X to stop</color>";
            }
            var d = b.Selected;
            if (d == null) return "";
            string power = d.Power > 0 ? $"  <color=#9CD8FF>+{d.Power:0.#} kW</color>" : d.Power < 0 ? $"  <color=#9CD8FF>{d.Power:0.#} kW</color>" : "";
            string head = $"<b>{d.Name}</b>  <color=#{Hex(Money)}>{Fmt.Money(d.Cost * sim.Scale)}</color>{power}";
            return !b.HasSpot || b.Valid ? head : head + $"  <color=#{Hex(Bad)}>· {b.Reason}</color>";
        }

        string PromptFor(Target t)
        {
            switch (t.Kind)
            {
                case TargetKind.Item:
                {
                    var it = sim.FindLoose(t.Uid);
                    if (it == null) return "";
                    var def = Content.Items[it.Type];
                    if (sim.IsFish(it.Type)) return $"<b>E</b>  Put the goldfish back  <color=#{Hex(Good)}>+1 ✦</color>";
                    if (!sim.CanCarry(it.Type)) return $"<color=#{Hex(Bad)}>Hands full</color>";
                    double v = it.Value * sim.ValueMult * Sim.CatRate(def.Cat);
                    // teach the sweep for the first handful of pickups; an oddity's joke shows while you look at it
                    string hold = sim.S.itemsPicked < 20 ? $"  <color=#{Hex(Soft)}>· hold to sweep</color>" : "";
                    string joke = def.Cat == ItemCat.Oddity && !string.IsNullOrEmpty(def.Desc) ? $"\n<size=14><i><color=#{Hex(Soft)}>{def.Desc}</color></i></size>" : "";
                    return $"<b>E</b>  {def.Name}  <color=#{Hex(Money)}>{Fmt.Money(v)}</color>{hold}{joke}";
                }
                case TargetKind.Wish:
                {
                    var w = sim.FindWish(t.Uid);
                    if (w == null) return "";
                    string col = UIKit.Hex(Pal.Rarity[(int)w.Def.Rarity]);
                    return $"<b>E</b>  Catch the <color=#{col}>wish</color>  <color=#{Hex(Good)}>{Fmt.Money(w.Value)}</color>";
                }
                case TargetKind.Board:
                {
                    int i = sim.NextFountainTech();
                    if (i < 0) return $"<color=#{Hex(Soft)}>Improvement plan: all done</color>";
                    var tech = Content.Techs[i];
                    string cost = Fmt.Money(sim.TechCost(i));
                    return sim.CanAfford(i)
                        ? $"<b>E</b>  {tech.Name}  <color=#{Hex(Money)}>{cost}</color>"
                        : $"{tech.Name}  <color=#{Hex(Bad)}>{cost}</color>";
                }
                case TargetKind.Rival:
                    return "<b>E</b>  Shoo Chad";
                case TargetKind.Crust:
                    return sim.MallCleared ? $"<color=#{Hex(Soft)}>Bare concrete</color>" : "<b>Click</b>  Dig";
                case TargetKind.Building:
                {
                    var b = sim.FindBuilding(t.Uid);
                    if (b == null) return "";
                    if (b.Def.IsBelt) return $"Conveyor Belt  <color=#{Hex(Soft)}>· {b.Items.Count}</color>";
                    return string.IsNullOrEmpty(b.Status) ? b.Def.Name : $"{b.Def.Name}  <color=#{Hex(Bad)}>· {b.Status}</color>";
                }
                case TargetKind.Terminal:
                {
                    int ready = 0;
                    for (int i = 0; i < Content.Techs.Length; i++) if (sim.CanBuyTech(i)) ready++;
                    return "<b>E</b>  Maintenance Terminal" + (ready > 0 ? $"  <color=#{Hex(Good)}>· {ready} ready</color>" : "");
                }
                case TargetKind.Kiosk:
                    return sim.Carried.Count > 0
                        ? $"<b>E</b>  Deposit  <color=#{Hex(Good)}>{Fmt.Money(sim.CarriedValue)}</color>"
                        : $"<color=#{Hex(Soft)}>COIN-O-MATIC 3000</color>";
            }
            return "";
        }
    }
}
