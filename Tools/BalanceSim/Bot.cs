// The balance bot: plays the real first-person Core. Movement is abstracted to walking times
// (kiosk ↔ fountain, item to item, the ramp once the crust sinks), but every pickup, deposit,
// wish, dig swing, purchase and building goes through the same Sim calls the game makes.
// "engaged" sprints, catches most wishes and shops greedily; "casual" is slower and sloppier.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WishExtractor.Core;

sealed class Bot
{
    public Sim Sim;
    readonly Random rng;
    readonly bool engaged;
    readonly double catchChance, reaction, idleShare;
    readonly HashSet<int> wishDecided = new HashSet<int>();
    public readonly StringBuilder Log = new StringBuilder();
    public readonly List<(string mall, double hours)> MallTimes = new List<(string, double)>();
    readonly Dictionary<string, double> firsts = new Dictionary<string, double>();
    double mallStart;
    public double ReportEvery = 3600;
    float px = -1.5f, pz = -8.5f;
    const float KioskX = -6.5f, KioskZ = -13.5f, EntryX = -1.2f, EntryZ = -8.2f;
    // factory: slot index → building uids in its line. Slots are rim angles in degrees (x = cos, z = sin);
    // the first two carry coin lines, the rest dig lines. Slots that don't fit (stepping stones, props,
    // neighbouring lines) are skipped, like a player would.
    static readonly float[] SlotAngles = MakeSlots();
    const int CoinSlots = 2;
    static float[] MakeSlots()
    {
        var a = new List<float> { 0, 180 };
        foreach (int k in new[] { 6, 3, 1, 5, 2, 4, 8, 7 })
            foreach (int q in new[] { 0, 180 })
            {
                a.Add(q + k * 10);
                a.Add(q - k * 10 + 360);
            }
        for (int i = 0; i < a.Count; i++) a[i] %= 360;
        return a.Distinct().ToArray();
    }
    readonly List<int>[] lines = new List<int>[SlotAngles.Length];
    readonly string[] lineIntake = new string[SlotAngles.Length];
    readonly HashSet<string> failed = new HashSet<string>();

    public Bot(SaveData save, int seed, string profile)
    {
        rng = new Random(seed);
        engaged = profile != "casual";
        catchChance = engaged ? 0.8 : 0.45;
        reaction = engaged ? 1.5 : 3;
        idleShare = engaged ? 0 : 0.25;
        Sim = new Sim(save, seed);
        if (save.itemsPicked == 0 && save.mallIndex == 0 && Sim.Loose.Count == 0) Sim.StartRun();
        Sim.OnDeposit += (cash, n, joke) => income[0] += cash;
        Sim.OnHopperSold += (b, cash, n) => income[1] += cash;
        Sim.OnWishCaught += (w, cash, tokens, first) => income[2] += cash;
        Sim.OnObjectiveDone += (o, reward) => income[3] += reward;
        Sim.OnFined += fine => income[4] += fine;
        Sim.OnStratumReached += s => layerTimes.Add(Sim.Time - mallStart);
        for (int i = 0; i < lines.Length; i++) lines[i] = new List<int>();
        mallStart = Sim.Time;
    }

    static BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];

    void Mark(string what) { if (!firsts.ContainsKey(what)) firsts[what] = Sim.Time - mallStart; }

    // ───────────────────────────── time ─────────────────────────────

    /// <summary>When set, PlayMall records (seconds into the mall, scoops dug) every few seconds (the fitter uses it).</summary>
    public List<(double t, double dug)> Curve;
    double nextCurve;

    /// <summary>Let time pass; the bot keeps an eye on wishes and Chad while it does.</summary>
    void Advance(double seconds)
    {
        while (seconds > 1e-6)
        {
            double step = Math.Min(0.5, seconds);
            Sim.PlayerX = px;
            Sim.PlayerZ = pz;
            Sim.PlayerInWater = px * px + pz * pz < 64;
            Sim.Tick(step);
            seconds -= step;
            if (Curve != null && Sim.Time >= nextCurve) { Curve.Add((Sim.Time - mallStart, Sim.S.dug)); nextCurve = Sim.Time + 5; }
            foreach (var w in Sim.Wishes.ToArray())
            {
                if (w.Age < reaction || wishDecided.Contains(w.Uid)) continue;
                wishDecided.Add(w.Uid);
                if (rng.NextDouble() < catchChance) Sim.CatchWish(w.Uid);
            }
            if (Sim.Rival.State == RivalState.Stealing && Sim.Rival.Timer > (engaged ? 6 : 25)) Sim.ChaseRival();
        }
    }

    double Speed(bool water)
    {
        double v = Balance.WalkSpeed * Sim.WalkSpeedMult * Sim.CarryDef.SpeedMult * (engaged ? Balance.SprintMult : 1);
        return water ? v * Balance.WadeMult : v;
    }

    /// <summary>Extra walking once the crust has sunk: down (and back up) the scaffold ramp.</summary>
    double RampLength => Sim.DepthFrac * 7.5 / 0.36;

    void WalkTo(float x, float z, bool water)
    {
        float dx = x - px, dz = z - pz;
        double d = Math.Sqrt(dx * dx + dz * dz);
        Advance(d / Speed(water));
        px = x; pz = z;
    }

    // ───────────────────────────── a trip: collect, carry, deposit ─────────────────────────────

    void Trip()
    {
        // kiosk → rim → (ramp) → into the water
        px = KioskX; pz = KioskZ;
        WalkTo(EntryX, EntryZ, false);
        Advance(0.8 + RampLength / Speed(false));
        int grabs = 0;
        while (Sim.CarryFree > 0 && grabs < 400)
        {
            var it = PickTarget();
            if (it == null) break;
            var def = Content.Items[it.Type];
            float dx = it.X - px, dz = it.Z - pz;
            double d = Math.Sqrt(dx * dx + dz * dz);
            double reach = Math.Max(0.6, Sim.Reach - 1.4);
            if (d > reach)
            {
                double go = d - reach;
                Advance(go / Speed(true));
                px += (float)(dx / d * go); pz += (float)(dz / d * go);
            }
            var g = Sim.Grab;
            if (g.Area > 0)
            {
                var near = new List<int>();
                foreach (var l in Sim.Loose)
                {
                    if (l.State == LooseState.Airborne) continue;
                    float ax = l.X - it.X, az = l.Z - it.Z;
                    if (ax * ax + az * az <= g.Area * g.Area) near.Add(l.Uid);
                }
                Sim.PickupMany(near);
            }
            else Sim.Pickup(it.Uid);
            Advance(1.0 / (g.Rate * Sim.GrabRateMult));
            grabs++;
            if (Sim.CarryDef.AutoRadius > 0) Vacuum();
        }
        // hand-dig while we're down here, if the fountain's thin and nothing else is digging
        HandDig();
        // back out and up to the kiosk
        Advance(0.8 + RampLength / Speed(false));
        WalkTo(KioskX, KioskZ, false);
        if (Sim.Carried.Count > 0) { Sim.Deposit(); Advance(1); }
        if (idleShare > 0 && rng.NextDouble() < idleShare) Advance(20 + rng.NextDouble() * 40);
    }

    void Vacuum()
    {
        float r = Sim.CarryDef.AutoRadius;
        var near = new List<int>();
        foreach (var l in Sim.Loose)
        {
            if (l.State == LooseState.Airborne) continue;
            float ax = l.X - px, az = l.Z - pz;
            if (ax * ax + az * az <= r * r) near.Add(l.Uid);
        }
        if (near.Count > 0) Sim.PickupMany(near);
    }

    LooseItem PickTarget()
    {
        LooseItem best = null;
        double bs = 0;
        foreach (var l in Sim.Loose)
        {
            if (l.State == LooseState.Airborne || Sim.IsFish(l.Type) || !Sim.CanCarry(l.Type)) continue;
            double v = l.Value * Sim.CatRate(Content.Items[l.Type].Cat);
            if (v <= 0) continue;
            float dx = l.X - px, dz = l.Z - pz;
            double s = (v + 0.004 * Sim.Scale) / (Math.Sqrt(dx * dx + dz * dz) + 1.5);
            if (s > bs) { bs = s; best = l; }
        }
        return best;
    }

    void HandDig()
    {
        if (Sim.DigTier <= 0 || Sim.MallCleared) return;
        bool rigs = Sim.Buildings.Any(b => b.Def.Intake == "dig");
        if (rigs && Sim.Stratum >= 1) return;
        int loose = Sim.Loose.Count(l => l.State != LooseState.Airborne);
        if (loose > 60 && Sim.Stratum == 0 && Sim.CarryFree > 0) return;
        var tool = Sim.DigTool;
        int swings = engaged ? 20 : 10;
        for (int i = 0; i < swings && !Sim.MallCleared; i++)
        {
            Sim.SwingDig(px + 0.8f, pz + 0.8f);
            Advance(1.0 / tool.Rate);
        }
        Mark("hand dig");
    }

    // ───────────────────────────── shopping ─────────────────────────────

    /// <summary>How much the bot wants a tech: its price is multiplied by this (lower = sooner).</summary>
    static double Priority(TechDef t)
    {
        // the headline upgrades (a bigger container, the next fountain job, a new machine) come first;
        // levelled nodes are what's left over
        switch (t.Kind)
        {
            case TechKind.Carry: return 0.25;
            case TechKind.Unlock: return 0.45;
            case TechKind.Tool: return 0.5;
            case TechKind.Wishability: return t.MaxLevel == 1 ? 0.35 : 1.5;
            case TechKind.GuardFine:
            case TechKind.RivalRepel: return 3.0;
            default: return 1.5;
        }
    }

    /// <summary>
    /// Wish Tokens and Lucky Pennies buy whatever they can (they have nothing else to do). Cash has a goal:
    /// the most wanted tech, affordable or not. The bot buys it as soon as it can and, while saving up,
    /// only spends pocket change (a tenth of the goal's price) on anything else, like a player would.
    /// </summary>
    void Shop()
    {
        for (int guard = 0; guard < 80; guard++)
        {
            int buy = -1, goal = -1;
            double buyScore = double.MaxValue, goalScore = double.MaxValue;
            for (int i = 0; i < Content.Techs.Length; i++)
            {
                if (Sim.TechMaxed(i) || !Sim.TechUnlocked(i)) continue;
                var t = Content.Techs[i];
                double score = Sim.TechCost(i) * Priority(t);
                if (t.WishTokens || t.LuckyPennies)
                {
                    if (Sim.CanAfford(i) && score < buyScore) { buyScore = score; buy = i; }
                }
                else if (score < goalScore) { goalScore = score; goal = i; }
            }
            if (buy < 0 && goal >= 0)
            {
                // the factory's next line competes with the tech goal (ManageFactory builds it once it's affordable)
                bool forFactory = factoryWant > 0 && factoryWant * 0.45 < goalScore;
                if (!forFactory && Sim.CanAfford(goal)) buy = goal;
                else
                {
                    double limit = (forFactory ? factoryWant : Sim.TechCost(goal)) * 0.1;
                    for (int i = 0; i < Content.Techs.Length; i++)
                    {
                        var t = Content.Techs[i];
                        if (t.WishTokens || t.LuckyPennies || !Sim.CanBuyTech(i) || Sim.TechCost(i) > limit) continue;
                        double score = Sim.TechCost(i) * Priority(t);
                        if (score < buyScore) { buyScore = score; buy = i; }
                    }
                }
            }
            if (buy < 0) return;
            Sim.BuyTech(buy);
            Mark(Content.Techs[buy].Id);
            NoteBuy();
        }
    }

    // where the money came from this mall: kiosk, hoppers, wishes, objectives (and went: fines)
    readonly double[] income = new double[5];
    readonly List<double> layerTimes = new List<double>();

    // purchase gaps: long stretches with nothing to buy are dead time for a player
    double lastBuy, maxGap, maxGapAt;
    int buys;
    void NoteBuy()
    {
        double gap = Sim.Time - lastBuy;
        if (gap > maxGap) { maxGap = gap; maxGapAt = lastBuy - mallStart; }
        lastBuy = Sim.Time;
        buys++;
    }

    // ───────────────────────────── the factory ─────────────────────────────

    static (int x, int z) AnchorFor(BuildDef d, float px, float pz, int rot)
    {
        int fx = Building.DX[rot], fz = Building.DZ[rot], rx = Building.DX[(rot + 1) & 3], rz = Building.DZ[(rot + 1) & 3];
        float ox = (rx * (d.W - 1) + fx * (d.D - 1)) * 0.5f, oz = (rz * (d.W - 1) + fz * (d.D - 1)) * 0.5f;
        return ((int)Math.Round(px - 0.5f - ox), (int)Math.Round(pz - 0.5f - oz));
    }

    /// <summary>Plan a straight line out from the rim at an angle (degrees): intake, then 2×2 stages, then a hopper.</summary>
    List<(BuildDef d, int x, int z, int rot)> PlanLine(float angleDeg, float radius, string intake, string[] stages, string hopper)
    {
        float a = angleDeg * (float)Math.PI / 180;
        float cx = (float)Math.Cos(a) * radius, cz = (float)Math.Sin(a) * radius;
        int rot = Sim.FacingRotation(cx, cz);
        var di = D(intake);
        var (ax, az) = AnchorFor(di, cx, cz, rot);
        var plan = new List<(BuildDef, int, int, int)> { (di, ax, az, rot) };
        int dir = (rot + 2) & 3;
        int fx = Building.DX[dir], fz = Building.DZ[dir];
        int rX = Building.DX[(rot + 1) & 3], rZ = Building.DZ[(rot + 1) & 3];
        // the intake's back row: cells (0,0) and (W-1,0); the next stage's anchor sits behind cell (W-1, 0)
        int w = di.W;
        int bx = ax + rX * (w - 1), bz = az + rZ * (w - 1);
        int nx = bx + fx, nz = bz + fz;
        if (w == 1)
        {
            // a 1-wide skimmer dock: one belt straight back, then the rest
            plan.Add((D("belt"), nx, nz, dir));
            nx += fx; nz += fz;
            // stages are 2 wide; shift so their back row covers the belt
        }
        foreach (var s in stages)
        {
            plan.Add((D(s), nx, nz, dir));
            nx += fx * D(s).D; nz += fz * D(s).D;
        }
        plan.Add((D(hopper), nx, nz, dir));
        return plan;
    }

    bool TryBuild(List<(BuildDef d, int x, int z, int rot)> plan, List<int> into)
    {
        double cost = plan.Sum(p => p.d.Cost) * Sim.Scale;
        if (Sim.S.cash < cost) return false;
        foreach (var p in plan) if (!Sim.CanPlace(p.d, p.x, p.z, p.rot, out _, true)) return false;
        foreach (var p in plan)
        {
            var b = Sim.Place(p.d, p.x, p.z, p.rot);
            if (b == null) { foreach (int u in into) Sim.Remove(u); into.Clear(); return false; }
            into.Add(b.Uid);
            Advance(3);
        }
        return true;
    }

    void ClearLine(int slot)
    {
        foreach (int u in lines[slot]) Sim.Remove(u);
        lines[slot].Clear();
        lineIntake[slot] = null;
        lineSig[slot] = null;
    }

    string BestUnlocked(params string[] ids)
    {
        string best = null;
        foreach (var id in ids) if (Sim.BuildUnlocked(D(id))) best = id;
        return best;
    }

    static int Rank(string id, string[] ladder) => id == null ? -1 : Array.IndexOf(ladder, id);

    static readonly string[] CoinLadder = { "intake_skimmer", "intake_pump", "intake_claw" };
    static readonly string[] DigLadder = { "dig_rig", "dig_borer" };

    /// <summary>
    /// The first placement near a slot where every machine of the line fits: the exact angle and radius
    /// first, then nudged outward and sideways (a 3-deep borer needs to stand further out). Null = no room.
    /// </summary>
    List<(BuildDef d, int x, int z, int rot)> FindPlan(int slot, string intake, string[] stages, string hopper)
    {
        foreach (float dr in new[] { 0f, 0.5f, 1f })
            foreach (float da in new[] { 0f, 4f, -4f })
            {
                var plan = PlanLine(SlotAngles[slot] + da, 10.6f + dr, intake, stages, hopper);
                if (plan.All(p => Sim.CanPlace(p.d, p.x, p.z, p.rot, out _, true))) return plan;
            }
        return null;
    }

    /// <summary>Diagnostics: build a borer line in every slot that has room (everything unlocked).</summary>
    public string ProbeSlots()
    {
        foreach (var t in Content.Techs) if (t.Kind == TechKind.Unlock) Sim.DebugSetTech(t.Id, 1);
        Sim.DebugAddCash(1e12);
        var sb = new StringBuilder();
        int built = 0;
        for (int s = 0; s < SlotAngles.Length; s++)
        {
            string intake = s < CoinSlots ? "intake_claw" : "dig_borer";
            var stages = s < CoinSlots ? new[] { "proc_roller" } : new[] { "proc_tumbler", "proc_sorter" };
            var plan = FindPlan(s, intake, stages, "hopper2");
            bool ok = plan != null && TryBuild(plan, lines[s]);
            if (ok) built++;
            sb.AppendLine($"slot {s,2} ({SlotAngles[s],3}°): {(ok ? "built at " + string.Join(" ", plan.Select(p => $"{p.d.Id}({p.x},{p.z})")) : "no room")}");
        }
        sb.AppendLine($"{built} lines");
        return sb.ToString();
    }

    /// <summary>The cheapest line the factory wants next but can't afford yet (0 = nothing): Shop saves for it.</summary>
    double factoryWant;
    bool bigHoppers;
    readonly string[] lineSig = new string[SlotAngles.Length];
    readonly (string intake, string[] stages, string hopper)[] linePlan = new (string, string[], string)[SlotAngles.Length];

    /// <summary>(Re)build the line in a slot if its planned machines beat what's there. False = can't afford it yet.</summary>
    bool UpgradeLine(int s, string intake, string[] stages, string hopper)
    {
        string sig = intake + "|" + string.Join(",", stages) + "|" + hopper;
        if (lineSig[s] == sig || failed.Contains(s + sig)) return true;
        double cost = (D(intake).Cost + stages.Sum(st => D(st).Cost) + D(hopper).Cost + (D(intake).W == 1 ? D("belt").Cost : 0)) * Sim.Scale;
        double need = cost * (lineSig[s] == null ? 1.05 : 1.3);
        if (Sim.S.cash < need)
        {
            if (factoryWant <= 0 || need < factoryWant) factoryWant = need;
            return false;
        }
        var old = linePlan[s];
        bool had = lineSig[s] != null;
        if (had) ClearLine(s);
        var plan = FindPlan(s, intake, stages, hopper);
        if (plan != null && TryBuild(plan, lines[s]))
        {
            lineIntake[s] = intake; lineSig[s] = sig; linePlan[s] = (intake, stages, hopper);
            Mark("line " + intake);
            return true;
        }
        failed.Add(s + sig);
        lineSig[s] = null;
        var back = had ? FindPlan(s, old.intake, old.stages, old.hopper) : null;
        if (back != null && TryBuild(back, lines[s]))
        {
            // put the old line back (the upgrade didn't fit here)
            lineIntake[s] = old.intake;
            lineSig[s] = old.intake + "|" + string.Join(",", old.stages) + "|" + old.hopper;
            linePlan[s] = old;
        }
        return true;
    }

    void ManageFactory()
    {
        factoryWant = 0;
        if (!Sim.BuildUnlocked(D("hopper")) || !Sim.BuildUnlocked(D("gen_hamster"))) return;
        if (Sim.BuildUnlocked(D("hopper2")) && Sim.S.cash > 20 * D("hopper2").Cost * Sim.Scale) bigHoppers = true;
        string hopper = bigHoppers ? "hopper2" : "hopper";
        // coin lines on the east and west (rollers make coins worth 10% more)
        string coin = BestUnlocked(CoinLadder);
        if (coin != null)
            for (int s = 0; s < CoinSlots; s++)
            {
                var stages = coin != "intake_skimmer" && Sim.BuildUnlocked(D("proc_roller")) ? new[] { "proc_roller" } : new string[0];
                if (!UpgradeLine(s, coin, stages, hopper)) break;
            }
        // dig lines (need the tumbler and a sorter to be worth it below the loose layer); dug loot isn't
        // coins, so there's no roller on these
        string dig = BestUnlocked(DigLadder);
        string sorter = BestUnlocked("proc_pigeons", "proc_sorter");
        if (dig != null && sorter != null && Sim.BuildUnlocked(D("proc_tumbler")) && !Sim.MallCleared)
            for (int s = CoinSlots; s < SlotAngles.Length; s++)
                if (!UpgradeLine(s, dig, new[] { "proc_tumbler", sorter }, hopper)) break;
        EnsurePower();
    }

    void EnsurePower()
    {
        string[] gens = { "gen_hamster", "gen_diesel", "gen_fryer", "gen_solar" };
        for (int guard = 0; guard < 12 && Sim.PowerGen < Sim.PowerUse * 1.1 + 0.5; guard++)
        {
            BuildDef pick = null;
            for (int i = gens.Length - 1; i >= 0; i--)
            {
                var d = D(gens[i]);
                if (Sim.BuildUnlocked(d) && Sim.S.cash >= d.Cost * Sim.Scale) { pick = d; break; }
            }
            if (pick == null) return;
            bool placed = false;
            for (int z = -27; z <= -17 && !placed; z += 2)
                for (int x = 18; x <= 32 && !placed; x += 2)
                    if (Sim.CanPlace(pick, x, z, 0, out _)) { Sim.Place(pick, x, z, 0); placed = true; Advance(3); }
            if (!placed) return;
        }
    }

    // ───────────────────────────── the loop ─────────────────────────────

    /// <summary>Play the current mall until bare concrete (then sign). Returns hours taken, or -1 on timeout.</summary>
    public double PlayMall(double maxHours)
    {
        mallStart = Sim.Time;
        firsts.Clear();
        for (int i = 0; i < lines.Length; i++) { lines[i].Clear(); lineIntake[i] = null; lineSig[i] = null; linePlan[i] = default; }
        failed.Clear();
        bigHoppers = false;
        factoryWant = 0;
        lastBuy = mallStart; maxGap = 0; maxGapAt = 0; buys = 0;
        Array.Clear(income, 0, income.Length);
        layerTimes.Clear();
        var mall = Sim.Mall;
        double end = mallStart + maxHours * 3600;
        double nextReport = mallStart + ReportEvery, lastDug = Sim.S.dug, lastEarned = Sim.S.runCash;
        while (!Sim.MallCleared && Sim.Time < end)
        {
            Trip();
            Shop();
            ManageFactory();
            Shop();
            if (Sim.Time >= nextReport)
            {
                nextReport += ReportEvery;
                Log.AppendLine($"  [{mall.Id}] {Fmt.Time(Sim.Time - mallStart)}: cash {Fmt.Money(Sim.S.cash)}, earned {Fmt.Money(Sim.S.runCash)} (+{Fmt.Money((Sim.S.runCash - lastEarned) / ReportEvery * 60)}/min), " +
                               $"dug {Sim.S.dug:0} ({(Sim.S.dug - lastDug) / ReportEvery:0.#}/s), depth {Sim.DepthFeet:0.0}/{mall.DepthFeet} ft (stratum {Sim.Stratum}), " +
                               $"wish {Sim.Wishability:0}, carry {Sim.CarryDef.Name}, machines {Sim.MachinesBuilt}, power {Sim.PowerGen:0}/{Sim.PowerUse:0}, " +
                               $"hoppers {Fmt.Money(Sim.HopperCashRate * 60)}/min, techs {Sim.TechLevelsOwned}, buys {buys}");
                lastDug = Sim.S.dug;
                lastEarned = Sim.S.runCash;
            }
        }
        double hours = (Sim.Time - mallStart) / 3600;
        Curve?.Add((Sim.Time - mallStart, Sim.S.dug));
        double tail = Sim.Time - lastBuy;
        if (tail > maxGap) { maxGap = tail; maxGapAt = lastBuy - mallStart; }
        Log.AppendLine($"  [{mall.Id}] {buys} purchases; longest gap {Fmt.Time(maxGap)} from {Fmt.Time(maxGapAt)}; last purchase {Fmt.Time(lastBuy - mallStart)}; " +
                       $"income: kiosk {Fmt.Money(income[0])}, hoppers {Fmt.Money(income[1])}, wishes {Fmt.Money(income[2])}, objectives {Fmt.Money(income[3])}, fines -{Fmt.Money(income[4])}; " +
                       $"value ×{Sim.ValueMult:0.0}, wishability {Sim.Wishability:0}, crust {Sim.TotalScoops:0} scoops, loot EV {Fmt.Money(mall.BaseEV)}/scoop");
        Log.AppendLine($"  [{mall.Id}] layers reached at " + string.Join(", ", layerTimes.Select(t => Fmt.Time(t))) + $"; bare concrete {Fmt.Time(Sim.Time - mallStart)}");
        Log.AppendLine($"  [{mall.Id}] lines at the end: " + string.Join(" ", Enumerable.Range(0, SlotAngles.Length).Select(s => lineSig[s] == null ? "-" : lineSig[s].Split('|')[0].Replace("intake_", "").Replace("dig_", ""))) +
                       $"; plans that didn't fit: {failed.Count}");
        string firstsLine = string.Join(", ", firsts.Where(kv => kv.Key.StartsWith("carry_") || kv.Key.StartsWith("line ") || kv.Key.StartsWith("unlock_") || kv.Key.StartsWith("fountain_") && !kv.Key.Contains("polish") && !kv.Key.Contains("mints") || kv.Key.StartsWith("dig_"))
            .OrderBy(kv => kv.Value).Take(24).Select(kv => $"{kv.Key} {Fmt.Time(kv.Value)}"));
        Log.AppendLine($"  [{mall.Id}] firsts: {firstsLine}");
        if (!Sim.MallCleared) return -1;
        MallTimes.Add((mall.Name, hours));
        return hours;
    }

    public void SignAndSpend()
    {
        Sim.Prestige();
        wishDecided.Clear();
        Shop();
    }
}
