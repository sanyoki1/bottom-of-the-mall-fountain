// The mall's Wonder: a megaproject hung from the skylight over the fountain, built in stages.
// Each stage asks for goods (coin rolls, bags, pallets, gold bars, wish bricks, relics, particular
// oddities, Wish Tokens) plus a fee. Goods that reach any hopper or the COIN-O-MATIC while the next
// stage still wants them are set aside for it instead of being sold. Approving a stage (at the
// Wonder Plan easel) grants its bonus for the rest of this contract; finishing the last stage earns
// the mall's permanent perk.
using System;
using System.Collections.Generic;
using System.Text;

namespace WishExtractor.Core
{
    public sealed partial class Sim
    {
        /// <summary>A good was set aside for the Wonder: need id, now have, want, hopper it came from (null = the kiosk).</summary>
        public event Action<string, int, int, Building> OnWonderGood;
        public event Action<TechDef> OnWonderStage;
        public event Action<MallDef> OnWonderComplete;

        public WonderDef Wonder => Mall.Wonder;

        /// <summary>Tech index of the Wonder's next stage, or -1 when it's finished (or the mall has none).</summary>
        public int WonderNext
        {
            get
            {
                var w = Mall.Wonder;
                if (w == null) return -1;
                foreach (int i in w.Stages) if (techLevel[i] == 0) return i;
                return -1;
            }
        }

        public int WonderStagesDone
        {
            get
            {
                var w = Mall.Wonder;
                if (w == null) return 0;
                int n = 0;
                foreach (int i in w.Stages) if (techLevel[i] > 0) n++;
                return n;
            }
        }

        public int WonderStageCount => Mall.Wonder != null ? Mall.Wonder.Stages.Length : 0;
        public bool WonderDone => Mall.Wonder != null && WonderNext < 0;
        public bool WonderFinishedEver(MallDef m) => S.wonders.Contains(m.Id);
        public int WondersFinished => S.wonders.Count;

        /// <summary>How many of a need the next stage has (Wish Tokens: your balance).</summary>
        public int WonderHave(string need)
        {
            if (need == "tokens") return (int)Math.Floor(S.wishTokens + 1e-9);
            foreach (var ic in S.wonderGoods) if (ic.id == need) return ic.count;
            return 0;
        }

        void SetWonderHave(string need, int count)
        {
            foreach (var ic in S.wonderGoods) if (ic.id == need) { ic.count = count; return; }
            S.wonderGoods.Add(new IdCount(need, count));
        }

        public static bool NeedMatches(string need, ItemType t)
        {
            switch (need)
            {
                case "relic": return t.Cat == ItemCat.Relic;
                case "relic_rare": return t.Cat == ItemCat.Relic && t.Rarity >= Rarity.Rare;
                case "relic_epic": return t.Cat == ItemCat.Relic && t.Rarity >= Rarity.Epic;
                case "relic_legendary": return t.Cat == ItemCat.Relic && t.Rarity == Rarity.Legendary;
                case "oddity": return t.Cat == ItemCat.Oddity;
                case "tokens": return false;
                default: return t.Id == need;
            }
        }

        /// <summary>What a need is called on the plan and the HUD (singular).</summary>
        public static string NeedName(string need)
        {
            switch (need)
            {
                case "relic": return "Relic";
                case "relic_rare": return "Rare Relic (or better)";
                case "relic_epic": return "Epic Relic (or better)";
                case "relic_legendary": return "Legendary Relic";
                case "oddity": return "Oddity";
                case "tokens": return "Wish Tokens";
                case "roll": return "Coin Roll";
                case "bag": return "Coin Bag";
                case "pallet": return "Coin Pallet";
                case "bar": return "Gold Bar (melted)";
                case "brick": return "Wish Brick";
            }
            int ti = Content.TypeOrNone(need);
            return ti >= 0 ? Content.Items[ti].Name : need;
        }

        /// <summary>Where a need comes from, for hints.</summary>
        public static string NeedHint(string need)
        {
            switch (need)
            {
                case "relic": case "relic_rare": case "relic_epic": case "relic_legendary": return "sorters turn up relics";
                case "tokens": return "catch wishes";
                case "roll": return "Coin Roller";
                case "bag": return "Coin Bagger (after a roller)";
                case "pallet": return "Palletiser (after a bagger)";
                case "bar": return "Gold Melter";
                case "brick": return "Wish Compressor";
            }
            return "shoppers throw these in";
        }

        /// <summary>
        /// Set aside up to count items of this type for the next stage (called for everything reaching a
        /// hopper or the kiosk). Returns how many were taken; the rest are sold as usual.
        /// </summary>
        int TakeForWonder(int type, int count, Building from)
        {
            if (count <= 0) return 0;
            int next = WonderNext;
            if (next < 0) return 0;
            var t = Content.Items[type];
            int taken = 0;
            foreach (var n in Content.Techs[next].Needs)
            {
                if (!NeedMatches(n.id, t)) continue;
                int have = WonderHave(n.id);
                int take = Math.Min(count - taken, n.count - have);
                if (take <= 0) continue;
                SetWonderHave(n.id, have + take);
                taken += take;
                S.wonderItems += take;
                OnWonderGood?.Invoke(n.id, have + take, n.count, from);
                if (taken >= count) break;
            }
            return taken;
        }

        /// <summary>Every good (and the Wish Tokens) the stage needs is in.</summary>
        public bool WonderGoodsReady(int techIndex)
        {
            foreach (var n in Content.Techs[techIndex].Needs)
                if (WonderHave(n.id) < n.count) return false;
            return true;
        }

        /// <summary>Does the next stage still want this item type?</summary>
        public bool WonderWants(int type)
        {
            int next = WonderNext;
            if (next < 0) return false;
            var t = Content.Items[type];
            foreach (var n in Content.Techs[next].Needs)
                if (NeedMatches(n.id, t) && WonderHave(n.id) < n.count) return true;
            return false;
        }

        /// <summary>Stage approved (BuyTech): pay the tokens, use up the goods, and finish the Wonder if it was the last.</summary>
        bool CompleteWonderStage(TechDef t)
        {
            foreach (var n in t.Needs) if (n.id == "tokens") S.wishTokens = Math.Max(0, S.wishTokens - n.count);
            S.wonderGoods.Clear();
            S.wonderStages++;
            if (WonderNext >= 0 || S.wonders.Contains(Mall.Id)) return false;
            S.wonders.Add(Mall.Id);
            return true;
        }

        /// <summary>The HUD's goal card shows the Wonder once the guided objectives are past the factory basics.</summary>
        public bool GoalIsWonder
        {
            get
            {
                if (S.mallCleared || WonderNext < 0) return false;
                var o = CurrentObjective;
                return o == null || o.Id == "wonder1" || o.Id == "concrete" || o.Id.StartsWith("mall");
            }
        }

        /// <summary>"Coin Roll 12/40 · Relic 1/1 ✓ · fee $400": the next stage's shopping list.</summary>
        public string WonderNeedsLine(int techIndex, string sep = "  ·  ")
        {
            var sb = new StringBuilder();
            var t = Content.Techs[techIndex];
            foreach (var n in t.Needs)
            {
                int have = Math.Min(WonderHave(n.id), n.count);
                if (sb.Length > 0) sb.Append(sep);
                sb.Append(NeedName(n.id)).Append(' ').Append(Fmt.Int(have)).Append('/').Append(Fmt.Int(n.count));
                if (have >= n.count) sb.Append(" ✓");
            }
            double fee = TechCost(techIndex);
            if (fee > 0) sb.Append(sep).Append("fee ").Append(Fmt.Money(fee));
            return sb.ToString();
        }

        /// <summary>What a one-off bonus does, in words (Wonder stages and perks).</summary>
        public static string RewardText(TechKind kind, double v)
        {
            switch (kind)
            {
                case TechKind.ValueMult: return $"+{v * 100:0}% sale value";
                case TechKind.DigPower: return $"+{v * 100:0}% digging (hands and rigs)";
                case TechKind.TossRate: return $"+{v * 100:0}% tosses";
                case TechKind.Wishability: return $"+{v:0} wishability";
                case TechKind.WishLife: return $"wishes hover {v * 100:0}% longer";
                case TechKind.WishValue: return $"+{v * 100:0}% wish value";
                case TechKind.RelicRate: return $"+{v * 100:0}% relic finds";
                case TechKind.FindRate: return $"buried finds turn up {v * 100:0}% more often";
                case TechKind.FrenzyTime: return $"frenzies last {v * 100:0}% longer";
                case TechKind.WalkSpeed: return $"+{v * 100:0}% walking speed";
                case TechKind.MachineSpeed: return $"+{v * 100:0}% machine speed";
            }
            return "";
        }

        /// <summary>Tests and the tour: fill the next stage's order (goods and tokens), so it can be approved.</summary>
        public void DebugFillWonder()
        {
            int next = WonderNext;
            if (next < 0) return;
            foreach (var n in Content.Techs[next].Needs)
            {
                if (n.id == "tokens") S.wishTokens = Math.Max(S.wishTokens, n.count);
                else SetWonderHave(n.id, n.count);
            }
            S.cash = Math.Max(S.cash, TechCost(next));
        }
    }
}
