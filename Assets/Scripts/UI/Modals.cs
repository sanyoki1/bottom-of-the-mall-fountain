// Modal screens: Wish Journal (wishes, relic museum, achievements, stats), Settings,
// "while you were away", contract signing (prestige), mall intros and the ending.
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WishExtractor.Core;

namespace WishExtractor.UI
{
    public sealed class Modals
    {
        Sim sim;
        RectTransform root, dim, card;
        CanvasGroup group;
        Action onClose;
        public bool IsOpen => root != null && root.gameObject.activeSelf;
        public string OpenName { get; private set; } = "";
        public Action<float, float> OnVolumes;       // music, sfx
        public Action OnSettingsChanged;
        public Action OnResetSave;
        public Action OnQuit;
        public Action OnSign;
        float appear;

        public void Build(Canvas canvas, Sim s)
        {
            sim = s;
            root = UIKit.Rect(canvas.transform, "Modals").Stretch();
            group = root.gameObject.AddComponent<CanvasGroup>();
            dim = UIKit.Image(root, "Dim", null, new Color(0.06f, 0.07f, 0.1f, 0.45f)).rectTransform;
            dim.Stretch();
            dim.GetComponent<Image>().raycastTarget = true;
            var closeBtn = dim.gameObject.AddComponent<Button>();
            closeBtn.transition = Selectable.Transition.None;
            closeBtn.onClick.AddListener(() => { if (OpenName != "contract" && OpenName != "intro") Close(); });
            root.gameObject.SetActive(false);
        }

        RectTransform Open(string name, float w, float h, Action closed = null)
        {
            Close();
            OpenName = name;
            onClose = closed;
            root.gameObject.SetActive(true);
            card = UIKit.Card(root, "Card", new Color(1, 1, 1, 0.97f), 28);
            card.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            appear = 0;
            group.alpha = 0;
            return card;
        }

        public void Close()
        {
            if (card != null) UnityEngine.Object.Destroy(card.gameObject);
            card = null;
            if (root != null) root.gameObject.SetActive(false);
            var cb = onClose;
            onClose = null;
            OpenName = "";
            cb?.Invoke();
        }

        public void Update(float dt)
        {
            if (!IsOpen) return;
            appear = Mathf.Min(1, appear + dt * 6);
            group.alpha = appear;
            if (card != null) card.localScale = Vector3.one * Mathf.Lerp(0.94f, 1f, 1 - (1 - appear) * (1 - appear));
        }

        UIKit.Btn CloseX(RectTransform c, float w)
        {
            var b = UIKit.Button(c, "Close", "✕", new Color(0, 0, 0, 0.06f), Pal.Ink2, 18, Close, 999, UIKit.Symbol);
            b.Rt.TL(w - 58, 18, 40, 40);
            return b;
        }

        Text Title(RectTransform c, string text, float w, string kicker = null)
        {
            if (kicker != null)
            {
                var k = UIKit.Label(c, "Kicker", kicker, 13, Pal.Accent, TextAnchor.UpperLeft, UIKit.Semibold);
                k.rectTransform.TL(32, 24, w - 100, 20);
            }
            var t = UIKit.Label(c, "Title", text, 30, Pal.Ink, TextAnchor.UpperLeft, UIKit.Bold);
            t.rectTransform.TL(32, kicker != null ? 44 : 28, w - 100, 42);
            return t;
        }

        // ───────────────────────────── Journal ─────────────────────────────

        int journalTab;

        public void OpenJournal(int tab = -1)
        {
            if (tab >= 0) journalTab = tab;
            const float w = 1040, h = 820;
            var c = Open("journal", w, h);
            Title(c, "Wish Journal", w, "COLLECTIONS");
            CloseX(c, w);
            string[] tabs = { "Wishes", "Relic Museum", "Achievements", "Stats" };
            var seg = UIKit.Image(c, "Tabs", UIKit.Rounded, new Color(0, 0, 0, 0.06f), 16);
            seg.rectTransform.TL(32, 96, 620, 40);
            for (int i = 0; i < tabs.Length; i++)
            {
                int ti = i;
                bool on = i == journalTab;
                var b = UIKit.Button(seg.rectTransform, tabs[i], tabs[i], on ? Color.white : Color.clear, on ? Pal.Ink : Pal.Ink2, 15, () => OpenJournal(ti), 14);
                b.Rt.anchorMin = new Vector2(i / 4f, 0);
                b.Rt.anchorMax = new Vector2((i + 1) / 4f, 1);
                b.Rt.offsetMin = new Vector2(3, 3);
                b.Rt.offsetMax = new Vector2(-3, -3);
            }
            var summary = UIKit.Label(c, "Summary", "", 14, Pal.Ink2, TextAnchor.MiddleRight, UIKit.Semibold);
            summary.rectTransform.TL(670, 96, 338, 40);
            var list = UIKit.ScrollList(c, "List", out _, 8, 4);
            list.parent.GetComponent<RectTransform>().Stretch(24, 150, 24, 24);
            switch (journalTab)
            {
                case 0: FillWishes(list, summary); break;
                case 1: FillRelics(list, summary); break;
                case 2: FillAchievements(list, summary); break;
                default: FillStats(list, summary); break;
            }
        }

        RectTransform Section(RectTransform list, string title, string right, Color color)
        {
            var rt = UIKit.Rect(list, "Section");
            UIKit.Layout(rt.gameObject, 44);
            var d = UIKit.Dot(rt, "Dot", color, 12);
            d.rectTransform.TL(8, 20, 12, 12);
            UIKit.Label(rt, "T", title, 18, Pal.Ink, TextAnchor.MiddleLeft, UIKit.Bold).rectTransform.TL(28, 10, 600, 32);
            UIKit.Label(rt, "R", right, 14, Pal.Ink2, TextAnchor.MiddleRight, UIKit.Semibold).rectTransform.TL(560, 10, 420, 32);
            return rt;
        }

        RectTransform Entry(RectTransform list, float height, bool found)
        {
            var rt = UIKit.Card(list, "Entry", found ? new Color(1, 1, 1, 1f) : new Color(0.92f, 0.92f, 0.94f, 1f), 16, false);
            UIKit.Layout(rt.gameObject, height);
            return rt;
        }

        void FillWishes(RectTransform list, Text summary)
        {
            summary.text = $"{sim.UniqueWishCount} / {Content.TotalWishes} recorded  ·  +{Fmt.Num(sim.UniqueWishCount * Balance.WishJournalBonus * 100)}% value";
            for (int m = 0; m < Content.Malls.Length; m++)
            {
                var mall = Content.Malls[m];
                bool visited = sim.S.mallIndex >= m || sim.S.maxMallCleared >= m - 1;
                Section(list, visited ? mall.Name : "??? (future mall)", $"{sim.WishesFoundInMall(m)} / {mall.Wishes.Length}", View.MeshKit.Hex(mall.Theme.Accent));
                if (!visited) continue;
                foreach (var w in mall.Wishes)
                {
                    bool found = sim.WishFound(w.Id);
                    var rt = Entry(list, 50, found);
                    var dot = UIKit.Dot(rt, "R", found ? Pal.Rarity[(int)w.Rarity] : Pal.Ink3, 12);
                    dot.rectTransform.TL(16, 19, 12, 12);
                    var t = UIKit.Label(rt, "Q", found ? "“" + w.Text + "”" : "“ ? ? ? ”", 16, found ? Pal.Ink : Pal.Ink3, TextAnchor.MiddleLeft, UIKit.Semibold, true);
                    t.rectTransform.TL(38, 3, 760, 44);
                    if (found) t.fontStyle = FontStyle.Italic;
                    UIKit.Label(rt, "Rarity", View.RarityColors.Names[(int)w.Rarity], 13, Pal.Rarity[(int)w.Rarity], TextAnchor.MiddleRight, UIKit.Semibold).rectTransform.TL(810, 3, 160, 44);
                }
            }
        }

        void FillRelics(RectTransform list, Text summary)
        {
            summary.text = $"{sim.RelicUniqueCount} / {Content.TotalRelics} relics  ·  {sim.SetsComplete} complete sets (+{Fmt.Num(sim.SetsComplete * Balance.RelicSetBonus * 100)}%)";
            for (int m = 0; m < Content.Malls.Length; m++)
            {
                var mall = Content.Malls[m];
                bool visited = sim.S.mallIndex >= m || sim.S.maxMallCleared >= m - 1;
                bool done = sim.RelicSetDone(m);
                bool treasure = sim.S.treasures.Contains(mall.Id);
                Section(list, visited ? mall.Name : "??? (future mall)", $"{sim.RelicsFoundInMall(m)} / {mall.Relics.Length}{(done ? "  ·  SET COMPLETE" : "")}", View.MeshKit.Hex(mall.Theme.Accent));
                if (!visited) continue;
                var tr = Entry(list, 64, treasure);
                UIKit.Label(tr, "Crown", "♛", 22, treasure ? Pal.Gold : Pal.Ink3, TextAnchor.MiddleCenter, UIKit.Symbol).rectTransform.TL(8, 10, 36, 44);
                UIKit.Label(tr, "Name", treasure ? mall.TreasureName : "Bottom treasure: ???", 16, treasure ? Pal.Ink : Pal.Ink3, TextAnchor.UpperLeft, UIKit.Bold).rectTransform.TL(50, 8, 700, 24);
                UIKit.Label(tr, "Desc", treasure ? mall.TreasureDesc : $"Waiting at {Fmt.Feet(mall.DepthFeet)} under {mall.Name}.", 13, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true).rectTransform.TL(50, 32, 900, 30);
                foreach (var r in mall.Relics)
                {
                    int count = sim.RelicCount(r.Id);
                    bool found = count > 0;
                    var rt = Entry(list, 64, found);
                    var dot = UIKit.Dot(rt, "R", found ? Pal.Rarity[(int)r.Rarity] : Pal.Ink3, 30);
                    dot.rectTransform.TL(12, 17, 30, 30);
                    UIKit.Label(rt, "Name", found ? r.Name : "???", 16, found ? Pal.Ink : Pal.Ink3, TextAnchor.UpperLeft, UIKit.Semibold).rectTransform.TL(54, 8, 640, 24);
                    UIKit.Label(rt, "Desc", found ? r.Desc : View.RarityColors.Names[(int)r.Rarity] + " relic, still buried somewhere in the crust.", 13, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true).rectTransform.TL(54, 32, 760, 30);
                    UIKit.Label(rt, "Count", found ? $"× {count}" : View.RarityColors.Names[(int)r.Rarity], 14, found ? Pal.Ink2 : Pal.Rarity[(int)r.Rarity], TextAnchor.MiddleRight, UIKit.Semibold).rectTransform.TL(820, 10, 150, 44);
                }
            }
        }

        void FillAchievements(RectTransform list, Text summary)
        {
            summary.text = $"{sim.AchievementCount} / {Content.Achievements.Length} unlocked  ·  +{Fmt.Num(sim.AchievementCount * Balance.AchievementBonus * 100)}% value";
            var done = new List<AchievementDef>();
            var todo = new List<AchievementDef>();
            foreach (var a in Content.Achievements) (sim.AchievementDone(a.Index) ? done : todo).Add(a);
            foreach (var group in new[] { done, todo })
                foreach (var a in group)
                {
                    bool got = sim.AchievementDone(a.Index);
                    var rt = Entry(list, 58, got);
                    UIKit.Label(rt, "Icon", got ? "★" : "☆", 22, got ? Pal.Gold : Pal.Ink3, TextAnchor.MiddleCenter, UIKit.Symbol).rectTransform.TL(8, 8, 40, 42);
                    UIKit.Label(rt, "Name", a.Name, 16, got ? Pal.Ink : Pal.Ink2, TextAnchor.UpperLeft, UIKit.Semibold).rectTransform.TL(54, 8, 600, 22);
                    UIKit.Label(rt, "Desc", a.Desc, 13, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular).rectTransform.TL(54, 30, 760, 22);
                    UIKit.Label(rt, "Bonus", got ? $"+{Fmt.Num(Balance.AchievementBonus * 100)}% value" : "", 13, Pal.Green, TextAnchor.MiddleRight, UIKit.Semibold).rectTransform.TL(820, 8, 150, 42);
                }
        }

        void FillStats(RectTransform list, Text summary)
        {
            var S = sim.S;
            summary.text = $"Played {Fmt.Time(S.playTime)}";
            void Stat(string name, string value)
            {
                var rt = Entry(list, 44, true);
                UIKit.Label(rt, "N", name, 15, Pal.Ink2, TextAnchor.MiddleLeft, UIKit.Semibold).rectTransform.TL(20, 4, 500, 36);
                UIKit.Label(rt, "V", value, 16, Pal.Ink, TextAnchor.MiddleRight, UIKit.Bold).rectTransform.TL(500, 4, 460, 36);
            }
            Section(list, "This contract", sim.Mall.Name, Pal.Accent);
            Stat("Time in this mall", Fmt.Time(S.runTime));
            Stat("Earned in this mall", Fmt.Money(S.runCash));
            Stat("Depth", $"{Fmt.Feet(sim.DepthFeet)} of {Fmt.Feet(sim.Mall.DepthFeet)}");
            Stat("Machines owned", Fmt.Int(sim.TotalMachines));
            Stat("Upgrades owned", Fmt.Int(sim.UpgradesOwnedCount));
            Stat("Dig / Wash / Sort", $"{Fmt.Rate(sim.DigRateNow)}  ·  {Fmt.Rate(sim.WashRateNow)}  ·  {Fmt.Rate(sim.SortRateNow)}");
            Section(list, "All time", "", Pal.Gold);
            Stat("Total play time", Fmt.Time(S.playTime));
            Stat("Lifetime earnings", Fmt.Money(S.lifetimeCash));
            Stat("Items dug", Fmt.Num(S.totalDug));
            Stat("Clicks", Fmt.Int(S.clicks));
            Stat("Best combo", $"×{S.bestCombo:0.0}");
            Stat("Wishes caught / compressed / seen", $"{Fmt.Int(S.wishesCaught)} / {Fmt.Int(S.wishesCompressed)} / {Fmt.Int(S.wishesSeen)}");
            Stat("Relics found", Fmt.Int(S.relicsFound));
            Stat("Golden pennies clicked", Fmt.Int(S.goldenClicked));
            Stat("Mall rats caught", Fmt.Int(S.ratsCaught));
            Stat("Malls cleared", $"{S.maxMallCleared + 1} of {Content.Malls.Length}" + (S.remodelsDone > 0 ? $"  ·  {S.remodelsDone} remodels" : ""));
            Stat("Biggest single sale", Fmt.Money(S.biggestSale));
            Stat("Lucky Pennies earned", Fmt.Num(S.lifetimeLP));
            Section(list, "Permanent value bonuses", "", Pal.Green);
            Stat("Achievements", $"+{Fmt.Num(sim.AchievementCount * Balance.AchievementBonus * 100)}%");
            Stat("Wish Journal", $"+{Fmt.Num(sim.UniqueWishCount * Balance.WishJournalBonus * 100)}%");
            Stat("Relic sets", $"+{Fmt.Num(sim.SetsComplete * Balance.RelicSetBonus * 100)}%");
            Stat("Bottom treasures", $"+{Fmt.Num(sim.TreasureCount * Balance.TreasureBonus * 100)}%");
        }

        // ───────────────────────────── Settings ─────────────────────────────

        public void OpenSettings()
        {
            const float w = 720, h = 760;
            var c = Open("settings", w, h);
            Title(c, "Settings", w, "WISH EXTRACTOR");
            CloseX(c, w);
            float y = 110;
            Slider(c, "Music", sim.S.musicVol, y, v => { sim.S.musicVol = v; OnVolumes?.Invoke(sim.S.musicVol, sim.S.sfxVol); });
            y += 64;
            Slider(c, "Sound effects", sim.S.sfxVol, y, v => { sim.S.sfxVol = v; OnVolumes?.Invoke(sim.S.musicVol, sim.S.sfxVol); });
            y += 72;
            Choice(c, "Numbers", new[] { "1.23M", "1.23e6" }, sim.S.notation, y, i => { sim.S.notation = i; Fmt.Notation = i; OnSettingsChanged?.Invoke(); });
            y += 62;
            Choice(c, "Graphics", new[] { "Low", "Medium", "High" }, sim.S.quality, y, i => { sim.S.quality = i; OnSettingsChanged?.Invoke(); });
            y += 62;
            Choice(c, "Screen shake", new[] { "Off", "On" }, sim.S.screenShake ? 1 : 0, y, i => { sim.S.screenShake = i == 1; OnSettingsChanged?.Invoke(); });
            y += 72;
            var help = UIKit.Label(c, "Help",
                "<b>Controls</b>\nLeft click: dig, catch wishes, click golden pennies & rats, whack machines\n" +
                "Hold left click on the crust: keep digging   ·   Space: sell pocket\n" +
                "Right-drag or Q/E: orbit   ·   Scroll: zoom   ·   WASD / middle-drag: pan   ·   R: reset view\n" +
                "1-4: shop tabs   ·   B: buy amount   ·   Tab: hide shop   ·   J: journal   ·   Esc: settings",
                14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            help.rectTransform.TL(32, y, w - 64, 120);
            y += 138;
            UIKit.Btn reset = null;
            reset = UIKit.Button(c, "Reset", "Reset save…", new Color(1f, 0.27f, 0.23f, 0.12f), Pal.Red, 15, () =>
            {
                if (reset.Label.text == "Reset save…") { reset.Label.text = "Tap again to erase EVERYTHING"; return; }
                OnResetSave?.Invoke();
                Close();
            });
            reset.Rt.TL(32, y, 300, 44);
            var quit = UIKit.Button(c, "Quit", "Save & quit", Pal.Ink, Color.white, 15, () => OnQuit?.Invoke());
            quit.Rt.TL(w - 32 - 200, y, 200, 44);
        }

        void Slider(RectTransform c, string label, float value, float y, Action<float> changed)
        {
            UIKit.Label(c, label, label, 16, Pal.Ink, TextAnchor.MiddleLeft, UIKit.Semibold).rectTransform.TL(32, y, 200, 40);
            var rt = UIKit.Rect(c, label + " Slider").TL(240, y + 8, 420, 24);
            var bg = UIKit.Image(rt, "Bg", UIKit.Rounded, new Color(0, 0, 0, 0.08f), 12);
            bg.rectTransform.Stretch(0, 6, 0, 6);
            var fillArea = UIKit.Rect(rt, "Fill Area").Stretch(0, 6, 0, 6);
            var fill = UIKit.Image(fillArea, "Fill", UIKit.Rounded, Pal.Accent, 12);
            fill.rectTransform.Stretch();
            var handleArea = UIKit.Rect(rt, "Handle Area").Stretch(12, 0, 12, 0);
            var handle = UIKit.Image(handleArea, "Handle", UIKit.Circle, Color.white);
            handle.rectTransform.sizeDelta = new Vector2(26, 26);
            handle.raycastTarget = true;
            var sh = handle.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.25f);
            var s = rt.gameObject.AddComponent<UnityEngine.UI.Slider>();
            s.fillRect = fill.rectTransform;
            s.handleRect = handle.rectTransform;
            s.targetGraphic = handle;
            s.direction = UnityEngine.UI.Slider.Direction.LeftToRight;
            s.minValue = 0;
            s.maxValue = 1;
            s.value = value;
            s.onValueChanged.AddListener(v => changed(v));
            var hit = rt.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
        }

        void Choice(RectTransform c, string label, string[] options, int selected, float y, Action<int> changed)
        {
            UIKit.Label(c, label, label, 16, Pal.Ink, TextAnchor.MiddleLeft, UIKit.Semibold).rectTransform.TL(32, y, 200, 40);
            var seg = UIKit.Image(c, label + " Seg", UIKit.Rounded, new Color(0, 0, 0, 0.06f), 14);
            seg.rectTransform.TL(240, y, 420, 40);
            var btns = new List<UIKit.Btn>();
            for (int i = 0; i < options.Length; i++)
            {
                int oi = i;
                var b = UIKit.Button(seg.rectTransform, options[i], options[i], Color.clear, Pal.Ink2, 15, null, 12);
                b.Rt.anchorMin = new Vector2(i / (float)options.Length, 0);
                b.Rt.anchorMax = new Vector2((i + 1) / (float)options.Length, 1);
                b.Rt.offsetMin = new Vector2(3, 3);
                b.Rt.offsetMax = new Vector2(-3, -3);
                btns.Add(b);
                b.Button.onClick.AddListener(() =>
                {
                    for (int k = 0; k < btns.Count; k++) { btns[k].SetColors(k == oi ? Color.white : Color.clear, Color.clear); btns[k].Label.color = k == oi ? Pal.Ink : Pal.Ink2; }
                    changed(oi);
                });
            }
            for (int k = 0; k < btns.Count; k++) { btns[k].SetColors(k == selected ? Color.white : Color.clear, Color.clear); btns[k].Label.color = k == selected ? Pal.Ink : Pal.Ink2; }
        }

        // ───────────────────────────── Offline / contract / intro / ending ─────────────────────────────

        public void OpenOffline(OfflineReport rep)
        {
            const float w = 620, h = 400;
            var c = Open("offline", w, h);
            Title(c, "While you were away…", w, $"GONE FOR {Fmt.Time(rep.Seconds).ToUpperInvariant()}");
            string pocket = rep.Pocket > 0 ? $"\n<b>{Fmt.Money(rep.Pocket)}</b> of loot waiting in your pocket. Sell it at the vending machine!" : "";
            var body = UIKit.Label(c, "Body",
                $"The crews kept digging at {Fmt.Pct(sim.OfflineEfficiency)} efficiency.\n\n" +
                $"<b>{Fmt.Money(rep.Cash)}</b> earned{pocket}\n{Fmt.Num(rep.Dug)} items dredged\n{rep.Relics} relics found\n{rep.Wishes} wishes compressed into bricks",
                17, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            body.rectTransform.TL(32, 100, w - 64, 200);
            var ok = UIKit.Button(c, "OK", "Back to digging", Pal.Accent, Color.white, 17, Close);
            ok.Rt.TL(w - 32 - 240, h - 32 - 50, 240, 50);
        }

        public void OpenContract()
        {
            const float w = 760, h = 520;
            var mall = sim.Mall;
            bool final = sim.IsFinalMall || sim.InRemodel;
            var next = Content.Malls[(sim.S.mallIndex + 1) % Content.Malls.Length];
            var c = Open("contract", w, h);
            Title(c, "Bare concrete!", w, "CONTRACT COMPLETE");
            CloseX(c, w);
            var tr = UIKit.Card(c, "Treasure", new Color(1f, 0.96f, 0.84f, 1f), 20, false);
            tr.TL(32, 100, w - 64, 120);
            UIKit.Label(tr, "Crown", "♛", 40, Pal.Gold, TextAnchor.MiddleCenter, UIKit.Symbol).rectTransform.TL(16, 20, 64, 80);
            UIKit.Label(tr, "Name", mall.TreasureName, 22, Pal.Ink, TextAnchor.UpperLeft, UIKit.Bold).rectTransform.TL(92, 18, w - 180, 30);
            UIKit.Label(tr, "Desc", mall.TreasureDesc, 15, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true).rectTransform.TL(92, 50, w - 180, 64);
            string nextText = final
                ? "Sign a Remodel contract: every mall again, bigger, pricier and richer."
                : $"Next contract: <b>{next.Name}</b>\n{next.Tagline}";
            var body = UIKit.Label(c, "Body",
                $"Head Office pays <b>{Fmt.Num(sim.PrestigeReward)} Lucky Pennies</b> and a permanent +{Fmt.Num(Balance.TreasureBonus * 100)}% value bonus for the treasure.\n\n{nextText}\n\n" +
                "You keep Lucky Pennies, Head Office perks, the Wish Journal, relics and achievements. Cash, machines and upgrades reset.",
                16, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            body.rectTransform.TL(32, 236, w - 64, 180);
            var later = UIKit.Button(c, "Later", "Not yet", new Color(0, 0, 0, 0.06f), Pal.Ink2, 16, Close);
            later.Rt.TL(32, h - 32 - 50, 160, 50);
            var sign = UIKit.Button(c, "Sign", "Sign the contract  →", Pal.Gold, Color.white, 18, () => { Close(); OnSign?.Invoke(); });
            sign.Rt.TL(w - 32 - 300, h - 32 - 50, 300, 50);
        }

        public void OpenIntro(bool firstTime)
        {
            const float w = 780, h = 560;
            var mall = sim.Mall;
            var c = Open("intro", w, h);
            Title(c, mall.Name, w, firstTime ? "WISH EXTRACTOR: MALL FOUNTAIN TYCOON" : (sim.InRemodel ? $"REMODEL {sim.Remodel}  ·  NEW CONTRACT" : "NEW CONTRACT SIGNED"));
            var tag = UIKit.Label(c, "Tag", mall.Tagline, 17, Pal.Accent, TextAnchor.UpperLeft, UIKit.Semibold, true);
            tag.rectTransform.TL(32, 92, w - 64, 26);
            string body = mall.Intro + "\n\n";
            if (firstTime)
                body += "<b>Dredge</b> the crust by clicking it.  <b>Dissolve</b> the syrup with washing machines.  <b>Sort</b> the treasure from the zinc.  <b>Sell</b> it at the greasy vending machine.  " +
                        "Then buy bigger machines until the whole thing runs itself.\n\nCatch the glowing <b>True Wishes</b> that float up, click <b>Golden Pennies</b>, grab the <b>mall rat</b>, and dig all the way to bare concrete.";
            else
                body += $"New here: {NewInThisMall()}\n\nThe fountain is {Fmt.Feet(mall.DepthFeet)} deep. {mall.TreasureName} is waiting at the bottom.";
            var b = UIKit.Label(c, "Body", body, 17, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            b.rectTransform.TL(32, 132, w - 64, 320);
            var go = UIKit.Button(c, "Go", firstTime ? "Start digging" : "Let's dig", Pal.Accent, Color.white, 18, Close);
            go.Rt.TL(w - 32 - 240, h - 32 - 52, 240, 52);
        }

        string NewInThisMall()
        {
            int m = sim.MallDefIndex;
            var news = new List<string>();
            foreach (var md in Content.Machines)
                if (md.UnlockMall == m && m > 0) news.Add(md.Name);
            news.Add($"{sim.Mall.Wishes.Length} new wishes and {sim.Mall.Relics.Length} new relics");
            news.Add($"the '{sim.Mall.Event.Name}' event");
            news.Add("10 themed upgrades you can only buy here");
            return string.Join(", ", news) + ".";
        }

        public void OpenEnding()
        {
            const float w = 820, h = 600;
            var c = Open("ending", w, h);
            Title(c, "The First Wish", w, "THE BOTTOM OF EVERY FOUNTAIN");
            var b = UIKit.Label(c, "Body",
                "At the very bottom of Eternity Plaza, under the mall, under the pirate gold, under the Roman well, you find it: a tiny coin that hums.\n\n" +
                "Whoever tossed it wished for “more wishes.” Every penny in every mall fountain since has been a small echo of that first one.\n\n" +
                "You dug them all up. You caught the ones you could. The rest were pressed into purple bricks and sold to billionaires. Honestly, that is the most mall thing that could have happened.\n\n" +
                "<b>Thanks for playing Wish Extractor.</b>  The malls will keep hiring: Remodel contracts run every fountain again, bigger and richer, for as long as you like.",
                17, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            b.rectTransform.TL(32, 100, w - 64, 400);
            var go = UIKit.Button(c, "Go", "Keep digging", Pal.Gold, Color.white, 18, Close);
            go.Rt.TL(w - 32 - 240, h - 32 - 52, 240, 52);
        }
    }
}
