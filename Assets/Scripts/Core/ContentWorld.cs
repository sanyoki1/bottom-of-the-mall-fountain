// The physical world's content: every item type (tossed coins, ridiculous oddities, crust loot,
// relics, processed goods), the carry and tool ladders, NPC archetypes and the jokes.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        // ───────────────────────────── item registry ─────────────────────────────

        static readonly List<ItemType> types = new List<ItemType>();

        static ItemType AddType(string id, string name, ItemCat cat, ItemShape shape, uint color, double value, float scale, string desc = null)
        {
            var t = new ItemType { Id = id, Name = name, Cat = cat, Shape = shape, Color = color, BaseValue = value, Scale = scale, Desc = desc, Index = types.Count };
            types.Add(t);
            ItemIndex[id] = t.Index;
            return t;
        }

        /// <summary>Coins NPCs toss, cheapest first. MinWish = fountain wishability needed before anyone throws one.</summary>
        static void BuildCoins()
        {
            void Coin(string id, string name, double value, uint color, float scale, double minWish, string desc)
            {
                var t = AddType(id, name, ItemCat.Coin, ItemShape.Coin, color, value, scale, desc);
                t.Tier = CoinTiers.Count;
                CoinTiers.Add(t.Index);
                CoinTierMinWish.Add(minWish);
            }
            Coin("penny", "Penny", 0.01, 0xC77B43, 0.40f, 0, "Canada stopped minting these in 2013. Nobody told the fountain.");
            Coin("nickel", "Nickel", 0.05, 0xB9BDC2, 0.43f, 3, "A beaver. Five cents. Mildly damp.");
            Coin("dime", "Dime", 0.10, 0xD4D8DC, 0.36f, 7, "Tiny, shiny, and somehow worth more than the nickel.");
            Coin("quarter", "Quarter", 0.25, 0xC9CDD2, 0.47f, 12, "A caribou that has seen things.");
            Coin("loonie", "Loonie", 1.00, 0xD4A83A, 0.50f, 20, "Eleven-sided. Somebody's whole bus fare.");
            Coin("toonie", "Toonie", 2.00, 0xC8C8C8, 0.54f, 30, "Two-tone, two dollars, too good to throw. They threw it.");
            Coin("silver", "Silver Dollar", 5.00, 0xE6EAEE, 0.60f, 45, "A collector's coin, tossed by someone who is not a collector.");
            Coin("gold", "Gold Coin", 50.0, 0xF2C230, 0.60f, 65, "Real gold. The person who threw it wished for 'a sign'.");
            var d = AddType("diamond", "Diamond", ItemCat.Coin, ItemShape.Diamond, 0xDFF4FF, 500, 0.55f, "A loose diamond. Apparently people just have these.");
            d.Tier = CoinTiers.Count;
            CoinTiers.Add(d.Index);
            CoinTierMinWish.Add(90);
        }

        /// <summary>The ridiculous things people throw once the fountain is fancy enough.</summary>
        static void BuildOddities()
        {
            void O(string id, string name, double value, ItemShape shape, uint color, float scale, double minWish, Rarity r, string desc, bool heavy = false)
            {
                var t = AddType(id, name, ItemCat.Oddity, shape, color, value, scale, desc);
                t.Rarity = r;
                t.Heavy = heavy;
                Oddities.Add(t.Index);
                OddityMinWish.Add(minWish);
            }
            O("duck", "Rubber Duck", 5, ItemShape.Duck, 0xFFD23A, 1.0f, 18, Rarity.Common, "Squeaks with quiet disapproval.");
            O("shoe", "Toddler Shoe", 15, ItemShape.Shoe, 0xFF7AA8, 1.0f, 20, Rarity.Common, "Single. Velcro. Light-up. Still blinking.");
            O("keys", "Car Keys", 40, ItemShape.Keys, 0xC9CDD2, 1.0f, 25, Rarity.Common, "They were in the wrong pocket the whole time. Now they're in the fountain.");
            O("teeth", "Dentures", 80, ItemShape.Teeth, 0xF4EEE0, 1.0f, 28, Rarity.Uncommon, "Grandma laughed so hard at the mime.");
            O("timeshare", "Timeshare Contract", 0.5, ItemShape.Paper, 0xE8E0C8, 1.4f, 32, Rarity.Uncommon, "Legally binding. Nobody, not even the fountain, can escape it.");
            O("phone", "Entire Phone (Mid-Call)", 300, ItemShape.Phone, 0x2A2D34, 1.0f, 35, Rarity.Uncommon, "Somebody's mom is still on the line. She says hi.");
            O("wallet", "Businessman's Wallet", 250, ItemShape.Wallet, 0x4A3222, 1.0f, 40, Rarity.Uncommon, "$214, a gym card, and a photo of a boat.");
            O("toaster", "Toaster", 60, ItemShape.Toaster, 0xD8DCE0, 1.0f, 42, Rarity.Uncommon, "Still warm. Please don't.");
            O("bowling", "Bowling Ball", 120, ItemShape.Ball, 0x3A2A7A, 1.0f, 45, Rarity.Rare, "Sixteen pounds of 'I'm done with this league'.", true);
            O("trophy", "Bowling Trophy", 200, ItemShape.Trophy, 0xE8B83A, 1.0f, 48, Rarity.Rare, "2nd place, 1997 regionals. Thrown out of spite.");
            O("ring", "Wedding Ring (Mid-Argument)", 900, ItemShape.Gem, 0xF0D060, 1.3f, 50, Rarity.Rare, "Thrown with remarkable accuracy.");
            O("beanie", "Beanie Baby", 400, ItemShape.Wad, 0x6A4FA8, 1.6f, 58, Rarity.Rare, "Tag intact. Hopes intact.");
            O("seed", "Crypto Seed Phrase (On a Napkin)", 1500, ItemShape.Paper, 0xF6F2EA, 1.2f, 62, Rarity.Epic, "Twelve words and a ketchup stain. Might be millions. Might be a grocery list.");
            O("lottery", "Lottery Ticket (Five Numbers)", 2500, ItemShape.Paper, 0xFFD34D, 1.1f, 70, Rarity.Epic, "Five numbers match. The sixth is under the gum.");
            O("fridge", "Smart Fridge", 3000, ItemShape.Cube, 0xE8ECEF, 4.2f, 82, Rarity.Epic, "It has been texting the fountain. The fountain has not replied.", true);
            O("vending", "Vending Machine", 5000, ItemShape.Vending, 0xD8283A, 1.0f, 95, Rarity.Epic, "A bodybuilder wanted a Diet Coke. It wanted to stay.", true);
            O("goldbar", "Gold Bar", 8000, ItemShape.Bar, 0xF2C230, 1.2f, 110, Rarity.Legendary, "Tossed by a billionaire who 'wanted to feel something'.");
            O("tiara", "Pageant Tiara", 12000, ItemShape.Gem, 0xE8F4FF, 1.8f, 125, Rarity.Legendary, "The pageant is over. The pageant is never over.");
            O("moonrock", "Moon Rock (Probably)", 25000, ItemShape.Chunk, 0x9A9A9A, 1.0f, 140, Rarity.Legendary, "Certificate of authenticity signed 'An Astronaut'.");
        }

        /// <summary>Crust loot, gunk, relics and processed goods, per mall where it matters.</summary>
        static void BuildMallTypes()
        {
            for (int m = 0; m < Malls.Length; m++)
            {
                var mall = Malls[m];
                mall.LootTypes = new int[mall.Items.Length];
                for (int i = 0; i < mall.Items.Length; i++)
                {
                    var it = mall.Items[i];
                    var t = AddType($"{mall.Id}_loot{i:00}", it.Name, it.Value > 0 ? ItemCat.Loot : ItemCat.Junk, it.Shape, it.Color, it.Value, it.Shape == ItemShape.Coin ? 0.45f : 0.9f);
                    t.Mall = m;
                    mall.LootTypes[i] = t.Index;
                }
                mall.GunkTypes = new int[mall.Strata.Length];
                for (int s = 0; s < mall.Strata.Length; s++)
                {
                    var st = mall.Strata[s];
                    var t = AddType($"{mall.Id}_gunk{s}", "Gunk: " + st.Name, ItemCat.Gunk, ItemShape.Chunk, st.Color, 0, 1f, "A chunk of crust. Worth pennies until it's washed and sorted.");
                    t.Mall = m;
                    t.Stratum = s;
                    mall.GunkTypes[s] = t.Index;
                }
                foreach (var r in mall.Relics)
                {
                    var t = AddType(r.Id, r.Name, ItemCat.Relic, r.Shape, r.Color, r.BaseValue, r.Shape == ItemShape.Coin ? 0.6f : 1.1f, r.Desc);
                    t.Mall = m;
                    t.Rarity = r.Rarity;
                    r.ItemType = t.Index;
                }
            }
            AddType("washed", "Washed Loot", ItemCat.Washed, ItemShape.Wad, 0xB8B8A8, 0, 1f, "Clean, unsorted, and 35% of the way to being money.");
            AddType("junk", "Assorted Junk", ItemCat.Junk, ItemShape.Wad, 0x8A7A6A, 0, 1f, "Gum, straws and hair ties. The COIN-O-MATIC will not be pleased.");
            AddType("roll", "Coin Roll", ItemCat.Roll, ItemShape.Roll, 0xC77B43, 0, 1f, "Fifty coins in a paper sleeve. Banks love these.").Units = 50;
            AddType("bag", "Coin Bag", ItemCat.Bag, ItemShape.Bag, 0xB89A5A, 0, 1f, "Twenty rolls in a canvas sack with a dollar sign on it, as is tradition.").Units = 1000;
            AddType("pallet", "Coin Pallet", ItemCat.Pallet, ItemShape.Cube, 0xA87A48, 0, 5f, "Forty bags shrink-wrapped to a pallet. Forklift not included.").Units = 40000;
            AddType("bar", "Melted Gold Bar", ItemCat.Bar, ItemShape.Bar, 0xF2C230, 0, 1f, "Everything shiny, melted into something shinier.");
            AddType("brick", "Wish Brick", ItemCat.Brick, ItemShape.Brick, 0x9A5CF7, 0, 1f, "Compressed nostalgia. Billionaires buy these to feel things.");
        }

        public static readonly Dictionary<string, int> ItemIndex = new Dictionary<string, int>();
        public static readonly List<int> CoinTiers = new List<int>();
        public static readonly List<double> CoinTierMinWish = new List<double>();
        public static readonly List<int> Oddities = new List<int>();
        public static readonly List<double> OddityMinWish = new List<double>();

        public static int Type(string id) => ItemIndex[id];

        // ───────────────────────────── carry & tools ─────────────────────────────

        static CarryDef[] BuildCarry()
        {
            var list = new List<CarryDef>();
            void C(string id, string name, int cap, double cost, string desc, float speed = 1f, bool noJump = false, float auto = 0)
                => list.Add(new CarryDef { Id = id, Name = name, Capacity = cap, Cost = cost, Desc = desc, SpeedMult = speed, NoJump = noJump, AutoRadius = auto, Index = list.Count });
            C("hands", "Bare Hands", 1, 0, "One coin at a time. Choose wisely.");
            C("cup", "Paper Cup", 5, 0.60, "From the food court. Lightly used. Heavily sticky.");
            C("pail", "Sand Pail", 20, 4, "Red plastic. Came free with a kids' meal in 1996.");
            C("bucket", "Mop Bucket", 60, 25, "Borrowed from the custodial closet. The mop is staying.");
            C("fanny", "Fanny Pack of Holding", 150, 150, "Bigger on the inside. Smells like 1994.");
            C("barrow", "Wheelbarrow", 500, 1200, "Squeaks on every rotation. You are slower, but richer.", 0.85f);
            C("cart", "Shopping Cart", 2000, 9000, "One wheel only turns left. You cannot jump with a cart. Nobody can.", 0.8f, true);
            C("scrubber", "Ride-On Floor Scrubber", 10000, 80000, "Drivable! Also cleans the floor, technically.", 1.3f);
            C("shopvac", "Shop-Vac Backpack", 40000, 600000, "Hoovers up everything within a couple of metres of you.", 1.1f, false, 1.8f);
            C("hopper", "Industrial Hopper Suit", 250000, 6e6, "A walking coin silo. The mall has asked you to stop.", 1.0f, false, 3.2f);
            return list.ToArray();
        }

        static ToolDef[] BuildGrabTools()
        {
            var list = new List<ToolDef>();
            void T(string id, string name, double cost, float reach, float area, float rate, string desc)
                => list.Add(new ToolDef { Id = id, Name = name, Cost = cost, Reach = reach, Area = area, Rate = rate, Desc = desc, Index = list.Count });
            T("fingers", "Fingers", 0, 2.7f, 0, 4f, "Ten of them. Slightly pruney.");
            T("grabber", "Litter Grabber", 2.5, 4.0f, 0, 5f, "The trash picker from the custodial closet. You feel professional.");
            T("net", "Pool Skimmer Net", 30, 4.2f, 0.45f, 3.5f, "Scoops everything in a small circle.");
            T("rake", "Coin Rake", 400, 4.6f, 0.8f, 3.5f, "A garden rake with its teeth bent inward. Very effective. Very ugly.");
            T("magnet", "Detector Magnet", 5000, 5.5f, 1.2f, 4f, "A metal detector welded to a salvage magnet. Beeps when happy.");
            T("blower", "Reverse Leaf Blower", 60000, 7.5f, 1.7f, 6f, "It sucks. That is the whole point.");
            T("glove", "Industrial Magnet Glove", 900000, 11f, 2.6f, 7f, "Pulls coins from across the fountain. Keep away from pacemakers and ATMs.");
            return list.ToArray();
        }

        static ToolDef[] BuildDigTools()
        {
            var list = new List<ToolDef>();
            void D(string id, string name, double cost, double power, float rate, string desc)
                => list.Add(new ToolDef { Id = id, Name = name, Cost = cost, DigPower = power, Rate = rate, Reach = 3.2f, Desc = desc, Index = list.Count });
            D("none", "Nothing", 0, 0, 0, "You'd need something to dig with.");
            D("sandshovel", "Sandbox Shovel", 12, 1, 1.6f, "Yellow plastic. Structurally a spoon.");
            D("snowshovel", "Snow Shovel", 160, 3, 1.5f, "Canadian-grade. Has survived worse than forty years of syrup.");
            D("pickaxe", "Pickaxe", 2000, 9, 1.4f, "For the Syrup Seal, which is technically a mineral now.");
            D("jackhammer", "Jackhammer", 25000, 28, 3f, "Loud enough that the food court complains.");
            D("borer", "Handheld Borer", 400000, 90, 3f, "It is not handheld. You are holding it anyway.");
            return list.ToArray();
        }

        // ───────────────────────────── jokes ─────────────────────────────

        /// <summary>Footer lines printed on COIN-O-MATIC 3000 receipts.</summary>
        public static readonly string[] ReceiptJokes =
        {
            "THANK YOU FOR YOUR BUSINESS. PLEASE DRY YOUR HANDS.",
            "COIN-O-MATIC 3000: COUNTING SINCE 1991. NEVER WRONG. ONCE.",
            "A 9% HANDLING FEE HAS NOT BEEN CHARGED. YOU'RE WELCOME.",
            "THIS RECEIPT IS NOT A WISH. PLEASE DO NOT THROW IT IN.",
            "DETECTED: 1 COIN, 3 HAIRS, 0 REGRETS.",
            "HAVE YOU TRIED OUR SISTER MACHINE, THE BILL-O-MATIC? IT IS BROKEN.",
            "YOUR COINS HAVE BEEN COUNTED. YOUR SINS HAVE NOT.",
            "PLEASE RATE YOUR COUNTING EXPERIENCE: 1 TO 3000.",
            "WARNING: COINS WERE WET. MACHINE IS NOW ALSO WET.",
            "FUN FACT: THE AVERAGE WISH WEIGHS 2.35 GRAMS.",
            "MALL MANAGEMENT IS AWARE OF YOU. MALL MANAGEMENT IS FINE WITH IT.",
            "COINSTAR IS A TRADEMARK. COIN-O-MATIC IS A LIFESTYLE.",
            "EXCELLENT CHOICE. THE MACHINE IS PROUD OF YOU.",
            "PRINTED ON 100% RECYCLED RECEIPTS FROM 1997.",
        };

        /// <summary>What the COIN-O-MATIC says when you try to deposit with empty hands.</summary>
        public static readonly string[] EmptyDepositLines =
        {
            "INSERT COINS. ANY COINS. PLEASE.",
            "THE COIN-O-MATIC CANNOT COUNT HOPES AND DREAMS. YET.",
            "YOUR HANDS ARE EMPTY. SO IS THE MACHINE'S HEART.",
        };

        /// <summary>Said when your hands are full and you try to grab more.</summary>
        public static readonly string[] FullHandsLines =
        {
            "Your hands are full. Go cash in at the COIN-O-MATIC.",
            "You physically cannot hold one more thing. Deposit first.",
            "Full! A bigger container would help. (Maintenance Terminal)",
        };
    }
}
