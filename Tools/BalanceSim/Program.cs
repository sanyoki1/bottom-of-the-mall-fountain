// Balance simulator: a bot plays the real Sim and we log when things happen.
// The bot is an efficient, engaged player: it clicks, catches most wishes and buys greedily.
// Real players are slower, so "bot hours" is a lower bound on real play time.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using WishExtractor.Core;

static class Program
{
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "calibrate") return Calibrate(args);
        if (args.Length > 0 && args[0] == "fit") return Fit(args);
        if (args.Length > 0 && args[0] == "counts")
        {
            int machineUps = Content.Upgrades.Count(u => u.Kind == UpgradeKind.MachineMult);
            int mallOnly = Content.Upgrades.Count(u => u.MallOnly);
            Console.WriteLine($"malls {Content.Malls.Length}, strata {Content.Malls.Sum(m => m.Strata.Length)}, loot kinds {Content.Malls.Sum(m => m.Items.Length)}");
            Console.WriteLine($"machines {Content.Machines.Length} (tiered {Content.Machines.Count(m => !m.IsMega && !m.IsCompressor)}, megaprojects {Content.Machines.Count(m => m.IsMega)}), tools {Content.Tools.Length}");
            Console.WriteLine($"upgrades {Content.Upgrades.Length} (per-machine {machineUps}, mall-exclusive {mallOnly}, general {Content.Upgrades.Length - machineUps - mallOnly})");
            Console.WriteLine($"head office perks {Content.HeadOffice.Length} (levels {Content.HeadOffice.Sum(h => h.MaxLevel)}), achievements {Content.Achievements.Length}, objectives {Content.Objectives.Length}");
            Console.WriteLine($"wishes {Content.TotalWishes}, relics {Content.TotalRelics}, bottom treasures {Content.Malls.Length}");
            return 0;
        }
        double hours = args.Length > 0 ? double.Parse(args[0]) : 40;
        int seed = args.Length > 1 ? int.Parse(args[1]) : 1234;
        string profile = args.Length > 2 ? args[2] : "engaged";
        var bot = new Bot(seed, profile);
        var report = bot.Run(hours * 3600);
        Console.WriteLine(report);
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", $"report_{profile}.txt"), report);
        return 0;
    }

    /// <summary>
    /// calibrate [firstMall] [targets in minutes...]: for each mall from firstMall on, bisect its
    /// TotalItems (in log space) until the bot clears it in the target time, keeping earlier malls fixed.
    /// </summary>
    static int Calibrate(string[] args)
    {
        int first = args.Length > 1 ? int.Parse(args[1]) : 1;
        var targets = new List<double>();
        for (int i = 2; i < args.Length; i++) targets.Add(double.Parse(args[i]) * 60);
        for (int m = first; m < first + targets.Count && m < Content.Malls.Length; m++)
        {
            double target = targets[m - first];
            double lo = Math.Log10(Balance.LooseLayerItems * 20), hi = 45;
            double bestT = 0, bestTime = 0;
            for (int iter = 0; iter < 16; iter++)
            {
                double mid = 0.5 * (lo + hi);
                SetTotal(m, Math.Pow(10, mid));
                var bot = new Bot(1234, "engaged");
                double dur = bot.TimeToClear(m, target * 3);
                if (dur < target) lo = mid; else hi = mid;
                bestT = Math.Pow(10, mid);
                bestTime = dur;
                Console.WriteLine($"  mall {m + 1} T=1e{mid:0.00} -> {Fmt.Time(dur)}");
            }
            SetTotal(m, bestT);
            Console.WriteLine($"MALL {m + 1} {Content.Malls[m].Name}: TotalItems = {bestT:0.###e0}  ({Fmt.Time(bestTime)})");
        }
        return 0;
    }

    static void SetTotal(int m, double total)
    {
        var mall = Content.Malls[m];
        mall.Bounds = Content.DefaultBounds(total, Balance.LooseLayerItems, mall.Strata.Length);
        mall.TotalItems = total;
    }

    /// <summary>
    /// fit [minutes per mall...]: plays through every mall once and sets each stratum boundary to
    /// wherever the bot's dig counter is at that stratum's planned time. Prints C# to paste into
    /// Content.FittedBounds. While a layer is being fitted, every deeper boundary is pushed out of reach,
    /// so the bot's behaviour inside a layer never depends on where the next one starts.
    /// </summary>
    static int Fit(string[] args)
    {
        var totals = new List<double>();
        bool apply = false;
        for (int i = 1; i < args.Length; i++)
        {
            if (args[i] == "--apply") { apply = true; continue; }
            totals.Add(double.Parse(args[i]) * 60);
        }
        if (totals.Count == 0) totals.AddRange(new double[] { 120 * 60, 180 * 60, 210 * 60, 270 * 60, 300 * 60, 360 * 60 });
        // Remodel lap 1 (after the finale): every mall gets the same, shorter target.
        int nMalls = Content.Malls.Length;
        while (totals.Count < nMalls * 2) totals.Add(90 * 60);
        // cumulative share of the mall (after the loose layer) at which each deeper stratum starts, then the bottom
        double[] share = { 0.07, 0.17, 0.29, 0.43, 0.58, 0.77, 1.0 };
        var save = new SaveData();
        var code = new StringBuilder();
        var remodelCode = new StringBuilder();
        var bot = new Bot(1234, "engaged", save);
        bot.StartRunIfFresh();
        for (int m = 0; m < totals.Count; m++)
        {
            var mall = Content.Malls[m % nMalls];
            bool remodel = m >= nMalls;
            void SetB(double[] b)
            {
                if (remodel) mall.RemodelBounds = b;
                else { mall.Bounds = b; mall.TotalItems = b[b.Length - 1]; }
            }
            int n = mall.Strata.Length;
            double loose = m == 0 ? 180 : 120;
            var bounds = new double[n + 1];
            if (remodel) bounds[1] = mall.Bounds[1];   // remodels keep the base loose layer; fit the rest
            double elapsed = 0;
            for (int s = remodel ? 2 : 1; s <= n; s++)
            {
                if (remodel && s == 2)
                {
                    // play through the unchanged loose layer first
                    var pre = new double[n + 1];
                    pre[1] = bounds[1];
                    for (int j = 2; j <= n; j++) pre[j] = 1e280 * Math.Pow(10, j);
                    SetB(pre);
                    bot.Reload();
                    double t0 = bot.Elapsed;
                    bot.RunUntilDug(bounds[1], 3600);
                    elapsed = bot.Elapsed - t0;
                    bot.EnterStratum(1, bounds[1]);
                }
                // push boundaries s..n out of reach
                var tmp = new double[n + 1];
                for (int j = 0; j < s; j++) tmp[j] = bounds[j];
                for (int j = s; j <= n; j++) tmp[j] = 1e280 * Math.Pow(10, j);
                SetB(tmp);
                bot.Reload();
                double targetT = s == 1 ? loose : loose + share[s - 2] * (totals[m] - loose);
                double run = Math.Max(30, targetT - elapsed);
                double dug = bot.RunFor(run);
                elapsed = Math.Max(targetT, elapsed + 30);
                bounds[s] = Math.Max(dug, bounds[s - 1] * 1.05 + 1);
                // snap exactly onto the boundary so the next layer starts clean
                var snap = (double[])tmp.Clone();
                snap[s] = bounds[s];
                SetB(snap);
                bot.EnterStratum(s, bounds[s]);
            }
            SetB(bounds);
            bot.Reload();
            string label = (remodel ? "Remodel " : "") + mall.Name;
            Console.WriteLine($"{label}: " + string.Join(", ", bounds.Select(b => b.ToString("0.###e0"))));
            (remodel ? remodelCode : code).AppendLine($"            new double[] {{ {string.Join(", ", bounds.Select(b => b == 0 ? "0" : b.ToString("0.####e0")))} }},  // {mall.Name}");
            bot.FinishMallAndPrestige();
        }
        Console.WriteLine();
        Console.WriteLine(code.ToString());
        Console.WriteLine(remodelCode.ToString());
        string root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        File.WriteAllText(Path.Combine(root, "Tools", "BalanceSim", "fitted_bounds.txt"), code + "\n" + remodelCode);
        if (apply)
        {
            string path = Path.Combine(root, "Assets", "Scripts", "Core", "ContentMalls.cs");
            string src = File.ReadAllText(path);
            src = ReplaceBlock(src, "fitted-bounds", code.ToString());
            src = ReplaceBlock(src, "fitted-remodel-bounds", remodelCode.ToString());
            File.WriteAllText(path, src);
            Console.WriteLine("Applied to " + path);
        }
        return 0;
    }

    static string ReplaceBlock(string src, string tag, string body)
    {
        string open = $"// <{tag}>", close = $"// </{tag}>";
        int a = src.IndexOf(open, StringComparison.Ordinal), b = src.IndexOf(close, StringComparison.Ordinal);
        int lineEnd = src.IndexOf('\n', a) + 1;
        int closeLineStart = src.LastIndexOf('\n', b) + 1;
        return src.Substring(0, lineEnd) + body + src.Substring(closeLineStart);
    }
}

sealed class Bot
{
    readonly Sim sim;
    readonly Random rng;
    readonly string profile;
    readonly StringBuilder log = new StringBuilder();
    readonly HashSet<string> firsts = new HashSet<string>();
    readonly HashSet<int> decidedWish = new HashSet<int>(), decidedGolden = new HashSet<int>();
    double t, clickAcc, buyTimer, sellTimer, clearedAt = -1, mallStart, lastPurchase, maxGap;
    string maxGapWhere = "";
    int purchasesThisMall;
    readonly List<string> mallSummary = new List<string>();
    readonly StringBuilder strataLine = new StringBuilder();

    static readonly double[] Targets = { 120, 180, 210, 270, 300, 360 };
    static double MallTarget(int m) => Targets[Math.Min(m, Targets.Length - 1)] * 60;

    double CatchRate => profile == "idle" ? 0.15 : 0.55;
    double GoldenRate => profile == "idle" ? 0.2 : 0.7;

    public Bot(int seed, string profile) : this(seed, profile, null) { sim.StartRun(); }

    public void StartRunIfFresh()
    {
        if (sim.S.mallIndex == 0 && sim.S.dug == 0 && sim.S.lifetimeCash == 0) sim.StartRun();
    }

    /// <summary>Re-read content (e.g. new stratum bounds) while keeping the save state.</summary>
    public void Reload() => sim.Load(sim.Snapshot());

    public double RunFor(double seconds)
    {
        double end = t + seconds;
        while (t < end) Step(0.25);
        return sim.S.dug;
    }

    public double Elapsed => t;

    public void RunUntilDug(double target, double cap)
    {
        double end = t + cap;
        while (sim.S.dug < target && t < end) Step(0.25);
    }

    public void EnterStratum(int s, double dug)
    {
        sim.S.dug = dug;
        if (sim.S.maxStratum < s) sim.S.maxStratum = s;
        Reload();
    }

    public void FinishMallAndPrestige()
    {
        int m = sim.S.mallIndex;
        sim.DebugFinishMall();
        double end = t + 400;
        while (sim.S.mallIndex == m && t < end) Step(0.25);
        if (sim.S.mallIndex == m) { sim.Prestige(); SpendLP(); mallStart = t; }
    }

    public Bot(int seed, string profile, SaveData save)
    {
        this.profile = profile;
        rng = new Random(seed);
        sim = new Sim(save ?? new SaveData(), seed);
        sim.OnStratumReached += s =>
        {
            strataLine.Append($" s{s}:{(t - mallStart) / 60,5:0}m");
            Log($"  reached stratum {s} '{sim.Mall.Strata[s].Name}'  depth {sim.DepthFeet:0.0}ft  cash {Fmt.Money(sim.S.cash)}  dig {Fmt.Num(sim.StageRateBase[0])}/s wash {Fmt.Num(sim.StageRateBase[1])}/s sort {Fmt.Num(sim.StageRateBase[2])}/s  value x{Fmt.Num(sim.ValueMult)}");
        };
        sim.OnMallCleared += () => { clearedAt = t; Log($"  *** CLEARED {sim.Mall.Name} in {Fmt.Time(t - mallStart)} (total {Fmt.Time(t)})"); };
        sim.OnPurchase += id => { OnBuy(id); };
        sim.OnRelicSetComplete += m => Log($"  relic set complete: {m.Name}");
    }

    double Cps()
    {
        if (profile == "idle") return sim.S.runTime < 600 ? 3 : 0.2;
        double rt = sim.S.runTime;
        if (rt < 600) return 3;
        if (rt < 2400) return 1.5;
        return 0.8;
    }

    void Log(string s) => log.AppendLine($"[{Fmt.Time(t),9}] {s}");

    readonly int[] buckets = new int[10];
    double mallGap;

    void OnBuy(string id)
    {
        double gap = t - lastPurchase;
        if (t - mallStart > 60 && gap > mallGap) mallGap = gap;
        int bucket = (int)Math.Min(9, (t - mallStart) / MallTarget(sim.S.mallIndex) * 10);
        buckets[bucket]++;
        if (gap > maxGap && t - mallStart > 60) { maxGap = gap; maxGapWhere = $"before {id} in {sim.Mall.Name} at {Fmt.Time(t)}"; }
        lastPurchase = t;
        purchasesThisMall++;
        string key = sim.S.mallIndex + ":" + id;
        bool isMachine = Content.MachineIndex.TryGetValue(id, out int mi);
        bool interesting = sim.S.mallIndex == 0 ? isMachine || Content.ToolIndex.ContainsKey(id)
                                                : isMachine && Content.Machines[mi].Tier >= 4;
        if (firsts.Add(key) && interesting)
            Log($"  first {Content.NameOf(id)}  (cash {Fmt.Money(sim.S.cash)})");
    }

    /// <summary>Play until mall index m is cleared; return how long that mall took (or cap if it never cleared).</summary>
    public double TimeToClear(int m, double cap)
    {
        double start = -1;
        while (true)
        {
            if (sim.S.mallIndex == m && start < 0) start = t;
            if (sim.S.mallIndex > m || sim.MallCleared && sim.S.mallIndex == m) return t - start;
            if (start >= 0 && t - start > cap) return cap;
            if (t > 400 * 3600) return cap;
            Step(0.25);
        }
    }

    void Step(double dt)
    {
        if (!sim.MallCleared)
        {
            clickAcc += Cps() * dt;
            while (clickAcc >= 1) { sim.Click(); clickAcc -= 1; }
        }
        sim.Tick(dt);
        t += dt;
        foreach (var w in sim.Wishes.ToArray())
        {
            if (w.Age < 1.5 || decidedWish.Contains(w.Uid)) continue;
            decidedWish.Add(w.Uid);
            if (rng.NextDouble() < CatchRate) sim.CatchWish(w.Uid);
        }
        foreach (var g in sim.Goldens.ToArray())
        {
            if (g.Age < 2 || decidedGolden.Contains(g.Uid)) continue;
            decidedGolden.Add(g.Uid);
            if (rng.NextDouble() < GoldenRate) sim.ClaimGolden(g.Uid);
        }
        if (sim.Rat != null && sim.Rat.Age > 3 && rng.NextDouble() < 0.02 * GoldenRate) sim.CatchRat();
        sellTimer += dt;
        if (!sim.AutoSell && sellTimer > 4) { sellTimer = 0; if (sim.S.pocketValue > 0) sim.SellPocket(); }
        buyTimer += dt;
        if (buyTimer >= 1) { buyTimer = 0; Shop(); }
        if (sim.MallCleared)
        {
            if (clearedAt < 0 || clearedAt < mallStart) clearedAt = t;
            bool drained = sim.S.hopperCount < 1 && sim.S.trayCount < 1;
            if (drained || t - clearedAt > 180)
            {
                sim.Prestige();
                SpendLP();
                mallStart = t;
                clearedAt = -1;
            }
        }
    }

    public string Run(double seconds)
    {
        const double dt = 0.25;
        Log($"START {sim.Mall.Name}");
        mallStart = 0;
        while (t < seconds)
        {
            // clicks
            if (!sim.MallCleared)
            {
                clickAcc += Cps() * dt;
                while (clickAcc >= 1) { sim.Click(); clickAcc -= 1; }
            }
            sim.Tick(dt);
            t += dt;

            // wishes, goldens, rats
            foreach (var w in sim.Wishes.ToArray())
            {
                if (w.Age < 1.5 || decidedWish.Contains(w.Uid)) continue;
                decidedWish.Add(w.Uid);
                if (rng.NextDouble() < CatchRate) sim.CatchWish(w.Uid);
            }
            foreach (var g in sim.Goldens.ToArray())
            {
                if (g.Age < 2 || decidedGolden.Contains(g.Uid)) continue;
                decidedGolden.Add(g.Uid);
                if (rng.NextDouble() < GoldenRate) sim.ClaimGolden(g.Uid);
            }
            if (sim.Rat != null && sim.Rat.Age > 3 && rng.NextDouble() < 0.02 * GoldenRate) sim.CatchRat();

            // selling
            sellTimer += dt;
            if (!sim.AutoSell && sellTimer > 4) { sellTimer = 0; if (sim.S.pocketValue > 0) sim.SellPocket(); }

            // purchases
            buyTimer += dt;
            if (buyTimer >= 1) { buyTimer = 0; Shop(); }

            // prestige
            if (sim.MallCleared)
            {
                bool drained = sim.S.hopperCount < 1 && sim.S.trayCount < 1;
                if (drained || t - clearedAt > 180)
                {
                    mallSummary.Add($"{sim.Mall.Name,-26} {Fmt.Time(t - mallStart),10}  (cum {Fmt.Time(t),10})  purchases {purchasesThisMall,5}  wishes {sim.WishesFoundInMall(sim.MallDefIndex)}/{sim.Mall.Wishes.Length}  relics {sim.RelicsFoundInMall(sim.MallDefIndex)}/{sim.Mall.Relics.Length}  LP+{sim.PrestigeReward}");
                    mallSummary.Add("      strata:" + strataLine + $" clear:{(clearedAt - mallStart) / 60,5:0}m");
                    mallSummary.Add($"      purchases per tenth of target: {string.Join(" ", buckets.Select(b => b.ToString().PadLeft(4)))}   longest gap {Fmt.Time(mallGap)}");
                    strataLine.Clear();
                    Array.Clear(buckets, 0, buckets.Length);
                    mallGap = 0;
                    sim.Prestige();
                    SpendLP();
                    mallStart = t;
                    lastPurchase = t;
                    purchasesThisMall = 0;
                    Log($"START {sim.Mall.Name}  (remodel {sim.Remodel})  LP left {sim.S.luckyPennies}  HO levels {sim.HOTotalLevels}");
                    if (sim.S.mallIndex >= 15) break;
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine($"=== Balance report ({profile}) — simulated {Fmt.Time(t)} ===");
        sb.AppendLine("Mall clear times:");
        foreach (var s in mallSummary) sb.AppendLine("  " + s);
        if (!sim.MallCleared)
            sb.AppendLine($"  (in progress) {sim.Mall.Name} {sim.DepthFeet:0.0}/{sim.Mall.DepthFeet} ft after {Fmt.Time(t - mallStart)}");
        sb.AppendLine($"Longest gap between purchases: {Fmt.Time(maxGap)} ({maxGapWhere})");
        sb.AppendLine($"Achievements {sim.AchievementCount}/{Content.Achievements.Length}  wishes {sim.UniqueWishCount}/{Content.TotalWishes}  relic sets {sim.SetsComplete}");
        sb.AppendLine($"Lifetime cash {Fmt.Money(sim.S.lifetimeCash)}  clicks {Fmt.Int(sim.S.clicks)}  wishes caught {sim.S.wishesCaught}  compressed {sim.S.wishesCompressed}  relics {sim.S.relicsFound}");
        sb.AppendLine();
        sb.Append(log);
        return sb.ToString();
    }

    void SpendLP()
    {
        // cheapest-first across a sensible priority set
        for (int guard = 0; guard < 500; guard++)
        {
            int best = -1;
            double bestCost = double.MaxValue;
            for (int i = 0; i < Content.HeadOffice.Length; i++)
            {
                if (sim.HOMaxed(i)) continue;
                var h = Content.HeadOffice[i];
                if (h.Kind == HOKind.OfflineHours || h.Kind == HOKind.OfflineEff) continue;
                double c = sim.HOCost(i);
                double weight = h.Kind == HOKind.RateMult || h.Kind == HOKind.ValueMult ? 0.8 : 1.0;
                if (c * weight < bestCost && c <= sim.S.luckyPennies) { bestCost = c * weight; best = i; }
            }
            if (best < 0) break;
            sim.BuyHO(best);
        }
    }

    void Shop()
    {
        for (int guard = 0; guard < 400; guard++)
        {
            if (!BuyOne()) break;
        }
    }

    bool BuyOne()
    {
        double cash = sim.S.cash;

        // Tools: cheap relative to their impact early, so buy on sight.
        var nt = sim.NextTool;
        if (nt != null && sim.ToolCost(nt) <= cash) return sim.BuyNextTool();

        // Upgrades: cheapest affordable first.
        int bu = -1;
        double bc = double.MaxValue;
        for (int i = 0; i < Content.Upgrades.Length; i++)
        {
            if (!sim.UpgradeAvailable(i)) continue;
            double c = sim.UpgradeCost(i);
            if (c < bc) { bc = c; bu = i; }
        }
        if (bu >= 0 && bc <= cash) return sim.BuyUpgrade(bu);

        // Megaprojects and compressor.
        for (int i = 0; i < Content.Machines.Length; i++)
        {
            var m = Content.Machines[i];
            if (!(m.IsMega || m.IsCompressor) || !sim.MachineUnlocked(i)) continue;
            double c = sim.MachineCost(i, 1);
            double share = m.IsMega ? 0.6 : 0.15;
            if (c <= cash * share) return sim.BuyMachine(i, 1);
        }

        // Machines: feed the bottleneck.
        bool loose = sim.CurStratum.Loose;
        double click = Cps() * sim.ClickPowerNow * sim.ComboMult;
        double D = sim.StageRateBase[0] + click, W = sim.StageRateBase[1], S = sim.StageRateBase[2];
        Stage need;
        if (loose) need = Stage.Dig;
        else
        {
            // keep the hopper from growing forever but always dig a bit faster than we wash
            double backlogFactor = sim.S.hopperCount > W * 120 ? 2.0 : 1.0;
            double dScore = D / (1.25 * backlogFactor), wScore = W, sScore = S / 1.05;
            if (sim.S.trayCount > S * 120) sScore *= 0.5;
            need = dScore <= wScore && dScore <= sScore ? Stage.Dig : wScore <= sScore ? Stage.Wash : Stage.Sort;
        }

        int best = -1, cheap = -1;
        double bestRatio = 0, cheapRatio = 0;
        for (int i = 0; i < Content.Machines.Length; i++)
        {
            var m = Content.Machines[i];
            if (m.IsMega || m.IsCompressor || m.Stage != need || !sim.MachineUnlocked(i)) continue;
            double c = sim.MachineCost(i, 1);
            // include the milestone jump if the next unit crosses one
            int cnt = sim.MachineCount(i);
            double gain = sim.MachineUnitRate[i] > 0 ? sim.MachineUnitRate[i] : m.BaseRate * sim.AllRateMult;
            int ms = Sim.NextMilestone(cnt);
            if (ms > 0 && ms - cnt <= 1) gain += sim.MachineUnitRate[i] * cnt;
            double ratio = gain / c;
            if (ratio > bestRatio) { bestRatio = ratio; best = i; }
            if (c <= cash && ratio > cheapRatio) { cheapRatio = ratio; cheap = i; }
        }
        if (best < 0) return false;
        if (sim.MachineCost(best, 1) <= cash) return sim.BuyMachine(best, 1);
        if (cheap >= 0 && cheapRatio >= bestRatio * 0.35) return sim.BuyMachine(cheap, 1);
        return false;
    }
}
