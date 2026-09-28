// Balance simulator for the first-person game. Compiles the real Core and plays it with a bot.
// Milestone 1 stub: content counts and a short hand-carry smoke run. The full throughput bot
// (walking times, tech purchases, factory) arrives with the balance pass.
using System;
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
        return Smoke();
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
