// Late-game content: each mall's Wonder (a staged megaproject hung over the fountain, built from
// the goods the factory makes) and the buried finds its crust gives up.
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        // ───────────────────────────── buried finds ─────────────────────────────

        static readonly (string mall, string name, ItemShape shape, uint color, float scale, string desc)[] FindTable =
        {
            ("crestview", "Time Capsule (Class of '94)", ItemShape.Cube, 0x8A9AA8, 2.4f, "Sealed at the grand opening, 'to be opened in 2044'. Close enough."),
            ("crestview", "Lisa Frank Lunchbox", ItemShape.Toaster, 0xE85FB0, 2.0f, "Metal, latched, and rattling. Something in there has been waiting since recess."),
            ("crestview", "Waterlogged Cash Register", ItemShape.Toaster, 0x3A3F44, 2.6f, "From the Sam Goody that flooded in '99. The drawer is jammed shut."),
            ("neongalaxy", "Arcade Token Vault", ItemShape.Cube, 0xFFD34D, 2.6f, "The prize counter's lockbox. The tickets inside are worthless. The tokens are not."),
            ("neongalaxy", "Factory-Sealed Pog Case", ItemShape.Cube, 0xB57CF2, 2.2f, "A whole case of Pogs and slammers. Somebody's 1995 retirement plan."),
            ("neongalaxy", "Mall Rat's Backpack", ItemShape.Bag, 0x3A7BD5, 2.0f, "JanSport, one strap, covered in band pins. Heavier than it looks."),
            ("aurelia", "Monogrammed Jewellery Box", ItemShape.Cube, 0x8A2A2A, 2.4f, "Velvet-lined and locked. Tossed during an especially dramatic divorce."),
            ("aurelia", "Locked Attaché Case", ItemShape.Wallet, 0x1A1A1A, 3.4f, "Handcuffed to nobody. The combination is probably 0000."),
            ("aurelia", "Wine Crate (Vintage 1961)", ItemShape.Cube, 0xA87A48, 2.8f, "The wine is fountain water now. The crate is still worth a fortune."),
            ("skyport", "Unclaimed Suitcase", ItemShape.Bag, 0x2F6FD6, 2.2f, "Lost in 1994, tagged for a flight that no longer exists."),
            ("skyport", "Duty-Free Crate", ItemShape.Cube, 0xE8E0C8, 2.8f, "Two hundred perfume samples and one very confused customs officer."),
            ("skyport", "Diplomatic Pouch", ItemShape.Bag, 0x2A2D34, 1.9f, "Stamped DO NOT OPEN in eleven languages. You open it."),
            ("luckylagoon", "Casino Drop Box", ItemShape.Cube, 0x2E8C4A, 2.6f, "The cash box from a craps table. Still full. Still locked."),
            ("luckylagoon", "High-Roller's Briefcase", ItemShape.Wallet, 0xD4AF37, 3.4f, "Gold-plated clasps. Inside: chips, a comb and an IOU to the house."),
            ("luckylagoon", "Slot Machine Hopper", ItemShape.Toaster, 0xD8283A, 2.4f, "The coin bucket from a slot machine that paid out once, in 1987, straight into the fountain."),
            ("eternity", "Roman Strongbox", ItemShape.Cube, 0x8A6A4A, 2.6f, "Bronze, riveted and very, very old. The lock is a riddle. The riddle is in Latin."),
            ("eternity", "Sealed Amphora", ItemShape.Trophy, 0xC8744A, 3.4f, "Two thousand years of wishes, corked with wax."),
            ("eternity", "Wishing Urn", ItemShape.Trophy, 0x5FE8F0, 3.0f, "It hums when you hold it. The wishes inside want out."),
        };

        /// <summary>The finds each mall's crust can give up (called after the per-mall item types).</summary>
        static void BuildFinds()
        {
            for (int m = 0; m < Malls.Length; m++)
            {
                var mall = Malls[m];
                var list = new List<int>();
                foreach (var f in FindTable)
                {
                    if (f.mall != mall.Id) continue;
                    var t = AddType($"{mall.Id}_find{list.Count}", f.name, ItemCat.Find, f.shape, f.color, 0, f.scale, f.desc);
                    t.Mall = m;
                    t.Rarity = Rarity.Epic;
                    list.Add(t.Index);
                }
                mall.FindTypes = list.ToArray();
            }
        }

        // ───────────────────────────── Wonders ─────────────────────────────

        sealed class WonderPlan
        {
            public int MallIndex;
            public MallDef Mall;
            public WonderDef Def;
            public readonly List<int> Stages = new List<int>();
        }

        /// <summary>Every mall's Wonder and its stages (called from BuildTechs; stages are TechDefs in TechBranch.Wonder).</summary>
        static void AddWonders(System.Func<string, string, TechBranch, TechKind, double, double, string, string[], TechDef> T)
        {
            WonderPlan NewWonder(string mallId, string name, string desc, ItemShape crown, uint crownColor, ItemShape charm, uint charmColor,
                         TechKind perk, double perkValue)
            {
                int mi = System.Array.FindIndex(Malls, x => x.Id == mallId);
                var def = new WonderDef
                {
                    Name = name, Desc = desc, Crown = crown, CrownColor = crownColor, Charm = charm, CharmColor = charmColor,
                    PerkKind = perk, PerkValue = perkValue,
                };
                def.PerkText = Sim.RewardText(perk, perkValue) + " in every mall, forever";
                Malls[mi].Wonder = def;
                return new WonderPlan { MallIndex = mi, Mall = Malls[mi], Def = def };
            }
            void Stage(WonderPlan w, string name, string desc, TechKind kind, double value, double fee, params (string id, int count)[] needs)
            {
                int n = w.Stages.Count;
                string id = $"wonder_{w.Mall.Id}_{n + 1}";
                var t = T(id, name, TechBranch.Wonder, kind, value, fee, desc, n > 0 ? new[] { $"wonder_{w.Mall.Id}_{n}" } : new string[0]);
                t.OnlyMall = w.MallIndex;
                t.Needs = needs;
                t.Col = n;
                t.Row = w.MallIndex;
                w.Stages.Add(t.Index);
                w.Def.Stages = w.Stages.ToArray();
            }

            // 1. Crestview: the crowd's junk and the first coin rolls
            var cv = NewWonder("crestview", "The Penny Chandelier",
                "Forty years of the fountain's pennies, strung on fishing line and hung from the skylight. The mall's first new attraction since the escalator.",
                ItemShape.Coin, 0xC77B43, ItemShape.Coin, 0xC77B43, TechKind.ValueMult, 0.10);
            Stage(cv, "Hang the Frame", "A brass hoop from the old carousel, hung on four cables. Shoppers look up for the first time in years.",
                TechKind.TossRate, 0.25, 20, ("duck", 3), ("keys", 2), ("relic", 1));
            Stage(cv, "String the Pennies", "Ten thousand pennies on fishing line. It tinkles whenever the air conditioning kicks in.",
                TechKind.ValueMult, 0.30, 2000, ("roll", 40), ("relic", 4), ("tokens", 20));
            Stage(cv, "Light It Up", "Fairy lights, a disco motor and a giant copper penny on top. The food court gives it a standing ovation.",
                TechKind.DigPower, 0.50, 50000, ("bag", 8), ("teeth", 3), ("tokens", 60));

            // 2. Neon Galaxy: wishes (and what's left of the ones you don't catch)
            var ng = NewWonder("neongalaxy", "The Mirrorball of Tomorrow",
                "A disco ball the size of a minivan, tiled with arcade tokens and pressed wishes. It spins at exactly 33⅓ rpm.",
                ItemShape.Ball, 0xE8ECF2, ItemShape.Coin, 0xFFD34D, TechKind.WishValue, 0.50);
            Stage(ng, "Rig the Motor", "A turntable motor from the roller rink, bolted to the skylight. It whirs. It believes.",
                TechKind.WishLife, 0.50, 2000, ("tokens", 40), ("roll", 30));
            Stage(ng, "Tile It in Tokens", "Every tile a token, every token a wish somebody made in 1986.",
                TechKind.ValueMult, 0.40, 2e8, ("brick", 20), ("bag", 15));
            Stage(ng, "Hit the Lights", "Lasers, a smoke machine and the mirrorball. The whole mall turns into a roller disco.",
                TechKind.DigPower, 0.60, 5e9, ("pallet", 4), ("relic_epic", 2), ("tokens", 300));

            // 3. Aurelia: the finest relics the sorters can find
            var ga = NewWonder("aurelia", "The Chandelier of Unnecessary Diamonds",
                "Crystal, platinum and a centrepiece diamond the size of a beach ball. Appraised at 'yes'.",
                ItemShape.Diamond, 0xDFF4FF, ItemShape.Gem, 0xE8F4FF, TechKind.RelicRate, 0.50);
            Stage(ga, "Hang the Crystal Frame", "Platinum hoops, crystal drops and a velvet rope so nobody touches it.",
                TechKind.RelicRate, 1.00, 4e5, ("relic_rare", 3), ("bag", 8));
            Stage(ga, "Cut the Diamonds", "Two hundred diamonds that people threw in, recut and hung point-down. (Keep them away from the Gold Melter.)",
                TechKind.ValueMult, 0.50, 1.6e10, ("diamond", 200), ("relic_epic", 3), ("bag", 60));
            Stage(ga, "The Hope-ish Centrepiece", "One enormous blue diamond. Allegedly only slightly cursed.",
                TechKind.DigPower, 1.00, 4e11, ("relic_legendary", 2), ("pallet", 6));

            // 4. Skyport: cargo
            var sp = NewWonder("skyport", "The Departures Mobile",
                "Every suitcase that ever missed its flight, hung from the skylight like a baby's crib mobile. It turns with the departures board.",
                ItemShape.Ball, 0x2F6FD6, ItemShape.Bag, 0x8A6A4A, TechKind.FindRate, 0.25);
            Stage(sp, "Luggage Carousel", "The old baggage carousel, hung upside down. It still goes round.",
                TechKind.DigPower, 0.50, 1e9, ("bag", 40), ("phone", 25));
            Stage(sp, "Hang the Suitcases", "Four hundred lost suitcases on steel cable. Some of them are ticking. (Alarm clocks.)",
                TechKind.ValueMult, 0.50, 2.5e11, ("pallet", 10), ("bowling", 50));
            Stage(sp, "The Globe of Departures", "A spinning globe with every destination anyone ever wished for.",
                TechKind.FindRate, 1.00, 5e12, ("pallet", 8), ("relic_legendary", 3), ("tokens", 400));

            // 5. The Lucky Lagoon: gold
            var ll = NewWonder("luckylagoon", "The Wheel of Fountain Fortune",
                "A casino prize wheel forty feet across, rimmed with gold bars. Every spin, somebody wins. Usually you.",
                ItemShape.Chip, 0xE84F4F, ItemShape.Bar, 0xF2C230, TechKind.FrenzyTime, 0.50);
            Stage(ll, "Spin Up the Wheel", "A slot machine motor and a wheel of fortune from a game show that was cancelled on air.",
                TechKind.FrenzyTime, 1.00, 4e8, ("bar", 5000), ("vending", 15));
            Stage(ll, "Rim It in Gold", "Twenty-five thousand gold bars around the rim. The wheel is now technically a bank.",
                TechKind.ValueMult, 0.50, 3e11, ("bar", 25000), ("pallet", 15));
            Stage(ll, "Jackpot Lights", "Ten thousand bulbs, a siren and a voice that says JACKPOT every few minutes whether or not there is one.",
                TechKind.DigPower, 1.00, 5e12, ("relic_legendary", 3), ("pallet", 10), ("tokens", 500));

            // 6. Eternity Plaza: everything, and every wish
            var ep = NewWonder("eternity", "The Wishing Star",
                "Every wish ever made in every fountain, pressed into one star and hung where the first coin fell.",
                ItemShape.Gem, 0x9FF0FF, ItemShape.Brick, 0x9A5CF7, TechKind.ValueMult, 0.25);
            Stage(ep, "Gather the Wishes", "A net of light, hung under the skylight to catch the wishes on their way up.",
                TechKind.WishValue, 0.50, 3e8, ("brick", 60), ("tokens", 500));
            Stage(ep, "Forge the Points", "Five points of gold, each one forged from four thousand bars and three moon rocks.",
                TechKind.ValueMult, 0.60, 3e9, ("pallet", 18), ("bar", 20000), ("moonrock", 15));
            Stage(ep, "Light the First Star", "The star catches fire with every wish at once. Somewhere, a first coin lands again.",
                TechKind.DigPower, 1.50, 1e12, ("relic_legendary", 4), ("pallet", 12), ("tokens", 600));
        }
    }
}
