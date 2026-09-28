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
    // factory: slot index → building uids in its line
    static readonly float[] SlotAngles = { 0, 180, 60, 120, 240, 300, 45, 135, 225, 315, 15, 165, 195, 345, 75, 105, 255, 285 };   // (cos, sin) degrees
    const int CoinSlots = 2;                   // the first two carry coin lines; the rest dig lines
    readonly List<int>[] lines = new List<int>[SlotAngles.Length];
    readonly string[] lineIntake = new string[SlotAngles.Length];
    readonly string[] lineSig = new string[SlotAngles.Length];
    readonly string[][] lineStages = new string[SlotAngles.Length][];
    readonly HashSet<string> failed = new HashSet<string>();

    // diagnostics: where the money comes from and goes, per report interval and per mall
    readonly Dictionary<string, double> earnedBy = new Dictionary<string, double>(), spentBy = new Dictionary<string, double>();
    double lastBuy, longestGap, gapAt, reportDug, reportTime;
    void Earn(string k, double v) { earnedBy.TryGetValue(k, out double o); earnedBy[k] = o + v; }
    void Spend(string k, double v)
    {
        if (v > 0) { spentBy.TryGetValue(k, out double o); spentBy[k] = o + v; }
        double gap = Sim.Time - lastBuy;
        if (gap > longestGap) { longestGap = gap; gapAt = lastBuy - mallStart; }
        lastBuy = Sim.Time;
    }

    public Bot(SaveData save, int seed, string profile)
    {
        rng = new Random(seed);
        engaged = profile != "casual";
        catchChance = engaged ? 0.8 : 0.45;
        reaction = engaged ? 1.5 : 3;
        idleShare = engaged ? 0 : 0.25;
        Sim = new Sim(save, seed);
        if (save.itemsPicked == 0 && save.mallIndex == 0 && Sim.Loose.Count == 0) Sim.StartRun();
        for (int i = 0; i < lines.Length; i++) lines[i] = new List<int>();
        mallStart = Sim.Time;
        Sim.OnDeposit += (cash, n, joke) => Earn("kiosk", cash);
        Sim.OnHopperSold += (b, cash, n) => Earn("hoppers", cash);
        Sim.OnWishCaught += (w, cash, tokens, first) => Earn("wishes", cash);
        Sim.OnObjectiveDone += (o, reward) => Earn("objectives", reward);
        Sim.OnFined += fine => Earn("fines", -fine);
    }

    static string Split(Dictionary<string, double> d)
    {
        double t = d.Values.Sum();
        if (Math.Abs(t) < 1e-9) return "-";
        return string.Join(" ", d.OrderByDescending(kv => kv.Value).Select(kv => $"{kv.Key} {kv.Value / t * 100:0}%"));
    }

    static BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];

    void Mark(string what) { if (!firsts.ContainsKey(what)) firsts[what] = Sim.Time - mallStart; }

    // ───────────────────────────── time ─────────────────────────────

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

    /// <summary>
    /// Shop like a player working down the terminal: save for the next one-off unlock (the cheapest
    /// "rung" not yet owned), picking up levelled upgrades on the way only while they cost at most
    /// a quarter of that rung. Wish Tokens and Lucky Pennies are separate budgets, spent cheapest first.
    /// </summary>
    void Shop()
    {
        for (int guard = 0; guard < 80; guard++)
        {
            int rung = -1, lev = -1, tok = -1, lp = -1;
            double rungC = double.MaxValue, levC = double.MaxValue, tokC = double.MaxValue, lpC = double.MaxValue;
            for (int i = 0; i < Content.Techs.Length; i++)
            {
                if (Sim.TechMaxed(i) || !Sim.TechUnlocked(i)) continue;
                var t = Content.Techs[i];
                double c = Sim.TechCost(i);
                if (t.LuckyPennies) { if (c < lpC) { lpC = c; lp = i; } }
                else if (t.WishTokens) { if (c < tokC) { tokC = c; tok = i; } }
                else if (t.MaxLevel == 1) { if (c < rungC) { rungC = c; rung = i; } }
                else if (c < levC) { levC = c; lev = i; }
            }
            int best = -1;
            if (lp >= 0 && Sim.CanAfford(lp)) best = lp;
            else if (tok >= 0 && Sim.CanAfford(tok)) best = tok;
            else if (rung >= 0 && Sim.CanAfford(rung)) best = rung;
            else if (lev >= 0 && Sim.CanAfford(lev) && (rung < 0 || levC <= 0.25 * rungC)) best = lev;
            if (best < 0) return;
            var bt = Content.Techs[best];
            double paid = Sim.TechCost(best);
            Sim.BuyTech(best);
            Spend(bt.Branch.ToString().ToLowerInvariant(), bt.WishTokens || bt.LuckyPennies ? 0 : paid);
            Mark(bt.Id);
        }
    }

    // ───────────────────────────── the factory ─────────────────────────────

    static (int x, int z) AnchorFor(BuildDef d, float px, float pz, int rot)
    {
        int fx = Building.DX[rot], fz = Building.DZ[rot], rx = Building.DX[(rot + 1) & 3], rz = Building.DZ[(rot + 1) & 3];
        float ox = (rx * (d.W - 1) + fx * (d.D - 1)) * 0.5f, oz = (rz * (d.W - 1) + fz * (d.D - 1)) * 0.5f;
        return ((int)Math.Round(px - 0.5f - ox), (int)Math.Round(pz - 0.5f - oz));
    }

    /// <summary>Plan a straight line out from the rim: intake, then 2×2 stages, then a hopper.</summary>
    List<(BuildDef d, int x, int z, int rot)> PlanLine(int slot, string intake, string[] stages, string hopper, float radius)
    {
        float a = SlotAngles[slot] * (float)Math.PI / 180;
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
        foreach (var s in stages ?? new string[0])
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
        Spend("build", cost);
        return true;
    }

    void ClearLine(int slot)
    {
        foreach (int u in lines[slot]) Sim.Remove(u);
        lines[slot].Clear();
        lineIntake[slot] = null;
        lineSig[slot] = null;
        lineStages[slot] = null;
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

    void ManageFactory()
    {
        if (!Sim.BuildUnlocked(D("hopper")) || !Sim.BuildUnlocked(D("gen_hamster"))) return;
        string hopper = Sim.BuildUnlocked(D("hopper2")) && Sim.S.cash > 40000 * Sim.Scale ? "hopper2" : "hopper";
        // coin lines on the east and west; once there's gunk in the water (hand digging), they wash and sort it too
        string coin = BestUnlocked(CoinLadder);
        string sorter = BestUnlocked("proc_pigeons", "proc_sorter");
        if (coin != null)
            for (int s = 0; s < CoinSlots; s++)
            {
                if (Rank(lineIntake[s], CoinLadder) > Rank(coin, CoinLadder)) continue;
                var cst = new List<string>();
                if (Sim.S.maxStratum >= 1 && sorter != null && Sim.BuildUnlocked(D("proc_tumbler"))) { cst.Add("proc_tumbler"); cst.Add(sorter); }
                if (Sim.BuildUnlocked(D("proc_roller")) && coin != "intake_skimmer") cst.Add("proc_roller");
                var stages = cst.ToArray();
                string sig = coin + "|" + string.Join(",", stages);
                if (lineSig[s] == sig || failed.Contains(s + sig)) continue;
                var plan = PlanLine(s, coin, stages, hopper, 10.6f);
                double cost = plan.Sum(p => p.d.Cost) * Sim.Scale;
                if (Sim.S.cash < cost * (lineIntake[s] == null ? 1 : 1.5)) continue;
                var old = lineIntake[s];
                var oldStages = lineStages[s];
                if (old != null) ClearLine(s);
                if (TryBuild(plan, lines[s])) { lineIntake[s] = coin; lineSig[s] = sig; lineStages[s] = stages; Mark("line " + coin); }
                else
                {
                    failed.Add(s + sig);
                    if (old != null && TryBuild(PlanLine(s, old, oldStages, "hopper", 10.6f), lines[s])) { lineIntake[s] = old; lineStages[s] = oldStages; lineSig[s] = old + "|" + string.Join(",", oldStages); }
                }
            }
        // dig lines (need the tumbler and a sorter to be worth it below the loose layer)
        string dig = BestUnlocked(DigLadder);
        if (dig != null && sorter != null && Sim.BuildUnlocked(D("proc_tumbler")) && !Sim.MallCleared)
            for (int s = CoinSlots; s < SlotAngles.Length; s++)
            {
                if (Rank(lineIntake[s], DigLadder) >= Rank(dig, DigLadder) || failed.Contains(s + dig)) continue;
                var stages = new List<string> { "proc_tumbler", sorter };
                var plan = PlanLine(s, dig, stages.ToArray(), hopper, 10.6f);
                double cost = plan.Sum(p => p.d.Cost) * Sim.Scale;
                if (Sim.S.cash < cost * (lineIntake[s] == null ? 1.1 : 1.6)) break;
                var old = lineIntake[s];
                if (old != null) ClearLine(s);
                if (TryBuild(plan, lines[s])) { lineIntake[s] = dig; Mark("line " + dig); break; }
                failed.Add(s + dig);
                if (old != null)
                {
                    // put the old line back (the upgrade didn't fit here)
                    var back = PlanLine(s, old, stages.ToArray(), "hopper", 10.6f);
                    if (TryBuild(back, lines[s])) lineIntake[s] = old;
                }
            }
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
                    if (Sim.CanPlace(pick, x, z, 0, out _)) { Sim.Place(pick, x, z, 0); placed = true; Spend("build", pick.Cost * Sim.Scale); Advance(3); }
            if (!placed) return;
        }
    }

    // ───────────────────────────── the loop ─────────────────────────────

    /// <summary>Play the current mall until bare concrete (then sign). Returns hours taken, or -1 on timeout.</summary>
    public double PlayMall(double maxHours)
    {
        mallStart = Sim.Time;
        firsts.Clear();
        for (int i = 0; i < lines.Length; i++) { lines[i].Clear(); lineIntake[i] = null; lineSig[i] = null; lineStages[i] = null; }
        failed.Clear();
        earnedBy.Clear();
        spentBy.Clear();
        lastBuy = mallStart; longestGap = 0; gapAt = 0;
        reportDug = Sim.S.dug; reportTime = Sim.Time;
        var mall = Sim.Mall;
        double end = mallStart + maxHours * 3600;
        double nextReport = mallStart + ReportEvery;
        while (!Sim.MallCleared && Sim.Time < end)
        {
            Trip();
            ManageFactory();
            Shop();
            ManageFactory();
            if (Sim.Time >= nextReport)
            {
                nextReport += ReportEvery;
                double digRate = (Sim.S.dug - reportDug) / Math.Max(1, Sim.Time - reportTime);
                reportDug = Sim.S.dug; reportTime = Sim.Time;
                Log.AppendLine($"  [{mall.Id}] {Fmt.Time(Sim.Time - mallStart)}: cash {Fmt.Money(Sim.S.cash)}, earned {Fmt.Money(Sim.S.runCash)}, dug {Fmt.Num(Sim.S.dug)} ({Fmt.Num(digRate)}/s), depth {Sim.DepthFeet:0.0}/{mall.DepthFeet} ft (stratum {Sim.Stratum}), " +
                               $"wish {Sim.Wishability:0}, carry {Sim.CarryDef.Name}, rigs {Sim.CountBuilt("dig_rig")}+{Sim.CountBuilt("dig_borer")} (dig ×{Sim.DigMult:0.#}, intake ×{Sim.CatSpeed(BuildCat.Intake):0.#}), machines {Sim.MachinesBuilt}, power {Sim.PowerGen:0}/{Sim.PowerUse:0}, " +
                               $"hoppers {Fmt.Money(Sim.HopperCashRate * 60)}/min, techs {Sim.TechLevelsOwned}");
            }
        }
        double hours = (Sim.Time - mallStart) / 3600;
        string firstsLine = string.Join(", ", firsts.Where(kv => kv.Key.StartsWith("carry_") || kv.Key.StartsWith("line ") || kv.Key.StartsWith("unlock_") || kv.Key.StartsWith("fountain_") && !kv.Key.Contains("polish") && !kv.Key.Contains("mints") || kv.Key.StartsWith("dig_"))
            .OrderBy(kv => kv.Value).Take(24).Select(kv => $"{kv.Key} {Fmt.Time(kv.Value)}"));
        Log.AppendLine($"  [{mall.Id}] firsts: {firstsLine}");
        Log.AppendLine($"  [{mall.Id}] earned {Fmt.Money(earnedBy.Values.Sum())}: {Split(earnedBy)} | spent: {Split(spentBy)} | longest gap between purchases {Fmt.Time(longestGap)} (from {Fmt.Time(gapAt)})");
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
