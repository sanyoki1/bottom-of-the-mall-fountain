// Hazards (always on, deliberately light): Officer Doug, mall security, patrols the plaza and every
// so often notices you wading; stay in the water after his whistle and he writes you a fine. And
// Chad, a rival fountain diver in a wetsuit, who sneaks in to pocket your coins until you chase
// him off (he drops everything when he runs).
using System;
using System.Collections.Generic;

namespace WishExtractor.Core
{
    public enum GuardState : byte { Patrol, Warning, Leaving }
    public enum RivalState : byte { Away, Arriving, Stealing, Fleeing, Leaving }

    public sealed class Guard
    {
        public float X, Z, Heading, Speed;
        public float Angle;                  // along the patrol loop
        public GuardState State;
        public float Timer;
        public string Line;
        public float LineTimer;
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
        double guardCheck = 90, rivalTimer = 240;

        public event Action<Guard, string> OnGuardSpeak;
        public event Action<double> OnFined;
        public event Action<Rival, string> OnRivalSpeak;
        public event Action<Rival> OnRivalArrived;
        public event Action<Rival, int> OnRivalChased;     // items dropped
        public event Action<Rival, int> OnRivalEscaped;    // items stolen

        const float PatrolR = 13.5f;

        static readonly string[] GuardWarnings =
        {
            "Sir! SIR! That's a WISHING fountain, not a WADING fountain!",
            "Mall policy 14b: no swimming. I will not repeat myself. (I will repeat myself.)",
            "Out of the fountain, pal. I've got my eye on you. My good eye.",
            "*whistle* Hey! Feet out of the water! Both of them!",
            "Ma'am, the fountain is for coins. Are you a coin? You are not a coin.",
        };
        static readonly string[] GuardFines =
        {
            "That's a fine. Don't make me get the clipboard. ...I've got the clipboard.",
            "Citation issued. Wading, first degree.",
            "I'm writing you up. For the record, I'm very disappointed.",
        };
        static readonly string[] GuardRelief = { "Thank you. The system works.", "Good. Very good. Carry on.", "That's what I like to see. Dry ankles." };
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
            switch (g.State)
            {
                case GuardState.Patrol:
                {
                    // a slow loop around the plaza, with the odd pause to adjust his belt
                    bool pausing = (g.Timer % 20f) > 17f;
                    g.Speed = pausing ? 0 : 1.1f;
                    if (!pausing) g.Angle += dt * g.Speed / PatrolR;
                    float tx = (float)Math.Sin(g.Angle) * PatrolR, tz = (float)Math.Cos(g.Angle) * PatrolR;
                    g.Heading = (float)(Math.Atan2(tx - g.X, tz - g.Z) * 180 / Math.PI);
                    g.X = tx; g.Z = tz;
                    guardCheck -= dt;
                    if (guardCheck <= 0)
                    {
                        guardCheck = RandRange(60, 150);
                        float dx = PlayerX - g.X, dz = PlayerZ - g.Z;
                        if (PlayerInWater && dx * dx + dz * dz < 22f * 22f)
                        {
                            g.State = GuardState.Warning;
                            g.Timer = 0;
                            GuardSay(GuardWarnings[Rng.Next(GuardWarnings.Length)]);
                        }
                    }
                    break;
                }
                case GuardState.Warning:
                {
                    g.Speed = 0;
                    g.Heading = (float)(Math.Atan2(PlayerX - g.X, PlayerZ - g.Z) * 180 / Math.PI);
                    if (!PlayerInWater) { GuardSay(GuardRelief[Rng.Next(GuardRelief.Length)]); g.State = GuardState.Leaving; g.Timer = 0; break; }
                    if (g.Timer >= 5f)
                    {
                        double fine = FineAmount;
                        fine = Math.Min(fine, S.cash);
                        S.cash -= fine;
                        S.finesPaid++;
                        GuardSay(GuardFines[Rng.Next(GuardFines.Length)]);
                        OnFined?.Invoke(fine);
                        g.State = GuardState.Leaving;
                        g.Timer = 0;
                    }
                    break;
                }
                case GuardState.Leaving:
                    if (g.Timer > 3f) { g.State = GuardState.Patrol; g.Timer = 0; }
                    break;
            }
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
                    rivalTimer -= dt / RivalFreqMult;
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
        public void DebugGuardCheck() { guardCheck = 0; }
    }
}
