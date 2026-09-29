// Balance simulator for the first-person game. Compiles the real Core and plays it with a bot
// (Bot.cs). Usage:
//   dotnet run -c Release --project Tools/BalanceSim -- [maxHours] [seed] [engaged|casual]   play all six malls
//   dotnet run -c Release --project Tools/BalanceSim -- fit [--apply]                         fit each mall's crust size and layers
//   ... -- counts | crowd <wishability> | factory | crust | smoke | slots                     content counts and system checks
// BOT_REPORT=<seconds> sets the report interval (default an hour).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using WishExtractor.Core;

static class Program
{
    /// <summary>Planned engaged-bot hours per mall: 26.5 in all, so every seed stays above Nico's 24 h.</summary>
    static readonly double[] TargetHours = { 3.25, 3.75, 4.25, 4.75, 5.25, 5.25 };

    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "fit") return Fit(args.Contains("--apply"));
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
        if (args.Length > 0 && args[0] == "smoke") return Smoke();
        if (args.Length > 0 && args[0] == "machines") return Machines();
        if (args.Length > 0 && args[0] == "guard") return GuardCheck();
        if (args.Length > 0 && args[0] == "slots") { Console.Write(new Bot(new SaveData(), 1, "engaged").ProbeSlots()); return 0; }
        double hours = args.Length > 0 ? double.Parse(args[0]) : 40;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 1234;
        string profile = args.Length > 2 ? args[2] : "engaged";
        return Run(hours, seed, profile);
    }

    static int Run(double maxHours, int seed, string profile)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var bot = new Bot(new SaveData(), seed, profile);
        if (Environment.GetEnvironmentVariable("BOT_REPORT") is string every) bot.ReportEvery = double.Parse(every);
        var sb = new StringBuilder();
        sb.AppendLine($"Wish Extractor balance run: profile {profile}, seed {seed}, cap {maxHours} h");
        double total = 0;
        for (int m = 0; m < Content.Malls.Length; m++)
        {
            double left = maxHours - total;
            if (left <= 0) break;
            double h = bot.PlayMall(Math.Min(left, 14));
            sb.Append(bot.Log);
            bot.Log.Clear();
            if (h < 0) { sb.AppendLine($"{Content.Malls[m].Name}: NOT CLEARED (depth {bot.Sim.DepthFeet:0.0} ft)"); break; }
            total += h;
            sb.AppendLine($"{Content.Malls[m].Name}: {h:0.00} h   (running total {total:0.00} h)");
            bot.SignAndSpend();
        }
        sb.AppendLine($"TOTAL {total:0.00} h for {bot.MallTimes.Count} malls  (simulated in {sw.Elapsed.TotalSeconds:0} s)");
        var report = sb.ToString();
        Console.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", $"report_{profile}.txt"), report);
        return 0;
    }

    static readonly JsonSerializerOptions JsonOpts = new JsonSerializerOptions { IncludeFields = true };
    static SaveData Clone(SaveData s) => JsonSerializer.Deserialize<SaveData>(JsonSerializer.Serialize(s, JsonOpts), JsonOpts);

    /// <summary>
    /// For each mall in turn, starting from the state the bot reached at the end of the previous mall:
    /// bisect the crust size until the engaged bot clears it in the target hours, then move the layer
    /// boundaries so it spends equal time in every layer below the loose one, and repeat (the layers
    /// change the income, so the size is fitted again). apply = rewrite the fitted blocks in ContentMalls.cs.
    /// </summary>
    static int Fit(bool apply)
    {
        int n = Content.Malls.Length;
        var fitted = new double[n];
        var fracs = new double[n][];
        var bot = new Bot(new SaveData(), 1234, "engaged");
        var start = Clone(bot.Sim.Snapshot());
        for (int m = 0; m < n; m++)
        {
            var mall = Content.Malls[m];
            double target = TargetHours[m];
            mall.BoundFracs = null;
            double lo = Math.Log(3000), hi = Math.Log(1e8), best = 0, hours = -1;
            Bot run = null;
            for (int pass = 0; pass < 3; pass++)
            {
                best = FitScoops(mall, target, start, lo, hi);
                lo = Math.Log(best / 3); hi = Math.Log(best * 3);
                Content.SetCrust(mall, best);
                run = new Bot(Clone(start), 1234, "engaged") { Curve = new List<(double, double)>() };
                hours = run.PlayMall(target * 3);
                Console.WriteLine($"  {mall.Id} pass {pass + 1}: {best:0} scoops → {hours:0.00} h");
                if (hours < 0 || pass == 2) break;
                mall.BoundFracs = EqualTimeFracs(run.Curve, mall.CrustScoops, mall.Strata.Length);
            }
            fitted[m] = best;
            fracs[m] = mall.BoundFracs;
            Console.WriteLine($"{mall.Name}: {best:0} scoops → {hours:0.00} h (target {target}); layers start at " +
                              string.Join(", ", mall.Bounds.Skip(1).Take(mall.Strata.Length - 1).Select(v => v.ToString("0"))));
            if (hours < 0) { Console.WriteLine("  did not clear; stopping"); break; }
            run.SignAndSpend();
            start = Clone(run.Sim.Snapshot());
        }
        Console.WriteLine("fitted: " + string.Join(", ", fitted.Select(v => v.ToString("0"))));
        if (apply)
        {
            string path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "Assets", "Scripts", "Core", "ContentMalls.cs"));
            var text = File.ReadAllText(path);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            text = ReplaceBlock(text, "fitted-scoops", new[] { string.Join(", ", fitted.Select(v => v.ToString("0", inv))) + "," });
            text = ReplaceBlock(text, "fitted-bounds", fracs.Select(f => f == null ? "null," : "new[] { " + string.Join(", ", f.Select(v => v.ToString("G6", inv))) + " },"));
            File.WriteAllText(path, text);
            Console.WriteLine("wrote " + path);
        }
        return 0;
    }

    /// <summary>Bisect (in log space) the crust size that the engaged bot clears in the target hours.</summary>
    static double FitScoops(MallDef mall, double target, SaveData start, double lo, double hi)
    {
        double Trial(double logScoops, string note)
        {
            Content.SetCrust(mall, Math.Exp(logScoops));
            double h = new Bot(Clone(start), 1234, "engaged").PlayMall(target * 2.2);
            Console.WriteLine($"  {mall.Id}: {Math.Exp(logScoops):0} scoops → {(h < 0 ? "timeout" : h.ToString("0.00") + " h")}{note}");
            return h < 0 ? double.MaxValue : h;
        }
        // widen the bracket until it straddles the target
        for (int k = 0; k < 8 && Trial(hi, " (bracket top)") <= target; k++) { lo = hi; hi += Math.Log(30); }
        for (int k = 0; k < 8 && lo > Math.Log(3000) && Trial(lo, " (bracket bottom)") > target; k++) { hi = lo; lo = Math.Max(Math.Log(3000), lo - Math.Log(30)); }
        double best = Math.Exp((lo + hi) / 2);
        for (int iter = 0; iter < 16; iter++)
        {
            double mid = (lo + hi) / 2;
            double h = Trial(mid, "");
            if (h > target) hi = mid; else lo = mid;
            best = Math.Exp((lo + hi) / 2);
            if (Math.Abs(h - target) / target < 0.015) { best = Math.Exp(mid); break; }
        }
        return Math.Round(best / 100) * 100;
    }

    /// <summary>Tops of layers 2.. (as fractions of the crust) that split the time after the loose layer evenly.</summary>
    static double[] EqualTimeFracs(List<(double t, double dug)> curve, double total, int strata)
    {
        double loose = Balance.LooseLayerScoops, tEnd = curve[curve.Count - 1].t, tLoose = tEnd;
        foreach (var c in curve) if (c.dug >= loose) { tLoose = c.t; break; }
        var f = new double[strata - 2];
        double prev = loose;
        for (int i = 2; i < strata; i++)
        {
            double t = tLoose + (i - 1) * (tEnd - tLoose) / (strata - 1);
            double dug = curve[curve.Count - 1].dug;
            for (int j = 1; j < curve.Count; j++)
                if (curve[j].t >= t)
                {
                    var (ta, da) = curve[j - 1];
                    var (tb, db) = curve[j];
                    dug = da + (db - da) * (t - ta) / Math.Max(1e-9, tb - ta);
                    break;
                }
            dug = Math.Max(dug, prev * 1.05);
            f[i - 2] = dug / total;
            prev = dug;
        }
        return f;
    }

    /// <summary>Replace the lines between "// &lt;tag&gt;" and "// &lt;/tag&gt;" (keeping the closing line's indent).</summary>
    static string ReplaceBlock(string text, string tag, IEnumerable<string> lines)
    {
        int a = text.IndexOf("// <" + tag + ">"), b = text.IndexOf("// </" + tag + ">");
        if (a < 0 || b < 0) throw new InvalidOperationException("missing // <" + tag + "> block");
        int lineEnd = text.IndexOf('\n', a) + 1;
        int indentStart = text.LastIndexOf('\n', b) + 1;
        string indent = text.Substring(indentStart, b - indentStart);
        string body = string.Concat(lines.Select(l => indent + l + "\n"));
        return text.Substring(0, lineEnd) + body + text.Substring(indentStart);
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

    /// <summary>
    /// The four mall-only machines, each in its own mall: only sold there, and each does its job
    /// (cannon slabs collected by a pump line, drones emptying a lineless borer into a carousel line,
    /// slot spins and a jackpot, the Old Well granting wishes by dissolving crust).
    /// </summary>
    static int Machines()
    {
        BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];
        bool ok = true;
        void Check(bool cond, string what) { ok &= cond; Console.WriteLine($"{(cond ? "PASS" : "FAIL")}  {what}"); }
        Sim Mall(int m, int seed)
        {
            var s = new Sim(new SaveData(), seed);
            s.DebugJumpToMall(m);
            foreach (var t in Content.Techs) if (t.Kind == TechKind.Unlock && s.TechInThisMall(t)) s.DebugSetTech(t.Id, 1);
            s.DebugAddCash(1e12);
            s.DebugSetDepth(0.3);
            for (int i = 0; i < 6; i++) s.Place(D("gen_solar"), 20 + (i % 3) * 3, -27 + (i / 3) * 3, 0, true);
            return s;
        }
        // mall-only techs are sold in their own mall only
        var probe = new Sim(new SaveData(), 1);
        probe.DebugJumpToMall(3);
        Check(!probe.TechInThisMall(Content.Techs[Content.TechIndex["unlock_cannon"]]) && probe.TechInThisMall(Content.Techs[Content.TechIndex["unlock_carousel"]]),
              "in Skyport the carousel is for sale and the champagne cannon isn't");
        probe.DebugJumpToMall(8);   // Aurelia, first remodel
        Check(probe.TechInThisMall(Content.Techs[Content.TechIndex["unlock_cannon"]]), "the cannon comes back in Aurelia's remodel");

        // Galleria Aurelia: a cannon behind the rim lobbing slabs over it, a pump line at the rim feeding a sorter
        var a = Mall(2, 11);
        var cannon = a.Place(D("dig_cannon"), 17, -1, 3, true);
        Check(a.Place(D("dig_cannon"), 24, 5, 3, true) == null, "a cannon too far from the fountain can't be built");
        var pumpN = a.Place(D("intake_pump"), 11, -3, 3, true);
        a.Place(D("proc_sorter"), 12, -3, 1, true);
        a.Place(D("hopper2"), 14, -3, 1, true);
        Check(cannon != null && pumpN != null, $"cannon and pump placed ({cannon?.Rot}, {pumpN?.Rot})");
        double dug0 = a.S.dug;
        for (int i = 0; i < 1200; i++) a.Tick(0.1);
        Check(a.S.cannonBlasts > 3 && a.S.dug > dug0, $"the cannon fires ({a.S.cannonBlasts} pops, {a.S.dug - dug0:0} scoops)");
        Check(a.Loose.Exists(l => Content.Items[l.Type].Id == "rinsed") || a.S.hopperItems > 0, "its slabs land rinsed in the water");
        Check(a.S.machinePicked > 0 && a.S.sorted > 0 && a.S.hopperCash > 0, $"the pump line sorts and sells them ({a.S.sorted} sorted, {Fmt.Money(a.S.hopperCash)})");
        Console.WriteLine($"  cannon status: '{cannon?.Status}', loose {a.Loose.Count}");

        // Skyport: a borer at the rim with no line; a carousel feeding a tumbler, a sorter and a hopper
        var s3 = Mall(3, 12);
        var borer = s3.Place(D("dig_borer"), -13, 0, 1, true);
        var car = s3.Place(D("carousel"), -24, 10, 0, true);
        s3.Place(D("proc_tumbler"), -24, 13, 0, true);
        s3.Place(D("proc_sorter"), -24, 15, 0, true);
        s3.Place(D("hopper2"), -24, 17, 0, true);
        Check(borer != null && car != null, "a lineless borer and a carousel line placed");
        for (int i = 0; i < 1200; i++) s3.Tick(0.1);
        Check(s3.S.droneTrips > 0 && s3.S.hopperItems > 0, $"drones fly the borer's chunks to the carousel line ({s3.S.droneTrips} trips, {s3.S.hopperItems} sold, borer '{borer?.Status}')");

        // the Lucky Lagoon: rig → slot machine → hopper
        var s4 = Mall(4, 13);
        s4.Place(D("dig_rig"), 11, -1, 3, true);
        var slots = s4.Place(D("proc_slots"), 12, 0, 1, true);
        s4.Place(D("hopper2"), 14, 0, 1, true);
        Check(slots != null, "a slot-machine line placed");
        for (int i = 0; i < 1200; i++) s4.Tick(0.1);
        Check(s4.S.slotSpins > 0 && s4.S.hopperCash > 0, $"the slots spin raw gunk into cash ({s4.S.slotSpins} spins, {s4.S.jackpots} jackpots, {Fmt.Money(s4.S.hopperCash)})");

        // Eternity Plaza: the Old Well grants wishes nobody catches
        var s5 = Mall(5, 14);
        var well = s5.Place(D("wishing_well"), -12, 1, 1, true);
        Check(well != null && s5.Place(D("wishing_well"), 11, -1, 3, true) == null, "one Old Well, and only one");
        double dug5 = s5.S.dug;
        var w = s5.DebugSpawnWish(2, 2);
        for (int i = 0; i < 40; i++) s5.Tick(0.1);
        Check(w != null && s5.S.wellWishes >= 1 && s5.S.dug > dug5, $"an uncaught wish falls in and crust vanishes ({s5.S.wellWishes} granted, {s5.S.dug - dug5:0} scoops)");
        Console.WriteLine(ok ? "all machines OK" : "SOME MACHINES FAILED");
        return ok ? 0 : 1;
    }

    /// <summary>Officer Doug's statue check in the Core: move while he looks and you're fined; freeze, or hide
    /// behind the centrepiece, and a shopper tips the statue; a sworn-in deputy never gets looked at.</summary>
    static int GuardCheck()
    {
        bool ok = true;
        void Check(bool cond, string what) { ok &= cond; Console.WriteLine($"{(cond ? "PASS" : "FAIL")}  {what}"); }
        var s = new Sim(new SaveData(), 5);
        s.StartRun();
        s.DebugAddCash(100);
        int tips = 0;
        s.OnStatueTipped += (x, z, v) => tips++;
        // one look with the player wading where `at` puts them (once Doug has stopped); true if busted
        bool Look(Func<Guard, (float x, float z)> at, bool moving)
        {
            for (int i = 0; i < 400 && s.Guard.State != GuardState.Patrol; i++) { s.PlayerInWater = false; s.Tick(0.1); }
            s.PlayerX = 3; s.PlayerZ = 3; s.PlayerInWater = true; s.PlayerMoving = false;
            s.DebugGuardCheck();
            double fines = s.S.finesPaid;
            for (int i = 0; i < 120 && (s.Guard.State == GuardState.Patrol || s.Guard.Watching); i++)
            {
                if (s.Guard.Watching) { (s.PlayerX, s.PlayerZ) = at(s.Guard); s.PlayerMoving = moving && s.Guard.State == GuardState.Looking; }
                s.Tick(0.1);
            }
            s.PlayerMoving = false;
            return s.S.finesPaid > fines;
        }
        (float, float) Near(Guard g) => (3, 3);
        // two metres from the centre, straight across from Doug: the centrepiece is in the way
        (float, float) Hidden(Guard g) { float l = (float)Math.Sqrt(g.X * g.X + g.Z * g.Z); return (-g.X / l * 2f, -g.Z / l * 2f); }
        double cash0 = s.S.cash;
        Check(Look(Near, true) && s.S.cash < cash0, $"moving while Doug looks: busted and fined ({Fmt.Money(cash0 - s.S.cash)})");
        double fooled0 = s.S.statuesFooled;
        Check(!Look(Near, false) && s.S.statuesFooled == fooled0 + 1 && tips == 1, "freezing until he looks away: a shopper tips the statue");
        Check(!Look(Hidden, true) && s.S.statuesFooled == fooled0 + 2 && tips == 2, "moving behind the centrepiece: he never sees you");
        double looks = s.S.guardLooks;
        s.DebugSetTech("sec_badge", 1);
        Check(!Look(Near, true) && s.S.guardLooks == looks, "a sworn-in deputy gets a nod, not a look");
        Console.WriteLine(ok ? "guard OK" : "GUARD FAILED");
        return ok ? 0 : 1;
    }

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
