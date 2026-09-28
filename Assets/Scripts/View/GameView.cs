// Owns every 3D piece of the first-person game: the mall, the fountain, the COIN-O-MATIC, the
// loose items, the player body and hands. Each frame it moves the player from an FPInput,
// works out what the crosshair is on, and turns clicks / E presses into Sim calls.
using System;
using System.Collections.Generic;
using UnityEngine;
using WishExtractor.Core;

namespace WishExtractor.View
{
    public enum TargetKind { None, Item, Kiosk, Terminal, Wish, Board, Building, Crust, Rival }

    public struct Target
    {
        public TargetKind Kind;
        public int Uid;
        public Vector3 Point;
        public float Distance;
    }

    public sealed class GameView : MonoBehaviour
    {
        public Sim Sim { get; private set; }
        public Camera Cam { get; private set; }
        public FirstPersonController Player { get; private set; }
        public FountainView Fountain { get; private set; }
        public FX Fx { get; private set; }
        public Kiosk Kiosk { get; private set; }
        public ItemRenderer Items { get; private set; }
        public Hands Hands { get; private set; }
        public CrowdView Crowd { get; private set; }
        public WishView WishOrbs { get; private set; }
        public FountainBoard Board { get; private set; }
        public TerminalProp Terminal { get; private set; }
        public FountainDecor Decor { get; private set; }
        public FactoryView Factory { get; private set; }
        public BuildMode Build { get; private set; }
        public HazardsView Hazards { get; private set; }
        bool decorPrimed;
        float wallDistNow = 999;
        int hoverBuilding = -1;
        /// <summary>Hotbar 2: the dig tool is out.</summary>
        public bool DigMode { get; private set; }
        float swingCooldown;
        GameObject treasure;
        /// <summary>A swing of the dig tool landed: point, scoops removed.</summary>
        public event Action<Vector3, double> Dug;
        /// <summary>The player pressed E on the Maintenance Terminal.</summary>
        public event Action OpenTerminal;
        Material areaMat;
        float vacuumAcc, detectAcc;
        public ViewContext Ctx { get; private set; }
        /// <summary>A one-line message for the HUD (e.g. "not enough cash").</summary>
        public event Action<string> Message;
        public Target Current;
        public bool Wading { get; private set; }
        /// <summary>What the aim ray hit last frame (diagnostics).</summary>
        public string LastHit { get; private set; }
        readonly WorldBuilder world = new WorldBuilder();
        Transform mallRoot;
        float grabCooldown, shake;
        readonly List<int> areaPick = new List<int>();

        /// <summary>World-anchored popup: position, text, colour, scale.</summary>
        public event Action<Vector3, string, Color, float> FloatText;

        public const float KioskReach = 3.2f;

        public void Init(Sim sim, Camera cam)
        {
            Sim = sim;
            Cam = cam;
            Ctx = new ViewContext { Sim = sim };
            Fx = new FX();
            Fx.Init(transform);
            Ctx.Fx = Fx;
            Fountain = new FountainView();
            Ctx.Fountain = Fountain;
            Items = new ItemRenderer();
            Items.Init(sim, Fountain);

            var pgo = new GameObject("Player") { layer = Layers.IgnoreRaycast };
            Player = pgo.AddComponent<FirstPersonController>();
            Player.Init(cam);
            Hands = new Hands();
            Hands.Build(cam);

            Sim.OnPickup += (t, n, v) => Hands.Grab();
            Sim.OnDeposit += (cash, n, joke) =>
            {
                Hands.Push();
                Kiosk.Count(cash, n);
                Fx.CoinShower(Kiosk.Funnel, new[] { new Color(0.78f, 0.48f, 0.26f), new Color(0.8f, 0.8f, 0.82f) }, Mathf.Clamp(n, 3, 40), 2.2f, 0.5f);
                Fx.Glint(Kiosk.Funnel + Vector3.up * 0.2f, new Color(1f, 0.95f, 0.6f), 1.2f, 3, 0.3f);
                FloatText?.Invoke(Kiosk.Front + Vector3.up * 0.9f, "+" + Fmt.Money(cash), new Color(0.55f, 1f, 0.55f), 1.3f);
            };
            Sim.OnDepositEmpty += line => Kiosk.Say(line);

            Crowd = new CrowdView();
            Crowd.Init(transform, sim);
            WishOrbs = new WishView();
            WishOrbs.Init(transform, Ctx, cam);
            Sim.OnWishSpawned += w => WishOrbs.Spawn(w);
            Sim.OnWishCaught += (w, cash, tokens, first) =>
            {
                var p = WishOrbs.PositionOf(w.Uid);
                WishOrbs.Caught(w);
                FloatText?.Invoke(p + Vector3.up * 0.3f, $"+{Fmt.Money(cash)}  +{tokens:0} ✦", RarityColors.Orb[(int)w.Def.Rarity], 1.1f + (int)w.Def.Rarity * 0.12f);
            };
            Sim.OnWishEscaped += w => WishOrbs.Escaped(w);
            Sim.OnLanded += it =>
            {
                var p = new Vector3(it.X, Fountain.WaterY + 0.01f, it.Z);
                var def = Content.Items[it.Type];
                float big = def.Cat == ItemCat.Coin ? 0.5f : Mathf.Clamp(def.Scale, 0.8f, 2.5f);
                Fx.Ripple(p, new Color(0.9f, 0.97f, 1f, 0.7f), 0.8f + big * 0.6f, 0.7f);
                Fx.Sparks(p, new Color(0.8f, 0.92f, 1f), def.Cat == ItemCat.Coin ? 5 : 16, 2.2f + big, 0.07f, 0.5f);
            };
            Sim.OnTechBought += t => { if (t.Branch == TechBranch.Fountain) { Board.Flash(); FountainCelebrate(); } };

            Factory = new FactoryView();
            Factory.Init(transform, Ctx);
            Hazards = new HazardsView();
            Hazards.Init(transform, Ctx);
            Build = new BuildMode();
            Build.Init(sim, transform);
            Build.Denied += why => Message?.Invoke(why);
            Build.Placed += b =>
            {
                var c = FactoryView.WorldCenter(b);
                Fx.Dust(c + Vector3.up * 0.2f, new Color(0.9f, 0.9f, 0.85f), b.Def.IsBelt ? 2 : 8, 0.8f, 0.6f, 1f);
                if (!b.Def.IsBelt) Fx.Glint(c + Vector3.up * 1.2f, Color.white, 1.5f, 2, 0.5f);
            };
            Sim.OnStratumReached += s =>
            {
                Fountain.SetStratum(Sim.Mall, s, false);
                Shake(0.5f);
                for (int i = 0; i < 16; i++)
                {
                    float a = i * Mathf.PI * 2 / 16;
                    Fx.Dust(Fountain.SurfacePoint(Mathf.Cos(a) * 6.8f, Mathf.Sin(a) * 6.8f), MeshKit.Hex(Sim.Mall.Strata[s].Color), 3, 1.4f, 0.8f, 2f);
                }
            };
            Sim.OnMallCleared += () => { SpawnTreasure(true); Shake(1f); };
            Sim.OnPrestige += () => { RebuildMall(); PlacePlayer(new SaveData()); };
            Sim.OnWishCompressed += (w, v) =>
            {
                Building comp = Sim.Buildings.Find(b => b.Def.Process == "compress");
                WishOrbs.Compressed(w, comp != null ? FactoryView.WorldCenter(comp) + Vector3.up * 1.8f : new Vector3(0, 10, 0));
            };
            Sim.OnHopperSold += (b, cash, n) =>
            {
                if (UnityEngine.Random.value < 0.25f)
                    FloatText?.Invoke(FactoryView.WorldCenter(b) + Vector3.up * 2f, "+" + Fmt.Money(cash), new Color(0.55f, 1f, 0.55f), 0.8f);
            };
            RebuildMall();
        }

        void FountainCelebrate()
        {
            for (int i = 0; i < 10; i++)
            {
                float a = i * Mathf.PI * 2 / 10;
                Fx.Glint(new Vector3(Mathf.Cos(a) * 6.5f, Fountain.WaterY + 0.6f, Mathf.Sin(a) * 6.5f), new Color(1f, 0.95f, 0.7f), 1.4f, 2, 0.4f);
            }
            Fx.Confetti(new Vector3(0, 4.5f, 0), 70, 7);
            Shake(0.2f);
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
            treasure = null;
            if (Sim.MallCleared) SpawnTreasure(false);
            Kiosk = new Kiosk();
            Kiosk.Build(mallRoot);
            Board = new FountainBoard();
            Board.Build(mallRoot, Sim);
            Terminal = new TerminalProp();
            Terminal.Build(mallRoot);
            Decor = new FountainDecor();
            Decor.Build(mallRoot, Ctx);
            decorPrimed = false;
            Crowd?.Clear();
            WishOrbs?.Clear();
            Factory?.Clear();
            Cam.backgroundColor = world.FogColor * 0.8f;
            Fountain.Update(0.016f);
        }

        /// <summary>Spawn the player where the save says (or at the entrance).</summary>
        public void PlacePlayer(SaveData s)
        {
            var p = new Vector3(s.px, s.py, s.pz);
            if (float.IsNaN(p.x) || p.sqrMagnitude < 0.01f) p = new Vector3(0, 0.05f, -18f);
            Player.Place(p + Vector3.up * 0.05f, s.yaw, s.pitch);
        }

        public void StorePose(SaveData s)
        {
            var p = Player.Feet;
            s.px = p.x; s.py = p.y; s.pz = p.z;
            s.yaw = Player.Yaw; s.pitch = Player.Pitch;
        }

        public void ApplySettings(SaveData s)
        {
            Player.Fov = s.fov;
            Player.HeadBob = s.headBob;
        }

        public void Shake(float amount) => shake = Mathf.Max(shake, amount);

        // ───────────────────────────── per-frame ─────────────────────────────

        /// <summary>Move the player and handle interaction. frozen = a menu is open.</summary>
        public void Step(FPInput input, float dt, bool frozen)
        {
            var carry = Sim.CarryDef;
            Wading = Fountain.InWater(Player.Feet);
            Player.SpeedMult = (float)Sim.WalkSpeedMult * carry.SpeedMult * (Wading ? Balance.WadeMult : 1f);
            Player.CanJump = !carry.NoJump;
            Player.Tick(frozen ? default : input, dt);
            Sim.S.distance += Player.Moved;
            if (Player.Feet.y < FountainView.BasinFloor - 5) PlacePlayer(new SaveData());

            if (!frozen && input.Hotbar == 3 && Build.SetActive(true)) DigMode = false;
            if (!frozen && input.Hotbar == 1) { Build.SetActive(false); DigMode = false; }
            if (!frozen && input.Hotbar == 2)
            {
                if (Sim.DigTier <= 0) Message?.Invoke("You need something to dig with. Sandbox Shovel: Tools tab at the Maintenance Terminal.");
                else { Build.SetActive(false); DigMode = true; }
            }
            if (!frozen && input.Catalogue && !Build.Active && Build.SetActive(true)) DigMode = false;
            if (Build.Active) DigMode = false;

            UpdateTarget();
            grabCooldown -= dt;
            Build.Tick(frozen ? default : input, Player.AimRay, wallDistNow, hoverBuilding, frozen);
            if (frozen) return;
            if (Build.Active)
            {
                // in build mode the mouse builds; E still works on the kiosk, terminal, easel and wishes
                if (input.Interact && Current.Kind != TargetKind.Item && Current.Kind != TargetKind.Building) Use();
                return;
            }
            swingCooldown -= dt;
            if (DigMode)
            {
                if (input.Interact && Current.Kind != TargetKind.Crust) Use();
                if ((input.PrimaryDown || input.Primary) && swingCooldown <= 0 && Current.Kind == TargetKind.Crust) SwingAt(Current.Point);
                return;
            }
            if (input.Interact || input.PrimaryDown) Use();
            else if (input.Primary && Current.Kind == TargetKind.Item && grabCooldown <= 0) Use();
        }

        void UpdateTarget()
        {
            var ray = Player.AimRay;
            float reach = Sim.Reach;
            Current = default;
            Items.HighlightUid = -1;
            Items.AreaHighlight.Clear();
            float wallDist = 999f;
            LastHit = null;
            int solid = ~((1 << Layers.IgnoreRaycast) | (1 << WishView.Layer));
            bool hitSomething = Physics.Raycast(ray, out var hit, 60f, solid, QueryTriggerInteraction.Ignore);
            if (hitSomething) wallDist = hit.distance;
            wallDistNow = wallDist;
            hoverBuilding = -1;
            if (hitSomething && hit.distance < 14f)
            {
                var bct = hit.collider.GetComponent<ClickTarget>();
                if (bct != null && bct.Kind == "building")
                {
                    hoverBuilding = bct.Uid;
                    // standing on a belt shouldn't hide its items from the build ghost's floor ray
                    if (Sim.FindBuilding(bct.Uid)?.Def.IsBelt == true) wallDistNow = 999;
                }
            }
            // floating wishes (and Chad) first: they're triggers on their own layer, and generous to aim at
            if (Physics.SphereCast(ray, 0.12f, out var wh, Mathf.Min(Balance.WishReach, wallDist), 1 << WishView.Layer, QueryTriggerInteraction.Collide))
            {
                var wct = wh.collider.GetComponent<ClickTarget>();
                if (wct != null && wct.Kind == "wish")
                {
                    Current = new Target { Kind = TargetKind.Wish, Uid = wct.Uid, Point = wh.point, Distance = wh.distance };
                    return;
                }
                if (wct != null && wct.Kind == "rival")
                {
                    Current = new Target { Kind = TargetKind.Rival, Point = wh.point, Distance = wh.distance };
                    return;
                }
            }
            if (hitSomething)
            {
                LastHit = hit.collider.name + " @" + hit.distance.ToString("0.00");
                var ct = hit.collider.GetComponentInParent<ClickTarget>();
                if (ct != null && hit.distance <= KioskReach)
                {
                    if (ct.Kind == "kiosk") { Current = new Target { Kind = TargetKind.Kiosk, Point = hit.point, Distance = hit.distance }; return; }
                    if (ct.Kind == "board") { Current = new Target { Kind = TargetKind.Board, Point = hit.point, Distance = hit.distance }; return; }
                    if (ct.Kind == "terminal") { Current = new Target { Kind = TargetKind.Terminal, Point = hit.point, Distance = hit.distance }; return; }
                }
            }
            if (DigMode)
            {
                if (hitSomething && hit.distance <= Balance.DigReach + 0.6f && hit.collider.name == "Crust Collider")
                    Current = new Target { Kind = TargetKind.Crust, Point = hit.point, Distance = hit.distance };
                return;
            }
            var grab = Sim.Grab;
            if (grab.Area > 0 && wallDist <= reach + 0.5f && FountainView.InBasin(hit.point))
            {
                Items.PickArea(hit.point, grab.Area, ray.origin, reach, areaPick, 400);
                if (areaPick.Count > 0)
                {
                    foreach (int u in areaPick) Items.AreaHighlight.Add(u);
                    Current = new Target { Kind = TargetKind.Item, Uid = areaPick[0], Point = hit.point, Distance = wallDist };
                    return;
                }
            }
            var it = Items.PickNearest(ray, reach, wallDist);
            if (it != null)
            {
                var p = Items.PositionOf(it);
                Items.HighlightUid = it.Uid;
                Current = new Target { Kind = TargetKind.Item, Uid = it.Uid, Point = p, Distance = Vector3.Distance(ray.origin, p) };
                return;
            }
            if (hoverBuilding >= 0 && hit.distance < 8f)
                Current = new Target { Kind = TargetKind.Building, Uid = hoverBuilding, Point = hit.point, Distance = hit.distance };
        }

        /// <summary>Act on the current target (E or left click).</summary>
        public void Use()
        {
            switch (Current.Kind)
            {
                case TargetKind.Item:
                {
                    float rate = Sim.Grab.Rate * (float)Sim.GrabRateMult;
                    grabCooldown = 1f / Mathf.Max(0.5f, rate);
                    if (Items.AreaHighlight.Count > 0)
                    {
                        var list = new List<int>(Items.AreaHighlight);
                        var first = Sim.FindLoose(list[0]);
                        Vector3 at = first != null ? Items.PositionOf(first) : Current.Point;
                        double before = Sim.CarriedValue;
                        int got = Sim.PickupMany(list);
                        if (got > 0) PickupFx(at, Sim.CarriedValue - before, got);
                    }
                    else
                    {
                        var it = Sim.FindLoose(Current.Uid);
                        if (it == null) break;
                        Vector3 at = Items.PositionOf(it);
                        double value = it.Value * Sim.ValueMult * Sim.CatRate(Content.Items[it.Type].Cat);
                        if (Sim.Pickup(it.Uid)) PickupFx(at, value, 1);
                    }
                    break;
                }
                case TargetKind.Kiosk:
                    Sim.Deposit();
                    break;
                case TargetKind.Wish:
                    Sim.CatchWish(Current.Uid);
                    break;
                case TargetKind.Terminal:
                    OpenTerminal?.Invoke();
                    break;
                case TargetKind.Rival:
                    Sim.ChaseRival();
                    break;
                case TargetKind.Board:
                {
                    int i = Sim.NextFountainTech();
                    if (i < 0) { Message?.Invoke("Every job on the plan is done. The Maintenance Terminal has more."); break; }
                    if (!Sim.CanAfford(i)) { Message?.Invoke($"{Content.Techs[i].Name} costs {Fmt.Money(Sim.TechCost(i))}. Go find some coins."); break; }
                    Sim.BuyTech(i);
                    break;
                }
            }
        }

        void SwingAt(Vector3 p)
        {
            var tool = Sim.DigTool;
            swingCooldown = 1f / Mathf.Max(0.5f, tool.Rate);
            Hands.Swing();
            if (Sim.MallCleared) { Message?.Invoke("Bare concrete! There's nothing left to dig. Sign the next contract."); return; }
            double scoops = Sim.SwingDig(p.x, p.z);
            var sp = Fountain.SurfacePoint(p.x, p.z);
            float mag = Mathf.Clamp01((float)Math.Log10(scoops + 1) / 2.5f);
            Fountain.Dig(sp, 0.12f + mag * 0.45f, 0.7f + mag * 1.3f);
            Color dirt = MeshKit.Hex(Sim.CurStratum.Color);
            Fx.Dust(sp + Vector3.up * 0.1f, dirt, 3 + (int)(mag * 8), 0.7f + mag, 0.6f, 1.2f + mag);
            if (Fountain.InWater(sp)) Fx.Ripple(new Vector3(sp.x, Fountain.WaterY + 0.01f, sp.z), new Color(0.9f, 0.95f, 1f, 0.6f), 0.9f + mag, 0.6f);
            Fx.CoinShower(sp + Vector3.up * 0.1f, new[] { dirt, dirt * 0.8f }, 2 + (int)(mag * 10), 2.5f + mag * 2, 0.6f);
            if (mag > 0.6f) Shake(0.12f);
            Dug?.Invoke(sp, scoops);
        }

        void SpawnTreasure(bool fanfare)
        {
            if (treasure != null) Destroy(treasure);
            var mall = Sim.Mall;
            ItemShape shape = ItemShape.Coin;
            Color col = new Color(1f, 0.8f, 0.3f);
            switch (mall.Id)
            {
                case "crestview": col = MeshKit.Hex(0xC77B43); break;
                case "neongalaxy": col = MeshKit.Hex(0xFFD34D); break;
                case "aurelia": shape = ItemShape.Paper; col = MeshKit.Hex(0xE8ECF2); break;
                case "skyport": shape = ItemShape.Cube; col = MeshKit.Hex(0x2F4F9F); break;
                case "luckylagoon": shape = ItemShape.Cube; col = MeshKit.Hex(0xF5F5F5); break;
                case "eternity": col = MeshKit.Hex(0x9FF0FF); break;
            }
            treasure = new GameObject("Treasure");
            treasure.transform.SetParent(mallRoot, false);
            var item = Loot.MakeItemMesh(shape, col, 5f, treasure.transform);
            item.transform.localRotation = Quaternion.Euler(70, 0, 0);
            var halo = new GameObject("Halo");
            halo.transform.SetParent(treasure.transform, false);
            halo.AddComponent<MeshFilter>().sharedMesh = FX.Quad;
            halo.AddComponent<MeshRenderer>().sharedMaterial = Mats.NewGlow(TexKit.SoftDot, 2.4f, new Color(1f, 0.9f, 0.55f));
            halo.transform.localScale = Vector3.one * 3.5f;
            var beam = new MeshKit();
            beam.Cylinder(new Vector3(0, 8, 0), 0.5f, 16, 16, Color.white, false);
            var bgo = beam.Build("Beam", treasure.transform, false);
            bgo.GetComponent<MeshRenderer>().sharedMaterial = Mats.NewGlow(TexKit.SoftDot, 0.6f, new Color(1f, 0.9f, 0.6f, 0.5f));
            treasure.transform.position = new Vector3(0, FountainView.BasinFloor + 1.4f, -2.4f);
            if (fanfare)
            {
                Fx.Confetti(treasure.transform.position + Vector3.up * 2, 150, 10);
                Fx.CoinShower(treasure.transform.position, new[] { col, Color.white }, 40, 9, 1.4f);
            }
        }

        void PickupFx(Vector3 at, double value, int count)
        {
            Fx.Glint(at + Vector3.up * 0.05f, new Color(1f, 0.95f, 0.7f), 0.5f, 1, 0.05f);
            if (Fountain.InWater(at)) Fx.Ripple(new Vector3(at.x, Fountain.WaterY + 0.01f, at.z), new Color(0.85f, 0.95f, 1f, 0.6f), 0.5f + count * 0.02f, 0.5f);
            string txt = count > 1 ? $"+{count}  {Fmt.Money(value)}" : "+" + Fmt.Money(value);
            FloatText?.Invoke(at + Vector3.up * 0.25f, txt, new Color(1f, 0.92f, 0.55f), 0.75f);
        }

        void Update()
        {
            if (Sim == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            float time = Time.time;
            Ctx.Time = time;
            Fountain.SetBeauty(Sim.TechLevel("fountain_scrub") > 0, Sim.TechLevel("fountain_jets") > 0, Sim.TechLevel("fountain_lights") > 0);
            Fountain.SetDepth(Sim.DepthFrac, Sim.MallCleared, false);
            Fountain.SetStratum(Sim.Mall, Sim.Stratum, false);
            Fountain.Update(dt);
            if (treasure != null)
            {
                treasure.transform.GetChild(0).localRotation = Quaternion.Euler(70, time * 60, 0);
                treasure.transform.Find("Halo").rotation = Cam.transform.rotation * Quaternion.Euler(-90, 0, 0);
            }
            world.Animate(time, dt);
            Kiosk.Update(dt, time);
            Board.Update(dt);
            Terminal.Update(dt, Sim);
            Decor.Sync(Sim, decorPrimed);
            decorPrimed = true;
            Decor.Update(dt, time);
            var feet = Player.Feet;
            Sim.PlayerX = feet.x;
            Sim.PlayerZ = feet.z;
            Sim.PlayerInWater = Wading;
            Hazards.Update(dt, time);
            Crowd.Update(dt, time);
            WishOrbs.Update(dt, time);
            Factory.Update(dt, time);
            Fx.Update(dt);

            // hands: bare hands show the top item; containers show how full they are
            Hands.SetCarryTier(Sim.CarryTier);
            if (DigMode) Hands.SetDigTool(Sim.DigTier);
            else Hands.SetTool(Build.Active ? 0 : Sim.GrabTier);
            AutoVacuum(dt, feet);
            DetectorGlints(dt, feet);
            if (Sim.CarryTier == 0) Hands.SetHeld(Sim.Carried.Count > 0 ? Sim.Carried[Sim.Carried.Count - 1].Type : -1);
            else { Hands.SetHeld(-1); Hands.SetFill(Sim.CarryUsed / (float)Mathf.Max(1, Sim.CarryCapacity)); }
            Hands.Update(dt, Player.BobPhase, Player.BobAmount);

            if (shake > 0)
            {
                shake = Mathf.Max(0, shake - dt * 2);
                Cam.transform.localPosition = UnityEngine.Random.insideUnitSphere * shake * 0.08f;
            }
            else Cam.transform.localPosition = Vector3.zero;
        }

        /// <summary>Shop-vac backpack and hopper suit: hoover up anything near your feet.</summary>
        void AutoVacuum(float dt, Vector3 feet)
        {
            float r = Sim.CarryDef.AutoRadius;
            if (r <= 0 || Sim.CarryFree <= 0) return;
            vacuumAcc += dt * 14f;
            if (vacuumAcc < 1) return;
            int n = (int)vacuumAcc;
            vacuumAcc -= n;
            Items.PickArea(feet + Vector3.up * 0.3f, r, feet + Vector3.up * 0.3f, r + 1f, areaPick, n);
            if (areaPick.Count == 0) return;
            var first = Sim.FindLoose(areaPick[0]);
            Vector3 at = first != null ? Items.PositionOf(first) : feet;
            double before = Sim.CarriedValue;
            int got = Sim.PickupMany(new List<int>(areaPick));
            if (got > 0)
            {
                Fx.Fly(at, () => Cam.transform.position + Cam.transform.forward * 0.4f - Vector3.up * 0.3f, Loot.Tinted(ItemShape.Coin, new Color(0.78f, 0.48f, 0.26f)), 0.8f, 0.3f, 0.2f);
                if (UnityEngine.Random.value < 0.3f) PickupFx(at, Sim.CarriedValue - before, got);
            }
        }

        /// <summary>The detector magnet (and better) makes valuable things glint, even underwater.</summary>
        void DetectorGlints(float dt, Vector3 feet)
        {
            if (Sim.GrabTier < 4) return;
            detectAcc += dt * 6;
            while (detectAcc >= 1)
            {
                detectAcc -= 1;
                if (Sim.Loose.Count == 0) return;
                var it = Sim.Loose[UnityEngine.Random.Range(0, Sim.Loose.Count)];
                var def = Content.Items[it.Type];
                bool rare = def.Cat == ItemCat.Oddity || def.Cat == ItemCat.Relic || (def.Cat == ItemCat.Coin && def.Tier >= 4);
                if (!rare) continue;
                var p = Items.PositionOf(it);
                if ((p - feet).sqrMagnitude > 14f * 14f) continue;
                Fx.Glint(p + Vector3.up * 0.1f, new Color(0.6f, 0.95f, 1f), 0.6f, 1, 0.05f);
            }
        }

        void LateUpdate()
        {
            if (Sim == null) return;
            Items.Render(Cam, Time.time, Factory.DrawBeltItems);
            // area tools: show the scoop circle where you're aiming
            var g = Sim.Grab;
            if (g.Area > 0 && Current.Kind == TargetKind.Item)
            {
                if (areaMat == null) areaMat = Mats.NewGlow(TexKit.Ring, 1.4f, new Color(0.6f, 1f, 0.85f));
                Vector3 c = Current.Point;
                c.y = Mathf.Max(Fountain.HeightAt(c.x, c.z), Fountain.WaterY) + 0.03f;
                var rp = new RenderParams(areaMat) { shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off, receiveShadows = false };
                Graphics.RenderMesh(rp, FX.Quad, 0, Matrix4x4.TRS(c, Quaternion.identity, Vector3.one * g.Area * 2.3f));
            }
        }
    }
}
