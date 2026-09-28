// Balance simulator for the first-person game. Compiles the real Core and plays it with a bot.
// Milestone 1 stub: content counts and a short hand-carry smoke run. The full throughput bot
// (walking times, tech purchases, factory) arrives with the balance pass.
using System;
using System.Collections.Generic;
using System.Linq;
using WishExtractor.Core;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "counts")
        {
            Console.WriteLine($"malls {Content.Malls.Length}, items {Content.Items.Length} (coins {Content.CoinTiers.Count}, oddities {Content.Oddities.Count})");
            Console.WriteLine($"carry tiers {Content.Carry.Length}, grab tools {Content.GrabTools.Length}, dig tools {Content.DigTools.Length}, tech nodes {Content.Techs.Length}");
            Console.WriteLine($"achievements {Content.Achievements.Length}, objectives {Content.Objectives.Length}, wishes {Content.TotalWishes}, relics {Content.TotalRelics}");
            return 0;
        }
        if (args.Length > 0 && args[0] == "crowd") return Crowd(args.Length > 1 ? double.Parse(args[1]) : 0);
        if (args.Length > 0 && args[0] == "factory") return Factory();
        if (args.Length > 0 && args[0] == "crust") return Crust();
        return Smoke();
    }

    /// <summary>Build a hamster wheel, a skimmer, four belts and a hopper; watch it run for three minutes.</summary>
    static int Factory()
    {
        var sim = new Sim(new SaveData(), 3);
        sim.StartRun();
        foreach (var t in new[] { "unlock_hamster", "unlock_skimmer", "unlock_belts", "unlock_hopper" }) sim.DebugSetTech(t, 1);
        sim.DebugAddCash(500);
        BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];
        Console.WriteLine(Can(sim, D("gen_hamster"), 12, 3, 0));
        Console.WriteLine(Can(sim, D("intake_skimmer"), 11, 0, 3));
        for (int x = 12; x <= 15; x++) Console.WriteLine(Can(sim, D("belt"), x, 0, 1));
        Console.WriteLine(Can(sim, D("hopper"), 16, 0, 0));
        Console.WriteLine(Can(sim, D("intake_skimmer"), 11, 0, 1) + " (facing away: should fail)");
        var ham = sim.Place(D("gen_hamster"), 12, 3, 0);
        var sk = sim.Place(D("intake_skimmer"), 11, 0, 3);
        for (int x = 12; x <= 15; x++) sim.Place(D("belt"), x, 0, 1);
        var hop = sim.Place(D("hopper"), 16, 0, 0);
        double cash0 = sim.S.cash;
        for (int step = 0; step < 1800; step++)
        {
            sim.Tick(0.1);
            if (step % 300 == 299)
                Console.WriteLine($"t={sim.Time:0}s power {sim.PowerGen:0.#}/{sim.PowerUse:0.#} kW, skimmer bot ({sk.BotX:0.0},{sk.BotZ:0.0}) state {sk.BotState} load {sk.BotLoad.Count} buf {sk.BufCount} '{sk.Status}', " +
                                  $"belt items {sim.Buildings.FindAll(b => b.Def.IsBelt).ConvertAll(b => b.Items.Count).Sum()}, hopper sold {sim.S.hopperItems} for {Fmt.Money(sim.S.hopperCash)}, loose {sim.Loose.Count}");
        }
        var snap = sim.Snapshot();
        var back = new Sim(snap, 4);
        Console.WriteLine($"save/load: {back.Buildings.Count} buildings (was {sim.Buildings.Count}), belt items {back.Buildings.Sum(b => b.Items.Count)}");
        return 0;
    }

    /// <summary>A dig rig feeding a tumbler, a pigeon sorter and a hopper, in the gunk strata; then prestige.</summary>
    static int Crust()
    {
        var sim = new Sim(new SaveData(), 5);
        sim.StartRun();
        foreach (var t in Content.Techs) if (t.Kind == TechKind.Unlock) sim.DebugSetTech(t.Id, 1);
        sim.DebugSetDepth(0.2);
        Console.WriteLine($"depth {sim.DepthFeet:0.0} ft, stratum {sim.Stratum} '{sim.CurStratum.Name}', dug {sim.S.dug:0} of {sim.TotalScoops:0}");
        BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];
        var placed = new List<Building>
        {
            sim.Place(D("gen_diesel"), 12, 8, 0, true), sim.Place(D("gen_diesel"), 12, 11, 0, true),
            sim.Place(D("dig_rig"), 8, 8, 2, true),
            sim.Place(D("belt"), 8, 9, 0, true), sim.Place(D("belt"), 7, 9, 1, true), sim.Place(D("belt"), 8, 10, 0, true),
            sim.Place(D("proc_tumbler"), 8, 11, 0, true),
            sim.Place(D("belt"), 8, 13, 0, true), sim.Place(D("belt"), 9, 13, 3, true),
            sim.Place(D("proc_pigeons"), 8, 14, 0, true),
            sim.Place(D("belt"), 8, 16, 0, true), sim.Place(D("belt"), 9, 16, 3, true),
            sim.Place(D("hopper"), 8, 17, 0, true),
        };
        Console.WriteLine("placed: " + string.Join(", ", placed.Select(b => b == null ? "FAILED" : b.Def.Id)));
        for (int step = 0; step < 3000; step++)
        {
            sim.Tick(0.1);
            if (step % 600 == 599)
                Console.WriteLine($"t={sim.Time:0}s dug {sim.S.dug:0} ({sim.DepthFeet:0.0} ft, {sim.CurStratum.Name}) washed {sim.S.washed} sorted {sim.S.sorted} relics {sim.S.relicsFound} " +
                                  $"hopper {sim.S.hopperItems} items {Fmt.Money(sim.S.hopperCash)} power {sim.PowerGen}/{sim.PowerUse} | " +
                                  string.Join(" ", sim.Buildings.Where(b => !b.Def.IsBelt && b.Def.Cat != BuildCat.Power).Select(b => $"{b.Def.Id}[{b.BufCount}/{b.OutCount}]{b.Status}")));
        }
        sim.DebugFinishMall();
        Console.WriteLine($"cleared {sim.MallCleared}, treasures {sim.S.treasures.Count}, reward {sim.PrestigeReward} LP");
        sim.Prestige();
        Console.WriteLine($"after prestige: mall {sim.S.mallIndex} {sim.Mall.Name}, LP {sim.S.luckyPennies}, buildings {sim.Buildings.Count}, cash {sim.S.cash}, techs {sim.TechLevelsOwned}, loose {sim.Loose.Count}, dug {sim.S.dug}");
        int ho = Content.TechIndex["ho_card"];
        Console.WriteLine($"buy Company Credit Card: {sim.BuyTech(ho)} → LP {sim.S.luckyPennies}");
        var back = new Sim(sim.Snapshot(), 9);
        Console.WriteLine($"save/load keeps head office: {back.TechLevel("ho_card")} and mall {back.S.mallIndex}");
        return 0;
    }

    static string Can(Sim sim, BuildDef d, int x, int z, int rot) => sim.CanPlace(d, x, z, rot, out var why) ? $"{d.Id} @({x},{z}) r{rot}: ok" : $"{d.Id} @({x},{z}) r{rot}: NO — {why}";

    /// <summary>Watch the crowd for two minutes at a given wishability and print what shoppers are doing.</summary>
    static int Crowd(double wish)
    {
        var sim = new Sim(new SaveData(), 7);
        sim.StartRun();
        if (wish >= 3) sim.DebugSetTech("fountain_scrub", 1);
        if (wish >= 7) sim.DebugSetTech("fountain_jets", 1);
        if (wish >= 12) sim.DebugSetTech("fountain_lights", 1);
        for (int step = 0; step < 1200; step++)
        {
            sim.Tick(0.1);
            if (step % 100 == 99)
            {
                var states = string.Join(" ", sim.Shoppers.Select(s => $"{s.Def.Id}:{s.State}@({s.X:0.0},{s.Z:0.0})→({s.TX:0.0},{s.TZ:0.0})"));
                Console.WriteLine($"t={sim.Time:0}s tosses={sim.S.tosses} wishes={sim.S.wishesSeen} loose={sim.Loose.Count} | {states}");
            }
        }
        return 0;
    }

    /// <summary>Walk, grab one coin, walk back, deposit — for ten minutes of game time.</summary>
    static int Smoke()
    {
        var sim = new Sim(new SaveData(), 1234);
        sim.StartRun();
        double t = 0, walk = 9.0 / Balance.WalkSpeed;
        while (t < 600 && sim.Loose.Count > 0)
        {
            var it = sim.Loose.OrderBy(l => l.X * l.X + (l.Z + 8) * (l.Z + 8)).First();
            sim.Tick(walk + 0.5);
            sim.Pickup(it.Uid);
            sim.Tick(walk + 0.5);
            sim.Deposit();
            t += 2 * walk + 1;
        }
        Console.WriteLine($"smoke: {Fmt.Time(t)} → {Fmt.Money(sim.S.cash)} from {sim.S.deposits} deposits, objective {sim.S.objective}, loose left {sim.Loose.Count}");
        return 0;
    }
}
