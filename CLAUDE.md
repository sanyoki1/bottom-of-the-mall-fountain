# CLAUDE.md — Wish Extractor (Unity)

Read `HANDOFF.md` first (state, verified results, open items), then `DESIGN.md` if you need the design.
`PIVOT_FPS.md` holds the settled decisions behind the first-person game; don't re-ask them.
Ask Nico before any git commit.

## Layout

| Path | What |
|---|---|
| `Assets/Scripts/Core/` | Pure C# rules (no UnityEngine): `Sim.cs` (loose items, carrying, deposits, tech tree, recalc), `SimCrowd.cs` (shoppers, tosses, wishes), `SimFactory.cs` (build grid, belts, intakes, hoppers, power), `SimCrust.cs` (digging, strata, processing, relics, events, prestige), `SimHazards.cs` (Officer Doug, Chad), `SimMallMachines.cs` (the late malls' own machines: cork cannon, baggage carousel and drones, slot machine, Old Well), `Content*.cs` (all content data), `Defs.cs`, `Balance.cs` (tunables), `Layout.cs`, `SaveData.cs`, `Fmt.cs`. Compiled by Unity AND by `Tools/BalanceSim`. |
| `Assets/Scripts/View/` | Procedural 3D: `GameView` (orchestrator, aiming, interaction), `FirstPerson` (controller + injectable `FPInput`), `Hands`, `ItemRenderer` (instanced loose and belt items), `WorldBuilder` (mall hall), `FountainView` (basin, water, crust heightfield, colliders, ramp), `Kiosk`, `Terminal`, `Board`, `Decor`, `Crowd`, `Wishes`, `Hazards`, `FactoryView`, `MallMachines` (models and animation of the mall machines), `BuildMode`, `FX`, `BloomFX`, `MeshKit`, `TexKit`, `Mats`, `Loot`, `Actors`, `ViewCommon`. |
| `Assets/Scripts/UI/` | uGUI built from code: `UIKit`, `HUD`, `Popups`, `Modals`, `TerminalPanel` (MAINT-OS 95, the tech tree), `BuildMenu`. |
| `Assets/Scripts/Audio/` | `Synth` (procedural SFX + music), `AudioHub`. |
| `Assets/Scripts/Game/` | `GameRoot` (entry point, input, events → feedback, `-autotour` / `-uitest` / `-loadtest`), `SaveSystem`. |
| `Assets/Editor/ProjectBuilder.cs` | Scene creation, player settings, Windows build (menu + `BuildWindowsCI`). |
| `Assets/Resources/Shaders/` | `WE/Lit`, `WE/Crust`, `WE/Water`, `WE/Glow`, `WE/Ghost`, `WE/Soft`, `WE/Scroll`, `WE/Text`, `Hidden/WE/Bloom`. |
| `Tools/BalanceSim/` | .NET 10 console: a bot plays the real Core; `fit --apply` writes each mall's crust size and layer bounds. |
| `Tools/UnityCompileCheck/` | Compile checks for machines without Unity: `unity6_compile.sh` (the real Unity 6 compiler and assemblies) and a quicker NuGet-based `.csproj`. |

## Build / verify (Unity 6000.4.2f1 at `C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe`)

1. Compile + build: `Unity.exe -batchmode -nographics -projectPath . -executeMethod WishExtractor.EditorTools.ProjectBuilder.BuildWindowsCI -logFile Logs/build.log` (look for `error CS` and `[WishExtractor] build Succeeded`).
2. Visual check: `Builds/Windows/WishExtractor.exe -autotour -shots Screenshots -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile Logs/player_tour.log`, then read every PNG.
3. Input check: `... -uitest -savefile wishextractor_uitest.json -fresh ...` → expect `[UITEST] done: 121 passed, 0 failed` in the log.
4. Save check: `... -loadtest -savefile wishextractor_uitest.json ...` reads the uitest's save back (`[LOADTEST]` line).
5. Balance: `dotnet run -c Release --project Tools/BalanceSim -- 40 1234 engaged` (and `casual`, and a few other seeds): every engaged total must stay ≥ 24 h.

Players and editor runs block the shell until they exit; the tour and uitest quit on their own (watchdogs at 720 s / 780 s).
Never kill processes; close a stray player window gracefully (WM_CLOSE). Never start a build while a player from the
same `Builds/` folder is still running.

If Nico has the project open in the Unity Editor, batch builds fail ("another Unity instance is running with this
project open"). Don't touch the Editor: build in a worktree (`git worktree add --detach ../wish-extractor-verify HEAD`,
copy the changed files over, pass that folder as `-projectPath`; the first import takes about a minute).

No Unity (cloud or Linux sessions): `bash Tools/UnityCompileCheck/unity6_compile.sh <extracted editor>` compiles every
script (editor and player defines, `Assets/Editor` too) with Unity 6000.4.2f1's own compiler and assemblies, taken from
the Linux editor archive (setup in the script's header); expect 0 errors. The quicker `dotnet build
Tools/UnityCompileCheck` uses Unity 2022.3 reference assemblies from NuGet: expect exactly one error, `GameRoot.cs …
'FindObjectsByType' takes 0 arguments` (a Unity 6 overload). Both only catch compile errors: a milestone still needs
steps 1–4 on Windows.

## Rules that bit before (keep them)

- **Vertex alpha is an emission mask** in `WE/Lit`. `new Color(r, g, b)` has alpha 1 and will glow like a
  lamp; use `MeshKit.Hex(0xRRGGBB)` (alpha 0) or pass alpha 0 explicitly.
- **Nothing may cap the basin at floor level.** The hall's grout base is an annulus for this reason; a full
  slab hid the crust as soon as it sank below y = 0.
- Ternaries of hex literals are `int`; view classes have `C(int)` overloads next to `C(uint)`.
- A nested class may not share a name with a method in the same class (hit twice: `Ripple`, `Toast`).
- A lit mesh at exactly zero scale makes NaN pixels that bloom smears into black squares; use 0.01 and deactivate.
- World TextMesh colour must have alpha 1 (`Mats.NewText` forces it); v1's coloured signs were invisible.
- The player is on the Ignore Raycast layer; wishes and Chad are triggers on layer 4 (Water), so they never block
  movement but the aim ray (SphereCast) finds them.
- Additive `WE/Glow` multiplies by vertex alpha, so it can't draw matte (alpha 0) meshes; build ghosts use `WE/Ghost`.
- **Mesh particles read vertex colours as bytes.** A MeshKit mesh (float colours) used as a particle mesh must go through
  `FX.ByteColours`, and the particle renderer must not be GPU-instanced (WE/Lit has no particle-instancing setup).
  Float white came out as (0, 0, 0.5, 0.25): every coin shower was navy and every confetti flake blue, from M1 to M7.
- The tour and uitest drive input through the scripted `FPInput` (`Press`, `SetFlag`, `WalkTo`, `AimAt`); relative
  `-shots` paths must be made absolute.
- **After changing any price, rate or multiplier, re-fit:** `dotnet run -c Release --project Tools/BalanceSim -- fit --apply`
  (about 10 minutes; rewrites the `<fitted-scoops>` and `<fitted-bounds>` blocks in `ContentMalls.cs`), then run the
  40 h engaged report for a few seeds and check every total stays ≥ 24 h (the targets add up to 26.5 h for margin).
- Tech descriptions are flavour only; the terminal generates the effect line from `Kind`/`Value`
  (`TerminalPanel.EffectText`), so a new `TechKind` needs a line there.
- The factory runs at a fixed 1/60 s step inside `Sim.Tick` (ports and belts hand over items per step). Never tick
  `UpdateFactory` with a frame's dt, or the bot and the game disagree about throughput.
- Nothing may destroy items silently: dig rigs stop when their output is full (`DigCrust`'s sink returns false) and
  hand digging stops at `Balance.RubbleCap`. The old rigs dug on and threw the loot away, so crust size stopped mattering.
- Deep money sinks use `CostPower` (polynomial cost) with a linear effect (`Bigger Chunks`). Exponential sinks explode in
  the rich late malls: the fitter wanted crusts of 10¹¹ scoops.
- The balance bot must play like a competent player (save for the next big thing, upgrade lines, fill the rim), or the
  ≥ 24 h check is optimistic: fitting 12 dig lines instead of 5 cut the same crusts from 26 h to 15 h. After bot changes,
  read the report's `lines at the end` and `longest gap` lines.
- A mall's own machine hangs off a `TechDef` with `MallOnly = true` and `UnlockMall` set: `Sim.TechInThisMall` /
  `BuildInThisMall` hide it everywhere else (terminal, catalogue, `CanPlace`), and it comes back on that mall's remodel
  laps. Nothing it buys may outlive the mall. `Tools/BalanceSim -- machines` checks all four in the Core.
