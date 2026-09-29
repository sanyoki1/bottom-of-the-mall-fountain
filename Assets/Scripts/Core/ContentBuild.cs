// Everything you can place in build mode (generators, intakes, belts, hoppers, processors) and
// the tech nodes that unlock them. Machine prices are flat per unit; the research is the big cost.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public static partial class Content
    {
        public static readonly Dictionary<string, int> BuildIndex = new Dictionary<string, int>();

        static BuildDef[] BuildBuildables()
        {
            var list = new List<BuildDef>();
            BuildDef B(string id, string name, BuildCat cat, int w, int d, double cost, double power, string tech, uint color, string desc)
            {
                var b = new BuildDef { Id = id, Name = name, Cat = cat, W = w, D = d, Cost = cost, Power = power, Tech = tech, Color = color, Desc = desc, Index = list.Count };
                list.Add(b);
                return b;
            }
            // ── power ──
            B("gen_hamster", "Hamster Wheel", BuildCat.Power, 1, 1, 2, 3, "unlock_hamster", 0xC8A070,
                "Gerald runs. The lights flicker. Gerald is paid in sunflower seeds and is unionising.");
            B("gen_diesel", "Diesel Generator", BuildCat.Power, 2, 2, 150, 25, "unlock_diesel", 0xE8C020,
                "Loud, smoky, and technically not allowed indoors. Mall management has been told it's a 'fog machine'.");
            B("gen_fryer", "Fryer-Oil Generator", BuildCat.Power, 2, 2, 2500, 90, "unlock_fryer", 0xE87A2A,
                "Runs on used food court fryer oil. The whole mall smells like a Tuesday.");
            B("gen_solar", "Skylight Solar Collector", BuildCat.Power, 2, 2, 40000, 400, "unlock_solar", 0x3A6FD6,
                "A light pipe from the skylight into a very confident box. It works at night. Don't ask.");

            // ── intakes: stand at the rim, reach into the fountain, output out the back ──
            var sk = B("intake_skimmer", "Pool Skimmer Bot", BuildCat.Intake, 1, 2, 10, -3, "unlock_skimmer", 0x39E5D0,
                "A pool-cleaning robot that roams the fountain eating coins, then trundles home to its dock to unload.");
            sk.RimOnly = true; sk.Intake = "skimmer"; sk.Rate = 1.2; sk.Capacity = 12; sk.Reach = 8;
            sk.Outputs = new[] { (0, 0, 2) };
            var pump = B("intake_pump", "Fountain Drain Pump", BuildCat.Intake, 2, 2, 700, -12, "unlock_pump", 0x2E6FD6,
                "Sucks everything within a few metres of its hose. Occasionally sucks a sock. Nobody knows whose.");
            pump.RimOnly = true; pump.Intake = "pump"; pump.Rate = 5; pump.Capacity = 40; pump.Reach = 4.5f;
            pump.Outputs = new[] { (0, 0, 2) };
            var claw = B("intake_claw", "Claw Machine Crane", BuildCat.Intake, 2, 2, 9000, -35, "unlock_claw", 0xFF4FA8,
                "The arcade claw, but it never drops anything. Goes straight for the shiniest thing in the fountain.");
            claw.RimOnly = true; claw.Intake = "claw"; claw.Rate = 3; claw.Capacity = 60; claw.Reach = 9;
            claw.Outputs = new[] { (0, 0, 2) };

            // dig rigs: stand at the rim and chew through the crust
            var rig = B("dig_rig", "Crust Jackhammer Rig", BuildCat.Intake, 2, 2, 1500, -20, "unlock_digrig", 0xE8C020,
                "A jackhammer on a crane arm, pounding the crust from the rim. The food court has filed a noise complaint.");
            rig.RimOnly = true; rig.Intake = "dig"; rig.Rate = 3; rig.Capacity = 60; rig.Reach = 6;
            rig.Outputs = new[] { (0, 0, 2), (1, 0, 2) };
            var borer = B("dig_borer", "Tunnel Borer", BuildCat.Intake, 2, 3, 60000, -120, "unlock_borer", 0x8A8F96,
                "Rented from a subway project that 'won't miss it'. Eats crust like the crust owes it money.");
            borer.RimOnly = true; borer.Intake = "dig"; borer.Rate = 40; borer.Capacity = 300; borer.Reach = 7;
            borer.Outputs = new[] { (0, 0, 2), (1, 0, 2) };

            // ── processing: raw gunk is worth pennies, washed loot a third, sorted loot full price ──
            BuildDef P(string id, string name, int w, int d, double cost, double power, string process, double rate, int cap, string tech, uint color, string desc)
            {
                var p = B(id, name, BuildCat.Processing, w, d, cost, power, tech, color, desc);
                p.Process = process; p.Rate = rate; p.Capacity = cap;
                var ins = new List<(int, int, int)>();
                var outs = new List<(int, int, int)>();
                for (int x = 0; x < w; x++) { ins.Add((x, 0, 2)); outs.Add((x, d - 1, 0)); }
                p.Inputs = ins.ToArray();
                p.Outputs = outs.ToArray();
                return p;
            }
            P("proc_tumbler", "Rinse Tumbler", 2, 2, 400, -8, "wash", 3, 30, "unlock_tumbler", 0x3A7BD5,
                "A laundromat dryer full of diet cola and gunk. The syrup dissolves. The smell does not.");
            P("proc_pigeons", "Pigeon Sorter", 2, 2, 900, -4, "sort", 1.5, 30, "unlock_pigeons", 0x8A8F9A,
                "Twelve trained pigeons pick the good stuff out of the wet stuff. They are paid in pretzel crumbs.");
            P("proc_sorter", "Coin Sorter", 2, 2, 12000, -20, "sort", 10, 80, "unlock_sorter", 0xC9CDD2,
                "A bank-grade coin counter that rattles like a slot machine and sorts faster than the pigeons (don't tell them).");
            P("proc_roller", "Coin Roller", 2, 2, 3000, -10, "roll", 25, 120, "unlock_roller", 0xC77B43,
                "Wraps fifty coins in a paper sleeve. Rolls carry and ship far better than loose coins, and pay 10% more.");
            P("proc_bagger", "Coin Bagger", 2, 2, 40000, -25, "bag", 4, 60, "unlock_bagger", 0xB89A5A,
                "Twenty rolls into a canvas sack with a dollar sign on it. Pays 15% more. As is tradition.");
            P("proc_pallet", "Palletiser", 3, 3, 400000, -80, "pallet", 1, 60, "unlock_pallet", 0xA87A48,
                "Forty bags, shrink-wrapped onto a pallet with a forklift nobody is licensed to drive.");
            P("proc_melter", "Gold Melter", 2, 2, 25000, -40, "melt", 3, 40, "unlock_melter", 0xF2C230,
                "Gold coins, rings and tiaras go in; bars worth 35% more come out. The fire marshal is on holiday.");
            var comp = P("proc_compressor", "Wish Compressor", 2, 2, 15000, -30, "compress", 1, 40, "unlock_compressor", 0x9A5CF7,
                "Catches every wish you don't and presses it into a Wish Brick (plus half its tokens). Nostalgia, by the kilo.");
            comp.Inputs = Array.Empty<(int, int, int)>();

            // ── logistics ──
            var belt = B("belt", "Conveyor Belt", BuildCat.Logistics, 1, 1, 0.5, 0, "unlock_belts", 0x3A3A3A,
                "A rubber belt salvaged from the food court's tray return. Still faintly smells of ketchup.");
            belt.IsBelt = true; belt.BeltSpeed = 1.2f; belt.Capacity = 4;
            var split = B("splitter", "Belt Splitter", BuildCat.Logistics, 1, 1, 60, 0, "unlock_splitter", 0xF2B233,
                "Sends items left, right and straight on in turn. Fair to a fault.");
            split.IsSplitter = true; split.Capacity = 4;
            split.Inputs = new[] { (0, 0, 2) };
            split.Outputs = new[] { (0, 0, 0), (0, 0, 1), (0, 0, 3) };

            // ── output ──
            var hop = B("hopper", "Deposit Hopper", BuildCat.Output, 2, 2, 12, 0, "unlock_hopper", 0x2E8C7A,
                "A COIN-O-MATIC with no screen, no receipts and no personality. Wired straight into your account.");
            hop.AnySideInput = true; hop.Rate = 8; hop.Capacity = 100;
            var hop2 = B("hopper2", "Armoured Deposit Hopper", BuildCat.Output, 2, 2, 5000, -10, "unlock_hopper2", 0x5A6470,
                "Bulletproof, bank-grade and deeply overqualified for a mall fountain. Pays a little extra, for dignity.");
            hop2.AnySideInput = true; hop2.Rate = 60; hop2.Capacity = 400; hop2.SellMult = 1.05;

            // ── the mall-only machines: one per late mall, researched there and nowhere else (SimMallMachines) ──
            var cannon = B("dig_cannon", "Champagne Cork Cannon", BuildCat.Intake, 2, 2, 40000, -30, "unlock_cannon", 0xE8C878,
                "A gilded cannon with a magnum of '96 for a barrel. Lobs a cork over the rim and a slab of crust comes off, rinsed on the way down. Leave the rim to pumps and claws.");
            cannon.Intake = "cannon"; cannon.Rate = 0.25; cannon.MaxRange = Balance.CannonRange;
            var carousel = B("carousel", "Baggage Claim Carousel", BuildCat.Logistics, 3, 3, 250000, -20, "unlock_carousel", 0x9AA4AE,
                "Carousel 4 (now Carousel 7). Its cargo drones empty every rim intake with no line behind it and bring it all here. The line starts at its back.");
            carousel.IsCarousel = true; carousel.Capacity = 800; carousel.Outputs = new[] { (1, 2, 0) };
            P("proc_slots", "Slot-Machine Sorter", 2, 2, 800000, -30, "slots", 9, 80, "unlock_slots", 0xD8283A,
                "One arm, no conscience. Spins once per chunk of raw gunk: cherries pay it out sorted, BAR double, 7-7-BAR five times, 7-7-7 sprays the fountain. The house keeps the rest.");
            var well = B("wishing_well", "The Old Well", BuildCat.Intake, 2, 2, 8000000, 0, "unlock_well", 0x8A8F7A,
                "The well Eternity Plaza was poured over, reopened. Wishes nobody catches fall in, and a chunk of crust simply stops existing. There is only one.");
            well.RimOnly = true; well.Intake = "well"; well.Unique = true;

            var arr = list.ToArray();
            foreach (var b in arr) BuildIndex[b.Id] = b.Index;
            return arr;
        }

        /// <summary>Tech nodes for the factory branches (called from BuildTechs).</summary>
        static void AddFactoryTechs(System.Func<string, string, TechBranch, TechKind, double, double, string, string[], TechDef> T)
        {
            TechDef U(string id, string name, TechBranch b, string target, double cost, int col, int row, string desc, params string[] req)
            {
                var t = T(id, name, b, TechKind.Unlock, 0, cost, desc, req);
                t.Target = target; t.Col = col; t.Row = row;
                return t;
            }
            TechDef Lv(string id, string name, TechBranch b, TechKind kind, string target, double value, double cost, double growth, int max, int col, int row, string desc, params string[] req)
            {
                var t = T(id, name, b, kind, value, cost, desc, req);
                t.Target = target; t.CostGrowth = growth; t.MaxLevel = max; t.Col = col; t.Row = row;
                return t;
            }
            // power
            U("unlock_hamster", "Hamster Wheel Power", TechBranch.Power, "gen_hamster", 6, 0, 0, "Adopt Gerald. Gerald wants to help.");
            U("unlock_diesel", "Diesel Generator", TechBranch.Power, "gen_diesel", 300, 1, 0, "Proper power, improper fumes.", "unlock_hamster");
            U("unlock_fryer", "Fryer-Oil Generator", TechBranch.Power, "gen_fryer", 4000, 2, 0, "The food court throws out forty litres of oil a day. Not any more.", "unlock_diesel");
            U("unlock_solar", "Skylight Solar", TechBranch.Power, "gen_solar", 50000, 3, 0, "The skylight was always there. You just had to believe in it.", "unlock_fryer");
            Lv("power_overclock", "Overclocked Generators", TechBranch.Power, TechKind.MachineSpeed, "Power", 0.1, 80, 1.9, 15, 0, 1,
                "Wires, rewired. Fuses, replaced with pennies. Output goes up. So does the risk.", "unlock_hamster");
            // intake
            U("unlock_skimmer", "Pool Skimmer Bot", TechBranch.Intake, "intake_skimmer", 12, 0, 0, "A second-hand pool robot. It has seen things in pools.", "unlock_hamster");
            U("unlock_pump", "Fountain Drain Pump", TechBranch.Intake, "intake_pump", 900, 1, 0, "Industrial suction for a commercial fountain.", "unlock_skimmer", "unlock_diesel");
            U("unlock_claw", "Claw Machine Crane", TechBranch.Intake, "intake_claw", 12000, 2, 0, "Bought from the arcade when it closed. Finally, a claw that doesn't cheat.", "unlock_pump");
            Lv("intake_firmware", "Intake Firmware", TechBranch.Intake, TechKind.MachineSpeed, "Intake", 0.15, 60, 1.8, 15, 0, 1,
                "Update available: 'fixed a bug where robot was sad'.", "unlock_skimmer");
            U("unlock_digrig", "Crust Jackhammer Rig", TechBranch.Intake, "dig_rig", 1800, 0, 2, "Dig without swinging. Your shoulders send their thanks.", "unlock_skimmer", "unlock_diesel");
            U("unlock_borer", "Tunnel Borer", TechBranch.Intake, "dig_borer", 90000, 1, 2, "A machine that was built to dig subways. Your fountain is basically a very short subway.", "unlock_digrig", "unlock_fryer");
            // processing
            U("unlock_tumbler", "Rinse Tumbler", TechBranch.Processing, "proc_tumbler", 500, 0, 0, "Washed gunk is worth 35% of its loot instead of 10%.", "unlock_belts");
            U("unlock_pigeons", "Pigeon Sorter", TechBranch.Processing, "proc_pigeons", 1200, 1, 0, "Sorted loot is worth full price, and sometimes it's a relic.", "unlock_tumbler");
            U("unlock_sorter", "Coin Sorter", TechBranch.Processing, "proc_sorter", 15000, 2, 0, "Sorting at the speed of commerce.", "unlock_pigeons");
            U("unlock_roller", "Coin Roller", TechBranch.Processing, "proc_roller", 3500, 0, 1, "Fifty coins per roll: one item on a belt, one slot in your bucket.", "unlock_belts");
            U("unlock_bagger", "Coin Bagger", TechBranch.Processing, "proc_bagger", 50000, 1, 1, "A thousand coins per bag.", "unlock_roller");
            U("unlock_pallet", "Palletiser", TechBranch.Processing, "proc_pallet", 500000, 2, 1, "Forty thousand coins per pallet.", "unlock_bagger");
            U("unlock_melter", "Gold Melter", TechBranch.Processing, "proc_melter", 30000, 0, 2, "Shiny things in, shinier bars out.", "unlock_sorter");
            U("unlock_compressor", "Wish Compressor", TechBranch.Processing, "proc_compressor", 20000, 1, 2, "No wish goes uncaught. Some go uncaught and then get compressed.", "unlock_tumbler");
            Lv("proc_speed", "Process Engineering", TechBranch.Processing, TechKind.MachineSpeed, "Processing", 0.15, 400, 1.8, 15, 3, 0,
                "A consultant rearranged the machines. Everything is 15% faster. The consultant charged 40% more.", "unlock_tumbler");
            // logistics
            U("unlock_belts", "Conveyor Belts", TechBranch.Logistics, "belt", 5, 0, 0, "Move things without walking. Revolutionary.");
            U("unlock_hopper", "Wired Deposit", TechBranch.Logistics, "hopper", 12, 1, 0, "Head Office agrees to take deposits from a box. Build hoppers anywhere.", "unlock_belts");
            U("unlock_splitter", "Belt Splitter", TechBranch.Logistics, "splitter", 150, 2, 0, "One belt becomes three. Physics is fine with this.", "unlock_belts");
            U("unlock_hopper2", "Armoured Hopper", TechBranch.Logistics, "hopper2", 6000, 3, 0, "For when the regular hopper can't keep up with your success.", "unlock_hopper");
            var fast = Lv("belt_fast", "Fast Belts", TechBranch.Logistics, TechKind.BeltSpeed, null, 1, 600, 1, 1, 0, 1, "Every belt runs twice as fast. The ketchup smell intensifies.", "unlock_belts");
            Lv("belt_express", "Express Belts", TechBranch.Logistics, TechKind.BeltSpeed, null, 2, 20000, 1, 1, 1, 1, "Twice as fast again. Coins arrive slightly warm.", "belt_fast");
            Lv("hopper_speed", "Hopper Throughput", TechBranch.Logistics, TechKind.MachineSpeed, "Output", 0.2, 100, 1.9, 10, 2, 1,
                "Greased the chutes with butter from the pretzel stand.", "unlock_hopper");

            // ── the mall-only machines: sold only in their own mall (MallOnly), so nothing stacks across the game.
            // The cannon (Aurelia) and the well (Eternity) share grid cells: they never show at the same time.
            TechDef M(TechDef t, int mall) { t.UnlockMall = mall; t.MallOnly = true; return t; }
            M(U("unlock_cannon", "Champagne Blasting", TechBranch.Intake, "dig_cannon", 120000, 2, 2,
                "The Champagne Bar's sommelier insists the crust must be opened, not dug. He brought a cannon.", "unlock_pump"), 2);
            M(Lv("cannon_vintage", "Vintage Reserve", TechBranch.Intake, TechKind.CannonSlab, null, 6, 150000, 2.1, 10, 3, 2,
                "Older bottles, bigger pops. The '82 took out a skylight panel. Management is calling it a feature.", "unlock_cannon"), 2);
            M(U("unlock_carousel", "Baggage Claim", TechBranch.Logistics, "carousel", 3000000, 0, 2,
                "Airport-grade baggage handling. Your coins will be waiting at Carousel 4. Or 7. Please check the screens.", "unlock_hopper"), 3);
            M(Lv("carousel_tags", "Priority Tags", TechBranch.Logistics, TechKind.DroneCount, null, 1, 1500000, 3, 4, 1, 2,
                "Bright orange PRIORITY tags for every bag. They change nothing, but each roll of tags comes with a new drone.", "unlock_carousel"), 3);
            M(U("unlock_slots", "Gaming License", TechBranch.Processing, "proc_slots", 12000000, 2, 2,
                "The gaming commission approves one (1) fountain-based slot machine. You build forty. Nobody reads the forms here.", "unlock_tumbler"), 4);
            M(Lv("slots_reels", "Loosen the Reels", TechBranch.Processing, TechKind.SlotOdds, null, 0.03, 6000000, 2.4, 6, 3, 2,
                "A man named Sal adjusts the reels with a butter knife. The house edge gets a little less edgy.", "unlock_slots"), 4);
            M(U("unlock_well", "Reopen the Old Well", TechBranch.Intake, "wishing_well", 60000000, 2, 2,
                "The blueprints say the fountain was 'poured over an existing well'. Corporate approves one jackhammer to find out what that means.", "unlock_digrig"), 5);
            M(Lv("well_deeper", "Deeper Wishes", TechBranch.Intake, TechKind.WellDepth, null, 0.5, 30000000, 1, 999, 3, 2,
                "More rope, a heavier bucket, bigger wishes. The well has started granting wishes nobody made, just to keep busy.", "unlock_well"), 5).CostPower = 2.5;
        }
    }
}
