// The game model. Owns the save state, derives every rate and multiplier from it, and
// advances the Dredge → Dissolve → Sort → Sell pipeline. No UnityEngine dependency.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public sealed class ActiveWish
    {
        public int Uid;
        public WishDef Def;
        public double Value;
        public double Age;
        public double Life;
    }

    public sealed class ActiveGolden
    {
        public int Uid;
        public double Age;
        public double Life;
        public double X, Z;   // position inside the basin, unit disc
    }

    public sealed class ActiveRat
    {
        public int Uid;
        public double Age;
        public double Life;
        public double StartAngle;
        public int Dir;
    }

    public sealed class Buff
    {
        public string Id;
        public string Name;
        public double Mult;
        public double Remaining;
        public double Duration;
    }

    public sealed class OfflineReport
    {
        public double Seconds;
        public double Cash;
        public double Pocket;     // sale value that piled up in the pocket (no auto-sell yet)
        public double Dug;
        public int Relics;
        public int Wishes;
    }

    public sealed partial class Sim
    {
        public SaveData S { get; private set; }
        public readonly Random Rng;

        // ── mall geometry ──
        public MallDef Mall { get; private set; }
        public int MallDefIndex => S.mallIndex % Content.Malls.Length;
        public int Remodel => S.mallIndex / Content.Malls.Length;
        public double TotalItems { get; private set; }
        public double Contract { get; private set; }
        public double CostScale { get; private set; }
        double[] stratumDug;

        // ── derived from purchases (Recalc) ──
        int[] machineCount;
        bool[] upgradeOwned;
        int[] hoLevel;
        bool[] achievementDone;
        readonly HashSet<string> wishFound = new HashSet<string>();
        readonly Dictionary<string, int> relicCount = new Dictionary<string, int>();

        public double[] MachineUnitRate { get; private set; }
        public double[] MachineMult { get; private set; }
        public readonly double[] StageRateBase = new double[3];
        public double ClickPowerBase { get; private set; }
        public double ClickPctOfDig { get; private set; }
        public double ValueMult { get; private set; }
        public double AllRateMult { get; private set; }
        public double WishFreqMult { get; private set; }
        public double WishValueMult { get; private set; }
        public double WishLifeMult { get; private set; }
        public double RelicRateMult { get; private set; }
        public double RelicValueMult { get; private set; }
        public double GoldenFreqMult { get; private set; }
        public double GoldenPowerMult { get; private set; }
        public double ComboMaxMult { get; private set; }
        public double ScrapBonus { get; private set; }
        public double CompressorChance { get; private set; }
        public double CostDiscount { get; private set; }
        public bool AutoSellOwned { get; private set; }
        public int UniqueWishCount => wishFound.Count;
        public int SetsComplete { get; private set; }
        public int AchievementCount { get; private set; }
        public int TreasureCount => S.treasures.Count;

        // ── live state (not saved) ──
        public readonly List<ActiveWish> Wishes = new List<ActiveWish>();
        public readonly List<ActiveGolden> Goldens = new List<ActiveGolden>();
        public ActiveRat Rat;
        public readonly List<Buff> Buffs = new List<Buff>();
        public double Combo;
        public bool EventActive;
        public double EventRemaining;
        public double EarnRate;            // smoothed real cash per second
        public double Time;
        public bool OfflineMode;
        double sinceClick = 99, wishAcc, relicAcc, goldenTimer, ratTimer, eventTimer, metaTimer;
        double earnWindow, earnWindowTime;
        int uid;

        // ── events for the presentation layer ──
        public event Action<double, bool> OnDig;                 // items, manual
        public event Action<double, double> OnProcessed;          // washed, sorted this tick
        public event Action<ActiveWish> OnWishSpawned;
        public event Action<ActiveWish, double, bool> OnWishCaught;   // value, first time
        public event Action<ActiveWish, double> OnWishCompressed;
        public event Action<ActiveWish> OnWishEscaped;
        public event Action<RelicDef, double, bool> OnRelicFound;     // value, first time
        public event Action<MallDef> OnRelicSetComplete;
        public event Action<int> OnStratumReached;
        public event Action OnMallCleared;
        public event Action<double, int> OnSold;                  // cash, source (0 pocket, 1 raw, 2 washed, 3 auto)
        public event Action<AchievementDef> OnAchievement;
        public event Action<ObjectiveDef, double> OnObjectiveDone;
        public event Action<ActiveGolden> OnGoldenSpawned;
        public event Action<ActiveGolden, string, string> OnGoldenClaimed;  // title, detail
        public event Action<ActiveGolden> OnGoldenMissed;
        public event Action<ActiveRat> OnRatSpawned;
        public event Action<ActiveRat> OnRatCaught;
        public event Action<bool> OnEventChanged;
        public event Action<string> OnPurchase;                   // id of what was bought
        public event Action OnPrestige;
        public event Action OnRecalc;

        public Sim(SaveData save, int seed = 0)
        {
            Rng = seed == 0 ? new Random() : new Random(seed);
            Load(save ?? new SaveData());
        }

        // ───────────────────────────── load / save ─────────────────────────────

        public void Load(SaveData save)
        {
            S = save;
            machineCount = new int[Content.Machines.Length];
            upgradeOwned = new bool[Content.Upgrades.Length];
            hoLevel = new int[Content.HeadOffice.Length];
            achievementDone = new bool[Content.Achievements.Length];
            MachineUnitRate = new double[Content.Machines.Length];
            MachineMult = new double[Content.Machines.Length];
            wishFound.Clear();
            relicCount.Clear();

            foreach (var ic in S.machines)
                if (Content.MachineIndex.TryGetValue(ic.id, out int mi)) machineCount[mi] = ic.count;
            foreach (var id in S.upgrades)
                if (Content.UpgradeIndex.TryGetValue(id, out int ui)) upgradeOwned[ui] = true;
            foreach (var ic in S.headOffice)
                if (Content.HOIndex.TryGetValue(ic.id, out int hi)) hoLevel[hi] = Math.Min(ic.count, Content.HeadOffice[hi].MaxLevel);
            foreach (var id in S.achievements)
                if (Content.AchievementIndex.TryGetValue(id, out int ai)) achievementDone[ai] = true;
            foreach (var id in S.wishes) wishFound.Add(id);
            foreach (var ic in S.relics) relicCount[ic.id] = ic.count;
            S.tool = Math.Max(0, Math.Min(S.tool, Content.Tools.Length - 1));

            Wishes.Clear(); Goldens.Clear(); Rat = null; Buffs.Clear();
            Combo = 0; EventActive = false; EventRemaining = 0;
            goldenTimer = RandRange(30, 60);
            ratTimer = RandRange(Balance.RatMin * 0.5, Balance.RatMax * 0.5);
            eventTimer = RandRange(Balance.EventMin, Balance.EventMax);
            SetupMall();
            Recalc();
        }

        /// <summary>Copy the in-memory tables back into the save object.</summary>
        public SaveData Snapshot()
        {
            S.machines.Clear();
            for (int i = 0; i < machineCount.Length; i++)
                if (machineCount[i] > 0) S.machines.Add(new IdCount(Content.Machines[i].Id, machineCount[i]));
            S.upgrades.Clear();
            for (int i = 0; i < upgradeOwned.Length; i++)
                if (upgradeOwned[i]) S.upgrades.Add(Content.Upgrades[i].Id);
            S.headOffice.Clear();
            for (int i = 0; i < hoLevel.Length; i++)
                if (hoLevel[i] > 0) S.headOffice.Add(new IdCount(Content.HeadOffice[i].Id, hoLevel[i]));
            S.achievements.Clear();
            for (int i = 0; i < achievementDone.Length; i++)
                if (achievementDone[i]) S.achievements.Add(Content.Achievements[i].Id);
            S.wishes.Clear();
            S.wishes.AddRange(wishFound);
            S.relics.Clear();
            foreach (var kv in relicCount) S.relics.Add(new IdCount(kv.Key, kv.Value));
            return S;
        }

        void SetupMall()
        {
            Mall = Content.Malls[MallDefIndex];
            CostScale = Mall.CostScale * Math.Pow(Balance.RemodelCostGrowth, Remodel);
            Contract = Mall.ContractRate * Math.Pow(Balance.RemodelCostGrowth * Balance.RemodelGenerosity, Remodel);
            int n = Mall.Strata.Length;
            stratumDug = new double[n + 1];
            // Remodel lap 1 uses its own fitted layer sizes; every later lap keeps the loose top layer
            // and grows the deeper layers progressively, so a lap starts quickly but the bottom needs real machinery.
            double[] bounds = Mall.Bounds;
            int laps = Remodel;
            if (Remodel >= 1 && Mall.RemodelBounds != null) { bounds = Mall.RemodelBounds; laps = Remodel - 1; }
            for (int i = 0; i <= n; i++)
            {
                double grow = i <= 1 ? 0 : (i - 1) / (double)(n - 1);
                stratumDug[i] = bounds[i] * Math.Pow(Balance.RemodelSizeGrowth, laps * grow);
            }
            TotalItems = stratumDug[n];
        }

        // ───────────────────────────── depth ─────────────────────────────
        // Depth in feet is piecewise: the loose layer is linear in items, every deeper layer is
        // linear in log(items), so the meter keeps moving while dig rates grow exponentially.

        double FracStart(int s) => s < Mall.Strata.Length ? Mall.Strata[s].StartFrac : 1.0;

        public double DugAtFrac(double f)
        {
            f = Math.Max(0, Math.Min(1, f));
            int s = 0;
            for (int i = 1; i < Mall.Strata.Length; i++) if (f >= Mall.Strata[i].StartFrac) s = i;
            double a = FracStart(s), b = FracStart(s + 1);
            double local = b > a ? (f - a) / (b - a) : 1;
            double lo = stratumDug[s], hi = stratumDug[s + 1];
            if (s == 0 || lo <= 0) return lo + (hi - lo) * local;
            return lo * Math.Pow(hi / lo, local);
        }

        public double FracAtDug(double dug)
        {
            int s = StratumAtDug(dug);
            double lo = stratumDug[s], hi = stratumDug[s + 1];
            double local;
            if (dug >= hi) local = 1;
            else if (s == 0 || lo <= 0) local = hi > lo ? (dug - lo) / (hi - lo) : 1;
            else local = Math.Log(Math.Max(dug, lo) / lo) / Math.Log(hi / lo);
            double a = FracStart(s), b = FracStart(s + 1);
            return Math.Min(1, a + (b - a) * Math.Max(0, Math.Min(1, local)));
        }
        public double DepthFrac => S.mallCleared ? 1 : FracAtDug(S.dug);
        public double DepthFeet => DepthFrac * Mall.DepthFeet;
        public int StratumAtDug(double dug)
        {
            int s = 0;
            for (int i = 1; i < Mall.Strata.Length; i++) if (dug >= stratumDug[i]) s = i;
            return s;
        }
        public int Stratum => StratumAtDug(S.dug);
        public StratumDef CurStratum => Mall.Strata[Stratum];
        public double StratumStartDug(int s) => stratumDug[Math.Max(0, Math.Min(s, stratumDug.Length - 1))];
        public double StratumEndDug(int s) => stratumDug[Math.Max(0, Math.Min(s + 1, stratumDug.Length - 1))];
        /// <summary>0..1 progress through the current stratum, measured in items.</summary>
        public double StratumProgress
        {
            get
            {
                int s = Stratum;
                double a = stratumDug[s], b = stratumDug[s + 1];
                return b > a ? Math.Max(0, Math.Min(1, (S.dug - a) / (b - a))) : 1;
            }
        }
        public bool MallCleared => S.mallCleared;
        public double ItemsLeft => Math.Max(0, TotalItems - S.dug);

        // ───────────────────────────── counts & lookups ─────────────────────────────

        public int MachineCount(int i) => machineCount[i];
        public int MachineCount(string id) => Content.MachineIndex.TryGetValue(id, out int i) ? machineCount[i] : 0;
        public bool HasUpgrade(int i) => upgradeOwned[i];
        public bool HasUpgrade(string id) => Content.UpgradeIndex.TryGetValue(id, out int i) && upgradeOwned[i];
        public int HOLevel(int i) => hoLevel[i];
        public int HOLevel(string id) => Content.HOIndex.TryGetValue(id, out int i) ? hoLevel[i] : 0;
        public bool AchievementDone(int i) => achievementDone[i];
        public bool WishFound(string id) => wishFound.Contains(id);
        public int RelicCount(string id) => relicCount.TryGetValue(id, out int c) ? c : 0;
        public ToolDef Tool => Content.Tools[S.tool];
        public ToolDef NextTool => S.tool + 1 < Content.Tools.Length ? Content.Tools[S.tool + 1] : null;

        public int TotalMachines
        {
            get
            {
                int t = 0;
                for (int i = 0; i < machineCount.Length; i++)
                    if (!Content.Machines[i].IsCompressor && !Content.Machines[i].IsMega) t += machineCount[i];
                return t;
            }
        }
        public int UpgradesOwnedCount { get { int c = 0; foreach (var b in upgradeOwned) if (b) c++; return c; } }
        public int HOTotalLevels { get { int c = 0; foreach (var l in hoLevel) c += l; return c; } }
        public int RelicUniqueCount => relicCount.Count;
        public int RelicsFoundInMall(int mall)
        {
            int c = 0;
            foreach (var r in Content.Malls[mall].Relics) if (relicCount.ContainsKey(r.Id)) c++;
            return c;
        }
        public bool RelicSetDone(int mall) => RelicsFoundInMall(mall) >= Content.Malls[mall].Relics.Length;
        public int WishesFoundInMall(int mall)
        {
            int c = 0;
            foreach (var w in Content.Malls[mall].Wishes) if (wishFound.Contains(w.Id)) c++;
            return c;
        }
        public int CompressorLevel => machineCount[Content.CompressorIndex];

        // ───────────────────────────── recalc ─────────────────────────────

        public void Recalc()
        {
            var machines = Content.Machines;
            var stageMult = new double[] { 1, 1, 1 };
            for (int i = 0; i < MachineMult.Length; i++) MachineMult[i] = 1;
            double allRate = 1, value = 1, click = 1, clickPct = 0, comboAdd = 0;
            double wishFreq = 1, wishVal = 1, wishLife = 1, relicRate = 1, relicVal = 1;
            double goldenFreq = 1, goldenPow = 1, scrap = 0, compEff = 0;
            bool autoSell = false;

            for (int i = 0; i < upgradeOwned.Length; i++)
            {
                if (!upgradeOwned[i]) continue;
                var u = Content.Upgrades[i];
                switch (u.Kind)
                {
                    case UpgradeKind.MachineMult:
                        if (Content.MachineIndex.TryGetValue(u.Target, out int mi)) MachineMult[mi] *= u.Value;
                        break;
                    case UpgradeKind.StageMult:
                        stageMult[(int)Enum.Parse(typeof(Stage), u.Target)] *= u.Value;
                        break;
                    case UpgradeKind.AllRateMult: allRate *= u.Value; break;
                    case UpgradeKind.ValueMult: value *= u.Value; break;
                    case UpgradeKind.ClickMult: click *= u.Value; break;
                    case UpgradeKind.ClickPctOfDig: clickPct += u.Value; break;
                    case UpgradeKind.ComboMax: comboAdd += u.Value; break;
                    case UpgradeKind.WishFreq: wishFreq *= u.Value; break;
                    case UpgradeKind.WishValue: wishVal *= u.Value; break;
                    case UpgradeKind.WishLife: wishLife *= u.Value; break;
                    case UpgradeKind.RelicRate: relicRate *= u.Value; break;
                    case UpgradeKind.RelicValue: relicVal *= u.Value; break;
                    case UpgradeKind.GoldenFreq: goldenFreq *= u.Value; break;
                    case UpgradeKind.GoldenPower: goldenPow *= u.Value; break;
                    case UpgradeKind.AutoSell: autoSell = true; break;
                    case UpgradeKind.ScrapRate: scrap += u.Value; break;
                    case UpgradeKind.CompressorEff: compEff += u.Value; break;
                }
            }

            // Head Office perks
            double discount = 1;
            for (int i = 0; i < hoLevel.Length; i++)
            {
                int L = hoLevel[i];
                if (L <= 0) continue;
                var h = Content.HeadOffice[i];
                switch (h.Kind)
                {
                    case HOKind.RateMult: allRate *= 1 + h.Value * L; break;
                    case HOKind.ValueMult: value *= 1 + h.Value * L; break;
                    case HOKind.ClickMult: click *= 1 + h.Value * L; break;
                    case HOKind.WishLife: wishLife *= 1 + h.Value * L; break;
                    case HOKind.WishValue: wishVal *= 1 + h.Value * L; break;
                    case HOKind.GoldenFreq: goldenFreq *= 1 + h.Value * L; break;
                    case HOKind.GoldenPower: goldenPow *= 1 + h.Value * L; break;
                    case HOKind.RelicRate: relicRate *= 1 + h.Value * L; break;
                    case HOKind.ComboMax: comboAdd += h.Value * L; break;
                    case HOKind.ScrapRate: scrap += h.Value * L; break;
                    case HOKind.CostDiscount: discount *= Math.Pow(1 - h.Value, L); break;
                    case HOKind.AutoSellStart: autoSell = true; break;
                    case HOKind.CompressorStart: compEff += h.Value * L; break;
                }
            }

            // Megaprojects
            for (int i = 0; i < machines.Length; i++)
            {
                var m = machines[i];
                if (!m.IsMega || machineCount[i] <= 0) continue;
                double f = 1 + m.MegaPerLevel * machineCount[i];
                if (m.MegaEffect == "rate") allRate *= f;
                else if (m.MegaEffect == "value") value *= f;
                else if (m.MegaEffect == "wish") { allRate *= f; wishVal *= f; }
            }

            // Permanent collection bonuses
            int ach = 0;
            foreach (var d in achievementDone) if (d) ach++;
            AchievementCount = ach;
            int sets = 0;
            for (int m = 0; m < Content.Malls.Length; m++) if (RelicSetDone(m)) sets++;
            SetsComplete = sets;
            value *= 1 + Balance.AchievementBonus * ach;
            value *= 1 + Balance.WishJournalBonus * wishFound.Count;
            value *= 1 + Balance.RelicSetBonus * sets;
            value *= 1 + Balance.TreasureBonus * S.treasures.Count;
            value *= Contract;

            ValueMult = value;
            AllRateMult = allRate;
            ClickPctOfDig = clickPct;
            WishFreqMult = wishFreq;
            WishValueMult = wishVal;
            WishLifeMult = wishLife;
            RelicRateMult = relicRate;
            RelicValueMult = relicVal;
            GoldenFreqMult = goldenFreq;
            GoldenPowerMult = goldenPow;
            ComboMaxMult = Balance.ComboBaseMax + comboAdd;
            ScrapBonus = scrap;
            CostDiscount = discount;
            AutoSellOwned = autoSell;
            int comp = CompressorLevel;
            CompressorChance = comp > 0
                ? Math.Min(Balance.CompressorMaxCapture, Balance.CompressorBaseCapture + Balance.CompressorCapturePerLevel * (comp - 1) + compEff)
                : 0;

            StageRateBase[0] = StageRateBase[1] = StageRateBase[2] = 0;
            for (int i = 0; i < machines.Length; i++)
            {
                var m = machines[i];
                if (m.IsCompressor || m.IsMega) { MachineUnitRate[i] = 0; continue; }
                double unit = m.BaseRate * MachineMult[i] * MilestoneMult(machineCount[i]) * stageMult[(int)m.Stage] * allRate;
                MachineUnitRate[i] = unit;
                StageRateBase[(int)m.Stage] += unit * machineCount[i];
            }
            ClickPowerBase = Tool.Power * click;
            OnRecalc?.Invoke();
        }

        public static double MilestoneMult(int count)
        {
            double mult = 1;
            foreach (int ms in Balance.Milestones) if (count >= ms) mult *= Balance.MilestoneMult; else break;
            return mult;
        }

        public static int NextMilestone(int count)
        {
            foreach (int ms in Balance.Milestones) if (count < ms) return ms;
            return -1;
        }

        // ───────────────────────────── live multipliers ─────────────────────────────

        double BuffMult(string id)
        {
            double m = 1;
            foreach (var b in Buffs) if (b.Id == id) m *= b.Mult;
            return m;
        }

        double EventMult(string effect)
        {
            if (!EventActive) return 1;
            var e = Mall.Event;
            if (e.Effect == effect) return e.Mult;
            if (e.Effect == "all" && (effect == "dig" || effect == "rate" || effect == "sell")) return e.Mult;
            return 1;
        }

        double OfflineEff => OfflineMode ? Math.Min(1, Balance.OfflineBaseEff + 0.05 * HOLevel("graveyard")) : 1;
        double RateNow => BuffMult("overdrive") * EventMult("rate") * OfflineEff;
        public double DigRateNow => S.mallCleared ? 0 : StageRateBase[0] * RateNow * EventMult("dig");
        public double WashRateNow => StageRateBase[1] * RateNow;
        public double SortRateNow => StageRateBase[2] * RateNow;
        public double ValueNow => ValueMult * BuffMult("lucky") * EventMult("sell");
        public double ClickPowerNow => ClickPowerBase * BuffMult("frenzy") * EventMult("dig") + DigRateNow * ClickPctOfDig;
        public double ComboMult => 1 + (ComboMaxMult - 1) * (Combo / Balance.ComboCap);
        public double Combo01 => Combo / Balance.ComboCap;
        public double WishRateNow => WishFreqMult * EventMult("wish") / Balance.WishBaseInterval;
        public double RelicRateNow => RelicRateMult * EventMult("relic") / Balance.RelicBaseInterval;
        public double CurrentEV => Mall.BaseEV * CurStratum.ValueMult;
        public bool AutoSell => AutoSellOwned && S.autoSellOn;

        /// <summary>Steady-state automatic income (no clicks, no bonuses) — used to scale wishes and relics.</summary>
        public double AutoIncome
        {
            get
            {
                if (S.mallCleared) return 0;
                double ev = CurrentEV * ValueMult;
                if (CurStratum.Loose) return StageRateBase[0] * ev;
                return Math.Min(StageRateBase[0], Math.Min(StageRateBase[1], StageRateBase[2])) * ev;
            }
        }

        /// <summary>Income estimate for sizing one-off rewards: automation plus one click per second.
        /// Deliberately excludes realised earnings so windfalls can't inflate the next windfall.</summary>
        public double RewardIncome => AutoIncome + (S.mallCleared ? 0 : ClickPowerBase * CurrentEV * ValueMult);

        // ───────────────────────────── core loop ─────────────────────────────

        public void Tick(double dt)
        {
            if (dt <= 0) return;
            Time += dt;
            S.playTime += dt;
            S.runTime += dt;

            UpdateBuffs(dt);
            sinceClick += dt;
            if (sinceClick > Balance.ComboDecayDelay && Combo > 0)
                Combo = Math.Max(0, Combo - dt * Balance.ComboCap * 0.9);

            double digNow = DigRateNow;
            if (digNow > 0) Dig(digNow * dt, false);

            double washed = 0, sorted = 0;
            double wr = WashRateNow * dt;
            if (S.hopperCount > 0 && wr > 0)
            {
                washed = Math.Min(wr, S.hopperCount);
                Move(ref S.hopperCount, ref S.hopperValue, ref S.trayCount, ref S.trayValue, washed);
            }
            double sr = SortRateNow * dt;
            if (S.trayCount > 0 && sr > 0)
            {
                sorted = Math.Min(sr, S.trayCount);
                Move(ref S.trayCount, ref S.trayValue, ref S.pocketCount, ref S.pocketValue, sorted);
            }
            if (washed > 0 || sorted > 0) OnProcessed?.Invoke(washed, sorted);
            if (AutoSell && S.pocketValue > 0) SellPocket(true);

            UpdateWishes(dt, washed > 0);
            UpdateRelics(dt, sorted > 0);
            if (!OfflineMode)
            {
                UpdateGoldens(dt);
                UpdateRat(dt);
                UpdateMallEvent(dt);
            }

            earnWindowTime += dt;
            if (earnWindowTime >= 1)
            {
                double inst = earnWindow / earnWindowTime;
                EarnRate = EarnRate <= 0 ? inst : EarnRate + (inst - EarnRate) * 0.25;
                earnWindow = 0;
                earnWindowTime = 0;
            }

            metaTimer += dt;
            if (metaTimer >= 0.5)
            {
                metaTimer = 0;
                CheckObjectives();
                CheckAchievements();
            }
        }

        static void Move(ref double fromCount, ref double fromValue, ref double toCount, ref double toValue, double amount)
        {
            if (amount >= fromCount * (1 - 1e-12))
            {
                toCount += fromCount;
                toValue += fromValue;
                fromCount = 0;
                fromValue = 0;
                return;
            }
            double v = fromValue * (amount / fromCount);
            fromCount -= amount;
            fromValue -= v;
            toCount += amount;
            toValue += v;
        }

        /// <summary>Remove crust from the basin. Loose layers go straight to the pocket, sticky ones to the hopper.</summary>
        public double Dig(double amount, bool manual)
        {
            if (S.mallCleared || amount <= 0) return 0;
            double done = 0;
            int guard = 0;
            while (amount > 0 && guard++ < 12)
            {
                int s = StratumAtDug(S.dug);
                double end = stratumDug[s + 1];
                double room = end - S.dug;
                if (room <= 0)
                {
                    if (s + 1 >= Mall.Strata.Length) break;
                    S.dug = end;
                    continue;
                }
                double chunk = Math.Min(amount, room);
                double val = chunk * Mall.BaseEV * Mall.Strata[s].ValueMult;
                if (Mall.Strata[s].Loose) { S.pocketCount += chunk; S.pocketValue += val; }
                else { S.hopperCount += chunk; S.hopperValue += val; }
                amount -= chunk;
                done += chunk;
                if (chunk >= room) S.dug = end; else S.dug += chunk;
            }
            if (done <= 0) return 0;
            S.totalDug += done;

            int ns = StratumAtDug(S.dug);
            if (ns > S.maxStratum)
            {
                S.maxStratum = ns;
                Recalc();
                OnStratumReached?.Invoke(ns);
            }
            if (S.dug >= TotalItems * (1 - 1e-12)) ClearMall();
            OnDig?.Invoke(done, manual);
            return done;
        }

        void ClearMall()
        {
            if (S.mallCleared) return;
            S.dug = TotalItems;
            S.mallCleared = true;
            if (S.mallIndex > S.maxMallCleared) S.maxMallCleared = S.mallIndex;
            if (!S.treasures.Contains(Mall.Id)) S.treasures.Add(Mall.Id);
            Recalc();
            OnMallCleared?.Invoke();
        }

        public double Click()
        {
            S.clicks++;
            Combo = Math.Min(Balance.ComboCap, Combo + 1);
            sinceClick = 0;
            if (Combo >= Balance.ComboCap) S.comboFilled = true;
            if (ComboMult > S.bestCombo) S.bestCombo = ComboMult;
            return Dig(ClickPowerNow * ComboMult, true);
        }

        void AddCash(double amount)
        {
            if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount)) return;
            S.cash += amount;
            S.runCash += amount;
            S.lifetimeCash += amount;
            earnWindow += amount;
        }

        public double SellPocket(bool auto = false)
        {
            if (S.pocketValue <= 0) { S.pocketCount = 0; return 0; }
            double amt = S.pocketValue * ValueNow;
            S.pocketValue = 0;
            S.pocketCount = 0;
            AddCash(amt);
            S.sales++;
            if (amt > S.biggestSale) S.biggestSale = amt;
            OnSold?.Invoke(amt, auto ? 3 : 0);
            return amt;
        }

        public double RawSellRate => Math.Min(0.9, Balance.RawRate + ScrapBonus);
        public double WashedSellRate => Math.Min(0.95, Balance.WashedRate + ScrapBonus);
        public double PocketWorth => S.pocketValue * ValueNow;
        public double HopperWorth => S.hopperValue * ValueNow * RawSellRate;
        public double TrayWorth => S.trayValue * ValueNow * WashedSellRate;

        public double DumpHopper()
        {
            if (S.hopperValue <= 0) return 0;
            double amt = HopperWorth;
            S.hopperCount = 0;
            S.hopperValue = 0;
            AddCash(amt);
            OnSold?.Invoke(amt, 1);
            return amt;
        }

        public double DumpTray()
        {
            if (S.trayValue <= 0) return 0;
            double amt = TrayWorth;
            S.trayCount = 0;
            S.trayValue = 0;
            AddCash(amt);
            OnSold?.Invoke(amt, 2);
            return amt;
        }

        /// <summary>Instant processing burst when a machine is whacked (≈ seconds of that stage's output).</summary>
        public void Whack(Stage stage, double seconds)
        {
            if (stage == Stage.Dig) { Dig(DigRateNow * seconds, true); return; }
            if (stage == Stage.Wash && S.hopperCount > 0)
            {
                double w = Math.Min(WashRateNow * seconds, S.hopperCount);
                Move(ref S.hopperCount, ref S.hopperValue, ref S.trayCount, ref S.trayValue, w);
                OnProcessed?.Invoke(w, 0);
            }
            if (stage == Stage.Sort && S.trayCount > 0)
            {
                double s = Math.Min(SortRateNow * seconds, S.trayCount);
                Move(ref S.trayCount, ref S.trayValue, ref S.pocketCount, ref S.pocketValue, s);
                OnProcessed?.Invoke(0, s);
                if (AutoSell) SellPocket(true);
            }
        }

        // ───────────────────────────── buffs ─────────────────────────────

        void UpdateBuffs(double dt)
        {
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buffs[i].Remaining -= dt;
                if (Buffs[i].Remaining <= 0) Buffs.RemoveAt(i);
            }
        }

        void AddBuff(string id, string name, double mult, double duration)
        {
            foreach (var b in Buffs)
                if (b.Id == id) { b.Mult = Math.Max(b.Mult, mult); b.Remaining = Math.Max(b.Remaining, duration); b.Duration = Math.Max(b.Duration, duration); return; }
            Buffs.Add(new Buff { Id = id, Name = name, Mult = mult, Remaining = duration, Duration = duration });
        }

        // ───────────────────────────── wishes ─────────────────────────────

        void UpdateWishes(double dt, bool washing)
        {
            if (washing) wishAcc += dt * WishRateNow;
            int spawnGuard = 0;
            while (wishAcc >= 1 && spawnGuard++ < 20)
            {
                wishAcc -= 1;
                var w = MakeWish();
                if (OfflineMode) { ResolveUncaught(w); continue; }
                if (Wishes.Count >= Balance.MaxActiveWishes) { ResolveUncaught(w); continue; }
                Wishes.Add(w);
                OnWishSpawned?.Invoke(w);
            }
            if (wishAcc > 5) wishAcc = 5;

            for (int i = Wishes.Count - 1; i >= 0; i--)
            {
                var w = Wishes[i];
                w.Age += dt;
                if (w.Age >= w.Life)
                {
                    Wishes.RemoveAt(i);
                    ResolveUncaught(w);
                }
            }
        }

        ActiveWish MakeWish()
        {
            S.wishesSeen++;
            var rarity = (Rarity)Pick(Balance.WishRarityWeight);
            var pool = Mall.Wishes;
            var candidates = new List<WishDef>();
            foreach (var w in pool) if (w.Rarity == rarity) candidates.Add(w);
            if (candidates.Count == 0) candidates.AddRange(pool);
            var fresh = candidates.FindAll(w => !wishFound.Contains(w.Id));
            WishDef def = fresh.Count > 0 && Rng.NextDouble() < Balance.UndiscoveredBias
                ? fresh[Rng.Next(fresh.Count)]
                : candidates[Rng.Next(candidates.Count)];
            return new ActiveWish { Uid = ++uid, Def = def, Value = WishValue(def), Life = Balance.WishLife * WishLifeMult };
        }

        public double WishValue(WishDef def)
        {
            double baseV = def.BaseValue * ValueMult;
            double incomeV = AutoIncome * Balance.WishIncomeSeconds[(int)def.Rarity];
            return (baseV + incomeV) * WishValueMult * EventMult("sell") * BuffMult("lucky");
        }

        void ResolveUncaught(ActiveWish w)
        {
            if (CompressorChance > 0 && Rng.NextDouble() < CompressorChance)
            {
                double v = w.Value * Balance.CompressorValue;
                AddCash(v);
                S.wishesCompressed++;
                OnWishCompressed?.Invoke(w, v);
            }
            else OnWishEscaped?.Invoke(w);
        }

        public bool CatchWish(int wishUid)
        {
            int idx = Wishes.FindIndex(w => w.Uid == wishUid);
            if (idx < 0) return false;
            var w = Wishes[idx];
            Wishes.RemoveAt(idx);
            AddCash(w.Value);
            S.wishesCaught++;
            if (w.Def.Rarity == Rarity.Legendary) S.legendaryWish = true;
            bool first = wishFound.Add(w.Def.Id);
            if (first) Recalc();
            OnWishCaught?.Invoke(w, w.Value, first);
            return true;
        }

        /// <summary>Spawn a burst of wishes immediately (golden penny effect).</summary>
        public void WishStorm(int count)
        {
            for (int i = 0; i < count; i++)
            {
                var w = MakeWish();
                if (OfflineMode) { ResolveUncaught(w); continue; }
                Wishes.Add(w);
                OnWishSpawned?.Invoke(w);
            }
        }

        // ───────────────────────────── relics ─────────────────────────────

        void UpdateRelics(double dt, bool sorting)
        {
            if (!sorting) return;
            relicAcc += dt * RelicRateNow;
            int guard = 0;
            while (relicAcc >= 1 && guard++ < 10)
            {
                relicAcc -= 1;
                FindRelic(false);
            }
            if (relicAcc > 3) relicAcc = 3;
        }

        static readonly double[] BoostedRelicWeights = { 25, 35, 25, 12, 3 };

        public RelicDef FindRelic(bool boosted)
        {
            var rarity = (Rarity)Pick(boosted ? BoostedRelicWeights : Balance.RelicRarityWeight);
            var candidates = new List<RelicDef>();
            foreach (var r in Mall.Relics) if (r.Rarity == rarity) candidates.Add(r);
            if (candidates.Count == 0) candidates.AddRange(Mall.Relics);
            var fresh = candidates.FindAll(r => !relicCount.ContainsKey(r.Id));
            var def = fresh.Count > 0 && Rng.NextDouble() < 0.5 ? fresh[Rng.Next(fresh.Count)] : candidates[Rng.Next(candidates.Count)];
            double value = RelicValue(def);
            AddCash(value);
            S.relicsFound++;
            if (def.Rarity == Rarity.Legendary) S.legendaryRelic = true;
            relicCount.TryGetValue(def.Id, out int c);
            relicCount[def.Id] = c + 1;
            bool first = c == 0;
            OnRelicFound?.Invoke(def, value, first);
            if (first)
            {
                // A first find is the only way a set can go from incomplete to complete.
                Recalc();
                if (RelicSetDone(def.MallIndex)) OnRelicSetComplete?.Invoke(Content.Malls[def.MallIndex]);
            }
            return def;
        }

        public double RelicValue(RelicDef def)
        {
            double baseV = def.BaseValue * ValueMult;
            double incomeV = AutoIncome * Balance.RelicIncomeSeconds[(int)def.Rarity];
            return (baseV + incomeV) * RelicValueMult * EventMult("sell") * BuffMult("lucky");
        }

        // ───────────────────────────── golden pennies, rats, events ─────────────────────────────

        void UpdateGoldens(double dt)
        {
            bool jackpot = EventActive && Mall.Event.Effect == "golden";
            goldenTimer -= dt * GoldenFreqMult * (jackpot ? 12 : 1);
            if (goldenTimer <= 0)
            {
                goldenTimer = RandRange(Balance.GoldenMin, Balance.GoldenMax);
                if (Goldens.Count < (jackpot ? 5 : 1)) SpawnGolden();
            }
            for (int i = Goldens.Count - 1; i >= 0; i--)
            {
                var g = Goldens[i];
                g.Age += dt;
                if (g.Age >= g.Life) { Goldens.RemoveAt(i); OnGoldenMissed?.Invoke(g); }
            }
        }

        public void SpawnGolden()
        {
            double a = Rng.NextDouble() * Math.PI * 2, r = Math.Sqrt(Rng.NextDouble()) * 0.8;
            var g = new ActiveGolden { Uid = ++uid, Life = Balance.GoldenLife, X = Math.Cos(a) * r, Z = Math.Sin(a) * r };
            Goldens.Add(g);
            OnGoldenSpawned?.Invoke(g);
        }

        static readonly double[] GoldenWeights = { 30, 28, 15, 12, 15 };

        public bool ClaimGolden(int goldenUid)
        {
            int idx = Goldens.FindIndex(g => g.Uid == goldenUid);
            if (idx < 0) return false;
            var g = Goldens[idx];
            Goldens.RemoveAt(idx);
            S.goldenClicked++;
            double p = GoldenPowerMult;
            string title, detail;
            switch (Pick(GoldenWeights))
            {
                case 0:
                    AddBuff("lucky", "Lucky Streak", 1 + 6 * p, 30);
                    title = "Lucky Streak!";
                    detail = $"Everything sells for ×{Fmt.Num(1 + 6 * p)} for 30 seconds";
                    break;
                case 1:
                    double inc = RewardIncome;
                    double gain = (Math.Min(S.cash * 0.2, inc * 900) + inc * 60 + ValueMult) * p;
                    AddCash(gain);
                    title = "Change Avalanche!";
                    detail = $"+{Fmt.Money(gain)}";
                    break;
                case 2:
                    AddBuff("frenzy", "Dig Frenzy", 15 * p, 15);
                    title = "Dig Frenzy!";
                    detail = $"Clicks dig ×{Fmt.Num(15 * p)} for 15 seconds";
                    break;
                case 3:
                    WishStorm(5 + (int)Math.Round(2 * p));
                    title = "Wish Storm!";
                    detail = "A flurry of wishes bursts from the crust";
                    break;
                default:
                    AddBuff("overdrive", "Overdrive", 1 + 2 * p, 30);
                    title = "Machine Overdrive!";
                    detail = $"Every machine ×{Fmt.Num(1 + 2 * p)} for 30 seconds";
                    break;
            }
            OnGoldenClaimed?.Invoke(g, title, detail);
            return true;
        }

        void UpdateRat(double dt)
        {
            if (Rat != null)
            {
                Rat.Age += dt;
                if (Rat.Age >= Rat.Life) Rat = null;
                return;
            }
            if (S.maxStratum < 1 || S.mallCleared) return;
            ratTimer -= dt;
            if (ratTimer <= 0)
            {
                ratTimer = RandRange(Balance.RatMin, Balance.RatMax);
                Rat = new ActiveRat { Uid = ++uid, Life = Balance.RatLife, StartAngle = Rng.NextDouble() * Math.PI * 2, Dir = Rng.Next(2) == 0 ? -1 : 1 };
                OnRatSpawned?.Invoke(Rat);
            }
        }

        public bool CatchRat()
        {
            if (Rat == null) return false;
            var r = Rat;
            Rat = null;
            S.ratsCaught++;
            OnRatCaught?.Invoke(r);
            FindRelic(true);
            return true;
        }

        void UpdateMallEvent(double dt)
        {
            if (EventActive)
            {
                EventRemaining -= dt;
                if (EventRemaining <= 0) { EventActive = false; OnEventChanged?.Invoke(false); }
                return;
            }
            if (S.maxStratum < 1 || S.mallCleared) return;
            eventTimer -= dt;
            if (eventTimer <= 0)
            {
                eventTimer = RandRange(Balance.EventMin, Balance.EventMax);
                EventActive = true;
                EventRemaining = Balance.EventDuration;
                OnEventChanged?.Invoke(true);
            }
        }

        public double EventCountdown => eventTimer;

        // ───────────────────────────── helpers ─────────────────────────────

        int Pick(double[] weights)
        {
            double total = 0;
            foreach (var w in weights) total += w;
            double r = Rng.NextDouble() * total;
            for (int i = 0; i < weights.Length; i++)
            {
                r -= weights[i];
                if (r <= 0) return i;
            }
            return weights.Length - 1;
        }

        double RandRange(double a, double b) => a + Rng.NextDouble() * (b - a);
    }
}
