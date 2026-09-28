// Always-on HUD: wallet (top-left), depth gauge (top-centre), pipeline card (left),
// goal card (bottom-left), combo meter, active buffs and the mall event banner (bottom-centre).
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WishExtractor.Core;
using WishExtractor.View;

namespace WishExtractor.UI
{
    public sealed class HUD
    {
        Sim sim;
        RectTransform root;
        // wallet
        Text cashText, incomeText, contractText, lpText;
        double shownCash;
        // depth
        Text mallText, stratumText, depthText, nextText, layerCount;
        Image stratumDot;
        UIKit.Bar depthBar;
        UIKit.Btn contractBtn;
        RectTransform depthCard;
        Image depthBg;
        // pipeline
        sealed class StageRow { public Text Name, Rate, Amount, Worth; public UIKit.Bar Bar; public RectTransform Bottleneck; public UIKit.Btn Dump; }
        readonly StageRow[] rows = new StageRow[4];
        UIKit.Btn sellBtn;
        UIKit.Btn autoBtn;
        Text sellHint;
        // goal
        Text goalTitle, goalText, goalHint, goalReward;
        RectTransform goalCard;
        // combo / buffs / event
        RectTransform comboCard;
        CanvasGroup comboGroup;
        Text comboText;
        UIKit.Bar comboBar;
        RectTransform buffRow;
        readonly List<(RectTransform rt, Text txt, UIKit.Bar bar)> buffPills = new List<(RectTransform, Text, UIKit.Bar)>();
        RectTransform eventCard;
        CanvasGroup eventGroup;
        Text eventTitle, eventDesc;
        UIKit.Bar eventBar;
        float slowTimer;

        public Action OnSell, OnDumpRaw, OnDumpWashed, OnContract, OnToggleAutoSell;

        public void Build(Canvas canvas, Sim s)
        {
            sim = s;
            root = UIKit.Rect(canvas.transform, "HUD").Stretch();
            BuildWallet();
            BuildDepth();
            BuildPipeline();
            BuildGoal();
            BuildBottom();
            shownCash = sim.S.cash;
        }

        void BuildWallet()
        {
            var card = UIKit.Card(root, "Wallet").TL(20, 20, 400, 140);
            UIKit.Label(card, "Caption", "CASH", 14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Semibold).rectTransform.TL(26, 16, 200, 20);
            cashText = UIKit.Label(card, "Cash", "$0.00", 46, Pal.Ink, TextAnchor.UpperLeft, UIKit.Bold);
            cashText.rectTransform.TL(22, 32, 370, 58);
            incomeText = UIKit.Label(card, "Income", "+$0/s", 18, Pal.Green, TextAnchor.UpperLeft, UIKit.Semibold);
            incomeText.rectTransform.TL(26, 96, 200, 26);
            contractText = UIKit.Label(card, "Contract", "", 15, Pal.Ink2, TextAnchor.UpperRight, UIKit.Regular);
            contractText.rectTransform.TL(180, 99, 196, 24);
            var lp = UIKit.Image(card, "LP", UIKit.Rounded, Pal.A(Pal.Gold, 0.16f), 14);
            lp.rectTransform.TL(270, 14, 106, 30);
            lpText = UIKit.Label(lp.rectTransform, "Text", "0 LP", 15, Pal.GoldInk, TextAnchor.MiddleCenter, UIKit.Semibold);
            lpText.rectTransform.Stretch();
        }

        void BuildDepth()
        {
            depthCard = UIKit.Card(root, "Depth").Place(new Vector2(0.5f, 1), new Vector2(-40, -20), new Vector2(680, 122));
            depthBg = depthCard.Find("Bg").GetComponent<Image>();
            mallText = UIKit.Label(depthCard, "Mall", "", 15, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Semibold);
            mallText.rectTransform.TL(24, 14, 440, 22);
            layerCount = UIKit.Label(depthCard, "Layers", "", 15, Pal.Ink2, TextAnchor.UpperRight, UIKit.Semibold);
            layerCount.rectTransform.TL(460, 14, 196, 22);
            stratumDot = UIKit.Dot(depthCard, "Dot", Color.white, 16);
            stratumDot.rectTransform.TL(24, 44, 16, 16);
            stratumText = UIKit.Label(depthCard, "Stratum", "", 24, Pal.Ink, TextAnchor.UpperLeft, UIKit.Bold);
            stratumText.rectTransform.TL(48, 34, 440, 32);
            depthText = UIKit.Label(depthCard, "Feet", "", 18, Pal.Ink, TextAnchor.UpperRight, UIKit.Semibold);
            depthText.rectTransform.TL(430, 38, 226, 28);
            depthBar = UIKit.ProgressBar(depthCard, "Bar", new Color(0, 0, 0, 0.07f), Pal.Accent);
            depthBar.Rt.TL(24, 74, 632, 12);
            nextText = UIKit.Label(depthCard, "Next", "", 14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular);
            nextText.rectTransform.TL(24, 92, 520, 22);
            contractBtn = UIKit.Button(depthCard, "Contract", "Sign next contract  →", Pal.Gold, Color.white, 17, () => OnContract?.Invoke());
            contractBtn.Rt.TL(420, 84, 236, 32);
            contractBtn.Rt.gameObject.SetActive(false);
        }

        void BuildPipeline()
        {
            var card = UIKit.Card(root, "Pipeline").TL(20, 176, 400, 474);
            UIKit.Label(card, "Title", "Fountain Pipeline", 18, Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold).rectTransform.TL(24, 16, 300, 26);
            string[] names = { "Dredge", "Dissolve", "Sort", "Sell" };
            string[] glyphs = { "D", "W", "S", "$" };
            for (int i = 0; i < 4; i++)
            {
                float y = 52 + i * 90;
                var row = new StageRow();
                var icon = UIKit.Dot(card, "Icon" + i, Pal.Stage[i], 40);
                icon.rectTransform.TL(22, y + 4, 40, 40);
                var g = UIKit.Label(icon.rectTransform, "G", glyphs[i], 19, Color.white, TextAnchor.MiddleCenter, UIKit.Bold);
                g.rectTransform.Stretch();
                row.Name = UIKit.Label(card, "Name" + i, names[i], 18, Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold);
                row.Name.rectTransform.TL(74, y, 180, 24);
                row.Rate = UIKit.Label(card, "Rate" + i, "", 15, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular);
                row.Rate.rectTransform.TL(74, y + 25, 200, 22);
                row.Amount = UIKit.Label(card, "Amount" + i, "", 17, Pal.Ink, TextAnchor.UpperRight, UIKit.Semibold);
                row.Amount.rectTransform.TL(190, y, 186, 24);
                row.Worth = UIKit.Label(card, "Worth" + i, "", 14, Pal.Ink2, TextAnchor.UpperRight, UIKit.Regular);
                row.Worth.rectTransform.TL(190, y + 25, 186, 22);
                row.Bar = UIKit.ProgressBar(card, "Bar" + i, new Color(0, 0, 0, 0.06f), Pal.A(Pal.Stage[i], 0.85f));
                row.Bar.Rt.TL(74, y + 56, (i == 1 || i == 2) ? 244 : 302, 7);
                var bn = UIKit.Image(card, "Bottleneck" + i, UIKit.Rounded, Pal.A(Pal.Red, 0.14f), 10);
                bn.rectTransform.TL(168, y + 3, 90, 20);
                UIKit.Label(bn.rectTransform, "T", "bottleneck", 12, Pal.Red, TextAnchor.MiddleCenter, UIKit.Semibold).rectTransform.Stretch();
                row.Bottleneck = bn.rectTransform;
                if (i == 1 || i == 2)
                {
                    int stage = i;
                    row.Dump = UIKit.Button(card, "Dump" + i, "Dump", Pal.A(Pal.Ink, 0.07f), Pal.Ink2, 12, () => { if (stage == 1) OnDumpRaw?.Invoke(); else OnDumpWashed?.Invoke(); });
                    row.Dump.Rt.TL(326, y + 49, 50, 21);
                    row.Dump.SetColors(Pal.A(Pal.Ink, 0.07f), Pal.A(Pal.Ink, 0.03f));
                }
                rows[i] = row;
            }
            sellBtn = UIKit.Button(card, "Sell", "SELL", Pal.Green, Color.white, 18, () => OnSell?.Invoke());
            sellBtn.Rt.TL(74, 404, 190, 42);
            sellBtn.SetColors(Pal.Green, Pal.Disabled);
            autoBtn = UIKit.Button(card, "Auto", "Auto: ON", Pal.A(Pal.Accent, 0.14f), Pal.AccentDark, 14, () => OnToggleAutoSell?.Invoke());
            autoBtn.Rt.TL(272, 407, 104, 36);
            sellHint = UIKit.Label(card, "SellHint", "", 13, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            sellHint.rectTransform.TL(74, 450, 300, 18);
        }

        void BuildGoal()
        {
            goalCard = UIKit.Card(root, "Goal").Place(new Vector2(0, 0), new Vector2(20, 20), new Vector2(400, 132));
            goalTitle = UIKit.Label(goalCard, "Caption", "NEXT GOAL", 13, Pal.Accent, TextAnchor.UpperLeft, UIKit.Semibold);
            goalTitle.rectTransform.TL(24, 14, 200, 18);
            goalText = UIKit.Label(goalCard, "Text", "", 19, Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold, true);
            goalText.rectTransform.TL(24, 34, 354, 50);
            goalHint = UIKit.Label(goalCard, "Hint", "", 14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            goalHint.rectTransform.TL(24, 84, 354, 40);
            var rw = UIKit.Image(goalCard, "RewardPill", UIKit.Rounded, Pal.A(Pal.Gold, 0.15f), 12);
            rw.rectTransform.TL(218, 12, 160, 24);
            goalReward = UIKit.Label(rw.rectTransform, "T", "", 13, Pal.GoldInk, TextAnchor.MiddleCenter, UIKit.Semibold);
            goalReward.rectTransform.Stretch();
        }

        void BuildBottom()
        {
            comboCard = UIKit.Card(root, "Combo", Pal.GlassStrong, 20).Place(new Vector2(0.5f, 0), new Vector2(-40, 22), new Vector2(300, 52));
            comboGroup = comboCard.gameObject.AddComponent<CanvasGroup>();
            comboGroup.blocksRaycasts = false;
            comboText = UIKit.Label(comboCard, "T", "Combo ×1.0", 18, Pal.Ink, TextAnchor.MiddleLeft, UIKit.Bold);
            comboText.rectTransform.TL(20, 6, 180, 26);
            comboBar = UIKit.ProgressBar(comboCard, "Bar", new Color(0, 0, 0, 0.08f), Pal.Pink);
            comboBar.Rt.TL(20, 34, 260, 8);
            buffRow = UIKit.Rect(root, "Buffs").Place(new Vector2(0.5f, 0), new Vector2(-40, 84), new Vector2(900, 40));
            var hl = buffRow.gameObject.AddComponent<HorizontalLayoutGroup>();
            hl.spacing = 10;
            hl.childAlignment = TextAnchor.MiddleCenter;
            hl.childControlWidth = false;
            hl.childControlHeight = false;
            eventCard = UIKit.Card(root, "Event", new Color(1f, 0.97f, 0.9f, 0.95f), 22).Place(new Vector2(0.5f, 0), new Vector2(-40, 134), new Vector2(560, 74));
            eventGroup = eventCard.gameObject.AddComponent<CanvasGroup>();
            eventGroup.blocksRaycasts = false;
            eventTitle = UIKit.Label(eventCard, "T", "", 20, Pal.Pink, TextAnchor.UpperLeft, UIKit.Bold);
            eventTitle.rectTransform.TL(22, 10, 516, 28);
            eventDesc = UIKit.Label(eventCard, "D", "", 14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular);
            eventDesc.rectTransform.TL(22, 38, 516, 20);
            eventBar = UIKit.ProgressBar(eventCard, "Bar", new Color(0, 0, 0, 0.07f), Pal.Pink);
            eventBar.Rt.TL(22, 60, 516, 6);
        }

        (RectTransform, Text, UIKit.Bar) NewBuffPill()
        {
            var rt = UIKit.Card(buffRow, "Buff", Pal.GlassStrong, 16, true, false);
            rt.sizeDelta = new Vector2(250, 38);
            var t = UIKit.Label(rt, "T", "", 15, Pal.Ink, TextAnchor.MiddleCenter, UIKit.Semibold);
            t.rectTransform.TL(0, 4, 250, 24);
            var bar = UIKit.ProgressBar(rt, "Bar", new Color(0, 0, 0, 0.06f), Pal.Gold);
            bar.Rt.TL(24, 29, 202, 4);
            return (rt, t, bar);
        }

        public RectTransform PipelineSellButton => sellBtn.Rt;

        public void Refresh(float dt)
        {
            // cash counts up smoothly
            double target = sim.S.cash;
            if (Math.Abs(target - shownCash) < Math.Max(0.005, Math.Abs(target) * 1e-6)) shownCash = target;
            else shownCash += (target - shownCash) * Math.Min(1, dt * 12);
            cashText.text = Fmt.Money(shownCash);

            float c01 = (float)sim.Combo01;
            comboGroup.alpha = Mathf.MoveTowards(comboGroup.alpha, c01 > 0.02f ? 1 : 0, dt * 4);
            comboBar.Set(c01);
            comboText.text = $"Combo ×{sim.ComboMult:0.0}";

            slowTimer -= dt;
            if (slowTimer > 0) return;
            slowTimer = 0.12f;
            RefreshSlow();
        }

        void RefreshSlow()
        {
            var mall = sim.Mall;
            double inc = Math.Max(sim.EarnRate, 0);
            incomeText.text = "+" + Fmt.Money(inc) + "/s";
            contractText.text = sim.Contract > 1.0001 ? "Contract ×" + Fmt.Num(sim.Contract) : "Value ×" + Fmt.Num(sim.ValueMult);
            lpText.text = Fmt.Num(sim.S.luckyPennies) + " LP";

            // depth
            string remodel = sim.InRemodel ? $" · Remodel {sim.Remodel}" : "";
            mallText.text = $"{mall.Name.ToUpperInvariant()}{remodel}";
            int s = sim.Stratum;
            var st = mall.Strata[s];
            layerCount.text = sim.MallCleared ? "CLEARED" : $"Layer {s + 1} of {mall.Strata.Length}";
            stratumText.text = sim.MallCleared ? "Bare Concrete!" : st.Name;
            stratumDot.color = MeshKit.Hex(st.Color);
            depthText.text = $"{Fmt.Feet(sim.DepthFeet)} / {Fmt.Feet(mall.DepthFeet)}";
            depthBar.Set((float)sim.DepthFrac);
            depthBar.Fill.color = sim.MallCleared ? Pal.Gold : Color.Lerp(MeshKit.Hex(st.Color), Pal.Accent, 0.35f);
            if (sim.MallCleared)
            {
                nextText.text = $"You found {mall.TreasureName}!";
                contractBtn.Rt.gameObject.SetActive(true);
                depthBg.color = new Color(1f, 0.97f, 0.86f, 0.94f);
            }
            else
            {
                contractBtn.Rt.gameObject.SetActive(false);
                depthBg.color = Pal.Glass;
                nextText.text = s + 1 < mall.Strata.Length
                    ? $"Next: {mall.Strata[s + 1].Name} at {Fmt.Feet(mall.Strata[s + 1].StartFrac * mall.DepthFeet)}   ·   {Fmt.Pct(sim.StratumProgress)} through this layer"
                    : $"Bare concrete at {Fmt.Feet(mall.DepthFeet)}   ·   {Fmt.Pct(sim.StratumProgress)} through the last layer";
            }

            // pipeline
            bool loose = sim.CurStratum.Loose && !sim.MallCleared;
            double dig = sim.DigRateNow, wash = sim.WashRateNow, sort = sim.SortRateNow;
            rows[0].Rate.text = dig > 0 ? Fmt.Rate(dig) + " + clicks" : "click the crust!";
            rows[0].Amount.text = sim.MallCleared ? "done" : Fmt.Num(sim.ItemsLeft) + " left";
            rows[0].Worth.text = $"{Fmt.Num(sim.ClickPowerNow * sim.ComboMult)} per click";
            rows[0].Bar.Set((float)sim.StratumProgress);

            rows[1].Rate.text = wash > 0 ? Fmt.Rate(wash) : (loose ? "not needed yet" : "no washers!");
            rows[1].Amount.text = Fmt.Num(sim.S.hopperCount) + " raw";
            rows[1].Worth.text = sim.S.hopperValue > 0 ? "dump: " + Fmt.Money(sim.HopperWorth) : "";
            rows[1].Bar.Set(Buffer01(sim.S.hopperCount, wash));

            rows[2].Rate.text = sort > 0 ? Fmt.Rate(sort) : (loose ? "not needed yet" : "no sorters!");
            rows[2].Amount.text = Fmt.Num(sim.S.trayCount) + " washed";
            rows[2].Worth.text = sim.S.trayValue > 0 ? "dump: " + Fmt.Money(sim.TrayWorth) : "";
            rows[2].Bar.Set(Buffer01(sim.S.trayCount, sort));

            rows[3].Rate.text = sim.AutoSell ? "auto-selling" : "sell by hand";
            rows[3].Amount.text = Fmt.Num(sim.S.pocketCount) + " sorted";
            rows[3].Worth.text = Fmt.Money(sim.PocketWorth);
            rows[3].Bar.Set(sim.PocketWorth > 0 ? 1 : 0);
            rows[1].Dump.Interactable = sim.S.hopperValue > 0;
            rows[2].Dump.Interactable = sim.S.trayValue > 0;

            // bottleneck: the slowest processing stage (only once the syrup layer is reached)
            int bn = -1;
            if (!loose && !sim.MallCleared)
            {
                double digEff = dig + sim.ClickPowerNow;
                if (wash <= 0) bn = 1;
                else if (sort <= 0) bn = 2;
                else if (wash < sort * 0.8 && wash < digEff * 0.8) bn = 1;
                else if (sort < wash * 0.8) bn = 2;
                else if (digEff < wash * 0.5 && sim.S.hopperCount < wash * 5) bn = 0;
            }
            for (int i = 0; i < 4; i++) rows[i].Bottleneck.gameObject.SetActive(i == bn);

            sellBtn.Label.text = sim.PocketWorth > 0 ? "SELL  " + Fmt.Money(sim.PocketWorth) : "SELL";
            sellBtn.Interactable = sim.S.pocketValue > 0;
            autoBtn.Rt.gameObject.SetActive(sim.AutoSellOwned);
            autoBtn.Label.text = sim.S.autoSellOn ? "Auto: ON" : "Auto: OFF";
            sellHint.text = sim.AutoSellOwned ? "" : "Space bar or click the red vending machine";

            // goal
            var obj = sim.CurrentObjective;
            if (obj != null)
            {
                goalTitle.text = "NEXT GOAL";
                goalText.text = obj.Text;
                goalHint.text = obj.Hint;
                double reward = sim.ObjectiveReward(obj);
                goalReward.transform.parent.gameObject.SetActive(reward > 0);
                goalReward.text = "Reward " + Fmt.Money(reward);
            }
            else
            {
                goalTitle.text = "KEEP DIGGING";
                goalText.text = sim.DynamicGoal;
                goalHint.text = "Every layer is richer than the last.";
                goalReward.transform.parent.gameObject.SetActive(false);
            }

            // buffs
            int n = sim.Buffs.Count;
            while (buffPills.Count < n) buffPills.Add(NewBuffPill());
            for (int i = 0; i < buffPills.Count; i++)
            {
                bool on = i < n;
                buffPills[i].rt.gameObject.SetActive(on);
                if (!on) continue;
                var b = sim.Buffs[i];
                buffPills[i].txt.text = $"{b.Name} ×{Fmt.Num(b.Mult)}  ·  {Math.Ceiling(b.Remaining)}s";
                buffPills[i].bar.Set((float)(b.Remaining / b.Duration));
            }

            // mall event
            eventGroup.alpha = sim.EventActive ? 1 : 0;
            if (sim.EventActive)
            {
                eventTitle.text = sim.Mall.Event.Name + "!";
                eventDesc.text = sim.Mall.Event.Desc;
                eventBar.Set((float)(sim.EventRemaining / Balance.EventDuration));
            }
        }

        static float Buffer01(double count, double rate)
        {
            if (count <= 0) return 0;
            if (rate <= 0) return 1;
            return Mathf.Clamp01((float)(count / (rate * 30)));
        }
    }
}
