// Right-hand shop: Tools, Machines, Upgrades and Head Office tabs. Cards are rebuilt when the
// set of visible entries changes and refreshed a few times a second otherwise.
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.UI;
using WishExtractor.Core;

namespace WishExtractor.UI
{
    public sealed class ShopPanel
    {
        public enum Tab { Tools, Machines, Upgrades, HeadOffice }

        Sim sim;
        RectTransform panel, content;
        ScrollRect scroll;
        readonly UIKit.Btn[] tabs = new UIKit.Btn[4];
        readonly UIKit.Btn[] amountBtns = new UIKit.Btn[4];
        RectTransform amountRow;
        Text headerNote;
        public Tab Current { get; private set; } = Tab.Machines;
        public int BuyAmount = 1;   // 1, 10, 100, -1 = max
        string signature = "";
        float refreshTimer;
        readonly List<Card> cards = new List<Card>();
        public bool Visible { get; private set; } = true;
        public Action<string> OnBought;
        public Action OnPrestige;
        public Func<bool> IsModalOpen;

        sealed class Card
        {
            public string Id;
            public int Kind;   // 0 tool, 1 machine, 2 upgrade, 3 head office, 4 header, 5 prestige, 9 info
            public int Index;
            public RectTransform Rt;
            public Text Title, Desc, Info, Badge;
            public UIKit.Btn Buy;
            public UIKit.Bar Bar;
            public Image Outline, Icon;
            public bool Locked;
        }

        public void Build(Canvas canvas, Sim s)
        {
            sim = s;
            panel = UIKit.Card(canvas.transform, "Shop");
            panel.anchorMin = new Vector2(1, 0);
            panel.anchorMax = new Vector2(1, 1);
            panel.pivot = new Vector2(1, 1);
            panel.offsetMin = new Vector2(-520, 20);
            panel.offsetMax = new Vector2(-20, -84);

            string[] names = { "Tools", "Machines", "Upgrades", "Head Office" };
            var seg = UIKit.Image(panel, "Tabs", UIKit.Rounded, new Color(0, 0, 0, 0.06f), 16);
            seg.rectTransform.Row(16, 42, 16, 16);
            for (int i = 0; i < 4; i++)
            {
                int ti = i;
                var b = UIKit.Button(seg.rectTransform, names[i], names[i], Color.clear, Pal.Ink2, 16, () => SetTab((Tab)ti), 14);
                b.Rt.anchorMin = new Vector2(i / 4f, 0);
                b.Rt.anchorMax = new Vector2((i + 1) / 4f, 1);
                b.Rt.offsetMin = new Vector2(3, 3);
                b.Rt.offsetMax = new Vector2(-3, -3);
                tabs[i] = b;
            }
            amountRow = UIKit.Rect(panel, "Amounts").Row(68, 34, 16, 16);
            headerNote = UIKit.Label(amountRow, "Note", "Buy", 14, Pal.Ink2, TextAnchor.MiddleLeft, UIKit.Semibold);
            headerNote.rectTransform.TL(6, 0, 140, 34);
            string[] amts = { "×1", "×10", "×100", "MAX" };
            int[] vals = { 1, 10, 100, -1 };
            for (int i = 0; i < 4; i++)
            {
                int v = vals[i];
                var b = UIKit.Button(amountRow, amts[i], amts[i], Color.clear, Pal.Ink2, 14, () => { BuyAmount = v; RefreshAmountButtons(); refreshTimer = 0; }, 12);
                b.Rt.anchorMin = b.Rt.anchorMax = new Vector2(1, 0.5f);
                b.Rt.pivot = new Vector2(1, 0.5f);
                b.Rt.sizeDelta = new Vector2(70, 30);
                b.Rt.anchoredPosition = new Vector2(-(3 - i) * 76, 0);
                amountBtns[i] = b;
            }
            content = UIKit.ScrollList(panel, "List", out scroll, 10, 10);
            content.parent.GetComponent<RectTransform>().Stretch(6, 108, 6, 10);
            SetTab(Tab.Machines);
            RefreshAmountButtons();
        }

        public void SetVisible(bool on)
        {
            Visible = on;
            panel.gameObject.SetActive(on);
        }

        public void SetTab(Tab t)
        {
            Current = t;
            for (int i = 0; i < 4; i++)
            {
                bool on = (int)t == i;
                tabs[i].SetColors(on ? Color.white : Color.clear, Color.clear);
                tabs[i].Label.color = on ? Pal.Ink : Pal.Ink2;
            }
            amountRow.gameObject.SetActive(t == Tab.Machines);
            content.parent.GetComponent<RectTransform>().offsetMax = new Vector2(-6, t == Tab.Machines ? -108 : -72);
            signature = "";
            refreshTimer = 0;
            scroll.verticalNormalizedPosition = 1;
        }

        void RefreshAmountButtons()
        {
            int[] vals = { 1, 10, 100, -1 };
            for (int i = 0; i < 4; i++)
            {
                bool on = BuyAmount == vals[i];
                amountBtns[i].SetColors(on ? Pal.Accent : new Color(0, 0, 0, 0.05f), Color.clear);
                amountBtns[i].Label.color = on ? Color.white : Pal.Ink2;
            }
        }

        public void CycleBuyAmount()
        {
            BuyAmount = BuyAmount == 1 ? 10 : BuyAmount == 10 ? 100 : BuyAmount == 100 ? -1 : 1;
            RefreshAmountButtons();
            refreshTimer = 0;
        }

        // ───────────────────────────── building ─────────────────────────────

        string ComputeSignature(out List<(int kind, int index, bool locked)> entries)
        {
            entries = new List<(int, int, bool)>();
            var sb = new StringBuilder();
            sb.Append((int)Current).Append('|').Append(sim.S.mallIndex).Append('|');
            switch (Current)
            {
                case Tab.Tools:
                    for (int i = 0; i < Content.Tools.Length; i++)
                    {
                        if (i < sim.S.tool) continue;
                        if (i > sim.S.tool + 3) break;
                        entries.Add((0, i, i > sim.S.tool + 1));
                    }
                    sb.Append(sim.S.tool);
                    break;
                case Tab.Machines:
                    Stage? last = null;
                    foreach (var m in OrderedMachines())
                    {
                        bool un = sim.MachineUnlocked(m.Index), tease = sim.MachineTeased(m.Index);
                        if (!un && !tease) continue;
                        Stage st = m.IsCompressor || m.IsMega ? (Stage)3 : m.Stage;
                        if (last == null || last.Value != st) { entries.Add((4, (int)st, false)); last = st; }
                        entries.Add((1, m.Index, !un));
                        sb.Append(m.Index).Append(un ? 'u' : 't');
                    }
                    break;
                case Tab.Upgrades:
                    var avail = new List<int>();
                    var locked = new List<int>();
                    for (int i = 0; i < Content.Upgrades.Length; i++)
                    {
                        if (sim.HasUpgrade(i)) continue;
                        var u = Content.Upgrades[i];
                        if (u.MallOnly && sim.MallDefIndex != u.ReqMall) continue;
                        if (sim.S.mallIndex < u.ReqMall) continue;
                        if (sim.UpgradeReqMet(i)) avail.Add(i);
                        else if (u.ReqMachine == null || sim.MachineCount(u.ReqMachine) > 0 || sim.MachineUnlocked(Content.MachineIndex[u.ReqMachine])) locked.Add(i);
                    }
                    avail.Sort((a, b) => sim.UpgradeCost(a).CompareTo(sim.UpgradeCost(b)));
                    locked.Sort((a, b) => sim.UpgradeCost(a).CompareTo(sim.UpgradeCost(b)));
                    foreach (var i in avail) { entries.Add((2, i, false)); sb.Append(i).Append('a'); }
                    for (int k = 0; k < Math.Min(4, locked.Count); k++) { entries.Add((2, locked[k], true)); sb.Append(locked[k]).Append('l'); }
                    if (avail.Count == 0 && locked.Count == 0) entries.Add((9, 0, false));
                    sb.Append('#').Append(sim.UpgradesOwnedCount);
                    break;
                case Tab.HeadOffice:
                    entries.Add((5, 0, false));
                    for (int i = 0; i < Content.HeadOffice.Length; i++) entries.Add((3, i, false));
                    sb.Append(sim.MallCleared ? 'c' : 'n');
                    break;
            }
            return sb.ToString();
        }

        IEnumerable<MachineDef> OrderedMachines()
        {
            for (int st = 0; st < 3; st++)
                foreach (var m in Content.Machines)
                    if (!m.IsCompressor && !m.IsMega && (int)m.Stage == st) yield return m;
            foreach (var m in Content.Machines) if (m.IsCompressor) yield return m;
            foreach (var m in Content.Machines) if (m.IsMega) yield return m;
        }

        void Rebuild(List<(int kind, int index, bool locked)> entries)
        {
            foreach (var c in cards) UnityEngine.Object.Destroy(c.Rt.gameObject);
            cards.Clear();
            foreach (var e in entries)
            {
                Card c;
                switch (e.kind)
                {
                    case 4: c = HeaderCard(e.index); break;
                    case 5: c = PrestigeCard(); break;
                    case 9: c = InfoCard("You've bought every upgrade on offer right now. Build more machines or dig deeper to reveal more."); break;
                    default: c = ItemCard(e.kind, e.index, e.locked); break;
                }
                cards.Add(c);
            }
        }

        Card HeaderCard(int stage)
        {
            string[] names = { "DREDGE  ·  dig crust out of the basin", "DISSOLVE  ·  wash the syrup off", "SORT  ·  separate treasure from zinc", "WISHES & MEGAPROJECTS" };
            var rt = UIKit.Rect(content, "Header");
            UIKit.Layout(rt.gameObject, 30);
            var dot = UIKit.Dot(rt, "Dot", Pal.Stage[Mathf.Min(stage, 3)], 10);
            dot.rectTransform.TL(10, 13, 10, 10);
            var t = UIKit.Label(rt, "T", names[stage], 13, Pal.Ink2, TextAnchor.MiddleLeft, UIKit.Semibold);
            t.rectTransform.TL(28, 4, 430, 24);
            return new Card { Kind = 4, Rt = rt };
        }

        Card InfoCard(string text)
        {
            var rt = UIKit.Card(content, "Info", new Color(1, 1, 1, 0.6f), 18, false);
            UIKit.Layout(rt.gameObject, 80);
            var t = UIKit.Label(rt, "T", text, 15, Pal.Ink2, TextAnchor.MiddleLeft, UIKit.Regular, true);
            t.rectTransform.Stretch(18, 10, 18, 10);
            return new Card { Kind = 9, Rt = rt };
        }

        Card PrestigeCard()
        {
            var rt = UIKit.Card(content, "Contract", new Color(1f, 0.97f, 0.88f, 0.95f), 18, false);
            UIKit.Layout(rt.gameObject, 150);
            var c = new Card { Kind = 5, Rt = rt };
            c.Title = UIKit.Label(rt, "Title", "", 19, Pal.Ink, TextAnchor.UpperLeft, UIKit.Bold, true);
            c.Title.rectTransform.TL(18, 14, 432, 28);
            c.Desc = UIKit.Label(rt, "Desc", "", 14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            c.Desc.rectTransform.TL(18, 44, 432, 56);
            c.Buy = UIKit.Button(rt, "Sign", "Sign contract", Pal.Gold, Color.white, 16, () => OnPrestige?.Invoke());
            c.Buy.Rt.TL(18, 104, 220, 34);
            c.Info = UIKit.Label(rt, "LP", "", 15, Pal.GoldInk, TextAnchor.MiddleRight, UIKit.Semibold);
            c.Info.rectTransform.TL(240, 104, 212, 34);
            return c;
        }

        Card ItemCard(int kind, int index, bool locked)
        {
            float h = kind == 1 ? (locked ? 80 : 124) : kind == 2 ? (locked ? 96 : 108) : 104;
            var rt = UIKit.Card(content, "Card", locked ? new Color(1, 1, 1, 0.55f) : new Color(1, 1, 1, 0.92f), 18, false);
            UIKit.Layout(rt.gameObject, h);
            var c = new Card { Kind = kind, Index = index, Rt = rt, Locked = locked };
            var outline = UIKit.Image(rt, "Goal", UIKit.Rounded, Pal.Accent, 18);
            outline.rectTransform.Stretch(-2, -2, -2, -2);
            outline.fillCenter = false;
            outline.gameObject.SetActive(false);
            c.Outline = outline;
            Color iconCol;
            string glyph;
            switch (kind)
            {
                case 0: iconCol = Pal.Pink; glyph = "✋"; c.Id = Content.Tools[index].Id; break;
                case 1:
                    var m = Content.Machines[index];
                    c.Id = m.Id;
                    iconCol = m.IsCompressor || m.IsMega ? Pal.Stage[2] : Pal.Stage[(int)m.Stage];
                    if (m.IsMega) iconCol = Pal.Gold;
                    glyph = m.IsCompressor ? "◆" : m.IsMega ? "★" : m.Tier.ToString();
                    break;
                case 2:
                    var u = Content.Upgrades[index];
                    c.Id = u.Id;
                    iconCol = UpgradeColor(u);
                    glyph = "↑";
                    break;
                default: iconCol = Pal.Gold; glyph = "¢"; c.Id = Content.HeadOffice[index].Id; break;
            }
            c.Icon = UIKit.Dot(rt, "Icon", locked ? Pal.Ink3 : iconCol, 44);
            c.Icon.rectTransform.TL(16, 16, 44, 44);
            var g = UIKit.Label(c.Icon.rectTransform, "G", glyph, 20, Color.white, TextAnchor.MiddleCenter, glyph.Length == 1 && char.IsDigit(glyph[0]) ? UIKit.Bold : UIKit.Symbol);
            g.rectTransform.Stretch();
            c.Title = UIKit.Label(rt, "Title", "", 17, locked ? Pal.Ink2 : Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold);
            c.Title.rectTransform.TL(72, 12, 250, 24);
            c.Badge = UIKit.Label(rt, "Badge", "", 15, Pal.Ink2, TextAnchor.UpperRight, UIKit.Semibold);
            c.Badge.rectTransform.TL(322, 12, 132, 24);
            c.Desc = UIKit.Label(rt, "Desc", "", 13, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            c.Desc.rectTransform.TL(72, 36, 380, 38);
            c.Info = UIKit.Label(rt, "Info", "", 13, Pal.AccentDark, TextAnchor.UpperLeft, UIKit.Semibold, true);
            c.Info.rectTransform.TL(72, h - 46, 222, 36);
            if (kind == 1)
            {
                c.Bar = UIKit.ProgressBar(rt, "Milestone", new Color(0, 0, 0, 0.06f), Pal.Gold);
                c.Bar.Rt.TL(72, h - 8, 222, 4);
            }
            int ci = cards.Count;
            c.Buy = UIKit.Button(rt, "Buy", "", Pal.Accent, Color.white, 15, () => Buy(c), 14);
            c.Buy.Rt.TL(304, h - 50, 150, 38);
            if (locked) c.Buy.Rt.gameObject.SetActive(false);
            FillStatic(c);
            return c;
        }

        static Color UpgradeColor(UpgradeDef u)
        {
            switch (u.Kind)
            {
                case UpgradeKind.MachineMult:
                    var m = Content.Machines[Content.MachineIndex[u.Target]];
                    return Pal.Stage[(int)m.Stage];
                case UpgradeKind.StageMult: return Pal.Stage[(int)Enum.Parse(typeof(Stage), u.Target)];
                case UpgradeKind.ValueMult: case UpgradeKind.AutoSell: case UpgradeKind.ScrapRate: return Pal.Green;
                case UpgradeKind.WishFreq: case UpgradeKind.WishValue: case UpgradeKind.WishLife: case UpgradeKind.CompressorEff: return Pal.Purple;
                case UpgradeKind.GoldenFreq: case UpgradeKind.GoldenPower: return Pal.Gold;
                case UpgradeKind.ClickMult: case UpgradeKind.ClickPctOfDig: case UpgradeKind.ComboMax: return Pal.Pink;
                default: return Pal.Accent;
            }
        }

        public static string EffectText(UpgradeDef u)
        {
            string v = Fmt.Num(u.Value);
            switch (u.Kind)
            {
                case UpgradeKind.MachineMult: return $"{Content.Machines[Content.MachineIndex[u.Target]].Name} ×{v} speed";
                case UpgradeKind.StageMult:
                    string st = u.Target == "Dig" ? "digging" : u.Target == "Wash" ? "washing" : "sorting";
                    return $"All {st} machines ×{v}";
                case UpgradeKind.AllRateMult: return $"Every machine ×{v}";
                case UpgradeKind.ValueMult: return $"Everything sells for ×{v}";
                case UpgradeKind.ClickMult: return $"Clicks dig ×{v}";
                case UpgradeKind.ClickPctOfDig: return $"Each click also digs {Fmt.Num(u.Value * 100)}% of auto-dig/s";
                case UpgradeKind.ComboMax: return $"Combo cap +{v}";
                case UpgradeKind.WishFreq: return $"Wishes appear ×{v} as often";
                case UpgradeKind.WishValue: return $"Wishes worth ×{v}";
                case UpgradeKind.WishLife: return $"Wishes linger ×{v} longer";
                case UpgradeKind.RelicRate: return $"Relic finds ×{v}";
                case UpgradeKind.RelicValue: return $"Relics worth ×{v}";
                case UpgradeKind.GoldenFreq: return $"Golden pennies ×{v} as often";
                case UpgradeKind.GoldenPower: return $"Golden penny effects ×{v}";
                case UpgradeKind.AutoSell: return "Sorted loot sells automatically";
                case UpgradeKind.ScrapRate: return $"Dumped gunk & unsorted loot +{Fmt.Num(u.Value * 100)}% value";
                case UpgradeKind.CompressorEff: return $"Compressor catches +{Fmt.Num(u.Value * 100)}% of missed wishes";
            }
            return "";
        }

        string HOEffect(HeadOfficeDef h, int level)
        {
            switch (h.Kind)
            {
                case HOKind.RateMult: return $"Every machine +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.ValueMult: return $"Sale value +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.ClickMult: return $"Clicks +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.SeedMoney: return level > 0 ? $"Start each mall with {Fmt.Money(20 * Math.Pow(15, level - 1) * sim.CostScale)} here" : "No starting cash";
                case HOKind.StartTool: return $"Start with: {Content.Tools[Math.Min(level, Content.Tools.Length - 1)].Name}";
                case HOKind.VeteranCrew: return $"Start with {level * 5} pogo sticks, tumblers and pigeon perches";
                case HOKind.AutoSellStart: return level > 0 ? "Auto-sell from the first penny" : "Buy the Coin-Op Hookup every mall";
                case HOKind.WishLife: return $"Wishes linger +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.WishValue: return $"Wish value +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.GoldenFreq: return $"Golden pennies +{Fmt.Num(h.Value * 100 * level)}% more often";
                case HOKind.GoldenPower: return $"Golden effects +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.RelicRate: return $"Relic finds +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.ComboMax: return $"Combo cap +{Fmt.Num(h.Value * level)}";
                case HOKind.ScrapRate: return $"Dump value +{Fmt.Num(h.Value * 100 * level)}%";
                case HOKind.CostDiscount: return $"Machines {Fmt.Num((1 - Math.Pow(1 - h.Value, level)) * 100)}% cheaper";
                case HOKind.OfflineHours: return $"Offline earnings for up to {Fmt.Num(Balance.OfflineBaseHours + h.Value * level)} h";
                case HOKind.OfflineEff: return $"Offline efficiency {Fmt.Num(Math.Min(100, (Balance.OfflineBaseEff + h.Value * level) * 100))}%";
                case HOKind.CompressorStart: return level > 0 ? $"Compressor from the start, +{Fmt.Num(h.Value * 100 * level)}% catch" : "Compressor unlocks at depth";
            }
            return "";
        }

        void FillStatic(Card c)
        {
            switch (c.Kind)
            {
                case 0:
                    var t = Content.Tools[c.Index];
                    c.Title.text = t.Name;
                    c.Desc.text = t.Desc;
                    break;
                case 1:
                    var m = Content.Machines[c.Index];
                    c.Title.text = m.Name;
                    c.Desc.text = c.Locked ? sim.MachineLockText(c.Index) : m.Desc;
                    break;
                case 2:
                    var u = Content.Upgrades[c.Index];
                    c.Title.text = u.Name;
                    c.Desc.text = c.Locked ? "Locked: " + sim.UpgradeReqText(c.Index) : u.Desc;
                    break;
                case 3:
                    var h = Content.HeadOffice[c.Index];
                    c.Title.text = h.Name;
                    c.Desc.text = h.Desc;
                    break;
            }
        }

        // ───────────────────────────── refresh ─────────────────────────────

        public void Refresh(float dt, string focusId)
        {
            if (!Visible) return;
            refreshTimer -= dt;
            if (refreshTimer > 0) return;
            refreshTimer = 0.2f;
            string sig = ComputeSignature(out var entries);
            if (sig != signature)
            {
                signature = sig;
                Rebuild(entries);
            }
            float pulse = 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 5f);
            foreach (var c in cards)
            {
                if (c.Outline != null)
                {
                    bool goal = focusId != null && c.Id == focusId && !c.Locked;
                    c.Outline.gameObject.SetActive(goal);
                    if (goal) UIKit.SetAlpha(c.Outline, pulse);
                }
                switch (c.Kind)
                {
                    case 0: RefreshTool(c); break;
                    case 1: RefreshMachine(c); break;
                    case 2: RefreshUpgrade(c); break;
                    case 3: RefreshHO(c); break;
                    case 5: RefreshPrestige(c); break;
                }
            }
        }

        void RefreshTool(Card c)
        {
            var t = Content.Tools[c.Index];
            bool owned = c.Index == sim.S.tool;
            c.Badge.text = owned ? "Equipped" : c.Locked ? "" : "Next tool";
            c.Info.text = $"{Fmt.Num(t.Power)} items per click";
            if (c.Locked) { c.Desc.text = "Buy the previous tool first."; return; }
            c.Buy.Rt.gameObject.SetActive(!owned);
            if (!owned)
            {
                double cost = sim.ToolCost(t);
                c.Buy.Label.text = Fmt.Money(cost);
                c.Buy.Interactable = sim.S.cash >= cost;
            }
            else
            {
                c.Info.text = $"{Fmt.Num(sim.ClickPowerNow)} per click now (×{sim.ComboMult:0.0} combo)";
            }
        }

        void RefreshMachine(Card c)
        {
            var m = Content.Machines[c.Index];
            int count = sim.MachineCount(c.Index);
            if (c.Locked)
            {
                c.Badge.text = "Locked";
                c.Info.text = "";
                c.Desc.text = sim.MachineLockText(c.Index);
                if (sim.MachineUnlocked(c.Index)) signature = "";
                return;
            }
            c.Badge.text = m.MaxCount < int.MaxValue ? $"Lv {count}/{m.MaxCount}" : $"× {count}";
            if (m.IsCompressor)
                c.Info.text = count > 0 ? $"Catches {Fmt.Pct(sim.CompressorChance)} of missed wishes" : "Catches wishes you miss";
            else if (m.IsMega)
                c.Info.text = $"+{Fmt.Num(m.MegaPerLevel * 100)}% per level · now ×{Fmt.Num(1 + m.MegaPerLevel * count)}";
            else
            {
                double unit = sim.MachineUnitRate[c.Index];
                if (unit <= 0) unit = m.BaseRate * sim.AllRateMult;
                c.Info.text = $"{Fmt.Rate(unit)} each · {Fmt.Rate(unit * count)} total";
                int next = Sim.NextMilestone(count);
                if (c.Bar != null)
                {
                    if (next > 0)
                    {
                        int prev = 0;
                        foreach (int ms in Balance.Milestones) { if (ms > count) break; prev = ms; }
                        c.Bar.Set((count - prev) / (float)(next - prev));
                        c.Info.text += $"\nNext ×2 at {next}";
                    }
                    else c.Bar.Set(1);
                }
            }
            int n = BuyAmount == -1 ? Math.Max(1, sim.MachineMaxAffordable(c.Index)) : BuyAmount;
            if (m.IsCompressor || m.IsMega) n = 1;
            double cost = sim.MachineCost(c.Index, n);
            bool maxed = count >= m.MaxCount;
            c.Buy.Label.text = maxed ? "Maxed" : (n > 1 ? $"×{n}  " : "") + Fmt.Money(cost);
            c.Buy.Interactable = !maxed && sim.S.cash >= cost;
        }

        void RefreshUpgrade(Card c)
        {
            var u = Content.Upgrades[c.Index];
            if (sim.HasUpgrade(c.Index)) { signature = ""; return; }
            c.Info.text = EffectText(u);
            if (c.Locked)
            {
                c.Badge.text = Fmt.Money(sim.UpgradeCost(c.Index));
                if (sim.UpgradeReqMet(c.Index)) signature = "";
                return;
            }
            c.Badge.text = "";
            double cost = sim.UpgradeCost(c.Index);
            c.Buy.Label.text = Fmt.Money(cost);
            c.Buy.Interactable = sim.S.cash >= cost;
        }

        void RefreshHO(Card c)
        {
            var h = Content.HeadOffice[c.Index];
            int lv = sim.HOLevel(c.Index);
            c.Badge.text = $"Lv {lv}/{h.MaxLevel}";
            c.Info.text = HOEffect(h, Math.Max(lv, 1)) + (lv == 0 ? "  (at Lv 1)" : "");
            bool maxed = sim.HOMaxed(c.Index);
            double cost = sim.HOCost(c.Index);
            c.Buy.Label.text = maxed ? "Maxed" : $"{Fmt.Num(cost)} LP";
            c.Buy.Interactable = !maxed && sim.S.luckyPennies >= cost;
            c.Buy.SetColors(Pal.Gold, Pal.Disabled);
        }

        void RefreshPrestige(Card c)
        {
            var next = Content.Malls[(sim.S.mallIndex + 1) % Content.Malls.Length];
            if (sim.MallCleared)
            {
                c.Title.text = sim.IsFinalMall ? "The First Wish is yours." : $"Contract complete! Next: {next.Name}";
                c.Desc.text = sim.IsFinalMall || sim.InRemodel
                    ? $"Sign a Remodel contract to start the malls again, bigger and richer. You keep Lucky Pennies, your Wish Journal, relics and achievements."
                    : $"{next.Tagline}. You keep Lucky Pennies, Head Office perks, the Wish Journal, relics and achievements. Cash, machines and upgrades reset.";
                c.Buy.Rt.gameObject.SetActive(true);
                c.Buy.Label.text = $"Sign  (+{Fmt.Num(sim.PrestigeReward)} LP)";
            }
            else
            {
                c.Title.text = "Head Office";
                c.Desc.text = $"Lucky Pennies buy permanent perks for every future mall. Hit bare concrete in {sim.Mall.Name} to earn {Fmt.Num(sim.PrestigeReward)} LP and sign with the next mall.";
                c.Buy.Rt.gameObject.SetActive(false);
            }
            c.Info.text = $"You have {Fmt.Num(sim.S.luckyPennies)} LP";
        }

        void Buy(Card c)
        {
            bool ok = false;
            switch (c.Kind)
            {
                case 0: ok = sim.BuyNextTool(); break;
                case 1:
                    var m = Content.Machines[c.Index];
                    int n = BuyAmount == -1 ? sim.MachineMaxAffordable(c.Index) : BuyAmount;
                    if (m.IsCompressor || m.IsMega) n = 1;
                    ok = n > 0 && sim.BuyMachine(c.Index, n);
                    break;
                case 2: ok = sim.BuyUpgrade(c.Index); break;
                case 3: ok = sim.BuyHO(c.Index); break;
            }
            OnBought?.Invoke(ok ? c.Id : null);
            refreshTimer = 0;
        }
    }
}
