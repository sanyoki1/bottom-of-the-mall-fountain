// Hazards (always on, deliberately light): Officer Doug, mall security, patrols the plaza, and now and
// then while you wade he stops, raises his binoculars and looks: the statue check, a game of red light,
// green light. Move in his sight and you're fined; freeze until he looks away and a shopper tips the
// "statue". And Chad, a rival fountain diver in a wetsuit, who sneaks in to pocket your coins until you
// chase him off (he drops everything when he runs).
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public enum GuardState : byte { Patrol, Suspicious, Looking, Busted, Leaving }
    public enum RivalState : byte { Away, Arriving, Stealing, Fleeing, Leaving }

    public sealed class Guard
    {
        public float X, Z, Heading, Speed;
        public float Angle;                  // along the patrol loop
        public GuardState State;
        public float Timer;
        public string Line;
        public float LineTimer;
        // the statue check: while he looks, moving in his sight fills Suspicion (1 = busted)
        public float Suspicion, LookLen;
        public bool Sees;                    // you're wading in his line of sight right now
        /// <summary>Suspicious (the tell) or Looking: freeze.</summary>
        public bool Watching => State == GuardState.Suspicious || State == GuardState.Looking;
        /// <summary>How far through his look he is (0..1).</summary>
        public float LookProgress => State == GuardState.Looking && LookLen > 0 ? Math.Min(1f, Timer / LookLen) : 0;
    }

    public sealed class Rival
    {
        public float X, Z, Heading, Speed;
        public RivalState State;
        public float Timer, GrabAcc;
        public int Target, Exit;
        public readonly List<(int type, double value)> Loot = new List<(int, double)>();
        public string Line;
        public float LineTimer;
    }

    public sealed partial class Sim
    {
        public readonly Guard Guard = new Guard { Angle = 2.5f };
        public readonly Rival Rival = new Rival();
        public bool PlayerInWater;           // set by the view every frame
        public bool PlayerMoving;            // set by the view (and the bot): walking or wading, not just looking around
        double guardCheck = Balance.GuardFirstLook, rivalTimer = 240, lastAction = -99;

        /// <summary>The player did something a statue wouldn't (grab, dig, catch): the view calls this.</summary>
        public void NoteAction() => lastAction = Time;
        /// <summary>Moving, or acted in the last moment: what Doug notices while he looks.</summary>
        public bool PlayerActive => PlayerMoving || Time - lastAction < 0.35;

        public event Action<Guard, string> OnGuardSpeak;
        public event Action<double> OnFined;
        public event Action<Guard> OnGuardLook;              // he stops and raises the binoculars: freeze
        public event Action<Guard, bool> OnGuardLookDone;    // true = busted, false = he looked away
        public event Action<float, float, double> OnStatueTipped;   // where the tip lands, and its value
        public event Action<Rival, string> OnRivalSpeak;
        public event Action<Rival> OnRivalArrived;
        public event Action<Rival, int> OnRivalChased;     // items dropped
        public event Action<Rival, int> OnRivalEscaped;    // items stolen

        const float PatrolR = 13.5f;

        static readonly string[] GuardHmm = { "Hm?", "Hold on...", "Now what was THAT?", "Something moved.", "Wait a minute..." };
        static readonly string[] GuardBusted =
        {
            "*whistle* Statues don't wade, pal. That's a fine.",
            "*whistle* I SAW that. Wading, first degree.",
            "*whistle* Statues don't pick up coins. I'm writing you up.",
            "*whistle* That's a fine. Don't make me get the clipboard. ...I've got the clipboard.",
            "*whistle* Mall policy 14b: no swimming. The statue exemption does not apply to you.",
        };
        static readonly string[] GuardFooled =
        {
            "Huh. Nice statue. Very lifelike.", "Must be one of those art installations.", "Could've sworn... nah. Carry on, statue.",
            "Is that new? It's very... damp.", "Management never tells me when they buy art.",
        };
        static readonly string[] GuardRelief = { "Thank you. The system works.", "Good. Very good. Carry on.", "That's what I like to see. Dry ankles." };
        static readonly string[] GuardNods = { "Deputy. *tips cap*", "Carry on, Deputy. Ankles are your business now.", "Nothing to see here, folks. That's a deputy." };
        static readonly string[] RivalLines =
        {
            "Finders keepers!", "This is MY fountain now!", "Don't mind me, just a regular guy. In a wetsuit.",
            "You snooze, you lose!", "Chad's coins. Chad's rules.",
        };
        static readonly string[] RivalFlee = { "Aaah! OK OK OK I'm going!", "You'll never catch me! (please don't touch me)", "This isn't over! (it's over)", "My snorkel!" };

        void GuardSay(string line) { Guard.Line = line; Guard.LineTimer = 3.5f; OnGuardSpeak?.Invoke(Guard, line); }
        void RivalSay(string line) { Rival.Line = line; Rival.LineTimer = 3f; OnRivalSpeak?.Invoke(Rival, line); }

        public double FineMult
        {
            get
            {
                double m = 1;
                for (int i = 0; i < techLevel.Length; i++)
                    if (Content.Techs[i].Kind == TechKind.GuardFine && techLevel[i] > 0) m *= Math.Pow(1 - Content.Techs[i].Value, techLevel[i]);
                return m;
            }
        }

        double RivalFreqMult
        {
            get
            {
                double m = 1;
                for (int i = 0; i < techLevel.Length; i++)
                    if (Content.Techs[i].Kind == TechKind.RivalRepel && techLevel[i] > 0) m *= Math.Pow(1 - Content.Techs[i].Value, techLevel[i]);
                return m;
            }
        }

        public double FineAmount => Math.Max(0.05 * Scale, S.cash * 0.04) * FineMult;

        void UpdateHazards(double dt)
        {
            float fdt = (float)dt;
            UpdateGuard(fdt);
            UpdateRival(fdt);
        }

        void UpdateGuard(float dt)
        {
            var g = Guard;
            if (g.LineTimer > 0) { g.LineTimer -= dt; if (g.LineTimer <= 0) g.Line = null; }
            g.Timer += dt;
            g.Sees = false;
            switch (g.State)
            {
                case GuardState.Patrol:
                {
                    // a slow loop around the plaza, with the odd pause to adjust his belt
                    bool pausing = (g.Timer % 20f) > 17f;
                    g.Speed = pausing ? 0 : 1.1f;
                    if (!pausing) g.Angle += dt * g.Speed / PatrolR;
                    float tx = (float)Math.Sin(g.Angle) * PatrolR, tz = (float)Math.Cos(g.Angle) * PatrolR;
                    // (standing still, atan2(0, 0) would snap him to face north)
                    if (tx != g.X || tz != g.Z) g.Heading = (float)(Math.Atan2(tx - g.X, tz - g.Z) * 180 / Math.PI);
                    g.X = tx; g.Z = tz;
                    guardCheck -= dt;
                    if (guardCheck <= 0)
                    {
                        guardCheck = RandRange(Balance.GuardLookMin, Balance.GuardLookMax);
                        if (PlayerInWater)
                        {
                            // a sworn-in deputy (fines × 0) gets a nod instead of a look
                            if (FineMult <= 1e-9) { if (Rng.NextDouble() < 0.5) GuardSay(GuardNods[Rng.Next(GuardNods.Length)]); }
                            else StartGuardLook();
                        }
                    }
                    break;
                }
                case GuardState.Suspicious:
                    // the tell: he stops, turns to you and raises his binoculars
                    g.Speed = 0;
                    FacePlayer(g);
                    if (g.Timer >= Balance.GuardTell) { g.State = GuardState.Looking; g.Timer = 0; g.Suspicion = 0; }
                    break;
                case GuardState.Looking:
                    g.Speed = 0;
                    FacePlayer(g);
                    g.Sees = PlayerInWater && GuardCanSee(g);
                    if (g.Sees && PlayerActive) g.Suspicion += dt * Balance.GuardNotice;
                    else g.Suspicion = Math.Max(0, g.Suspicion - dt * 0.6f);
                    if (g.Suspicion >= 1) BustPlayer();
                    else if (g.Timer >= g.LookLen) EndGuardLook();
                    break;
                case GuardState.Busted:
                    g.Speed = 0;
                    FacePlayer(g);
                    if (g.Timer > 2.5f) { g.State = GuardState.Leaving; g.Timer = 0; }
                    break;
                case GuardState.Leaving:
                    if (g.Timer > 2f) { g.State = GuardState.Patrol; g.Timer = 0; }
                    break;
            }
        }

        void FacePlayer(Guard g) => g.Heading = (float)(Math.Atan2(PlayerX - g.X, PlayerZ - g.Z) * 180 / Math.PI);

        /// <summary>Doug can see you unless the fountain's centrepiece stands between you.</summary>
        bool GuardCanSee(Guard g)
        {
            // closest point to the fountain's centre on the line of sight from Doug to you
            float bx = PlayerX - g.X, bz = PlayerZ - g.Z, len2 = bx * bx + bz * bz;
            float t = len2 > 1e-4f ? Math.Max(0, Math.Min(1, -(g.X * bx + g.Z * bz) / len2)) : 0;
            float cx = g.X + bx * t, cz = g.Z + bz * t;
            return cx * cx + cz * cz > Balance.StatueHideR * Balance.StatueHideR;
        }

        void StartGuardLook()
        {
            var g = Guard;
            g.State = GuardState.Suspicious;
            g.Timer = 0;
            g.Suspicion = 0;
            g.LookLen = (float)RandRange(Balance.GuardLookLenMin, Balance.GuardLookLenMax);
            S.guardLooks++;
            GuardSay(GuardHmm[Rng.Next(GuardHmm.Length)]);
            OnGuardLook?.Invoke(g);
        }

        /// <summary>You moved while he looked: whistle, fine.</summary>
        void BustPlayer()
        {
            var g = Guard;
            double fine = Math.Min(FineAmount, S.cash);
            S.cash -= fine;
            S.finesPaid++;
            g.State = GuardState.Busted;
            g.Timer = 0;
            g.Suspicion = 1;
            GuardSay(GuardBusted[Rng.Next(GuardBusted.Length)]);
            OnFined?.Invoke(fine);
            OnGuardLookDone?.Invoke(g, true);
        }

        /// <summary>He looks away. Still in the water (and still a statue)? A passing shopper tips you.</summary>
        void EndGuardLook()
        {
            var g = Guard;
            g.State = GuardState.Leaving;
            g.Timer = 0;
            if (PlayerInWater)
            {
                S.statuesFooled++;
                GuardSay(GuardFooled[Rng.Next(GuardFooled.Length)]);
                TipTheStatue();
            }
            else GuardSay(GuardRelief[Rng.Next(GuardRelief.Length)]);
            OnGuardLookDone?.Invoke(g, false);
        }

        /// <summary>A shopper takes you for the fountain's newest statue and tosses you a coin: it lands at your feet.</summary>
        void TipTheStatue()
        {
            int tier = Math.Max(0, Math.Min(Content.CoinTiers.Count - 1, (int)Math.Round(Wishability * Balance.TierPerWish) + 1));
            int type = Content.CoinTiers[tier];
            (float x, float y, float z) from = (PlayerX * 1.6f, 1.5f, PlayerZ * 1.6f - 2f);
            float best = float.MaxValue;
            foreach (var s in Shoppers)
            {
                float d = (s.X - PlayerX) * (s.X - PlayerX) + (s.Z - PlayerZ) * (s.Z - PlayerZ);
                if (d < best) { best = d; from = (s.X, 1.5f, s.Z); }
            }
            double a = Rng.NextDouble() * Math.PI * 2;
            float x = PlayerX + (float)Math.Cos(a) * 0.7f, z = PlayerZ + (float)Math.Sin(a) * 0.7f;
            float r = (float)Math.Sqrt(x * x + z * z);
            if (r > Balance.LandMaxR) { x *= Balance.LandMaxR / r; z *= Balance.LandMaxR / r; }
            else if (r < Balance.LandMinR && r > 0.01f) { x *= Balance.LandMinR / r; z *= Balance.LandMinR / r; }
            double value = Content.Items[type].BaseValue * Scale * Balance.StatueTip;
            AddLoose(type, x, z, value, from);
            OnStatueTipped?.Invoke(x, z, value);
        }

        void UpdateRival(float dt)
        {
            var r = Rival;
            if (r.LineTimer > 0) { r.LineTimer -= dt; if (r.LineTimer <= 0) r.Line = null; }
            r.Timer += dt;
            switch (r.State)
            {
                case RivalState.Away:
                    if (S.tosses < 40 || S.mallCleared) return;
                    // the sign makes him come less often: his clock runs slower
                    rivalTimer -= dt * RivalFreqMult;
                    if (rivalTimer > 0) return;
                    rivalTimer = RandRange(300, 540);
                    int di = Rng.Next(Layout.Doors.Length);
                    var door = Layout.Doors[di];
                    r.X = door.x; r.Z = door.z;
                    r.Exit = Rng.Next(Layout.Doors.Length);
                    r.Loot.Clear();
                    r.State = RivalState.Arriving;
                    r.Timer = 0;
                    OnRivalArrived?.Invoke(r);
                    RivalSay("Heh heh heh. Nice fountain. Be a shame if someone... took the coins out of it.");
                    break;
                case RivalState.Arriving:
                {
                    // walk straight to the rim and hop in
                    float len = (float)Math.Sqrt(r.X * r.X + r.Z * r.Z);
                    float goal = Balance.LandMaxR - 1f;
                    r.Speed = 2.2f;
                    float step = Math.Min(len - goal, r.Speed * dt);
                    if (len > goal + 0.05f) { r.X -= r.X / len * step; r.Z -= r.Z / len * step; }
                    else { r.State = RivalState.Stealing; r.Timer = 0; }
                    r.Heading = (float)(Math.Atan2(-r.X, -r.Z) * 180 / Math.PI);
                    break;
                }
                case RivalState.Stealing:
                {
                    r.Speed = 1.3f;
                    int idx = r.Target != 0 && looseIndex.TryGetValue(r.Target, out int ti) ? ti : -1;
                    if (idx < 0 || Loose[idx].State == LooseState.Airborne)
                    {
                        idx = RichestNear(r.X, r.Z, 5f);
                        r.Target = idx >= 0 ? Loose[idx].Uid : 0;
                    }
                    if (idx >= 0)
                    {
                        var it = Loose[idx];
                        float dx = it.X - r.X, dz = it.Z - r.Z, d = (float)Math.Sqrt(dx * dx + dz * dz);
                        if (d < 0.4f)
                        {
                            RemoveLooseAt(idx);
                            r.Loot.Add((it.Type, it.Value));
                            r.Target = 0;
                            if (Rng.NextDouble() < 0.15) RivalSay(RivalLines[Rng.Next(RivalLines.Length)]);
                        }
                        else { r.X += dx / d * r.Speed * dt; r.Z += dz / d * r.Speed * dt; r.Heading = (float)(Math.Atan2(dx, dz) * 180 / Math.PI); }
                    }
                    // chased off by getting close to him
                    float px = PlayerX - r.X, pz = PlayerZ - r.Z;
                    if (px * px + pz * pz < 2.2f * 2.2f) { ChaseRival(); break; }
                    if (r.Timer > 70f || r.Loot.Count >= 40)
                    {
                        r.State = RivalState.Leaving;
                        r.Timer = 0;
                        RivalSay("Pleasure doing business! Bye!");
                    }
                    break;
                }
                case RivalState.Fleeing:
                case RivalState.Leaving:
                {
                    var exit = Layout.Doors[r.Exit % Layout.Doors.Length];
                    float dx = exit.x - r.X, dz = exit.z - r.Z, d = (float)Math.Sqrt(dx * dx + dz * dz);
                    r.Speed = r.State == RivalState.Fleeing ? 5.5f : 2.4f;
                    if (d < 0.5f)
                    {
                        if (r.State == RivalState.Leaving) OnRivalEscaped?.Invoke(r, r.Loot.Count);
                        r.State = RivalState.Away;
                        r.Loot.Clear();
                        break;
                    }
                    r.X += dx / d * r.Speed * dt;
                    r.Z += dz / d * r.Speed * dt;
                    r.Heading = (float)(Math.Atan2(dx, dz) * 180 / Math.PI);
                    break;
                }
            }
        }

        /// <summary>You got close enough (or pressed E on him): he drops everything back in the water and runs.</summary>
        public bool ChaseRival()
        {
            var r = Rival;
            if (r.State != RivalState.Stealing && r.State != RivalState.Arriving) return false;
            int n = r.Loot.Count;
            foreach (var l in r.Loot)
            {
                var (x, z) = RandomLanding();
                float mx = (r.X + x) * 0.5f, mz = (r.Z + z) * 0.5f;
                AddLoose(l.type, mx, mz, l.value, (r.X, 1.4f, r.Z));
            }
            r.Loot.Clear();
            r.State = RivalState.Fleeing;
            r.Timer = 0;
            S.rivalsChased++;
            RivalSay(RivalFlee[Rng.Next(RivalFlee.Length)]);
            OnRivalChased?.Invoke(r, n);
            return true;
        }

        int RichestNear(float x, float z, float within)
        {
            int best = -1;
            double bv = -1;
            for (int i = 0; i < Loose.Count; i++)
            {
                var it = Loose[i];
                if (it.State == LooseState.Airborne || IsFish(it.Type)) continue;
                float dx = it.X - x, dz = it.Z - z;
                if (dx * dx + dz * dz > within * within) continue;
                double score = it.Value + 0.001;
                if (score > bv) { bv = score; best = i; }
            }
            if (best < 0) best = NearestLoose((x, z), 20f);
            return best;
        }

        public void DebugRival() { rivalTimer = 0; S.tosses = Math.Max(S.tosses, 40); }
        /// <summary>Tests and the tour: Doug starts a look on his next patrol step (if you're wading).</summary>
        public void DebugGuardCheck() { guardCheck = 0; }
    }
}
