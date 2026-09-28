// The game model. Owns the save state, the loose items lying in the fountain, what the player
// carries, the tech tree and every multiplier derived from it. No UnityEngine dependency, so
// Tools/BalanceSim can run the exact same rules.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public enum LooseState : byte { Airborne, Sinking, Resting }

    /// <summary>Something lying in (or flying into) the fountain. The view derives heights from the crust.</summary>
    public sealed class LooseItem
    {
        public int Uid;
        public int Type;
        public float X, Z;                 // where it lands / lies
        public float FromX, FromY, FromZ;  // toss origin (airborne only)
        public float Yaw;
        public LooseState State;
        public float Timer;                // seconds spent in the current state
        public double Value;               // base value (mall and stratum included)
        public WishDef Wish;               // a True Wish that rises when this toss lands (not saved)
    }

    /// <summary>A pile of one item type: in the player's hands or a machine buffer.</summary>
    public sealed class ItemStack
    {
        public int Type;
        public int Count;
        public double Value;
        public ItemType Def => Content.Items[Type];
    }

    public sealed partial class Sim
    {
        public SaveData S { get; private set; }
        public readonly Random Rng;

        public MallDef Mall { get; private set; }
        public int MallDefIndex => S.mallIndex % Content.Malls.Length;
        public int Remodel => S.mallIndex / Content.Malls.Length;
        public bool IsFinalMall => MallDefIndex == Content.Malls.Length - 1;
        public bool InRemodel => S.mallIndex >= Content.Malls.Length;

        // ── loose items ──
        public readonly List<LooseItem> Loose = new List<LooseItem>();
        readonly Dictionary<int, int> looseIndex = new Dictionary<int, int>();
        int uid;

        // ── carried ──
        public readonly List<ItemStack> Carried = new List<ItemStack>();
        public int CarryUsed { get; private set; }

        // ── derived (Recalc) ──
        int[] techLevel;
        bool[] achievementDone;
        readonly HashSet<string> wishFound = new HashSet<string>();
        readonly Dictionary<string, int> relicCount = new Dictionary<string, int>();
        public int CarryTier { get; private set; }
        public int GrabTier { get; private set; }
        public int DigTier { get; private set; }
        public double ValueMult { get; private set; }
        public double Wishability { get; private set; }
        public double TossRateMult { get; private set; }
        public double WalkSpeedMult { get; private set; }
        public double ReachBonus { get; private set; }
        public double GrabRateMult { get; private set; }
        public double WishLifeMult { get; private set; }
        public int AchievementCount { get; private set; }
        public int SetsComplete { get; private set; }
        public int UniqueWishCount => wishFound.Count;
        public int TreasureCount => S.treasures.Count;

        // ── live ──
        public double Time;
        public double EarnRate;
        double metaTimer, earnWindow, earnWindowTime;

        // ── events for the presentation layer ──
        public event Action<ItemType, int, double> OnPickup;           // type, count, base value
        public event Action<string> OnPickupFail;                      // reason line
        public event Action<double, int, string> OnDeposit;            // cash, items, receipt joke
        public event Action<string> OnDepositEmpty;                    // what the kiosk says
        public event Action<LooseItem> OnLooseAdded;
        public event Action<AchievementDef> OnAchievement;
        public event Action<ObjectiveDef, double> OnObjectiveDone;
        public event Action<TechDef> OnTechBought;
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
            techLevel = new int[Content.Techs.Length];
            achievementDone = new bool[Content.Achievements.Length];
            wishFound.Clear();
            relicCount.Clear();
            foreach (var ic in S.tech)
                if (Content.TechIndex.TryGetValue(ic.id, out int ti)) techLevel[ti] = Math.Min(ic.count, Content.Techs[ti].MaxLevel);
            foreach (var id in S.achievements)
                if (Content.AchievementIndex.TryGetValue(id, out int ai)) achievementDone[ai] = true;
            foreach (var id in S.wishes) wishFound.Add(id);
            foreach (var ic in S.relics) relicCount[ic.id] = ic.count;

            Mall = Content.Malls[MallDefIndex];
            Loose.Clear();
            looseIndex.Clear();
            ClearCrowd();
            Carried.Clear();
            CarryUsed = 0;
            int n = Math.Min(S.looseType.Count, Math.Min(S.looseX.Count, Math.Min(S.looseZ.Count, S.looseValue.Count)));
            for (int i = 0; i < n; i++)
            {
                int ti = S.looseType[i];
                int type = ti >= 0 && ti < S.typeIds.Count ? Content.TypeOrNone(S.typeIds[ti]) : -1;
                if (type < 0) continue;
                AddLoose(type, S.looseX[i], S.looseZ[i], S.looseValue[i], null, false);
            }
            foreach (var st in S.carried)
            {
                int type = st.type >= 0 && st.type < S.typeIds.Count ? Content.TypeOrNone(S.typeIds[st.type]) : -1;
                if (type >= 0 && st.count > 0) AddCarried(type, st.count, st.value);
            }
            LoadFactory();
            Recalc();
            SeedCrowd();
        }

        /// <summary>Copy the live tables back into the save object.</summary>
        public SaveData Snapshot()
        {
            S.tech.Clear();
            for (int i = 0; i < techLevel.Length; i++)
                if (techLevel[i] > 0) S.tech.Add(new IdCount(Content.Techs[i].Id, techLevel[i]));
            S.achievements.Clear();
            for (int i = 0; i < achievementDone.Length; i++)
                if (achievementDone[i]) S.achievements.Add(Content.Achievements[i].Id);
            S.wishes.Clear();
            S.wishes.AddRange(wishFound);
            S.relics.Clear();
            foreach (var kv in relicCount) S.relics.Add(new IdCount(kv.Key, kv.Value));

            var typeSlot = new Dictionary<int, int>();
            S.typeIds.Clear();
            int Slot(int type)
            {
                if (typeSlot.TryGetValue(type, out int s)) return s;
                s = S.typeIds.Count;
                S.typeIds.Add(Content.Items[type].Id);
                typeSlot[type] = s;
                return s;
            }
            S.looseType.Clear(); S.looseX.Clear(); S.looseZ.Clear(); S.looseValue.Clear();
            foreach (var it in Loose)
            {
                S.looseType.Add(Slot(it.Type));
                S.looseX.Add(it.X);
                S.looseZ.Add(it.Z);
                S.looseValue.Add(it.Value);
            }
            S.carried.Clear();
            foreach (var st in Carried) S.carried.Add(new SavedStack { type = Slot(st.Type), count = st.Count, value = st.Value });
            SaveFactory(Slot);
            return S;
        }

        /// <summary>A brand-new run in the current mall: seed the fountain with decades of loose change.</summary>
        public void StartRun()
        {
            Mall = Content.Malls[MallDefIndex];
            Loose.Clear();
            looseIndex.Clear();
            ClearCrowd();
            ClearFactory();
            SeedFountain((int)Balance.SeedCoins);
            Recalc();
            SeedCrowd();
        }

        static readonly double[] SeedWeights = { 70, 15, 10, 5 };

        void SeedFountain(int count)
        {
            for (int i = 0; i < count; i++)
            {
                int tier = Pick(SeedWeights);
                int type = Content.CoinTiers[tier];
                var (x, z) = RandomLanding();
                AddLoose(type, x, z, Content.Items[type].BaseValue * Mall.ValueScale, null, false);
            }
        }

        // ───────────────────────────── lookups ─────────────────────────────

        public int TechLevel(int i) => techLevel[i];
        public int TechLevel(string id) => Content.TechIndex.TryGetValue(id, out int i) ? techLevel[i] : 0;
        public bool AchievementDone(int i) => achievementDone[i];
        public bool WishFound(string id) => wishFound.Contains(id);
        public int RelicCount(string id) => relicCount.TryGetValue(id, out int c) ? c : 0;
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
        public int TechLevelsOwned { get { int c = 0; foreach (var l in techLevel) c += l; return c; } }

        public CarryDef CarryDef => Content.Carry[CarryTier];
        public ToolDef Grab => Content.GrabTools[GrabTier];
        public ToolDef DigTool => Content.DigTools[DigTier];
        public double CarryBonusMult { get; private set; } = 1;
        public int CarryCapacity => Math.Max(1, (int)Math.Floor(CarryDef.Capacity * (CarryTier == 0 ? 1 : CarryBonusMult) + 1e-6));
        public int CarryFree => Math.Max(0, CarryCapacity - CarryUsed);
        public float Reach => Grab.Reach + (float)ReachBonus;
        public double CarriedValue { get { double v = 0; foreach (var s in Carried) v += s.Value * CatRate(s.Def.Cat); return v * ValueMult; } }
        public int CarriedCount { get { int c = 0; foreach (var s in Carried) c += s.Count; return c; } }
        public ObjectiveDef CurrentObjective => S.objective < Content.Objectives.Length ? Content.Objectives[S.objective] : null;

        // ───────────────────────────── recalc ─────────────────────────────

        public void Recalc()
        {
            int carry = 0, grab = 0, dig = 0;
            double value = 1, wish = 0, toss = 1, walk = 1, reach = 0, grabRate = 1, wishLife = 1, carryBonus = 1;
            for (int i = 0; i < techLevel.Length; i++)
            {
                int L = techLevel[i];
                if (L <= 0) continue;
                var t = Content.Techs[i];
                switch (t.Kind)
                {
                    case TechKind.Carry: carry = Math.Max(carry, (int)t.Value); break;
                    case TechKind.Tool:
                        if (t.Target == "dig") dig = Math.Max(dig, (int)t.Value); else grab = Math.Max(grab, (int)t.Value);
                        break;
                    case TechKind.Wishability: wish += t.Value * L; break;
                    case TechKind.ValueMult: value *= Math.Pow(1 + t.Value, L); break;
                    case TechKind.TossRate: toss *= Math.Pow(1 + t.Value, L); break;
                    case TechKind.WalkSpeed: walk *= Math.Pow(1 + t.Value, L); break;
                    case TechKind.Reach: reach += t.Value * L; break;
                    case TechKind.GrabRate: grabRate *= Math.Pow(1 + t.Value, L); break;
                    case TechKind.WishLife: wishLife *= Math.Pow(1 + t.Value, L); break;
                    case TechKind.CarryBonus: carryBonus *= Math.Pow(1 + t.Value, L); break;
                }
            }
            CarryTier = Math.Min(carry, Content.Carry.Length - 1);
            GrabTier = Math.Min(grab, Content.GrabTools.Length - 1);
            DigTier = Math.Min(dig, Content.DigTools.Length - 1);

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

            ValueMult = value;
            Wishability = wish;
            TossRateMult = toss;
            WalkSpeedMult = walk;
            ReachBonus = reach;
            GrabRateMult = grabRate;
            WishLifeMult = wishLife;
            CarryBonusMult = carryBonus;
            RecalcFactory();
            OnRecalc?.Invoke();
        }

        // ───────────────────────────── core loop ─────────────────────────────

        public void Tick(double dt)
        {
            if (dt <= 0) return;
            Time += dt;
            S.playTime += dt;
            S.runTime += dt;

            UpdateLoose((float)dt);
            UpdateCrowd(dt);
            UpdateWishes(dt);
            UpdateFactory(dt);

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

        void UpdateLoose(float dt)
        {
            for (int i = 0; i < Loose.Count; i++)
            {
                var it = Loose[i];
                if (it.State == LooseState.Resting) continue;
                it.Timer += dt;
                if (it.State == LooseState.Airborne && it.Timer >= Balance.TossFlight) { it.State = LooseState.Sinking; it.Timer = 0; Landed(it); }
                else if (it.State == LooseState.Sinking && it.Timer >= Balance.SinkTime) { it.State = LooseState.Resting; it.Timer = 0; }
            }
        }

        // ───────────────────────────── loose items ─────────────────────────────

        public (float x, float z) RandomLanding()
        {
            double a = Rng.NextDouble() * Math.PI * 2;
            double r2 = Balance.LandMinR * Balance.LandMinR + Rng.NextDouble() * (Balance.LandMaxR * Balance.LandMaxR - Balance.LandMinR * Balance.LandMinR);
            double r = Math.Sqrt(r2);
            return ((float)(Math.Cos(a) * r), (float)(Math.Sin(a) * r));
        }

        /// <summary>Put an item in the fountain. from = toss origin (airborne) or null (already resting).</summary>
        public LooseItem AddLoose(int type, float x, float z, double value, (float x, float y, float z)? from, bool notify = true)
        {
            if (Loose.Count >= Balance.MaxLoose) RemoveLooseAt(0);
            var it = new LooseItem
            {
                Uid = ++uid, Type = type, X = x, Z = z, Value = value, Yaw = (float)(Rng.NextDouble() * 360),
                State = from.HasValue ? LooseState.Airborne : LooseState.Resting,
            };
            if (from.HasValue) { it.FromX = from.Value.x; it.FromY = from.Value.y; it.FromZ = from.Value.z; }
            looseIndex[it.Uid] = Loose.Count;
            Loose.Add(it);
            if (notify) OnLooseAdded?.Invoke(it);
            return it;
        }

        public LooseItem FindLoose(int itemUid) => looseIndex.TryGetValue(itemUid, out int i) ? Loose[i] : null;

        void RemoveLooseAt(int i)
        {
            var it = Loose[i];
            int last = Loose.Count - 1;
            if (i != last)
            {
                Loose[i] = Loose[last];
                looseIndex[Loose[i].Uid] = i;
            }
            Loose.RemoveAt(last);
            looseIndex.Remove(it.Uid);
        }

        // ───────────────────────────── carrying ─────────────────────────────

        public static int SlotsFor(ItemType t) => t.Heavy ? 2 : 1;

        void AddCarried(int type, int count, double value)
        {
            foreach (var s in Carried)
                if (s.Type == type) { s.Count += count; s.Value += value; CarryUsed += count * SlotsFor(Content.Items[type]); return; }
            Carried.Add(new ItemStack { Type = type, Count = count, Value = value });
            CarryUsed += count * SlotsFor(Content.Items[type]);
        }

        public bool CanCarry(int type) => CarryUsed + SlotsFor(Content.Items[type]) <= CarryCapacity;

        string FullLine() => Content.FullHandsLines[Rng.Next(Content.FullHandsLines.Length)];

        /// <summary>Pick up one loose item. False if it's gone, still airborne, or your hands are full.</summary>
        public bool Pickup(int itemUid)
        {
            if (!looseIndex.TryGetValue(itemUid, out int i)) return false;
            var it = Loose[i];
            if (it.State == LooseState.Airborne) return false;
            if (!CanCarry(it.Type)) { OnPickupFail?.Invoke(FullLine()); return false; }
            RemoveLooseAt(i);
            AddCarried(it.Type, 1, it.Value);
            S.itemsPicked++;
            OnPickup?.Invoke(Content.Items[it.Type], 1, it.Value);
            return true;
        }

        /// <summary>Pick up several at once (nets, rakes, magnets). Returns how many made it into your hands.</summary>
        public int PickupMany(IList<int> uids)
        {
            int got = 0;
            double value = 0;
            ItemType last = null;
            bool full = false;
            foreach (int u in uids)
            {
                if (!looseIndex.TryGetValue(u, out int i)) continue;
                var it = Loose[i];
                if (it.State == LooseState.Airborne) continue;
                if (!CanCarry(it.Type)) { full = true; continue; }
                RemoveLooseAt(i);
                AddCarried(it.Type, 1, it.Value);
                got++;
                value += it.Value;
                last = Content.Items[it.Type];
            }
            S.itemsPicked += got;
            if (got > 0) OnPickup?.Invoke(last, got, value);
            if (full && got == 0) OnPickupFail?.Invoke(FullLine());
            return got;
        }

        /// <summary>Fraction of an item's full value the COIN-O-MATIC pays for its processing stage.</summary>
        public static double CatRate(ItemCat c)
        {
            switch (c)
            {
                case ItemCat.Gunk: return Balance.GunkRate;
                case ItemCat.Washed: return Balance.WashedRate;
                case ItemCat.Junk: return 0;
                default: return 1;
            }
        }

        /// <summary>Feed everything you carry into the COIN-O-MATIC 3000.</summary>
        public double Deposit()
        {
            if (Carried.Count == 0)
            {
                OnDepositEmpty?.Invoke(Content.EmptyDepositLines[Rng.Next(Content.EmptyDepositLines.Length)]);
                return 0;
            }
            double cash = CarriedValue;
            int count = CarriedCount;
            Carried.Clear();
            CarryUsed = 0;
            AddCash(cash);
            S.deposits++;
            S.itemsDeposited += count;
            if (cash > S.biggestDeposit) S.biggestDeposit = cash;
            OnDeposit?.Invoke(cash, count, Content.ReceiptJokes[Rng.Next(Content.ReceiptJokes.Length)]);
            return cash;
        }

        void AddCash(double amount)
        {
            if (amount <= 0 || double.IsNaN(amount) || double.IsInfinity(amount)) return;
            S.cash += amount;
            S.runCash += amount;
            S.lifetimeCash += amount;
            earnWindow += amount;
        }

        // ───────────────────────────── tech tree ─────────────────────────────

        public double TechCost(int i) => Content.Techs[i].CostAt(techLevel[i], Mall.ValueScale);
        public bool TechMaxed(int i) => techLevel[i] >= Content.Techs[i].MaxLevel;
        public bool TechUnlocked(int i)
        {
            var t = Content.Techs[i];
            if (t.UnlockMall > MallDefIndex && S.maxMallCleared < t.UnlockMall - 1) return false;
            foreach (var r in t.Requires)
                if (!Content.TechIndex.TryGetValue(r, out int ri) || techLevel[ri] <= 0) return false;
            return true;
        }
        public bool CanAfford(int i) => Content.Techs[i].WishTokens ? S.wishTokens >= TechCost(i) : S.cash >= TechCost(i) - 1e-9;
        public bool CanBuyTech(int i) => !TechMaxed(i) && TechUnlocked(i) && CanAfford(i);

        public bool BuyTech(int i)
        {
            if (!CanBuyTech(i)) return false;
            double cost = TechCost(i);
            if (Content.Techs[i].WishTokens) S.wishTokens -= cost; else S.cash -= cost;
            techLevel[i]++;
            Recalc();
            OnTechBought?.Invoke(Content.Techs[i]);
            return true;
        }

        /// <summary>The cheapest fountain upgrade you could buy next (unlocked, not maxed), or -1.</summary>
        public int NextFountainTech()
        {
            int best = -1;
            double bestCost = double.MaxValue;
            for (int i = 0; i < techLevel.Length; i++)
            {
                var t = Content.Techs[i];
                if (t.Branch != TechBranch.Fountain || TechMaxed(i) || !TechUnlocked(i)) continue;
                double c = TechCost(i);
                if (c < bestCost) { bestCost = c; best = i; }
            }
            return best;
        }

        public int FountainUpgradesOwned
        {
            get
            {
                int c = 0;
                for (int i = 0; i < techLevel.Length; i++) if (Content.Techs[i].Branch == TechBranch.Fountain) c += techLevel[i];
                return c;
            }
        }

        // ───────────────────────────── meta ─────────────────────────────

        void CheckObjectives()
        {
            int guard = 0;
            while (guard++ < 5)
            {
                var o = CurrentObjective;
                if (o == null || !o.Check(this)) return;
                double reward = o.Reward * Mall.ValueScale;
                S.objective++;
                AddCash(reward);
                OnObjectiveDone?.Invoke(o, reward);
            }
        }

        void CheckAchievements()
        {
            for (int i = 0; i < achievementDone.Length; i++)
            {
                if (achievementDone[i]) continue;
                var a = Content.Achievements[i];
                if (!a.Check(this)) continue;
                achievementDone[i] = true;
                Recalc();
                OnAchievement?.Invoke(a);
            }
        }

        // ───────────────────────────── debug ─────────────────────────────

        public void DebugAddCash(double amount) => AddCash(amount);

        /// <summary>Tour / tests: start fresh in another mall (keeps permanent progress).</summary>
        public void DebugJumpToMall(int m)
        {
            S.mallIndex = m;
            S.mallCleared = false;
            S.dug = 0;
            S.maxStratum = 0;
            Carried.Clear();
            CarryUsed = 0;
            StartRun();
        }
        public void DebugSetTech(string id, int level)
        {
            if (!Content.TechIndex.TryGetValue(id, out int i)) return;
            techLevel[i] = Math.Max(0, Math.Min(level, Content.Techs[i].MaxLevel));
            Recalc();
        }

        // ───────────────────────────── helpers ─────────────────────────────

        public int Pick(double[] weights)
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

        public double RandRange(double a, double b) => a + Rng.NextDouble() * (b - a);
    }
}
