// The six malls: strata, loot, wishes, relics, bottom treasures and themes.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        static StratumDef[] Strata(params (string name, bool loose, uint color, uint speck, string flavor)[] layers)
        {
            var list = new StratumDef[layers.Length];
            for (int i = 0; i < layers.Length; i++)
            {
                var l = layers[i];
                list[i] = new StratumDef(l.name, Balance.StratumStart[i], Balance.StratumValue[i], l.loose, l.color, l.speck, l.flavor);
            }
            return list;
        }

        /// <summary>
        /// Total crust (scoops from the top to bare concrete) per mall, fitted by Tools/BalanceSim "fit"
        /// so the engaged bot takes the planned hours in each mall. Regenerate after any balance change.
        /// Zero falls back to the CrustScoops placeholder in the mall's definition.
        /// </summary>
        static readonly double[] FittedScoops =
        {
            // <fitted-scoops> (written by: dotnet run -c Release --project Tools/BalanceSim -- fit --apply)
            203300, 8700400, 29663400, 117892300, 235027100, 546179600,
            // </fitted-scoops>
        };

        /// <summary>Fallback: a loose layer of looseScoops, then geometric growth down to total.</summary>
        public static double[] DefaultBounds(double total, double looseScoops, int strata)
        {
            var b = new double[strata + 1];
            b[0] = 0;
            b[1] = looseScoops;
            double ratio = Math.Pow(total / looseScoops, 1.0 / (strata - 1));
            for (int i = 2; i <= strata; i++) b[i] = b[i - 1] * ratio;
            b[strata] = total;
            return b;
        }

        /// <summary>Resize a mall's crust (the balance fitter calls this between runs).</summary>
        public static void SetCrust(MallDef mall, double scoops)
        {
            mall.CrustScoops = Math.Max(Balance.LooseLayerScoops * 3, scoops);
            mall.Bounds = DefaultBounds(mall.CrustScoops, Balance.LooseLayerScoops, mall.Strata.Length);
        }

        static WishDef W(Rarity r, double v, string text) => new WishDef(r, v, text);
        static RelicDef R(Rarity r, double v, string name, string desc, ItemShape s, uint c) => new RelicDef(r, v, name, desc, s, c);
        static ItemKind I(string name, double v, double w, ItemShape s, uint c) => new ItemKind(name, v, w, s, c);

        const Rarity C = Rarity.Common, U = Rarity.Uncommon, Ra = Rarity.Rare, E = Rarity.Epic, L = Rarity.Legendary;

        static MallDef[] BuildMalls()
        {
            var malls = new List<MallDef>();

            // ─────────────────────────────── 1. Crestview Commons ───────────────────────────────
            malls.Add(new MallDef
            {
                Id = "crestview",
                Name = "Crestview Commons",
                Tagline = "The Suburban Dead Mall — now 40% leased!",
                Intro = "A dry, cracked, 30-foot concrete basin packed solid with forty years of pennies, gum and calcified mall. You have a piece of chewed bubblegum on a string. Good luck.",
                DepthFeet = 30,
                CrustScoops = 6e4,
                LuckyPennies = 6,
                TreasureName = "The Founder's Penny",
                TreasureDesc = "A 1985 penny super-glued to the drain by the mall's founder, labeled FOR LUCK. It did not work.",
                Event = new MallEventDef("Mall Walker Rush Hour", "toss", 3, "Tracksuits everywhere. Coins fly into the fountain three times as often!"),
                Strata = Strata(
                    ("Fresh Toss Layer", true, 0x8C8A6A, 0xD99A5B, "Pennies tossed this week. Still shiny. Still worthless."),
                    ("The Syrup Seal", false, 0xC8742E, 0xF2C14E, "A glossy crust of Orange Julius, Mountain Dew and regret. Everything below here is glued together."),
                    ("Food Court Silt", false, 0xA5835E, 0xEBD9B4, "Cinnabon crumbs compressed into sedimentary rock. Smells incredible. Do not lick."),
                    ("Smartphone Sludge", false, 0x5E6670, 0xB8D8F0, "Cracked screen protectors, earbud tips and a decade of selfie-distracted change."),
                    ("Y2K Lacquer", false, 0x9C8FC9, 0xF7F2FF, "Frosted-tip hair gel and body glitter, fossilized into a shimmering shell."),
                    ("Beanie Baby Boom Layer", false, 0x7A6FA8, 0xFFD166, "Tags still attached. Hopes still high. Retirement plans still pending."),
                    ("The Pre-90s Stratum", false, 0x4F6B4A, 0xC0C8C0, "Wheat pennies. Silver dimes. Dark green grime. The crust is finally old enough to be worth something."),
                    ("Grand Opening Grime", false, 0x3E4A3A, 0xE8C66A, "Ribbon-cutting confetti and the very first coins ever tossed in. The concrete is close.")),
                Items = new[]
                {
                    I("Zinc Penny", 0.01, 62, ItemShape.Coin, 0xC77B43),
                    I("Nickel", 0.05, 10, ItemShape.Coin, 0xB9BDC2),
                    I("Dime", 0.10, 7, ItemShape.Coin, 0xD4D8DC),
                    I("Quarter", 0.25, 5, ItemShape.Coin, 0xC9CDD2),
                    I("Gum Wad", 0, 6, ItemShape.Wad, 0xF28DB2),
                    I("Bendy Straw", 0, 4, ItemShape.Stick, 0xE8505B),
                    I("Cinnabon Crumb", 0, 3, ItemShape.Wad, 0xC9955C),
                    I("Mall Map", 0, 2, ItemShape.Paper, 0xF1E9D2),
                    I("Bobby Pin", 0, 1, ItemShape.Stick, 0x33302E),
                },
                Wishes = new[]
                {
                    W(C, 2, "I wish for a soft pretzel. The big one."),
                    W(C, 3, "I wish the Sbarro guy would notice me."),
                    W(C, 3, "I wish Mom would buy me the light-up sneakers."),
                    W(C, 2, "I wish this penny was a quarter."),
                    W(C, 4, "I wish Blockbuster had one more copy of Titanic."),
                    W(C, 3, "I wish I could remember where we parked."),
                    W(C, 3, "I wish the Orange Julius lady remembered my order."),
                    W(C, 5, "I wish for a pager so people think I'm a doctor."),
                    W(U, 10, "I wish I was tall enough for the carousel's cool horse."),
                    W(U, 12, "I wish my frosted tips looked like Justin's."),
                    W(U, 10, "I wish Sam Goodie had the clean version."),
                    W(U, 15, "I wish food court Santa was the real one."),
                    W(U, 15, "I wish my Tamagotchi would come back to life."),
                    W(U, 20, "I wish the Sharper-ish Image massage chair was mine."),
                    W(Ra, 50, "I wish my crush likes my neon windbreaker."),
                    W(Ra, 50, "I wish I get slimed on Kids' Choice Awards."),
                    W(Ra, 60, "I wish my band gets to play the food court stage."),
                    W(Ra, 75, "I wish my Glamour Shotz photo gets me a modeling contract."),
                    W(E, 100, "I wish Beanie Babies pay for my college."),
                    W(E, 150, "I wish this mall stays open forever."),
                    W(E, 250, "I wish I'd bought Apple stock instead of this Discman."),
                    W(L, 1000, "I wish someone, someday, digs all these wishes back up."),
                },
                Relics = new[]
                {
                    R(C, 15, "Mall Map (You Are Here: 1997)", "The YOU ARE HERE star has been rubbed off by ten thousand fingers.", ItemShape.Paper, 0xF1E9D2),
                    R(C, 20, "Food Court Tray", "Still sticky. Structurally, it is mostly soda.", ItemShape.Paper, 0xB5563C),
                    R(C, 25, "Orange Julep Souvenir Cup", "Limited edition. Unlimited supply.", ItemShape.Cube, 0xF29A3A),
                    R(U, 60, "Pog Slammer (Skull Edition)", "Banned at three separate middle schools.", ItemShape.Chip, 0x2B2B2B),
                    R(U, 40, "Lost Retainer", "Somewhere, an orthodontist is still furious.", ItemShape.Wad, 0xE9A8B8),
                    R(U, 80, "Tamagotchi (Deceased)", "Cause of death: neglect during the 1998 school year.", ItemShape.Gem, 0x7FC8E8),
                    R(Ra, 300, "Class Ring (Crestview High '94)", "Go Fighting Escalators!", ItemShape.Gem, 0xD4AF37),
                    R(Ra, 200, "Cinna-Buns Punch Card (1 Stamp Away)", "So close. So very, very close.", ItemShape.Paper, 0xE8D1A0),
                    R(Ra, 250, "Pager With 1 Unread Message", "The message is 143. It has been waiting since 1996.", ItemShape.Cube, 0x3A3F44),
                    R(E, 2500, "Diamond Engagement Ring", "Thrown in after a food court argument. Retrieved 29 years later.", ItemShape.Gem, 0xE8F4FF),
                    R(E, 1500, "Signed Mall Santa Beard", "Authenticated by three elves and one security guard.", ItemShape.Wad, 0xFFFFFF),
                    R(L, 10000, "Mint Princess Bear Beanie Baby", "Tag protector intact. Finally, the college fund.", ItemShape.Wad, 0x6A4FA8),
                },
                Theme = new ThemeDef
                {
                    FloorA = 0xE8D5C0, FloorB = 0xD9A98F, BasinTileA = 0x2E9C95, BasinTileB = 0xE7F0EC,
                    Rim = 0xE3B4A0, Wall = 0xB7A7B5, WallTrim = 0x5FA8A0, Ceiling = 0xD8D2C8, Pillar = 0xE9E1D3,
                    LightColor = 0xFFF3D6, AmbientSky = 0xBFD8D4, AmbientEquator = 0xB9A89A, AmbientGround = 0x6E5A4E,
                    FogColor = 0xCFC6BA, Accent = 0x2BB3A6, NeonA = 0x39E5D0, NeonB = 0xFF7AA8, Plant = 0x6C8F4E,
                    LightIntensity = 1.05f, FogDensity = 0.0040f, NeonStrength = 1.2f,
                    Signs = new[] { "PRETZEL TIME", "SAM GOODIE", "SPACE AVAILABLE", "CINNA-BUNS", "GLAMOUR SHOTZ", "KB TOYZ", "ORANGE JULEP", "SHARPER-ISH IMAGE" },
                    SignColors = new uint[] { 0xFF7AA8, 0x39E5D0, 0xE8E0D0, 0xFFB35A, 0xFF7AA8, 0x5AC8FF, 0xFF9A3A, 0x39E5D0 },
                    MusicRoot = 62, MusicTempo = 76, MusicMode = 0,
                },
            });

            // ─────────────────────────────── 2. Neon Galaxy Mega-Mall ───────────────────────────────
            malls.Add(new MallDef
            {
                Id = "neongalaxy",
                Name = "Neon Galaxy Mega-Mall",
                Tagline = "The 1980s Mega-Mall — totally radical since 1983",
                Intro = "Four storeys of neon, a laser show that has not been switched off since 1989, and a 45-foot fountain full of slap bracelets, Walkmans and seriously valuable vintage tokens.",
                DepthFeet = 45,
                CrustScoops = 1.5e5,
                LuckyPennies = 14,
                TreasureName = "Golden Arcade Token #0001",
                TreasureDesc = "The first token this mall ever dispensed. Good for one free game of eternity.",
                Event = new MallEventDef("Neon Hour", "wish", 3, "The lights go neon. Wishes float up three times as often!"),
                Strata = Strata(
                    ("Fresh Toss (Totally Tubular)", true, 0x5A4F6E, 0xFFE066, "Coins tossed by nostalgic tourists last weekend. Most of them are wearing the shirt."),
                    ("Aqua Net Varnish", false, 0x8FD6E8, 0xFFFFFF, "A hairspray lacquer so strong it survived three renovations and one small fire."),
                    ("Arcade Token Tar", false, 0x23202B, 0xE8C04A, "Tokens and Tab soda, bonded into a jet-black paste."),
                    ("Slap Bracelet Shale", false, 0xE84FB3, 0x7CF45A, "Layer after layer of neon slap bracelets, still snapping shut on anything that touches them."),
                    ("Cassette Tape Tangle", false, 0x3A2E2A, 0xB08B5A, "Miles of unspooled mixtapes. Somewhere in here is the perfect breakup song."),
                    ("Leg Warmer Lint", false, 0xF28CC2, 0x57E0F0, "Fluffy, pastel, and load-bearing."),
                    ("Rubik's Rubble", false, 0x2E7AD1, 0xF7D51D, "Nobody down here ever solved one. The stickers were peeled off and swapped."),
                    ("Grand Opening Laser Show Layer", false, 0x3B1D5E, 0xFF3F3F, "1983. The lasers were aimed straight into the fountain. Everything still faintly glows.")),
                Items = new[]
                {
                    I("Penny", 0.01, 20, ItemShape.Coin, 0xC77B43),
                    I("Quarter", 0.25, 14, ItemShape.Coin, 0xC9CDD2),
                    I("Arcade Token", 0.50, 22, ItemShape.Coin, 0xB08D57),
                    I("Vintage Arcade Token", 3.00, 8, ItemShape.Coin, 0xE0B040),
                    I("Slap Bracelet", 2.00, 8, ItemShape.Stick, 0xFF4FD8),
                    I("Scrunchie", 0.50, 8, ItemShape.Wad, 0x57E0F0),
                    I("Cassette Tape", 5.00, 5, ItemShape.Cube, 0x2A2A2A),
                    I("Walkman", 35.00, 1, ItemShape.Cube, 0x3A7BD5),
                    I("Friendship Pin", 0, 8, ItemShape.Stick, 0xFFD34D),
                    I("Gum Wrapper", 0, 6, ItemShape.Paper, 0xE8E8E8),
                },
                Wishes = new[]
                {
                    W(C, 40, "I wish for the high score on Galaga."),
                    W(C, 40, "I wish my Walkman batteries last the whole bus ride."),
                    W(C, 50, "I wish Mom buys the real sneakers, not the ones with four stripes."),
                    W(C, 50, "I wish for a Cabbage Patch Kid. Any Cabbage Patch Kid."),
                    W(C, 60, "I wish my perm holds until the dance."),
                    W(C, 40, "I wish I could beam up out of algebra class."),
                    W(U, 160, "I wish my mixtape makes her fall in love with me."),
                    W(U, 180, "I wish MTV plays my cousin's music video."),
                    W(U, 200, "I wish I could moonwalk like Michael."),
                    W(U, 240, "I wish for a car phone. With the curly cord."),
                    W(U, 160, "I wish the laser tag arena was open every single day."),
                    W(Ra, 800, "I wish my Rubik's Cube would solve itself."),
                    W(Ra, 1000, "I wish Dad doesn't find out I taped over the wedding video."),
                    W(Ra, 900, "I wish I was the first kid on the block with a VCR."),
                    W(E, 3000, "I wish I was in a John Hughes movie."),
                    W(E, 4000, "I wish the eighties would never, ever end."),
                    W(L, 24000, "I wish my DeLorean worked. For reasons."),
                },
                Relics = new[]
                {
                    R(C, 200, "Lite-Brite Peg", "Tiny. Glowing. Found exclusively by bare feet.", ItemShape.Stick, 0xFF3FD0),
                    R(C, 160, "Neon Slap Bracelet (Still Slaps)", "Snapped onto your wrist the moment you picked it up. You live here now.", ItemShape.Stick, 0x7CF45A),
                    R(C, 240, "Scratch-and-Sniff Sticker Sheet", "Grape. It still smells like grape. How.", ItemShape.Paper, 0xB57CF2),
                    R(U, 1200, "Walkman (Mixtape Still Inside)", "Side B: 'SONGS 4 JENNIFER'. Side A: also songs for Jennifer.", ItemShape.Cube, 0x3A7BD5),
                    R(U, 800, "Garbage Pail Kids Card", "Adam Bomb. The holy grail of the lunch table.", ItemShape.Paper, 0xE84F4F),
                    R(U, 1000, "Pac-Man High Score Tag", "'AAA' — 3,333,360. Perfect game.", ItemShape.Paper, 0xFFE066),
                    R(Ra, 3000, "Jelly Shoe (Left)", "The right one is in a different fountain. Probably.", ItemShape.Wad, 0xFF6FA8),
                    R(Ra, 5000, "Solved Rubik's Cube", "Solved legitimately. Probably.", ItemShape.Cube, 0x2E7AD1),
                    R(Ra, 6000, "Boombox", "Held over a head outside a window exactly once.", ItemShape.Cube, 0x444444),
                    R(E, 24000, "Nintendo Cartridge (Blown Into Once)", "It works now. It always works after you blow into it.", ItemShape.Cube, 0x8A8A8A),
                    R(E, 20000, "Laser Tag Champion Vest", "Still beeping. Still champion.", ItemShape.Cube, 0x3FE8FF),
                    R(L, 160000, "Sealed Alien Game Cartridge", "Rescued from a fountain instead of a desert landfill.", ItemShape.Cube, 0x3FAF3F),
                },
                Theme = new ThemeDef
                {
                    FloorA = 0x1B1B24, FloorB = 0xE9E6F2, BasinTileA = 0xD63AAF, BasinTileB = 0x2BC4E0,
                    Rim = 0xC7CAD6, Wall = 0x2A2340, WallTrim = 0xFF4FD8, Ceiling = 0x16122A, Pillar = 0x3A2F5E,
                    LightColor = 0xC9B8FF, AmbientSky = 0x6A4FB8, AmbientEquator = 0x3B2C6E, AmbientGround = 0x1A1030,
                    FogColor = 0x2A1F4A, Accent = 0xFF4FD8, NeonA = 0xFF3FD0, NeonB = 0x3FE8FF, Plant = 0x2FAF7F,
                    LightIntensity = 0.85f, FogDensity = 0.0060f, NeonStrength = 2.4f, Night = true, SunPitch = 62f, SunYaw = -30f,
                    Signs = new[] { "ARCADE", "LASER TAG", "VIDEO VAULT", "RADIO SHACKLE", "HOT DOG ON A STIK", "MIXTAPE MUSIC", "TOTALLY TEES", "MALL HAIR" },
                    SignColors = new uint[] { 0xFF3FD0, 0x3FE8FF, 0xFFE066, 0xFF4F4F, 0xFFB03F, 0x7CF45A, 0xFF3FD0, 0x3FE8FF },
                    MusicRoot = 57, MusicTempo = 104, MusicMode = 2,
                },
            });

            // ─────────────────────────────── 3. Galleria Aurelia ───────────────────────────────
            malls.Add(new MallDef
            {
                Id = "aurelia",
                Name = "Galleria Aurelia",
                Tagline = "The High-End Luxury Galleria — by appointment only",
                Intro = "Marble, gold leaf and a 60-foot fountain that people toss gold coins into because pennies are 'vulgar'. Rumour says the bottom is solid gold.",
                DepthFeet = 60,
                CrustScoops = 3e5,
                LuckyPennies = 30,
                TreasureName = "The Platinum Membership Card",
                TreasureDesc = "Grants lifetime access to a VIP lounge that does not exist.",
                Event = new MallEventDef("Black Card Hour", "relic", 4, "The one percent are shopping. Relic finds ×4!"),
                Strata = Strata(
                    ("Fresh Toss (Tax-Deductible)", true, 0xB8A77A, 0xFFD45A, "Gold coins tossed by people who find pennies vulgar."),
                    ("Champagne Crust", false, 0xE8D9A8, 0xFFF6D8, "Vintage champagne residue crystallized into rock candy for the rich."),
                    ("Espresso Silt", false, 0x4A3226, 0xC9A064, "Twelve-dollar single-origin espresso grounds, compacted by very expensive heels."),
                    ("Designer Shades Shale", false, 0x2B2B30, 0xE0C080, "Discarded sunglasses from last season. Tragic."),
                    ("Offshore Account Ooze", false, 0x3F6E5A, 0xA8E0C0, "Shredded bank statements. Sticky, and it really does not want to be investigated."),
                    ("Yacht Club Clay", false, 0x2F4F7F, 0xFFFFFF, "Navy blue, monogrammed, and smelling faintly of sunscreen."),
                    ("Corporate Secret Sediment", false, 0x5A5048, 0xE8E0D0, "NDAs, stock tips and sealed envelopes marked DESTROY AFTER READING."),
                    ("The Founders' Vault", false, 0xB88A2E, 0xFFF0A0, "Solid gold. Obviously.")),
                Items = new[]
                {
                    I("Gold Coin", 45, 18, ItemShape.Coin, 0xE8B83A),
                    I("Silver Dollar", 3, 22, ItemShape.Coin, 0xD8DCE0),
                    I("Quarter", 0.25, 16, ItemShape.Coin, 0xC9CDD2),
                    I("Diamond Cufflink", 30, 8, ItemShape.Gem, 0xE8F4FF),
                    I("Designer Sunglasses", 180, 2, ItemShape.Stick, 0x1A1A1A),
                    I("Pearl", 60, 4, ItemShape.Gem, 0xF8F4EC),
                    I("Receipt (Very Long)", 0, 18, ItemShape.Paper, 0xF4F0E8),
                    I("Champagne Cork", 0, 12, ItemShape.Wad, 0xC8A870),
                },
                Wishes = new[]
                {
                    W(C, 500, "I wish the valet would park my other car closer."),
                    W(C, 550, "I wish my assistant's assistant would call me back."),
                    W(C, 500, "I wish the caviar samples were bigger."),
                    W(C, 600, "I wish I remembered which yacht I left my phone on."),
                    W(C, 700, "I wish for a normal amount of money. Just to see what it's like."),
                    W(C, 550, "I wish someone would ask me about my watch."),
                    W(U, 2000, "I wish my horse would finally win at the races."),
                    W(U, 2500, "I wish my tech startup's app did something."),
                    W(U, 2200, "I wish Mummy would unfreeze my trust fund."),
                    W(U, 2200, "I wish the private jet had better Wi-Fi."),
                    W(U, 3000, "I wish I was invited to the party I am currently hosting."),
                    W(Ra, 12000, "I wish I could buy the moon. Again."),
                    W(Ra, 11000, "I wish my ex's yacht sinks. Gently. Nobody hurt."),
                    W(Ra, 10000, "I wish to be on the cover of a magazine nobody reads."),
                    W(E, 50000, "I wish money could buy happiness. I'd buy all of it."),
                    W(E, 60000, "I wish I hadn't sold the company for only four billion."),
                    W(L, 400000, "I wish I was just a regular person at a regular mall."),
                },
                Relics = new[]
                {
                    R(C, 2500, "Gold-Plated Parking Validation", "Valid for one (1) complimentary helicopter pad.", ItemShape.Paper, 0xE8B83A),
                    R(C, 3000, "Designer Sunglasses (Last Season)", "Worn once. Mocked twice. Tossed immediately.", ItemShape.Stick, 0x1A1A1A),
                    R(C, 3500, "Monogrammed Silver Spoon", "Came with a mouth attached, originally.", ItemShape.Stick, 0xD8DCE0),
                    R(U, 15000, "Pearl Necklace", "Clutched, then tossed, during a dramatic gasp.", ItemShape.Gem, 0xF8F4EC),
                    R(U, 12000, "Sealed Corporate Secret", "Do not open. Do sell.", ItemShape.Paper, 0x8A2A2A),
                    R(U, 18000, "Crystal Champagne Flute", "Lead crystal. Still has a lipstick print.", ItemShape.Gem, 0xE0F4FF),
                    R(Ra, 100000, "Diver's Luxury Watch", "Waterproof to 300 metres. Fountain-proof to 60 feet, apparently.", ItemShape.Chip, 0x2F4F7F),
                    R(Ra, 120000, "Jeweled Egg", "It is not an egg. It is an heirloom. It is kind of an egg.", ItemShape.Gem, 0x3FAF8F),
                    R(Ra, 90000, "Private Island Deed (Water-Damaged)", "The island is fine. The deed is soggy.", ItemShape.Paper, 0xE8DCC0),
                    R(E, 500000, "Unlimited Black Card", "Declined exactly once, at this fountain, in 2004.", ItemShape.Paper, 0x111111),
                    R(E, 600000, "Tiny Lost Masterpiece", "A postcard-sized oil painting. The museum has been very quiet about it.", ItemShape.Paper, 0xC8A050),
                    R(L, 4000000, "The Hope-ish Diamond", "Blue, enormous, and allegedly only slightly cursed.", ItemShape.Gem, 0x3F7FFF),
                },
                Theme = new ThemeDef
                {
                    FloorA = 0xEAE4D8, FloorB = 0xC4B9A4, BasinTileA = 0xD9B45A, BasinTileB = 0xEFE9DD,
                    Rim = 0xE0D8C6, Wall = 0xE6DECF, WallTrim = 0xC9A64A, Ceiling = 0xFFFFFF, Pillar = 0xE8E1D3,
                    LightColor = 0xFFF1D2, AmbientSky = 0xEAF2FF, AmbientEquator = 0xE7DCC6, AmbientGround = 0x8E7F66,
                    FogColor = 0xEDE6D8, Accent = 0xC9A64A, NeonA = 0xFFD98A, NeonB = 0xFFFFFF, Plant = 0x3F7D4F,
                    LightIntensity = 1.02f, FogDensity = 0.0035f, NeonStrength = 1.0f, SunPitch = 60f, SunYaw = 20f,
                    Signs = new[] { "MAISON LUXE", "GOLDLEAF & SONS", "VELVET ROPE", "CAVIAR SOCIETY", "YACHT OUTFITTERS", "CHAMPAGNE BAR", "VALET", "HAUTE" },
                    SignColors = new uint[] { 0xC9A64A, 0xC9A64A, 0x8A2A3A, 0x2A2A2A, 0x2F4F7F, 0xC9A64A, 0x2A2A2A, 0xC9A64A },
                    MusicRoot = 65, MusicTempo = 70, MusicMode = 3,
                },
            });

            // ─────────────────────────────── 4. Skyport Terminal C ───────────────────────────────
            malls.Add(new MallDef
            {
                Id = "skyport",
                Name = "Skyport Terminal C",
                Tagline = "The Airport Concourse Fountain — now boarding all zones",
                Intro = "A 75-foot fountain in the middle of a concourse where every flight is delayed. It is full of coins from ninety countries, lost passports and an alarming amount of Toblerone.",
                DepthFeet = 75,
                CrustScoops = 6e5,
                LuckyPennies = 60,
                TreasureName = "The Lost Passport of Everyone",
                TreasureDesc = "A single passport with a stamp from every country on Earth and a very, very tired photo.",
                Event = new MallEventDef("Exchange Rate Spike", "sell", 2.5, "The currency board glitched in your favour. Sale value ×2.5!"),
                Strata = Strata(
                    ("Fresh Toss (Layover)", true, 0x7A7F86, 0xE8D080, "Coins from ninety countries, tossed by people on a six-hour delay."),
                    ("Jet Lag Jelly", false, 0x9A7B4F, 0xF0E0B0, "Airplane gravy fused with travel-size shampoo. Would not pass security."),
                    ("Duty-Free Drift", false, 0xC9A0C8, 0xFFE0F0, "Perfume samples and triangular chocolate shards in geological quantities."),
                    ("Lost Luggage Loam", false, 0x4D3B2E, 0x3A7BD5, "Somewhere down here is everyone's missing suitcase."),
                    ("Neck Pillow Peat", false, 0x8A6FA0, 0xF5F5F5, "Memory foam that remembers everything. Everything."),
                    ("Boarding Pass Breccia", false, 0xD8D2C0, 0x2F6FD6, "Crushed boarding passes. Gate changes all the way down."),
                    ("Frequent Flyer Flint", false, 0x3B4552, 0xD4AF37, "Elite-status tags, polished by a million humble-brags."),
                    ("Runway Foundation (1972)", false, 0x2E3238, 0xFFD23A, "Original tarmac. It still vibrates when planes take off.")),
                Items = new[]
                {
                    I("Euro Coin", 1.10, 18, ItemShape.Coin, 0xD8B85A),
                    I("100 Yen Coin", 0.70, 14, ItemShape.Coin, 0xD8DCE0),
                    I("Pound Coin", 1.30, 12, ItemShape.Coin, 0xE0C060),
                    I("Loonie", 0.73, 12, ItemShape.Coin, 0xD4A83A),
                    I("Toonie", 1.46, 8, ItemShape.Coin, 0xC8C8C8),
                    I("Peso", 0.05, 10, ItemShape.Coin, 0xB08D57),
                    I("Mini Toblerone", 4, 6, ItemShape.Stick, 0xE8C85A),
                    I("Rolled-Up Euros", 50, 2, ItemShape.Paper, 0x6FA86F),
                    I("Duty-Free Watch", 300, 0.5, ItemShape.Chip, 0x3A3A3A),
                    I("Luggage Tag", 0, 10, ItemShape.Paper, 0xF2B233),
                    I("Travel Shampoo", 0, 6, ItemShape.Cube, 0x5AB0FF),
                },
                Wishes = new[]
                {
                    W(C, 800, "I wish my flight was only delayed a normal amount."),
                    W(C, 800, "I wish for the empty seat next to me."),
                    W(C, 900, "I wish the moving walkway went faster than walking."),
                    W(C, 900, "I wish the guy in 14C would stop reclining."),
                    W(C, 1000, "I wish my bag comes out first. Just once."),
                    W(C, 800, "I wish airport sandwiches cost less than a car payment."),
                    W(U, 3500, "I wish for an upgrade. Any upgrade. Premium economy, even."),
                    W(U, 4000, "I wish I'd packed the charger in my carry-on."),
                    W(U, 3500, "I wish the lounge would let me in with a coupon."),
                    W(U, 4500, "I wish I could live at gate 23 forever. It's nice here."),
                    W(U, 3500, "I wish they would stop calling my name over the speakers."),
                    W(Ra, 16000, "I wish I get home in time for the recital."),
                    W(Ra, 18000, "I wish the person I'm flying to see says yes."),
                    W(Ra, 15000, "I wish to see every country before I'm eighty."),
                    W(E, 70000, "I wish planes were powered by wishes. Imagine the legroom."),
                    W(E, 80000, "I wish goodbye at the gate didn't hurt this much."),
                    W(L, 500000, "I wish everybody gets home safe tonight."),
                },
                Relics = new[]
                {
                    R(C, 4000, "Complimentary Peanut Packet", "Sealed since 1994. Contains exactly three peanuts.", ItemShape.Paper, 0xC8A060),
                    R(C, 4500, "Neck Pillow (Bejeweled)", "Travel in comfort, sparkle in regret.", ItemShape.Wad, 0x8A6FA0),
                    R(C, 5000, "Airline Wings Pin", "Given to a very proud eight-year-old in 1987.", ItemShape.Stick, 0xD4AF37),
                    R(U, 20000, "Ninety-Country Coin Collection", "Somebody lost their entire gap year in here.", ItemShape.Coin, 0xB08D57),
                    R(U, 25000, "Lost Boarding Pass (First Class)", "Seat 1A. Champagne was included. You missed it.", ItemShape.Paper, 0x2F6FD6),
                    R(U, 22000, "Snow Globe of Every City", "Shake it and it snows in all of them.", ItemShape.Gem, 0xBFE8FF),
                    R(Ra, 140000, "Pilot's Aviator Sunglasses", "Mirrored. Iconic. Slightly scratched from a very cool landing.", ItemShape.Stick, 0xC0C0C0),
                    R(Ra, 150000, "Golden Luggage Tag", "Status: Platinum Diamond Obsidian Infinite.", ItemShape.Paper, 0xE8B83A),
                    R(Ra, 130000, "Model Concorde", "Supersonic in spirit.", ItemShape.Stick, 0xF5F5F5),
                    R(E, 700000, "Lifetime First-Class Pass", "Non-transferable. Transferred to you by a fountain.", ItemShape.Paper, 0xD4AF37),
                    R(E, 650000, "Black Box Recorder (Toy)", "It only recorded someone humming. Beautifully.", ItemShape.Cube, 0xFF7A1A),
                    R(L, 5000000, "The Original Wright Flyer Bolt", "Tiny, rusty, and the reason any of this exists.", ItemShape.Stick, 0x8A6A4A),
                },
                Theme = new ThemeDef
                {
                    FloorA = 0x5D6B78, FloorB = 0x4A5664, BasinTileA = 0x2F6FD6, BasinTileB = 0xDDE6EE,
                    Rim = 0xB8C2CC, Wall = 0xD8DEE4, WallTrim = 0xF2B233, Ceiling = 0xEEF2F5, Pillar = 0xCBD3DA,
                    LightColor = 0xF2F7FF, AmbientSky = 0xBFD6F0, AmbientEquator = 0x9FAFBF, AmbientGround = 0x4A5560,
                    FogColor = 0xC8D4E0, Accent = 0xF2B233, NeonA = 0xFFC83A, NeonB = 0x5AB0FF, Plant = 0x4F8A5B,
                    LightIntensity = 1.15f, FogDensity = 0.0040f, NeonStrength = 1.3f, SunPitch = 52f, SunYaw = 60f,
                    Signs = new[] { "GATES C1-C40", "DUTY FREE", "BAGGAGE CLAIM", "CURRENCY EXCHANGE", "NECK PILLOWS", "GATE 23: DELAYED", "SKY LOUNGE", "SNACKS" },
                    SignColors = new uint[] { 0xFFC83A, 0x5AB0FF, 0xFFC83A, 0x7CF45A, 0x5AB0FF, 0xFF5A5A, 0xFFC83A, 0x5AB0FF },
                    MusicRoot = 64, MusicTempo = 92, MusicMode = 1,
                },
            });

            // ─────────────────────────────── 5. The Lucky Lagoon ───────────────────────────────
            malls.Add(new MallDef
            {
                Id = "luckylagoon",
                Name = "The Lucky Lagoon",
                Tagline = "The Casino Resort Fountain — the house always digs",
                Intro = "A 90-foot fountain under a chandelier the size of a bus. Gamblers have been tossing chips, dice and wedding rings in here since 1966. Some of it was on purpose.",
                DepthFeet = 90,
                CrustScoops = 1.2e6,
                LuckyPennies = 110,
                TreasureName = "The Lucky Die",
                TreasureDesc = "It rolls a seven. Every face is a seven. Nobody at the casino wants to talk about it.",
                Event = new MallEventDef("Jackpot Hour", "golden", 1, "Sirens! Golden pennies rain down all over the fountain!"),
                Strata = Strata(
                    ("Fresh Toss (House Money)", true, 0x5A2A30, 0xFFD34D, "Chips tossed by winners, losers, and one very confused bachelor party."),
                    ("Buffet Gravy Glaze", false, 0x8A5A2E, 0xFF8A6A, "All-you-can-eat shrimp cocktail sauce, all-you-can-regret."),
                    ("Comp Drink Crust", false, 0xB0405A, 0xF0F0F0, "Sugar rims, maraschino cherries and tiny umbrellas, fused solid."),
                    ("Slot Ticket Strata", false, 0xE8E4D0, 0x3A3A3A, "Thousands of unredeemed $0.03 cash-out tickets."),
                    ("Sequin Layer", false, 0xC0C0D8, 0xFFFFFF, "Thank you. Thank you very much."),
                    ("Chapel Confetti", false, 0xF4D0DC, 0xFF5A8A, "Quickie wedding rice, bouquets and second thoughts."),
                    ("High Roller Hardpan", false, 0x1F3F2F, 0xD4AF37, "Green felt and black chips, compressed by the weight of enormous bets."),
                    ("Original Casino Foundation", false, 0x2A1418, 0xFF3048, "1966. The concrete was poured with a lucky horseshoe in it.")),
                Items = new[]
                {
                    I("Red Chip ($5)", 5, 20, ItemShape.Chip, 0xD8283A),
                    I("Green Chip ($25)", 25, 8, ItemShape.Chip, 0x1F8F4F),
                    I("Black Chip ($100)", 100, 3, ItemShape.Chip, 0x1A1A1A),
                    I("Purple Chip ($500)", 500, 0.6, ItemShape.Chip, 0x7A3FC8),
                    I("Silver Dollar", 1, 18, ItemShape.Coin, 0xD8DCE0),
                    I("Slot Token", 1, 12, ItemShape.Coin, 0xD4AF37),
                    I("Wedding Ring", 400, 0.4, ItemShape.Gem, 0xF0D060),
                    I("Die", 0, 8, ItemShape.Cube, 0xF5F5F5),
                    I("Cocktail Umbrella", 0, 10, ItemShape.Stick, 0xFF5A8A),
                    I("Playing Card", 0, 12, ItemShape.Paper, 0xFFFFFF),
                },
                Wishes = new[]
                {
                    W(C, 1500, "I wish the buffet had more crab legs."),
                    W(C, 1500, "I wish I could win back the rent money."),
                    W(C, 1800, "I wish the magician would pick my card."),
                    W(C, 1600, "I wish I remembered my room number."),
                    W(C, 1500, "I wish my lucky socks were luckier."),
                    W(C, 2000, "I wish the carpet was a less aggressive pattern."),
                    W(U, 7000, "I wish the Elvis who married us was the real Elvis."),
                    W(U, 8000, "I wish for a royal flush. Just once in my life."),
                    W(U, 7000, "I wish my roulette system actually worked."),
                    W(U, 7500, "I wish the comedian hadn't picked on me."),
                    W(U, 9000, "I wish the slot that paid $2 would pay $2 million."),
                    W(Ra, 35000, "I wish what happens here stays here. Please."),
                    W(Ra, 40000, "I wish I could beat the dealer on purpose."),
                    W(Ra, 32000, "I wish my cousin's magic show gets a residency."),
                    W(E, 150000, "I wish I had stopped while I was ahead."),
                    W(E, 180000, "I wish to marry my high school sweetheart. Again. For real this time."),
                    W(L, 1200000, "I wish to break the bank. The whole building."),
                },
                Relics = new[]
                {
                    R(C, 8000, "Buffet Crab Cracker", "Battle-scarred.", ItemShape.Stick, 0xC0C0C0),
                    R(C, 9000, "Loaded Dice", "Don't tell anyone.", ItemShape.Cube, 0xF5F5F5),
                    R(C, 10000, "Cocktail Umbrella Collection", "Forty-two umbrellas. Zero rain.", ItemShape.Stick, 0xFF5A8A),
                    R(U, 40000, "$5,000 Plaque Chip", "Rectangular money. The fanciest kind.", ItemShape.Chip, 0x3FA0FF),
                    R(U, 45000, "Impersonator's Gold Sunglasses", "The sideburns were sold separately.", ItemShape.Stick, 0xD4AF37),
                    R(U, 42000, "Chapel Wedding Ring", "Engraved: 'Probably Forever'.", ItemShape.Gem, 0xF0D060),
                    R(Ra, 250000, "Unclaimed Jackpot Ticket", "$1,000,000. Expired 1998. Framed.", ItemShape.Paper, 0xFFD34D),
                    R(Ra, 280000, "Laminated Royal Flush", "Proof, for everyone who didn't believe them.", ItemShape.Paper, 0xFFFFFF),
                    R(Ra, 220000, "Magician's Missing Rabbit (Plush)", "The show must go on.", ItemShape.Wad, 0xF5F5F5),
                    R(E, 1200000, "Diamond-Encrusted Chip", "Worth more than the chip says. Much more.", ItemShape.Chip, 0xE8F4FF),
                    R(E, 1400000, "Poker Champion's Gold Bracelet", "Won with a pair of twos and a very good face.", ItemShape.Stick, 0xE8B83A),
                    R(L, 9000000, "The High Roller's Briefcase", "Handcuffed to nothing. Contents: extremely yes.", ItemShape.Cube, 0x3A2A1A),
                },
                Theme = new ThemeDef
                {
                    FloorA = 0x7A1022, FloorB = 0x5A0A18, BasinTileA = 0x0F6B4E, BasinTileB = 0xD4AF37,
                    Rim = 0xD4AF37, Wall = 0x2A0A10, WallTrim = 0xFFD34D, Ceiling = 0x14060A, Pillar = 0x3A0E18,
                    LightColor = 0xFFD8A8, AmbientSky = 0x8A3A4A, AmbientEquator = 0x5A1A26, AmbientGround = 0x1A0508,
                    FogColor = 0x2A0A14, Accent = 0xFFD34D, NeonA = 0xFF3048, NeonB = 0xFFD34D, Plant = 0x2F8F5F,
                    LightIntensity = 0.9f, FogDensity = 0.0055f, NeonStrength = 2.2f, Night = true, SunPitch = 65f, SunYaw = 10f,
                    Signs = new[] { "SLOTS", "BUFFET", "WEDDING CHAPEL", "HIGH LIMIT", "CASHIER", "LUCKY LOUNGE", "KENO", "ELVIS LIVE!" },
                    SignColors = new uint[] { 0xFF3048, 0xFFD34D, 0xFF7AB8, 0xFFD34D, 0x3FE8A0, 0xFF3048, 0x3FA0FF, 0xFFD34D },
                    MusicRoot = 55, MusicTempo = 112, MusicMode = 4,
                },
            });

            // ─────────────────────────────── 6. Eternity Plaza ───────────────────────────────
            malls.Add(new MallDef
            {
                Id = "eternity",
                Name = "Eternity Plaza",
                Tagline = "The World's First Mall Fountain — est. 1956",
                Intro = "The oldest mall fountain in the world, 120 feet deep. The blueprints say it was poured over 'an existing well'. Nobody at corporate knows what that means.",
                DepthFeet = 120,
                CrustScoops = 2.5e6,
                LuckyPennies = 200,
                TreasureName = "The First Wish",
                TreasureDesc = "A tiny, ancient coin that hums in your hand. Whoever tossed it wished for 'more wishes'. It worked.",
                Event = new MallEventDef("Wishing Hour", "all", 2, "The old well stirs. Everything is worth double!"),
                Strata = Strata(
                    ("Fresh Toss (Tourist Season)", true, 0x8A8470, 0xE8C080, "Tourists came to see the world's first mall fountain. They mostly saw a hole."),
                    ("Atomic Age Aspic", false, 0x3FB86F, 0xFF6A6A, "Lime gelatin salad, preserved by pure 1950s optimism."),
                    ("Soda Fountain Sediment", false, 0xF2B8C6, 0xFFFFFF, "Malt shop milkshakes, pressed into pink marble."),
                    ("Poodle Skirt Pumice", false, 0xE8A0B8, 0x2A2A2A, "Felt poodles, saddle shoes and a very old jukebox coin."),
                    ("Mid-Century Mortar", false, 0xA89A80, 0xF08A3C, "The original concrete. Beneath it... something older."),
                    ("Doubloon Drift (1715)", false, 0x3A4A5A, 0xFFD34D, "Pirate gold. Nobody at corporate can explain this."),
                    ("Denarius Deposit (80 AD)", false, 0x8A6A4A, 0xD8C8A0, "Roman coins. The well is two thousand years older than the mall."),
                    ("The Wellspring", false, 0x2A2060, 0x9FF0FF, "The original wishing well. Everything down here glows.")),
                Items = new[]
                {
                    I("Buffalo Nickel", 1, 18, ItemShape.Coin, 0xB9BDC2),
                    I("Mercury Dime", 3, 14, ItemShape.Coin, 0xD4D8DC),
                    I("Silver Dollar (1956)", 25, 8, ItemShape.Coin, 0xE0E4E8),
                    I("Wheat Penny", 0.2, 16, ItemShape.Coin, 0xB86A38),
                    I("Atomic Bottle Cap", 0.5, 16, ItemShape.Coin, 0xF08A3C),
                    I("Roman Denarius", 300, 1.2, ItemShape.Coin, 0xC8C0A8),
                    I("Pirate Doubloon", 1500, 0.4, ItemShape.Coin, 0xF0C040),
                    I("Soda Straw", 0, 14, ItemShape.Stick, 0xFF6A8A),
                    I("Jukebox Slip", 0, 10, ItemShape.Paper, 0xF5E6C0),
                },
                Wishes = new[]
                {
                    W(C, 2500, "I wish for a malted. Extra thick."),
                    W(C, 2500, "I wish my poodle skirt twirls higher than Betty's."),
                    W(C, 3000, "I wish for a jukebox of my very own."),
                    W(C, 2800, "I wish the King himself would visit Eternity Plaza."),
                    W(C, 3000, "I wish for a robot maid, like at the World's Fair."),
                    W(C, 2500, "I wish the drive-in played monster movies every night."),
                    W(U, 12000, "I wish for a flying car by 1970."),
                    W(U, 12000, "I wish my Tupperware party is the talk of the street."),
                    W(U, 14000, "I wish to be the first man on the moon. Or second. Second is fine."),
                    W(U, 12000, "I wish my hula hoop never stops spinning."),
                    W(U, 13000, "I wish, O Neptune, for a better harvest this year."),
                    W(Ra, 60000, "I wish the future is as bright as the brochure."),
                    W(Ra, 65000, "I wish my legion comes home safely."),
                    W(Ra, 70000, "I wish this treasure stays hidden until someone worthy finds it."),
                    W(E, 300000, "I wish someone finds this coin and remembers us."),
                    W(E, 320000, "I wish for peace. Honestly, just some peace."),
                    W(L, 2500000, "I wish for more wishes."),
                },
                Relics = new[]
                {
                    R(C, 15000, "Soda Jerk Paper Hat", "Crisp, white, and slightly malted.", ItemShape.Paper, 0xFFFFFF),
                    R(C, 16000, "Atomic Starburst Clock Hand", "Points permanently to 'the future'.", ItemShape.Stick, 0xF08A3C),
                    R(C, 18000, "Jukebox Selection Card", "B-17: a song about a fountain. How meta.", ItemShape.Paper, 0xF5E6C0),
                    R(U, 70000, "Space-Age Ray Gun Toy", "Pew. Pew. (Batteries not included, invented, or available.)", ItemShape.Stick, 0xC0C8D0),
                    R(U, 75000, "1956 Grand Opening Ribbon", "Cut with golden scissors by a mayor nobody remembers.", ItemShape.Paper, 0xD8283A),
                    R(U, 80000, "Rocket Ride Token", "Good for one trip to the moon. The mechanical kind.", ItemShape.Coin, 0x3FB8AF),
                    R(Ra, 400000, "Pirate Doubloon Hoard", "Arr. Also, how.", ItemShape.Coin, 0xF0C040),
                    R(Ra, 450000, "Captain's Brass Spyglass", "It only shows fountains.", ItemShape.Stick, 0xC8A050),
                    R(Ra, 420000, "Roman Denarius of Titus", "The first person to ever throw a coin in this well was very specific about it.", ItemShape.Coin, 0xC8C0A8),
                    R(E, 2000000, "Centurion's Helmet Crest", "Red, proud, and slightly soggy for two millennia.", ItemShape.Wad, 0xC8283A),
                    R(E, 2400000, "Oracle's Bronze Mirror", "Shows you what you wished for. Not what you wanted.", ItemShape.Chip, 0xB08D57),
                    R(L, 16000000, "Neptune's Trident Tip", "The original fountain ornament. The mall's was a replica.", ItemShape.Stick, 0x5FE8F0),
                },
                Theme = new ThemeDef
                {
                    FloorA = 0xF1E6CF, FloorB = 0x3FB8AF, BasinTileA = 0xF08A3C, BasinTileB = 0x3FB8AF,
                    Rim = 0xE8E0CC, Wall = 0xF5EEDC, WallTrim = 0xF08A3C, Ceiling = 0x2B3A5A, Pillar = 0xE6DCC4,
                    LightColor = 0xFFE6C0, AmbientSky = 0x9FD0E0, AmbientEquator = 0xE0C8A0, AmbientGround = 0x5A4A3A,
                    FogColor = 0xE8DCC4, Accent = 0xF08A3C, NeonA = 0x3FE0D0, NeonB = 0xFF9A3C, Plant = 0x5F9F4F,
                    LightIntensity = 1.15f, FogDensity = 0.0040f, NeonStrength = 1.4f, SunPitch = 50f, SunYaw = -45f,
                    Signs = new[] { "MALT SHOPPE", "ATOMIC TV", "HI-FI HAVEN", "DRIVE-IN", "SODA FOUNTAIN", "ROCKET RIDES", "5 & DIME", "THE FUTURE" },
                    SignColors = new uint[] { 0xFF6A8A, 0x3FE0D0, 0xFF9A3C, 0xFFD34D, 0x3FE0D0, 0xFF9A3C, 0xFF6A8A, 0x3FE0D0 },
                    MusicRoot = 60, MusicTempo = 88, MusicMode = 5,
                },
            });

            // v1 sized each mall's values for a ×10⁴-per-mall economy; scale them back so later malls are
            // somewhat richer than Crestview (crust ≈ ×1.6 per mall) rather than astronomically richer
            double[] lootScale = { 1, 25, 225, 28, 60, 40 };
            double[] storyScale = { 1, 10, 84, 83, 110, 118 };
            // Prices and tosses grow modestly from mall to mall (ValueScale). Loot, wishes and relics are
            // divided by the same factor here because Scale multiplies it back in play, so their actual
            // values stay where the v1 rescale put them: each mall is Crestview's economy at a bigger
            // scale, and Head Office perks (not cheaper prices) are what make later malls go faster.
            double[] valueScale = { 1, 1.5, 2.5, 4, 6.5, 10 };
            for (int m = 0; m < malls.Count; m++)
            {
                var mall = malls[m];
                mall.ValueScale = valueScale[m];
                mall.LootScale = lootScale[m] * valueScale[m];
                mall.StoryScale = storyScale[m] * valueScale[m];
                foreach (var it in mall.Items) it.Value /= mall.LootScale;
                foreach (var w in mall.Wishes) w.BaseValue /= mall.StoryScale;
                foreach (var r in mall.Relics) r.BaseValue /= mall.StoryScale;
            }

            for (int m = 0; m < malls.Count; m++)
            {
                var mall = malls[m];
                mall.ComputeEV();
                if (m < FittedScoops.Length && FittedScoops[m] > 0) mall.CrustScoops = FittedScoops[m];
                SetCrust(mall, mall.CrustScoops);
            }

            for (int m = 0; m < malls.Count; m++)
            {
                var mall = malls[m];
                for (int i = 0; i < mall.Wishes.Length; i++) { mall.Wishes[i].Id = $"{mall.Id}_w{i:00}"; mall.Wishes[i].MallIndex = m; }
                for (int i = 0; i < mall.Relics.Length; i++) { mall.Relics[i].Id = $"{mall.Id}_r{i:00}"; mall.Relics[i].MallIndex = m; }
            }
            return malls.ToArray();
        }
    }
}
