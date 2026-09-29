// Entry point. Builds the camera, simulation, 3D view, UI and audio; turns keyboard and mouse
// into an FPInput for the first-person controller; turns sim events into sounds, receipts and
// toasts; autosaves; and runs the test harnesses (-autotour, -uitest, -loadtest).
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WishExtractor.Audio;
using WishExtractor.Core;
using WishExtractor.UI;
using WishExtractor.View;

namespace WishExtractor.Game
{
    public sealed class GameRoot : MonoBehaviour
    {
        Sim sim;
        GameView view;
        Camera cam;
        BloomFX bloom;
        AudioHub sfx;
        HUD hud;
        Popups pops;
        Modals modals;
        TerminalPanel terminal;
        BuildMenu buildMenu;
        bool AnyMenu => modals.IsOpen || terminal.IsOpen || buildMenu.IsOpen;
        bool swallowInput;
        Canvas uiCanvas, popCanvas, modalCanvas;
        float autosave;
        bool devMode, touring, testing;
        bool pendingFirstIntro;
        string shotDir;
        /// <summary>When set, the test harness drives the player instead of the keyboard and mouse.</summary>
        FPInput? scripted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameRoot>() == null) new GameObject("Wish Extractor").AddComponent<GameRoot>();
        }

        static bool HasArg(string a) => Array.IndexOf(Environment.GetCommandLineArgs(), a) >= 0;

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            devMode = Application.isEditor || HasArg("-dev");
            touring = HasArg("-autotour");
            testing = touring || HasArg("-uitest") || HasArg("-loadtest");
            int si = Array.IndexOf(args, "-shots");
            shotDir = Path.GetFullPath(si >= 0 && si + 1 < args.Length ? args[si + 1] : Path.Combine(Application.dataPath, "..", "Screenshots"));
            if (touring) SaveSystem.FileName = "wishextractor_tour.json";
            int sf = Array.IndexOf(args, "-savefile");
            if (sf >= 0 && sf + 1 < args.Length) SaveSystem.FileName = args[sf + 1];
            if (touring || HasArg("-fresh")) SaveSystem.Erase();

            Application.targetFrameRate = 144;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 70;
            QualitySettings.shadowCascades = 4;
            QualitySettings.pixelLightCount = 2;
            QualitySettings.antiAliasing = 4;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 75;
            cam.nearClipPlane = 0.03f;
            cam.farClipPlane = 300;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            bloom = camGo.AddComponent<BloomFX>();

            var save = SaveSystem.Load();
            bool fresh = save == null || save.version < 2;
            if (fresh) save = new SaveData();
            sim = new Sim(save);
            if (fresh) sim.StartRun();
            Fmt.Notation = sim.S.notation;

            var world = new GameObject("World");
            view = world.AddComponent<GameView>();
            view.Init(sim, cam);
            view.PlacePlayer(sim.S);
            view.FloatText += (p, t, c, s) => pops?.Float(p, t, c, s);
            view.Player.OnLand += v => { if (v > 6) sfx?.Play("dig", Mathf.Clamp01(v / 14f) * 0.6f, 0.1f, 0.2f, 0.7f); };

            UIKit.EnsureEventSystem();
            uiCanvas = UIKit.CreateCanvas("UI", 10);
            popCanvas = UIKit.CreateCanvas("Popups", 20);
            modalCanvas = UIKit.CreateCanvas("Modals", 30);
            hud = new HUD();
            hud.Build(uiCanvas, sim);
            pops = new Popups();
            pops.Build(popCanvas, cam);
            modals = new Modals();
            modals.Build(modalCanvas, sim);
            modals.OnVolumes = (m, s) => sfx.SetVolumes(m, s);
            modals.OnSettingsChanged = ApplySettings;
            modals.OnResetSave = ResetSave;
            modals.OnQuit = () => { Save(); Application.Quit(); };
            terminal = new TerminalPanel();
            terminal.Build(modalCanvas, sim);
            terminal.OnDenied = () => sfx.Play("denied", 0.6f);
            view.OpenTerminal += () => { sfx.Play("ui"); terminal.Open(); };
            buildMenu = new BuildMenu();
            buildMenu.Build(modalCanvas, sim);
            buildMenu.OnPick = d => { sfx.Play("ui"); view.Build.Select(d); };
            view.Build.Placed += b => sfx.PlayAt(b.Def.IsBelt ? "coin" : "whack", FactoryView.WorldCenter(b), b.Def.IsBelt ? 0.25f : 0.6f, 0.1f, 0.03f, b.Def.IsBelt ? 0.6f : 1f);
            view.Build.Removed += b => sfx.PlayAt("dig", FactoryView.WorldCenter(b), 0.6f, 0.1f, 0.05f);

            sfx = gameObject.AddComponent<AudioHub>();
            sfx.Init(sim.S.musicVol, sim.S.sfxVol);
            sfx.PlayMusicFor(sim.Mall, sim.Remodel);
            HookEvents();
            ApplySettings();

            if (fresh || (sim.S.itemsPicked == 0 && sim.S.mallIndex == 0)) pendingFirstIntro = true;
        }

        void Start()
        {
            if (touring) { StartCoroutine(Tour()); return; }
            if (HasArg("-loadtest")) { StartCoroutine(LoadTest()); return; }
            if (HasArg("-uitest"))
            {
                modals.OpenIntro(true);
                StartCoroutine(UiTest());
                StartCoroutine(Watchdog(Time.realtimeSinceStartup + 780));
                return;
            }
            if (pendingFirstIntro) modals.OpenIntro(true);
        }

        void ApplySettings()
        {
            int q = sim.S.quality;
            bloom.Enabled = q >= 1;
            bloom.Iterations = q >= 2 ? 6 : 4;
            QualitySettings.shadows = q == 0 ? ShadowQuality.Disable : ShadowQuality.All;
            QualitySettings.shadowResolution = q >= 2 ? ShadowResolution.VeryHigh : ShadowResolution.Medium;
            QualitySettings.antiAliasing = q >= 2 ? 4 : q == 1 ? 2 : 0;
            view.Fx.Quality = q == 0 ? 0.4f : q == 1 ? 0.7f : 1f;
            Fmt.Notation = sim.S.notation;
            view.ApplySettings(sim.S);
        }

        // ───────────────────────────── events → feedback ─────────────────────────────

        void HookEvents()
        {
            sim.OnPickup += (t, n, v) =>
            {
                float pitch = t.Cat == ItemCat.Coin ? 1f + Mathf.Min(0.5f, t.Tier * 0.06f) : 0.8f;
                sfx.Play("coin", n > 1 ? 0.8f : 0.6f, 0.08f, 0.03f, pitch);
            };
            sim.OnPickupFail += line => { hud.ShowMessage(line); sfx.Play("denied", 0.4f, 0.05f, 0.4f); };
            sim.OnDeposit += (cash, n, joke) =>
            {
                sfx.Play("register", 0.8f, 0.03f, 0.1f);
                sfx.Play("coin", 0.5f, 0.1f, 0.02f, 0.9f);
                hud.ShowReceipt(cash, n, joke);
            };
            sim.OnDepositEmpty += line => { hud.ShowMessage("COIN-O-MATIC: \"" + line + "\""); sfx.Play("denied", 0.5f, 0.05f, 0.4f); };
            sim.OnAchievement += a =>
            {
                sfx.Play("achievement", 0.8f, 0.02f, 0.2f);
                pops.Toast("Achievement: " + a.Name, $"{a.Desc}  ·  +{Fmt.Num(Balance.AchievementBonus * 100)}% value forever", Pal.Gold, "★");
            };
            sim.OnObjectiveDone += (o, reward) =>
            {
                sfx.Play("buy_big", 0.6f, 0f, 0.2f);
                pops.Toast("Goal complete!", reward > 0 ? $"{o.Text}  ·  +{Fmt.Money(reward)}" : o.Text, Pal.Green, "✓", 3.2f);
            };
            sim.OnTechBought += t =>
            {
                sfx.Play(t.Branch == TechBranch.Fountain ? "buy_big" : "buy", 0.7f, 0.05f, 0.04f);
                if (t.Branch == TechBranch.Fountain)
                    pops.Banner("Fountain upgraded", t.Name, $"Wishability {sim.Wishability:0}: a toss every {sim.TossInterval:0.0}s, up to {sim.CrowdTarget} shoppers. {t.Desc}", Pal.Accent, 4.5f);
            };

            // the crowd
            view.Crowd.Speak = (head, text, wish, rarity) => pops.Say(head, text, wish, rarity);
            view.Message += m => { hud.ShowMessage(m); sfx.Play("denied", 0.4f, 0.05f, 0.3f); };
            sim.OnToss += (s, it) =>
            {
                if (s != null) sfx.PlayAt("throw", new Vector3(s.X, 1.5f, s.Z), 0.25f, 0.15f, 0.05f, 1.4f);
                else sfx.PlayAt("wish_spawn", new Vector3(0, 7, 0), 0.5f, 0.2f, 0.2f, 0.7f);
            };
            sim.OnLanded += it =>
            {
                var def = Content.Items[it.Type];
                var p = new Vector3(it.X, view.Fountain.WaterY, it.Z);
                if (def.Cat == ItemCat.Coin) sfx.PlayAt("plop", p, 0.55f, 0.15f, 0.03f, 1.1f + Mathf.Min(0.4f, def.Tier * 0.05f));
                else sfx.PlayAt("splash", p, 0.8f, 0.1f, 0.05f, Mathf.Clamp(1.3f - def.Scale * 0.15f, 0.6f, 1.2f));
                if (def.Cat == ItemCat.Oddity && def.Rarity >= Rarity.Rare)
                    pops.Toast("Somebody threw in a " + def.Name + "!", def.Desc, Pal.Rarity[(int)def.Rarity], "!", 4f);
            };
            sim.OnWishSpawned += w => sfx.PlayAt("wish_spawn", new Vector3(w.X, view.Fountain.WaterY + 1, w.Z), 0.6f, 0.1f, 0.3f);
            sim.OnWishCaught += (w, cash, tokens, first) =>
            {
                sfx.Play("wish_catch", 0.9f, 0.04f, 0.05f, 1f + (int)w.Def.Rarity * 0.03f);
                pops.WishQuote(view.WishOrbs.LastCaught, w.Def, cash, first);
                if (w.Def.Rarity == Rarity.Legendary) pops.Banner("Legendary wish!", "“" + w.Def.Text + "”", "+" + Fmt.Money(cash), Pal.Gold, 4.5f);
            };
            sim.OnWishEscaped += w => sfx.PlayAt("wish_escape", new Vector3(w.X, view.Fountain.WaterY + 2, w.Z), 0.3f, 0.1f, 0.4f);
            sim.OnWishCompressed += (w, v) => sfx.Play("compress", 0.35f, 0.1f, 0.4f);

            // the crust, relics, events and the next contract
            view.Dug += (p, scoops) =>
            {
                sfx.PlayAt("dig", p, 0.8f, 0.08f, 0.04f);
                if (sim.CurStratum.Loose && scoops > 0) sfx.PlayAt("coin", p, 0.4f, 0.12f, 0.05f);
            };
            sim.OnStratumReached += s =>
            {
                sfx.Play("stratum", 0.9f, 0f, 1f);
                var st = sim.Mall.Strata[s];
                pops.Banner($"New layer · {Fmt.Feet(st.StartFrac * sim.Mall.DepthFeet)} down", st.Name, st.Flavor, MeshKit.Hex(st.Color) + new Color(0, 0, 0, 1), 4.8f);
            };
            sim.OnRelicFound += (r, v, first) =>
            {
                sfx.Play("relic", 0.7f, 0.02f, 0.3f);
                pops.Toast((first ? "New relic! " : "Relic: ") + r.Name, $"{r.Desc}  ·  worth {Fmt.Money(v)} at the kiosk", Pal.Rarity[(int)r.Rarity], "◆", first ? 5f : 3.6f);
                if (r.Rarity == Rarity.Legendary) pops.Banner("Legendary find!", r.Name, r.Desc, Pal.Gold, 4.5f);
            };
            sim.OnRelicSetComplete += m => { sfx.Play("achievement"); pops.Banner("Collection complete", m.Name + " relics", $"Every relic found: +{Fmt.Num(Balance.RelicSetBonus * 100)}% value forever", Pal.Purple, 4.5f); };
            sim.OnEventChanged += on =>
            {
                if (!on) return;
                sfx.Play("event", 0.8f, 0, 1f);
                pops.Banner("Mall event", sim.Mall.Event.Name, sim.Mall.Event.Desc, Pal.Pink, 4f);
            };
            sim.OnMallCleared += () =>
            {
                sfx.Play("cleared", 1f, 0f, 1f);
                pops.Banner("Bare concrete!", sim.Mall.TreasureName, sim.Mall.TreasureDesc, Pal.Gold, 6f);
                StartCoroutine(AfterClear());
            };
            sim.OnPrestige += () =>
            {
                pops.ClearBanners();
                sfx.PlayMusicFor(sim.Mall, sim.Remodel);
                Save();
            };
            // hazards, the goldfish, footsteps
            view.Hazards.Speak = (head, text, wish, rarity) => pops.Say(head, text, wish, rarity);
            sim.OnGuardSpeak += (g, line) => { if (g.State == GuardState.Warning) sfx.PlayAt("whistle", new Vector3(g.X, 1.6f, g.Z), 0.9f, 0.05f, 1f); };
            sim.OnFined += fine =>
            {
                sfx.Play("denied", 0.7f, 0.02f, 0.5f);
                pops.Toast("Fined by Officer Doug", $"-{Fmt.Money(fine)} for wading during mall hours. (Security tab: Donut Diplomacy.)", Pal.Red, "!", 4.5f);
            };
            sim.OnRivalArrived += r => { sfx.Play("event", 0.5f, 0.05f, 2f, 1.3f); pops.Toast("A rival diver is in your fountain!", "Chad is pocketing your coins. Get close to him (or press E) to chase him off.", Pal.Pink, "!", 5f); };
            sim.OnRivalChased += (r, n) => { sfx.Play("splash", 0.9f, 0.05f, 0.3f, 0.8f); pops.Toast("Chad fled!", n > 0 ? $"He dropped {n} stolen item{(n == 1 ? "" : "s")} back in the fountain." : "He didn't get anything. Good.", Pal.Green, "✓", 3.5f); };
            sim.OnRivalEscaped += (r, n) => pops.Toast("Chad got away", $"...with {n} of your items. He'll be back.", Pal.Ink2, "✕", 4f);
            sim.OnFishReturned += it =>
            {
                sfx.Play("splash", 0.7f, 0.1f, 0.2f, 1.3f);
                hud.ShowMessage("You gently return the goldfish to the water. Nearby shoppers applaud.  +1 ✦");
            };
            view.Player.OnStep += () =>
            {
                if (view.Wading) sfx.Play("plop", 0.18f, 0.2f, 0.12f, 0.7f);
                else sfx.Play("step", 0.35f, 0.12f, 0.12f);
            };

            // the mall-only machines: the sommelier's cannon, the baggage drones, the slot machine, the Old Well
            sim.OnCannonBlast += (b, n) =>
            {
                sfx.PlayAt("pop", FactoryView.WorldCenter(b) + Vector3.up, 0.9f, 0.08f, 0.1f);
                if (sim.S.cannonBlasts <= 1)
                    pops.Toast("Pop!", "The Champagne Cork Cannon blasts whole slabs into the water, already rinsed. Pumps and claws by the splash can feed them straight to a sorter.", Pal.Gold, "!", 5.5f);
                else if (Time.time - lastSommelierLine > 20 && UnityEngine.Random.value < 0.25f)
                {
                    lastSommelierLine = Time.time;
                    pops.Say(view.Factory.Voice(b.Uid), Pick(SommelierLines), false, Rarity.Common);
                }
            };
            sim.OnDroneUnloaded += b =>
            {
                sfx.PlayAt("drone", FactoryView.WorldCenter(b) + Vector3.up * 1.5f, 0.45f, 0.1f, 0.4f);
                if (sim.S.droneTrips <= 1)
                    pops.Toast("Now arriving at Carousel 4", "Cargo drones empty every rim intake with no belt behind it and fly the loads here. Start your line at the carousel's back.", Pal.Accent, "!", 5.5f);
                else if (Time.time - lastPaLine > 30 && UnityEngine.Random.value < 0.2f)
                {
                    lastPaLine = Time.time;
                    pops.Say(view.Factory.Voice(b.Uid), Pick(PaLines), false, Rarity.Common);
                }
            };
            sim.OnJackpot += (b, v) =>
            {
                sfx.PlayAt("jackpot", FactoryView.WorldCenter(b) + Vector3.up * 2, 1f, 0.02f, 0.5f);
                pops.Say(view.Factory.Voice(b.Uid), Pick(ElvisLines), false, Rarity.Common);
                if (Time.time - lastJackpotBanner > 45)
                {
                    lastJackpotBanner = Time.time;
                    pops.Banner("Jackpot!", "7 · 7 · 7", $"{Fmt.Money(v * sim.ValueMult)} in tokens just hit the fountain. Claws and pumps will find them (so will Chad).", Pal.Gold, 4f);
                }
            };
            sim.OnSlotSpin += (b, r) =>
            {
                sfx.PlayAt("reels", FactoryView.WorldCenter(b) + Vector3.up * 1.4f, 0.3f, 0.12f, 0.9f);
                if (sim.S.slotSpins <= 1)
                {
                    pops.Toast("Spin to win", "The Slot-Machine Sorter spins every chunk of raw gunk: cherries pay it out sorted, BAR double, sevens five times, 7-7-7 sprays the fountain. The house keeps the rest.", Pal.Gold, "!", 5.5f);
                    return;
                }
                // Elvis works the floor: a word on a big win, now and then a word on the house's
                if ((r != SlotResult.Sevens && r != SlotResult.Lose) || Time.time - lastElvisLine < 40) return;
                if (r == SlotResult.Lose && UnityEngine.Random.value > 0.02f) return;
                lastElvisLine = Time.time;
                pops.Say(view.Factory.Voice(b.Uid), Pick(r == SlotResult.Sevens ? SevensLines : HouseLines), false, Rarity.Common);
            };
            sim.OnWellGranted += (b, w, scoops) =>
            {
                sfx.PlayAt("well", FactoryView.WorldCenter(b) + Vector3.up, 0.7f, 0.04f, 0.3f);
                if (sim.S.wellWishes <= 1)
                    pops.Toast("The Old Well granted a wish", "Wishes nobody catches in a few seconds fall in, and that much crust stops existing. Catch the ones you want first.", Pal.Accent, "✦", 5.5f);
                else if (Time.time - lastWellLine > 10)
                {
                    lastWellLine = Time.time;
                    pops.Say(view.Factory.Voice(b.Uid), w.Def.Rarity >= Rarity.Epic ? "Now THAT is a wish." : Pick(WellLines), false, Rarity.Common);
                }
            };

            modals.OnSign = () =>
            {
                if (!sim.MallCleared) return;
                sim.Prestige();
                modals.OpenIntro(false);
            };
        }

        float lastJackpotBanner = -999, lastWellLine = -999, lastSommelierLine = -999, lastPaLine = -999, lastElvisLine = -999;
        static string Pick(string[] lines) => lines[UnityEngine.Random.Range(0, lines.Length)];

        static readonly string[] SommelierLines =
        {
            "Santé!", "Notes of Cinnabon, and a finish of regret.", "An impertinent little crust. Pairs well with a pump.",
            "The '96. It opens like a jackhammer.", "Please, no photos of the cannon.", "Do not drink the fountain. I am serious this time.",
            "Forty years in the cellar. The crust, I mean.", "Chilled to exactly fountain temperature.", "The cork is complimentary. The skylight is not.",
            "A bold vintage. Hints of penny, a whisper of pretzel.", "One does not shake the magnum. One aims it.",
        };
        static readonly string[] PaLines =
        {
            "Would the owner of a 1987 penny please report to Carousel 7.", "Carousel 4 is now Carousel 7. Carousel 7 is now closed.",
            "Unattended coins will be collected, and sold. Thank you.", "Now boarding: Group Gunk.",
            "Your baggage may have shifted in flight. It was coins. It's still coins.", "The fountain is not a moving walkway. Please stop wading on it.",
            "Delayed: the 14:05 from the rim. Reason: rubble.", "Drones are not a lounge. Please don't ride the drones.",
        };
        static readonly string[] ElvisLines =
        {
            "Thank you. Thank you very much.", "Viva Lost Wages!", "Uh-huh-huh. JACKPOT, baby.", "The King has left the building. With your coins.",
            "A little less conversation, a little more digging.",
        };
        static readonly string[] SevensLines =
        {
            "Seven, seven, BAR. Close enough, darlin'.", "Five times the gunk! Somebody call the gaming commission. Actually, don't.",
            "Lucky sevens. The house is all shook up.", "Well, bless my rhinestones.",
        };
        static readonly string[] HouseLines =
        {
            "The house thanks you for your generous donation.", "Don't be cruel. Spin again.", "Return to sender. Address unknown.",
            "It's now or never. It's usually never.",
        };
        static readonly string[] WellLines =
        {
            "Granted.", "Wish received. Crust dissolved. Next!", "Your wish is... under review. Approved.", "Glub. (That means yes.)",
            "One small wish for a man, one giant hole for the fountain.", "Filed under 'mostly harmless'. Granted.",
            "I have been down here since 1971. You are my favourite.", "No refunds. Also no crust.", "Wish noted. Crust deleted.",
        };

        IEnumerator AfterClear()
        {
            yield return new WaitForSeconds(3.5f);
            if (touring) yield break;
            if (sim.IsFinalMall && !sim.S.endingSeen)
            {
                sim.S.endingSeen = true;
                modals.OpenEnding();
                while (modals.IsOpen) yield return null;
            }
            if (!AnyMenu && sim.MallCleared) modals.OpenContract();
        }

        void ResetSave()
        {
            SaveSystem.Erase();
            sim.Load(new SaveData());
            sim.StartRun();
            view.RebuildMall();
            view.PlacePlayer(sim.S);
            sfx.PlayMusicFor(sim.Mall, sim.Remodel);
            modals.OpenIntro(true);
        }

        void Save()
        {
            if (sim == null) return;
            view.StorePose(sim.S);
            SaveSystem.Save(sim.Snapshot());
        }

        void OnApplicationQuit() => Save();
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationFocus(bool focus) { if (!focus && !testing) Save(); }

        // ───────────────────────────── frame ─────────────────────────────

        void Update()
        {
            float dt = Time.deltaTime;
            float left = Mathf.Min(dt, 5f);
            while (left > 0)
            {
                float step = Mathf.Min(left, 0.1f);
                sim.Tick(step);
                left -= step;
            }

            bool modal = AnyMenu;
            if (!testing) HandleKeys(modal);
            modal = AnyMenu;
            UpdateCursor(modal);

            FPInput input = scripted ?? (testing || swallowInput ? default : ReadInput());
            swallowInput = false;
            if (scripted.HasValue)
            {
                var s = scripted.Value;
                s.Interact = false;
                s.PrimaryDown = false;
                s.Look = Vector2.zero;
                s.Hotbar = 0;
                s.Rotate = s.Demolish = s.Catalogue = false;
                s.Cycle = 0;
                scripted = s;
            }
            if (input.Catalogue && !modal && view.Build.SetActive(true)) { buildMenu.Open(); modal = true; }
            view.Step(input, dt, modal);

            hud.Refresh(dt, view.Current, view.Wading, modal, view.Build, view.DigMode);
            pops.Update(dt);
            modals.Update(dt);
            terminal.Update(dt);

            autosave += dt;
            if (autosave >= Balance.AutosaveSeconds) { autosave = 0; Save(); }
        }

        void HandleKeys(bool modal)
        {
            if (Input.GetKeyDown(KeyCode.Escape) || (terminal.IsOpen && Input.GetKeyDown(KeyCode.E)))
            {
                if (terminal.IsOpen) { terminal.Close(); swallowInput = true; }
                else if (buildMenu.IsOpen) { buildMenu.Close(); swallowInput = true; }
                else if (modal) { if (modals.OpenName != "intro") modals.Close(); }
                else modals.OpenSettings();
            }
            if (Input.GetKeyDown(KeyCode.J) && !modal) { sfx.Play("ui"); modals.OpenJournal(); }
            if (devMode && !modal)
            {
                if (Input.GetKeyDown(KeyCode.F5)) sim.DebugAddCash(Math.Max(10, sim.S.cash * 9));
                if (Input.GetKeyDown(KeyCode.F6)) sim.DebugFinishMall();
                if (Input.GetKeyDown(KeyCode.F7)) sim.DebugSetDepth(sim.DepthFrac + 0.1);
                if (Input.GetKeyDown(KeyCode.F8)) sim.DebugStartEvent();
            }
            if (!modal && sim.MallCleared && Input.GetKeyDown(KeyCode.C)) modals.OpenContract();
        }

        void UpdateCursor(bool modal)
        {
            bool wantLock = !modal && !testing && Application.isFocused;
            if (wantLock && Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0)) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (wantLock && Cursor.lockState != CursorLockMode.Locked && lockedBefore) { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (!wantLock && Cursor.lockState != CursorLockMode.None) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
            lockedBefore = wantLock;
        }
        bool lockedBefore;

        FPInput ReadInput()
        {
            var i = new FPInput();
            if (Cursor.lockState != CursorLockMode.Locked) return i;
            float x = (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow) ? 1 : 0) - (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow) ? 1 : 0);
            float y = (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow) ? 1 : 0) - (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow) ? 1 : 0);
            i.Move = new Vector2(x, y);
            float sens = sim.S.mouseSens * 2.2f;
            i.Look = new Vector2(Input.GetAxisRaw("Mouse X") * sens, Input.GetAxisRaw("Mouse Y") * sens * (sim.S.invertY ? -1 : 1));
            i.Jump = Input.GetKeyDown(KeyCode.Space);
            i.Sprint = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            i.Primary = Input.GetMouseButton(0);
            i.PrimaryDown = Input.GetMouseButtonDown(0);
            i.Interact = Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.F);
            if (Input.GetKeyDown(KeyCode.Alpha1)) i.Hotbar = 1;
            if (Input.GetKeyDown(KeyCode.Alpha2)) i.Hotbar = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.B)) i.Hotbar = 3;
            i.Rotate = Input.GetKeyDown(KeyCode.R);
            i.Demolish = Input.GetKeyDown(KeyCode.X);
            i.Catalogue = Input.GetKeyDown(KeyCode.Tab);
            float wheel = Input.mouseScrollDelta.y;
            i.Cycle = wheel > 0.1f ? -1 : wheel < -0.1f ? 1 : 0;
            return i;
        }

        // ───────────────────────────── scripted movement (tests, tour) ─────────────────────────────

        void Drive(Vector2 move, bool jump = false, bool sprint = false)
        {
            var s = scripted ?? default;
            s.Move = move;
            s.Jump = jump;
            s.Sprint = sprint;
            scripted = s;
        }

        void Press(bool interact = true, bool primary = false)
        {
            var s = scripted ?? default;
            s.Interact = interact;
            s.PrimaryDown = primary;
            scripted = s;
        }

        /// <summary>Walk (through the real controller) until the feet are within tol of target (xz).</summary>
        IEnumerator WalkTo(Vector3 target, float tol = 0.5f, float timeout = 20f, bool sprint = true)
        {
            float t = 0, stuck = 0;
            Vector3 last = view.Player.Feet;
            while (t < timeout)
            {
                var feet = view.Player.Feet;
                Vector3 d = target - feet;
                d.y = 0;
                if (d.magnitude <= tol) break;
                float want = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
                view.Player.Yaw = Mathf.MoveTowardsAngle(view.Player.Yaw, want, 540 * Time.deltaTime);
                float moved = (feet - last).magnitude;
                last = feet;
                stuck = moved < 0.01f ? stuck + Time.deltaTime : 0;
                Drive(new Vector2(0, 1), stuck > 0.25f, sprint && d.magnitude > 2);
                t += Time.deltaTime;
                yield return null;
            }
            Drive(Vector2.zero);
            yield return null;
            yield return null;
        }

        /// <summary>Smoothly turn the view toward a world point.</summary>
        IEnumerator LookAtSmooth(Vector3 world, float seconds = 0.35f)
        {
            float y0 = view.Player.Yaw, p0 = view.Player.Pitch;
            view.Player.LookAt(world);
            float y1 = view.Player.Yaw, p1 = view.Player.Pitch;
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / seconds);
                view.Player.Yaw = Mathf.LerpAngle(y0, y1, k);
                view.Player.Pitch = Mathf.Lerp(p0, p1, k);
                Drive(Vector2.zero);
                yield return null;
            }
            view.Player.LookAt(world);
            yield return null;
        }

        LooseItem NearestLoose(Vector3 from, Func<LooseItem, bool> ok = null)
        {
            LooseItem best = null;
            float bd = float.MaxValue;
            foreach (var it in sim.Loose)
            {
                if (it.State != LooseState.Resting || (ok != null && !ok(it))) continue;
                float d = (new Vector3(it.X, 0, it.Z) - new Vector3(from.x, 0, from.z)).sqrMagnitude;
                if (d < bd) { bd = d; best = it; }
            }
            return best;
        }

        /// <summary>Walk into the fountain over the south stepping stone.</summary>
        IEnumerator EnterFountain()
        {
            yield return WalkTo(new Vector3(0, 0, -11.2f), 0.4f);
            yield return WalkTo(new Vector3(0, 0, -6.5f), 0.4f, 8f, false);
        }

        /// <summary>Walk out of the fountain and up to the COIN-O-MATIC.</summary>
        IEnumerator GoToKiosk()
        {
            yield return WalkTo(new Vector3(0, 0, -11.5f), 0.5f);
            Vector3 front = view.Kiosk.Root.position + view.Kiosk.Root.forward * 1.5f;
            yield return WalkTo(front, 0.35f);
            yield return LookAtSmooth(view.Kiosk.Root.position + Vector3.up * 1.2f);
        }

        void SetFlag(Action<FPInputBox> set)
        {
            var box = new FPInputBox { I = scripted ?? default };
            set(box);
            scripted = box.I;
        }

        /// <summary>Lambdas can't take a struct by ref, so the scripted input rides in a box.</summary>
        sealed class FPInputBox
        {
            public FPInput I;
            public int Hotbar { set => I.Hotbar = value; }
            public bool Primary { set => I.Primary = value; }
            public bool PrimaryDown { set => I.PrimaryDown = value; }
            public bool Demolish { set => I.Demolish = value; }
            public bool Catalogue { set => I.Catalogue = value; }
            public bool Rotate { set => I.Rotate = value; }
        }

        IEnumerator AimAt(Vector3 p) => LookAtSmooth(p, 0.12f);

        IEnumerator PickFromCatalogue(string id)
        {
            SetFlag(i => i.Catalogue = true);
            yield return null;
            yield return new WaitForSeconds(0.15f);
            Check(buildMenu.IsOpen && ClickSelectable(FindButton("Build:" + id)), $"pick {id} from the build catalogue");
            yield return null;
            Check(!buildMenu.IsOpen && view.Build.Selected?.Id == id, $"holding {id}");
        }

        IEnumerator GoToTerminal()
        {
            var t = view.Terminal.Root;
            yield return WalkTo(t.position + t.forward * 1.3f, 0.3f);
            yield return LookAtSmooth(t.position + Vector3.up * 0.85f);
        }

        IEnumerator GoToBoard()
        {
            var b = view.Board.Root;
            yield return WalkTo(b.position + b.forward * 1.7f, 0.35f);
            yield return LookAtSmooth(b.position + Vector3.up * 1.25f);
        }

        /// <summary>Wade toward Chad, re-aiming as he moves, until within dist of him (or he's left).</summary>
        IEnumerator ApproachRival(float dist, float timeout = 25f)
        {
            float t0 = Time.time;
            while (Time.time - t0 < timeout && (sim.Rival.State == RivalState.Stealing || sim.Rival.State == RivalState.Arriving))
            {
                var r = new Vector3(sim.Rival.X, 0, sim.Rival.Z);
                var feet = view.Player.Feet;
                if (new Vector2(r.x - feet.x, r.z - feet.z).magnitude <= dist) break;
                yield return WalkTo(r, dist, 0.4f, false);
            }
            Drive(Vector2.zero);
        }

        /// <summary>A goldfish resting on the crust a step ahead of the player (kept inside the basin).</summary>
        LooseItem DropGoldfishNearby()
        {
            var feet = view.Player.Feet;
            var p = new Vector2(feet.x + 0.9f, feet.z + 1.2f);
            if (p.magnitude > 6.5f) p = p.normalized * 6.5f;
            if (p.magnitude < 1.6f) p = p.normalized * 1.6f;
            return sim.AddLoose(Content.Type("goldfish"), p.x, p.y, 0, null);
        }

        /// <summary>Send Chad in now and wait until he's in the fountain stealing.</summary>
        IEnumerator SummonRival()
        {
            float wait = 0;
            while (sim.Rival.State != RivalState.Away && wait < 20) { wait += Time.deltaTime; yield return null; }
            sim.DebugRival();
            wait = 0;
            while (sim.Rival.State != RivalState.Stealing && wait < 40) { wait += Time.deltaTime; yield return null; }
        }

        // ───────────────────────────── screenshot tour (-autotour) ─────────────────────────────

        IEnumerator Shot(string name)
        {
            Directory.CreateDirectory(shotDir);
            yield return new WaitForSeconds(0.25f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + ".png"));
            yield return null;
            yield return null;
        }

        IEnumerator Tour()
        {
            StartCoroutine(Watchdog(Time.realtimeSinceStartup + 720));
            scripted = default(FPInput);
            yield return new WaitForSeconds(2.0f);
            modals.OpenIntro(true);
            yield return Shot("00_intro");
            modals.Close();
            view.Player.Place(new Vector3(0, 0.05f, -18), 0, 0);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("01_spawn");
            yield return LookAtSmooth(view.Kiosk.Root.position + Vector3.up * 1.2f);
            yield return WalkTo(view.Kiosk.Root.position + view.Kiosk.Root.forward * 2.2f, 0.4f);
            yield return LookAtSmooth(view.Kiosk.Root.position + Vector3.up * 1.3f);
            yield return Shot("02_kiosk");

            yield return EnterFountain();
            var it = NearestLoose(view.Player.Feet);
            if (it != null)
            {
                yield return WalkTo(new Vector3(it.X, 0, it.Z) - (new Vector3(it.X, 0, it.Z) - view.Player.Feet).normalized * 1.0f, 0.3f, 6f, false);
                yield return LookAtSmooth(view.Items.PositionOf(it));
            }
            yield return Shot("03_aim_coin");
            Press();
            yield return new WaitForSeconds(0.3f);
            yield return Shot("04_holding");
            var it2 = NearestLoose(view.Player.Feet);
            if (it2 != null) { yield return LookAtSmooth(view.Items.PositionOf(it2)); Press(); }
            yield return new WaitForSeconds(0.3f);
            yield return Shot("05_hands_full");
            yield return LookAtSmooth(new Vector3(0, 0.6f, 4));
            yield return Shot("06_in_fountain");

            yield return GoToKiosk();
            Press();
            yield return new WaitForSeconds(1.4f);
            yield return Shot("07_deposit");

            // the Fountain Improvement Plan, and what beautification does to the crowd
            sim.DebugAddCash(1);
            yield return GoToBoard();
            yield return Shot("07b_board");
            Press();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("07c_board_bought");
            sim.DebugSetTech("fountain_jets", 1);
            sim.DebugSetTech("fountain_lights", 1);
            view.Player.Place(new Vector3(0, 0.05f, -15.5f), 0, 4);
            yield return new WaitForSeconds(7f);
            yield return Shot("07d_crowd");
            view.Player.Place(new Vector3(-9.2f, 0.95f, -3.8f), 70, -12);
            yield return new WaitForSeconds(3f);
            yield return Shot("07e_beautified");
            var wish = sim.DebugSpawnWish(-4.5f, -1.5f);
            yield return new WaitForSeconds(1.6f);
            if (wish != null) yield return LookAtSmooth(view.WishOrbs.PositionOf(wish.Uid));
            yield return Shot("07f_wish");
            Press();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("07g_wish_caught");

            // the Maintenance Terminal
            sim.DebugAddCash(30);
            sim.S.wishTokens += 12;
            yield return GoToTerminal();
            yield return Shot("07h_terminal_prop");
            Press();
            yield return new WaitForSeconds(0.5f);
            terminal.SetBranch(TechBranch.Carry);
            yield return Shot("07i_terminal_carry");
            terminal.SetBranch(TechBranch.Fountain);
            yield return Shot("07j_terminal_fountain");
            terminal.SetBranch(TechBranch.Security);
            yield return Shot("30_terminal_security");
            terminal.Close();

            // the hazards: Officer Doug's whistle and fine, Chad the rival diver, and a goldfish that goes back
            sim.DebugAddCash(20);
            yield return EnterFountain();
            for (float w = 0; sim.Guard.State != GuardState.Patrol && w < 10; w += Time.deltaTime) yield return null;
            sim.DebugGuardCheck();
            yield return new WaitForSeconds(0.6f);
            yield return LookAtSmooth(view.Hazards.GuardHead.position);
            yield return Shot("31_guard_warning");
            yield return new WaitForSeconds(5f);
            yield return Shot("32_guard_fine");
            yield return WalkTo(new Vector3(0, 0, -11.5f), 0.5f);
            yield return SummonRival();
            yield return new WaitForSeconds(3f);
            yield return EnterFountain();
            yield return ApproachRival(5f);
            yield return LookAtSmooth(view.Hazards.RivalHead.position - Vector3.up * 0.3f, 0.2f);
            yield return Shot("33_chad");
            yield return LookAtSmooth(view.Hazards.RivalHead.position - Vector3.up * 0.3f, 0.1f);
            Press();
            yield return new WaitForSeconds(0.7f);
            yield return Shot("34_chad_chased");
            var fish = DropGoldfishNearby();
            yield return new WaitForSeconds(0.3f);
            yield return LookAtSmooth(view.Items.PositionOf(fish));
            yield return Shot("35_goldfish");
            Press();
            yield return new WaitForSeconds(0.6f);
            yield return Shot("36_goldfish_returned");
            Debug.Log($"[TOUR] hazards: fines {sim.S.finesPaid}, Chad chased {sim.S.rivalsChased}, goldfish returned {sim.S.fishReturned}");

            // every fountain upgrade, from the rim and the balcony
            foreach (var t in Content.Techs) if (t.Branch == TechBranch.Fountain && t.MaxLevel == 1) sim.DebugSetTech(t.Id, 1);
            view.Player.Place(new Vector3(0, 0.05f, -15.5f), 0, 6);
            yield return new WaitForSeconds(4f);
            yield return Shot("07k_all_decor");
            view.Player.Place(new Vector3(-17, 6.85f, 27f), 180, -20);
            yield return LookAtSmooth(new Vector3(0, 2, 0));
            yield return new WaitForSeconds(1f);
            yield return Shot("07l_all_decor_balcony");

            // containers and tools
            foreach (var (carry, tool, shot) in new[] { ("carry_barrow", "grab_rake", "07m_wheelbarrow"), ("carry_cart", "grab_magnet", "07n_cart"), ("carry_scrubber", "grab_blower", "07o_scrubber"), ("carry_shopvac", "grab_glove", "07p_shopvac") })
            {
                sim.DebugSetTech(carry, 1);
                sim.DebugSetTech(tool, 1);
                view.Player.Place(new Vector3(-3, 0.95f, -8.2f), 20, -30);
                yield return new WaitForSeconds(0.8f);
                yield return Shot(shot);
            }

            // the factory: three intakes feeding three hoppers, powered by a small zoo of generators
            foreach (var t in Content.Techs) if (t.Kind == TechKind.Unlock || t.Kind == TechKind.BeltSpeed) sim.DebugSetTech(t.Id, 1);
            BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];
            sim.Place(D("intake_skimmer"), 11, 0, 3, true);
            for (int x = 12; x <= 15; x++) sim.Place(D("belt"), x, 0, 1, true);
            sim.Place(D("hopper"), 16, 0, 0, true);
            sim.Place(D("gen_hamster"), 12, 3, 0, true);
            sim.Place(D("gen_hamster"), 13, 3, 0, true);
            sim.Place(D("gen_diesel"), 12, 5, 0, true);
            sim.Place(D("gen_fryer"), 15, 5, 0, true);
            sim.Place(D("intake_pump"), 8, 8, 2, true);
            for (int z = 9; z <= 12; z++) sim.Place(D("belt"), 8, z, 0, true);
            sim.Place(D("hopper"), 8, 13, 0, true);
            sim.Place(D("intake_claw"), 7, -9, 0, true);
            for (int z = -10; z >= -12; z--) sim.Place(D("belt"), 7, z, 2, true);
            sim.Place(D("hopper2"), 7, -14, 0, true);
            view.Player.Place(new Vector3(16.5f, 0.05f, -5.5f), 0, 0);
            yield return new WaitForSeconds(6f);
            yield return LookAtSmooth(new Vector3(11.5f, 0.6f, 2.5f));
            yield return Shot("13_factory_east");
            view.Player.Place(new Vector3(2.5f, 0.05f, 13.5f), 0, 0);
            yield return LookAtSmooth(new Vector3(7.5f, 1f, 9f));
            yield return Shot("14_factory_pump");
            view.Player.Place(new Vector3(2.5f, 0.05f, -15.5f), 0, 0);
            yield return LookAtSmooth(new Vector3(7.5f, 1.5f, -10.5f));
            yield return Shot("15_factory_claw");
            Debug.Log($"[TOUR] factory: power {sim.PowerGen:0}/{sim.PowerUse:0} kW, intakes picked {sim.S.machinePicked}, hoppers sold {sim.S.hopperItems} for {Fmt.Money(sim.S.hopperCash)}");
            view.Build.SetActive(true);
            view.Build.Select(D("belt"));
            view.Player.Place(new Vector3(18f, 0.05f, -4f), 0, 0);
            yield return AimAt(new Vector3(18.5f, 0, -1.5f));
            yield return Shot("16_build_ghost");
            view.Build.Select(D("gen_diesel"));
            yield return AimAt(new Vector3(12.5f, 0, 0.5f));
            yield return Shot("17_build_invalid");
            buildMenu.Open();
            yield return Shot("18_build_catalogue");
            buildMenu.Close();
            view.Build.SetActive(false);

            // the crust: a jackhammer by hand, the gunk strata, a processing line, the ramp, bare concrete
            sim.DebugSetTech("dig_jackhammer", 1);
            SetFlag(i => i.Hotbar = 2);
            yield return null;
            yield return EnterFountain();
            var ff = view.Player.Feet;
            yield return AimAt(view.Fountain.SurfacePoint(ff.x + 0.6f, ff.z + 1.4f));
            for (int k = 0; k < 8; k++) { Press(false, true); yield return new WaitForSeconds(0.34f); }
            yield return Shot("19_dig");
            SetFlag(i => i.Hotbar = 1);
            sim.DebugSetDepth(0.4);
            view.Player.Place(new Vector3(0, 0.95f, -8.7f), 0, -35);
            yield return new WaitForSeconds(3.5f);
            yield return Shot("20_crust_deep");
            sim.Place(D("dig_rig"), -12, 0, 1, true);
            sim.Place(D("belt"), -13, 0, 3, true);
            sim.Place(D("belt"), -13, -1, 0, true);
            sim.Place(D("belt"), -14, 0, 3, true);
            sim.Place(D("proc_tumbler"), -15, 0, 3, true);
            sim.Place(D("proc_pigeons"), -17, 0, 3, true);
            sim.Place(D("proc_roller"), -19, 0, 3, true);
            sim.Place(D("hopper"), -22, 0, 0, true);
            sim.Place(D("gen_fryer"), -15, 4, 0, true);
            sim.Place(D("gen_fryer"), -18, 4, 0, true);
            sim.Place(D("proc_sorter"), -16, -5, 0, true);
            sim.Place(D("proc_melter"), -19, -5, 0, true);
            sim.Place(D("proc_compressor"), -22, -5, 0, true);
            view.Player.Place(new Vector3(-13.5f, 0.05f, -6.5f), 0, 0);
            yield return new WaitForSeconds(6f);
            yield return LookAtSmooth(new Vector3(-17f, 0.8f, 0f));
            yield return Shot("21_processing");
            Debug.Log($"[TOUR] processing: dug {sim.S.dug:0}, washed {sim.S.washed}, sorted {sim.S.sorted}, relics {sim.S.relicsFound}, bundles {sim.S.bundles}");
            sim.DebugSetDepth(0.8);
            view.Player.Place(new Vector3(6.2f, 0.95f, -6.2f), -45, -40);
            yield return new WaitForSeconds(3.5f);
            yield return Shot("22_deep_ramp");
            sim.DebugFinishMall();
            yield return new WaitForSeconds(3.5f);
            yield return Shot("23_bare_concrete");
            modals.OpenContract();
            yield return Shot("24_contract");
            modals.Close();

            // bigger containers (tech the terminal will sell in M3), shown off in the fountain
            sim.DebugSetTech("carry_bucket", 1);
            sim.DebugSetTech("carry_cup", 1);
            sim.DebugSetTech("carry_pail", 1);
            sim.DebugSetTech("grab_grabber", 1);
            sim.DebugSetTech("grab_net", 1);
            yield return EnterFountain();
            var it3 = NearestLoose(view.Player.Feet);
            if (it3 != null) yield return LookAtSmooth(view.Items.PositionOf(it3));
            for (int i = 0; i < 6; i++) { Press(); yield return new WaitForSeconds(0.3f); }
            yield return Shot("08_net_bucket");

            // overview from the balcony
            view.Player.Place(new Vector3(-17, 6.85f, 27f), 180, -28);
            yield return new WaitForSeconds(0.8f);
            yield return LookAtSmooth(new Vector3(0, 0, 0));
            yield return Shot("09_balcony");
            view.Player.Place(new Vector3(8.7f, 0.95f, 0), -90, -22);
            yield return new WaitForSeconds(0.6f);
            yield return Shot("10_rim_view");
            float fpsT = 0;
            int frames = 0;
            while (fpsT < 3f) { fpsT += Time.unscaledDeltaTime; frames++; yield return null; }
            Debug.Log($"[TOUR] average FPS {frames / fpsT:0.0} with {sim.Loose.Count} loose items ({view.Items.Drawn} drawn)");

            modals.OpenSettings();
            yield return Shot("11_settings");
            modals.OpenJournal(3);
            yield return Shot("12_journal_stats");
            modals.Close();

            for (int m = 1; m < Content.Malls.Length; m++)
            {
                sim.DebugJumpToMall(m);
                view.RebuildMall();
                sfx.PlayMusicFor(sim.Mall, sim.Remodel);
                view.Player.Place(new Vector3(0, 0.05f, -18), 0, -4);
                yield return new WaitForSeconds(1.2f);
                yield return Shot($"2{m}_mall{m + 1}");
                yield return MallMachineShots(m);
            }
            Debug.Log($"[TOUR] mall machines: cannon pops {sim.S.cannonBlasts}, drone trips {sim.S.droneTrips}, slot spins {sim.S.slotSpins} ({sim.S.jackpots} jackpots), wishes granted by the well {sim.S.wellWishes}");
            Application.Quit();
        }

        /// <summary>The tour: each late mall's own machine (TechDef.MallOnly), built in its mall and caught at work.</summary>
        IEnumerator MallMachineShots(int m)
        {
            if (m < 2 || m > 5) yield break;
            BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];
            foreach (var t in Content.Techs) if (t.Kind == TechKind.Unlock && sim.TechInThisMall(t)) sim.DebugSetTech(t.Id, 1);
            sim.DebugSetDepth(0.25);
            for (int i = 0; i < 6; i++) sim.Place(D("gen_solar"), 20 + (i % 3) * 3, -27 + (i / 3) * 3, 0, true);
            if (m == 2)
            {
                // Galleria Aurelia: a cork cannon behind the rim lobs slabs over a pump line
                sim.Place(D("dig_cannon"), 17, -1, 3, true);
                sim.Place(D("intake_pump"), 11, -3, 3, true);
                sim.Place(D("proc_sorter"), 12, -3, 1, true);
                sim.Place(D("hopper2"), 14, -3, 1, true);
                view.Player.Place(new Vector3(15f, 0.05f, -10f), -20, 0);
                yield return LookAtSmooth(new Vector3(11.5f, 2.2f, 0));
                yield return new WaitForSeconds(2.5f);
                sim.DebugFireCannons();
                yield return null;
                yield return Shot("40_aurelia_cannon");
                view.Player.Place(new Vector3(8.7f, 0.95f, 2.2f), -110, -25);
                yield return LookAtSmooth(view.Fountain.SurfacePoint(4.6f, 0.4f));
                yield return new WaitForSeconds(0.8f);
                sim.DebugFireCannons();
                yield return new WaitForSeconds(0.45f);
                yield return Shot("41_aurelia_splash");
            }
            else if (m == 3)
            {
                // Skyport: a borer at the rim with no line; drones fly its chunks to a carousel line at the back
                sim.Place(D("dig_borer"), -13, 0, 1, true);
                sim.Place(D("carousel"), -24, 10, 0, true);
                sim.Place(D("proc_tumbler"), -24, 13, 0, true);
                sim.Place(D("proc_sorter"), -24, 15, 0, true);
                sim.Place(D("hopper2"), -24, 17, 0, true);
                sim.DebugSetTech("carousel_tags", 2);
                view.Player.Place(new Vector3(-26f, 0.05f, -3f), 45, 0);
                yield return LookAtSmooth(new Vector3(-17.5f, 2.4f, 5f));
                double trips0 = sim.S.droneTrips;
                for (float w = 0; w < 15 && sim.S.droneTrips <= trips0; w += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(1.5f);
                yield return Shot("42_skyport_carousel");
                terminal.Open();
                terminal.SelectTech("carousel_tags");
                yield return Shot("46_terminal_mall_only");
                terminal.Close();
                view.Build.SetActive(true);
                buildMenu.Open();
                yield return Shot("47_catalogue_skyport");
                buildMenu.Close();
                view.Build.SetActive(false);
            }
            else if (m == 4)
            {
                // the Lucky Lagoon: rig → slot machine → hopper, then 7-7-7
                sim.Place(D("dig_rig"), 11, -1, 3, true);
                var slots = sim.Place(D("proc_slots"), 12, 0, 1, true);
                sim.Place(D("hopper2"), 14, 0, 1, true);
                view.Player.Place(new Vector3(13.2f, 0.05f, -5f), 0, 0);
                yield return LookAtSmooth(new Vector3(12.6f, 1.5f, 0));
                double spins0 = sim.S.slotSpins;
                for (float w = 0; w < 20 && sim.S.slotSpins < spins0 + 2; w += Time.deltaTime) yield return null;
                yield return Shot("43_lagoon_slots");
                yield return LookAtSmooth(new Vector3(10.5f, 2.3f, 0), 0.2f);
                if (slots != null) sim.DebugJackpot(slots);
                yield return new WaitForSeconds(0.35f);
                yield return Shot("44_lagoon_jackpot");
            }
            else
            {
                // Eternity Plaza: the Old Well pulls in a wish nobody caught
                sim.Place(D("wishing_well"), -12, 1, 1, true);
                view.Player.Place(new Vector3(-11f, 0.05f, -6f), 20, 0);
                yield return LookAtSmooth(new Vector3(-8.2f, 1.8f, 1f));
                yield return new WaitForSeconds(0.5f);
                double granted = sim.S.wellWishes;
                sim.DebugSpawnWish(-4.5f, 1.5f);
                for (float w = 0; w < 8 && sim.S.wellWishes <= granted; w += Time.deltaTime) yield return null;
                yield return new WaitForSeconds(0.2f);
                yield return Shot("45_eternity_well");
            }
        }

        IEnumerator Watchdog(float deadline)
        {
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Debug.Log("[WATCHDOG] time limit reached, quitting");
            Application.Quit();
        }

        /// <summary>-loadtest: report what came back from the save, then quit.</summary>
        IEnumerator LoadTest()
        {
            yield return new WaitForSeconds(1.0f);
            var p = view.Player.Feet;
            Debug.Log($"[LOADTEST] cash={Fmt.Money(sim.S.cash)} carried={sim.CarriedCount} ({Fmt.Money(sim.CarriedValue)}) loose={sim.Loose.Count} " +
                      $"pos=({p.x:0.0},{p.y:0.0},{p.z:0.0}) yaw={view.Player.Yaw:0} picked={sim.S.itemsPicked} deposits={sim.S.deposits} " +
                      $"carryTier={sim.CarryTier} objective={sim.S.objective} achievements={sim.AchievementCount} " +
                      $"buildings={sim.Buildings.Count} (machines {sim.MachinesBuilt}, belts {sim.BeltsBuilt}) power={sim.PowerGen}/{sim.PowerUse} kW");
            yield return Shot("ui_loadtest");
            Application.Quit();
        }

        // ───────────────────────────── UI / input self-test (-uitest) ─────────────────────────────
        // Drives the real controller through FPInput (walk, look, interact), the real crosshair
        // targeting, and the real uGUI event pipeline for menus. Logs PASS/FAIL per step.

        int uiPass, uiFail;

        void Check(bool ok, string what)
        {
            if (ok) uiPass++; else uiFail++;
            Debug.Log($"[UITEST] {(ok ? "PASS" : "FAIL")}  {what}");
        }

        bool PointerOverUI(Vector2 screen)
        {
            var ped = new PointerEventData(EventSystem.current) { position = screen };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);
            return hits.Count > 0;
        }

        bool ClickSelectable(Selectable s)
        {
            if (s == null || !s.gameObject.activeInHierarchy) return false;
            var rt = (RectTransform)s.transform;
            Vector2 screen = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
            var ped = new PointerEventData(EventSystem.current) { position = screen, button = PointerEventData.InputButton.Left };
            var hits = new List<RaycastResult>();
            EventSystem.current.RaycastAll(ped, hits);
            if (hits.Count == 0) { Debug.Log($"[UITEST] nothing under {s.name} at {screen}"); return false; }
            var top = hits[0].gameObject;
            if (top != s.gameObject && !top.transform.IsChildOf(s.transform)) { Debug.Log($"[UITEST] {s.name} is covered by {top.name}"); return false; }
            ped.pointerPress = s.gameObject;
            ped.pointerCurrentRaycast = hits[0];
            ExecuteEvents.Execute(s.gameObject, ped, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(s.gameObject, ped, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(s.gameObject, ped, ExecuteEvents.pointerClickHandler);
            return true;
        }

        /// <summary>The on-screen button with this name that sits highest on the screen (like a player would pick).</summary>
        Button FindButton(string name, Func<Button, bool> extra = null)
        {
            Button best = null;
            float bestY = float.MinValue;
            foreach (var b in FindObjectsByType<Button>())
            {
                if (b.name != name || !b.gameObject.activeInHierarchy || (extra != null && !extra(b))) continue;
                var rt = (RectTransform)b.transform;
                Vector2 sp = RectTransformUtility.WorldToScreenPoint(null, rt.TransformPoint(rt.rect.center));
                if (sp.x < 0 || sp.y < 0 || sp.x > Screen.width || sp.y > Screen.height) continue;
                if (!PointerOverUI(sp)) continue;
                if (sp.y > bestY) { bestY = sp.y; best = b; }
            }
            return best;
        }

        IEnumerator UiTest()
        {
            scripted = default(FPInput);
            yield return new WaitForSeconds(2.5f);
            Check(modals.IsOpen && modals.OpenName == "intro", "intro shown on a fresh save");
            Check(ClickSelectable(FindButton("Go")), "click 'Clock in'");
            yield return null;
            Check(!modals.IsOpen, "intro closed");
            Check(sim.Loose.Count >= Balance.SeedCoins * 0.9, $"fountain seeded with loose coins ({sim.Loose.Count})");

            // mouse look through FPInput
            float yaw0 = view.Player.Yaw;
            var s = scripted.Value; s.Look = new Vector2(30, 0); scripted = s;
            yield return null;
            yield return null;
            Check(Mathf.Abs(Mathf.DeltaAngle(yaw0, view.Player.Yaw) - 30) < 0.5f, $"mouse look turns the view ({Mathf.DeltaAngle(yaw0, view.Player.Yaw):0.0}°)");
            view.Player.Yaw = 0;

            // walking
            Vector3 start = view.Player.Feet;
            Drive(new Vector2(0, 1));
            yield return new WaitForSeconds(0.8f);
            Drive(Vector2.zero);
            Check(view.Player.Feet.z > start.z + 1.5f, $"W walks forward ({view.Player.Feet.z - start.z:0.00} m)");

            // empty deposit
            yield return GoToKiosk();
            Check(view.Current.Kind == TargetKind.Kiosk, $"crosshair targets the COIN-O-MATIC (got {view.Current.Kind})");
            Press();
            yield return null;
            yield return null;
            Check(sim.S.deposits == 0 && sim.S.cash == 0, "depositing with empty hands pays nothing");

            // into the fountain over the stepping stone
            yield return EnterFountain();
            var feet = view.Player.Feet;
            Check(FountainView.InBasin(feet), $"walked over the rim into the fountain (r={new Vector2(feet.x, feet.z).magnitude:0.0} m, y={feet.y:0.00})");
            Check(view.Wading, "wading in the water slows you down");

            // pick up one coin
            var it = NearestLoose(view.Player.Feet);
            Check(it != null, "there is a coin nearby");
            if (it != null)
            {
                Vector3 flat = new Vector3(it.X, 0, it.Z);
                yield return WalkTo(flat - (flat - new Vector3(view.Player.Feet.x, 0, view.Player.Feet.z)).normalized * 0.9f, 0.3f, 6f, false);
                yield return LookAtSmooth(view.Items.PositionOf(it));
                Check(view.Current.Kind == TargetKind.Item && view.Current.Uid == it.Uid, $"crosshair targets the coin (got {view.Current.Kind})");
                Press();
                yield return null;
                yield return null;
                Check(sim.CarriedCount == 1 && sim.FindLoose(it.Uid) == null, "E picks the coin up");
            }
            // hands full
            var it2 = NearestLoose(view.Player.Feet);
            if (it2 != null)
            {
                yield return LookAtSmooth(view.Items.PositionOf(it2));
                Press(false, true);
                yield return null;
                yield return null;
                Check(sim.CarriedCount == 1 && sim.FindLoose(it2.Uid) != null, "bare hands hold only one thing (left click refused)");
            }

            // deposit
            yield return GoToKiosk();
            double cash0 = sim.S.cash;
            Press();
            yield return null;
            yield return null;
            Check(sim.S.cash > cash0 && sim.CarriedCount == 0, $"deposit pays out ({Fmt.Money(sim.S.cash - cash0)})");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("ui_deposit");

            // buy a bigger container at the Maintenance Terminal, through its UI
            sim.DebugAddCash(1);
            yield return GoToTerminal();
            Check(view.Current.Kind == TargetKind.Terminal, $"crosshair targets the Maintenance Terminal (got {view.Current.Kind})");
            Press();
            yield return null;
            yield return new WaitForSeconds(0.3f);
            Check(terminal.IsOpen, "E opens MAINT-OS 95");
            Check(ClickSelectable(FindButton("Carry")), "open the Carry branch");
            yield return null;
            Check(ClickSelectable(FindButton("Node:carry_cup")), "select the Paper Cup node");
            yield return null;
            Check(ClickSelectable(FindButton("Buy", b => b.interactable)), "click INSTALL");
            yield return null;
            Check(sim.CarryTier == 1 && sim.CarryCapacity == 5, $"paper cup bought, holds 5 (got {sim.CarryCapacity})");
            yield return Shot("ui_terminal");
            sim.DebugAddCash(40);
            Check(ClickSelectable(FindButton("Tools")), "open the Tools branch");
            yield return null;
            float reach0 = sim.Reach;
            ClickSelectable(FindButton("Node:grab_grabber"));
            yield return null;
            Check(ClickSelectable(FindButton("Buy", b => b.interactable)), "install the Litter Grabber");
            yield return null;
            Check(sim.GrabTier == 1 && sim.Reach > reach0, $"grabber adds reach ({reach0:0.0} → {sim.Reach:0.0} m)");
            Check(!ClickSelectable(FindButton("Node:grab_rake")) || !FindButton("Buy", b => b.interactable), "the rake stays locked until the net is installed");
            Check(ClickSelectable(FindButton("Close")), "close the terminal");
            yield return null;
            Check(!terminal.IsOpen, "terminal closed");
            yield return EnterFountain();
            int got = 0;
            for (int i = 0; i < 5; i++)
            {
                var c = NearestLoose(view.Player.Feet);
                if (c == null) break;
                Vector3 flat = new Vector3(c.X, 0, c.Z);
                if ((flat - new Vector3(view.Player.Feet.x, 0, view.Player.Feet.z)).magnitude > 1.6f)
                    yield return WalkTo(flat - (flat - new Vector3(view.Player.Feet.x, 0, view.Player.Feet.z)).normalized * 0.9f, 0.3f, 6f, false);
                yield return LookAtSmooth(view.Items.PositionOf(c), 0.15f);
                int before = sim.CarriedCount;
                Press();
                yield return null;
                yield return null;
                if (sim.CarriedCount > before) got++;
            }
            Check(got == 5 && sim.CarriedCount == 5, $"picked up five coins into the cup ({got})");
            yield return GoToKiosk();
            double cash1 = sim.S.cash;
            Press();
            yield return null;
            yield return null;
            Check(sim.S.cash > cash1 && sim.S.deposits == 2, "second deposit pays for all five");

            // the crowd has been arriving and tossing the whole time
            Check(sim.Shoppers.Count > 0 && view.Crowd.Count == sim.Shoppers.Count, $"shoppers walk in and are drawn ({sim.Shoppers.Count})");
            float waitToss = 0;
            while (sim.S.tosses == 0 && waitToss < 25) { waitToss += Time.deltaTime; yield return null; }
            Check(sim.S.tosses > 0, $"shoppers toss things into the fountain ({sim.S.tosses} after {sim.Time:0}s; " +
                  string.Join(", ", sim.Shoppers.ConvertAll(x => $"{x.Def.Id} {x.State} ({x.X:0},{x.Z:0})→({x.TX:0},{x.TZ:0})")) + ")");

            // buy the first beautification at the Fountain Improvement Plan
            sim.DebugAddCash(1);
            double tossInterval0 = sim.TossInterval;
            yield return GoToBoard();
            Check(view.Current.Kind == TargetKind.Board, $"crosshair targets the Fountain Improvement Plan (got {view.Current.Kind})");
            Press();
            yield return null;
            yield return null;
            Check(sim.TechLevel("fountain_scrub") == 1 && sim.Wishability >= 3, $"approving the job buys 'Scrub the Grime' (wishability {sim.Wishability})");
            Check(sim.TossInterval < tossInterval0, $"shoppers toss more often ({tossInterval0:0.0}s → {sim.TossInterval:0.0}s)");
            Check(view.Fountain.Grime < 0.5f, "the fountain looks scrubbed");
            double w0 = sim.TierWeights()[1];
            Check(w0 > 0, "nickels are now in the toss mix");

            // catch a True Wish
            yield return EnterFountain();
            var wish = sim.DebugSpawnWish(view.Player.Feet.x + 1.5f, view.Player.Feet.z + 1.5f);
            Check(wish != null, "a wish rises from the water");
            yield return new WaitForSeconds(1.6f);
            if (wish != null)
            {
                yield return LookAtSmooth(view.WishOrbs.PositionOf(wish.Uid), 0.2f);
                Check(view.Current.Kind == TargetKind.Wish && view.Current.Uid == wish.Uid, $"crosshair targets the wish (got {view.Current.Kind})");
                double tok0 = sim.S.wishTokens;
                Press();
                yield return null;
                yield return null;
                Check(sim.S.wishesCaught == 1 && sim.S.wishTokens > tok0 && sim.WishFound(wish.Def.Id), "E catches the wish (cash, tokens and a journal entry)");
            }
            yield return Shot("ui_wish");

            // ── the goldfish: E puts it back in the water (to applause), it never goes in your cup ──
            var fish = DropGoldfishNearby();
            yield return null;
            yield return LookAtSmooth(view.Items.PositionOf(fish), 0.2f);
            Check(view.Current.Kind == TargetKind.Item && view.Current.Uid == fish.Uid, $"crosshair targets the goldfish (got {view.Current.Kind})");
            double tokFish = sim.S.wishTokens;
            int carriedFish = sim.CarriedCount;
            Press();
            yield return null;
            yield return null;
            Check(sim.S.fishReturned == 1 && Math.Abs(sim.S.wishTokens - tokFish - 1) < 1e-9 && sim.CarriedCount == carriedFish && sim.FindLoose(fish.Uid) != null,
                  "E returns the goldfish to the water (+1 Wish Token) instead of pocketing it");
            yield return new WaitForSeconds(0.4f);
            yield return Shot("ui_goldfish");

            // ── Officer Doug: wade on after his whistle and he fines you; step out and he calms down ──
            Check(view.Wading, "still wading in the fountain");
            for (float w = 0; sim.Guard.State != GuardState.Patrol && w < 10; w += Time.deltaTime) yield return null;
            double cashFine = sim.S.cash;
            double fines0 = sim.S.finesPaid;
            sim.DebugGuardCheck();
            yield return new WaitForSeconds(0.4f);
            Check(sim.Guard.State == GuardState.Warning && sim.Guard.Line != null, $"Officer Doug blows his whistle at you (\"{sim.Guard.Line}\")");
            yield return new WaitForSeconds(5.3f);
            Check(sim.S.finesPaid == fines0 + 1 && sim.S.cash < cashFine, $"staying in the water gets you fined ({Fmt.Money(cashFine - sim.S.cash)})");
            yield return Shot("ui_guard_fine");
            for (float w = 0; sim.Guard.State != GuardState.Patrol && w < 10; w += Time.deltaTime) yield return null;
            sim.DebugGuardCheck();
            yield return new WaitForSeconds(0.4f);
            Check(sim.Guard.State == GuardState.Warning, "a second whistle");
            yield return WalkTo(new Vector3(0, 0, -11.5f), 0.5f, 4f);
            yield return new WaitForSeconds(0.3f);
            Check(!view.Wading && sim.Guard.State != GuardState.Warning && sim.S.finesPaid == fines0 + 1, "stepping out of the water after the whistle avoids the fine");

            // ── Chad the rival diver: wade up to him and he drops everything and runs; E works too ──
            double chased0 = sim.S.rivalsChased;
            yield return SummonRival();
            Check(sim.Rival.State == RivalState.Stealing && GameObject.Find("Chad the Rival Diver") != null, "Chad the rival diver climbs into the fountain");
            yield return new WaitForSeconds(2.5f);
            int stolen = sim.Rival.Loot.Count;
            yield return EnterFountain();
            yield return ApproachRival(1.2f);
            yield return null;
            Check(sim.S.rivalsChased == chased0 + 1 && sim.Rival.State == RivalState.Fleeing, $"wading up to Chad chases him off (he had pocketed {stolen}+ items)");
            yield return Shot("ui_chad_chased");
            yield return SummonRival();
            yield return ApproachRival(4.5f);
            yield return LookAtSmooth(view.Hazards.RivalHead.position - Vector3.up * 0.3f, 0.15f);
            Check(view.Current.Kind == TargetKind.Rival, $"crosshair targets Chad (got {view.Current.Kind})");
            Press();
            yield return null;
            yield return null;
            Check(sim.S.rivalsChased == chased0 + 2 && sim.Rival.State == RivalState.Fleeing, "E on Chad chases him off too");
            yield return WalkTo(new Vector3(0, 0, -6.5f), 0.6f, 10f, false);

            // ── the factory: hamster wheels, a skimmer bot, a belt line and a hopper, built through build mode ──
            foreach (var t in new[] { "unlock_hamster", "unlock_skimmer", "unlock_belts", "unlock_hopper" }) sim.DebugSetTech(t, 1);
            sim.DebugAddCash(300);
            yield return WalkTo(new Vector3(0, 0, -11.5f), 0.5f);
            yield return WalkTo(new Vector3(13.5f, 0, -3.5f), 0.4f);
            SetFlag(i => i.Hotbar = 3);
            yield return null;
            yield return null;
            Check(view.Build.Active, "3 switches to build mode");
            yield return PickFromCatalogue("gen_hamster");
            yield return AimAt(new Vector3(12.5f, 0, 3.5f));
            Check(view.Build.HasSpot && view.Build.Valid && view.Build.AX == 12 && view.Build.AZ == 3,
                  $"the ghost snaps to the grid ({view.Build.AX},{view.Build.AZ}) valid={view.Build.Valid} {view.Build.Reason}");
            yield return Shot("ui_build_ghost");
            Press(false, true);
            yield return null;
            yield return null;
            Check(sim.CountBuilt("gen_hamster") == 1, "click builds a hamster wheel");
            yield return AimAt(new Vector3(13.5f, 0, 3.5f));
            Press(false, true);
            yield return null;
            yield return null;
            yield return PickFromCatalogue("intake_skimmer");
            yield return AimAt(new Vector3(12.5f, 0, 3.5f));
            Check(!view.Build.Valid, $"can't build on top of the hamster wheel ({view.Build.Reason})");
            yield return AimAt(new Vector3(10.9f, 0, 0.5f));
            Check(view.Build.Valid && view.Build.ARot == 3, $"the skimmer dock turns to face the fountain (rot {view.Build.ARot}, {view.Build.Reason})");
            Press(false, true);
            yield return null;
            yield return null;
            Check(sim.CountBuilt("intake_skimmer") == 1, "skimmer dock built at the rim");
            yield return PickFromCatalogue("belt");
            yield return AimAt(new Vector3(12.5f, 0, 0.5f));
            SetFlag(i => { i.PrimaryDown = true; i.Primary = true; });
            yield return null;
            for (int k = 0; k <= 20; k++)
            {
                view.Player.LookAt(new Vector3(Mathf.Lerp(12.5f, 15.5f, k / 20f), 0, 0.5f));
                SetFlag(i => i.Primary = true);
                yield return null;
            }
            SetFlag(i => i.Primary = false);
            yield return null;
            bool line = true;
            for (int x = 12; x <= 15; x++) { var b = sim.At(x, 0); line &= b != null && b.Def.IsBelt && b.Rot == 1; }
            Check(line, "dragging lays a straight belt line pointing away from the dock");
            yield return PickFromCatalogue("hopper");
            yield return AimAt(new Vector3(17f, 0, 1f));
            Press(false, true);
            yield return null;
            yield return null;
            Check(sim.CountBuilt("hopper") == 1 && sim.At(16, 0)?.Def.Id == "hopper", "deposit hopper built at the end of the line");
            Check(sim.PowerGen >= sim.PowerUse && sim.PowerUse > 0, $"power: {sim.PowerGen} kW for {sim.PowerUse} kW");
            float waitSale = 0;
            while (sim.S.hopperItems == 0 && waitSale < 80) { waitSale += Time.deltaTime; yield return null; }
            Check(sim.S.hopperItems > 0 && sim.S.machinePicked > 0, $"the skimmer's coins ride the belts into the hopper and get paid ({sim.S.hopperItems} items, {Fmt.Money(sim.S.hopperCash)}, {waitSale:0}s)");
            yield return AimAt(new Vector3(12, 0.8f, 1.5f));
            yield return Shot("ui_factory");
            SetFlag(i => i.Demolish = true);
            yield return null;
            yield return AimAt(new Vector3(13.5f, 0.15f, 0.5f));
            double cashD = sim.S.cash;
            Press(false, true);
            yield return null;
            yield return null;
            Check(sim.At(13, 0) == null && sim.S.cash > cashD, "demolish mode removes a belt and refunds it");
            SetFlag(i => i.Demolish = true);
            SetFlag(i => i.Hotbar = 1);
            yield return null;
            Check(!view.Build.Active, "1 goes back to grabbing");

            // ── the crust: dig by hand, gunk below the loose layer, bare concrete, the next contract ──
            sim.DebugSetTech("dig_sandshovel", 1);
            SetFlag(i => i.Hotbar = 2);
            yield return null;
            yield return null;
            Check(view.DigMode, "2 takes out the dig tool");
            yield return WalkTo(new Vector3(0, 0, -11.5f), 0.5f);
            yield return EnterFountain();
            var df = view.Player.Feet;
            yield return AimAt(view.Fountain.SurfacePoint(df.x + 0.6f, df.z + 1.3f));
            Check(view.Current.Kind == TargetKind.Crust, $"crosshair targets the crust (got {view.Current.Kind})");
            double dug0 = sim.S.dug;
            int loose0 = sim.Loose.Count;
            for (int k = 0; k < 8; k++) { Press(false, true); yield return new WaitForSeconds(0.7f); }
            Check(sim.S.dug >= dug0 + 6, $"swinging the shovel digs the crust ({sim.S.dug - dug0:0} scoops)");
            Check(sim.Loose.Count > loose0, $"loose-layer loot lands in the water ({sim.Loose.Count - loose0} new items)");
            sim.DebugSetDepth(0.2);
            yield return new WaitForSeconds(3f);
            Check(sim.Stratum >= 1 && view.Fountain.SurfaceY < FountainView.CrustTop - 1f, $"the crust sinks to stratum {sim.Stratum} (surface y {view.Fountain.SurfaceY:0.00})");
            Check(GameObject.Find("Scaffold Ramp") != null, "a scaffold ramp spirals down the wall");
            df = view.Player.Feet;
            yield return AimAt(view.Fountain.SurfacePoint(df.x + 0.6f, df.z + 1.3f));
            for (int k = 0; k < 6; k++) { Press(false, true); yield return new WaitForSeconds(0.7f); }
            Check(sim.Loose.Exists(l => Content.Items[l.Type].Cat == ItemCat.Gunk), "below the loose layer the crust comes up as gunk chunks");
            yield return Shot("ui_dig_gunk");
            SetFlag(i => i.Hotbar = 1);
            sim.DebugFinishMall();
            Check(sim.MallCleared && sim.S.treasures.Contains(sim.Mall.Id), "bare concrete clears the mall and banks its treasure");
            yield return new WaitForSeconds(4.5f);
            Check(modals.IsOpen && modals.OpenName == "contract", "the contract opens after bare concrete");
            yield return Shot("ui_contract");
            Check(ClickSelectable(FindButton("Sign")), "click 'Sign the contract'");
            yield return new WaitForSeconds(0.6f);
            Check(sim.S.mallIndex == 1 && !sim.MallCleared && sim.S.luckyPennies > 0 && sim.Buildings.Count == 0 && sim.S.cash == 0,
                  $"moved to {sim.Mall.Name} with {sim.S.luckyPennies} Lucky Pennies; factory and cash reset");
            Check(modals.IsOpen && modals.OpenName == "intro", "the new mall's intro shows");
            Check(ClickSelectable(FindButton("Go")), "start the new contract");
            yield return null;
            yield return GoToTerminal();
            Press();
            yield return new WaitForSeconds(0.3f);
            Check(terminal.IsOpen && ClickSelectable(FindButton("Head Office")), "open the Head Office tab");
            yield return null;
            ClickSelectable(FindButton("Node:ho_card"));
            yield return null;
            Check(ClickSelectable(FindButton("Buy", b => b.interactable)), "buy a Head Office perk with Lucky Pennies");
            yield return null;
            Check(sim.TechLevel("ho_card") == 1, "Company Credit Card bought");
            yield return Shot("ui_head_office");
            ClickSelectable(FindButton("Close"));
            yield return null;

            // ── the late malls' own machines: sold in their own mall only, and each one works ──
            yield return MallMachineChecks();

            // menus
            modals.OpenSettings();
            yield return new WaitForSeconds(0.3f);
            Check(modals.IsOpen && modals.OpenName == "settings", "Esc menu opens");
            Check(ClickSelectable(FindButton("1.23e6")), "switch number format");
            Check(sim.S.notation == 1, "notation setting changed");
            ClickSelectable(FindButton("1.23M"));
            var fwd0 = view.Player.Feet;
            Drive(new Vector2(0, 1));
            yield return new WaitForSeconds(0.3f);
            Drive(Vector2.zero);
            Check((view.Player.Feet - fwd0).magnitude < 0.05f, "the player can't walk while a menu is open");
            Check(ClickSelectable(FindButton("Close")), "close settings");
            yield return null;
            Check(!modals.IsOpen, "settings closed");
            modals.OpenJournal(0);
            yield return new WaitForSeconds(0.3f);
            Check(ClickSelectable(FindButton("Relic Museum")), "switch to the Relic Museum tab");
            yield return new WaitForSeconds(0.2f);
            Check(ClickSelectable(FindButton("Stats")), "switch to the Stats tab");
            yield return new WaitForSeconds(0.2f);
            yield return Shot("ui_journal_stats");
            Check(ClickSelectable(FindButton("Close")), "close the journal");
            yield return null;

            // save round trip (the -loadtest run reads this file back)
            view.StorePose(sim.S);
            var snap = JsonUtility.ToJson(sim.Snapshot());
            var back = new Sim(JsonUtility.FromJson<SaveData>(snap));
            Check(Math.Abs(back.S.cash - sim.S.cash) < 1e-9 && back.Loose.Count == sim.Loose.Count && back.CarryTier == sim.CarryTier && back.Buildings.Count == sim.Buildings.Count,
                  $"save → load keeps cash, {back.Loose.Count} loose items, the carry tier and {back.Buildings.Count} buildings");
            yield return Shot("ui_after_test");
            Debug.Log($"[UITEST] done: {uiPass} passed, {uiFail} failed");
            Save();
            Application.Quit();
        }

        /// <summary>Jump to a late mall (tests only) with its regular and mall-only buildings researched and the lights on.</summary>
        IEnumerator JumpToMall(int m)
        {
            sim.DebugJumpToMall(m);
            view.RebuildMall();
            sfx.PlayMusicFor(sim.Mall, sim.Remodel);
            foreach (var t in Content.Techs) if (t.Kind == TechKind.Unlock && !t.MallOnly) sim.DebugSetTech(t.Id, 1);
            var solar = Content.Buildables[Content.BuildIndex["gen_solar"]];
            for (int i = 0; i < 6; i++) sim.Place(solar, 20 + (i % 3) * 3, -27 + (i / 3) * 3, 0, true);
            yield return new WaitForSeconds(0.6f);
        }

        /// <summary>uitest: Galleria Aurelia's cannon (bought and built through the UI), Skyport's carousel, the Lagoon's
        /// slot machine and Eternity's Old Well; none of them is for sale anywhere else.</summary>
        IEnumerator MallMachineChecks()
        {
            BuildDef D(string id) => Content.Buildables[Content.BuildIndex[id]];

            // Neon Galaxy doesn't sell Galleria Aurelia's cannon
            terminal.Open(TechBranch.Intake);
            yield return new WaitForSeconds(0.3f);
            Check(FindButton("Node:unlock_pump") != null && FindButton("Node:unlock_cannon") == null, "the champagne cannon isn't sold outside Galleria Aurelia");
            terminal.Close();
            yield return null;

            // Galleria Aurelia: buy Champagne Blasting at the terminal, build a cannon behind the rim, fire it
            yield return JumpToMall(2);
            sim.DebugSetDepth(0.25);
            int ci = Content.TechIndex["unlock_cannon"];
            sim.DebugAddCash(sim.TechCost(ci) + D("dig_cannon").Cost * sim.Scale * 3);
            terminal.Open(TechBranch.Intake);
            yield return new WaitForSeconds(0.3f);
            Check(ClickSelectable(FindButton("Node:unlock_cannon")), "Galleria Aurelia's terminal sells Champagne Blasting");
            yield return null;
            Check(ClickSelectable(FindButton("Buy", b => b.interactable)), "install Champagne Blasting");
            yield return null;
            Check(sim.TechLevel(ci) == 1 && sim.BuildUnlocked(D("dig_cannon")), "the champagne cork cannon is researched");
            yield return Shot("ui_mall_only_tech");
            ClickSelectable(FindButton("Close"));
            yield return null;
            view.Player.Place(new Vector3(13.5f, 0.05f, -3.5f), 0, 0);
            yield return PickFromCatalogue("dig_cannon");
            yield return AimAt(new Vector3(22.1f, 0, 5.9f));
            Check(!view.Build.Valid && view.Build.Reason != null && view.Build.Reason.StartsWith("Too far"), $"a cannon 23 m out is out of range ({view.Build.Reason})");
            yield return AimAt(new Vector3(16.1f, 0, 0.9f));
            Check(view.Build.Valid && view.Build.ARot == 3 && view.Build.AX == 16 && view.Build.AZ == 0,
                  $"the cannon stands back from the rim, aimed at the fountain ({view.Build.AX},{view.Build.AZ}) rot {view.Build.ARot} {view.Build.Reason}");
            Press(false, true);
            yield return null;
            yield return null;
            Check(sim.CountBuilt("dig_cannon") == 1, "click builds a champagne cork cannon");
            SetFlag(i => i.Hotbar = 1);
            double pops0 = sim.S.cannonBlasts;
            sim.DebugFireCannons();
            yield return new WaitForSeconds(1.5f);
            Check(sim.S.cannonBlasts > pops0 && sim.Loose.Exists(l => Content.Items[l.Type].Id == "rinsed"),
                  $"pop: the cannon blasts a slab into the water, already rinsed ({sim.S.cannonBlasts - pops0} pops)");
            yield return AimAt(new Vector3(10f, 1f, 0.5f));
            yield return Shot("ui_cannon");

            // Skyport: the carousel is in the catalogue (the cannon isn't), and its drones empty a lineless borer
            yield return JumpToMall(3);
            sim.DebugSetDepth(0.25);
            sim.DebugSetTech("unlock_carousel", 1);
            sim.DebugAddCash(D("carousel").Cost * sim.Scale * 3);
            Check(!sim.BuildUnlocked(D("dig_cannon")) && sim.BuildUnlocked(D("carousel")), "the cannon stayed in Aurelia; Skyport builds carousels");
            view.Player.Place(new Vector3(-20f, 0.05f, 6f), 0, 0);
            view.Build.Rot = 0;
            SetFlag(i => i.Catalogue = true);
            yield return null;
            yield return new WaitForSeconds(0.15f);
            Check(buildMenu.IsOpen && FindButton("Build:carousel") != null && FindButton("Build:dig_cannon") == null, "Skyport's catalogue lists the carousel and not the cannon");
            yield return Shot("ui_catalogue_skyport");
            Check(ClickSelectable(FindButton("Build:carousel")), "pick carousel from the build catalogue");
            yield return null;
            Check(!buildMenu.IsOpen && view.Build.Selected?.Id == "carousel", "holding carousel");
            yield return AimAt(new Vector3(-22.4f, 0, 11.6f));
            Check(view.Build.Valid && view.Build.AX == -24 && view.Build.AZ == 10, $"the carousel ghost fits at the back ({view.Build.AX},{view.Build.AZ}) {view.Build.Reason}");
            Press(false, true);
            yield return null;
            yield return null;
            Check(sim.CountBuilt("carousel") == 1 && sim.At(-24, 10)?.Def.Id == "carousel", "click builds a baggage claim carousel");
            SetFlag(i => i.Hotbar = 1);
            sim.Place(D("proc_tumbler"), -24, 13, 0, true);
            sim.Place(D("proc_sorter"), -24, 15, 0, true);
            sim.Place(D("hopper2"), -24, 17, 0, true);
            var borer = sim.Place(D("dig_borer"), -13, 0, 1, true);
            float waitDrone = 0;
            while (sim.S.droneTrips < 1 && waitDrone < 30) { waitDrone += Time.deltaTime; yield return null; }
            Check(borer != null && sim.S.droneTrips >= 1 && GameObject.Find("Baggage Drone") != null,
                  $"a baggage drone flies the borer's chunks to the carousel ({sim.S.droneTrips} trips, {waitDrone:0}s)");
            yield return LookAtSmooth(new Vector3(-18f, 2.5f, 5f));
            yield return Shot("ui_carousel");

            // the Lucky Lagoon: a jackhammer rig feeds a slot machine, which spins gunk into loot, then hits 7-7-7
            yield return JumpToMall(4);
            sim.DebugSetDepth(0.25);
            sim.DebugSetTech("unlock_slots", 1);
            sim.Place(D("dig_rig"), 11, -1, 3, true);
            var slots = sim.Place(D("proc_slots"), 12, 0, 1, true);
            sim.Place(D("hopper2"), 14, 0, 1, true);
            view.Player.Place(new Vector3(13.2f, 0.05f, -5f), 0, 0);
            yield return LookAtSmooth(new Vector3(12.6f, 1.5f, 0));
            float waitSpin = 0;
            while (sim.S.slotSpins < 1 && waitSpin < 40) { waitSpin += Time.deltaTime; yield return null; }
            Check(slots != null && sim.S.slotSpins >= 1, $"the slot machine spins the rig's gunk ({sim.S.slotSpins} spins, {waitSpin:0}s)");
            double jackpots0 = sim.S.jackpots;
            if (slots != null) sim.DebugJackpot(slots);
            yield return new WaitForSeconds(0.4f);
            Check(sim.S.jackpots > jackpots0 && sim.Loose.Exists(l => Content.Items[l.Type].Id == "jackpot"), "7-7-7 sprays jackpot tokens over the fountain");
            yield return Shot("ui_jackpot");

            // Eternity Plaza: one Old Well at the rim, which grants the wishes nobody catches
            yield return JumpToMall(5);
            sim.DebugSetTech("unlock_well", 1);
            sim.DebugAddCash(D("wishing_well").Cost * sim.Scale * 3);
            view.Player.Place(new Vector3(-14f, 0.05f, -3f), 0, 0);
            yield return PickFromCatalogue("wishing_well");
            yield return AimAt(new Vector3(-10.9f, 0, 0.9f));
            Check(view.Build.Valid && view.Build.ARot == 1 && view.Build.AX == -12 && view.Build.AZ == 1,
                  $"the well faces the fountain at the rim ({view.Build.AX},{view.Build.AZ}) rot {view.Build.ARot} {view.Build.Reason}");
            Press(false, true);
            yield return null;
            yield return null;
            Check(sim.CountBuilt("wishing_well") == 1, "click builds the Old Well");
            yield return AimAt(new Vector3(-12.5f, 0, -1.6f));
            Check(!view.Build.Valid && view.Build.Reason != null && view.Build.Reason.Contains("only one Old Well"), $"there's only one Old Well ({view.Build.Reason})");
            SetFlag(i => i.Hotbar = 1);
            yield return LookAtSmooth(new Vector3(-8f, 1.2f, 1f));
            double dugW = sim.S.dug, grantedW = sim.S.wellWishes;
            var wish = sim.DebugSpawnWish(-4.5f, 1.5f);
            float waitWish = 0;
            while (sim.S.wellWishes <= grantedW && waitWish < 8) { waitWish += Time.deltaTime; yield return null; }
            Check(wish != null && sim.S.wellWishes > grantedW && sim.S.dug > dugW,
                  $"an uncaught wish falls into the well and crust dissolves ({sim.S.dug - dugW:0} scoops, {waitWish:0.0}s)");
            yield return new WaitForSeconds(0.3f);
            yield return Shot("ui_well");
        }
    }
}
