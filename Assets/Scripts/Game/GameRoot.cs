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
                StartCoroutine(Watchdog(Time.realtimeSinceStartup + 200));
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
            sim.OnToss += (s, it) => sfx.PlayAt("throw", new Vector3(s.X, 1.5f, s.Z), 0.25f, 0.15f, 0.05f, 1.4f);
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

            bool modal = modals.IsOpen;
            if (!testing) HandleKeys(modal);
            modal = modals.IsOpen;
            UpdateCursor(modal);

            FPInput input = scripted ?? (testing ? default : ReadInput());
            if (scripted.HasValue)
            {
                var s = scripted.Value;
                s.Interact = false;
                s.PrimaryDown = false;
                s.Look = Vector2.zero;
                s.Hotbar = 0;
                scripted = s;
            }
            if (input.Hotbar > 0) hud.Slot = input.Hotbar;
            view.Step(input, dt, modal);

            hud.Refresh(dt, view.Current, view.Wading, modal);
            pops.Update(dt);
            modals.Update(dt);

            autosave += dt;
            if (autosave >= Balance.AutosaveSeconds) { autosave = 0; Save(); }
        }

        void HandleKeys(bool modal)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (modal) { if (modals.OpenName != "intro") modals.Close(); }
                else modals.OpenSettings();
            }
            if (Input.GetKeyDown(KeyCode.J) && !modal) { sfx.Play("ui"); modals.OpenJournal(); }
            if (devMode && !modal)
            {
                if (Input.GetKeyDown(KeyCode.F5)) sim.DebugAddCash(Math.Max(10, sim.S.cash * 9));
            }
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
            if (Input.GetKeyDown(KeyCode.Alpha3)) i.Hotbar = 3;
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

        IEnumerator GoToBoard()
        {
            var b = view.Board.Root;
            yield return WalkTo(b.position + b.forward * 1.7f, 0.35f);
            yield return LookAtSmooth(b.position + Vector3.up * 1.25f);
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
            StartCoroutine(Watchdog(Time.realtimeSinceStartup + 300));
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
                      $"carryTier={sim.CarryTier} objective={sim.S.objective} achievements={sim.AchievementCount}");
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

            // a bigger container carries more per trip
            sim.DebugSetTech("carry_cup", 1);
            Check(sim.CarryCapacity == 5, $"paper cup holds 5 (got {sim.CarryCapacity})");
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
            Check(Math.Abs(back.S.cash - sim.S.cash) < 1e-9 && back.Loose.Count == sim.Loose.Count && back.CarryTier == sim.CarryTier,
                  $"save → load keeps cash, {back.Loose.Count} loose items and the carry tier");
            yield return Shot("ui_after_test");
            Debug.Log($"[UITEST] done: {uiPass} passed, {uiFail} failed");
            Save();
            Application.Quit();
        }
    }
}
