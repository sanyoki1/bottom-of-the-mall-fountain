# CLAUDE.md — Wish Extractor (Unity)

Read `HANDOFF.md` first (state, verified results, open items), then `DESIGN.md` if you need the design.
Ask Nico before any git commit.

## Layout

| Path | What |
|---|---|
| `Assets/Scripts/Core/` | Pure C# rules (no UnityEngine): `Sim*.cs` (model), `Content*.cs` (all content data), `Balance.cs` (tunables), `SaveData.cs`, `Fmt.cs`. Compiled by Unity AND by `Tools/BalanceSim`. |
| `Assets/Scripts/View/` | Procedural 3D: `WorldBuilder` (mall), `FountainView` (basin + crust heightfield), `MachineVisuals` (19 machines), `Stations` (vending machine, piles, belts), `Clickables` (wishes, golden pennies, rat, worker), `FX`, `CameraRig`, `BloomFX`, `MeshKit`, `TexKit`, `Mats`, `Loot`, `Actors`, `GameView` (orchestrator + picking). |
| `Assets/Scripts/UI/` | uGUI built from code: `UIKit`, `HUD`, `ShopPanel`, `Popups`, `Modals`. |
| `Assets/Scripts/Audio/` | `Synth` (procedural SFX + music), `AudioHub`. |
| `Assets/Scripts/Game/` | `GameRoot` (entry point, input, events → feedback, autotour/uitest/loadtest), `SaveSystem`. |
| `Assets/Editor/ProjectBuilder.cs` | Scene creation, player settings, Windows build (menu + `BuildWindowsCI`). |
| `Assets/Resources/Shaders/` | `WE/Lit`, `WE/Crust`, `WE/Glow`, `WE/Soft`, `WE/Scroll`, `WE/Text`, `Hidden/WE/Bloom`. |
| `Tools/BalanceSim/` | .NET 10 console: bot plays the real Core; `fit --apply` writes stratum bounds. |

## Build / verify (Unity 6000.4.2f1 at `C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe`)

1. Compile + build: `Unity.exe -batchmode -nographics -projectPath . -executeMethod WishExtractor.EditorTools.ProjectBuilder.BuildWindowsCI -logFile Logs/build.log` (look for `error CS` and `[WishExtractor] build Succeeded`).
2. Visual check: `Builds/Windows/WishExtractor.exe -autotour -shots Screenshots -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile Logs/player_tour.log`, then read the PNGs.
3. Input check: `... -uitest -savefile wishextractor_uitest.json -fresh ...` → expect `[UITEST] done: 39 passed, 0 failed` in the log.
4. Balance: `dotnet run -c Release --project Tools/BalanceSim -- 40 1234 engaged` (and `idle`).

Players and editor runs block the shell until they exit; the tour/uitest quit on their own (watchdogs at 240 s / 120 s).
Never kill processes; close a stray player window gracefully (WM_CLOSE).

## Rules that bit before (keep them)

- **Vertex alpha is an emission mask** in `WE/Lit`. `new Color(r, g, b)` has alpha 1 and will glow like a
  lamp; use `MeshKit.Hex(0xRRGGBB)` (alpha 0) or pass alpha 0 explicitly.
- **Nothing may cap the basin at floor level.** The hall's grout base is an annulus for this reason; a full
  slab hid the crust as soon as it sank below y = 0.
- Ternaries of hex literals are `int`; view classes have `C(int)` overloads next to `C(uint)`.
- A nested class may not share a name with a method in the same class (hit twice: `Ripple`, `Toast`).
- **After changing any price, rate or multiplier, re-fit:** `dotnet run -c Release --project Tools/BalanceSim -- fit --apply`
  (rewrites the `<fitted-bounds>` and `<fitted-remodel-bounds>` blocks in `ContentMalls.cs`), then re-run
  the 40 h report and check totals stay ≥ 24 h for the engaged bot.
- Upgrade descriptions are flavour only; the shop generates the effect line from `Kind`/`Value`
  (`ShopPanel.EffectText`), so numbers never drift from the data.
- Mall-specific ("themed") upgrades must stay `MallOnly`; letting them carry over stacks multipliers
  across the game and collapses later malls to seconds.
- The UI must leave the fountain visible: machines live in the north arc (wash) and south arc (sort/sell);
  east/west are under the side panels.
