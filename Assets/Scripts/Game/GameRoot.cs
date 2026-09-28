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
                StartCoroutine(Watchdog(Time.realtimeSinceStartup + 420));
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

            modals.OnSign = () =>
            {
                if (!sim.MallCleared) return;
                sim.Prestige();
                modals.OpenIntro(false);
            };
        }

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

        /// <summary>The floor just outside the stepping stone nearest to (x, z): walk in from there like EnterFountain does.</summary>
        static Vector3 OutsideStoneNear(float x, float z)
        {
            float want = Mathf.Atan2(z, x) * Mathf.Rad2Deg, stone = Layout.StoneAngles[0], bestD = 999f;
            foreach (float a in Layout.StoneAngles)
            {
                float d = Mathf.Abs(Mathf.DeltaAngle(a, want));
                if (d < bestD) { bestD = d; stone = a; }
            }
            return new Vector3(Mathf.Cos(stone * Mathf.Deg2Rad), 0, Mathf.Sin(stone * Mathf.Deg2Rad)) * 11.2f;
        }

        /// <summary>Hop in over the stepping stone nearest to Chad, then walk up to him (he flees within 2.2 m).</summary>
        IEnumerator ApproachRival(float stopAt, float timeout)
        {
            var outside = OutsideStoneNear(sim.Rival.X, sim.Rival.Z);
            view.Player.Place(outside + Vector3.up * 0.05f, Mathf.Atan2(-outside.x, -outside.z) * Mathf.Rad2Deg, 0);
            yield return null;
            yield return WalkTo(outside * (6.5f / 11.2f), 0.4f, 8f, false);
            for (float spent = 0; sim.Rival.State == RivalState.Stealing && spent < timeout; spent += 0.5f)
            {
                var at = new Vector3(sim.Rival.X, 0, sim.Rival.Z);
                var feet = view.Player.Feet;
                if (new Vector2(at.x - feet.x, at.z - feet.z).magnitude <= stopAt) yield break;
                yield return WalkTo(at, stopAt, 0.5f);
            }
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
            StartCoroutine(Watchdog(Time.realtimeSinceStartup + 660));
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
            yield return Shot("07j2_terminal_security");
            terminal.Close();

            // hazards: Officer Doug's whistle, Chad the rival diver, and a goldfish that has to go back
            yield return EnterFountain();
            sim.DebugGuardCheck();
            float guardWait = 0;
            while (sim.Guard.State != GuardState.Warning && guardWait < 5) { guardWait += Time.deltaTime; yield return null; }
            yield return LookAtSmooth(view.Hazards.GuardHead.position);
            yield return Shot("07q_guard_warning");
            yield return WalkTo(new Vector3(0, 0, -12.5f), 0.5f, 6f);
            sim.DebugRival();
            float rivalWait = 0;
            while (sim.Rival.State != RivalState.Stealing && rivalWait < 30) { rivalWait += Time.deltaTime; yield return null; }
            yield return new WaitForSeconds(2.5f);
            yield return LookAtSmooth(view.Hazards.RivalHead.position);
            yield return Shot("07r_rival");
            sim.ChaseRival();
            yield return new WaitForSeconds(0.8f);
            yield return Shot("07s_rival_fleeing");
            Debug.Log($"[TOUR] hazards: guard {sim.Guard.State} after {guardWait:0.0}s, rival arrived after {rivalWait:0}s and is {sim.Rival.State}");
            yield return EnterFountain();
            var fp = view.Player.Feet;
            var fish = sim.AddLoose(Content.Type("goldfish"), fp.x + 0.3f, fp.z + 1.5f, 0, null);
            yield return new WaitForSeconds(0.4f);
            yield return LookAtSmooth(view.Items.PositionOf(fish));
            yield return Shot("07t_goldfish");
            Press();
            yield return new WaitForSeconds(0.5f);
            yield return Shot("07u_goldfish_returned");

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
            }
            Application.Quit();
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

            // ── hazards: the Security tab, Officer Doug's whistle and fine, Chad the rival diver, the goldfish ──
            yield return GoToTerminal();
            Press();
            yield return new WaitForSeconds(0.3f);
            Check(terminal.IsOpen && ClickSelectable(FindButton("Security")), "open the Security tab");
            yield return null;
            ClickSelectable(FindButton("Node:sec_donuts"));
            yield return null;
            Check(terminal.Branch == TechBranch.Security && ClickSelectable(FindButton("Buy", b => b.interactable)) && sim.TechLevel("sec_donuts") == 1 && sim.FineMult < 1,
                  $"Donut Diplomacy bought on the Security tab (fines × {sim.FineMult:0.00})");
            yield return Shot("ui_security");
            ClickSelectable(FindButton("Close"));
            yield return null;

            yield return EnterFountain();
            double fined = 0, finesBefore = sim.S.finesPaid;
            Action<double> onFine = amount => fined += amount;
            sim.OnFined += onFine;
            sim.DebugGuardCheck();
            float waitGuard = 0;
            while (sim.Guard.State != GuardState.Warning && waitGuard < 5) { waitGuard += Time.deltaTime; yield return null; }
            Check(view.Wading && sim.Guard.State == GuardState.Warning && !string.IsNullOrEmpty(sim.Guard.Line), $"Officer Doug blows the whistle on a wading player: \"{sim.Guard.Line}\"");
            yield return LookAtSmooth(view.Hazards.GuardHead.position);
            yield return Shot("ui_guard_warning");
            yield return new WaitForSeconds(5.5f);
            Check(sim.S.finesPaid > finesBefore && fined > 0, $"staying in the water gets you fined ({Fmt.Money(fined)})");
            yield return new WaitForSeconds(3.5f);
            finesBefore = sim.S.finesPaid;
            sim.DebugGuardCheck();
            waitGuard = 0;
            while (sim.Guard.State != GuardState.Warning && waitGuard < 5) { waitGuard += Time.deltaTime; yield return null; }
            yield return WalkTo(new Vector3(0, 0, -12.5f), 0.5f, 6f);
            yield return new WaitForSeconds(0.3f);
            Check(!view.Wading && sim.Guard.State != GuardState.Warning && sim.S.finesPaid == finesBefore, "climbing out after the whistle avoids the fine");
            sim.OnFined -= onFine;

            // Chad: shows up, pockets coins, and flees (dropping them) when you walk up to him or press E on him
            int dropped = -1;
            Action<Rival, int> onChase = (who, count) => dropped = count;
            sim.OnRivalChased += onChase;
            double chased0 = sim.S.rivalsChased;
            sim.DebugRival();
            float waitRival = 0;
            while (sim.Rival.State != RivalState.Stealing && waitRival < 30) { waitRival += Time.deltaTime; yield return null; }
            Check(sim.Rival.State == RivalState.Stealing && view.Hazards.RivalHead.gameObject.activeInHierarchy, $"Chad the rival diver wades in ({waitRival:0}s after his cue)");
            yield return new WaitForSeconds(2f);
            yield return LookAtSmooth(view.Hazards.RivalHead.position);
            yield return Shot("ui_rival");
            yield return ApproachRival(0.8f, 15f);
            Check(sim.S.rivalsChased > chased0 && dropped >= 0, $"walking up to Chad chases him off (he drops {dropped} stolen item{(dropped == 1 ? "" : "s")})");
            float waitAway = 0;
            while (sim.Rival.State != RivalState.Away && waitAway < 20) { waitAway += Time.deltaTime; yield return null; }
            view.Player.Place(new Vector3(0, 0.05f, -15f), 0, 0);
            double chased1 = sim.S.rivalsChased;
            sim.DebugRival();
            waitRival = 0;
            while (sim.Rival.State != RivalState.Stealing && waitRival < 30) { waitRival += Time.deltaTime; yield return null; }
            var rivalStone = OutsideStoneNear(sim.Rival.X, sim.Rival.Z);
            view.Player.Place(rivalStone + Vector3.up * 0.05f, Mathf.Atan2(-rivalStone.x, -rivalStone.z) * Mathf.Rad2Deg, 0);
            yield return null;
            yield return LookAtSmooth(view.Hazards.RivalHead.position, 0.15f);
            Check(view.Current.Kind == TargetKind.Rival, $"the crosshair finds Chad from the rim (got {view.Current.Kind})");
            Press();
            yield return null;
            yield return null;
            Check(sim.S.rivalsChased > chased1 && sim.Rival.State == RivalState.Fleeing, "E on Chad shoos him out too");
            sim.OnRivalChased -= onChase;

            // the goldfish: you don't carry it, you put it straight back (to applause and a Wish Token)
            view.Player.Place(new Vector3(0, 0.05f, -12f), 0, 0);
            yield return null;
            yield return EnterFountain();
            var pf = view.Player.Feet;
            var fish = sim.AddLoose(Content.Type("goldfish"), pf.x + 0.3f, pf.z + 1.4f, 0, null);
            yield return new WaitForSeconds(0.3f);
            yield return LookAtSmooth(view.Items.PositionOf(fish));
            Check(view.Current.Kind == TargetKind.Item && view.Current.Uid == fish.Uid, $"crosshair targets the goldfish (got {view.Current.Kind})");
            int carried0 = sim.CarriedCount;
            double tok0f = sim.S.wishTokens, returned0 = sim.S.fishReturned;
            Press();
            yield return null;
            yield return null;
            Check(sim.S.fishReturned == returned0 + 1 && sim.CarriedCount == carried0 && sim.S.wishTokens == tok0f + 1 && sim.FindLoose(fish.Uid)?.State == LooseState.Airborne,
                  "E on the goldfish puts it straight back in the water (+1 Wish Token, nothing carried)");
            yield return Shot("ui_goldfish");

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
    }
}
