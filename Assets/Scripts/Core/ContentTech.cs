// The Maintenance Terminal's tech tree. Ladder rungs (carry, grab, dig) are generated from
// the ladders in ContentWorld so prices live in one place.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        public static readonly Dictionary<string, int> TechIndex = new Dictionary<string, int>();

        static TechDef[] BuildTechs()
        {
            var list = new List<TechDef>();
            TechDef T(string id, string name, TechBranch branch, TechKind kind, double value, double cost, string desc, params string[] req)
            {
                var t = new TechDef { Id = id, Name = name, Branch = branch, Kind = kind, Value = value, Cost = cost, Desc = desc, Requires = req, Index = list.Count };
                list.Add(t);
                return t;
            }

            // carry ladder
            for (int i = 1; i < Carry.Length; i++)
            {
                var c = Carry[i];
                var t = T("carry_" + c.Id, c.Name, TechBranch.Carry, TechKind.Carry, i, c.Cost, c.Desc, i > 1 ? new[] { "carry_" + Carry[i - 1].Id } : new string[0]);
                t.Col = i - 1; t.Row = 0;
            }
            // grab tools
            for (int i = 1; i < GrabTools.Length; i++)
            {
                var g = GrabTools[i];
                var t = T("grab_" + g.Id, g.Name, TechBranch.Tools, TechKind.Tool, i, g.Cost, g.Desc, i > 1 ? new[] { "grab_" + GrabTools[i - 1].Id } : new string[0]);
                t.Target = "grab"; t.Col = i - 1; t.Row = 0;
            }
            // levelled nodes: cheap to author, they give every visit to the terminal something to buy
            TechDef Lv(string id, string name, TechBranch b, TechKind kind, double value, double cost, double growth, int max, int col, int row, string desc, string req = null, bool tokens = false)
            {
                var t = T(id, name, b, kind, value, cost, desc, req == null ? new string[0] : new[] { req });
                t.CostGrowth = growth; t.MaxLevel = max; t.Col = col; t.Row = row; t.WishTokens = tokens;
                return t;
            }
            Lv("carry_bottom", "Sturdier Bottoms", TechBranch.Carry, TechKind.CarryBonus, 0.10, 1.5, 2.0, 10, 0, 1,
                "Duct tape, applied lovingly to the bottom of whatever you're carrying things in.", "carry_cup");
            Lv("carry_sneakers", "Comfy Sneakers", TechBranch.Carry, TechKind.WalkSpeed, 0.05, 1.0, 2.0, 10, 1, 1,
                "Squeaky, orthopedic, and bought from the Payless that closed in 2019.");
            Lv("grab_arms", "Longer Arms", TechBranch.Tools, TechKind.Reach, 0.2, 2, 2.3, 8, 0, 1,
                "Stretching exercises from a VHS tape found in the fountain. They work, somehow.", "grab_grabber");
            Lv("grab_fingers", "Nimble Fingers", TechBranch.Tools, TechKind.GrabRate, 0.12, 1.5, 2.0, 10, 1, 1,
                "You practise picking up pennies from a flat table every night. Your family is worried.");
            // dig tools (hotbar 2): break the crust by hand
            for (int i = 1; i < DigTools.Length; i++)
            {
                var d = DigTools[i];
                var t = T("dig_" + d.Id, d.Name, TechBranch.Tools, TechKind.Tool, i, d.Cost, d.Desc, i > 1 ? new[] { "dig_" + DigTools[i - 1].Id } : new string[0]);
                t.Target = "dig"; t.Col = i - 1; t.Row = 2;
            }
            Lv("dig_shoulders", "Stronger Shoulders", TechBranch.Tools, TechKind.DigPower, 0.15, 20, 2.0, 12, 0, 3,
                "Forty push-ups a day, all of them with a shovel. Every swing and every dig rig bites deeper.", "dig_sandshovel");

            // security: keeping Officer Doug and Chad off your back
            Lv("sec_donuts", "Donut Diplomacy", TechBranch.Security, TechKind.GuardFine, 0.3, 4, 3.5, 3, 0, 0,
                "A box of crullers for mall security, every morning. Fines shrink. Doug's waistline does not.");
            Lv("sec_sign", "'No Rival Divers' Sign", TechBranch.Security, TechKind.RivalRepel, 0.3, 15, 3.5, 3, 1, 0,
                "Laminated, official-looking, and entirely unenforceable. Chad shows up less anyway.");
            var badge = T("sec_badge", "Honorary Deputy Badge", TechBranch.Security, TechKind.GuardFine, 1.0, 4000, "Doug swears you in. You may now wade with impunity (and a badge).", "sec_donuts");
            badge.Col = 0; badge.Row = 1;

            // Head Office: paid in Lucky Pennies (from signing contracts), kept forever
            TechDef H(string id, string name, TechKind kind, double value, double cost, double growth, int max, int col, int row, string desc, params string[] req)
            {
                var t = T(id, name, TechBranch.HeadOffice, kind, value, cost, desc, req);
                t.LuckyPennies = true; t.CostGrowth = growth; t.MaxLevel = max; t.Col = col; t.Row = row;
                return t;
            }
            H("ho_card", "Company Credit Card", TechKind.StartCash, 25, 2, 1.6, 10, 0, 0, "Head Office fronts you some cash at the start of every contract. Receipts required. Receipts ignored.");
            H("ho_van", "Company Van", TechKind.StartCarry, 1, 3, 2.2, 5, 1, 0, "Start every contract already owning a bigger container. The van smells of mop.");
            H("ho_memory", "Institutional Knowledge", TechKind.StartUnlocks, 0, 8, 1, 1, 2, 0, "Start every contract with the cup, grabber, sandbox shovel, hamster wheel, skimmer, belts and hoppers researched.");
            H("ho_seniority", "Seniority", TechKind.ValueMult, 0.12, 2, 1.45, 25, 0, 1, "Everything you deposit is worth a bit more. Nobody knows why. It's just how seniority works.");
            H("ho_sneakers", "Union-Issue Sneakers", TechKind.WalkSpeed, 0.05, 2, 1.6, 8, 1, 1, "Comfortable, regulation grey, and seemingly faster than regular sneakers.");
            H("ho_wishful", "Wishful Thinking", TechKind.Wishability, 3, 3, 1.55, 15, 2, 1, "Head Office runs a billboard campaign: 'Throw Your Money Away (Here)'.");
            H("ho_relics", "Relic Radar", TechKind.RelicRate, 0.25, 4, 1.7, 10, 3, 1, "A dowsing rod that mostly points at relics and occasionally at the food court.");
            H("ho_digger", "Excavation Grant", TechKind.DigPower, 0.2, 3, 1.6, 15, 3, 0, "Every contract starts with a bigger digging budget and a smaller sense of caution.");

            // fountain beautification ("wishability"): more shoppers, more tosses, fancier tosses
            void F(string id, string name, double wish, double cost, bool tokens, string desc, string req, int col)
            {
                var t = T(id, name, TechBranch.Fountain, TechKind.Wishability, wish, cost, desc, req == null ? new string[0] : new[] { req });
                t.Col = col; t.Row = 0; t.WishTokens = tokens;
            }
            F("fountain_scrub", "Scrub the Grime", 3, 0.50, false, "Forty years of algae, gone. The tiles were teal this whole time. Shoppers start trusting the water with nickels.", null, 0);
            F("fountain_jets", "Fix the Water Jets", 4, 3, false, "The jets sputter back to life. People love a fountain that actually fountains.", "fountain_scrub", 1);
            F("fountain_lights", "Coloured Lights", 5, 15, false, "Underwater LEDs in every colour of the 1996 rainbow. Dimes incoming.", "fountain_jets", 2);
            F("fountain_neon", "'MAKE A WISH' Neon", 6, 80, false, "A pink neon sign that buzzes at exactly the pitch of hope.", "fountain_lights", 3);
            F("fountain_cherub", "Cherub Statue", 8, 15, true, "A chubby stone baby who judges everyone's throwing form. Paid for in pure wishes.", "fountain_neon", 4);
            F("fountain_koi", "Koi", 8, 40, true, "Twelve koi, each named after a former mall manager. They are thriving. The managers are not.", "fountain_cherub", 5);
            F("fountain_music", "Mood Music", 10, 2000, false, "Smooth jazz, piped in from a speaker disguised as a rock. The rock is also smooth.", "fountain_koi", 6);
            F("fountain_dispenser", "Lucky Penny Dispenser", 10, 12000, false, "Sells shoppers 'lucky' pennies to throw in. You are, technically, selling them your own pennies.", "fountain_music", 7);
            F("fountain_photo", "Influencer Photo Spot", 12, 150, true, "A ring light, a flower wall and a sign saying #WishWall. The influencers arrive within minutes.", "fountain_dispenser", 8);
            F("fountain_golden", "Golden Statue", 15, 150000, false, "A gold-plated statue of the mall's founder, mid-toss. Slightly too shiny to look at.", "fountain_photo", 9);
            F("fountain_show", "Fountain Light Show", 20, 500, true, "Every hour on the hour: lasers, fog and a synth version of 'Wind Beneath My Wings'.", "fountain_golden", 10);
            F("fountain_certified", "Official Wishing Fountain", 25, 3000000, false, "Certified by the International Wishing Fountain Board (you founded it last week).", "fountain_show", 11);
            F("fountain_wormhole", "Wormhole to Other Fountains", 40, 2000, true, "A shimmering portal to every mall fountain on Earth. Things come through. Some of them are coins.", "fountain_certified", 12);
            Lv("fountain_polish", "Polish the Tiles", TechBranch.Fountain, TechKind.Wishability, 1, 1, 1.62, 30, 0, 1,
                "Elbow grease, applied one tile at a time. The fountain gets a little more wishable every time.", "fountain_scrub");
            Lv("fountain_mints", "Free Mints by the Fountain", TechBranch.Fountain, TechKind.TossRate, 0.08, 4, 1.85, 15, 1, 1,
                "Shoppers linger for a free mint, then feel obligated to throw something in.", "fountain_jets");
            Lv("fountain_coinpolish", "Coin Polish", TechBranch.Fountain, TechKind.ValueMult, 0.10, 8, 1.75, 20, 2, 1,
                "The COIN-O-MATIC pays more for shiny coins. Nobody knows why. Don't ask it.", "fountain_lights");
            Lv("fountain_patience", "Wish Catcher's Patience", TechBranch.Fountain, TechKind.WishLife, 0.15, 3, 1.8, 6, 3, 1,
                "Wishes linger a little longer over the water, as if they want to be caught.", "fountain_scrub", true);

            AddFactoryTechs(T);

            var arr = list.ToArray();
            foreach (var t in arr) TechIndex[t.Id] = t.Index;
            return arr;
        }
    }
}
