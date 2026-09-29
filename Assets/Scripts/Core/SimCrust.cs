// The finite goal: the crust. Digging (by hand or with rigs) removes scoops; the loose top layer
// comes up as coins, every deeper stratum as gunk chunks worth pennies until they're washed and
// sorted (sorting finds relics). Bare concrete clears the mall; signing the next contract pays
// Lucky Pennies for Head Office perks. Also: the processing machines, and the mall's timed event.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public sealed partial class Sim
    {
        // ───────────────────────────── scale and depth ─────────────────────────────

        /// <summary>Every price and value in this contract (the mall's scale, grown per remodel lap).</summary>
        public double Scale => Mall.ValueScale * Math.Pow(Balance.RemodelValueGrowth, Remodel);
        double CrustScale => Math.Pow(Balance.RemodelCrustGrowth, Remodel);
        public double TotalScoops => Mall.CrustScoops * CrustScale;
        public double DigMult { get; private set; } = 1;
        public double ChunkMult { get; private set; } = 1;
        public double RelicMult { get; private set; } = 1;

        public double StratumStartDug(int s) => Mall.Bounds[Math.Max(0, Math.Min(s, Mall.Bounds.Length - 1))] * CrustScale;
        public int StratumAtDug(double dug)
        {
            int s = 0;
            for (int i = 1; i < Mall.Strata.Length; i++) if (dug >= StratumStartDug(i)) s = i;
            return s;
        }
        public int Stratum => StratumAtDug(S.dug);
        public StratumDef CurStratum => Mall.Strata[Stratum];
        public bool MallCleared => S.mallCleared;

        double FracStart(int s) => s < Mall.Strata.Length ? Mall.Strata[s].StartFrac : 1.0;

        /// <summary>
        /// Visual depth 0..1. The loose layer is linear in scoops. Fitted layers each take about the same
        /// time, so inside one the depth follows the square root of the scoops (steady for a digger that
        /// keeps speeding up); unfitted (geometric) layers follow log(scoops).
        /// </summary>
        public double FracAtDug(double dug)
        {
            int s = StratumAtDug(dug);
            double lo = StratumStartDug(s), hi = StratumStartDug(s + 1);
            double local;
            if (dug >= hi) local = 1;
            else if (s == 0 || lo <= 0) local = hi > lo ? (dug - lo) / (hi - lo) : 1;
            else if (Mall.BoundFracs != null) local = Math.Sqrt(Math.Max(0, dug - lo) / (hi - lo));
            else local = Math.Log(Math.Max(dug, lo) / lo) / Math.Log(hi / lo);
            double a = FracStart(s), b = FracStart(s + 1);
            return Math.Min(1, a + (b - a) * Math.Max(0, Math.Min(1, local)));
        }
        public double DepthFrac => S.mallCleared ? 1 : FracAtDug(S.dug);
        public double DepthFeet => DepthFrac * Mall.DepthFeet;
        public double StratumProgress
        {
            get
            {
                int s = Stratum;
                double a = StratumStartDug(s), b = StratumStartDug(s + 1);
                return b > a ? Math.Max(0, Math.Min(1, (S.dug - a) / (b - a))) : 1;
            }
        }

        public event Action<int> OnStratumReached;
        public event Action OnMallCleared;
        public event Action OnPrestige;
        public event Action<RelicDef, double, bool> OnRelicFound;
        public event Action<MallDef> OnRelicSetComplete;
        public event Action<bool> OnEventChanged;
        public event Action<ActiveWish, double> OnWishCompressed;

        // ───────────────────────────── digging ─────────────────────────────

        double digAcc;

        /// <summary>Scoops of crust in one gunk chunk (Bigger Chunks makes them bigger).</summary>
        public double ChunkScoops => Balance.ChunkValue * ChunkMult;

        /// <summary>
        /// Remove crust. The loose layer comes up one item per scoop (coins, or junk that isn't worth
        /// keeping); every deeper stratum breaks into gunk chunks of ChunkScoops scoops, each carrying
        /// their worth of loot. Items go to sink(type, value), which returns false when there's no room;
        /// digging stops there (the crust only breaks as fast as its rubble can go somewhere).
        /// Returns scoops removed.
        /// </summary>
        public double DigCrust(double scoops, Func<int, double, bool> sink)
        {
            if (S.mallCleared || scoops <= 0) return 0;
            digAcc += scoops;
            double want = Math.Floor(digAcc);
            if (want <= 0) return 0;
            digAcc -= want;
            double total = TotalScoops, done = 0, cv = ChunkScoops;
            while (done < want && S.dug < total)
            {
                int s = StratumAtDug(S.dug);
                var st = Mall.Strata[s];
                if (st.Loose)
                {
                    int k = PickLoot(false);
                    double v = Mall.Items[k].Value * st.ValueMult * Scale;
                    if (v > 0 && !sink(Mall.LootTypes[k], v)) break;
                    S.dug = Math.Min(total, S.dug + 1);
                    done += 1;
                    continue;
                }
                // a chunk at a time (never a scoop at a time: late rigs break thousands of scoops a second)
                double end = s + 1 < Mall.Strata.Length ? Math.Min(total, StratumStartDug(s + 1)) : total;
                double step = Math.Min(want - done, end - S.dug);
                if (chunkAcc + step < cv)
                {
                    chunkAcc += step;
                    done += step;
                    S.dug = step >= end - S.dug ? end : S.dug + step;
                    continue;
                }
                if (!sink(Mall.GunkTypes[s], Mall.BaseEV * st.ValueMult * Scale * cv)) break;
                double need = cv - chunkAcc;
                chunkAcc = 0;
                done += need;
                S.dug = need >= end - S.dug ? end : S.dug + need;
            }
            if (done < want) digAcc = 0;
            S.scoops += done;
            int ns = StratumAtDug(S.dug);
            if (ns > S.maxStratum)
            {
                S.maxStratum = ns;
                OnStratumReached?.Invoke(ns);
            }
            if (S.dug >= total - 1e-9) ClearMall();
            return done;
        }
        double chunkAcc;

        /// <summary>Weighted pick of a loot kind; valueWeighted favours the valuable ones (sorting).</summary>
        int PickLoot(bool valueWeighted)
        {
            var items = Mall.Items;
            double t = 0;
            foreach (var it in items) t += valueWeighted ? (it.Value > 0 ? it.Weight * it.Value : 0) : it.Weight;
            double r = Rng.NextDouble() * t;
            for (int i = 0; i < items.Length; i++)
            {
                r -= valueWeighted ? (items[i].Value > 0 ? items[i].Weight * items[i].Value : 0) : items[i].Weight;
                if (r <= 0) return i;
            }
            return items.Length - 1;
        }

        /// <summary>Gunk chunks lying in the fountain or in your hands: hand digging stops at Balance.RubbleCap.</summary>
        public int RubbleInPlay
        {
            get
            {
                int n = 0;
                foreach (var l in Loose) if (Content.Items[l.Type].Cat == ItemCat.Gunk) n++;
                foreach (var s in Carried) if (s.Def.Cat == ItemCat.Gunk) n += s.Count;
                return n;
            }
        }

        /// <summary>True when the last swing broke nothing because too much rubble is lying around.</summary>
        public bool RubbleBlocked { get; private set; }

        /// <summary>A swing of the hand dig tool at (x, z): returns scoops; items land around the spot.</summary>
        public double SwingDig(float x, float z)
        {
            var tool = DigTool;
            RubbleBlocked = false;
            if (tool.DigPower <= 0) return 0;
            S.swings++;
            double scoops = tool.DigPower * DigMult * ChunkMult;
            int room = Balance.RubbleCap - RubbleInPlay;
            double done = DigCrust(scoops, (type, value) =>
            {
                if (Content.Items[type].Cat == ItemCat.Gunk && room-- <= 0) return false;
                double a = Rng.NextDouble() * Math.PI * 2, r = Rng.NextDouble() * 0.45;
                float px = x + (float)(Math.Cos(a) * r), pz = z + (float)(Math.Sin(a) * r);
                float rr = (float)Math.Sqrt(px * px + pz * pz);
                if (rr > Balance.LandMaxR + 0.4f) { px *= (Balance.LandMaxR + 0.4f) / rr; pz *= (Balance.LandMaxR + 0.4f) / rr; }
                AddLoose(type, px, pz, value, null);
                return true;
            });
            RubbleBlocked = done <= 0 && room < 0 && !S.mallCleared;
            return done;
        }

        void TickDigRig(Building b, double dt, double speed)
        {
            if (S.mallCleared) { b.Status = "Bare concrete!"; b.Activity = 0; return; }
            b.Activity = 1;
            b.Acc += dt * b.Def.Rate * speed * DigMult * ChunkMult;
            if (b.Acc < 1) return;
            double take = Math.Floor(b.Acc);
            b.Acc -= take;
            var (sx, sz) = SuctionPoint(b);
            b.LastPickX = sx; b.LastPickZ = sz; b.PickSerial++;
            // a rig only breaks off what its output can hold: a blocked line stops the digging
            if (DigCrust(take, (type, value) => BufAdd(b, type, value)) < take)
            {
                b.Status = "Full: output blocked";
                b.Acc = Math.Min(b.Acc, 1);
            }
        }

        // ───────────────────────────── processing ─────────────────────────────

        static readonly Dictionary<string, Func<ItemType, bool>> Accepts = new Dictionary<string, Func<ItemType, bool>>
        {
            ["wash"] = t => t.Cat == ItemCat.Gunk,
            ["sort"] = t => t.Cat == ItemCat.Washed,
            ["roll"] = t => t.Cat == ItemCat.Coin,
            ["bag"] = t => t.Cat == ItemCat.Roll,
            ["pallet"] = t => t.Cat == ItemCat.Bag,
            ["melt"] = t => (t.Cat == ItemCat.Coin && t.Tier >= 7) || t.Id == "ring" || t.Id == "tiara" || t.Id == "trophy" || t.Id == "goldbar",
            ["compress"] = t => false,
            ["slots"] = t => t.Cat == ItemCat.Gunk || t.Cat == ItemCat.Washed,   // the Lucky Lagoon's slot machine gambles it all
        };

        public static bool ProcessAccepts(string process, ItemType t) => process != null && Accepts.TryGetValue(process, out var f) && f(t);

        partial void TickProcessorImpl(Building b, double dt, double speed)
        {
            var d = b.Def;
            b.Activity = Math.Max(0, b.Activity - (float)dt * 1.5f);
            if (b.OutCount >= d.Capacity) { if (b.Status == "") b.Status = "Output blocked"; return; }
            // items this machine can't use pass straight through, so one line can carry a mixed stream
            int guard = 0;
            while (b.BufCount > 0 && guard++ < 64)
            {
                var head = Content.Items[b.Buf[0].Type];
                if (ProcessAccepts(d.Process, head)) break;
                BufTake(b, out int pt, out double pv);
                AddTo(b.Out, ref b.OutCount, pt, pv);
            }
            if (b.BufCount <= 0 || speed <= 0) return;
            b.Acc += dt * d.Rate * speed;
            while (b.Acc >= 1 && b.BufCount > 0 && b.OutCount < d.Capacity)
            {
                var head = Content.Items[b.Buf[0].Type];
                if (!ProcessAccepts(d.Process, head)) break;
                b.Acc -= 1;
                BufTake(b, out int type, out double value);
                b.Activity = 1;
                ProcessOne(b, type, value);
            }
            if (b.BufCount == 0) b.Acc = Math.Min(b.Acc, 1);
        }

        void ProcessOne(Building b, int type, double value)
        {
            switch (b.Def.Process)
            {
                case "wash":
                    AddTo(b.Out, ref b.OutCount, Content.Type("washed"), value);
                    S.washed++;
                    break;
                case "sort":
                {
                    int k = PickLoot(true);
                    AddTo(b.Out, ref b.OutCount, Mall.LootTypes[k], value);
                    S.sorted++;
                    double chance = Balance.RelicChance * (1 + 0.35 * Stratum) * RelicMult * EventMult("relic");
                    if (Rng.NextDouble() < chance)
                    {
                        var r = FindRelic();
                        AddTo(b.Out, ref b.OutCount, r.def.ItemType, r.value);
                    }
                    break;
                }
                case "roll": Bundle(b, type, value, Balance.RollSize, "roll", Balance.RollMult); break;
                case "bag": Bundle(b, type, value, Balance.BagSize, "bag", Balance.BagMult); break;
                case "pallet": Bundle(b, type, value, Balance.PalletSize, "pallet", Balance.PalletMult); break;
                case "melt":
                    AddTo(b.Out, ref b.OutCount, Content.Type("bar"), value * Balance.MeltMult);
                    break;
                case "slots":
                    SpinSlots(b, value);
                    break;
                default:
                    AddTo(b.Out, ref b.OutCount, type, value);
                    break;
            }
        }

        void Bundle(Building b, int type, double value, int size, string outId, double mult)
        {
            b.PartialCount++;
            b.PartialValue += value;
            if (b.PartialCount >= size)
            {
                AddTo(b.Out, ref b.OutCount, Content.Type(outId), b.PartialValue * mult);
                b.PartialCount = 0;
                b.PartialValue = 0;
                S.bundles++;
            }
        }

        /// <summary>Wishes that nobody catches drift into the nearest powered Wish Compressor.</summary>
        bool TryCompress(ActiveWish w)
        {
            foreach (var b in Buildings)
            {
                if (b.Def.Process != "compress" || b.OutCount >= b.Def.Capacity || PowerRatio < 0.2) continue;
                double v = w.Value * Balance.CompressorValue;
                AddTo(b.Out, ref b.OutCount, Content.Type("brick"), v);
                S.wishTokens += w.Tokens * 0.5;
                S.wishesCompressed++;
                b.Activity = 1;
                OnWishCompressed?.Invoke(w, v);
                return true;
            }
            return false;
        }

        // ───────────────────────────── relics ─────────────────────────────

        (RelicDef def, double value) FindRelic()
        {
            var rarity = (Rarity)Pick(Balance.RelicRarityWeight);
            var candidates = new List<RelicDef>();
            foreach (var r in Mall.Relics) if (r.Rarity == rarity) candidates.Add(r);
            if (candidates.Count == 0) candidates.AddRange(Mall.Relics);
            var fresh = candidates.FindAll(r => !relicCount.ContainsKey(r.Id));
            var def = fresh.Count > 0 && Rng.NextDouble() < 0.6 ? fresh[Rng.Next(fresh.Count)] : candidates[Rng.Next(candidates.Count)];
            double value = def.BaseValue * Balance.RelicValueScale * Scale;
            S.relicsFound++;
            if (def.Rarity == Rarity.Legendary) S.legendaryRelic = true;
            relicCount.TryGetValue(def.Id, out int c);
            relicCount[def.Id] = c + 1;
            bool first = c == 0;
            if (first) Recalc();
            OnRelicFound?.Invoke(def, value, first);
            if (first && RelicSetDone(def.MallIndex)) OnRelicSetComplete?.Invoke(Content.Malls[def.MallIndex]);
            return (def, value);
        }

        // ───────────────────────────── bare concrete & the next contract ─────────────────────────────

        void ClearMall()
        {
            if (S.mallCleared) return;
            S.dug = TotalScoops;
            S.mallCleared = true;
            if (S.mallIndex > S.maxMallCleared) S.maxMallCleared = S.mallIndex;
            if (!S.treasures.Contains(Mall.Id)) S.treasures.Add(Mall.Id);
            Recalc();
            OnMallCleared?.Invoke();
        }

        public double PrestigeReward => Mall.LuckyPennies * (1 + Remodel);

        /// <summary>Sign the next contract: Lucky Pennies now, Head Office perks kept, everything else starts over.</summary>
        public void Prestige()
        {
            if (!S.mallCleared) return;
            double lp = PrestigeReward;
            S.luckyPennies += lp;
            S.lifetimeLP += lp;
            if (IsFinalMall || InRemodel) S.remodelsDone++;
            S.mallIndex++;
            S.mallCleared = false;
            S.dug = 0;
            S.maxStratum = 0;
            S.runCash = 0;
            S.runTime = 0;
            S.cash = 0;
            for (int i = 0; i < techLevel.Length; i++) if (!Content.Techs[i].Persistent) techLevel[i] = 0;
            Carried.Clear();
            CarryUsed = 0;
            digAcc = chunkAcc = 0;
            EventActive = false;
            eventTimer = RandRange(Balance.EventMin, Balance.EventMax);
            ApplyHeadStart();
            StartRun();
            OnPrestige?.Invoke();
        }

        /// <summary>Head Office perks that act at the start of a contract.</summary>
        void ApplyHeadStart()
        {
            for (int i = 0; i < techLevel.Length; i++)
            {
                var t = Content.Techs[i];
                if (!t.Persistent || techLevel[i] <= 0) continue;
                if (t.Kind == TechKind.StartCash) S.cash += t.Value * techLevel[i] * Scale;
                if (t.Kind == TechKind.StartUnlocks)
                    foreach (var id in new[] { "unlock_hamster", "unlock_skimmer", "unlock_belts", "unlock_hopper", "carry_cup", "grab_grabber", "dig_sandshovel" })
                        if (Content.TechIndex.TryGetValue(id, out int ti) && techLevel[ti] == 0) techLevel[ti] = 1;
            }
        }

        // ───────────────────────────── the mall event ─────────────────────────────

        public bool EventActive { get; private set; }
        public double EventRemaining { get; private set; }
        double eventTimer = 600;

        public double EventMult(string effect)
        {
            if (!EventActive) return 1;
            var e = Mall.Event;
            if (e.Effect == effect) return e.Mult;
            if (e.Effect == "all" && (effect == "sell" || effect == "toss")) return e.Mult;
            return 1;
        }

        public double EventValueMult => EventMult("sell");

        void UpdateEvent(double dt)
        {
            if (EventActive)
            {
                EventRemaining -= dt;
                if (Mall.Event.Effect == "golden")
                {
                    // Jackpot Hour: gold coins rain in from the ceiling
                    goldAcc += dt * 2.5;
                    while (goldAcc >= 1)
                    {
                        goldAcc -= 1;
                        var (x, z) = RandomLanding();
                        int g = Content.Type("gold");
                        AddLoose(g, x, z, Content.Items[g].BaseValue * Scale, (x * 0.3f, 16f, z * 0.3f));
                    }
                }
                if (EventRemaining <= 0) { EventActive = false; OnEventChanged?.Invoke(false); }
                return;
            }
            if (S.tosses < 60 || S.mallCleared) return;
            eventTimer -= dt;
            if (eventTimer <= 0)
            {
                eventTimer = RandRange(Balance.EventMin, Balance.EventMax);
                EventActive = true;
                EventRemaining = Balance.EventDuration;
                OnEventChanged?.Invoke(true);
            }
        }
        double goldAcc;

        public void DebugStartEvent() { eventTimer = 0; S.tosses = Math.Max(S.tosses, 60); }

        /// <summary>Tests and the tour: jump the crust to a depth fraction.</summary>
        public void DebugSetDepth(double frac)
        {
            frac = Math.Max(0, Math.Min(1, frac));
            // invert FracAtDug by bisection (it's monotonic)
            double lo = 0, hi = TotalScoops;
            for (int i = 0; i < 60; i++) { double mid = (lo + hi) / 2; if (FracAtDug(mid) < frac) lo = mid; else hi = mid; }
            S.dug = Math.Min(TotalScoops, hi);
            int s = StratumAtDug(S.dug);
            if (s > S.maxStratum) { S.maxStratum = s; OnStratumReached?.Invoke(s); }
            if (S.dug >= TotalScoops - 1e-9) ClearMall();
        }

        public void DebugFinishMall() => DebugSetDepth(1);
    }
}
