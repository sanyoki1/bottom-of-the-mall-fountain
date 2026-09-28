// Owns every 3D piece of the game and reacts to Sim events: builds the mall, fountain and
// machines, animates stage activity, flies loot around, spawns wishes / golden pennies /
// rats, and resolves world clicks. The UI listens to FloatText for world-anchored numbers.
using System;
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public sealed class GameView : MonoBehaviour
    {
        public Sim Sim { get; private set; }
        public Camera Cam { get; private set; }
        public CameraRig Rig { get; private set; }
        public FountainView Fountain { get; private set; }
        public FX Fx { get; private set; }
        public Stations Stations { get; private set; }
        public ViewContext Ctx { get; private set; }
        readonly WorldBuilder world = new WorldBuilder();
        readonly Dictionary<string, MachineVisual> machines = new Dictionary<string, MachineVisual>();
        WishOrbs wishes;
        GoldenPennies goldens;
        RatView rat;
        WorkerView worker;
        Transform mallRoot, machineRoot;
        GameObject treasure;
        float clumpAcc, sortFlyAcc, autoSellCooldown;
        readonly float[] stageKick = new float[3];

        /// <summary>World-anchored popup: position, text, colour, scale.</summary>
        public event Action<Vector3, string, Color, float> FloatText;
        /// <summary>Something noteworthy happened that audio might want: name, world position.</summary>
        public event Action<string, Vector3> Cue;

        static Color C(uint hex) => MeshKit.Hex(hex);

        public void Init(Sim sim, Camera cam)
        {
            Sim = sim;
            Cam = cam;
            Rig = cam.gameObject.GetComponent<CameraRig>() ?? cam.gameObject.AddComponent<CameraRig>();
            Rig.Init(cam);
            Ctx = new ViewContext { Sim = sim };
            Fx = new FX();
            Fx.Init(transform);
            Ctx.Fx = Fx;
            Fountain = new FountainView();
            Ctx.Fountain = Fountain;
            Stations = new Stations();
            wishes = new WishOrbs();
            wishes.Init(transform, Ctx, cam);
            wishes.SourcePoint = WishSource;
            wishes.CompressorIntake = () => machines.TryGetValue("compressor", out var mv) && mv.Shown > 0 ? ((CompressorVisual)mv).Intake : new Vector3(0, 12, 20);
            goldens = new GoldenPennies();
            goldens.Init(transform, Ctx, cam);
            rat = new RatView();
            rat.Init(transform, Ctx);
            Hook();
            RebuildMall();
        }

        void Hook()
        {
            Sim.OnDig += OnDig;
            Sim.OnProcessed += (w, s) => { if (w > 0) stageKick[1] = 1; if (s > 0) stageKick[2] = 1; };
            Sim.OnWishSpawned += w => { wishes.Spawn(w); Cue?.Invoke("wish_spawn", wishes.PositionOf(w.Uid)); };
            Sim.OnWishCaught += (w, v, first) =>
            {
                var p = wishes.PositionOf(w.Uid);
                wishes.Caught(w);
                FloatText?.Invoke(p + Vector3.up * 0.8f, "+" + Fmt.Money(v), RarityColors.Orb[(int)w.Def.Rarity], 1.2f + (int)w.Def.Rarity * 0.15f);
            };
            Sim.OnWishCompressed += (w, v) =>
            {
                wishes.Compressed(w);
                if (machines.TryGetValue("compressor", out var mv) && mv is CompressorVisual cv) cv.Pulse();
            };
            Sim.OnWishEscaped += w => wishes.Escaped(w);
            Sim.OnRelicFound += (r, v, first) =>
            {
                Vector3 at = Stations.TrayTop + Vector3.up * 0.5f;
                Fx.Glint(at, Color.white, 2.5f, 3, 0.4f);
                Fx.Sparks(at, new Color(1f, 0.85f, 0.4f), 16, 4f);
                if (r.Rarity >= Rarity.Epic) Fx.Confetti(at, 50, 7);
                Fx.Fly(at, () => Stations.VendingSlot, Loot.Tinted(r.Shape, MeshKit.Hex(r.Color)), 3f, 1.1f, 3f);
                FloatText?.Invoke(at + Vector3.up, "+" + Fmt.Money(v), new Color(1f, 0.85f, 0.35f), 1.3f);
            };
            Sim.OnStratumReached += s =>
            {
                Fountain.SetStratum(Sim.Mall, s, false);
                Rig.Shake(0.6f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2 / 16;
                    Fx.Dust(Fountain.SurfacePoint(Mathf.Cos(a) * 7f, Mathf.Sin(a) * 7f), MeshKit.Hex(Sim.Mall.Strata[s].Color), 3, 1.4f, 0.8f, 2f);
                }
            };
            Sim.OnMallCleared += OnCleared;
            Sim.OnSold += OnSold;
            Sim.OnGoldenSpawned += g => { goldens.Spawn(g); Cue?.Invoke("golden_spawn", goldens.PositionOf(g.Uid)); };
            Sim.OnGoldenClaimed += (g, title, detail) => goldens.Remove(g.Uid, true);
            Sim.OnGoldenMissed += g => goldens.Remove(g.Uid, false);
            Sim.OnRatSpawned += r => { rat.Spawn(r); Cue?.Invoke("rat_spawn", rat.Position); };
            Sim.OnRatCaught += r => rat.Remove(true);
            Sim.OnPurchase += OnPurchase;
            Sim.OnPrestige += () => RebuildMall();
        }

        Vector3 WishSource()
        {
            var sources = new List<MachineVisual>();
            foreach (var mv in machines.Values)
                if (mv.Def.Stage == Stage.Wash && !mv.Def.IsCompressor && !mv.Def.IsMega && mv.Shown > 0) sources.Add(mv);
            if (sources.Count == 0) return Fountain.RandomSurfacePoint(2, 6) + Vector3.up * 0.5f;
            var pick = sources[UnityEngine.Random.Range(0, sources.Count)];
            return pick.RandomCopyPosition() + Vector3.up * 2.2f;
        }

        public void RebuildMall()
        {
            var mall = Sim.Mall;
            Ctx.Mall = mall;
            if (mallRoot != null) Destroy(mallRoot.gameObject);
            mallRoot = new GameObject("Mall").transform;
            mallRoot.SetParent(transform, false);
            world.Build(mall, mallRoot, Sim.S.mallIndex * 97 + 5);
            Fountain.Build(mall, mallRoot);
            Fountain.SetDepth(Sim.DepthFrac, Sim.MallCleared, true);
            Fountain.SetStratum(mall, Sim.Stratum, true);
            Stations.Build(mallRoot, Ctx);
            machineRoot = new GameObject("Machines").transform;
            machineRoot.SetParent(mallRoot, false);
            machines.Clear();
            foreach (var def in Content.Machines)
            {
                var mv = MakeVisual(def.Id);
                if (mv == null) continue;
                mv.Init(def, machineRoot, Ctx);
                machines[def.Id] = mv;
            }
            wishes.Clear();
            goldens.Clear();
            rat.Remove(false);
            worker = new WorkerView();
            worker.Init(mallRoot, Ctx);
            worker.SetTool(Sim.S.tool);
            SyncCounts();
            if (treasure != null) Destroy(treasure);
            if (Sim.MallCleared) SpawnTreasure(false);
            Cam.backgroundColor = world.FogColor * 0.8f;
            Rig.ResetView();
        }

        static MachineVisual MakeVisual(string id)
        {
            switch (id)
            {
                case "pogo": return new PogoVisual();
                case "walkers": return new WalkersVisual();
                case "jackhammer": return new JackhammerVisual();
                case "claw": return new ClawVisual();
                case "borer": return new BorerVisual();
                case "tumbler": return new TumblerVisual();
                case "dishwasher": return new DishwasherVisual();
                case "acid": return new AcidVisual();
                case "carwash": return new CarwashVisual();
                case "jacuzzi": return new JacuzziVisual();
                case "pigeons": return new PigeonVisual();
                case "coinstar": return new CoinStarVisual();
                case "lasers": return new LaserVisual();
                case "prizebots": return new PrizeBotVisual();
                case "sieve": return new SieveVisual();
                case "compressor": return new CompressorVisual();
                case "carousel": return new CarouselVisual();
                case "slots": return new SlotsVisual();
                case "wishengine": return new WishEngineVisual();
            }
            return null;
        }

        void SyncCounts()
        {
            bool anyWash = false, anySort = false;
            foreach (var kv in machines)
            {
                int n = Sim.MachineCount(kv.Key);
                kv.Value.SetCount(n);
                var d = kv.Value.Def;
                if (n > 0 && d.Stage == Stage.Wash && !d.IsCompressor && !d.IsMega) anyWash = true;
                if (n > 0 && d.Stage == Stage.Sort && !d.IsMega) anySort = true;
            }
            Stations.SetBeltsVisible(anyWash, anySort);
        }

        void OnPurchase(string id)
        {
            if (machines.TryGetValue(id, out var mv))
            {
                int before = mv.Shown;
                SyncCounts();
                Vector3 at = mv.Shown > before ? mv.LastCopyPosition() : mv.RandomCopyPosition();
                Fx.Dust(at, new Color(0.9f, 0.9f, 0.85f), 8, 1.2f, 1f, 1.5f);
                Fx.Glint(at + Vector3.up * 1.5f, Color.white, 2f, 2, 0.6f);
                if (id == "slots" && mv is SlotsVisual sv) sv.Pull();
            }
            else if (Content.ToolIndex.ContainsKey(id))
            {
                worker.SetTool(Sim.S.tool);
            }
        }

        void OnCleared()
        {
            SpawnTreasure(true);
            worker.Celebrate();
            Rig.Shake(1f);
        }

        void SpawnTreasure(bool fanfare)
        {
            if (treasure != null) Destroy(treasure);
            var mall = Sim.Mall;
            ItemShape shape = ItemShape.Coin;
            Color col = new Color(1f, 0.8f, 0.3f);
            switch (mall.Id)
            {
                case "crestview": col = C(0xC77B43); break;
                case "neongalaxy": col = C(0xFFD34D); break;
                case "aurelia": shape = ItemShape.Paper; col = C(0xE8ECF2); break;
                case "skyport": shape = ItemShape.Cube; col = C(0x2F4F9F); break;
                case "luckylagoon": shape = ItemShape.Cube; col = C(0xF5F5F5); break;
                case "eternity": col = C(0x9FF0FF); break;
            }
            treasure = new GameObject("Treasure");
            treasure.transform.SetParent(mallRoot, false);
            var item = Loot.MakeItemMesh(shape, col, 7f, treasure.transform);
            item.transform.localRotation = Quaternion.Euler(70, 0, 0);
            var halo = new GameObject("Halo");
            halo.transform.SetParent(treasure.transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
            var mr = halo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = Mats.NewGlow(TexKit.SoftDot, 2.6f, new Color(1f, 0.9f, 0.55f));
            halo.transform.localScale = Vector3.one * 5f;
            var beam = new MeshKit();
            beam.Cylinder(new Vector3(0, 6, 0), 0.6f, 12, 16, Color.white, false);
            var bgo = beam.Build("Beam", treasure.transform, false);
            bgo.GetComponent<MeshRenderer>().sharedMaterial = Mats.NewGlow(TexKit.SoftDot, 0.6f, new Color(1f, 0.9f, 0.6f, 0.5f));
            treasure.transform.position = new Vector3(0, FountainView.BasinFloor + 1.6f, -2.4f);
            if (fanfare)
            {
                Fx.Confetti(treasure.transform.position + Vector3.up * 2, 150, 10);
                Fx.CoinShower(treasure.transform.position, new[] { col, Color.white }, 40, 9, 1.4f);
            }
        }

        void OnDig(double amount, bool manual)
        {
            if (manual) return;   // manual digs are visualised at the click point in Click()
        }

        void OnSold(double amount, int source)
        {
            if (source == 3)
            {
                if (autoSellCooldown > 0) return;
                autoSellCooldown = 0.6f;
            }
            Stations.FlashSale(amount > Sim.RewardIncome * 30);
            int n = Mathf.Clamp(1 + (int)Math.Log10(amount + 1), 1, 7);
            if (source == 1 || source == 2) n = 2;
            Vector3 from = source == 1 ? Stations.HopperTop : source == 2 ? Stations.TrayTop : Stations.PocketTop;
            for (int i = 0; i < n; i++)
                Fx.Fly(from + UnityEngine.Random.insideUnitSphere * 0.3f, () => Stations.VendingSlot, Loot.Tinted(ItemShape.Coin, i % 2 == 0 ? C(0xE0B040) : C(0xC9CDD2)), 2.4f, 0.55f + i * 0.06f, 2.5f,
                    i == n - 1 ? (Action)(() => Fx.Glint(Stations.VendingSlot, new Color(1f, 0.95f, 0.6f), 1.2f)) : null);
            if (source != 3) FloatText?.Invoke(Stations.Vending.position + Vector3.up * 3.6f, "+" + Fmt.Money(amount), new Color(0.55f, 1f, 0.55f), 1.25f);
        }

        // ───────────────────────────── clicking ─────────────────────────────

        public enum ClickKind { None, Crust, Wish, Golden, Rat, Vending, Machine }

        public struct ClickResult
        {
            public ClickKind Kind;
            public string Id;
            public int Uid;
            public Vector3 Point;
        }

        public ClickResult Pick(Vector2 screen)
        {
            var res = new ClickResult();
            var ray = Cam.ScreenPointToRay(screen);
            var hits = Physics.RaycastAll(ray, 400f);
            int best = 0;
            foreach (var h in hits)
            {
                var ct = h.collider.GetComponent<ClickTarget>();
                if (ct == null) continue;
                int pri = ct.Kind == "wish" ? 5 : ct.Kind == "golden" ? 4 : ct.Kind == "rat" ? 3 : ct.Kind == "vending" ? 2 : 1;
                if (pri > best)
                {
                    best = pri;
                    res.Kind = ct.Kind == "wish" ? ClickKind.Wish : ct.Kind == "golden" ? ClickKind.Golden : ct.Kind == "rat" ? ClickKind.Rat : ct.Kind == "vending" ? ClickKind.Vending : ClickKind.Machine;
                    res.Id = ct.Id;
                    res.Uid = ct.Uid;
                    res.Point = h.point;
                }
            }
            if (best >= 3) return res;
            if (Fountain.Raycast(ray, out var p))
            {
                // the crust wins over machines standing behind it, but not over wishes/pennies/rats
                if (best == 0 || (res.Point - ray.origin).sqrMagnitude > (p - ray.origin).sqrMagnitude)
                {
                    res.Kind = ClickKind.Crust;
                    res.Point = p;
                }
            }
            return res;
        }

        /// <summary>Visualise a manual dig that removed `amount` items at `point`.</summary>
        public void ShowDig(Vector3 point, double amount, bool loose, double value)
        {
            var mall = Sim.Mall;
            var st = Sim.CurStratum;
            float mag = Mathf.Clamp01((float)Math.Log10(amount + 1) / 8f);
            Fountain.Dig(point, 0.3f + mag * 0.7f, 1.2f + mag * 1.4f);
            worker.DigAt(point);
            Fx.Ripple(point, new Color(1f, 0.95f, 0.8f, 0.8f), 1.6f + mag * 2f);
            Fx.Dust(point, MeshKit.Hex(st.Color), 3 + (int)(mag * 8), 0.8f + mag, 0.7f, 1.2f + mag);
            var colors = new Color[Mathf.Min(4, mall.Items.Length)];
            for (int i = 0; i < colors.Length; i++) colors[i] = MeshKit.Hex(mall.Items[i].Color);
            Fx.CoinShower(point + Vector3.up * 0.1f, colors, 2 + (int)(mag * 16), 3.5f + mag * 3f);
            int flyers = Mathf.Clamp(1 + (int)Math.Log10(amount + 1), 1, 6);
            if (Fx.FlyerCount > 60) flyers = 1;
            for (int i = 0; i < flyers; i++)
            {
                var item = mall.Items[UnityEngine.Random.Range(0, mall.Items.Length)];
                Func<Vector3> dest = loose ? (Func<Vector3>)(() => Stations.PocketTop) : () => Stations.HopperTop;
                var mesh = loose ? Loot.Tinted(item.Shape, MeshKit.Hex(item.Color)) : Loot.Tinted(ItemShape.Wad, MeshKit.Hex(st.Color) * 0.8f);
                Fx.Fly(point + UnityEngine.Random.insideUnitSphere * 0.3f, dest, mesh, 2.4f, 0.7f + i * 0.07f, 4f + UnityEngine.Random.value * 2f);
            }
            if (loose) FloatText?.Invoke(point + Vector3.up * 0.8f, "+" + Fmt.Money(value), new Color(1f, 0.92f, 0.55f), 0.95f + mag * 0.4f);
            else FloatText?.Invoke(point + Vector3.up * 0.8f, "+" + Fmt.Num(amount) + " gunk", new Color(0.95f, 0.8f, 0.6f), 0.9f + mag * 0.4f);
            if (mag > 0.7f) Rig.Shake(0.15f);
        }

        public void WhackMachine(string id)
        {
            if (machines.TryGetValue(id, out var mv))
            {
                mv.Whack();
                Fx.Sparks(mv.RandomCopyPosition() + Vector3.up * 1.5f, new Color(1f, 0.9f, 0.6f), 10, 4f);
            }
        }

        public Vector3 WishPosition(int uid) => wishes.PositionOf(uid);
        public Vector3 GoldenPosition(int uid) => goldens.PositionOf(uid);
        public Vector3 RatPosition => rat.Position;
        public Vector3 VendingPosition => Stations.Vending != null ? Stations.Vending.position : Vector3.zero;

        public bool TryGetMachineAnchor(string id, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (!machines.TryGetValue(id, out var mv) || mv.Shown == 0) return false;
            pos = mv.RandomCopyPosition();
            return true;
        }

        // ───────────────────────────── per-frame ─────────────────────────────

        void Update()
        {
            if (Sim == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float time = Time.time;
            Ctx.Time = time;

            stageKick[0] = Sim.DigRateNow > 0 ? 1 : 0;
            for (int s = 0; s < 3; s++)
            {
                float target = stageKick[s];
                Ctx.Activity[s] = Mathf.MoveTowards(Ctx.Activity[s], target, dt * (target > Ctx.Activity[s] ? 4f : 0.8f));
                if (s > 0) stageKick[s] = Mathf.Max(0, stageKick[s] - dt * 1.5f);
            }

            Fountain.SetDepth(Sim.DepthFrac, Sim.MallCleared, false);
            Fountain.Update(dt);
            Rig.SetDepth(Fountain.SurfaceY, (float)Sim.DepthFrac);
            foreach (var mv in machines.Values)
                mv.Tick(dt, time, Ctx.Activity[(int)mv.Def.Stage]);
            Stations.Update(dt, time, Sim, Ctx.Activity[1], Ctx.Activity[2]);
            wishes.Update(dt, time);
            goldens.Update(dt, time);
            rat.Update(dt, time, Sim.Rat);
            worker.Update(dt, time);
            world.Animate(time, dt);
            Fx.Update(dt);
            autoSellCooldown -= dt;

            // automatic digging: clumps arc from the crust to the hopper (or pocket in loose layers)
            if (Sim.DigRateNow > 0)
            {
                clumpAcc += dt * Mathf.Min(3.5f, (float)Math.Log10(1 + Sim.DigRateNow) * 0.6f);
                while (clumpAcc >= 1)
                {
                    clumpAcc -= 1;
                    var from = Fountain.RandomSurfacePoint(1.6f, 7.2f);
                    bool loose = Sim.CurStratum.Loose;
                    var st = Sim.CurStratum;
                    var mesh = loose ? Loot.Tinted(ItemShape.Coin, C(0xC77B43)) : Loot.Tinted(ItemShape.Wad, MeshKit.Hex(st.Color) * 0.8f);
                    Fx.Fly(from, loose ? (Func<Vector3>)(() => Stations.PocketTop) : () => Stations.HopperTop, mesh, 2.2f, 1.0f, 5f);
                    Fx.Dust(from, MeshKit.Hex(st.Color), 2, 0.6f, 0.5f, 0.8f);
                }
            }
            if (Ctx.Activity[2] > 0.2f)
            {
                sortFlyAcc += dt * Ctx.Activity[2] * 1.5f;
                while (sortFlyAcc >= 1)
                {
                    sortFlyAcc -= 1;
                    Fx.Fly(Stations.TrayTop, () => Stations.PocketTop, Loot.Tinted(ItemShape.Coin, UnityEngine.Random.value < 0.5f ? C(0xE0B040) : C(0xC9CDD2)), 2.2f, 0.8f, 3f);
                }
            }
        }
    }
}
