// MAINT-OS 95: the Maintenance Terminal's tech tree. Branch tabs across the top, a node graph
// (each node placed by its Col/Row, lines to its prerequisites), and a detail pane with the
// generated effect line, the price and a Buy button. Descriptions are flavour; numbers come
// from the data (EffectText), so they never drift.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WishExtractor.Core;

namespace WishExtractor.UI
{
    public sealed class TerminalPanel
    {
        Sim sim;
        RectTransform root, graph, detail;
        CanvasGroup group;
        Text statsText, dName, dKind, dDesc, dEffect, dCost, dReq, dLevel;
        UIKit.Btn buyBtn;
        readonly List<UIKit.Btn> tabs = new List<UIKit.Btn>();
        readonly Dictionary<int, NodeCard> cards = new Dictionary<int, NodeCard>();
        TechBranch branch = TechBranch.Carry;
        int selected = -1;
        float refresh, appear;
        public bool IsOpen => root != null && root.gameObject.activeSelf;
        public Action<TechDef> OnBought;
        public Action OnDenied;

        static readonly Color Bg = new Color(0.05f, 0.08f, 0.09f, 0.96f);
        static readonly Color Panel = new Color(0.09f, 0.13f, 0.14f, 1f);
        static readonly Color Green = new Color(0.42f, 1f, 0.62f);
        static readonly Color Dim = new Color(0.55f, 0.66f, 0.64f);
        static readonly Color Faint = new Color(0.3f, 0.38f, 0.37f);

        sealed class NodeCard
        {
            public UIKit.Btn Btn;
            public Text Name, Sub, Cost;
            public Image Bar;
        }

        public static readonly (TechBranch b, string name)[] Branches =
        {
            (TechBranch.Carry, "Carry"), (TechBranch.Tools, "Tools"), (TechBranch.Fountain, "Fountain"),
            (TechBranch.Power, "Power"), (TechBranch.Intake, "Intake"), (TechBranch.Logistics, "Logistics"),
            (TechBranch.Processing, "Processing"), (TechBranch.Security, "Security"),
        };

        public void Build(Canvas canvas, Sim s)
        {
            sim = s;
            root = UIKit.Rect(canvas.transform, "Terminal").Stretch();
            group = root.gameObject.AddComponent<CanvasGroup>();
            var bg = UIKit.Image(root, "Bg", null, Bg);
            bg.rectTransform.Stretch();
            bg.raycastTarget = true;
            // scanlines, because it's a 1995 laptop
            for (int i = 0; i < 54; i++)
            {
                var l = UIKit.Image(root, "Scan", null, new Color(0, 0, 0, 0.12f));
                l.rectTransform.anchorMin = new Vector2(0, 1 - (i + 0.5f) / 54f);
                l.rectTransform.anchorMax = new Vector2(1, 1 - (i + 0.5f) / 54f);
                l.rectTransform.sizeDelta = new Vector2(0, 3);
            }
            var title = UIKit.Label(root, "Title", "MAINT-OS 95  ·  FOUNTAIN MAINTENANCE TERMINAL", 26, Green, TextAnchor.MiddleLeft, UIKit.Mono);
            title.rectTransform.TL(40, 24, 1000, 40);
            statsText = UIKit.Label(root, "Stats", "", 20, Green, TextAnchor.MiddleRight, UIKit.Mono);
            statsText.rectTransform.anchorMin = statsText.rectTransform.anchorMax = new Vector2(1, 1);
            statsText.rectTransform.pivot = new Vector2(1, 1);
            statsText.rectTransform.anchoredPosition = new Vector2(-100, -24);
            statsText.rectTransform.sizeDelta = new Vector2(900, 40);
            var close = UIKit.Button(root, "Close", "✕", new Color(1, 1, 1, 0.08f), Green, 20, Close, 999, UIKit.Symbol);
            close.Rt.anchorMin = close.Rt.anchorMax = new Vector2(1, 1);
            close.Rt.pivot = new Vector2(1, 1);
            close.Rt.anchoredPosition = new Vector2(-30, -24);
            close.Rt.sizeDelta = new Vector2(46, 46);

            var tabBar = UIKit.Rect(root, "Tabs").TL(40, 80, 1400, 44);
            for (int i = 0; i < Branches.Length; i++)
            {
                var br = Branches[i];
                var b = UIKit.Button(tabBar, br.name, br.name.ToUpperInvariant(), Panel, Dim, 16, () => SetBranch(br.b), 10, UIKit.Mono);
                b.Rt.TL(i * 172, 0, 164, 44);
                tabs.Add(b);
            }

            var graphFrame = UIKit.Image(root, "GraphFrame", UIKit.Rounded, Panel, 14);
            graphFrame.rectTransform.Stretch(40, 140, 520, 40);
            var content = UIKit.Rect(graphFrame.rectTransform, "Viewport").Stretch(0, 0, 0, 0);
            content.gameObject.AddComponent<RectMask2D>();
            var hit = content.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            var sr = content.gameObject.AddComponent<ScrollRect>();
            graph = UIKit.Rect(content, "Graph");
            graph.anchorMin = graph.anchorMax = new Vector2(0, 1);
            graph.pivot = new Vector2(0, 1);
            sr.content = graph;
            sr.viewport = content;
            sr.movementType = ScrollRect.MovementType.Clamped;
            sr.scrollSensitivity = 40;

            detail = UIKit.Image(root, "Detail", UIKit.Rounded, Panel, 14).rectTransform;
            detail.anchorMin = new Vector2(1, 0);
            detail.anchorMax = new Vector2(1, 1);
            detail.pivot = new Vector2(1, 0.5f);
            detail.offsetMin = new Vector2(-460, 40);
            detail.offsetMax = new Vector2(-40, -140);
            dKind = UIKit.Label(detail, "Kind", "", 14, Dim, TextAnchor.UpperLeft, UIKit.Mono);
            dKind.rectTransform.TL(24, 20, 372, 20);
            dName = UIKit.Label(detail, "Name", "", 28, Color.white, TextAnchor.UpperLeft, UIKit.Bold, true);
            dName.rectTransform.TL(24, 44, 372, 76);
            dLevel = UIKit.Label(detail, "Level", "", 15, Green, TextAnchor.UpperLeft, UIKit.Mono);
            dLevel.rectTransform.TL(24, 122, 372, 22);
            dEffect = UIKit.Label(detail, "Effect", "", 18, Green, TextAnchor.UpperLeft, UIKit.Semibold, true);
            dEffect.rectTransform.TL(24, 152, 372, 80);
            dDesc = UIKit.Label(detail, "Desc", "", 16, Dim, TextAnchor.UpperLeft, UIKit.Regular, true);
            dDesc.rectTransform.TL(24, 236, 372, 150);
            dDesc.fontStyle = FontStyle.Italic;
            dReq = UIKit.Label(detail, "Req", "", 14, new Color(1f, 0.6f, 0.5f), TextAnchor.UpperLeft, UIKit.Mono, true);
            dReq.rectTransform.TL(24, 396, 372, 60);
            dCost = UIKit.Label(detail, "Cost", "", 30, Color.white, TextAnchor.MiddleLeft, UIKit.Bold);
            dCost.rectTransform.anchorMin = dCost.rectTransform.anchorMax = new Vector2(0, 0);
            dCost.rectTransform.pivot = new Vector2(0, 0);
            dCost.rectTransform.anchoredPosition = new Vector2(24, 100);
            dCost.rectTransform.sizeDelta = new Vector2(372, 44);
            buyBtn = UIKit.Button(detail, "Buy", "INSTALL", Pal.Accent, Color.white, 20, BuySelected, 12, UIKit.Mono);
            buyBtn.Rt.anchorMin = buyBtn.Rt.anchorMax = new Vector2(0, 0);
            buyBtn.Rt.pivot = new Vector2(0, 0);
            buyBtn.Rt.anchoredPosition = new Vector2(24, 28);
            buyBtn.Rt.sizeDelta = new Vector2(372, 58);

            root.gameObject.SetActive(false);
        }

        public void Open(TechBranch? b = null)
        {
            root.gameObject.SetActive(true);
            appear = 0;
            SetBranch(b ?? branch);
        }

        public void Close() { if (root != null) root.gameObject.SetActive(false); }

        public void SetBranch(TechBranch b)
        {
            branch = b;
            selected = -1;
            RebuildGraph();
            // select the first thing worth buying
            foreach (var kv in cards)
                if (!sim.TechMaxed(kv.Key) && sim.TechUnlocked(kv.Key)) { Select(kv.Key); break; }
            if (selected < 0) foreach (var kv in cards) { Select(kv.Key); break; }
            Refresh();
        }

        public TechBranch Branch => branch;

        const float CardW = 230, CardH = 96, StepX = 262, StepY = 140, Pad = 30;

        static Vector2 NodePos(TechDef t) => new Vector2(Pad + t.Col * StepX, -(Pad + t.Row * StepY));

        void RebuildGraph()
        {
            foreach (Transform c in graph) UnityEngine.Object.Destroy(c.gameObject);
            cards.Clear();
            int maxCol = 0, maxRow = 0;
            var inBranch = new List<TechDef>();
            foreach (var t in Content.Techs) if (t.Branch == branch) { inBranch.Add(t); maxCol = Math.Max(maxCol, t.Col); maxRow = Math.Max(maxRow, t.Row); }
            graph.sizeDelta = new Vector2(Pad * 2 + maxCol * StepX + CardW, Pad * 2 + maxRow * StepY + CardH);
            // connection lines first, so cards draw over them
            foreach (var t in inBranch)
                foreach (var r in t.Requires)
                {
                    if (!Content.TechIndex.TryGetValue(r, out int ri) || Content.Techs[ri].Branch != branch) continue;
                    var a = NodePos(Content.Techs[ri]) + new Vector2(CardW / 2, -CardH / 2);
                    var b = NodePos(t) + new Vector2(CardW / 2, -CardH / 2);
                    var line = UIKit.Image(graph, "Line", null, sim.TechLevel(ri) > 0 ? Pal.A(Green, 0.55f) : Pal.A(Faint, 0.8f));
                    var rt = line.rectTransform;
                    rt.anchorMin = rt.anchorMax = new Vector2(0, 1);
                    rt.pivot = new Vector2(0, 0.5f);
                    rt.anchoredPosition = a;
                    rt.sizeDelta = new Vector2(Vector2.Distance(a, b), 4);
                    rt.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(b.y - a.y, b.x - a.x) * Mathf.Rad2Deg);
                }
            if (inBranch.Count == 0)
            {
                var none = UIKit.Label(graph, "Soon", "NO MODULES INSTALLED YET.\nHEAD OFFICE SAYS: 'SOON.'", 22, Dim, TextAnchor.UpperLeft, UIKit.Mono);
                none.rectTransform.TL(Pad, Pad, 800, 80);
                graph.sizeDelta = new Vector2(900, 200);
            }
            foreach (var t in inBranch)
            {
                int ti = t.Index;
                var btn = UIKit.Button(graph, "Node:" + t.Id, "", Panel, Color.white, 14, () => { Select(ti); }, 12);
                btn.Rt.anchorMin = btn.Rt.anchorMax = new Vector2(0, 1);
                btn.Rt.pivot = new Vector2(0, 1);
                btn.Rt.anchoredPosition = NodePos(t);
                btn.Rt.sizeDelta = new Vector2(CardW, CardH);
                btn.Label.text = "";
                var card = new NodeCard { Btn = btn };
                card.Name = UIKit.Label(btn.Rt, "Name", t.Name, 16, Color.white, TextAnchor.UpperLeft, UIKit.Semibold, true);
                card.Name.rectTransform.TL(12, 8, CardW - 24, 42);
                card.Sub = UIKit.Label(btn.Rt, "Sub", "", 12, Dim, TextAnchor.LowerLeft, UIKit.Mono);
                card.Sub.rectTransform.TL(12, CardH - 28, CardW - 24, 20);
                card.Cost = UIKit.Label(btn.Rt, "Cost", "", 15, Green, TextAnchor.LowerRight, UIKit.Bold);
                card.Cost.rectTransform.TL(12, CardH - 30, CardW - 24, 22);
                card.Bar = UIKit.Image(btn.Rt, "Bar", null, Green);
                card.Bar.rectTransform.anchorMin = new Vector2(0, 0);
                card.Bar.rectTransform.anchorMax = new Vector2(0, 0);
                card.Bar.rectTransform.pivot = new Vector2(0, 0);
                card.Bar.rectTransform.sizeDelta = new Vector2(0, 4);
                cards[ti] = card;
            }
        }

        void Select(int i)
        {
            selected = i;
            Refresh();
        }

        void BuySelected()
        {
            if (selected < 0) return;
            var t = Content.Techs[selected];
            if (!sim.BuyTech(selected)) { OnDenied?.Invoke(); return; }
            OnBought?.Invoke(t);
            RebuildGraph();
            Refresh();
        }

        public static string CostText(TechDef t, double cost) => t.WishTokens ? $"✦ {Fmt.Num(cost)}" : Fmt.Money(cost);

        public static string EffectText(TechDef t)
        {
            switch (t.Kind)
            {
                case TechKind.Carry:
                {
                    var c = Content.Carry[(int)t.Value];
                    string s = $"Carry {Fmt.Int(c.Capacity)} items per trip";
                    if (c.SpeedMult < 0.99f) s += $" · walk {Fmt.Pct(1 - c.SpeedMult)} slower";
                    if (c.SpeedMult > 1.01f) s += $" · {Fmt.Pct(c.SpeedMult - 1)} faster";
                    if (c.NoJump) s += " · no jumping";
                    if (c.AutoRadius > 0) s += $" · hoovers up items within {c.AutoRadius:0.0} m";
                    return s;
                }
                case TechKind.Tool:
                {
                    var g = t.Target == "dig" ? Content.DigTools[(int)t.Value] : Content.GrabTools[(int)t.Value];
                    if (t.Target == "dig") return $"Digs {Fmt.Num(g.DigPower)} scoops per swing · {g.Rate:0.#} swings/s";
                    return $"Reach {g.Reach:0.#} m · " + (g.Area > 0 ? $"grabs everything within {g.Area:0.##} m" : "one item at a time") + $" · {g.Rate:0.#} grabs/s";
                }
                case TechKind.Wishability: return $"+{t.Value:0} wishability" + (t.MaxLevel > 1 ? " per level" : "") + ": more shoppers, more tosses, fancier tosses";
                case TechKind.ValueMult: return $"+{t.Value * 100:0}% deposit value per level";
                case TechKind.TossRate: return $"+{t.Value * 100:0}% toss rate per level";
                case TechKind.WalkSpeed: return $"+{t.Value * 100:0}% walking speed per level";
                case TechKind.Reach: return $"+{t.Value:0.##} m reach per level";
                case TechKind.GrabRate: return $"+{t.Value * 100:0}% grab speed per level";
                case TechKind.WishLife: return $"+{t.Value * 100:0}% wish hover time per level";
                case TechKind.CarryBonus: return $"+{t.Value * 100:0}% carry capacity per level (containers only)";
                case TechKind.MachineSpeed: return $"+{t.Value * 100:0}% speed for {(string.IsNullOrEmpty(t.Target) ? "all machines" : t.Target)} per level";
                case TechKind.BeltSpeed: return "Faster conveyor belts";
                case TechKind.DepositMult: return $"+{t.Value * 100:0}% value for {t.Target} per level";
                case TechKind.GuardFine: return $"-{t.Value * 100:0}% security fines per level";
                case TechKind.Unlock: return "Unlocks: " + t.Target;
            }
            return "";
        }

        public void Update(float dt)
        {
            if (!IsOpen) return;
            appear = Mathf.Min(1, appear + dt * 7);
            group.alpha = appear;
            refresh -= dt;
            if (refresh <= 0) { refresh = 0.25f; Refresh(); }
        }

        void Refresh()
        {
            statsText.text = $"CASH {Fmt.Money(sim.S.cash)}   ✦ {Fmt.Num(sim.S.wishTokens)}   WISHABILITY {sim.Wishability:0}";
            for (int i = 0; i < tabs.Count; i++)
            {
                bool on = Branches[i].b == branch;
                bool any = false;
                foreach (var t in Content.Techs) if (t.Branch == Branches[i].b) { any = true; break; }
                tabs[i].SetColors(on ? Pal.A(Green, 0.25f) : Panel, Panel);
                tabs[i].Label.color = on ? Green : any ? Dim : Faint;
            }
            foreach (var kv in cards)
            {
                int i = kv.Key;
                var t = Content.Techs[i];
                var c = kv.Value;
                int lvl = sim.TechLevel(i);
                bool maxed = sim.TechMaxed(i), unlocked = sim.TechUnlocked(i), afford = sim.CanAfford(i);
                c.Sub.text = t.MaxLevel > 1 ? $"LV {lvl}/{t.MaxLevel}" : lvl > 0 ? "INSTALLED" : unlocked ? "" : "LOCKED";
                c.Cost.text = maxed ? "✓" : CostText(t, sim.TechCost(i));
                c.Cost.color = maxed ? Green : afford && unlocked ? Color.white : new Color(1f, 0.5f, 0.45f);
                Color bg = maxed || (lvl > 0 && t.MaxLevel == 1) ? new Color(0.1f, 0.26f, 0.18f) : unlocked ? (afford ? new Color(0.12f, 0.2f, 0.22f) : Panel) : new Color(0.07f, 0.09f, 0.1f);
                if (i == selected) bg = Color.Lerp(bg, Pal.Accent, 0.35f);
                c.Btn.SetColors(bg, bg);
                c.Name.color = unlocked ? Color.white : Faint;
                c.Bar.rectTransform.sizeDelta = new Vector2(CardW * (t.MaxLevel > 0 ? lvl / (float)t.MaxLevel : 0), 4);
            }
            if (selected < 0 || selected >= Content.Techs.Length)
            {
                dName.text = ""; dKind.text = ""; dDesc.text = ""; dEffect.text = ""; dCost.text = ""; dReq.text = ""; dLevel.text = "";
                buyBtn.Rt.gameObject.SetActive(false);
                return;
            }
            var st = Content.Techs[selected];
            int L = sim.TechLevel(selected);
            dKind.text = $"{st.Branch.ToString().ToUpperInvariant()} MODULE";
            dName.text = st.Name;
            dLevel.text = st.MaxLevel > 1 ? $"LEVEL {L} / {st.MaxLevel}" : L > 0 ? "INSTALLED" : "NOT INSTALLED";
            dEffect.text = "» " + EffectText(st);
            dDesc.text = st.Desc;
            var missing = new List<string>();
            foreach (var r in st.Requires)
                if (Content.TechIndex.TryGetValue(r, out int ri) && sim.TechLevel(ri) <= 0) missing.Add(Content.Techs[ri].Name);
            dReq.text = missing.Count > 0 ? "REQUIRES: " + string.Join(", ", missing) : "";
            bool maxedSel = sim.TechMaxed(selected);
            dCost.text = maxedSel ? "MAXED" : CostText(st, sim.TechCost(selected));
            dCost.color = maxedSel ? Green : sim.CanAfford(selected) ? Color.white : new Color(1f, 0.5f, 0.45f);
            buyBtn.Rt.gameObject.SetActive(!maxedSel);
            bool can = sim.CanBuyTech(selected);
            buyBtn.Interactable = can;
            buyBtn.Label.text = !sim.TechUnlocked(selected) ? "LOCKED" : !sim.CanAfford(selected) ? (st.WishTokens ? "NEED MORE WISH TOKENS" : "NEED MORE CASH") : L > 0 ? "UPGRADE" : "INSTALL";
        }
    }
}
