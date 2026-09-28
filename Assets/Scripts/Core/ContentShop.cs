// Machines, tools, the upgrade tree and Head Office (prestige) perks.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        // All prices below are in Crestview dollars; the Sim multiplies them by the current mall's CostScale.
        static MachineDef[] BuildMachines()
        {
            var list = new List<MachineDef>();
            void M(string id, string name, Stage stage, int tier, double cost, double rate, int mall, int stratum, string desc)
            {
                list.Add(new MachineDef { Id = id, Name = name, Stage = stage, Tier = tier, BaseCost = cost, BaseRate = rate, UnlockMall = mall, UnlockStratum = stratum, Desc = desc });
            }

            // DIG — dredging crust out of the basin
            M("pogo", "Metal-Detecting Pogo Stick", Stage.Dig, 1, 4, 1.0, 0, 0,
                "Boing. Beep. Crunch. Every bounce shakes loose a little shower of crust.");
            M("walkers", "Mall Walker Brigade", Stage.Dig, 2, 400, 10, 0, 1,
                "Retirees in matching tracksuits power-walk laps across the crust, stomping it loose. They were coming anyway.");
            M("jackhammer", "Jackhammer Excavator", Stage.Dig, 3, 8e5, 1700, 0, 4,
                "A massive robotic drill that pounds the hardened penny-crust into manageable chunks. The neighbours have complained.");
            M("claw", "Giant Claw Machine", Stage.Dig, 4, 1e9, 1.75e5, 1, 0,
                "An arcade claw the size of a bus. Unlike the real ones, it never, ever lets go.");
            M("borer", "Gilded Tunnel Borer", Stage.Dig, 5, 2e12, 2.9e7, 2, 0,
                "A diamond-tipped, gold-plated boring machine. Tastefully tasteless.");

            // WASH — dissolving decades of syrup
            M("tumbler", "Diet Cola Tumbler", Stage.Wash, 1, 40, 5, 0, 1,
                "A hand-cranked drum fed by a generic two-litre bottle. The carbonation eats through forty years of syrup.");
            M("dishwasher", "Food Court Dishwasher", Stage.Wash, 2, 3200, 40, 0, 2,
                "Liberated from the food court. Industrial steam, lemon scent, zero questions asked.");
            M("acid", "Acid-Wash River", Stage.Wash, 3, 1e6, 1040, 0, 4,
                "A glowing river of industrial citric acid that instantly dissolves gum, mold and calcified lime.");
            M("carwash", "Neon Car Wash Tunnel", Stage.Wash, 4, 1.2e9, 1.04e5, 1, 0,
                "Spinning neon brushes scrub the crust at eighty-eight miles per hour.");
            M("jacuzzi", "Champagne Jacuzzi Cascade", Stage.Wash, 5, 2.4e12, 1.74e7, 2, 0,
                "Vintage bubbly, heated to 104°F. Dissolves grime. Also dignity.");

            // SORT — separating wishes and jewelry from zinc
            M("pigeons", "Pigeon Perch", Stage.Sort, 1, 60, 6, 0, 1,
                "Local mall pigeons, paid in pretzel crumbs, peck away cigarette butts and hairpins until only shiny metal is left.");
            M("coinstar", "Coin Star Junction", Stage.Sort, 2, 5000, 50, 0, 2,
                "A rattling mechanical tray that separates quarters, dimes and nickels from the pennies. Takes a 9% cut out of habit.");
            M("lasers", "Optical Laser Scanners", Stage.Sort, 3, 1.5e6, 1250, 0, 4,
                "Cameras vaporize common zinc pennies while robot arms snatch rings, tokens and watches straight out of the air.");
            M("prizebots", "Prize Counter Robo-Arms", Stage.Sort, 4, 1.5e9, 1.04e5, 1, 0,
                "Retired arcade prize-counter robots. They sort with the dead-eyed precision of a teenager on minimum wage.");
            M("sieve", "Quantum Coin Sieve", Stage.Sort, 5, 3e12, 1.74e7, 2, 0,
                "Sorts every coin in every possible universe at once, and only keeps the good universes.");

            // WISHES — automatic capture
            list.Add(new MachineDef
            {
                Id = "compressor", Name = "Wish Compressor", Stage = Stage.Wash, Tier = 9, BaseCost = 3e6, BaseRate = 0,
                CostGrowth = 1.6, UnlockMall = 0, UnlockStratum = 6, IsCompressor = true, MaxCount = 30,
                Desc = "Sucks up floating Wish Energy and presses it into purple bricks of concentrated nostalgia for eccentric billionaires. Each level catches more escaping wishes."
            });

            // MEGAPROJECTS — one per late mall, big multiplicative boosts
            list.Add(new MachineDef
            {
                Id = "carousel", Name = "Baggage Carousel Loop", Stage = Stage.Sort, Tier = 10, BaseCost = 1e8, BaseRate = 0,
                CostGrowth = 8, UnlockMall = 3, UnlockStratum = 0, IsMega = true, MegaEffect = "rate", MegaPerLevel = 0.6, MaxCount = 25,
                Desc = "A carousel that loops the whole fountain. Every bag, every coin, every lost wish goes round until someone claims it. +60% to every machine per level."
            });
            list.Add(new MachineDef
            {
                Id = "slots", Name = "Slot Machine of Fortune", Stage = Stage.Sort, Tier = 10, BaseCost = 1e9, BaseRate = 0,
                CostGrowth = 8, UnlockMall = 4, UnlockStratum = 0, IsMega = true, MegaEffect = "value", MegaPerLevel = 0.6, MaxCount = 25,
                Desc = "A three-storey slot machine that pays out in sale value. +60% value of everything per level. The house has decided you win."
            });
            list.Add(new MachineDef
            {
                Id = "wishengine", Name = "The Wish Engine", Stage = Stage.Wash, Tier = 10, BaseCost = 1e10, BaseRate = 0,
                CostGrowth = 8, UnlockMall = 5, UnlockStratum = 0, IsMega = true, MegaEffect = "wish", MegaPerLevel = 0.6, MaxCount = 25,
                Desc = "An ancient Roman mechanism, rebuilt with mall parts. Refines raw wish energy into pure momentum: +60% machine speed and wish value per level."
            });

            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            return list.ToArray();
        }

        static ToolDef[] BuildTools()
        {
            var list = new List<ToolDef>();
            void T(string id, string name, double cost, double power, string desc) => list.Add(new ToolDef { Id = id, Name = name, Cost = cost, Power = power, Desc = desc });

            T("gum", "Chewed Bubblegum on a String", 0, 1, "It sticks to exactly one thing at a time. Usually your fingers.");
            T("knife", "Butter Knife", 0.8, 5, "Pries loose five coins at once. Do not use on toast afterwards.");
            T("shovel", "Plastic Sandbox Shovel", 12, 25, "Bright yellow. Slightly chewed. Scoops little piles into a bucket.");
            T("trowel", "Garden Trowel", 1500, 100, "Borrowed from the garden centre. Not returning it.");
            T("crowbar", "Crowbar", 6e4, 400, "For prying up the crust. And prying open the vending machine. Allegedly.");
            T("shopvac", "Shop-Vac 3000", 3e6, 1500, "Sucks crust straight out of the floor. Also sucked up a small dog once. He's fine.");
            T("leafblower", "Reverse Leaf Blower", 1.5e8, 6000, "Rewired to suck instead of blow. The warranty is extremely void.");
            T("handjack", "Handheld Jackhammer", 8e9, 25000, "Your arms will never stop vibrating. Worth it.");
            T("plasma", "Plasma Cutter", 5e11, 1e5, "Slices through syrup-crust like a hot knife through a Cinna-Bun.");
            T("gauntlet", "Wish-Powered Excavation Gauntlet", 3e13, 4e5, "Snap your fingers and half the crust disappears.");
            T("trident", "Trident of Neptune", 2e15, 1.6e6, "The original owner would like it back. He can dig for it.");
            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            return list.ToArray();
        }

        static UpgradeDef[] BuildUpgrades()
        {
            var list = new List<UpgradeDef>();

            // ── Per-machine upgrades: six per machine, each doubles that machine ──
            var machineNames = new Dictionary<string, string[]>
            {
                ["pogo"] = new[] { "Extra-Springy Spring", "Bigger Metal Detector", "Pogo Stunt Team", "Rocket Pogo", "Pogo Olympics Sponsorship", "Quantum Bounce", "Pogo Swarm", "Anti-Gravity Pogo", "Pogo Pogo Pogo", "The Last Bounce" },
                ["walkers"] = new[] { "Matching Tracksuits", "Orthopedic Sneakers", "Mall Walker Mixtape", "Senior Discount Smoothies", "Competitive Mall Walking League", "Speed-Walking Legends", "Marathon Mall Walkers", "Walker Rollerblades", "Titanium Tracksuits", "Eternal Laps" },
                ["jackhammer"] = new[] { "Diamond Drill Bit", "Noise Complaint Waiver", "Twin Hammers", "Hydraulic Overdrive", "Seismic Permit", "Jackhammer Symphony", "Jackhammer Choir", "Tectonic Hammering", "Richter-Scale Rating", "The Core Knock" },
                ["claw"] = new[] { "Firm Grip Firmware", "Rigged In Your Favour", "Twin Claws", "Prize Chute Expansion", "Claw Machine Champion", "The Infinite Claw", "Claw Cartel", "Claw of Destiny", "Unwinnable No More", "The Hand of Fate" },
                ["borer"] = new[] { "Gold-Leaf Teeth", "Diamond Cutters", "Velvet Tunnel Lining", "Borer Butler", "Twin Borers", "The Mole of Wall Street", "Borer Brunch Club", "Diamond Sharks", "Private Tunnel Network", "The Midas Mole" },
                ["tumbler"] = new[] { "Name-Brand Cola", "Crank Motor", "Mint Candy Injection", "Two-Litre Upsizing", "Carbonation Overdrive", "Endless Refills", "Fizz Singularity", "Cola Monsoon", "Bottomless Bottle", "The Big Fizz" },
                ["dishwasher"] = new[] { "Lemon-Scented Tablets", "Pots & Pans Cycle", "Steam Boost", "Double Racks", "Industrial Rinse Aid", "Dishwasher Army", "Heavy-Duty Scrub Cycle", "Dishwasher Diaspora", "Sparkle Protocol", "The Clean Machine" },
                ["acid"] = new[] { "Extra-Sour Citric Acid", "Glow-in-the-Dark Additive", "River Rapids", "Acid Rain Sprinklers", "Lime Dissolution Theory", "Acid Ocean", "Acid Waterfall", "Citrus Tsunami", "Glowing Rapids Resort", "The Dissolver" },
                ["carwash"] = new[] { "Rainbow Foam", "Hot Wax Cycle", "88 MPH Brushes", "Tire Shine", "Undercarriage Blast", "Car Wash Franchise", "Wax On, Wax Off", "Turbo Rinse", "Chrome Everything", "Neon Tidal Wave" },
                ["jacuzzi"] = new[] { "Vintage Bubbly", "Rose Petals", "Heated Marble Seats", "Jet Upgrade", "Sommelier on Staff", "Champagne Supernova", "Bubbles on Bubbles", "Caviar Scrub", "Gold Leaf Foam", "Infinite Brunch" },
                ["pigeons"] = new[] { "Pretzel Crumb Bonus", "Pigeon Union Contract", "Tiny Pigeon Visors", "Pigeon Middle Management", "The Pigeon Mafia", "Pigeon Singularity", "Pigeon Parliament", "Pigeon Satellite Network", "Pigeon Hive Mind", "Pigeon Ascension" },
                ["coinstar"] = new[] { "Oiled Coin Rollers", "Skip the 9% Fee", "Dime Detector", "Turbo Rattle", "Coin Star Constellation", "Supernova Sorter", "Coin Star Galaxy", "Rattle Resonance", "Sentient Change Machine", "Heat Death of Pennies" },
                ["lasers"] = new[] { "Sharper Lasers", "Robot Arm Reflexes", "Wristwatch Recognition AI", "Laser Light Show Mode", "Frickin' Laser Beams", "Laser Cathedral", "Laser Tag Veterans", "Photon Tweezers", "Laser Hyperdrive", "The Laser Dimension" },
                ["prizebots"] = new[] { "Ticket Counter Training", "Dead-Eyed Precision", "Extra Arms", "Plush Recognition", "Employee of the Month", "Prize Counter Uprising", "Plush Prize Cartel", "Robot Union Local 88", "Ticket Singularity", "The Grand Prize Machine" },
                ["sieve"] = new[] { "Superposition Mesh", "Many-Worlds Filter", "Schrödinger's Coin", "Entangled Sorting", "Quantum Tunnelling", "The Multiverse Sieve", "Probability Mesh", "Parallel Pennies", "Coin Superposition", "The Everything Sieve" },
            };
            int[] reqCounts = { 1, 10, 25, 50, 100, 150, 200, 250, 300, 400 };
            string[] UpgradeFlavor = { "A small tweak from the manual.", "Somebody finally read the warranty.", "Fresh parts, straight off the truck.", "Custom work from a guy at the auto shop.", "Engineers flown in from head office.", "Patented. Probably.", "Barely legal in this municipality.", "Scientists are concerned.", "It hums now. Nobody knows why.", "The final form." };
            double[] costMult = { 10, 80, 1e3, 5e4, 5e7, 2e10, 5e12, 5e15, 5e18, 5e24 };
            double[] upMult = { 1.5, 1.5, 2, 2, 2, 2, 3, 2, 3, 3 };
            foreach (var kv in machineNames)
            {
                for (int k = 0; k < reqCounts.Length; k++)
                {
                    var m = Array.Find(Machines, x => x.Id == kv.Key);
                    list.Add(new UpgradeDef
                    {
                        Id = $"up_{kv.Key}_{k}",
                        Name = kv.Value[k],
                        Desc = UpgradeFlavor[k],
                        Cost = m.BaseCost * costMult[k],
                        Kind = UpgradeKind.MachineMult,
                        Target = kv.Key,
                        Value = upMult[k],
                        ReqMachine = kv.Key,
                        ReqCount = reqCounts[k],
                        ReqMall = m.UnlockMall,
                    });
                }
            }

            // ── Crestview Commons (available from the start) ──
            void G(string id, string name, string desc, double cost, UpgradeKind kind, double value, string target = null,
                   string reqMachine = null, int reqCount = 0, int reqMall = 0, int reqStratum = -1, int reqTool = -1)
            {
                list.Add(new UpgradeDef
                {
                    Id = id, Name = name, Desc = desc, Cost = cost, Kind = kind, Value = value, Target = target,
                    ReqMachine = reqMachine, ReqCount = reqCount, ReqMall = reqMall, ReqStratum = reqStratum, ReqTool = reqTool
                });
            }

            G("coinop", "Coin-Op Hookup", "Wire your pocket straight into the vending machine.", 150, UpgradeKind.AutoSell, 1, reqStratum: 1);
            G("griptape", "Grip Tape", "Your tools stop slipping out of your sticky hands.", 30, UpgradeKind.ClickMult, 2, reqTool: 1);
            G("rubbergloves", "Rubber Gloves", "The syrup cannot get you now.", 900, UpgradeKind.ClickMult, 2, reqTool: 3);
            G("carpal", "Carpal Tunnel Insurance", "Your clicks now carry the momentum of the whole crew.", 4000, UpgradeKind.ClickPctOfDig, 0.02, reqMachine: "walkers", reqCount: 5);
            G("ergomouse", "Ergonomic Mouse", "Curved. Cushioned. Terrifyingly efficient.", 2e6, UpgradeKind.ClickPctOfDig, 0.03, reqMachine: "jackhammer", reqCount: 1);
            G("rageclick", "Rage Clicking Seminar", "Channel every frustration straight into the crust.", 5e8, UpgradeKind.ClickPctOfDig, 0.05, reqMachine: "jackhammer", reqCount: 25);
            G("polish", "Penny Polish", "Shiny pennies fetch more at the vending machine.", 250, UpgradeKind.ValueMult, 1.25, reqStratum: 1);
            G("coinrolls", "Coin Roll Wrappers", "Neatly rolled coins look legit.", 6000, UpgradeKind.ValueMult, 1.25, reqStratum: 2);
            G("numismatist", "Numismatist on Retainer", "An expert who spots the rare dates.", 8e5, UpgradeKind.ValueMult, 1.5, reqStratum: 5);
            G("roadshow", "Antique Roadshow Appearance", "Your finds get appraised on television.", 8e7, UpgradeKind.ValueMult, 1.5, reqStratum: 6);
            G("butterflynet", "Butterfly Net", "For catching wishes, obviously.", 350, UpgradeKind.WishLife, 1.5, reqMachine: "tumbler", reqCount: 1);
            G("wishmagnet", "Wish Magnet", "A fridge magnet shaped like a star.", 15000, UpgradeKind.WishFreq, 1.5, reqMachine: "tumbler", reqCount: 10);
            G("glitterglue", "Glitter Glue", "Everything is better with glitter. Especially wishes.", 2.5e5, UpgradeKind.WishValue, 2, reqMachine: "dishwasher", reqCount: 10);
            G("shootingstar", "Shooting Star Registry", "Officially register every wish you catch.", 2.5e7, UpgradeKind.WishValue, 2, reqMachine: "acid", reqCount: 10);
            G("headphones", "Metal Detector Headphones", "Hear the good stuff coming.", 8000, UpgradeKind.RelicRate, 1.5, reqMachine: "pigeons", reqCount: 5);
            G("curator", "Museum Curator Friend", "She knows a guy.", 4e5, UpgradeKind.RelicValue, 2, reqMachine: "coinstar", reqCount: 10);
            G("rabbitfoot", "Lucky Rabbit's Foot", "Synthetic. Still works.", 1500, UpgradeKind.GoldenFreq, 1.3, reqStratum: 1);
            G("clover", "Four-Leaf Clover", "Found in the planter by the food court.", 1.5e6, UpgradeKind.GoldenPower, 1.5, reqStratum: 5);
            G("rhythm", "Rhythm Game Training", "Hours of practice at the arcade finally pay off.", 700, UpgradeKind.ComboMax, 1, reqTool: 2);
            G("scrapdealer", "Scrap Dealer Contacts", "A guy in the parking lot buys gunk by the bucket.", 600, UpgradeKind.ScrapRate, 0.10, reqStratum: 1);
            G("biggerbuckets", "Bigger Buckets", "Industrial pails for every digger.", 4e4, UpgradeKind.StageMult, 1.5, "Dig", reqMachine: "walkers", reqCount: 10);
            G("extrafizzy", "Extra-Fizzy Formula", "Twice the carbonation, twice the fizz.", 4e4, UpgradeKind.StageMult, 1.5, "Wash", reqMachine: "dishwasher", reqCount: 5);
            G("sortinghat", "Pigeon-Sized Sorting Hat", "Tiny hats. Big decisions.", 4e4, UpgradeKind.StageMult, 1.5, "Sort", reqMachine: "coinstar", reqCount: 5);
            G("compressortune", "Compressor Tune-Up", "A wrench, a whack and a new filter.", 5e6, UpgradeKind.CompressorEff, 0.10, reqMachine: "compressor", reqCount: 1);
            G("mallpa", "Mall PA Announcement", "\"Attention shoppers: the fountain crew is on fire today.\"", 1.5e7, UpgradeKind.AllRateMult, 1.25, reqStratum: 6);
            G("securityguard", "Bribe the Security Guard", "He looks the other way.", 3e8, UpgradeKind.AllRateMult, 1.25, reqStratum: 7);

            // ── Mall-specific upgrades: price = 10^(mall offset + exponent) Crestview dollars ──
            double[] mallExp = { 0, 3, 3.5, 4, 4.5, 5 };
            void MU(int mall, string id, string name, string desc, double costExp, UpgradeKind kind, double value, string target = null)
            {
                list.Add(new UpgradeDef
                {
                    Id = id, Name = name, Desc = desc, Cost = Math.Pow(10, mallExp[mall] + costExp),
                    Kind = kind, Value = value, Target = target, ReqMall = mall, ReqStratum = -1, MallOnly = true
                });
            }

            MU(1, "konami", "Konami Code", "Up, up, down, down... Clicks dig 3× more.", 0.5, UpgradeKind.ClickMult, 3);
            MU(1, "mixtape", "Mixtape Motivation", "A killer workout mixtape. Every machine ×2.", 1.5, UpgradeKind.AllRateMult, 2);
            MU(1, "neonsigns", "Neon Signage", "Wishes can't resist the glow. Wishes appear 50% more often.", 2, UpgradeKind.WishFreq, 1.5);
            MU(1, "tickets", "Arcade Ticket Exchange", "Trade tickets for cash at a very favourable rate. Everything sells for 2× more.", 3, UpgradeKind.ValueMult, 2);
            MU(1, "tubular", "Totally Tubular Tubes", "All washing machines ×3.", 4, UpgradeKind.StageMult, 3, "Wash");
            MU(1, "powerglove", "Power Glove", "It's so bad. Every click also digs 20% of your automatic dig per second.", 4.5, UpgradeKind.ClickPctOfDig, 0.2);
            MU(1, "rewind", "Be Kind, Rewind", "Wishes linger 50% longer.", 5, UpgradeKind.WishLife, 1.5);
            MU(1, "segway", "Mall Cop Segway Patrol", "All digging machines ×3.", 5.5, UpgradeKind.StageMult, 3, "Dig");
            MU(1, "prizetickets", "Prize Ticket Surplus", "All sorting machines ×3.", 6, UpgradeKind.StageMult, 3, "Sort");
            MU(1, "radical", "Radical Marketing", "Everything sells for 3× more. Tubular.", 7, UpgradeKind.ValueMult, 3);
            MU(1, "laserencore", "Laser Show Encore", "The 1983 laser show, back by popular demand.", 8, UpgradeKind.AllRateMult, 2);
            MU(1, "halloffame", "High Score Hall of Fame", "Every initial in the fountain is famous now.", 9, UpgradeKind.ValueMult, 2);
            MU(1, "synthoverdrive", "Synthesizer Overdrive", "The car wash brushes spin to a keytar solo.", 10, UpgradeKind.StageMult, 3, "Wash");
            MU(1, "rubikalgo", "Rubik's Algorithm", "Sorted in 20 moves or fewer.", 11, UpgradeKind.StageMult, 3, "Sort");
            MU(1, "neondrill", "Neon Drill Bits", "Pink. Sharp. Radical.", 12, UpgradeKind.StageMult, 3, "Dig");

            MU(2, "butler", "Hire a Butler", "He oversees the machines, disapprovingly. Every machine ×2.", 0.5, UpgradeKind.AllRateMult, 2);
            MU(2, "sommelier", "Wish Sommelier", "'Notes of 1994 and regret.' Wishes are worth 3× more.", 1.5, UpgradeKind.WishValue, 3);
            MU(2, "appraiser", "Private Appraiser", "Relics sell for 3× more.", 2, UpgradeKind.RelicValue, 3);
            MU(2, "offshore", "Offshore Accounting", "Legally dubious. Everything sells for 3× more.", 3, UpgradeKind.ValueMult, 3);
            MU(2, "goldcard", "Gold Card Perks", "Golden pennies appear 30% more often.", 3.5, UpgradeKind.GoldenFreq, 1.3);
            MU(2, "yachtparty", "Yacht Party Networking", "All sorting machines ×3.", 4, UpgradeKind.StageMult, 3, "Sort");
            MU(2, "marbletools", "Marble-Handled Tools", "Clicks dig 5× more.", 4.5, UpgradeKind.ClickMult, 5);
            MU(2, "caviarfuel", "Caviar-Powered Engines", "All digging machines ×3.", 5, UpgradeKind.StageMult, 3, "Dig");
            MU(2, "champshower", "Champagne Shower", "All washing machines ×3.", 6, UpgradeKind.StageMult, 3, "Wash");
            MU(2, "trustfund", "Trust Fund", "Everything sells for 2× more.", 7, UpgradeKind.ValueMult, 2);
            MU(2, "personalshoppers", "Personal Shopper Army", "They carry the crust in monogrammed bags.", 8, UpgradeKind.AllRateMult, 2);
            MU(2, "diamonddust", "Diamond-Dust Polish", "Even the pennies sparkle now.", 9, UpgradeKind.ValueMult, 2);
            MU(2, "spaday", "Spa Day for the Machines", "Cucumber slices on every washer.", 10, UpgradeKind.StageMult, 3, "Wash");
            MU(2, "concierge", "Concierge Sorting Service", "Every coin is personally greeted and sorted.", 11, UpgradeKind.StageMult, 3, "Sort");
            MU(2, "golddrills", "Gold-Plated Drill Heads", "Softer than steel. Much more expensive. Somehow faster.", 12, UpgradeKind.StageMult, 3, "Dig");

            MU(3, "priority", "Priority Boarding", "Every machine ×3.", 0.5, UpgradeKind.AllRateMult, 3);
            MU(3, "dutyfree", "Duty-Free Pricing", "Everything sells for 3× more.", 1.5, UpgradeKind.ValueMult, 3);
            MU(3, "flyermiles", "Frequent Flyer Miles", "Wishes appear 50% more often.", 2, UpgradeKind.WishFreq, 1.5);
            MU(3, "carryon", "Carry-On Only", "All washing machines ×3.", 3, UpgradeKind.StageMult, 3, "Wash");
            MU(3, "precheck", "Trusted Traveller Lane", "All sorting machines ×3.", 3.5, UpgradeKind.StageMult, 3, "Sort");
            MU(3, "jetfuel", "Jet Fuel Additive", "All digging machines ×3.", 4, UpgradeKind.StageMult, 3, "Dig");
            MU(3, "lounge", "Lounge Access", "Wishes linger 50% longer.", 4.5, UpgradeKind.WishLife, 1.5);
            MU(3, "lostfound", "Lost & Found Connections", "Relic finds ×2.", 5, UpgradeKind.RelicRate, 2);
            MU(3, "pilotwings", "Pilot Wings", "Clicks dig 5× more.", 5.5, UpgradeKind.ClickMult, 5);
            MU(3, "exchangedesk", "Currency Exchange Desk", "Everything sells for 2× more.", 7, UpgradeKind.ValueMult, 2);
            MU(3, "redeye", "Red-Eye Night Shift", "The crews work while the flights sleep.", 8, UpgradeKind.AllRateMult, 2);
            MU(3, "arbitrage", "Global Currency Arbitrage", "Buy yen, sell loonies, repeat.", 9, UpgradeKind.ValueMult, 2);
            MU(3, "deicing", "De-Icing Sprayers", "Hot glycol melts forty years of syrup.", 10, UpgradeKind.StageMult, 3, "Wash");
            MU(3, "xrayline", "Baggage X-Ray Line", "Everything goes through the scanner. Everything.", 11, UpgradeKind.StageMult, 3, "Sort");
            MU(3, "runwaydiggers", "Runway Excavators", "Borrowed from the tarmac crew. Please return by 6 AM.", 12, UpgradeKind.StageMult, 3, "Dig");

            MU(4, "cardcount", "Card Counting", "Everything sells for 3× more. Not technically illegal.", 0.5, UpgradeKind.ValueMult, 3);
            MU(4, "compbuffet", "Comped Buffet", "Well-fed crews work harder. Every machine ×3.", 1.5, UpgradeKind.AllRateMult, 3);
            MU(4, "highroller", "High Roller Status", "Wishes are worth 3× more.", 2, UpgradeKind.WishValue, 3);
            MU(4, "loadeddice", "Loaded Dice", "Golden penny effects ×2.", 3, UpgradeKind.GoldenPower, 2);
            MU(4, "luckysevens", "Lucky Sevens", "Golden pennies appear 50% more often.", 3.5, UpgradeKind.GoldenFreq, 1.5);
            MU(4, "elvis", "The King's Blessing", "Relics sell for 3× more. Thank you very much.", 4, UpgradeKind.RelicValue, 3);
            MU(4, "chipstacks", "Chip Stacks", "All sorting machines ×3.", 4.5, UpgradeKind.StageMult, 3, "Sort");
            MU(4, "chandelier", "Chandelier Spotlight", "All washing machines ×3.", 5, UpgradeKind.StageMult, 3, "Wash");
            MU(4, "slotlever", "Slot Lever Arms", "All digging machines ×3.", 5.5, UpgradeKind.StageMult, 3, "Dig");
            MU(4, "doubledown", "Double Down", "Everything sells for 2× more.", 7, UpgradeKind.ValueMult, 2);
            MU(4, "floorcrew", "24-Hour Floor Crew", "No clocks, no windows, no breaks.", 8, UpgradeKind.AllRateMult, 2);
            MU(4, "highlimit", "High-Limit Room", "Your loot only sells to whales now.", 9, UpgradeKind.ValueMult, 2);
            MU(4, "chocfountain", "Chocolate Fountain Rinse", "Borrowed from the buffet. Nobody will notice.", 10, UpgradeKind.StageMult, 3, "Wash");
            MU(4, "cardshuffler", "Card Shuffler Sorting", "Shuffled, dealt and sorted in one motion.", 11, UpgradeKind.StageMult, 3, "Sort");
            MU(4, "jackpotdrills", "Jackpot Drills", "Every hole pays out.", 12, UpgradeKind.StageMult, 3, "Dig");

            MU(5, "atomicage", "Atomic Age Engineering", "Every machine ×3. The future is now.", 0.5, UpgradeKind.AllRateMult, 3);
            MU(5, "malted", "Extra-Thick Malted", "Everything sells for 3× more.", 1.5, UpgradeKind.ValueMult, 3);
            MU(5, "aqueduct", "Roman Aqueduct Engineers", "All washing machines ×4.", 2, UpgradeKind.StageMult, 4, "Wash");
            MU(5, "treasuremap", "Pirate Treasure Map", "Relic finds ×2. X marks the fountain.", 3, UpgradeKind.RelicRate, 2);
            MU(5, "oracle", "The Oracle's Advice", "Wishes are worth 3× more.", 3.5, UpgradeKind.WishValue, 3);
            MU(5, "centurion", "Centurion Discipline", "All sorting machines ×4.", 4, UpgradeKind.StageMult, 4, "Sort");
            MU(5, "legion", "Legion Dig Crews", "All digging machines ×4.", 4.5, UpgradeKind.StageMult, 4, "Dig");
            MU(5, "neptune", "Neptune's Favour", "Every machine ×2.", 5, UpgradeKind.AllRateMult, 2);
            MU(5, "whispers", "Whispers from the Well", "Wishes appear 50% more often.", 5.5, UpgradeKind.WishFreq, 1.5);
            MU(5, "brightfuture", "The Future Is Bright", "Everything sells for 3× more.", 7, UpgradeKind.ValueMult, 3);
            MU(5, "timecapsule", "Time Capsule Crews", "They dig at the speed of history.", 8, UpgradeKind.AllRateMult, 2);
            MU(5, "museumdeal", "Museum Acquisition Deal", "Every find is on loan to the Louvre-ish.", 9, UpgradeKind.ValueMult, 2);
            MU(5, "bathhouse", "Roman Bathhouse Rinse", "Two thousand years of experience in getting things clean.", 10, UpgradeKind.StageMult, 3, "Wash");
            MU(5, "oraclesort", "Oracle Sorting", "She already knows which coins are worth keeping.", 11, UpgradeKind.StageMult, 3, "Sort");
            MU(5, "atomicdrills", "Atomic Drill Array", "Powered by a tiny, friendly reactor.", 12, UpgradeKind.StageMult, 3, "Dig");

            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            return list.ToArray();
        }

        static HeadOfficeDef[] BuildHeadOffice()
        {
            var list = new List<HeadOfficeDef>();
            void H(string id, string name, string desc, HOKind kind, double value, int max, double cost, double growth)
                => list.Add(new HeadOfficeDef { Id = id, Name = name, Desc = desc, Kind = kind, Value = value, MaxLevel = max, BaseCost = cost, CostGrowth = growth });

            H("union", "Union Wages", "Happy crews work faster. Every machine +25% per level.", HOKind.RateMult, 0.25, 60, 1, 1.32);
            H("contracts", "Better Contracts", "Negotiate harder. Everything sells for +25% per level.", HOKind.ValueMult, 0.25, 60, 1, 1.32);
            H("strongarms", "Strong Arms", "Clicks dig +50% per level.", HOKind.ClickMult, 0.5, 20, 1, 1.45);
            H("seed", "Seed Money", "Start every mall with some cash in the tip jar.", HOKind.SeedMoney, 1, 10, 1, 1.6);
            H("toolkit", "Franchise Toolkit", "Start every mall with a better tool already in hand.", HOKind.StartTool, 1, 6, 2, 1.8);
            H("veterans", "Veteran Crew", "Start every mall with 5 of each basic machine per level.", HOKind.VeteranCrew, 5, 6, 3, 1.9);
            H("autosell", "Auto-Sell License", "Start every mall with the Coin-Op Hookup installed.", HOKind.AutoSellStart, 1, 1, 2, 1);
            H("netpro", "Butterfly Net Pro", "Wishes linger +15% longer per level.", HOKind.WishLife, 0.15, 6, 2, 1.6);
            H("whisperer", "Wish Whisperer", "Wishes are worth +30% per level.", HOKind.WishValue, 0.3, 25, 2, 1.4);
            H("charm", "Lucky Charm", "Golden pennies appear +10% more often per level.", HOKind.GoldenFreq, 0.10, 8, 3, 1.6);
            H("goldentouch", "Golden Touch", "Golden penny effects +20% per level.", HOKind.GoldenPower, 0.2, 8, 3, 1.6);
            H("radar", "Relic Radar", "Relic finds +20% per level.", HOKind.RelicRate, 0.2, 10, 2, 1.5);
            H("combocoach", "Combo Coach", "Your click combo can climb +0.5 higher per level.", HOKind.ComboMax, 0.5, 6, 2, 1.6);
            H("deeppockets", "Deep Pockets", "Raw gunk and unsorted loot sell for +5% of full value per level.", HOKind.ScrapRate, 0.05, 5, 2, 1.5);
            H("bulk", "Bulk Discount", "Machines cost 3% less per level.", HOKind.CostDiscount, 0.03, 10, 4, 1.7);
            H("nightshift", "Night Shift", "Earn offline for +2 more hours per level.", HOKind.OfflineHours, 2, 11, 1, 1.4);
            H("graveyard", "Graveyard Shift Crew", "Offline earnings +5% efficiency per level.", HOKind.OfflineEff, 0.05, 10, 2, 1.5);
            H("patent", "Compressor Patent", "The Wish Compressor is unlocked from the start of every mall, and catches +5% per level.", HOKind.CompressorStart, 0.05, 3, 5, 2);
            for (int i = 0; i < list.Count; i++) list[i].Index = i;
            return list.ToArray();
        }
    }
}
