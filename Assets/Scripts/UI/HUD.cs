// First-person HUD: crosshair and interact prompt (centre), wallet (top-left), goal (top-centre),
// carry meter (bottom-left), tool hotbar (bottom-centre), COIN-O-MATIC receipts (right) and a
// short message line for things like "your hands are full".
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
        public RectTransform Root => root;
        // crosshair + prompt
        Image dot, ring;
        Text prompt;
        float ringScale = 1;
        // wallet
        Text cashText, rateText, tokenText;
        double shownCash;
        // goal
        RectTransform goalCard;
        Text goalText, goalHint;
        // carry
        Text carryName, carryCount, carryValue, wadeText;
        UIKit.Bar carryBar;
        Image carryBarFill;
        // hotbar
        readonly Text[] slotName = new Text[3];
        readonly Image[] slotBg = new Image[3];
        public int Slot = 1;
        // receipt + message
        RectTransform receipt;
        CanvasGroup receiptGroup;
        Text receiptBody;
        float receiptAge = 99;
        Text message;
        float messageAge = 99;
        Text keysHint;

        public void Build(Canvas canvas, Sim s)
        {
            sim = s;
            root = UIKit.Rect(canvas.transform, "HUD").Stretch();
            BuildCrosshair();
            BuildWallet();
            BuildGoal();
            BuildCarry();
            BuildHotbar();
            BuildReceipt();
            message = UIKit.Label(root, "Message", "", 22, Color.white, TextAnchor.MiddleCenter, UIKit.Semibold);
            message.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -150), new Vector2(1200, 40));
            Shadowed(message);
            keysHint = UIKit.Label(root, "Keys", "", 14, new Color(1, 1, 1, 0.75f), TextAnchor.LowerRight, UIKit.Semibold);
            keysHint.rectTransform.Place(new Vector2(1, 0), new Vector2(-24, 20), new Vector2(700, 24));
            Shadowed(keysHint);
            shownCash = sim.S.cash;
        }

        static void Shadowed(Text t)
        {
            var sh = t.gameObject.AddComponent<Shadow>();
            sh.effectColor = new Color(0, 0, 0, 0.6f);
            sh.effectDistance = new Vector2(1.5f, -2f);
        }

        void BuildCrosshair()
        {
            ring = UIKit.Image(root, "Ring", UIKit.SoftRing, new Color(1, 1, 1, 0.85f));
            ring.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34, 34));
            ring.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            dot = UIKit.Image(root, "Dot", UIKit.Circle, new Color(1, 1, 1, 0.9f));
            dot.rectTransform.Place(new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(6, 6));
            dot.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            var dsh = dot.gameObject.AddComponent<Shadow>();
            dsh.effectColor = new Color(0, 0, 0, 0.5f);
            prompt = UIKit.Label(root, "Prompt", "", 20, Color.white, TextAnchor.UpperCenter, UIKit.Semibold);
            prompt.rectTransform.Place(new Vector2(0.5f, 0.5f), new Vector2(0, -34), new Vector2(900, 60));
            prompt.rectTransform.pivot = new Vector2(0.5f, 1);
            Shadowed(prompt);
        }

        void BuildWallet()
        {
            var card = UIKit.Card(root, "Wallet", new Color(1, 1, 1, 0.86f), 22, false).TL(20, 20, 330, 104);
            UIKit.Label(card, "Caption", "CASH", 13, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Semibold).rectTransform.TL(22, 12, 200, 18);
            cashText = UIKit.Label(card, "Cash", "$0.00", 40, Pal.Ink, TextAnchor.UpperLeft, UIKit.Bold);
            cashText.rectTransform.TL(18, 26, 300, 50);
            rateText = UIKit.Label(card, "Rate", "", 15, Pal.Green, TextAnchor.UpperLeft, UIKit.Semibold);
            rateText.rectTransform.TL(22, 76, 180, 22);
            tokenText = UIKit.Label(card, "Tokens", "", 15, Pal.Purple, TextAnchor.UpperRight, UIKit.Semibold);
            tokenText.rectTransform.TL(150, 76, 160, 22);
            wishText = UIKit.Label(root, "Wishability", "", 14, Color.white, TextAnchor.UpperLeft, UIKit.Semibold);
            wishText.rectTransform.TL(26, 132, 600, 22);
            Shadowed(wishText);
        }

        Text wishText;

        void BuildGoal()
        {
            goalCard = UIKit.Card(root, "Goal", new Color(1, 1, 1, 0.84f), 18, false).Place(new Vector2(0.5f, 1), new Vector2(0, -20), new Vector2(680, 88));
            var k = UIKit.Label(goalCard, "Kicker", "GOAL", 12, Pal.Accent, TextAnchor.UpperLeft, UIKit.Bold);
            k.rectTransform.TL(20, 10, 100, 16);
            goalText = UIKit.Label(goalCard, "Text", "", 18, Pal.Ink, TextAnchor.UpperLeft, UIKit.Semibold);
            goalText.rectTransform.TL(70, 7, 530, 26);
            goalHint = UIKit.Label(goalCard, "Hint", "", 14, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Regular, true);
            goalHint.rectTransform.TL(20, 36, 640, 46);
        }

        void BuildCarry()
        {
            var card = UIKit.Card(root, "Carry", new Color(1, 1, 1, 0.86f), 22, false).Place(new Vector2(0, 0), new Vector2(20, 20), new Vector2(360, 108));
            carryName = UIKit.Label(card, "Name", "", 13, Pal.Ink2, TextAnchor.UpperLeft, UIKit.Semibold);
            carryName.rectTransform.TL(22, 12, 240, 18);
            wadeText = UIKit.Label(card, "Wade", "", 13, Pal.Blue, TextAnchor.UpperRight, UIKit.Bold);
            wadeText.rectTransform.TL(200, 12, 140, 18);
            carryCount = UIKit.Label(card, "Count", "", 30, Pal.Ink, TextAnchor.UpperLeft, UIKit.Bold);
            carryCount.rectTransform.TL(20, 28, 200, 40);
            carryValue = UIKit.Label(card, "Value", "", 16, Pal.GoldInk, TextAnchor.UpperRight, UIKit.Semibold);
            carryValue.rectTransform.TL(180, 38, 160, 24);
            carryBar = UIKit.ProgressBar(card, "Bar", new Color(0, 0, 0, 0.08f), Pal.Accent);
            carryBar.Rt.TL(22, 76, 316, 12);
            carryBarFill = carryBar.Fill;
        }

        void BuildHotbar()
        {
            var bar = UIKit.Rect(root, "Hotbar").Place(new Vector2(0.5f, 0), new Vector2(0, 20), new Vector2(3 * 170 + 20, 64));
            for (int i = 0; i < 3; i++)
            {
                var bg = UIKit.Image(bar, "Slot" + (i + 1), UIKit.Rounded, new Color(0, 0, 0, 0.35f), 14);
                bg.rectTransform.TL(i * 177, 0, 166, 64);
                slotBg[i] = bg;
                UIKit.Label(bg.rectTransform, "Key", (i + 1).ToString(), 13, new Color(1, 1, 1, 0.6f), TextAnchor.UpperLeft, UIKit.Bold).rectTransform.TL(10, 6, 20, 16);
                slotName[i] = UIKit.Label(bg.rectTransform, "Name", "", 15, Color.white, TextAnchor.MiddleCenter, UIKit.Semibold, true);
                slotName[i].rectTransform.TL(14, 8, 140, 50);
            }
        }

        void BuildReceipt()
        {
            receipt = UIKit.Card(root, "Receipt", new Color(0.99f, 0.98f, 0.95f, 0.97f), 6, false).Place(new Vector2(1, 0.5f), new Vector2(-24, 60), new Vector2(300, 250));
            receiptGroup = receipt.gameObject.AddComponent<CanvasGroup>();
            receiptGroup.blocksRaycasts = false;
            receiptGroup.alpha = 0;
            var mono = UIKit.Mono;
            var head = UIKit.Label(receipt, "Head", "COIN-O-MATIC 3000", 17, Pal.Ink, TextAnchor.UpperCenter, UIKit.Bold);
            head.rectTransform.TL(0, 14, 300, 24);
            receiptBody = UIKit.Label(receipt, "Body", "", 14, Pal.Ink2, TextAnchor.UpperLeft, mono, true);
            receiptBody.rectTransform.TL(20, 44, 260, 200);
        }

        // ───────────────────────────── events ─────────────────────────────

        public void ShowReceipt(double cash, int items, string joke)
        {
            string line = new string('-', 32);
            receiptBody.text =
                $"{line}\nITEMS COUNTED{Pad(items.ToString("#,0"), 19)}\n" +
                $"<b>TOTAL{Pad(Fmt.Money(cash), 27)}</b>\n" +
                $"FEE (9%){Pad("WAIVED", 24)}\n{line}\n<i>{joke}</i>";
            receiptAge = 0;
        }

        static string Pad(string s, int width) => s.PadLeft(width);

        public void ShowMessage(string text)
        {
            message.text = text;
            messageAge = 0;
        }

        // ───────────────────────────── per-frame ─────────────────────────────

        public void Refresh(float dt, Target target, bool wading, bool frozen)
        {
            // crosshair + prompt
            bool on = target.Kind != TargetKind.None && !frozen;
            ringScale = Mathf.Lerp(ringScale, on ? 1f : 0.55f, 1 - Mathf.Exp(-dt * 14));
            ring.rectTransform.localScale = Vector3.one * ringScale;
            ring.color = on ? Pal.A(Color.Lerp(Pal.Accent, Color.white, 0.35f), 0.95f) : new Color(1, 1, 1, 0.35f);
            dot.enabled = !frozen;
            ring.enabled = !frozen;
            prompt.text = frozen ? "" : PromptFor(target);

            // wallet
            shownCash = Mathf.Abs((float)(sim.S.cash - shownCash)) < 0.005 ? sim.S.cash : shownCash + (sim.S.cash - shownCash) * (1 - Mathf.Exp(-dt * 10));
            cashText.text = Fmt.Money(shownCash);
            rateText.text = sim.EarnRate > 0.0001 ? $"+{Fmt.Money(sim.EarnRate * 60)}/min" : "";
            tokenText.text = sim.S.wishTokens > 0 ? $"✦ {Fmt.Num(sim.S.wishTokens)} tokens" : "";
            wishText.text = $"Wishability <b>{sim.Wishability:0}</b>  ·  a toss every {sim.TossInterval:0.0}s  ·  {sim.Shoppers.Count} shoppers";

            // goal
            var o = sim.CurrentObjective;
            goalCard.gameObject.SetActive(o != null);
            if (o != null)
            {
                goalText.text = o.Text;
                goalHint.text = o.Hint;
            }

            // carry
            var c = sim.CarryDef;
            carryName.text = c.Name.ToUpperInvariant();
            carryCount.text = $"{sim.CarryUsed} / {Fmt.Int(sim.CarryCapacity)}";
            carryValue.text = sim.Carried.Count > 0 ? Fmt.Money(sim.CarriedValue) : "";
            float frac = sim.CarryUsed / (float)Mathf.Max(1, sim.CarryCapacity);
            carryBar.Set(frac);
            carryBarFill.color = frac >= 1 ? Pal.Pink : Pal.Accent;
            wadeText.text = wading ? "WADING" : "";

            // hotbar
            slotName[0].text = sim.Grab.Name;
            slotName[1].text = sim.DigTier > 0 ? sim.DigTool.Name : "—";
            slotName[2].text = "Build (soon)";
            for (int i = 0; i < 3; i++)
                slotBg[i].color = i + 1 == Slot ? Pal.A(Pal.Accent, 0.75f) : new Color(0, 0, 0, 0.35f);

            // receipt + message
            receiptAge += dt;
            receiptGroup.alpha = receiptAge < 4.5f ? Mathf.Clamp01(receiptAge / 0.2f) * Mathf.Clamp01((4.5f - receiptAge) / 0.6f) : 0;
            receipt.anchoredPosition = new Vector2(-24 + (1 - Mathf.Clamp01(receiptAge / 0.25f)) * 60, 60);
            messageAge += dt;
            message.color = new Color(1, 1, 1, messageAge < 2.6f ? Mathf.Clamp01((2.6f - messageAge) / 0.5f) : 0);

            keysHint.text = frozen ? "" : "WASD move · Shift sprint · Space jump · E / click use · J journal · Esc menu";
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
                    string name = def.Name;
                    if (!sim.CanCarry(it.Type)) return $"<color=#FF8FA8>Hands full</color>  ·  deposit at the COIN-O-MATIC";
                    double v = it.Value * sim.ValueMult * Sim.CatRate(def.Cat);
                    string key = sim.Grab.Area > 0 ? "Scoop" : "Pick up";
                    return $"<b>[E]</b> {key} {name}  <color=#FFE08A>{Fmt.Money(v)}</color>";
                }
                case TargetKind.Wish:
                {
                    var w = sim.FindWish(t.Uid);
                    if (w == null) return "";
                    string col = UIKit.Hex(Pal.Rarity[(int)w.Def.Rarity]);
                    return $"<b>[E]</b> Catch the <color=#{col}>{RarityColors.Names[(int)w.Def.Rarity].ToLowerInvariant()} wish</color>  <color=#9CFFB0>{Fmt.Money(w.Value)}</color>";
                }
                case TargetKind.Board:
                {
                    int i = sim.NextFountainTech();
                    if (i < 0) return "Fountain Improvement Plan  ·  <color=#C8C8C8>all done</color>";
                    var tech = Content.Techs[i];
                    string cost = Fmt.Money(sim.TechCost(i));
                    return sim.CanAfford(i)
                        ? $"<b>[E]</b> Approve: {tech.Name}  <color=#FFE08A>{cost}</color>  <color=#C8C8C8>(wishability +{tech.Value:0})</color>"
                        : $"{tech.Name}  <color=#FF8FA8>{cost}</color>  <color=#C8C8C8>· can't afford yet</color>";
                }
                case TargetKind.Kiosk:
                    return sim.Carried.Count > 0
                        ? $"<b>[E]</b> Deposit {Fmt.Int(sim.CarriedCount)} item{(sim.CarriedCount == 1 ? "" : "s")}  <color=#9CFFB0>{Fmt.Money(sim.CarriedValue)}</color>"
                        : "COIN-O-MATIC 3000  ·  <color=#C8C8C8>nothing to deposit</color>";
            }
            return "";
        }
    }
}
