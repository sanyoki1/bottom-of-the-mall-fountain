// The factory: a one-metre grid on the hall floor holding buildings (generators, intakes at the
// fountain's edge, belts, splitters, hoppers, processors). Everything is plain data ticked here,
// so the balance bot runs the same factory the player builds. Power is one global budget: if
// demand beats supply, every consumer slows by the same ratio.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public sealed class BeltItem
    {
        public int Type;
        public double Value;
        public float Pos;      // 0 = entry edge, 1 = exit edge
    }

    public sealed class Building
    {
        public int Uid;
        public BuildDef Def;
        public int X, Z, Rot;                 // anchor cell (local 0,0) and rotation 0..3 (local +z → world +z, +x, -z, -x)
        public readonly List<BeltItem> Items = new List<BeltItem>();   // belts
        public readonly List<ItemStack> Buf = new List<ItemStack>();   // machine buffer (processors: the input queue)
        public int BufCount;
        public readonly List<ItemStack> Out = new List<ItemStack>();   // processors: finished items waiting to leave
        public int OutCount;
        public double PartialCount, PartialValue;                      // rollers, baggers: coins collected toward the next bundle
        public int PartialType = -1;
        public double Acc, OutAcc;
        public int SplitNext;
        public float Activity;                // 0..1, smoothed, for the view
        public string Status = "";
        // skimmer bot
        public float BotX, BotZ;
        public byte BotState;                 // 0 seek, 1 return, 2 unload
        public int BotTarget;
        public readonly List<(int type, double value)> BotLoad = new List<(int, double)>();
        public float Pause;
        public int LastPicked = -1;           // uid of the item the intake just grabbed (for the view)
        public float LastPickX, LastPickZ;
        public int PickSerial;

        public static readonly int[] DX = { 0, 1, 0, -1 }, DZ = { 1, 0, -1, 0 };
        public int Fx => DX[Rot];
        public int Fz => DZ[Rot];
        public int Rx => DX[(Rot + 1) & 3];
        public int Rz => DZ[(Rot + 1) & 3];
        public (int x, int z) Cell(int lx, int lz) => (X + Rx * lx + Fx * lz, Z + Rz * lx + Fz * lz);
        /// <summary>World centre of the footprint (metres).</summary>
        public (float x, float z) Center
        {
            get
            {
                float cx = X + 0.5f + (Rx * (Def.W - 1) + Fx * (Def.D - 1)) * 0.5f;
                float cz = Z + 0.5f + (Rz * (Def.W - 1) + Fz * (Def.D - 1)) * 0.5f;
                return (cx, cz);
            }
        }
        public float Radius => Math.Max(Def.W, Def.D) * 0.6f;
    }

    public sealed partial class Sim
    {
        public const float BeltSpacing = 0.25f;
        public const int HallMinX = -35, HallMaxX = 34, HallMinZ = -29, HallMaxZ = 30;   // inclusive cell ranges

        public readonly List<Building> Buildings = new List<Building>();
        readonly Dictionary<long, Building> grid = new Dictionary<long, Building>();
        public double PowerGen { get; private set; }
        public double PowerUse { get; private set; }
        public double PowerRatio { get; private set; } = 1;
        public int BeltTier { get; private set; }
        public double HopperCashRate { get; private set; }
        double hopperWindow, hopperWindowTime;

        public event Action<Building> OnBuilt;
        public event Action<Building> OnRemoved;
        public event Action<Building, double, int> OnHopperSold;

        static long Key(int x, int z) => ((long)x << 32) ^ (uint)z;
        public Building At(int x, int z) => grid.TryGetValue(Key(x, z), out var b) ? b : null;
        public Building FindBuilding(int buildingUid) => Buildings.Find(b => b.Uid == buildingUid);

        public bool BuildUnlocked(BuildDef d) => string.IsNullOrEmpty(d.Tech) || TechLevel(d.Tech) > 0;

        double MachineMult(BuildCat cat)
        {
            double m = 1;
            for (int i = 0; i < techLevel.Length; i++)
            {
                var t = Content.Techs[i];
                if (t.Kind == TechKind.MachineSpeed && techLevel[i] > 0 && (string.IsNullOrEmpty(t.Target) || t.Target == cat.ToString()))
                    m *= Math.Pow(1 + t.Value, techLevel[i]);
            }
            return m;
        }
        double[] catMult = { 1, 1, 1, 1, 1 };
        public double CatSpeed(BuildCat c) => catMult[(int)c];

        void RecalcFactory()
        {
            int tier = 0;
            for (int i = 0; i < techLevel.Length; i++)
                if (Content.Techs[i].Kind == TechKind.BeltSpeed && techLevel[i] > 0) tier = Math.Max(tier, (int)Content.Techs[i].Value);
            BeltTier = tier;
            for (int c = 0; c < catMult.Length; c++) catMult[c] = MachineMult((BuildCat)c);
        }

        public float BeltSpeedNow(BuildDef d) => d.BeltSpeed * (1 << BeltTier);

        // ───────────────────────────── placement ─────────────────────────────

        static bool CellBlockedByLayout(int x, int z)
        {
            float cx = x + 0.5f, cz = z + 0.5f;
            if (x < HallMinX || x > HallMaxX || z < HallMinZ || z > HallMaxZ) return true;
            if (cx * cx + cz * cz < (Balance.BasinRadius + 1.7f) * (Balance.BasinRadius + 1.7f)) return true;   // the rim and stepping stones
            foreach (var o in Layout.Obstacles)
            {
                float dx = cx - o.x, dz = cz - o.z;
                if (dx * dx + dz * dz < (o.r + 0.2f) * (o.r + 0.2f)) return true;
            }
            // keep the spawn spot and the path from the entrance clear
            if (Math.Abs(cx) < 1.5f && cz < -16f) return true;
            // and the stepping stones, or you'd wall yourself out of the fountain
            foreach (float a in Layout.StoneAngles)
            {
                float sx = (float)Math.Cos(a * Math.PI / 180) * 9.95f, sz = (float)Math.Sin(a * Math.PI / 180) * 9.95f;
                if ((cx - sx) * (cx - sx) + (cz - sz) * (cz - sz) < 1.6f * 1.6f) return true;
            }
            return false;
        }

        /// <summary>Can this be placed here? reason explains why not.</summary>
        public bool CanPlace(BuildDef d, int x, int z, int rot, out string reason, bool ignoreCost = false)
        {
            reason = null;
            if (!BuildUnlocked(d)) { reason = "Not researched yet"; return false; }
            if (!ignoreCost && S.cash < d.Cost * Scale - 1e-9) { reason = $"Costs {Fmt.Money(d.Cost * Scale)}"; return false; }
            var probe = new Building { Def = d, X = x, Z = z, Rot = rot & 3 };
            bool nearRim = false;
            for (int lx = 0; lx < d.W; lx++)
                for (int lz = 0; lz < d.D; lz++)
                {
                    var (cx, cz) = probe.Cell(lx, lz);
                    if (grid.ContainsKey(Key(cx, cz))) { reason = "Something's already there"; return false; }
                    if (CellBlockedByLayout(cx, cz)) { reason = "Can't build here"; return false; }
                    float px = cx + 0.5f, pz = cz + 0.5f;
                    if (px * px + pz * pz < (Balance.BasinRadius + 3.2f) * (Balance.BasinRadius + 3.2f)) nearRim = true;
                }
            if (d.RimOnly)
            {
                if (!nearRim) { reason = "Must stand at the fountain's edge"; return false; }
                var (mx, mz) = probe.Center;
                float len = (float)Math.Sqrt(mx * mx + mz * mz);
                float dot = (probe.Fx * -mx + probe.Fz * -mz) / Math.Max(0.01f, len);
                if (dot < 0.5f) { reason = "Face it toward the fountain (R)"; return false; }
            }
            return true;
        }

        /// <summary>The rotation a rim machine at this spot should use to face the fountain.</summary>
        public static int FacingRotation(float x, float z)
        {
            float ax = -x, az = -z;
            if (Math.Abs(ax) > Math.Abs(az)) return ax > 0 ? 1 : 3;
            return az > 0 ? 0 : 2;
        }

        public Building Place(BuildDef d, int x, int z, int rot, bool free = false)
        {
            if (!CanPlace(d, x, z, rot, out _, free)) return null;
            if (!free) S.cash -= d.Cost * Scale;
            var b = new Building { Uid = ++uid, Def = d, X = x, Z = z, Rot = rot & 3 };
            AddBuilding(b);
            S.built++;
            OnBuilt?.Invoke(b);
            return b;
        }

        void AddBuilding(Building b)
        {
            Buildings.Add(b);
            for (int lx = 0; lx < b.Def.W; lx++)
                for (int lz = 0; lz < b.Def.D; lz++)
                {
                    var (cx, cz) = b.Cell(lx, lz);
                    grid[Key(cx, cz)] = b;
                }
            if (b.Def.Intake == "skimmer") { var (dx, dz) = DockPoint(b); b.BotX = dx; b.BotZ = dz; }
        }

        /// <summary>Remove a building (refunds its build cost; anything inside it is lost).</summary>
        public bool Remove(int buildingUid)
        {
            var b = FindBuilding(buildingUid);
            if (b == null) return false;
            Buildings.Remove(b);
            for (int lx = 0; lx < b.Def.W; lx++)
                for (int lz = 0; lz < b.Def.D; lz++)
                {
                    var (cx, cz) = b.Cell(lx, lz);
                    grid.Remove(Key(cx, cz));
                }
            AddCash(b.Def.Cost * Scale);
            S.runCash -= b.Def.Cost * Scale;
            S.lifetimeCash -= b.Def.Cost * Scale;
            OnRemoved?.Invoke(b);
            return true;
        }

        void ClearFactory()
        {
            Buildings.Clear();
            grid.Clear();
        }

        /// <summary>Where an intake reaches into the basin: a point just inside the rim, in front of it.</summary>
        public (float x, float z) DockPoint(Building b)
        {
            var (cx, cz) = b.Center;
            float len = (float)Math.Sqrt(cx * cx + cz * cz);
            float r = Balance.BasinRadius - 0.6f;
            return (cx / len * r, cz / len * r);
        }

        public (float x, float z) SuctionPoint(Building b)
        {
            var (cx, cz) = b.Center;
            float len = (float)Math.Sqrt(cx * cx + cz * cz);
            float r = Balance.BasinRadius - 2.8f;
            return (cx / len * r, cz / len * r);
        }

        // ───────────────────────────── buffers ─────────────────────────────

        static bool BufAdd(Building b, int type, double value)
        {
            if (b.BufCount >= b.Def.Capacity) return false;
            foreach (var s in b.Buf) if (s.Type == type) { s.Count++; s.Value += value; b.BufCount++; return true; }
            b.Buf.Add(new ItemStack { Type = type, Count = 1, Value = value });
            b.BufCount++;
            return true;
        }

        static bool BufTake(Building b, out int type, out double value)
        {
            type = -1; value = 0;
            if (b.BufCount <= 0 || b.Buf.Count == 0) return false;
            var s = b.Buf[0];
            type = s.Type;
            value = s.Count > 0 ? s.Value / s.Count : 0;
            s.Count--;
            s.Value -= value;
            b.BufCount--;
            if (s.Count <= 0) b.Buf.RemoveAt(0);
            return true;
        }

        /// <summary>Can building b accept an item arriving from cell (fx,fz)?</summary>
        bool AcceptsFrom(Building b, int fromX, int fromZ)
        {
            if (b.Def.IsBelt)
            {
                // a belt takes items from anywhere but its own exit
                return !(b.X + b.Fx == fromX && b.Z + b.Fz == fromZ);
            }
            if (b.Def.AnySideInput) return true;
            foreach (var p in b.Def.Inputs)
            {
                var (cx, cz) = b.Cell(p.x, p.z);
                int d = (p.side + b.Rot) & 3;
                if (cx + Building.DX[d] == fromX && cz + Building.DZ[d] == fromZ) return true;
            }
            return false;
        }

        bool TryDeliver(Building target, int fromX, int fromZ, int type, double value)
        {
            if (target == null || !AcceptsFrom(target, fromX, fromZ)) return false;
            if (target.Def.IsBelt)
            {
                if (target.Items.Count >= target.Def.Capacity) return false;
                if (target.Items.Count > 0 && target.Items[target.Items.Count - 1].Pos < BeltSpacing) return false;
                target.Items.Add(new BeltItem { Type = type, Value = value, Pos = 0 });
                return true;
            }
            return BufAdd(target, type, value);
        }

        // ───────────────────────────── tick ─────────────────────────────

        void UpdateFactory(double dt)
        {
            if (Buildings.Count == 0) { PowerGen = PowerUse = 0; PowerRatio = 1; return; }
            float fdt = (float)dt;
            double gen = 0, use = 0;
            foreach (var b in Buildings)
            {
                if (b.Def.Power > 0) gen += b.Def.Power * CatSpeed(BuildCat.Power);
                else use -= b.Def.Power;
            }
            PowerGen = gen;
            PowerUse = use;
            PowerRatio = use <= 0 ? 1 : Math.Min(1, gen / use);

            // belts: items (front first) creep forward, then try to hop to the next cell
            foreach (var b in Buildings)
            {
                if (!b.Def.IsBelt) continue;
                float speed = BeltSpeedNow(b.Def) * fdt;
                var items = b.Items;
                for (int i = 0; i < items.Count; i++)
                {
                    var it = items[i];
                    float limit = i == 0 ? 1f : items[i - 1].Pos - BeltSpacing;
                    it.Pos = Math.Min(it.Pos + speed, Math.Max(it.Pos, limit));
                }
                if (items.Count > 0 && items[0].Pos >= 1f)
                {
                    var front = items[0];
                    int nx = b.X + b.Fx, nz = b.Z + b.Fz;
                    if (TryDeliver(At(nx, nz), b.X, b.Z, front.Type, front.Value)) items.RemoveAt(0);
                    else front.Pos = 1f;
                }
                b.Activity = items.Count > 0 ? 1 : 0;
            }

            hopperWindowTime += dt;
            foreach (var b in Buildings)
            {
                var d = b.Def;
                if (d.IsBelt) continue;
                double ratio = d.Power < 0 ? PowerRatio : 1;
                if (d.Power < 0 && PowerGen <= 0) b.Status = "No power";
                else if (ratio < 0.999) b.Status = $"Low power ({ratio * 100:0}%)";
                else b.Status = "";
                double speed = ratio * CatSpeed(d.Cat);
                if (d.Cat == BuildCat.Intake) TickIntake(b, dt, speed);
                else if (d.Cat == BuildCat.Output) TickHopper(b, dt, speed);
                else if (d.IsSplitter) TickSplitter(b, dt);
                else if (d.Cat == BuildCat.Processing) TickProcessor(b, dt, speed);
                else if (d.Cat == BuildCat.Power) b.Activity = 1;
                // push buffered output through the output ports
                if (d.Outputs.Length > 0 && (b.BufCount > 0 || b.OutCount > 0) && !d.IsSplitter && d.Cat != BuildCat.Output) PushOut(b);
            }
            if (hopperWindowTime >= 2)
            {
                double inst = hopperWindow / hopperWindowTime;
                HopperCashRate = HopperCashRate <= 0 ? inst : HopperCashRate + (inst - HopperCashRate) * 0.3;
                hopperWindow = 0;
                hopperWindowTime = 0;
            }
        }

        void PushOut(Building b)
        {
            bool proc = b.Def.Cat == BuildCat.Processing;
            var list = proc ? b.Out : b.Buf;
            foreach (var p in b.Def.Outputs)
            {
                if (list.Count == 0) return;
                var (cx, cz) = b.Cell(p.x, p.z);
                int d = (p.side + b.Rot) & 3;
                var target = At(cx + Building.DX[d], cz + Building.DZ[d]);
                if (target == null || target == b) { if (b.Status == "") b.Status = "Output needs a belt"; continue; }
                var s = list[0];
                double v = s.Count > 0 ? s.Value / s.Count : 0;
                if (TryDeliver(target, cx, cz, s.Type, v))
                {
                    if (proc) TakeFrom(b.Out, ref b.OutCount, out _, out _);
                    else BufTake(b, out _, out _);
                }
                else if (b.Status == "") b.Status = "Output blocked";
            }
        }

        static bool TakeFrom(List<ItemStack> list, ref int count, out int type, out double value)
        {
            type = -1; value = 0;
            if (list.Count == 0) return false;
            var s = list[0];
            type = s.Type;
            value = s.Count > 0 ? s.Value / s.Count : 0;
            s.Count--;
            s.Value -= value;
            count--;
            if (s.Count <= 0) list.RemoveAt(0);
            return true;
        }

        static void AddTo(List<ItemStack> list, ref int count, int type, double value, int n = 1)
        {
            count += n;
            if (list.Count > 0 && list[list.Count - 1].Type == type) { list[list.Count - 1].Count += n; list[list.Count - 1].Value += value; return; }
            list.Add(new ItemStack { Type = type, Count = n, Value = value });
        }

        void TickSplitter(Building b, double dt)
        {
            b.Activity = b.BufCount > 0 ? 1 : 0;
            if (b.BufCount <= 0) return;
            var outs = b.Def.Outputs;
            for (int k = 0; k < outs.Length; k++)
            {
                var p = outs[(b.SplitNext + k) % outs.Length];
                var (cx, cz) = b.Cell(p.x, p.z);
                int d = (p.side + b.Rot) & 3;
                var target = At(cx + Building.DX[d], cz + Building.DZ[d]);
                var s = b.Buf[0];
                double v = s.Count > 0 ? s.Value / s.Count : 0;
                if (TryDeliver(target, cx, cz, s.Type, v))
                {
                    BufTake(b, out _, out _);
                    b.SplitNext = (b.SplitNext + k + 1) % outs.Length;
                    return;
                }
            }
        }

        void TickHopper(Building b, double dt, double speed)
        {
            b.Acc += dt * b.Def.Rate * speed;
            double cash = 0;
            int n = 0;
            while (b.Acc >= 1 && b.BufCount > 0)
            {
                b.Acc -= 1;
                BufTake(b, out int type, out double value);
                cash += value * CatRate(Content.Items[type].Cat) * ValueMult * EventValueMult * b.Def.SellMult;
                n++;
            }
            if (b.BufCount == 0) b.Acc = Math.Min(b.Acc, 1);
            b.Activity = Math.Max(0, b.Activity - (float)dt * 2);
            if (n > 0)
            {
                b.Activity = 1;
                AddCash(cash);
                hopperWindow += cash;
                S.hopperItems += n;
                S.hopperCash += cash;
                OnHopperSold?.Invoke(b, cash, n);
            }
        }

        void TickIntake(Building b, double dt, double speed)
        {
            if (b.BufCount >= b.Def.Capacity) { b.Status = "Full: output blocked"; b.Activity = 0; return; }
            if (speed <= 0) { b.Activity = 0; return; }
            var d = b.Def;
            switch (d.Intake)
            {
                case "skimmer": TickSkimmer(b, dt, speed); break;
                case "dig": TickDigRig(b, dt, speed); break;
                case "pump":
                case "claw":
                {
                    b.Acc += dt * d.Rate * speed;
                    b.Activity = 0.4f;
                    while (b.Acc >= 1 && b.BufCount < d.Capacity)
                    {
                        b.Acc -= 1;
                        int idx = d.Intake == "pump" ? NearestLoose(SuctionPoint(b), d.Reach) : RichestLoose();
                        if (idx < 0) { b.Acc = 0; if (b.Status == "") b.Status = "Nothing in reach"; break; }
                        var it = Loose[idx];
                        b.LastPicked = it.Uid; b.LastPickX = it.X; b.LastPickZ = it.Z; b.PickSerial++;
                        RemoveLooseAt(idx);
                        BufAdd(b, it.Type, it.Value);
                        S.machinePicked++;
                        b.Activity = 1;
                    }
                    break;
                }
            }
        }

        const float BotSpeed = 1.6f;

        void TickSkimmer(Building b, double dt, double speed)
        {
            float step = (float)(BotSpeed * speed * dt);
            b.Activity = 1;
            if (b.Pause > 0) { b.Pause -= (float)dt; return; }
            var (dx, dz) = DockPoint(b);
            if (b.BotState == 0)
            {
                int idx = -1;
                if (b.BotTarget != 0 && looseIndex.TryGetValue(b.BotTarget, out int ti) && Loose[ti].State != LooseState.Airborne) idx = ti;
                if (idx < 0)
                {
                    idx = NearestLoose((b.BotX, b.BotZ), b.Def.Reach * 2);
                    b.BotTarget = idx >= 0 ? Loose[idx].Uid : 0;
                }
                if (idx < 0) { b.BotState = b.BotLoad.Count > 0 ? (byte)1 : (byte)0; if (b.BotLoad.Count == 0) { b.Activity = 0.2f; if (b.Status == "") b.Status = "Fountain's empty"; } return; }
                var it = Loose[idx];
                if (MoveBot(b, it.X, it.Z, step))
                {
                    b.LastPicked = it.Uid; b.LastPickX = it.X; b.LastPickZ = it.Z; b.PickSerial++;
                    RemoveLooseAt(idx);
                    b.BotLoad.Add((it.Type, it.Value));
                    S.machinePicked++;
                    b.BotTarget = 0;
                    b.Pause = (float)(1 / Math.Max(0.1, b.Def.Rate * speed));
                    if (b.BotLoad.Count >= b.Def.Capacity) b.BotState = 1;
                }
            }
            else if (b.BotState == 1)
            {
                if (MoveBot(b, dx, dz, step)) b.BotState = 2;
            }
            else
            {
                while (b.BotLoad.Count > 0 && b.BufCount < b.Def.Capacity)
                {
                    var l = b.BotLoad[b.BotLoad.Count - 1];
                    b.BotLoad.RemoveAt(b.BotLoad.Count - 1);
                    BufAdd(b, l.type, l.value);
                }
                if (b.BotLoad.Count == 0) b.BotState = 0;
            }
        }

        static bool MoveBot(Building b, float tx, float tz, float step)
        {
            float ddx = tx - b.BotX, ddz = tz - b.BotZ;
            float dist = (float)Math.Sqrt(ddx * ddx + ddz * ddz);
            if (dist <= Math.Max(0.3f, step)) { b.BotX = tx; b.BotZ = tz; return true; }
            b.BotX += ddx / dist * step;
            b.BotZ += ddz / dist * step;
            // keep the bot off the centrepiece
            float r = (float)Math.Sqrt(b.BotX * b.BotX + b.BotZ * b.BotZ);
            if (r < 0.9f && r > 0.001f) { b.BotX *= 0.9f / r; b.BotZ *= 0.9f / r; }
            return false;
        }

        int NearestLoose((float x, float z) p, float within)
        {
            int best = -1;
            float bd = within * within;
            for (int i = 0; i < Loose.Count; i++)
            {
                var it = Loose[i];
                if (it.State == LooseState.Airborne || IsFish(it.Type)) continue;
                float dx = it.X - p.x, dz = it.Z - p.z, d2 = dx * dx + dz * dz;
                if (d2 < bd) { bd = d2; best = i; }
            }
            return best;
        }

        int RichestLoose()
        {
            int best = -1;
            double bv = -1;
            for (int i = 0; i < Loose.Count; i++)
            {
                var it = Loose[i];
                if (it.State == LooseState.Airborne || IsFish(it.Type)) continue;
                if (it.Value > bv) { bv = it.Value; best = i; }
            }
            return best;
        }

        partial void TickProcessorImpl(Building b, double dt, double speed);
        void TickProcessor(Building b, double dt, double speed) => TickProcessorImpl(b, dt, speed);

        // ───────────────────────────── save / load ─────────────────────────────

        void SaveFactory(Func<int, int> slot)
        {
            S.buildings.Clear();
            S.beltItems.Clear();
            for (int i = 0; i < Buildings.Count; i++)
            {
                var b = Buildings[i];
                var sb = new SavedBuilding { id = b.Def.Id, x = b.X, z = b.Z, rot = b.Rot };
                foreach (var s in b.Buf) sb.buf.Add(new SavedStack { type = slot(s.Type), count = s.Count, value = s.Value });
                foreach (var s in b.Out) sb.buf.Add(new SavedStack { type = slot(s.Type), count = s.Count, value = s.Value });
                foreach (var l in b.BotLoad) sb.buf.Add(new SavedStack { type = slot(l.type), count = 1, value = l.value });
                S.buildings.Add(sb);
                foreach (var it in b.Items) S.beltItems.Add(new SavedBeltItem { b = i, type = slot(it.Type), pos = it.Pos, value = it.Value });
            }
        }

        void LoadFactory()
        {
            ClearFactory();
            var made = new List<Building>();
            foreach (var sb in S.buildings)
            {
                if (!Content.BuildIndex.TryGetValue(sb.id ?? "", out int di)) { made.Add(null); continue; }
                var b = new Building { Uid = ++uid, Def = Content.Buildables[di], X = sb.x, Z = sb.z, Rot = sb.rot & 3 };
                bool clash = false;
                for (int lx = 0; lx < b.Def.W && !clash; lx++)
                    for (int lz = 0; lz < b.Def.D && !clash; lz++)
                    {
                        var (cx, cz) = b.Cell(lx, lz);
                        if (grid.ContainsKey(Key(cx, cz))) clash = true;
                    }
                if (clash) { made.Add(null); continue; }
                AddBuilding(b);
                foreach (var st in sb.buf)
                {
                    int type = st.type >= 0 && st.type < S.typeIds.Count ? Content.TypeOrNone(S.typeIds[st.type]) : -1;
                    if (type < 0 || st.count <= 0) continue;
                    b.Buf.Add(new ItemStack { Type = type, Count = st.count, Value = st.value });
                    b.BufCount += st.count;
                }
                made.Add(b);
            }
            foreach (var bi in S.beltItems)
            {
                if (bi.b < 0 || bi.b >= made.Count || made[bi.b] == null) continue;
                int type = bi.type >= 0 && bi.type < S.typeIds.Count ? Content.TypeOrNone(S.typeIds[bi.type]) : -1;
                if (type < 0) continue;
                made[bi.b].Items.Add(new BeltItem { Type = type, Value = bi.value, Pos = bi.pos });
            }
            foreach (var b in made) if (b != null) b.Items.Sort((p, q) => q.Pos.CompareTo(p.Pos));
        }

        /// <summary>Re-aim a belt (dragging a belt line bends the previous piece toward the next).</summary>
        public void SetBeltRotation(Building b, int rot)
        {
            if (b == null || !b.Def.IsBelt) return;
            b.Rot = rot & 3;
        }

        public int CountBuilt(string id) { int c = 0; foreach (var b in Buildings) if (b.Def.Id == id) c++; return c; }
        public int MachinesBuilt { get { int c = 0; foreach (var b in Buildings) if (!b.Def.IsBelt) c++; return c; } }
        public int BeltsBuilt { get { int c = 0; foreach (var b in Buildings) if (b.Def.IsBelt) c++; return c; } }
    }
}
