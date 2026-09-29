// The four mall-only machines, one per late mall. Their techs are MallOnly, so each exists only in its own
// mall (and that mall's remodels) and nothing stacks across the game:
// - Galleria Aurelia's Champagne Cork Cannon blasts whole slabs of crust into the water, already rinsed;
//   pumps and claws collect them straight into sorters (no rinse tumbler, the usual bottleneck).
// - Skyport's Baggage Claim Carousel sends cargo drones to empty every rim intake that has no line behind
//   it, so the rim holds intakes only and processing moves to the back of the hall.
// - The Lucky Lagoon's Slot-Machine Sorter gambles raw gunk straight into sorted loot (or nothing);
//   a jackpot sprays tokens across the fountain.
// - Eternity Plaza's Old Well grants the wishes nobody catches: that much crust simply stops existing.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public enum SlotResult { Lose, Cherries, Bar, Sevens, Jackpot }

    public sealed partial class Sim
    {
        // derived from the machines' levelled techs in Recalc
        public int CannonBonusChunks { get; private set; }
        public int DroneBonus { get; private set; }
        public double SlotLoseCut { get; private set; }
        public double WellMult { get; private set; } = 1;

        public event Action<Building, int> OnCannonBlast;                  // the cannon, chunks thrown
        public event Action<Building> OnDroneUnloaded;                     // a drone emptied its cargo into this carousel
        public event Action<Building, double> OnJackpot;                   // the slot machine, value sprayed
        public event Action<Building, SlotResult> OnSlotSpin;              // every spin (sounds rate-limit themselves)
        public event Action<Building, ActiveWish, double> OnWellGranted;   // the well, the wish, scoops dissolved

        int rinsedType = -2, jackpotType = -2;
        int RinsedType => rinsedType >= -1 ? rinsedType : (rinsedType = Content.TypeOrNone("rinsed"));
        int JackpotType => jackpotType >= -1 ? jackpotType : (jackpotType = Content.TypeOrNone("jackpot"));

        // ───────────────────────────── Galleria Aurelia: the Champagne Cork Cannon ─────────────────────────────

        /// <summary>Where a cannon's cork lands: inside the basin, straight out from the cannon.</summary>
        public (float x, float z) CannonImpact(Building b)
        {
            var (cx, cz) = b.Center;
            float len = Math.Max(0.01f, (float)Math.Sqrt(cx * cx + cz * cz));
            return (cx / len * Balance.CannonImpactR, cz / len * Balance.CannonImpactR);
        }

        /// <summary>Rubble (gunk or rinsed chunks) lying within r of a point; coins don't count.</summary>
        int RubbleNear(float x, float z, float r)
        {
            int n = 0;
            float r2 = r * r;
            foreach (var it in Loose)
            {
                var cat = Content.Items[it.Type].Cat;
                if (cat != ItemCat.Gunk && cat != ItemCat.Washed) continue;
                float dx = it.X - x, dz = it.Z - z;
                if (dx * dx + dz * dz < r2) n++;
            }
            return n;
        }

        void TickCannon(Building b, double dt, double speed)
        {
            b.Activity = Math.Max(0, b.Activity - (float)dt * 1.2f);
            if (S.mallCleared) { b.Status = "Bare concrete!"; return; }
            if (speed <= 0) return;
            b.Acc = Math.Min(1.5, b.Acc + dt * b.Def.Rate * speed);
            if (b.Acc < 1) return;
            var (ix, iz) = CannonImpact(b);
            // the sommelier won't pour into a dirty glass (and the fountain only holds so much)
            int hold = Math.Max(Balance.CannonHoldCount, 3 * (Balance.CannonChunks + CannonBonusChunks));
            if (Loose.Count > Balance.MaxLoose - 300 || RubbleNear(ix, iz, Balance.CannonHoldR) >= hold)
            {
                b.Status = "Holding fire: the splash zone is full of rubble";
                return;
            }
            b.Acc -= 1;
            CannonBlast(b, ix, iz);
        }

        /// <summary>Pop! A slab of chunks (coins in the loose layer) flies from the cannon and lands around the impact point, rinsed.</summary>
        void CannonBlast(Building b, float ix, float iz)
        {
            int want = Balance.CannonChunks + CannonBonusChunks, made = 0;
            var (mx, mz) = b.Center;
            DigCrust(want * ChunkScoops + 1, (type, value) =>
            {
                if (made >= want) return false;
                double a = Rng.NextDouble() * Math.PI * 2, r = Math.Sqrt(Rng.NextDouble()) * Balance.CannonSplash;
                float px = ix + (float)(Math.Cos(a) * r), pz = iz + (float)(Math.Sin(a) * r);
                float rr = (float)Math.Sqrt(px * px + pz * pz);
                if (rr > Balance.LandMaxR) { px *= Balance.LandMaxR / rr; pz *= Balance.LandMaxR / rr; }
                else if (rr < Balance.LandMinR && rr > 0.01f) { px *= Balance.LandMinR / rr; pz *= Balance.LandMinR / rr; }
                int t = Content.Items[type].Cat == ItemCat.Gunk && RinsedType >= 0 ? RinsedType : type;
                AddLoose(t, px, pz, value, (mx, 2.1f, mz));
                made++;
                return true;
            });
            if (made == 0) return;
            b.Activity = 1;
            b.LastPickX = ix; b.LastPickZ = iz; b.PickSerial++;
            S.cannonBlasts++;
            OnCannonBlast?.Invoke(b, made);
        }

        // ───────────────────────────── Skyport: the Baggage Claim Carousel ─────────────────────────────

        int carousels;

        /// <summary>Does anything take this building's output (a belt or machine at one of its output ports)?</summary>
        public bool HasLine(Building b)
        {
            foreach (var p in b.Def.Outputs)
            {
                var (cx, cz) = b.Cell(p.x, p.z);
                int d = (p.side + b.Rot) & 3;
                var t = At(cx + Building.DX[d], cz + Building.DZ[d]);
                if (t != null && t != b && AcceptsFrom(t, cx, cz)) return true;
            }
            return false;
        }

        /// <summary>Rim intakes a drone may empty: they have a buffer and nothing behind them.</summary>
        public bool DroneServes(Building b) =>
            b.Def.Cat == BuildCat.Intake && b.Def.Outputs.Length > 0 && !HasLine(b);

        static bool FlyTo(Drone d, float x, float z, float step)
        {
            float dx = x - d.X, dz = z - d.Z, dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist <= Math.Max(0.05f, step)) { d.X = x; d.Z = z; return true; }
            d.X += dx / dist * step;
            d.Z += dz / dist * step;
            return false;
        }

        Building PickDroneTarget(Building home, Drone self)
        {
            Building best = null;
            double bestScore = 0;
            var (hx, hz) = home.Center;
            foreach (var t in Buildings)
            {
                if (t.BufCount <= 0 || !DroneServes(t)) continue;
                // leave loads another drone is already on its way to collect
                int claimed = 0;
                foreach (var c in Buildings)
                    if (c.Def.IsCarousel)
                        foreach (var o in c.Drones)
                            if (o != self && o.Target == t.Uid && (o.State == DroneState.Outbound || o.State == DroneState.Loading)) claimed += Balance.DroneCapacity;
                int avail = t.BufCount - claimed;
                if (avail <= 0) continue;
                var (tx, tz) = t.Center;
                double dist = Math.Sqrt((tx - hx) * (tx - hx) + (tz - hz) * (tz - hz));
                double score = Math.Min(avail, Balance.DroneCapacity) / (dist + 8);
                if (score > bestScore) { bestScore = score; best = t; }
            }
            return best;
        }

        void TickCarousel(Building b, double dt, double speed)
        {
            int want = Balance.CarouselDrones + DroneBonus;
            var (hx, hz) = b.Center;
            while (b.Drones.Count < want) b.Drones.Add(new Drone { X = hx, Z = hz });
            float fdt = (float)dt, step = (float)(Balance.DroneSpeed * speed) * fdt;
            bool flying = false;
            if (speed <= 0) { b.Activity = 0; return; }
            foreach (var d in b.Drones)
            {
                switch (d.State)
                {
                    case DroneState.Docked:
                    {
                        d.X = hx; d.Z = hz;
                        if (b.BufCount >= b.Def.Capacity) break;
                        var t = PickDroneTarget(b, d);
                        if (t != null) { d.Target = t.Uid; d.State = DroneState.Outbound; }
                        break;
                    }
                    case DroneState.Outbound:
                    {
                        flying = true;
                        var t = FindBuilding(d.Target);
                        if (t == null) { d.State = DroneState.Inbound; break; }
                        var (tx, tz) = t.Center;
                        if (FlyTo(d, tx, tz, step)) { d.State = DroneState.Loading; d.Timer = 0; }
                        break;
                    }
                    case DroneState.Loading:
                    {
                        d.Timer += fdt;
                        if (d.Timer < Balance.DroneHandle) break;
                        var t = FindBuilding(d.Target);
                        if (t != null)
                            while (d.CargoCount < Balance.DroneCapacity && BufTake(t, out int ty, out double v)) AddTo(d.Cargo, ref d.CargoCount, ty, v);
                        d.State = DroneState.Inbound;
                        break;
                    }
                    case DroneState.Inbound:
                        flying = true;
                        if (FlyTo(d, hx, hz, step)) { d.State = DroneState.Unloading; d.Timer = 0; }
                        break;
                    case DroneState.Unloading:
                    {
                        d.Timer += fdt;
                        if (d.Timer < Balance.DroneHandle) break;
                        while (d.CargoCount > 0 && b.BufCount < b.Def.Capacity)
                        {
                            TakeFrom(d.Cargo, ref d.CargoCount, out int ty, out double v);
                            BufAdd(b, ty, v);
                        }
                        if (d.CargoCount > 0) { if (b.Status == "") b.Status = "Carousel full: the line behind it can't keep up"; break; }
                        d.State = DroneState.Docked;
                        d.Target = 0;
                        S.droneTrips++;
                        OnDroneUnloaded?.Invoke(b);
                        break;
                    }
                }
            }
            b.Activity = b.BufCount > 0 || flying ? 1 : 0.25f;
            if (b.Status == "" && b.BufCount == 0 && !flying && !Buildings.Exists(DroneServes))
                b.Status = "Nothing to collect: build rim intakes with no line behind them";
        }

        // ───────────────────────────── the Lucky Lagoon: the Slot-Machine Sorter ─────────────────────────────

        /// <summary>One spin per chunk: the house keeps it, or it pays out as sorted loot (×1, ×2, ×5), or 7-7-7.</summary>
        void SpinSlots(Building b, double value)
        {
            b.Spins++;
            S.slotSpins++;
            double lose = Math.Max(0.05, 1 - Balance.SlotCherries - Balance.SlotBar - Balance.SlotSevens - SlotLoseCut);
            double r = Rng.NextDouble();
            SlotResult res;
            double mult;
            if (r < Balance.SlotJackpot) { res = SlotResult.Jackpot; mult = 0; }
            else if ((r -= Balance.SlotJackpot) < lose) { res = SlotResult.Lose; mult = 0; }
            else if ((r -= lose) < Balance.SlotSevens) { res = SlotResult.Sevens; mult = 5; }
            else if ((r -= Balance.SlotSevens) < Balance.SlotBar) { res = SlotResult.Bar; mult = 2; }
            else { res = SlotResult.Cherries; mult = 1; }
            b.SpinResult = (int)res;
            OnSlotSpin?.Invoke(b, res);
            if (mult > 0)
            {
                AddTo(b.Out, ref b.OutCount, Mall.LootTypes[PickLoot(true)], value * mult);
                S.sorted++;
            }
            if (res == SlotResult.Jackpot) Jackpot(b, value);
        }

        /// <summary>7-7-7: sirens, and a fortune in tokens sprayed across the fountain (plus a relic, the only way the slots find one).</summary>
        void Jackpot(Building b, double value)
        {
            S.jackpots++;
            double total = value * Balance.JackpotPayout;
            var (mx, mz) = b.Center;
            if (JackpotType >= 0)
                for (int i = 0; i < Balance.JackpotTokens; i++)
                {
                    var (x, z) = RandomLanding();
                    AddLoose(JackpotType, x, z, total / Balance.JackpotTokens, (mx, 2.4f, mz));
                }
            var relic = FindRelic();
            AddTo(b.Out, ref b.OutCount, relic.def.ItemType, relic.value);
            OnJackpot?.Invoke(b, total);
        }

        /// <summary>Tour and tests: every champagne cannon fires on its next tick (unless it's holding fire).</summary>
        public void DebugFireCannons() { foreach (var b in Buildings) if (b.Def.Intake == "cannon") b.Acc = Math.Max(b.Acc, 1); }

        /// <summary>Tour and tests: this slot machine hits 7-7-7 right now.</summary>
        public void DebugJackpot(Building b) => Jackpot(b, Mall.BaseEV * CurStratum.ValueMult * Scale * ChunkScoops);

        // ───────────────────────────── Eternity Plaza: the Old Well ─────────────────────────────

        Building FindWell()
        {
            foreach (var b in Buildings) if (b.Def.Intake == "well") return b;
            return null;
        }

        /// <summary>A wish nobody caught falls into the well, and that much crust is simply gone (no rubble, no loot).</summary>
        void GrantWish(Building well, ActiveWish w)
        {
            double bite = Balance.WellChunks * Balance.WishTokens[(int)w.Def.Rarity] * ChunkScoops * WellMult;
            double done = DigCrust(bite, (type, value) => true);
            S.wellWishes++;
            well.Activity = 1;
            well.LastPickX = w.X; well.LastPickZ = w.Z; well.PickSerial++;
            OnWellGranted?.Invoke(well, w, done);
        }
    }
}
