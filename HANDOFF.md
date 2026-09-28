# HANDOFF — Wish Extractor

_Last updated 2026-09-28 (session 4, a cloud session on Linux with no Unity: M6 balance done, tour/uitest
written but not run)._

## NEXT: verify M6 in Unity on Windows, then commit it as a milestone

Everything below "M6 done" compiles against Unity 2022.3 reference assemblies (`dotnet build
Tools/UnityCompileCheck`: only the one expected Unity 6-overload error), and the balance numbers are
measured, but **nothing was built or run in Unity this session**. Before calling M6 done:

1. Build (CLAUDE.md step 1) and fix any `error CS` the reference-assembly check couldn't see.
2. Run `-autotour` and look at every screenshot, especially the new ones: `30_terminal_security`,
   `31_guard_warning`, `32_guard_fine`, `33_chad`, `34_chad_chased`, `35_goldfish`, `36_goldfish_returned`.
3. Run `-uitest`: expect `[UITEST] done: 96 passed, 0 failed` (85 old checks + 11 new: the goldfish,
   Officer Doug's whistle and fine and "step out to avoid it", Chad chased by wading up and by E). Then
   `-loadtest`.
4. Play the first 20 minutes and the start of a dig by hand: this session changed the early economy and
   digging (see below), and only the bot has played it.
5. Refresh `Docs/Screenshots` from the tour (they are still v1's overhead shots; the README no longer uses them).

**Git:** branch `master` holds M1–M5 and the M6 WIP commit `4a99037`. This session worked on
`claude/upbeat-dijkstra-ypw7nn` (branched from `4a99037`) and left its changes uncommitted pending Nico's OK
(CLAUDE.md: ask before any commit). Once committed there, it is still unverified in Unity: review before
merging into `master`.

## M6 done (session 4)

### Mechanics that broke the balance (fixed in Core, so the game and the bot both get them)
- **Dig rigs dug on when their output was full** and threw the loot away, so the crust cleared at raw dig
  speed and crust size barely mattered (the fitter couldn't reach the targets even at 60M scoops).
  `DigCrust`'s sink now returns false when there's no room and digging stops there ("Full: output blocked").
- **Hand digging had no limit** (a jackhammer × Stronger Shoulders × Excavation Grant cleared late malls in
  minutes). It now stops while `Balance.RubbleCap` (300) gunk chunks lie in the fountain or your hands; the
  swing clangs and the HUD says to haul the gunk out or pump it into a line (`GameView.SwingAt`).
- **The factory ticked once per frame and ports handed over one item per tick**, so throughput depended on
  frame rate and the bot (0.5 s ticks) ran a factory several times slower than the game's. `Sim.Tick` now
  steps `UpdateFactory` at a fixed 1/60 s (`Balance.FactoryStep`), and machine-to-machine ports hand over up
  to 32 items per step.
- **`DigCrust` walked a scoop at a time**; it now steps a chunk at a time (late rigs break thousands of scoops
  a second).

### Economy
- **Early game** (targets from PIVOT_FPS: cup + bucket in ~10 min, generator + skimmer by 15–20, first line by
  ~45): a fresh fountain holds 400 loose coins (was 180, slightly richer mix), shoppers toss every 5 s at
  wishability 0 (was 7), `TierPerWish` 0.08 (was 0.075); cheaper bucket ($10), coloured lights ($8), neon
  ($45), hamster research $6 / build $2, skimmer $12 / $10, belts $5, Wired Deposit $12, hopper $12.
- **Bigger Chunks** (Tools branch, after Stronger Shoulders): the deep money sink. Each level adds 25% of the
  original chunk size (more crust and loot per chunk; rigs and swings dig that much faster); level L costs
  $300 × (L + 1)^2.5 (`TechDef.CostPower`, new), up to 999 levels. Before it, the late malls bought the whole
  tree in 12–35 minutes and then waited hours with billions in the bank. An exponential version (×1.1 per level)
  exploded instead: rich late malls bought 100+ levels at once and the fitter wanted 10¹¹-scoop crusts.
- **Layer boundaries are fitted** (`<fitted-bounds>` in `ContentMalls.cs`, fractions of the crust) so the
  engaged bot spends about as long in each layer below the loose one; inside a fitted layer the depth gauge
  follows the square root of the scoops. Before, geometric layers meant late malls sat in their last layer
  for three hours.
- Targets are now 3.25, 3.75, 4.25, 4.75, 5.25, 5.25 h (26.5 h) so every seed stays above 24 h.

### The balance bot (`Tools/BalanceSim`)
- Shops like a player: a cash **goal** (the most wanted tech by price × priority: containers 0.25, unlocks 0.45,
  tools 0.5, fountain jobs 0.35, levelled nodes 1.5, security 3), bought as soon as it's affordable; only
  pocket change (≤ 10% of the goal) goes elsewhere meanwhile. The next factory line competes as a goal.
- Upgrades lines when better machines unlock (pigeons → coin sorter, hopper → armoured hopper), not only when
  the intake changes; no coin rollers on dig lines (dug loot isn't coins).
- Plans lines at 34 rim angles and nudges each one outward/sideways until it fits (the 3-deep tunnel borer
  used to poke into the rim's no-build ring). It now fills 12 dig lines + 2 coin lines instead of 5 + 2;
  with the old planner the same crusts took 26 h instead of 15, i.e. the 24 h check was optimistic.
- Reports: hourly income and dig rate, layer arrival times, purchase count and longest gap, income by source
  (kiosk / hoppers / wishes / objectives), final lines per slot. `fit` does three passes per mall (size →
  equal-time layers → size) and widens its bracket as needed; `slots` probes line placement.
- The fitter's `Math.Round(best, -2)` crashed (.NET has no negative digits): fixed.

### Measured (balance bot)
- **Final fit** (engaged bot, seed 1234, each mall's last fit pass, chained): Crestview 3.26 h, Neon Galaxy
  3.77, Galleria Aurelia 4.20, Skyport 4.73, Lucky Lagoon 5.20, Eternity Plaza 5.19 = **26.35 h**. Crusts
  194,700 / 27.9M / 94.0M / 200.9M / 319.1M / 488.1M scoops.
- **The fit before it** (same game rules; the bot still built only 5 dig lines): engaged 26.09–26.42 h over six
  seeds, casual 27.5–28.1 h over three. Longest gap between purchases 12 min (Crestview), 10.5 min (Neon),
  under 5 min from Aurelia on; a new layer every ~20 min in Crestview up to ~45 min in Eternity; late-mall
  income tens of $M per minute.
- The six-seed and casual runs after the final fit didn't run this session. Run them before relying on the
  totals: `dotnet run -c Release --project Tools/BalanceSim -- 40 <seed> engaged` for a few seeds, and
  `-- 60 1234 casual`.
- **Early game** (Crestview; unaffected by the final bot and fit changes). Engaged, six seeds: cup and scrub
  in under 30 s, pail by ~1 min, bucket median ~7 min (40 s–15 min), hamster wheel ~10 min (40 s–17 min),
  skimmer ~21 min, first line ~37 min (24–45). Casual, four seeds: bucket 7–15 min, hamster 10–21, skimmer
  19–37, first line 36–61. PIVOT_FPS's targets: bucket within 10 min, generator + skimmer by 15–20, first line
  by ~45.

### Tour and uitest (written, compile-checked, not run)
- Tour: the Security tab, Doug's whistle and his fine (wading on purpose), Chad walked up to and chased off
  with E, a goldfish returned. Helpers `ApproachRival`, `SummonRival`, `DropGoldfishNearby` in `GameRoot`.
- uitest: +11 checks after the wish catch (see NEXT). Watchdog raised to 600 s (Chad walks in twice).

### Tools
- `Tools/UnityCompileCheck` (new): compiles `Assets/Scripts` against Unity 2022.3 reference assemblies from
  NuGet (`RocketModFix.UnityEngine.Redist`). It caught two type errors in the new uitest code this session.

## Ideas / later
- Where a human could beat the bot (so the real campaign could run a bit shorter than measured): routing
  belts around obstacles to fit more lines than the bot's straight radial ones (it fills 14 of its 34
  candidate rim slots; the south side stays clear for the entrance, kiosk and terminal), and hand digging
  alongside the rigs (the bot stops swinging once it owns a rig; the rubble cap bounds what that's worth).
  The bot ignores Officer Doug, but that costs it nothing that matters (fines were 0.2% of Crestview's
  income and ~0 later, once the Deputy Badge is bought).
- The casual bot is only ~7% slower than the engaged one, because late malls are factory-driven and both
  profiles build identically. A sloppier casual builder would make the casual numbers more meaningful.
- Late-mall money reaches tens of billions (Eternity). Fine for an incremental game, but if Nico wants
  smaller numbers, trim the permanent value bonuses (coin polish, Seniority, treasures) and re-fit.
- Late malls re-buy the early tree in minutes; more mall-specific content (new machines per mall) would make
  their first half more interesting than Bigger Chunks levels.
- Remodel laps (after mall 6) aren't fitted; they reuse each mall's fitted shape with crust ×1.6 per lap.

## Milestone notes (M1–M5, verified and committed on Windows)

**M1:** first-person controller (`View/FirstPerson.cs`, injectable `FPInput`), view-model hands, instanced
loose items, COIN-O-MATIC 3000, WE/Water, south wall + skylight ceiling + colliders, FP HUD, Core `Sim` v2,
SaveData v2. **M2:** shopper crowd (`SimCrowd.cs`, 10 archetypes), True Wishes, the Fountain Improvement
Plan, first beautification visuals, speech bubbles, positional audio. **M3:** MAINT-OS 95 tech tree
(`TerminalPanel`), carry/grab ladders, held tool models, area grab, shop-vac, detector glints, fountain
decor, wormhole tosses. **M4:** the factory (`SimFactory.cs`: grid, belts, splitter, skimmer, pump, claw,
hoppers, power; `FactoryView`, `BuildMode`, `BuildMenu`). **M5:** the crust and processing chain
(`SimCrust.cs`: digging, strata, processors, relics, events, bare concrete → contract → `Prestige()`, Head
Office). **M6 WIP (`4a99037`, Nico's session 3):** hazards (`SimHazards.cs`, `View/Hazards.cs`), the Security
branch, the goldfish, footsteps, more jokes, the first bot rewrite and economy fixes (per-mall `LootScale` /
`StoryScale`, coin ladder and oddity values, late fountain upgrade prices).

v1 (the overhead auto-clicker) lives on only in git history at `96a8f81`.
