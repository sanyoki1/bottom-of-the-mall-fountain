// Entry point. Builds the camera, simulation, 3D view, UI and audio; routes input; turns sim
// events into toasts, banners and sounds; autosaves; and runs the screenshot tour (-autotour).
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
        ShopPanel shop;
        Popups pops;
        Modals modals;
        Canvas uiCanvas, popCanvas, modalCanvas;
        RectTransform tooltipRt;
        Text tooltipText;
        UIKit.Btn shopToggle;
        float autosave, holdTimer, holdRepeat;
        bool holding, devMode, touring;
        Vector3 lastWishPos, lastGoldenPos;
        readonly Dictionary<string, float> whackCooldown = new Dictionary<string, float>();
        OfflineReport pendingOffline;
        bool pendingIntro, pendingFirstIntro;
        string shotDir;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (FindAnyObjectByType<GameRoot>() == null) new GameObject("Wish Extractor").AddComponent<GameRoot>();
        }

        void Awake()
        {
            var args = Environment.GetCommandLineArgs();
            devMode = Application.isEditor || Array.IndexOf(args, "-dev") >= 0;
            touring = Array.IndexOf(args, "-autotour") >= 0;
            int si = Array.IndexOf(args, "-shots");
            shotDir = si >= 0 && si + 1 < args.Length ? args[si + 1] : Path.Combine(Application.dataPath, "..", "Screenshots");
            if (touring) SaveSystem.FileName = "wishextractor_tour.json";
            int sf = Array.IndexOf(args, "-savefile");
            if (sf >= 0 && sf + 1 < args.Length) SaveSystem.FileName = args[sf + 1];
            if (touring || Array.IndexOf(args, "-fresh") >= 0) SaveSystem.Erase();

            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 1;
            QualitySettings.shadows = ShadowQuality.All;
            QualitySettings.shadowResolution = ShadowResolution.High;
            QualitySettings.shadowDistance = 90;
            QualitySettings.shadowCascades = 2;
            QualitySettings.pixelLightCount = 1;
            QualitySettings.antiAliasing = 4;

            var camGo = new GameObject("Main Camera") { tag = "MainCamera" };
            cam = camGo.AddComponent<Camera>();
            cam.fieldOfView = 38;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 400;
            cam.allowHDR = true;
            cam.allowMSAA = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            camGo.AddComponent<AudioListener>();
            bloom = camGo.AddComponent<BloomFX>();
            camGo.AddComponent<CameraRig>();

            var save = SaveSystem.Load();
            bool fresh = save == null;
            int awayArg = Array.IndexOf(args, "-pretendaway");
            if (save != null && awayArg >= 0 && awayArg + 1 < args.Length && double.TryParse(args[awayArg + 1], out double awaySec))
                save.lastSaveUnix -= (long)awaySec;
            sim = new Sim(save ?? new SaveData());
            if (fresh) sim.StartRun();
            Fmt.Notation = sim.S.notation;
            if (!fresh && sim.S.lastSaveUnix > 0)
            {
                double away = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - sim.S.lastSaveUnix;
                if (away > 60)
                {
                    var rep = sim.SimulateOffline(away);
                    if (rep.Cash > 0 || rep.Dug > 0) pendingOffline = rep;
                }
            }

            var world = new GameObject("World");
            view = world.AddComponent<GameView>();
            view.Init(sim, cam);
            view.FloatText += (p, t, c, s) => pops?.Float(p, t, c, s);

            UIKit.EnsureEventSystem();
            uiCanvas = UIKit.CreateCanvas("UI", 10);
            popCanvas = UIKit.CreateCanvas("Popups", 20);
            modalCanvas = UIKit.CreateCanvas("Modals", 30);
            hud = new HUD();
            hud.Build(uiCanvas, sim);
            hud.OnSell = Sell;
            hud.OnDumpRaw = () => { if (sim.DumpHopper() > 0) sfx.Play("coin"); };
            hud.OnDumpWashed = () => { if (sim.DumpTray() > 0) sfx.Play("coin"); };
            hud.OnContract = () => modals.OpenContract();
            hud.OnToggleAutoSell = () => { sim.S.autoSellOn = !sim.S.autoSellOn; sfx.Play("ui"); };
            shop = new ShopPanel();
            shop.Build(uiCanvas, sim);
            shop.OnBought = id => { if (id == null) sfx.Play("denied", 0.6f); };
            shop.OnPrestige = () => modals.OpenContract();
            BuildTopButtons();
            BuildTooltip();
            pops = new Popups();
            pops.Build(popCanvas, cam);
            modals = new Modals();
            modals.Build(modalCanvas, sim);
            modals.OnVolumes = (m, s) => sfx.SetVolumes(m, s);
            modals.OnSettingsChanged = ApplySettings;
            modals.OnResetSave = ResetSave;
            modals.OnQuit = () => { Save(); Application.Quit(); };
            modals.OnSign = SignContract;

            sfx = gameObject.AddComponent<AudioHub>();
            sfx.Init(sim.S.musicVol, sim.S.sfxVol);
            sfx.PlayMusicFor(sim.Mall, sim.Remodel);
            HookEvents();
            ApplySettings();

            if (fresh || (sim.S.clicks == 0 && sim.S.mallIndex == 0)) pendingFirstIntro = true;
        }

        void Start()
        {
            if (touring) { StartCoroutine(Tour()); return; }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-loadtest") >= 0)
            {
                StartCoroutine(LoadTest());
                return;
            }
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-uitest") >= 0)
            {
                modals.OpenIntro(true);
                StartCoroutine(UiTest());
                StartCoroutine(Watchdog(Time.realtimeSinceStartup + 120));
                return;
            }
            if (pendingFirstIntro) modals.OpenIntro(true);
            else if (pendingOffline != null) modals.OpenOffline(pendingOffline);
        }

        void BuildTopButtons()
        {
            var bar = UIKit.Rect(uiCanvas.transform, "TopButtons").Place(new Vector2(1, 1), new Vector2(-20, -20), new Vector2(500, 50));
            var journal = UIKit.Button(bar, "Journal", "Journal", Pal.GlassStrong, Pal.Ink, 16, () => { sfx.Play("ui"); modals.OpenJournal(); });
            journal.Rt.TL(0, 0, 140, 48);
            var settings = UIKit.Button(bar, "Settings", "Settings", Pal.GlassStrong, Pal.Ink, 16, () => { sfx.Play("ui"); modals.OpenSettings(); });
            settings.Rt.TL(150, 0, 140, 48);
            shopToggle = UIKit.Button(bar, "ShopToggle", "Hide shop", Pal.Accent, Color.white, 16, ToggleShop);
            shopToggle.Rt.TL(300, 0, 200, 48);
            foreach (var b in new[] { journal, settings })
            {
                var sh = b.Rt.gameObject.AddComponent<Shadow>();
                sh.effectColor = new Color(0, 0, 0, 0.12f);
                sh.effectDistance = new Vector2(0, -3);
            }
        }

        void BuildTooltip()
        {
            tooltipRt = UIKit.Card(uiCanvas.transform, "Tooltip", new Color(0.1f, 0.1f, 0.12f, 0.88f), 14, false, false);
            tooltipRt.anchorMin = tooltipRt.anchorMax = new Vector2(0, 0);
            tooltipRt.pivot = new Vector2(0, 0);
            tooltipRt.sizeDelta = new Vector2(380, 40);
            tooltipText = UIKit.Label(tooltipRt, "T", "", 15, Color.white, TextAnchor.MiddleLeft, UIKit.Semibold);
            tooltipText.rectTransform.Stretch(14, 0, 14, 0);
            tooltipRt.gameObject.SetActive(false);
        }

        void ToggleShop()
        {
            sfx.Play("ui");
            shop.SetVisible(!shop.Visible);
            shopToggle.Label.text = shop.Visible ? "Hide shop" : "Show shop";
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
            if (view.Rig != null) view.Rig.enabled = true;
        }

        // ───────────────────────────── events → feedback ─────────────────────────────

        void HookEvents()
        {
            sim.OnWishSpawned += w => sfx.Play("wish_spawn", 0.5f, 0.1f, 0.3f);
            sim.OnWishCaught += (w, v, first) =>
            {
                sfx.Play("wish_catch", 0.9f, 0.04f, 0.05f, 1f + (int)w.Def.Rarity * 0.03f);
                pops.WishQuote(lastWishPos, w.Def, v, first);
                if (w.Def.Rarity == Rarity.Legendary) pops.Banner("Legendary wish!", "“" + w.Def.Text + "”", "+" + Fmt.Money(v), Pal.Gold, 4.5f);
            };
            sim.OnWishCompressed += (w, v) => sfx.Play("compress", 0.35f, 0.1f, 0.4f);
            sim.OnWishEscaped += w => sfx.Play("wish_escape", 0.3f, 0.1f, 0.4f);
            sim.OnRelicFound += (r, v, first) =>
            {
                sfx.Play("relic", 0.7f, 0.02f, 0.3f);
                pops.Toast((first ? "New relic! " : "Rare find: ") + r.Name, $"{r.Desc}  ·  +{Fmt.Money(v)}", Pal.Rarity[(int)r.Rarity], "◆", first ? 5f : 3.6f);
                if (r.Rarity == Rarity.Legendary) pops.Banner("Legendary find!", r.Name, r.Desc, Pal.Gold, 4.5f);
            };
            sim.OnRelicSetComplete += m => { sfx.Play("achievement"); pops.Banner("Collection complete", m.Name + " relics", $"Every relic found: +{Fmt.Num(Balance.RelicSetBonus * 100)}% value forever", Pal.Purple, 4.5f); };
            sim.OnStratumReached += s =>
            {
                sfx.Play("stratum", 0.9f, 0f, 1f);
                var st = sim.Mall.Strata[s];
                pops.Banner($"New layer · {Fmt.Feet(st.StartFrac * sim.Mall.DepthFeet)} down", st.Name, st.Flavor, MeshKit.Hex(st.Color), 4.8f);
                if (view.Rig != null && sim.S.screenShake) view.Rig.Shake(0.4f);
            };
            sim.OnMallCleared += () =>
            {
                sfx.Play("cleared", 1f, 0f, 1f);
                pops.Banner("Bare concrete!", sim.Mall.TreasureName, sim.Mall.TreasureDesc, Pal.Gold, 6f);
                StartCoroutine(AfterClear());
            };
            sim.OnSold += (amt, src) =>
            {
                if (src == 3) sfx.Play("register", 0.25f, 0.05f, 1.2f);
                else sfx.Play("register", 0.8f, 0.03f, 0.1f);
            };
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
            sim.OnGoldenSpawned += g => sfx.Play("golden_spawn", 0.7f, 0.05f, 0.3f);
            sim.OnGoldenClaimed += (g, title, detail) =>
            {
                sfx.Play("golden", 1f, 0.02f, 0.1f);
                pops.Toast(title, detail, Pal.Gold, "¢", 4f);
                pops.Float(lastGoldenPos + Vector3.up, title, new Color(1f, 0.85f, 0.3f), 1.5f);
            };
            sim.OnRatSpawned += r => sfx.Play("squeak", 0.5f, 0.1f, 1f);
            sim.OnRatCaught += r => { sfx.Play("squeak", 1f, 0.05f, 0.1f, 1.2f); pops.Toast("Mall rat caught!", "It dropped something shiny…", Pal.Ink2, "✋", 3f); };
            sim.OnEventChanged += on =>
            {
                if (!on) return;
                sfx.Play("event", 0.8f, 0, 1f);
                pops.Banner("Mall event", sim.Mall.Event.Name, sim.Mall.Event.Desc, Pal.Pink, 4f);
            };
            sim.OnPurchase += id =>
            {
                bool big = Content.MachineIndex.ContainsKey(id) && Sim.NextMilestone(sim.MachineCount(id) - 1) == sim.MachineCount(id);
                sfx.Play(big ? "buy_big" : "buy", 0.7f, 0.05f, 0.04f);
                if (big) pops.Toast($"{Content.NameOf(id)} milestone!", $"{sim.MachineCount(id)} owned: output ×2", Pal.Accent, "↑", 3f);
            };
            sim.OnPrestige += () =>
            {
                pops.ClearBanners();
                sfx.PlayMusicFor(sim.Mall, sim.Remodel);
                Save();
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
            if (!modals.IsOpen && sim.MallCleared) modals.OpenContract();
        }

        void SignContract()
        {
            if (!sim.MallCleared) return;
            sim.Prestige();
            shop.SetTab(ShopPanel.Tab.HeadOffice);
            modals.OpenIntro(false);
        }

        void ResetSave()
        {
            SaveSystem.Erase();
            sim.Load(new SaveData());
            sim.StartRun();
            view.RebuildMall();
            sfx.PlayMusicFor(sim.Mall, sim.Remodel);
            modals.OpenIntro(true);
        }

        void Save()
        {
            if (sim == null) return;
            SaveSystem.Save(sim.Snapshot());
        }

        void OnApplicationQuit() => Save();
        void OnApplicationPause(bool paused) { if (paused) Save(); }
        void OnApplicationFocus(bool focus) { if (!focus) Save(); }

        // ───────────────────────────── actions ─────────────────────────────

        void Sell()
        {
            if (sim.S.pocketValue <= 0) { sfx.Play("denied", 0.6f); return; }
            sim.SellPocket();
        }

        void DoDig(Vector3 point)
        {
            if (sim.MallCleared)
            {
                pops.Float(point + Vector3.up, "Bare concrete! Sign the next contract.", Pal.Gold, 0.9f);
                return;
            }
            bool loose = sim.CurStratum.Loose;
            double unitValue = sim.CurrentEV * sim.ValueNow;
            double amount = sim.Click();
            if (amount <= 0) return;
            view.ShowDig(point, amount, loose, amount * unitValue);
            sfx.Play("dig", 0.75f, 0.08f, 0.05f);
            if (loose) sfx.Play("coin", 0.45f, 0.1f, 0.06f);
        }

        void Whack(string id)
        {
            if (!Content.MachineIndex.TryGetValue(id, out int mi)) return;
            float now = Time.time;
            if (whackCooldown.TryGetValue(id, out var until) && now < until) return;
            whackCooldown[id] = now + 0.4f;
            var m = Content.Machines[mi];
            if (!m.IsCompressor && !m.IsMega) sim.Whack(m.Stage, 0.15);
            view.WhackMachine(id);
            sfx.Play("whack", 0.55f, 0.1f, 0.05f);
        }

        void HandlePick(GameView.ClickResult r)
        {
            switch (r.Kind)
            {
                case GameView.ClickKind.Wish: lastWishPos = r.Point; sim.CatchWish(r.Uid); break;
                case GameView.ClickKind.Golden: lastGoldenPos = r.Point; sim.ClaimGolden(r.Uid); break;
                case GameView.ClickKind.Rat: sim.CatchRat(); break;
                case GameView.ClickKind.Vending: Sell(); break;
                case GameView.ClickKind.Machine: Whack(r.Id); break;
                case GameView.ClickKind.Crust: DoDig(r.Point); break;
            }
        }

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

            bool overUI = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            bool modal = modals.IsOpen;
            if (!touring) HandleInput(dt, overUI, modal);
            view.Rig.HandleInput(dt, overUI || modal);
            view.Rig.ScreenShiftX = shop.Visible ? 0.045f : -0.02f;
            if (view.Rig != null && !sim.S.screenShake) view.Rig.Shake(0);

            hud.Refresh(dt);
            shop.Refresh(dt, sim.CurrentObjective?.Focus);
            pops.Update(dt);
            modals.Update(dt);
            UpdateTooltip(overUI || modal);

            autosave += dt;
            if (autosave >= Balance.AutosaveSeconds) { autosave = 0; Save(); }
        }

        void HandleInput(float dt, bool overUI, bool modal)
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (modal) { if (modals.OpenName != "intro") modals.Close(); }
                else modals.OpenSettings();
            }
            if (modal) { holding = false; return; }
            if (Input.GetMouseButtonDown(0) && !overUI)
            {
                var r = view.Pick(Input.mousePosition);
                HandlePick(r);
                holding = r.Kind == GameView.ClickKind.Crust;
                holdTimer = 0;
                holdRepeat = 0;
            }
            if (Input.GetMouseButton(0) && holding && !overUI)
            {
                holdTimer += dt;
                if (holdTimer > 0.3f)
                {
                    holdRepeat += dt;
                    const float interval = 1f / 6f;
                    while (holdRepeat >= interval)
                    {
                        holdRepeat -= interval;
                        var r = view.Pick(Input.mousePosition);
                        if (r.Kind == GameView.ClickKind.Crust) DoDig(r.Point);
                    }
                }
            }
            if (Input.GetMouseButtonUp(0)) holding = false;

            if (Input.GetKeyDown(KeyCode.Space)) Sell();
            if (Input.GetKeyDown(KeyCode.Tab)) ToggleShop();
            if (Input.GetKeyDown(KeyCode.J)) { sfx.Play("ui"); modals.OpenJournal(); }
            if (Input.GetKeyDown(KeyCode.B)) { shop.CycleBuyAmount(); sfx.Play("ui"); }
            if (Input.GetKeyDown(KeyCode.Alpha1)) { EnsureShop(); shop.SetTab(ShopPanel.Tab.Tools); }
            if (Input.GetKeyDown(KeyCode.Alpha2)) { EnsureShop(); shop.SetTab(ShopPanel.Tab.Machines); }
            if (Input.GetKeyDown(KeyCode.Alpha3)) { EnsureShop(); shop.SetTab(ShopPanel.Tab.Upgrades); }
            if (Input.GetKeyDown(KeyCode.Alpha4)) { EnsureShop(); shop.SetTab(ShopPanel.Tab.HeadOffice); }

            if (devMode)
            {
                if (Input.GetKeyDown(KeyCode.F5)) sim.DebugAddCash(Math.Max(1000 * sim.CostScale, sim.S.cash * 9));
                if (Input.GetKeyDown(KeyCode.F6)) sim.DebugFinishMall();
                if (Input.GetKeyDown(KeyCode.F7)) sim.DebugSetDug(sim.DepthFrac + 0.1);
                if (Input.GetKeyDown(KeyCode.F8)) sim.SpawnGolden();
                if (Input.GetKeyDown(KeyCode.F9)) sim.WishStorm(6);
            }
        }

        void EnsureShop() { if (!shop.Visible) ToggleShop(); }

        void UpdateTooltip(bool blocked)
        {
            if (blocked || touring) { tooltipRt.gameObject.SetActive(false); return; }
            var r = view.Pick(Input.mousePosition);
            string text = null;
            switch (r.Kind)
            {
                case GameView.ClickKind.Machine:
                    if (Content.MachineIndex.TryGetValue(r.Id ?? "", out int mi))
                    {
                        var m = Content.Machines[mi];
                        int n = sim.MachineCount(mi);
                        text = m.IsCompressor || m.IsMega ? $"{m.Name}  ·  level {n}" : $"{m.Name} ×{n}  ·  {Fmt.Rate(sim.MachineUnitRate[mi] * n)}  ·  click to whack";
                    }
                    break;
                case GameView.ClickKind.Vending:
                    text = sim.S.pocketValue > 0 ? $"Greasy Vending Machine  ·  click to sell {Fmt.Money(sim.PocketWorth)}" : "Greasy Vending Machine  ·  your pocket is empty";
                    break;
                case GameView.ClickKind.Wish:
                    var w = sim.Wishes.Find(x => x.Uid == r.Uid);
                    if (w != null) text = $"{RarityColors.Names[(int)w.Def.Rarity]} True Wish  ·  {Fmt.Money(w.Value)}  ·  click to catch!";
                    break;
                case GameView.ClickKind.Golden: text = "Golden Penny  ·  click it!"; break;
                case GameView.ClickKind.Rat: text = "A mall rat with stolen loot  ·  catch it!"; break;
                case GameView.ClickKind.Crust:
                    if (sim.S.clicks < 30 && !sim.MallCleared) text = $"Click to dig  ·  {Fmt.Num(sim.ClickPowerNow)} per click";
                    break;
            }
            if (text == null) { tooltipRt.gameObject.SetActive(false); return; }
            tooltipRt.gameObject.SetActive(true);
            tooltipText.text = text;
            tooltipRt.sizeDelta = new Vector2(tooltipText.preferredWidth + 30, 40);
            RectTransformUtility.ScreenPointToLocalPointInRectangle((RectTransform)uiCanvas.transform, Input.mousePosition, null, out var local);
            var size = ((RectTransform)uiCanvas.transform).rect.size;
            tooltipRt.anchoredPosition = new Vector2(Mathf.Min(local.x + size.x / 2 + 18, size.x - tooltipRt.sizeDelta.x - 10), local.y + size.y / 2 + 18);
        }

        // ───────────────────────────── screenshot tour (-autotour) ─────────────────────────────

        IEnumerator Shot(string name)
        {
            Directory.CreateDirectory(shotDir);
            yield return new WaitForSeconds(0.2f);
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + ".png"));
            yield return null;
            yield return null;
        }

        IEnumerator ClickCrust(int times, float interval)
        {
            for (int i = 0; i < times; i++)
            {
                float a = UnityEngine.Random.value * Mathf.PI * 2, r = UnityEngine.Random.Range(2.5f, 6f);
                var p = view.Fountain.SurfacePoint(Mathf.Cos(a) * r, Mathf.Sin(a) * r - 1);
                DoDig(p);
                yield return new WaitForSeconds(interval);
            }
        }

        IEnumerator Tour()
        {
            float timeout = Time.realtimeSinceStartup + 240;
            StartCoroutine(Watchdog(timeout));
            yield return new WaitForSeconds(2.5f);
            modals.OpenIntro(true);
            yield return Shot("00_intro");
            modals.Close();
            yield return Shot("01_start");
            yield return ClickCrust(40, 0.07f);
            yield return Shot("02_first_clicks");

            // Phase 2: syrup seal, first washers and sorters, a wish in the air
            sim.DebugAddCash(4000);
            sim.BuyNextTool(); sim.BuyNextTool();
            sim.DebugSetDug(0.16);
            sim.DebugSetMachine("pogo", 8);
            sim.DebugSetMachine("tumbler", 6);
            sim.DebugSetMachine("pigeons", 3);
            sim.DebugSetMachine("walkers", 4);
            view.RebuildMall();
            yield return new WaitForSeconds(1.5f);
            // a low, close camera shows off the crust and the little workers
            var rig = view.Rig;
            rig.enabled = false;
            cam.transform.SetPositionAndRotation(new Vector3(2, 4.5f, -14f), Quaternion.Euler(24, -4, 0));
            yield return ClickCrust(6, 0.12f);
            yield return Shot("02c_low_angle");
            rig.enabled = true;
            sim.WishStorm(4);
            sim.SpawnGolden();
            yield return ClickCrust(15, 0.08f);
            yield return new WaitForSeconds(1.2f);
            yield return Shot("03_phase2");

            // Phase 3: the industrial mega-factory
            sim.DebugAddCash(5e7);
            sim.DebugSetDug(0.66);
            foreach (var id in new[] { "dishwasher", "coinstar" }) sim.DebugSetMachine(id, 4);
            foreach (var id in new[] { "jackhammer", "acid", "lasers" }) sim.DebugSetMachine(id, 3);
            sim.DebugSetMachine("compressor", 2);
            sim.DebugSetMachine("pigeons", 12);
            view.RebuildMall();
            yield return new WaitForSeconds(2.5f);
            sim.WishStorm(3);
            yield return new WaitForSeconds(1.0f);
            yield return Shot("04_phase3");
            float fpsT = 0;
            int frames = 0;
            while (fpsT < 3f) { fpsT += Time.unscaledDeltaTime; frames++; yield return null; }
            Debug.Log($"[TOUR] phase-3 average FPS {frames / fpsT:0.0}");
            sim.SpawnGolden();
            sim.DebugSpawnRat();
            sim.WishStorm(3);
            yield return new WaitForSeconds(1.4f);
            yield return Shot("04b_clickables");
            // catch a wish and claim the golden penny through the real picking path
            if (sim.Wishes.Count > 0)
            {
                var w = sim.Wishes[0];
                HandlePick(view.Pick(cam.WorldToScreenPoint(view.WishPosition(w.Uid))));
            }
            if (sim.Goldens.Count > 0)
            {
                var g = sim.Goldens[0];
                HandlePick(view.Pick(cam.WorldToScreenPoint(view.GoldenPosition(g.Uid))));
            }
            yield return new WaitForSeconds(0.7f);
            yield return Shot("04c_caught");
            shop.SetTab(ShopPanel.Tab.Upgrades);
            yield return new WaitForSeconds(0.5f);
            yield return Shot("05_upgrades");
            shop.SetTab(ShopPanel.Tab.Machines);
            modals.OpenJournal(0);
            yield return Shot("06_journal");
            modals.OpenJournal(1);
            yield return Shot("07_relics");
            modals.OpenSettings();
            yield return Shot("08_settings");
            modals.Close();

            // Every other mall, mid-dig with a few machines
            string[] ids = { "pogo", "walkers", "tumbler", "dishwasher", "pigeons", "coinstar", "jackhammer", "acid", "lasers", "claw", "carwash", "prizebots", "borer", "jacuzzi", "sieve", "carousel", "slots", "wishengine", "compressor" };
            for (int m = 1; m < Content.Malls.Length; m++)
            {
                sim.DebugJumpToMall(m);
                sim.DebugSetDug(0.45);
                foreach (var id in ids)
                    if (sim.MachineUnlocked(Content.MachineIndex[id]) || Content.Machines[Content.MachineIndex[id]].UnlockMall <= m)
                        sim.DebugSetMachine(id, Content.Machines[Content.MachineIndex[id]].IsMega ? 2 : 3);
                view.RebuildMall();
                modals.OpenIntro(false);
                yield return new WaitForSeconds(1.0f);
                if (m == 1) yield return Shot("09_mall2_intro");
                modals.Close();
                yield return new WaitForSeconds(1.8f);
                sim.WishStorm(2);
                yield return ClickCrust(6, 0.1f);
                yield return new WaitForSeconds(0.8f);
                yield return Shot($"1{m}_mall{m + 1}");
            }
            sim.DebugFinishMall();
            yield return new WaitForSeconds(3f);
            yield return Shot("20_cleared");
            modals.OpenContract();
            yield return Shot("21_contract");
            Application.Quit();
        }

        IEnumerator Watchdog(float deadline)
        {
            while (Time.realtimeSinceStartup < deadline) yield return null;
            Application.Quit();
        }

        /// <summary>-loadtest: report what came back from the save (and any offline earnings), then quit.</summary>
        IEnumerator LoadTest()
        {
            Debug.Log($"[LOADTEST] cash={Fmt.Money(sim.S.cash)} tool={Content.Tools[sim.S.tool].Name} pogo={sim.MachineCount("pogo")} " +
                      $"clicks={sim.S.clicks} dug={Fmt.Num(sim.S.totalDug)} depth={Fmt.Feet(sim.DepthFeet)} achievements={sim.AchievementCount} objective={sim.S.objective}");
            if (pendingOffline != null)
            {
                Debug.Log($"[LOADTEST] offline: {Fmt.Time(pendingOffline.Seconds)} cash+{Fmt.Money(pendingOffline.Cash)} dug+{Fmt.Num(pendingOffline.Dug)}");
                modals.OpenOffline(pendingOffline);
            }
            else Debug.Log("[LOADTEST] no offline report");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("ui_offline");
            Application.Quit();
        }

        // ───────────────────────────── UI / input self-test (-uitest) ─────────────────────────────
        // Drives the real uGUI event pipeline (EventSystem raycast at the button's screen position,
        // then a pointer click) and the real world-picking path, and logs PASS/FAIL per step.

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
            yield return new WaitForSeconds(2.5f);
            Check(modals.IsOpen && modals.OpenName == "intro", "intro shown on a fresh save");
            Check(ClickSelectable(FindButton("Go")), "click 'Start digging'");
            yield return null;
            Check(!modals.IsOpen, "intro closed");

            var spot = cam.WorldToScreenPoint(view.Fountain.SurfacePoint(1.5f, -2.5f));
            Check(!PointerOverUI(spot), "fountain centre is not covered by UI");
            double dug0 = sim.S.totalDug;
            for (int i = 0; i < 45; i++)
            {
                var r = view.Pick((Vector2)spot + UnityEngine.Random.insideUnitCircle * 40f);
                if (i == 0) Check(r.Kind == GameView.ClickKind.Crust, $"picking the fountain hits the crust (got {r.Kind})");
                HandlePick(r);
                yield return new WaitForSeconds(0.06f);
            }
            Check(sim.S.totalDug > dug0, $"clicking digs ({Fmt.Num(sim.S.totalDug - dug0)} items)");
            Check(sim.S.pocketValue > 0, "loose layer loot lands in the pocket");

            double cash0 = sim.S.cash;
            Check(ClickSelectable(FindButton("Sell")), "click SELL in the pipeline card");
            yield return null;
            Check(sim.S.cash > cash0, $"selling pays out ({Fmt.Money(sim.S.cash - cash0)})");

            Check(ClickSelectable(FindButton("Tools")), "open the Tools tab");
            yield return new WaitForSeconds(0.5f);
            sim.DebugAddCash(5);
            yield return new WaitForSeconds(0.4f);
            int tool0 = sim.S.tool;
            Check(ClickSelectable(FindButton("Buy", b => b.interactable)), "click a Buy button on a tool card");
            yield return null;
            Check(sim.S.tool == tool0 + 1, "the Butter Knife was bought");

            Check(ClickSelectable(FindButton("Machines")), "open the Machines tab");
            sim.DebugAddCash(10);
            yield return new WaitForSeconds(0.6f);
            int pogo0 = sim.MachineCount("pogo");
            Check(ClickSelectable(FindButton("Buy", b => b.interactable)), "click Buy on the pogo stick card");
            yield return null;
            Check(sim.MachineCount("pogo") == pogo0 + 1, "a pogo stick was bought");

            // world clicks on the vending machine (sell) and a machine (whack)
            for (int i = 0; i < 20; i++) { HandlePick(view.Pick((Vector2)spot)); yield return new WaitForSeconds(0.03f); }
            var vend = cam.WorldToScreenPoint(view.VendingPosition + Vector3.up * 1.5f);
            var vr = view.Pick(vend);
            Check(vr.Kind == GameView.ClickKind.Vending, $"picking the vending machine hits it (got {vr.Kind})");
            double cash1 = sim.S.cash;
            HandlePick(vr);
            Check(sim.S.cash > cash1, "clicking the vending machine sells");

            Check(ClickSelectable(FindButton("Journal")), "open the Journal");
            yield return new WaitForSeconds(0.3f);
            Check(modals.IsOpen && modals.OpenName == "journal", "journal is open");
            Check(ClickSelectable(FindButton("Relic Museum")), "switch to the Relic Museum tab");
            yield return new WaitForSeconds(0.2f);
            yield return Shot("ui_journal_relics");
            Check(ClickSelectable(FindButton("Close")), "close the journal with X");
            yield return null;
            Check(!modals.IsOpen, "journal closed");

            Check(ClickSelectable(FindButton("Settings")), "open Settings");
            yield return new WaitForSeconds(0.3f);
            Check(ClickSelectable(FindButton("1.23e6")), "switch number format");
            Check(sim.S.notation == 1, "notation setting changed");
            ClickSelectable(FindButton("1.23M"));
            Check(ClickSelectable(FindButton("Close")), "close settings");
            yield return null;

            Check(ClickSelectable(FindButton("ShopToggle")), "hide the shop");
            yield return null;
            Check(!shop.Visible, "shop hidden");
            ClickSelectable(FindButton("ShopToggle"));
            yield return null;
            Check(shop.Visible, "shop shown again");

            yield return Shot("ui_after_test");

            // clear the mall, sign the next contract, spend Lucky Pennies at Head Office
            sim.DebugFinishMall();
            Check(sim.MallCleared, "mall cleared");
            yield return new WaitForSeconds(4.5f);
            Check(modals.IsOpen && modals.OpenName == "contract", "contract dialog opens after bare concrete");
            yield return Shot("ui_contract");
            Check(ClickSelectable(FindButton("Sign")), "click 'Sign the contract'");
            yield return new WaitForSeconds(0.6f);
            Check(sim.S.mallIndex == 1 && !sim.MallCleared, $"moved to the next mall ({sim.Mall.Name})");
            Check(sim.S.luckyPennies > 0, $"earned Lucky Pennies ({sim.S.luckyPennies})");
            Check(modals.IsOpen && modals.OpenName == "intro", "new mall intro shown");
            Check(ClickSelectable(FindButton("Go")), "start digging in the new mall");
            yield return new WaitForSeconds(0.6f);
            Check(shop.Current == ShopPanel.Tab.HeadOffice, "shop opened on Head Office");
            int ho0 = sim.HOTotalLevels;
            Check(ClickSelectable(FindButton("Buy", b => b.interactable)), "click Buy on a Head Office perk");
            yield return null;
            Check(sim.HOTotalLevels == ho0 + 1, "Head Office perk bought");
            yield return new WaitForSeconds(1.5f);
            yield return Shot("ui_mall2");
            Debug.Log($"[UITEST] done: {uiPass} passed, {uiFail} failed");
            Save();
            Application.Quit();
        }
    }
}
