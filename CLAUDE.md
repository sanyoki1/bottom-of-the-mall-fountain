# CLAUDE.md — Wish Extractor (Unity, first person)

Read `HANDOFF.md` first (state, verified results, open items), then `DESIGN.md` if you need the design
(`PIVOT_FPS.md` is the plan the first-person pivot followed).
Ask Nico before any git commit. Cloud sessions run on Linux without Unity: they work on their `claude/`
branch, and the Unity build, tour and uitest can only run on Nico's Windows PC.

## Layout

| Path | What |
|---|---|
| `Assets/Scripts/Core/` | Pure C# rules (no UnityEngine): `Sim.cs` (loose items, carrying, deposits, tech tree, multipliers), `SimCrowd.cs` (shoppers, tosses, wishes), `SimFactory.cs` (build grid, power, belts, intakes, hoppers), `SimCrust.cs` (digging, processing, relics, prestige, mall events), `SimHazards.cs` (Officer Doug, Chad), `Content*.cs` (all content data), `Balance.cs` (tunables), `Layout.cs` (hall positions), `SaveData.cs`, `Fmt.cs`. Compiled by Unity AND by `Tools/BalanceSim`. |
| `Assets/Scripts/View/` | `GameView` (orchestrator, crosshair targeting, `Use`), `FirstPerson` (controller, `FPInput`), `Hands`, `ItemRenderer` (instanced loose items), `WorldBuilder` (the hall), `FountainView` (basin, crust heightfield, water, colliders, ramp), `Kiosk`, `Terminal`, `Board`, `Crowd`, `Wishes`, `Hazards`, `Decor`, `FactoryView`, `BuildMode`, `Actors`, `Loot`, `FX`, `BloomFX`, `MeshKit`, `TexKit`, `Mats`, `ViewCommon`. |
| `Assets/Scripts/UI/` | uGUI built from code: `UIKit`, `HUD`, `Popups`, `Modals`, `TerminalPanel` (MAINT-OS 95), `BuildMenu`. |
| `Assets/Scripts/Audio/` | `Synth` (procedural SFX + music), `AudioHub`. |
| `Assets/Scripts/Game/` | `GameRoot` (entry point, keyboard/mouse → `FPInput`, events → feedback, `-autotour`/`-uitest`/`-loadtest`), `SaveSystem`. |
| `Assets/Editor/ProjectBuilder.cs` | Scene creation, player settings, Windows build (menu + `BuildWindowsCI`). |
| `Assets/Resources/Shaders/` | `WE/Lit`, `WE/Crust`, `WE/Water`, `WE/Glow`, `WE/Ghost`, `WE/Soft`, `WE/Scroll`, `WE/Text`, `Hidden/WE/Bloom`. |
| `Tools/BalanceSim/` | .NET 10 console: `Bot.cs` plays the real Core; `Program.cs` runs it (`fit --apply` writes the crust sizes). |

## Build / verify (Unity 6000.4.2f1 at `C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe`)

1. Compile + build: `Unity.exe -batchmode -nographics -projectPath . -executeMethod WishExtractor.EditorTools.ProjectBuilder.BuildWindowsCI -logFile Logs/build.log` (look for `error CS` and `[WishExtractor] build Succeeded`).
2. Visual check: `Builds/Windows/WishExtractor.exe -autotour -shots Screenshots -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile Logs/player_tour.log`, then read the 51 PNGs and the `[TOUR]` log lines.
3. Input check: `... -uitest -savefile wishextractor_uitest.json -fresh ...` → expect `[UITEST] done: 96 passed, 0 failed` in the log.
4. Save check: `... -loadtest -savefile wishextractor_uitest.json ...` → read the `[LOADTEST]` line.
5. Balance: `dotnet run -c Release --project Tools/BalanceSim -- 40 1234 engaged` and `-- 60 1234 casual`.

Players and editor runs block the shell until they exit; the tour and uitest quit on their own (watchdogs
at 660 s and 420 s). Never kill processes; close a stray player window gracefully (WM_CLOSE).

## Rules that bit before (keep them)

- **Vertex alpha is an emission mask** in `WE/Lit`. `new Color(r, g, b)` has alpha 1 and will glow like a
  lamp; use `MeshKit.Hex(0xRRGGBB)` (alpha 0) or pass alpha 0 explicitly.
- Additive `WE/Glow` multiplies by vertex alpha, so it can't draw matte (alpha 0) meshes; build ghosts use `WE/Ghost`.
- A lit mesh at exactly zero scale makes NaN pixels that bloom smears into black squares; scale to 0.01
  and deactivate instead.
- World `TextMesh` colour must have alpha 1 (`Mats.NewText` forces it).
- **Nothing may cap the basin at floor level.** The hall's grout base is an annulus for this reason; a full
  slab hid the crust as soon as it sank below y = 0.
- The player is on the Ignore Raycast layer; wishes and Chad are triggers on layer 4 (Water), so they never
  block movement but the aim SphereCast finds them.
- Ternaries of hex literals are `int`; view classes have `C(int)` overloads next to `C(uint)`.
- A nested class may not share a name with a method in the same class (hit twice: `Ripple`, `Toast`).
- `Tour()` and `UiTest()` are single long iterator methods: a new local may not reuse a name declared
  anywhere else in the method (CS0136). Drive input through the scripted `FPInput` (`Press`, `SetFlag`,
  `WalkTo`, `AimAt`); relative `-shots` paths are made absolute in `Awake`.
- Core is compiled with C# 9 (`LangVersion` in the sim's csproj matches Unity): no newer syntax, and no
  APIs outside .NET Standard 2.1. C#'s `Math.Round(x, -2)` throws; use `Math.Round(x / 100) * 100`.
- **No loops over quantities that grow with the economy.** `DigCrust` works per stratum, not per scoop;
  a rig digs only what its output buffer can take; a hand swing drops at most `Balance.MaxSwingItems` chunks.
- **After changing any price, rate or multiplier, re-fit:** `dotnet run -c Release --project Tools/BalanceSim -- fit --apply`
  (rewrites the `<fitted-scoops>` block in `ContentMalls.cs`), then re-run the 40 h engaged report and
  the casual one; the engaged total must stay ≥ 24 h, and read the "longest gap between purchases".
- **Levelled multipliers compound.** For the levelled nodes a player is buying at the same time, keep the
  sum of log(effect per level) / log(cost growth) well below 1, or income runs away (it did: ×200 in 40 min).
  Several steep sinks in parallel keep purchases frequent without runaway growth.
- `MallDef.ValueScale` multiplies every price and value in a mall; loot, wish and relic values are divided
  by it in `BuildMalls` so their actual values stay put. Change both together.
- Upgrade descriptions are flavour only; the terminal generates the effect line from `Kind`/`Value`
  (`TerminalPanel.EffectText`), so numbers never drift from the data. A new `TechKind` needs a case there.
- Tech nodes are laid out by `Col`/`Row` per branch; `dotnet run ... -- techs` warns about overlaps.
