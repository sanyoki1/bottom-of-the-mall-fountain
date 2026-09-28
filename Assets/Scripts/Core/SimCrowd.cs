// The crowd: shoppers walk in from the doors, stand at the fountain, wind up and toss coins (or
// ridiculous things), sometimes with a True Wish attached, then leave. Wishability decides how
// many come, how often they throw and how fancy the throws are. Pure data, so the balance bot
// runs exactly this.
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public enum ShopperState : byte { Arriving, Waiting, WindUp, Strolling, Leaving }

    public sealed class Shopper
    {
        public int Uid;
        public ArchetypeDef Def;
        public float X, Z, Heading;          // heading in degrees (0 = +z)
        public float TX, TZ;                 // where they're walking to
        public float Speed;                  // current walking speed (for the walk cycle)
        public ShopperState State;
        public float Timer;                  // seconds in the current state
        public int TossesLeft;
        public int Exit;                     // door index they leave by
        public string Line;                  // what they're saying right now
        public float LineTimer;
        public bool LineIsWish;
        public Rarity LineRarity;
        public int PendingType = -1;         // item in hand during the wind-up
        public WishDef PendingWish;
    }

    public sealed class ActiveWish
    {
        public int Uid;
        public WishDef Def;
        public double Value;
        public double Tokens;
        public double Age, Life;
        public float X, Z;                   // where it rose from
    }

    public sealed partial class Sim
    {
        public readonly List<Shopper> Shoppers = new List<Shopper>();
        public readonly List<ActiveWish> Wishes = new List<ActiveWish>();
        double tossAcc, spawnTimer;
        int pendingTosses;
        public float PlayerX, PlayerZ = -18;   // the view keeps this current so shoppers step around you

        public event Action<Shopper, LooseItem> OnToss;
        public event Action<LooseItem> OnLanded;
        public event Action<Shopper> OnSpeak;
        public event Action<ActiveWish> OnWishSpawned;
        public event Action<ActiveWish, double, double, bool> OnWishCaught;   // cash, tokens, first time
        public event Action<ActiveWish> OnWishEscaped;

        void ClearCrowd()
        {
            Shoppers.Clear();
            Wishes.Clear();
            tossAcc = 0;
            spawnTimer = 0;
            pendingTosses = 0;
        }

        // ───────────────────────────── rates ─────────────────────────────

        /// <summary>Tosses per second across the whole crowd.</summary>
        public double TossRate => (1 + Wishability * Balance.TossPerWish) * TossRateMult * EventTossMult / Balance.TossBaseInterval;
        public double TossInterval => 1 / Math.Max(1e-6, TossRate);
        public int CrowdTarget => (int)Math.Min(Balance.CrowdMax, Balance.CrowdBase + Wishability * Balance.CrowdPerWish + Math.Max(0, TossRate - 0.3) * 6);
        public double EventTossMult => 1;

        /// <summary>Coin tier weights right now (for the terminal's "what gets thrown" readout).</summary>
        public double[] TierWeights(double bias = 0)
        {
            var w = new double[Content.CoinTiers.Count];
            double mu = Wishability * Balance.TierPerWish + bias;
            for (int i = 0; i < w.Length; i++)
            {
                if (Content.CoinTierMinWish[i] > Wishability) continue;
                double d = i - mu;
                w[i] = Math.Exp(-d * d / (2 * Balance.TierSigma * Balance.TierSigma));
            }
            if (w[0] <= 0) w[0] = 1e-3;
            return w;
        }

        public double OddityChance
        {
            get
            {
                if (Content.OddityMinWish.Count == 0 || Content.OddityMinWish[0] > Wishability) return 0;
                return Math.Min(Balance.OddityMaxChance, Balance.OddityBaseChance + Balance.OddityPerWish * Wishability);
            }
        }

        // ───────────────────────────── update ─────────────────────────────

        void UpdateCrowd(double dt)
        {
            float fdt = (float)dt;
            // arrivals
            spawnTimer -= dt;
            if (Shoppers.Count < CrowdTarget && spawnTimer <= 0)
            {
                SpawnShopper();
                spawnTimer = Balance.SpawnInterval * (0.6 + Rng.NextDouble() * 0.8);
            }

            // the toss clock: hand out throws to whoever is waiting at the rim
            tossAcc += dt * TossRate;
            while (tossAcc >= 1)
            {
                tossAcc -= 1;
                if (pendingTosses < Balance.MaxPendingTosses) pendingTosses++;
            }
            if (pendingTosses > 0)
            {
                Shopper best = null;
                float bestT = -1;
                foreach (var s in Shoppers)
                    if (s.State == ShopperState.Waiting && s.TossesLeft > 0 && s.Timer > bestT) { best = s; bestT = s.Timer; }
                if (best != null)
                {
                    pendingTosses--;
                    StartWindUp(best);
                }
            }

            // the wormhole: other malls' fountains leak into this one
            if (HasPortal)
            {
                portalAcc += dt * TossRate * Balance.PortalShare;
                while (portalAcc >= 1) { portalAcc -= 1; PortalToss(); }
            }

            for (int i = Shoppers.Count - 1; i >= 0; i--)
            {
                var s = Shoppers[i];
                s.Timer += fdt;
                if (s.LineTimer > 0) { s.LineTimer -= fdt; if (s.LineTimer <= 0) s.Line = null; }
                switch (s.State)
                {
                    case ShopperState.Arriving:
                    case ShopperState.Strolling:
                        if (Walk(s, fdt, true))
                        {
                            s.State = ShopperState.Waiting;
                            s.Timer = 0;
                            s.Heading = (float)(Math.Atan2(-s.X, -s.Z) * 180 / Math.PI);
                        }
                        break;
                    case ShopperState.Waiting:
                        s.Speed = 0;
                        if (s.TossesLeft <= 0 || s.Timer > Balance.Patience)
                        {
                            if (s.TossesLeft > 0 && Rng.NextDouble() < 0.35) StrollTo(s);
                            else Leave(s);
                        }
                        break;
                    case ShopperState.WindUp:
                        s.Speed = 0;
                        if (s.Timer >= Balance.WindUp) Release(s);
                        break;
                    case ShopperState.Leaving:
                        if (Walk(s, fdt, false)) { Shoppers.RemoveAt(i); continue; }
                        break;
                }
            }
        }

        void SpawnShopper(bool atRim = false)
        {
            var def = PickArchetype();
            int door = Rng.Next(Layout.Doors.Length);
            var d = Layout.Doors[door];
            var s = new Shopper
            {
                Uid = ++uid, Def = def, X = d.x, Z = d.z, State = ShopperState.Arriving,
                TossesLeft = def.Tosses + Rng.Next(0, 2) + (int)(TossRate * 2),
                Exit = Rng.Next(Layout.Doors.Length),
            };
            // stand on the side of the fountain nearest the door they came in by
            float doorDeg = (float)(Math.Atan2(d.x, d.z) * 180 / Math.PI);
            PickStandSpot(s, atRim ? (float)(Rng.NextDouble() * 360) : doorDeg);
            if (atRim)
            {
                s.X = s.TX;
                s.Z = s.TZ;
                s.State = ShopperState.Waiting;
                s.Heading = (float)(Math.Atan2(-s.X, -s.Z) * 180 / Math.PI);
            }
            Shoppers.Add(s);
        }

        /// <summary>A fresh mall (or a loaded save) starts with a couple of shoppers already at the rim.</summary>
        void SeedCrowd()
        {
            int n = Math.Min(2, CrowdTarget);
            for (int i = 0; i < n; i++) SpawnShopper(true);
        }

        ArchetypeDef PickArchetype()
        {
            double total = 0;
            foreach (var a in Content.Archetypes) if (a.MinWish <= Wishability) total += a.Weight;
            double r = Rng.NextDouble() * total;
            foreach (var a in Content.Archetypes)
            {
                if (a.MinWish > Wishability) continue;
                r -= a.Weight;
                if (r <= 0) return a;
            }
            return Content.Archetypes[0];
        }

        /// <summary>Choose a free spot on the stand ring near an angle, away from the stepping stones and other shoppers.</summary>
        void PickStandSpot(Shopper s, float nearDeg)
        {
            for (int attempt = 0; attempt < 12; attempt++)
            {
                float a = nearDeg + (float)(Rng.NextDouble() * 2 - 1) * (attempt < 6 ? 25 : 180);
                bool ok = true;
                // stone angles are measured from +x (like the fountain mesh); shopper angles from +z
                foreach (float st in Layout.StoneAngles) if (Math.Abs(DeltaAngle(a, 90 - st)) < 7) { ok = false; break; }
                float x = (float)Math.Sin(a * Math.PI / 180) * Balance.StandRadius, z = (float)Math.Cos(a * Math.PI / 180) * Balance.StandRadius;
                if (ok)
                    foreach (var o in Shoppers)
                        if (o != s && o.State != ShopperState.Leaving && (o.TX - x) * (o.TX - x) + (o.TZ - z) * (o.TZ - z) < 1.1f) { ok = false; break; }
                if (ok)
                    foreach (var bl in Buildings)
                    {
                        var (bx, bz) = bl.Center;
                        float rr = bl.Radius + 0.7f;
                        if ((bx - x) * (bx - x) + (bz - z) * (bz - z) < rr * rr) { ok = false; break; }
                    }
                if (ok) { s.TX = x; s.TZ = z; return; }
            }
            float fa = nearDeg + 13;
            s.TX = (float)Math.Sin(fa * Math.PI / 180) * Balance.StandRadius;
            s.TZ = (float)Math.Cos(fa * Math.PI / 180) * Balance.StandRadius;
        }

        static float DeltaAngle(float a, float b)
        {
            float d = (a - b) % 360;
            if (d > 180) d -= 360;
            if (d < -180) d += 360;
            return d;
        }

        void StrollTo(Shopper s)
        {
            float here = (float)(Math.Atan2(s.X, s.Z) * 180 / Math.PI);
            PickStandSpot(s, here + (Rng.NextDouble() < 0.5 ? -40 : 40));
            s.State = ShopperState.Strolling;
            s.Timer = 0;
        }

        void Leave(Shopper s)
        {
            var d = Layout.Doors[s.Exit];
            s.TX = d.x;
            s.TZ = d.z;
            s.State = ShopperState.Leaving;
            s.Timer = 0;
        }

        /// <summary>Steer toward the target around obstacles, the fountain, the player and each other. True on arrival.</summary>
        bool Walk(Shopper s, float dt, bool toRing)
        {
            float dx = s.TX - s.X, dz = s.TZ - s.Z;
            float dist = (float)Math.Sqrt(dx * dx + dz * dz);
            if (dist < 0.15f) { s.Speed = 0; return true; }
            float speed = s.Def.Speed;
            float vx = dx / dist, vz = dz / dist;
            // keep off the fountain: when heading around it, push outward from the rim
            float r = (float)Math.Sqrt(s.X * s.X + s.Z * s.Z);
            float keep = Balance.StandRadius - 0.3f;
            if (r < keep + 1.2f && !(toRing && dist < 1.5f))
            {
                float push = (keep + 1.2f - r) * 1.2f;
                vx += s.X / Math.Max(0.01f, r) * push;
                vz += s.Z / Math.Max(0.01f, r) * push;
                // walk around the ring rather than into it
                float tang = Math.Sign(s.X * dz - s.Z * dx);
                vx += -s.Z / Math.Max(0.01f, r) * tang * 0.6f;
                vz += s.X / Math.Max(0.01f, r) * tang * 0.6f;
            }
            foreach (var o in Layout.Obstacles) Repel(s, o.x, o.z, o.r + 0.5f, ref vx, ref vz);
            foreach (var bl in Buildings)
            {
                if (bl.Def.IsBelt) continue;   // shoppers step over belts
                var (bx, bz) = bl.Center;
                Repel(s, bx, bz, bl.Radius + 0.5f, ref vx, ref vz);
            }
            Repel(s, PlayerX, PlayerZ, 0.9f, ref vx, ref vz);
            foreach (var o in Shoppers)
                if (o != s) Repel(s, o.X, o.Z, 0.55f, ref vx, ref vz);
            float len = (float)Math.Sqrt(vx * vx + vz * vz);
            if (len < 1e-4f) return false;
            float step = Math.Min(dist, speed * dt);
            s.X += vx / len * step;
            s.Z += vz / len * step;
            s.Speed = speed;
            float want = (float)(Math.Atan2(vx, vz) * 180 / Math.PI);
            s.Heading += DeltaAngle(want, s.Heading) * Math.Min(1, dt * 8);
            return false;
        }

        static void Repel(Shopper s, float ox, float oz, float radius, ref float vx, ref float vz)
        {
            float dx = s.X - ox, dz = s.Z - oz;
            float d2 = dx * dx + dz * dz;
            if (d2 > radius * radius || d2 < 1e-6f) return;
            float d = (float)Math.Sqrt(d2);
            float k = (radius - d) / radius * 2.2f;
            vx += dx / d * k;
            vz += dz / d * k;
        }

        // ───────────────────────────── tossing ─────────────────────────────

        void StartWindUp(Shopper s)
        {
            s.State = ShopperState.WindUp;
            s.Timer = 0;
            s.TossesLeft--;
            s.PendingType = PickTossType(s.Def);
            var t = Content.Items[s.PendingType];
            int tier = t.Tier >= 0 ? t.Tier : 3 + (int)t.Rarity * 2;
            s.PendingWish = Rng.NextDouble() < Balance.WishChanceBase + Balance.WishChancePerTier * tier ? PickWish() : null;
            if (s.PendingWish != null) Say(s, "“" + s.PendingWish.Text + "”", 4.5f, true, s.PendingWish.Rarity);
            else if (Rng.NextDouble() < 0.3) Say(s, Rng.NextDouble() < 0.75 && s.Def.Barks.Length > 0 ? s.Def.Barks[Rng.Next(s.Def.Barks.Length)] : Content.GenericBarks[Rng.Next(Content.GenericBarks.Length)], 3f, false, Rarity.Common);
        }

        void Say(Shopper s, string line, float seconds, bool wish, Rarity r)
        {
            s.Line = line;
            s.LineTimer = seconds;
            s.LineIsWish = wish;
            s.LineRarity = r;
            OnSpeak?.Invoke(s);
        }

        int PickTossType(ArchetypeDef a)
        {
            if (a.JunkChance > 0 && Rng.NextDouble() < a.JunkChance) return Content.Type("gum");
            double odd = OddityChance * a.OddityBoost;
            // archetypes with signature items throw them a bit early, before the fountain is fancy enough for everyone
            if (a.Oddities.Length > 0 && Rng.NextDouble() < Math.Max(odd, a.OddityBoost > 1 ? 0.04 * a.OddityBoost : 0))
            {
                var own = new List<int>();
                foreach (var id in a.Oddities)
                {
                    int ti = Content.TypeOrNone(id);
                    if (ti < 0) continue;
                    int oi = Content.Oddities.IndexOf(ti);
                    double need = oi >= 0 ? Content.OddityMinWish[oi] : (Content.Items[ti].Tier >= 0 ? Content.CoinTierMinWish[Content.Items[ti].Tier] : 0);
                    if (need <= Wishability + 15) own.Add(ti);
                }
                if (own.Count > 0 && Rng.NextDouble() < 0.6) return own[Rng.Next(own.Count)];
                if (odd > 0)
                {
                    var any = new List<int>();
                    for (int i = 0; i < Content.Oddities.Count; i++) if (Content.OddityMinWish[i] <= Wishability) any.Add(Content.Oddities[i]);
                    if (any.Count > 0) return any[Rng.Next(any.Count)];
                }
            }
            else if (odd > 0 && Rng.NextDouble() < odd)
            {
                var any = new List<int>();
                for (int i = 0; i < Content.Oddities.Count; i++) if (Content.OddityMinWish[i] <= Wishability) any.Add(Content.Oddities[i]);
                if (any.Count > 0) return any[Rng.Next(any.Count)];
            }
            int tier = Pick(TierWeights(a.TierBias));
            return Content.CoinTiers[tier];
        }

        void Release(Shopper s)
        {
            int type = s.PendingType >= 0 ? s.PendingType : Content.CoinTiers[0];
            s.PendingType = -1;
            // throw toward the middle, a little to either side
            double a = Math.Atan2(s.X, s.Z) + (Rng.NextDouble() * 2 - 1) * 0.45;
            double rMin = Balance.LandMinR, rMax = Balance.LandMaxR;
            double rr = Math.Sqrt(rMin * rMin + Rng.NextDouble() * (rMax * rMax - rMin * rMin));
            float x = (float)(Math.Sin(a) * rr), z = (float)(Math.Cos(a) * rr);
            float h = (float)(Math.PI / 180 * s.Heading);
            float hx = s.X + (float)Math.Sin(h) * 0.35f, hz = s.Z + (float)Math.Cos(h) * 0.35f;
            var def = Content.Items[type];
            var it = AddLoose(type, x, z, def.BaseValue * Mall.ValueScale, (hx, 1.55f * s.Def.Scale + 0.2f, hz));
            it.Wish = s.PendingWish;
            s.PendingWish = null;
            s.State = ShopperState.Waiting;
            s.Timer = 0;
            S.tosses++;
            if (def.Cat == ItemCat.Oddity) S.oddities++;
            OnToss?.Invoke(s, it);
        }

        double portalAcc;
        public bool HasPortal => TechLevel("fountain_wormhole") > 0;

        void PortalToss()
        {
            int m = Rng.Next(Content.Malls.Length);
            if (m == MallDefIndex) m = (m + 1) % Content.Malls.Length;
            var mall = Content.Malls[m];
            var good = new List<int>();
            foreach (int t in mall.LootTypes) if (Content.Items[t].BaseValue > 0) good.Add(t);
            if (good.Count == 0) return;
            int type = good[Rng.Next(good.Count)];
            var (x, z) = RandomLanding();
            var it = AddLoose(type, x, z, Content.Items[type].BaseValue * Mall.ValueScale, (0f, 7.2f, 0f));
            S.tosses++;
            OnToss?.Invoke(null, it);
        }

        void Landed(LooseItem it)
        {
            OnLanded?.Invoke(it);
            if (it.Wish != null)
            {
                SpawnWish(it.Wish, it.X, it.Z);
                it.Wish = null;
            }
        }

        // ───────────────────────────── wishes ─────────────────────────────

        WishDef PickWish()
        {
            var rarity = (Rarity)Pick(Balance.WishRarityWeight);
            var pool = Mall.Wishes;
            var candidates = new List<WishDef>();
            foreach (var w in pool) if (w.Rarity == rarity) candidates.Add(w);
            if (candidates.Count == 0) candidates.AddRange(pool);
            var fresh = candidates.FindAll(w => !wishFound.Contains(w.Id));
            return fresh.Count > 0 && Rng.NextDouble() < Balance.UndiscoveredBias
                ? fresh[Rng.Next(fresh.Count)]
                : candidates[Rng.Next(candidates.Count)];
        }

        public double WishValue(WishDef def) => def.BaseValue * Balance.WishValueScale * Mall.ValueScale * ValueMult;

        void SpawnWish(WishDef def, float x, float z)
        {
            S.wishesSeen++;
            if (Wishes.Count >= Balance.MaxActiveWishes) return;
            var w = new ActiveWish
            {
                Uid = ++uid, Def = def, Value = WishValue(def), Tokens = Balance.WishTokens[(int)def.Rarity],
                Life = Balance.WishLife * WishLifeMult, X = x, Z = z,
            };
            Wishes.Add(w);
            OnWishSpawned?.Invoke(w);
        }

        /// <summary>Tests and the tour: a wish rises right now.</summary>
        public ActiveWish DebugSpawnWish(float x, float z)
        {
            SpawnWish(PickWish(), x, z);
            return Wishes.Count > 0 ? Wishes[Wishes.Count - 1] : null;
        }

        void UpdateWishes(double dt)
        {
            for (int i = Wishes.Count - 1; i >= 0; i--)
            {
                var w = Wishes[i];
                w.Age += dt;
                if (w.Age >= w.Life)
                {
                    Wishes.RemoveAt(i);
                    OnWishEscaped?.Invoke(w);
                }
            }
        }

        public bool CatchWish(int wishUid)
        {
            int idx = Wishes.FindIndex(w => w.Uid == wishUid);
            if (idx < 0) return false;
            var w = Wishes[idx];
            Wishes.RemoveAt(idx);
            AddCash(w.Value);
            S.wishTokens += w.Tokens;
            S.wishesCaught++;
            if (w.Def.Rarity == Rarity.Legendary) S.legendaryWish = true;
            bool first = wishFound.Add(w.Def.Id);
            if (first) Recalc();
            OnWishCaught?.Invoke(w, w.Value, w.Tokens, first);
            return true;
        }

        public ActiveWish FindWish(int wishUid) => Wishes.Find(w => w.Uid == wishUid);
    }
}
