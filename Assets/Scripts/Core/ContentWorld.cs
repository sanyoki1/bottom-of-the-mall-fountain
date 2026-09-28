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
            Coin("silver", "Silver Dollar", 5.00, 0xE6EAEE, 0.60f, 55, "A collector's coin, tossed by someone who is not a collector.");
            Coin("gold", "Gold Coin", 20.0, 0xF2C230, 0.60f, 85, "Real gold. The person who threw it wished for 'a sign'.");
            var d = AddType("diamond", "Diamond", ItemCat.Coin, ItemShape.Diamond, 0xDFF4FF, 100, 0.55f, "A loose diamond. Apparently people just have these.");
            d.Tier = CoinTiers.Count;
            CoinTiers.Add(d.Index);
            CoinTierMinWish.Add(120);
        }

        /// <summary>The ridiculous things people throw once the fountain is fancy enough.</summary>
        static void BuildOddities()
        {
            // not oddities proper: thrown by particular archetypes regardless of the fountain's fanciness
            AddType("gum", "Chewed Gum", ItemCat.Junk, ItemShape.Wad, 0xF28DB2, 0, 0.8f, "Worth nothing. Sticks to everything. A teenager's wish, basically.");
            AddType("foreign", "Foreign Coin", ItemCat.Coin, ItemShape.Coin, 0xB08D57, 0.40, 0.46f, "From a country with a very confident-looking bird on its money.");
            void O(string id, string name, double value, ItemShape shape, uint color, float scale, double minWish, Rarity r, string desc, bool heavy = false)
            {
                var t = AddType(id, name, ItemCat.Oddity, shape, color, value, scale, desc);
                t.Rarity = r;
                t.Heavy = heavy;
                Oddities.Add(t.Index);
                OddityMinWish.Add(minWish);
            }
            // the silly early ones are worth about a coin of their tier; the truly ridiculous ones come after diamonds
            O("duck", "Rubber Duck", 0.50, ItemShape.Duck, 0xFFD23A, 1.0f, 18, Rarity.Common, "Squeaks with quiet disapproval.");
            O("shoe", "Toddler Shoe", 0.75, ItemShape.Shoe, 0xFF7AA8, 1.0f, 20, Rarity.Common, "Single. Velcro. Light-up. Still blinking.");
            O("goldfish", "Live Goldfish", 0, ItemShape.Fish, 0xFF8A20, 2.0f, 22, Rarity.Uncommon, "Won at a fair, released 'into the wild'. Must be returned to the water. Immediately.");
            O("keys", "Car Keys", 2, ItemShape.Keys, 0xC9CDD2, 1.0f, 25, Rarity.Common, "They were in the wrong pocket the whole time. Now they're in the fountain.");
            O("teeth", "Dentures", 3, ItemShape.Teeth, 0xF4EEE0, 1.0f, 28, Rarity.Uncommon, "Grandma laughed so hard at the mime.");
            O("timeshare", "Timeshare Contract", 0.05, ItemShape.Paper, 0xE8E0C8, 1.4f, 32, Rarity.Uncommon, "Legally binding. Nobody, not even the fountain, can escape it.");
            O("phone", "Entire Phone (Mid-Call)", 10, ItemShape.Phone, 0x2A2D34, 1.0f, 45, Rarity.Uncommon, "Somebody's mom is still on the line. She says hi.");
            O("wallet", "Businessman's Wallet", 12, ItemShape.Wallet, 0x4A3222, 1.0f, 50, Rarity.Uncommon, "$214, a gym card, and a photo of a boat. Most of the $214 is Monopoly money.");
            O("toaster", "Toaster", 6, ItemShape.Toaster, 0xD8DCE0, 1.0f, 55, Rarity.Uncommon, "Still warm. Please don't.");
            O("bowling", "Bowling Ball", 15, ItemShape.Ball, 0x3A2A7A, 1.0f, 65, Rarity.Rare, "Sixteen pounds of 'I'm done with this league'.", true);
            O("trophy", "Bowling Trophy", 12, ItemShape.Trophy, 0xE8B83A, 1.0f, 70, Rarity.Rare, "2nd place, 1997 regionals. Thrown out of spite.");
            O("ring", "Wedding Ring (Mid-Argument)", 40, ItemShape.Gem, 0xF0D060, 1.3f, 80, Rarity.Rare, "Thrown with remarkable accuracy.");
            O("beanie", "Beanie Baby", 30, ItemShape.Wad, 0x6A4FA8, 1.6f, 90, Rarity.Rare, "Tag intact. Hopes intact.");
            O("seed", "Crypto Seed Phrase (On a Napkin)", 120, ItemShape.Paper, 0xF6F2EA, 1.2f, 110, Rarity.Epic, "Twelve words and a ketchup stain. Might be millions. Might be a grocery list.");
            O("lottery", "Lottery Ticket (Five Numbers)", 200, ItemShape.Paper, 0xFFD34D, 1.1f, 125, Rarity.Epic, "Five numbers match. The sixth is under the gum.");
            O("fridge", "Smart Fridge", 150, ItemShape.Cube, 0xE8ECEF, 4.2f, 140, Rarity.Epic, "It has been texting the fountain. The fountain has not replied.", true);
            O("vending", "Vending Machine", 250, ItemShape.Vending, 0xD8283A, 1.0f, 150, Rarity.Epic, "A bodybuilder wanted a Diet Coke. It wanted to stay.", true);
            O("goldbar", "Gold Bar", 600, ItemShape.Bar, 0xF2C230, 1.2f, 165, Rarity.Legendary, "Tossed by a billionaire who 'wanted to feel something'.");
            O("tiara", "Pageant Tiara", 900, ItemShape.Gem, 0xE8F4FF, 1.8f, 180, Rarity.Legendary, "The pageant is over. The pageant is never over.");
            O("moonrock", "Moon Rock (Probably)", 2000, ItemShape.Chunk, 0x9A9A9A, 1.0f, 195, Rarity.Legendary, "Certificate of authenticity signed 'An Astronaut'.");
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
            C("pail", "Sand Pail", 20, 3, "Red plastic. Came free with a kids' meal in 1996.");
            C("bucket", "Mop Bucket", 60, 10, "Borrowed from the custodial closet. The mop is staying.");
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

        // ───────────────────────────── shoppers ─────────────────────────────

        static ArchetypeDef[] BuildArchetypes()
        {
            var list = new List<ArchetypeDef>();
            ArchetypeDef A(string id, string name, double minWish, double weight, double bias, uint shirt, uint pants, uint hat, string prop, string[] barks, params string[] odd)
            {
                var a = new ArchetypeDef
                {
                    Id = id, Name = name, MinWish = minWish, Weight = weight, TierBias = bias, Shirt = shirt, Pants = pants, Hat = hat,
                    Skin = 0xE0B08A, Prop = prop, Barks = barks, Oddities = odd, Index = list.Count,
                };
                list.Add(a);
                return a;
            }
            var walker = A("walker", "Mall Walker", 0, 5, 0, 0x7A3FC8, 0x3A2A7A, 0xFF4FA8, null, new[]
            {
                "Lap 43! A penny for luck!", "Keep those knees up, Doris!", "One penny per lap. That's the rule.",
                "Power walking is a lifestyle.", "I've walked to Winnipeg and back in this mall.",
            });
            walker.Headband = true; walker.Speed = 1.9f; walker.Tosses = 2;
            var teen = A("teen", "Bored Teen", 0, 3, 0.3, 0x2A2A30, 0x3A5A8A, 0xD8283A, "phone", new[]
            {
                "ugh this mall is so dead", "i wish i was literally anywhere else", "*chews loudly*", "is this, like, a vibe?",
                "my mom gave me a quarter. for WHAT", "no cap this fountain is mid",
            });
            teen.JunkChance = 0.3; teen.Speed = 1.1f;
            var grandma = A("grandma", "Grandma", 3, 2, 0.25, 0xB86A8A, 0x6A6A78, 0xD8D8D8, "cane", new[]
            {
                "In my day this fountain had fish!", "This one's for my grandson's braces.", "Ohh, I've dropped my... never mind.",
                "Forty years I've been wishing for a nicer husband.", "Is that young man in the fountain allowed to be in there?",
            }, "teeth");
            grandma.Speed = 0.8f; grandma.Tosses = 2;
            var toddler = A("toddler", "Toddler (Unsupervised)", 5, 2, 0, 0xFFD23A, 0x3A7BD5, 0xFF7AA8, "balloon", new[]
            {
                "MINE!", "Penny go SPLASH!", "*throws shoe*", "Mommy I wished for a DINOSAUR", "WAAAAH", "again! AGAIN!",
            }, "shoe", "duck", "goldfish");
            toddler.Scale = 0.55f; toddler.Speed = 1.0f; toddler.OddityBoost = 3; toddler.Tosses = 3;
            var suit = A("business", "Businessman", 10, 2, 0.8, 0x2A3140, 0x222630, 0x2A2A2A, "briefcase", new[]
            {
                "Synergy.", "I'll expense it.", "Let's circle back to this wish.", "Per my last wish...",
                "This fountain has great ROI.", "Can this wish be a meeting? No? Fine.",
            }, "wallet", "phone", "keys");
            suit.Speed = 1.6f;
            var tourist = A("tourist", "Tourist", 15, 2, 0.5, 0x2FA8C8, 0xE8D8B0, 0xF2E6C8, "camera", new[]
            {
                "Is this the famous fountain?", "Honey, take a picture of me wishing!", "How much is this in real money?",
                "Our guidebook said this was a must-see.", "Do they have fountains like this back home? No. No they do not.",
            }, "foreign");
            tourist.OddityBoost = 4;
            var influencer = A("influencer", "Influencer", 35, 1.5, 0.6, 0xFF7AA8, 0xF4F0EA, 0xFFE0F0, "selfie", new[]
            {
                "Hey besties, wish with me!", "Don't forget to like and subscribe to this fountain!", "Wait, the lighting's wrong. Again.",
                "Take 47. And... wish!", "#blessed #fountaincore #nofilter",
            }, "phone");
            influencer.OddityBoost = 2;
            var proposer = A("proposer", "Heartbroken Proposer", 65, 1, 1.0, 0x1D1D22, 0x1D1D22, 0x1D1D22, "rose", new[]
            {
                "Will you... okay. Never mind.", "She said she needs 'space'. The fountain has space.", "It was a promise ring. I'm keeping the promise.",
                "I rented a flash mob. They're on their break.",
            }, "ring");
            proposer.OddityBoost = 6;
            var gym = A("bodybuilder", "Bodybuilder", 120, 1, 1.5, 0xE84F4F, 0x2A2A2A, 0x2A2A2A, "dumbbell", new[]
            {
                "DO YOU EVEN WISH, BRO?", "Leg day tomorrow. Wish me luck.", "HNNNGH!", "Protein. I wished for protein.",
            }, "vending", "bowling", "fridge");
            gym.Scale = 1.2f; gym.OddityBoost = 3;
            var rich = A("billionaire", "Billionaire", 150, 0.6, 3.0, 0x1A1A1A, 0x1A1A1A, 0x1A1A1A, "tophat", new[]
            {
                "Money can't buy happiness. It CAN buy fountains.", "Keep the change. All of it.", "I own the mall. And this fountain. And you.",
                "I wished for a second moon. Let's see.", "Pocket lint. Diamonds. Same thing.",
            }, "goldbar", "tiara", "moonrock", "diamond");
            rich.OddityBoost = 4; rich.Tosses = 3;
            return list.ToArray();
        }

        /// <summary>Said by anyone when they have nothing specific to say.</summary>
        public static readonly string[] GenericBarks =
        {
            "For luck!", "Here goes nothing.", "Make it count!", "Please please please...", "Heads I win.",
            "Is someone... in the fountain?", "Ooh, it sparkles now!",
        };

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
            "TODAY'S LUCKY NUMBER IS 0.01.",
            "THE COIN-O-MATIC HAS SEEN WHAT YOU DID IN THAT FOUNTAIN.",
            "ONE OF THESE COINS WAS A WISH. WE ARE LEGALLY OBLIGATED TO SAY NOTHING.",
            "REMEMBER: IT'S NOT STEALING IF THE MALL SAYS IT'S FINE.",
            "YOU HAVE DEPOSITED MORE THAN THE FOOD COURT MADE TODAY.",
            "THIS MACHINE IS 30 YEARS OLD AND STILL SMARTER THAN THE PARKING METERS.",
            "BEEP BOOP. (THAT'S COIN-O-MATIC FOR 'THANK YOU'.)",
            "PLEASE DO NOT FEED THE MACHINE GUM. IT HAS TRIED IT. IT DID NOT LIKE IT.",
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
