// Buried finds: once the digging is past the loose layer, every few minutes the crust gives up
// something big (a time capsule, a strongbox, a lost suitcase) that surfaces loose in the water.
// Machines won't touch it and Chad wants it badly. You open it by picking it up, and it pays out
// one of several ways: a jackpot, a burst of coins, a relic, a swarm of wishes, Wish Tokens, a
// Lucky Penny, or a frenzy (a minute of double value, triple digging or a tossing spree).
// Also here: the steady income that jackpots and wishes scale with.
using System;

namespace WishExtractor.Core
{
    public sealed class FrenzyDef
    {
        public string Id, Name, Desc;
        public string Effect;          // "sell", "dig" or "toss" (see Sim.EventMult)
        public double Mult;
    }

    /// <summary>What came out of a find, for the presentation layer.</summary>
    public sealed class FindResult
    {
        public ItemType Find;
        public string Kind;            // "cash", "coins", "frenzy", "wishes", "relic", "tokens", "penny"
        public string Title, Detail;
        public double Cash, Tokens;
        public int Count;
        public float X, Z;
        public FrenzyDef Frenzy;
        public RelicDef Relic;
    }

    public sealed partial class Sim
    {
        public static readonly FrenzyDef[] Frenzies =
        {
            new FrenzyDef { Id = "gold", Name = "Golden Hour", Effect = "sell", Mult = 2, Desc = "Everything sells for double." },
            new FrenzyDef { Id = "quake", Name = "Crust Quake", Effect = "dig", Mult = 3, Desc = "The crust is cracking: every swing and every rig digs three times as hard." },
            new FrenzyDef { Id = "sale", Name = "Flash Sale", Effect = "toss", Mult = 3, Desc = "A flash sale upstairs: shoppers throw three times as often." },
        };

        static readonly string[] FindKinds = { "cash", "coins", "frenzy", "wishes", "relic", "tokens", "penny" };
        static readonly double[] FindWeights = { 30, 20, 20, 14, 10, 4, 1.5 };

        public FrenzyDef Frenzy { get; private set; }
        public double FrenzyRemaining { get; private set; }
        public double FrenzyLength { get; private set; }
        public double FindRateMult { get; private set; } = 1;
        public double WishValueMult { get; private set; } = 1;
        public double FrenzyTimeMult { get; private set; } = 1;
        /// <summary>Smoothed income from hoppers and the kiosk ($/s), without windfalls.</summary>
        public double SteadyIncome { get; private set; }
        /// <summary>Finds lying in the fountain right now.</summary>
        public int FindsWaiting => findsLoose;

        public event Action<LooseItem> OnFindUnearthed;
        public event Action<FindResult> OnFindOpened;
        public event Action<FrenzyDef, bool> OnFrenzyChanged;

        double findTimer = -1, steadyAcc, steadyTime, steadyElapsed;
        readonly double[] steadyBuckets = new double[30];
        int steadyBucket, findsLoose;

        public bool IsFind(int type) => Content.Items[type].Cat == ItemCat.Find;
        /// <summary>Loose things no machine (and no overflow) may take: the goldfish and buried finds.</summary>
        bool Protected(int type) => IsFish(type) || IsFind(type);

        public double FrenzyMult(string effect) => Frenzy != null && Frenzy.Effect == effect ? Frenzy.Mult : 1;

        void TrackSteady(double cash) => steadyAcc += cash;

        void ResetFinds()
        {
            findTimer = -1;
            steadyAcc = steadyTime = steadyElapsed = 0;
            Array.Clear(steadyBuckets, 0, steadyBuckets.Length);
            SteadyIncome = 0;
            if (Frenzy != null) EndFrenzy();
        }

        void UpdateFinds(double dt)
        {
            // income over the last few minutes (hoppers and the kiosk, not windfalls), in 10 s buckets:
            // a long window, so lumpy deposits and the odd coin burst barely move it
            double bucketLen = Balance.SteadyWindow / steadyBuckets.Length;
            steadyTime += dt;
            steadyElapsed = Math.Min(Balance.SteadyWindow, steadyElapsed + dt);
            if (steadyTime >= bucketLen)
            {
                steadyBucket = (steadyBucket + 1) % steadyBuckets.Length;
                steadyBuckets[steadyBucket] = 0;
                steadyTime -= bucketLen;
            }
            steadyBuckets[steadyBucket] += steadyAcc;
            steadyAcc = 0;
            double sum = 0;
            foreach (var v in steadyBuckets) sum += v;
            SteadyIncome = steadyElapsed > 1 ? sum / steadyElapsed : 0;
            if (Frenzy != null)
            {
                FrenzyRemaining -= dt;
                if (FrenzyRemaining <= 0) EndFrenzy();
            }
            if (S.maxStratum < 1 || S.mallCleared || Mall.FindTypes == null || Mall.FindTypes.Length == 0) return;
            if (findTimer < 0) findTimer = NextFindDelay();
            findTimer -= dt;
            if (findTimer > 0) return;
            findTimer = NextFindDelay();
            if (findsLoose < Balance.MaxFinds) SpawnFind();
        }

        double NextFindDelay() => RandRange(Balance.FindMin, Balance.FindMax) / FindRateMult;

        /// <summary>The crust gives something up: it surfaces somewhere in the fountain.</summary>
        public LooseItem SpawnFind()
        {
            if (Mall.FindTypes == null || Mall.FindTypes.Length == 0 || S.mallCleared) return null;
            int type = Mall.FindTypes[Rng.Next(Mall.FindTypes.Length)];
            var (x, z) = RandomLanding();
            var it = AddLoose(type, x, z, 0, null);
            OnFindUnearthed?.Invoke(it);
            return it;
        }

        /// <summary>Crack a find open where it lies (picking it up does this).</summary>
        void OpenFind(LooseItem it, int index)
        {
            RemoveLooseAt(index);
            S.findsOpened++;
            var def = Content.Items[it.Type];
            var r = new FindResult { Find = def, X = it.X, Z = it.Z };
            double income = Math.Max(0, SteadyIncome);
            double floor = Balance.FindCashFloor * Scale * (1 + S.maxStratum);
            // the first one is always a jackpot, so nobody walks away thinking finds are junk
            string kind = S.findsOpened <= 1 ? "cash" : FindKinds[Pick(FindWeights)];
            if (kind == "relic" && (Mall.Relics == null || Mall.Relics.Length == 0)) kind = "cash";
            r.Kind = kind;
            switch (kind)
            {
                case "cash":
                {
                    double cash = Math.Max(floor, income * 60 * RandRange(Balance.FindCashMin, Balance.FindCashMax));
                    AddCash(cash);
                    r.Cash = cash;
                    r.Title = "Jackpot!";
                    r.Detail = $"The {def.Name} was stuffed with cash: +{Fmt.Money(cash)}";
                    break;
                }
                case "coins":
                {
                    // the fanciest coin anybody throws here, bursting out in a shower
                    int tier = 0;
                    for (int i = 0; i < Content.CoinTiers.Count; i++) if (Content.CoinTierMinWish[i] <= Wishability) tier = i;
                    int type = Content.CoinTiers[tier];
                    int n = 18 + Rng.Next(13);
                    double total = Math.Max(floor, income * 60 * RandRange(Balance.FindCoinsMin, Balance.FindCoinsMax));
                    for (int i = 0; i < n; i++)
                    {
                        double a = Rng.NextDouble() * Math.PI * 2, d = 0.4 + Rng.NextDouble() * 1.6;
                        float x = (float)(it.X + Math.Cos(a) * d), z = (float)(it.Z + Math.Sin(a) * d);
                        float rr = (float)Math.Sqrt(x * x + z * z);
                        if (rr > Balance.LandMaxR) { x *= Balance.LandMaxR / rr; z *= Balance.LandMaxR / rr; }
                        AddLoose(type, x, z, total / n / Math.Max(1e-9, ValueMult), (it.X, 0.6f, it.Z));
                    }
                    r.Cash = total;
                    r.Count = n;
                    r.Title = "Coin burst!";
                    r.Detail = $"{n} {Content.Items[type].Name}s spill out of the {def.Name}. Grab them before Chad does.";
                    break;
                }
                case "frenzy":
                {
                    var f = Frenzies[Rng.Next(Frenzies.Length)];
                    StartFrenzy(f);
                    r.Frenzy = f;
                    r.Title = f.Name + "!";
                    r.Detail = $"{f.Desc} ({FrenzyLength:0}s)";
                    break;
                }
                case "wishes":
                {
                    int n = 4 + Rng.Next(3);
                    for (int i = 0; i < n; i++)
                    {
                        double a = i * Math.PI * 2 / n, d = 1.2;
                        SpawnWish(PickWish(), (float)(it.X + Math.Cos(a) * d), (float)(it.Z + Math.Sin(a) * d));
                    }
                    r.Count = n;
                    r.Title = "A swarm of wishes!";
                    r.Detail = $"The {def.Name} was full of old wishes. {n} of them float up. Catch them!";
                    break;
                }
                case "relic":
                {
                    var rel = FindRelic();
                    AddLoose(rel.def.ItemType, it.X, it.Z, rel.value, (it.X, 0.8f, it.Z));
                    r.Relic = rel.def;
                    r.Cash = rel.value;
                    r.Title = "A relic!";
                    r.Detail = $"{rel.def.Name} was inside the {def.Name}. It's in the water: take it to the kiosk.";
                    break;
                }
                case "tokens":
                {
                    double tokens = 10 + Rng.Next(21);
                    S.wishTokens += tokens;
                    r.Tokens = tokens;
                    r.Title = "Wish Tokens!";
                    r.Detail = $"A stash of {tokens:0} Wish Tokens, sealed in the {def.Name}.";
                    break;
                }
                case "penny":
                {
                    S.luckyPennies += 1;
                    S.lifetimeLP += 1;
                    r.Count = 1;
                    r.Title = "A Lucky Penny!";
                    r.Detail = $"The {def.Name} held a genuine Lucky Penny. Head Office will want to hear about this.";
                    break;
                }
            }
            OnFindOpened?.Invoke(r);
        }

        void StartFrenzy(FrenzyDef f)
        {
            bool dig = f.Effect == "dig" || (Frenzy != null && Frenzy.Effect == "dig");
            Frenzy = f;
            FrenzyLength = Balance.FrenzyTime * FrenzyTimeMult;
            FrenzyRemaining = FrenzyLength;
            S.frenzies++;
            if (dig) Recalc();
            OnFrenzyChanged?.Invoke(f, true);
        }

        void EndFrenzy()
        {
            var f = Frenzy;
            Frenzy = null;
            FrenzyRemaining = 0;
            if (f != null && f.Effect == "dig") Recalc();
            if (f != null) OnFrenzyChanged?.Invoke(f, false);
        }

        /// <summary>Tests and the tour: a find surfaces right now (at x, z if given).</summary>
        public LooseItem DebugSpawnFind(float? x = null, float? z = null)
        {
            var it = SpawnFind();
            if (it != null && x.HasValue && z.HasValue) { it.X = x.Value; it.Z = z.Value; }
            return it;
        }

        public void DebugFrenzy(int i) => StartFrenzy(Frenzies[Math.Max(0, Math.Min(Frenzies.Length - 1, i))]);
    }
}
