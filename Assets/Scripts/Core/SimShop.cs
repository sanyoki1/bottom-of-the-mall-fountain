// Purchases, prestige, run resets, offline progress and meta progression checks.
using System;

namespace WishExtractor.Core
{
    public sealed partial class Sim
    {
        // ───────────────────────────── machines ─────────────────────────────

        /// <summary>Stratum of the current mall that unlocks machine i (99 = not in this mall).</summary>
        public int MachineUnlockStratum(int i)
        {
            var m = Content.Machines[i];
            if (S.mallIndex < m.UnlockMall) return 99;
            int col = Math.Min(S.mallIndex, Content.Malls.Length - 1);
            if (m.IsCompressor) return HOLevel("patent") > 0 ? 0 : Balance.CompressorUnlock[col];
            if (m.IsMega) return S.mallIndex == m.UnlockMall ? Balance.MegaUnlockStratum : Balance.MegaUnlockStratum - 1;
            if (m.Stage == Stage.Dig && m.Tier == 1) return 0;
            if (col == 0) return m.UnlockStratum;
            return Balance.TierUnlock[Math.Min(m.Tier, 5) - 1, col];
        }

        public bool MachineUnlocked(int i) => S.maxStratum >= MachineUnlockStratum(i);

        /// <summary>Visible but locked: the next machines the player is working toward.</summary>
        public bool MachineTeased(int i)
        {
            if (MachineUnlocked(i)) return false;
            int s = MachineUnlockStratum(i);
            return s < 99 && s <= S.maxStratum + 2;
        }

        public string MachineLockText(int i)
        {
            var m = Content.Machines[i];
            if (S.mallIndex < m.UnlockMall) return $"Unlocks at {Content.Malls[m.UnlockMall].Name}";
            int s = MachineUnlockStratum(i);
            if (s >= Mall.Strata.Length) return "Not available in this mall";
            return $"Reach {Mall.Strata[s].Name} ({Fmt.Feet(Mall.Strata[s].StartFrac * Mall.DepthFeet)})";
        }

        public double MachineCost(int i, int n)
        {
            var m = Content.Machines[i];
            int k = machineCount[i];
            if (k + n > m.MaxCount) n = m.MaxCount - k;
            if (n <= 0) return double.PositiveInfinity;
            double g = m.CostGrowth;
            return m.BaseCost * CostScale * Math.Pow(g, k) * (Math.Pow(g, n) - 1) / (g - 1) * CostDiscount;
        }

        public int MachineMaxAffordable(int i)
        {
            var m = Content.Machines[i];
            int k = machineCount[i];
            if (k >= m.MaxCount) return 0;
            double g = m.CostGrowth;
            double first = m.BaseCost * CostScale * Math.Pow(g, k) * CostDiscount;
            if (S.cash < first) return 0;
            int n = (int)Math.Floor(Math.Log(S.cash * (g - 1) / first + 1) / Math.Log(g));
            n = Math.Max(1, Math.Min(n, m.MaxCount - k));
            while (n > 1 && MachineCost(i, n) > S.cash) n--;
            return n;
        }

        public bool BuyMachine(int i, int n)
        {
            if (n <= 0 || !MachineUnlocked(i)) return false;
            double cost = MachineCost(i, n);
            if (double.IsInfinity(cost) || S.cash < cost) return false;
            S.cash -= cost;
            machineCount[i] += n;
            Recalc();
            OnPurchase?.Invoke(Content.Machines[i].Id);
            return true;
        }

        // ───────────────────────────── tools ─────────────────────────────

        public double ToolCost(ToolDef t) => t.Cost * CostScale;

        public bool BuyNextTool()
        {
            var t = NextTool;
            if (t == null || S.cash < ToolCost(t)) return false;
            S.cash -= ToolCost(t);
            S.tool = t.Index;
            Recalc();
            OnPurchase?.Invoke(t.Id);
            return true;
        }

        // ───────────────────────────── upgrades ─────────────────────────────

        public bool UpgradeReqMet(int i)
        {
            var u = Content.Upgrades[i];
            if (S.mallIndex < u.ReqMall) return false;
            if (u.MallOnly && MallDefIndex != u.ReqMall) return false;
            if (u.ReqStratum >= 0 && S.mallIndex == u.ReqMall && S.maxStratum < u.ReqStratum) return false;
            if (u.ReqTool >= 0 && S.tool < u.ReqTool) return false;
            if (u.ReqMachine != null && MachineCount(u.ReqMachine) < u.ReqCount) return false;
            return true;
        }

        public bool UpgradeAvailable(int i) => !upgradeOwned[i] && UpgradeReqMet(i);

        public double UpgradeCost(int i) => Content.Upgrades[i].Cost * CostScale;

        public string UpgradeReqText(int i)
        {
            var u = Content.Upgrades[i];
            if (S.mallIndex < u.ReqMall || u.MallOnly && MallDefIndex != u.ReqMall) return $"Only sold at {Content.Malls[u.ReqMall].Name}";
            if (u.ReqStratum >= 0 && S.mallIndex == u.ReqMall && S.maxStratum < u.ReqStratum)
                return $"Reach {Mall.Strata[Math.Min(u.ReqStratum, Mall.Strata.Length - 1)].Name}";
            if (u.ReqTool >= 0 && S.tool < u.ReqTool) return $"Requires {Content.Tools[u.ReqTool].Name}";
            if (u.ReqMachine != null && MachineCount(u.ReqMachine) < u.ReqCount)
            {
                var m = Content.Machines[Content.MachineIndex[u.ReqMachine]];
                return $"Own {u.ReqCount} {m.Name}";
            }
            return "";
        }

        public bool BuyUpgrade(int i)
        {
            if (!UpgradeAvailable(i)) return false;
            var u = Content.Upgrades[i];
            double cost = UpgradeCost(i);
            if (S.cash < cost) return false;
            S.cash -= cost;
            upgradeOwned[i] = true;
            Recalc();
            OnPurchase?.Invoke(u.Id);
            return true;
        }

        // ───────────────────────────── head office ─────────────────────────────

        public double HOCost(int i) => Content.HeadOffice[i].CostAt(hoLevel[i]);
        public bool HOMaxed(int i) => hoLevel[i] >= Content.HeadOffice[i].MaxLevel;

        public bool BuyHO(int i)
        {
            if (HOMaxed(i)) return false;
            double cost = HOCost(i);
            if (S.luckyPennies < cost) return false;
            S.luckyPennies -= cost;
            hoLevel[i]++;
            var h = Content.HeadOffice[i];
            // Start-of-run perks also apply right away to the current run where it makes sense.
            if (h.Kind == HOKind.StartTool) S.tool = Math.Max(S.tool, Math.Min(hoLevel[i], Content.Tools.Length - 1));
            Recalc();
            OnPurchase?.Invoke(h.Id);
            return true;
        }

        // ───────────────────────────── prestige ─────────────────────────────

        public double PrestigeReward => Math.Round(Mall.LuckyPennies * Math.Pow(Balance.RemodelLuckyGrowth, Remodel));
        public bool IsFinalMall => S.mallIndex == Content.Malls.Length - 1;
        public bool InRemodel => S.mallIndex >= Content.Malls.Length;

        public void Prestige()
        {
            if (!S.mallCleared) return;
            double lp = PrestigeReward;
            S.luckyPennies += lp;
            S.lifetimeLP += lp;
            if (InRemodel) S.remodelsDone++;
            S.mallIndex++;
            StartRun();
            OnPrestige?.Invoke();
        }

        public double SeedMoney
        {
            get
            {
                int L = HOLevel("seed");
                return L <= 0 ? 0 : 20 * Math.Pow(15, L - 1) * CostScale;
            }
        }

        /// <summary>Reset everything that belongs to a single mall run.</summary>
        public void StartRun()
        {
            S.runCash = 0;
            S.runTime = 0;
            S.dug = 0;
            S.mallCleared = false;
            S.maxStratum = 0;
            S.hopperCount = S.hopperValue = 0;
            S.trayCount = S.trayValue = 0;
            S.pocketCount = S.pocketValue = 0;
            S.tool = Math.Min(HOLevel("toolkit"), Content.Tools.Length - 1);
            for (int i = 0; i < machineCount.Length; i++) machineCount[i] = 0;
            for (int i = 0; i < upgradeOwned.Length; i++) upgradeOwned[i] = false;
            int vet = HOLevel("veterans") * 5;
            if (vet > 0)
            {
                machineCount[Content.MachineIndex["pogo"]] = vet;
                machineCount[Content.MachineIndex["tumbler"]] = vet;
                machineCount[Content.MachineIndex["pigeons"]] = vet;
            }
            Wishes.Clear();
            Goldens.Clear();
            Rat = null;
            Buffs.Clear();
            Combo = 0;
            EventActive = false;
            EarnRate = 0;
            earnWindow = earnWindowTime = 0;
            wishAcc = relicAcc = 0;
            SetupMall();
            S.cash = SeedMoney;
            Recalc();
        }

        // ───────────────────────────── offline ─────────────────────────────

        public double OfflineCapHours => Balance.OfflineBaseHours + 2 * HOLevel("nightshift");
        public double OfflineEfficiency => Math.Min(1, Balance.OfflineBaseEff + 0.05 * HOLevel("graveyard"));

        public OfflineReport SimulateOffline(double seconds)
        {
            var rep = new OfflineReport();
            seconds = Math.Min(seconds, OfflineCapHours * 3600);
            if (seconds < 30) return rep;
            double cash0 = S.lifetimeCash, dug0 = S.totalDug, rel0 = S.relicsFound, wish0 = S.wishesCompressed, pocket0 = PocketWorth;
            OfflineMode = true;
            int steps = (int)Math.Min(4000, Math.Max(20, seconds / 2));
            double dt = seconds / steps;
            for (int i = 0; i < steps; i++) Tick(dt);
            OfflineMode = false;
            rep.Seconds = seconds;
            rep.Cash = S.lifetimeCash - cash0;
            rep.Pocket = Math.Max(0, PocketWorth - pocket0);
            rep.Dug = S.totalDug - dug0;
            rep.Relics = (int)(S.relicsFound - rel0);
            rep.Wishes = (int)(S.wishesCompressed - wish0);
            return rep;
        }

        // ───────────────────────────── objectives & achievements ─────────────────────────────

        public ObjectiveDef CurrentObjective => S.objective < Content.Objectives.Length ? Content.Objectives[S.objective] : null;

        public double ObjectiveReward(ObjectiveDef o) => Math.Max(o.RewardFlat * CostScale, RewardIncome * o.RewardSeconds);

        /// <summary>A sensible next goal once the scripted objectives run out.</summary>
        public string DynamicGoal
        {
            get
            {
                if (S.mallCleared) return IsFinalMall || InRemodel ? "Sign the next Remodel contract at Head Office" : "Sign the contract for the next mall (Prestige)";
                int s = Stratum;
                if (s + 1 < Mall.Strata.Length)
                    return $"Dig down to {Mall.Strata[s + 1].Name} ({Fmt.Num(Mall.Strata[s + 1].StartFrac * Mall.DepthFeet)} ft)";
                return $"Hit bare concrete at {Fmt.Num(Mall.DepthFeet)} ft";
            }
        }

        void CheckObjectives()
        {
            int guard = 0;
            while (S.objective < Content.Objectives.Length && guard++ < 6)
            {
                var o = Content.Objectives[S.objective];
                if (!o.Check(this)) break;
                double reward = ObjectiveReward(o);
                AddCash(reward);
                S.objective++;
                OnObjectiveDone?.Invoke(o, reward);
            }
        }

        void CheckAchievements()
        {
            bool any = false;
            for (int i = 0; i < achievementDone.Length; i++)
            {
                if (achievementDone[i]) continue;
                bool ok;
                try { ok = Content.Achievements[i].Check(this); }
                catch { ok = false; }
                if (!ok) continue;
                achievementDone[i] = true;
                any = true;
                OnAchievement?.Invoke(Content.Achievements[i]);
            }
            if (any) Recalc();
        }

        // ───────────────────────────── debug helpers (used by the auto-tour and dev keys) ─────────────────────────────

        public void DebugAddCash(double amount) => AddCash(amount);
        public void DebugSetDug(double frac)
        {
            S.dug = DugAtFrac(Math.Max(0, Math.Min(0.999999, frac)));
            int ns = StratumAtDug(S.dug);
            if (ns > S.maxStratum) S.maxStratum = ns;
            Recalc();
        }
        public void DebugSetMachine(string id, int count)
        {
            machineCount[Content.MachineIndex[id]] = count;
            Recalc();
        }
        public void DebugJumpToMall(int mallIndex)
        {
            S.mallIndex = mallIndex;
            if (S.maxMallCleared < mallIndex - 1) S.maxMallCleared = mallIndex - 1;
            StartRun();
            OnPrestige?.Invoke();
        }
        public void DebugSpawnRat()
        {
            if (Rat != null) return;
            Rat = new ActiveRat { Uid = ++uid, Life = Balance.RatLife, StartAngle = -Math.PI * 0.65, Dir = 1 };
            OnRatSpawned?.Invoke(Rat);
        }

        public void DebugFinishMall()
        {
            S.dug = TotalItems * 0.999999;
            Dig(TotalItems, false);
        }
    }
}
