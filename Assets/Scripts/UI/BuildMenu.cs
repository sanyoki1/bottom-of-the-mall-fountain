// The build catalogue (Tab in build mode): every researched buildable by category, with price,
// power and a one-line description. Click one to hold it as the build ghost.
using System;
using UnityEngine;
using UnityEngine.UI;
using WishExtractor.Core;

namespace WishExtractor.UI
{
    public sealed class BuildMenu
    {
        Sim sim;
        RectTransform root, panel;
        public bool IsOpen => root != null && root.gameObject.activeSelf;
        public Action<BuildDef> OnPick;

        public void Build(Canvas canvas, Sim s)
        {
            sim = s;
            root = UIKit.Rect(canvas.transform, "Build Menu").Stretch();
            var dim = UIKit.Image(root, "Dim", null, new Color(0.03f, 0.05f, 0.06f, 0.55f));
            dim.rectTransform.Stretch();
            dim.raycastTarget = true;
            root.gameObject.SetActive(false);
        }

        public void Open()
        {
            root.gameObject.SetActive(true);
            if (panel != null) UnityEngine.Object.Destroy(panel.gameObject);
            const float w = 1500, h = 760;
            panel = UIKit.Card(root, "Panel", new Color(0.09f, 0.13f, 0.14f, 0.98f), 22, false);
            panel.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(w, h));
            var title = UIKit.Label(panel, "Title", "BUILD CATALOGUE", 26, new Color(0.42f, 1f, 0.62f), TextAnchor.UpperLeft, UIKit.Mono);
            title.rectTransform.TL(32, 24, 600, 36);
            var power = UIKit.Label(panel, "Power", $"POWER {sim.PowerGen:0.#} / {sim.PowerUse:0.#} kW   ·   CASH {Fmt.Money(sim.S.cash)}", 18, new Color(0.55f, 0.66f, 0.64f), TextAnchor.UpperRight, UIKit.Mono);
            power.rectTransform.TL(w - 32 - 900, 30, 840, 30);
            var close = UIKit.Button(panel, "Close", "✕", new Color(1, 1, 1, 0.08f), Color.white, 18, Close, 999, UIKit.Symbol);
            close.Rt.TL(w - 60, 20, 40, 40);
            var cats = new[] { BuildCat.Power, BuildCat.Intake, BuildCat.Logistics, BuildCat.Processing, BuildCat.Output };
            float colW = (w - 64 - 4 * 16) / cats.Length;
            for (int c = 0; c < cats.Length; c++)
            {
                float x = 32 + c * (colW + 16);
                UIKit.Label(panel, "Cat", cats[c].ToString().ToUpperInvariant(), 16, new Color(0.55f, 0.66f, 0.64f), TextAnchor.UpperLeft, UIKit.Mono).rectTransform.TL(x, 84, colW, 24);
                float y = 116;
                int shown = 0;
                foreach (var d in Content.Buildables)
                {
                    if (d.Cat != cats[c]) continue;
                    bool unlocked = sim.BuildUnlocked(d);
                    var def = d;
                    var btn = UIKit.Button(panel, "Build:" + d.Id, "", unlocked ? new Color(0.12f, 0.2f, 0.22f) : new Color(0.07f, 0.09f, 0.1f), Color.white, 14, () => { if (unlocked) { OnPick?.Invoke(def); Close(); } }, 12);
                    btn.Rt.TL(x, y, colW, 130);
                    btn.Label.text = "";
                    btn.Button.interactable = unlocked;
                    btn.SetColors(unlocked ? new Color(0.12f, 0.2f, 0.22f) : new Color(0.07f, 0.09f, 0.1f), new Color(0.07f, 0.09f, 0.1f));
                    UIKit.Label(btn.Rt, "Name", unlocked ? d.Name : "??? (research it)", 17, unlocked ? Color.white : new Color(0.4f, 0.48f, 0.47f), TextAnchor.UpperLeft, UIKit.Semibold).rectTransform.TL(14, 10, colW - 28, 24);
                    string stats = $"{Fmt.Money(d.Cost * sim.Scale)}   {d.W}×{d.D}" + (d.Power > 0 ? $"   +{d.Power:0.#} kW" : d.Power < 0 ? $"   {d.Power:0.#} kW" : "");
                    UIKit.Label(btn.Rt, "Stats", stats, 13, new Color(0.42f, 1f, 0.62f), TextAnchor.UpperLeft, UIKit.Mono).rectTransform.TL(14, 36, colW - 28, 20);
                    if (unlocked) UIKit.Label(btn.Rt, "Desc", d.Desc, 13, new Color(0.62f, 0.72f, 0.7f), TextAnchor.UpperLeft, UIKit.Regular, true).rectTransform.TL(14, 58, colW - 28, 68);
                    int built = sim.CountBuilt(d.Id);
                    if (built > 0) UIKit.Label(btn.Rt, "Built", $"×{built}", 13, Color.white, TextAnchor.UpperRight, UIKit.Mono).rectTransform.TL(14, 12, colW - 28, 20);
                    y += 142;
                    shown++;
                }
                if (shown == 0) UIKit.Label(panel, "Soon", "Coming soon.", 14, new Color(0.4f, 0.48f, 0.47f), TextAnchor.UpperLeft, UIKit.Mono).rectTransform.TL(x, 116, colW, 24);
            }
            var help = UIKit.Label(panel, "Help", "Click to build  ·  R rotate  ·  mouse wheel: next item  ·  drag to lay belts  ·  X demolish (full refund)  ·  1 back to grabbing",
                14, new Color(0.55f, 0.66f, 0.64f), TextAnchor.LowerLeft, UIKit.Mono);
            help.rectTransform.TL(32, h - 44, w - 64, 24);
        }

        public void Close() { if (root != null) root.gameObject.SetActive(false); }
    }
}
