# HANDOFF — Wish Extractor

_Last updated 2026-09-27 (end of session 3: M1–M5 committed, M6 in progress, stopped at Nico's request)._

## NEXT: finish M6 (balance fit + tour/uitest for hazards + docs)

Nico wants the game reworked into a first-person, Find The Needle–style factory game with NPCs throwing
coins into the fountain. Plan and confirmed decisions: `PIVOT_FPS.md`. Session prompt: `NEXT_SESSION_PROMPT.md`.

**Git:** nested repo in this folder, branch `master`. Commits: `96a8f81` v1 baseline, `f37e470` M1,
`7edf45a` M2, `d8089f0` M3, `ec44256` M4, `053f424` M5. Commit after each verified milestone (authorized).

**Working tree = uncommitted M6 WIP. It compiles** (Unity batch build OK, BalanceSim OK) and the existing
`-uitest` still passes 85/85. Not yet run on this WIP: `-autotour` (and nothing in it shows the new hazards).

### M6 done so far (uncommitted)
- Hazards, always on (`Core/SimHazards.cs`, `View/Hazards.cs`): Officer Doug patrols a loop at r = 13.5;
  every 60–150 s, if you're wading within 22 m, he whistles and warns, and fines you after 5 s in the water
  (4% of cash, min $0.05 × scale; reduced by Security techs). Chad the rival diver arrives every 5–9 min
  (after 40 tosses), steals the richest nearby loose items, and flees dropping everything when you get
  within 2.2 m or press E on him (trigger collider on the Water layer, `TargetKind.Rival`).
- Security branch (tab added to MAINT-OS): Donut Diplomacy, 'No Rival Divers' Sign, Honorary Deputy Badge
  (`TechKind.GuardFine`, new `TechKind.RivalRepel`).
- Goldfish oddity: picking it up returns it to the water (+1 Wish Token, applause message); intakes and
  Chad ignore it (`Sim.IsFish`).
- Juice: footsteps (`Synth.Footstep`, wading plops), whistle (`Synth.Whistle`), 8 more receipt jokes,
  toasts for fines/Chad/goldfish.
- Balance bot rewritten (`Tools/BalanceSim/Bot.cs`): plays the real Core with abstracted walking (kiosk
  ↔ fountain, item to item, ramp length from depth), real pickups/deposits/wishes/dig swings, cheapest-first
  shopping (cash, tokens, Lucky Pennies), factory lines planned radially from up to 18 rim slots (2 coin
  lines: skimmer→pump→claw [+roller]; the rest dig lines: rig→borer + tumbler + sorter [+roller], then a
  hopper), generator field at x 18..32, z −27..−17, prestige + Head Office spending. `Program.cs`:
  `run` (default, writes `report_<profile>.txt`), `fit [--apply]` (bisects each mall's crust size against
  `TargetHours` = 3, 3.5, 4, 4.5, 5, 5 and rewrites the `<fitted-scoops>` block in `ContentMalls.cs`),
  plus `counts | crowd | factory | crust | smoke`. `BOT_REPORT=<seconds>` env var sets the log interval.
- Economy fixes found by the bot (the first run finished all six malls in 2.9 h):
  - v1's per-mall values are divided back down: `MallDef.LootScale` {1, 25, 225, 28, 60, 40} for crust loot,
    `MallDef.StoryScale` {1, 10, 84, 83, 110, 118} for wishes and relics (set in `ContentMalls.BuildMalls`).
  - Coin ladder top tamed: silver $5 @ W55, gold $20 @ W85, diamond $100 @ W120.
  - Oddities moved to the top of the ladder: cheap silly ones early (duck $0.50 @ W18 … toaster $6 @ W55),
    the ridiculous ones after diamonds (seed phrase $120 @ W110 … moon rock $2,000 @ W195); archetype gates
    raised (influencer 35, proposer 65, bodybuilder 120, billionaire 150).
  - Late fountain upgrades cost what they unlock (music $2k, dispenser $12k, golden statue $150k,
    certification $3M; token ones 15/40/150/500/2000); Polish the Tiles growth 1.62.
  - Early costs trimmed so automation arrives sooner (pail $3, bucket $15, hamster research $10 / build $3,
    skimmer $18 / $15, Wired Deposit $20).
- Crust sizes are still the unfitted placeholders (`CrustScoops` in each MallDef; `FittedScoops` all 0).
  Latest unfitted engaged run: Crestview 5.0 h, Neon 2.9, Aurelia 1.7, Skyport 0.8, Lagoon 0.4,
  Eternity 0.3 = **11.1 h** (needs ≥ 24 h).

### M6 still to do
1. `dotnet run -c Release --project Tools/BalanceSim -- fit --apply`, then re-run the 40 h engaged report
   and a `casual` one; check engaged total ≥ 24 h and read the per-mall logs for sanity (runaway income,
   dead stretches, slots that never fit). The late malls finish fast because of Head Office perks and
   richer content: expect the fit to grow their crusts a lot; if the late part of a mall turns into
   "waiting for borers", consider a money-sink dig upgrade (a levelled DigPower node with steep growth
   was planned but not added yet).
2. Check early pacing in the Crestview "firsts" line against PIVOT_FPS.md (cup/bucket in the first ~10 min,
   generator + skimmer by ~15–20 min, first line by ~45 min).
3. Tour: add shots of Officer Doug warning, Chad in the fountain, a goldfish, the Security tab; run the
   full `-autotour` and look at every screenshot (M5 tour shots were fine).
4. uitest: add checks for the guard fine (`sim.DebugGuardCheck()` while wading), chasing Chad
   (`sim.DebugRival()`, walk to him), and returning a goldfish.
5. Build, tour, uitest, loadtest → commit M6.
6. Docs: rewrite HANDOFF.md, CLAUDE.md (layout table, `fit --apply` now writes `<fitted-scoops>`, new
   rules below), DESIGN.md and README.md for the first-person game.

### Lessons this session (also worth adding to CLAUDE.md)
- A lit mesh at exactly zero scale makes NaN pixels that bloom smears into black squares; use 0.01 and
  deactivate instead.
- World TextMesh colour must have alpha 1 (`Mats.NewText` forces it); v1's coloured signs were invisible.
- The player is on the Ignore Raycast layer; wishes and Chad are triggers on layer 4 (Water) so they never
  block movement but the aim ray (SphereCast) finds them.
- Additive `WE/Glow` multiplies by vertex alpha, so it can't draw matte (alpha 0) meshes; build ghosts use
  `WE/Ghost`.
- In the tour/uitest, drive input through `scripted` FPInput (`Press`, `SetFlag`, `WalkTo`, `AimAt`); relative
  `-shots` paths must be made absolute.

Detailed per-milestone notes follow.

**M1 (verified, committed):** first-person controller (`View/FirstPerson.cs`, injectable `FPInput`),
view-model hands (`Hands.cs`), instanced loose items (`ItemRenderer.cs`), COIN-O-MATIC 3000 (`Kiosk.cs`),
WE/Water shader, south wall + skylight ceiling + colliders (`WorldBuilder`), water/rim/stepping-stone/crust
colliders (`FountainView`), FP HUD, Core `Sim` v2 (loose items, carry, deposit, tech levels), SaveData v2.
Tour: 18 screenshots; uitest 28/28; loadtest restores pose, cash, carry tier and loose items.
Lessons: a lit mesh at exactly zero scale makes NaN pixels that bloom turns into black squares; world
text colours must have alpha 1 (`Mats.NewText` now forces it — v1's coloured signs were invisible);
the player sits on the Ignore Raycast layer so the aim ray doesn't hit its own capsule.

**M2 (verified, committed):** shopper crowd in Core (`SimCrowd.cs`: 10 archetypes walk in from doors in
`Layout.cs`, stand at r = 10.7, wind up, toss coins/oddities/gum along the Gaussian tier curve, speak barks
or wish quotes, leave), True Wishes rising where wishful tosses land (catch with E: cash + Wish Tokens +
journal), the Fountain Improvement Plan easel (buys Fountain-branch techs), first 3 beautification upgrades
with visuals (scrubbed tiles, water jets, coloured LED ring), speech bubbles, positional splash/plop audio.
uitest 38/38. `dotnet run ... -- crowd <wishability>` prints what the crowd is doing headlessly.

**M3 (verified, committed):** Maintenance Terminal (`View/Terminal.cs` prop, `UI/TerminalPanel.cs` =
MAINT-OS 95: branch tabs, node graph by Col/Row with prerequisite lines, detail pane with generated effect
line). 36 nodes so far (carry ladder + Sturdier Bottoms/Comfy Sneakers, grab ladder + Longer Arms/Nimble
Fingers, 13 fountain uniques — 5 paid in Wish Tokens — plus 4 levelled fountain nodes). Held tool models and
floor-level carts/barrows (`Hands.cs`), area-grab ring, shop-vac auto pickup, detector glints, all fountain
decor (`Decor.cs`), wormhole tosses of other malls' loot. Dig tools are defined but not sold until M5.
uitest 49/49 (buys through the real terminal UI).

**M4 (verified, committed):** the factory. Core `SimFactory.cs` (1 m grid on the hall floor, placement rules
incl. rim-only intakes that must face the fountain, stepping stones and the entrance kept clear; belts with
0.25-spaced items that hop cell to cell; splitter; skimmer bot that roams and docks; drain pump; claw
crane; hoppers that sell; one global power budget with brownout ratio; save/load of buildings, buffers and
belt items). `ContentBuild.cs`: 11 buildables + 16 Power/Intake/Logistics tech nodes. View: `FactoryView.cs`
(models, scrolling belts, instanced belt items, bot/hose/claw animation), `BuildMode.cs` (grid ghost with
`WE/Ghost` shader, R, auto-facing, belt drag, X demolish), `UI/BuildMenu.cs` (Tab catalogue). Headless:
`dotnet run ... -- factory`. uitest 69/69 builds a working skimmer→belt→hopper line through the real input path.

**M5 (verified, committed):** the crust and the processing chain. Core `SimCrust.cs`: `DigCrust` turns scoops
into loot (loose layer) or gunk chunks (deeper strata, one per `Balance.ChunkValue` scoops); hand swings
(hotbar 2) and dig rigs/borers; depth mapping (loose layer linear, deeper layers log) drives `FountainView`
(the crust sinks, water follows, colliders rebuild, a scaffold ramp spirals down the wall); processors
(tumbler/pigeon sorter/coin sorter/roller/bagger/palletiser/melter/compressor) pass through what they
can't use; sorting finds relics; mall events (per-mall effects, Jackpot Hour rains gold coins); bare
concrete → treasure → contract modal → `Prestige()` (Lucky Pennies; non-Head-Office techs, cash, factory
reset); Head Office perks (`TechDef.LuckyPennies`, saved in `S.headOffice`). `Sim.Scale` = mall value scale
× remodel growth. uitest 85/85 (dig by hand, gunk, bare concrete, sign, buy a Head Office perk).
Headless: `dotnet run ... -- crust`.

Session 2 notes (kept for history):
- Done (session 2):
  - `Core/Defs.cs` rewritten for v2: ItemType/ItemCat, CarryDef, ToolDef, ArchetypeDef, TechDef/TechKind/
    TechBranch, ObjectiveDef with a flat reward. MallDef now has CrustScoops, ValueScale, LootTypes and
    GunkTypes.
  - `Core/Balance.cs` rewritten (tosses, tier curve, wishes, crust, deposit rates).
  - `Core/ContentWorld.cs` added: item registry (9 coin tiers, 19 oddities, per-mall loot, gunk and relic
    types, processed goods), 10 carry tiers, 7 grab tools, 6 dig tools, receipt and "hands full" jokes.
  - `Core/ContentMalls.cs` trimmed: v1 fitted bounds gone, CrustScoops per mall (placeholders), Crestview
    event now boosts tosses.
- Still to do before M1 compiles:
  - `Content.cs`: after BuildMalls, call BuildCoins, BuildOddities and BuildMallTypes; expose the `types`
    list as an `Items` array; add Carry, GrabTools and DigTools; drop Machines, Tools, Upgrades and
    HeadOffice.
  - `ContentMeta.cs`: achievements and objectives against the new Sim.
  - `SaveData.cs` v2: player pose, carried and loose items with a type-id table, tech levels, stats,
    mouse sensitivity and FOV settings.
  - `Sim.cs` v2: loose items, toss scheduler, pickup, deposit, crust.
  - Remove the v1-only files with `git rm`, including their `.meta`: SimShop, ContentShop, MachineVisuals,
    Stations, CameraRig, Clickables (keep RarityColors), ShopPanel.
  - Rewrite GameView, HUD, Modals and GameRoot (FP autotour, uitest and loadtest).
  - New view code: first-person controller, first-person hands, instanced item renderer, COIN-O-MATIC kiosk,
    water shader.
  - WorldBuilder: south wall, ceiling, colliders. FountainView: water, crust collider, ramp.
  - Stub `Tools/BalanceSim/Program.cs` against the new Core so it compiles until M6.
- Note: `ContentMalls.cs` was edited once with a PowerShell `WriteAllText`. Use Write/Edit for file changes.

Everything below describes the v1 (overhead auto-clicker) build at commit `96a8f81`.

## State

Complete, playable game: six malls, endless Remodel contracts, full UI, procedural art and audio,
save/load and offline progress. Windows build at `Builds/Windows/WishExtractor.exe` (not in git).
No git commits yet (the folder sits inside the home-directory repo; ask Nico before `git init`/commit).

## Verified (measured, not assumed)

- Compiles clean in Unity 6000.4.2f1; Windows player builds (85 MB).
- `-uitest`: 39/39 checks pass (intro, dig, sell button and vending machine, buy tool/machine, journal tabs,
  settings, shop toggle, clear mall → contract → sign → new mall intro → buy a Head Office perk).
- `-loadtest`: save/load restores cash, tool, machines, clicks, depth, objective; offline 2 h simulated
  at 50% efficiency.
- `-autotour`: screenshots of every mall and every major screen; ~165 FPS at 1920×1080 in the busiest
  Crestview scene (RX 7800 XT).
- Balance sim: engaged bot 24h29m for the six malls, idle bot 40h55m; Remodel lap 1 ≈ 9 h (engaged).

## Not verified

- Audio was never listened to (the clips are synthesised and play without errors; mix levels are guesses).
- Real OS mouse input was not driven end-to-end (computer-use couldn't target the unpackaged exe); the
  uitest drives the same EventSystem raycasts and world picking a real click uses.
- Only 1600×900 and 1920×1080 windows were looked at; ultrawide/4K layouts are untested.

## Ideas / next steps

- Play it for real and tune the feel: click juice, sound levels, wish frequency, golden penny rate.
- The idle profile stalls in the Lucky Lagoon's last layers (12 h); if idle players matter, add a late
  Lucky Lagoon upgrade or soften its last two boundaries.
- Late-mall purchase gaps reach ~13 min for the engaged bot (malls 5–6); more late upgrades would help.
- Possible additions: per-mall gimmick mechanics beyond timed events, cosmetic worker hats, a statistics
  graph, controller support, Steam-style achievements.
