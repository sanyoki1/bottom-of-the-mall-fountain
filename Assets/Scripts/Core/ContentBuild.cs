// Everything you can place in build mode (generators, intakes, belts, hoppers, processors) and
// the tech nodes that unlock them. Machine prices are flat per unit; the research is the big cost.
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
            B("gen_hamster", "Hamster Wheel", BuildCat.Power, 1, 1, 5, 3, "unlock_hamster", 0xC8A070,
                "Gerald runs. The lights flicker. Gerald is paid in sunflower seeds and is unionising.");
            B("gen_diesel", "Diesel Generator", BuildCat.Power, 2, 2, 150, 25, "unlock_diesel", 0xE8C020,
                "Loud, smoky, and technically not allowed indoors. Mall management has been told it's a 'fog machine'.");
            B("gen_fryer", "Fryer-Oil Generator", BuildCat.Power, 2, 2, 2500, 90, "unlock_fryer", 0xE87A2A,
                "Runs on used food court fryer oil. The whole mall smells like a Tuesday.");
            B("gen_solar", "Skylight Solar Collector", BuildCat.Power, 2, 2, 40000, 400, "unlock_solar", 0x3A6FD6,
                "A light pipe from the skylight into a very confident box. It works at night. Don't ask.");

            // ── intakes: stand at the rim, reach into the fountain, output out the back ──
            var sk = B("intake_skimmer", "Pool Skimmer Bot", BuildCat.Intake, 1, 2, 25, -3, "unlock_skimmer", 0x39E5D0,
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
            var hop = B("hopper", "Deposit Hopper", BuildCat.Output, 2, 2, 20, 0, "unlock_hopper", 0x2E8C7A,
                "A COIN-O-MATIC with no screen, no receipts and no personality. Wired straight into your account.");
            hop.AnySideInput = true; hop.Rate = 8; hop.Capacity = 100;
            var hop2 = B("hopper2", "Armoured Deposit Hopper", BuildCat.Output, 2, 2, 5000, -10, "unlock_hopper2", 0x5A6470,
                "Bulletproof, bank-grade and deeply overqualified for a mall fountain. Pays a little extra, for dignity.");
            hop2.AnySideInput = true; hop2.Rate = 60; hop2.Capacity = 400; hop2.SellMult = 1.05;

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
            U("unlock_hamster", "Hamster Wheel Power", TechBranch.Power, "gen_hamster", 15, 0, 0, "Adopt Gerald. Gerald wants to help.");
            U("unlock_diesel", "Diesel Generator", TechBranch.Power, "gen_diesel", 300, 1, 0, "Proper power, improper fumes.", "unlock_hamster");
            U("unlock_fryer", "Fryer-Oil Generator", TechBranch.Power, "gen_fryer", 4000, 2, 0, "The food court throws out forty litres of oil a day. Not any more.", "unlock_diesel");
            U("unlock_solar", "Skylight Solar", TechBranch.Power, "gen_solar", 50000, 3, 0, "The skylight was always there. You just had to believe in it.", "unlock_fryer");
            Lv("power_overclock", "Overclocked Generators", TechBranch.Power, TechKind.MachineSpeed, "Power", 0.1, 80, 1.9, 15, 0, 1,
                "Wires, rewired. Fuses, replaced with pennies. Output goes up. So does the risk.", "unlock_hamster");
            // intake
            U("unlock_skimmer", "Pool Skimmer Bot", TechBranch.Intake, "intake_skimmer", 25, 0, 0, "A second-hand pool robot. It has seen things in pools.", "unlock_hamster");
            U("unlock_pump", "Fountain Drain Pump", TechBranch.Intake, "intake_pump", 900, 1, 0, "Industrial suction for a commercial fountain.", "unlock_skimmer", "unlock_diesel");
            U("unlock_claw", "Claw Machine Crane", TechBranch.Intake, "intake_claw", 12000, 2, 0, "Bought from the arcade when it closed. Finally, a claw that doesn't cheat.", "unlock_pump");
            Lv("intake_firmware", "Intake Firmware", TechBranch.Intake, TechKind.MachineSpeed, "Intake", 0.15, 60, 1.8, 15, 0, 1,
                "Update available: 'fixed a bug where robot was sad'.", "unlock_skimmer");
            // logistics
            U("unlock_belts", "Conveyor Belts", TechBranch.Logistics, "belt", 8, 0, 0, "Move things without walking. Revolutionary.");
            U("unlock_hopper", "Wired Deposit", TechBranch.Logistics, "hopper", 30, 1, 0, "Head Office agrees to take deposits from a box. Build hoppers anywhere.", "unlock_belts");
            U("unlock_splitter", "Belt Splitter", TechBranch.Logistics, "splitter", 150, 2, 0, "One belt becomes three. Physics is fine with this.", "unlock_belts");
            U("unlock_hopper2", "Armoured Hopper", TechBranch.Logistics, "hopper2", 6000, 3, 0, "For when the regular hopper can't keep up with your success.", "unlock_hopper");
            var fast = Lv("belt_fast", "Fast Belts", TechBranch.Logistics, TechKind.BeltSpeed, null, 1, 600, 1, 1, 0, 1, "Every belt runs twice as fast. The ketchup smell intensifies.", "unlock_belts");
            Lv("belt_express", "Express Belts", TechBranch.Logistics, TechKind.BeltSpeed, null, 2, 20000, 1, 1, 1, 1, "Twice as fast again. Coins arrive slightly warm.", "belt_fast");
            Lv("hopper_speed", "Hopper Throughput", TechBranch.Logistics, TechKind.MachineSpeed, "Output", 0.2, 100, 1.9, 10, 2, 1,
                "Greased the chutes with butter from the pretzel stand.", "unlock_hopper");
        }
    }
}
