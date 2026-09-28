# HANDOFF — Wish Extractor

_Last updated 2026-09-28 (session 5, a cloud session on Linux with no Unity license: M6 compile-checked
against the real Unity 6000.4.2f1 assemblies, three hazard bugs and the overflowing build catalogue fixed,
re-fitted; still not built or run in Unity)._

## NEXT: verify M6 in Unity on Windows, then commit it as a milestone

Sessions 4 and 5 ran in cloud containers where the Unity editor can't start (it needs an activated
license, even in batch mode), so the checks that need Unity are still open:

1. Build (CLAUDE.md step 1). Session 5 compiled every script with Unity 6000.4.2f1's own compiler and
   assemblies (`Tools/UnityCompileCheck/unity6_compile.sh`: 0 errors, 0 warnings, editor and player
   defines, `Assets/Editor` included), so the build should compile cleanly.
2. Run `-autotour` and look at every screenshot, especially the new ones: `30_terminal_security`,
   `31_guard_warning`, `32_guard_fine`, `33_chad`, `34_chad_chased`, `35_goldfish` (the prompt should read
   "Put the goldfish back in the water"), `36_goldfish_returned`, and `18_build_catalogue` (new six-column
   layout, see below).
3. Run `-uitest`: expect `[UITEST] done: 96 passed, 0 failed` (85 old checks + 11 new: the goldfish,
   Officer Doug's whistle and fine and "step out to avoid it", Chad chased by wading up and by E). Then
   `-loadtest`.
4. Play the first 20 minutes and the start of a dig by hand: session 4 changed the early economy and
   digging (see below), and only the bot has played it.
5. Refresh `Docs/Screenshots` from the tour (they are still v1's overhead shots; the README no longer uses them).

**Git:** branch `master` holds M1–M5 and the M6 WIP commit `4a99037`. Session 4's M6 work is commit
`93cae22` on `claude/upbeat-dijkstra-ypw7nn`; session 5 continued on `claude/wish-extractor-m6-verify-m6ww7w`
(fast-forwarded to `93cae22`). Unverified in Unity: review before merging into `master`.

## Session 5 (cloud, no Unity license)

- **Compile check against the real Unity 6.** `Tools/UnityCompileCheck/unity6_compile.sh` (new) takes
  Unity's own C# compiler (Roslyn 4.3.1), .NET Standard 2.1 profile and engine/editor module assemblies from
  the Linux editor archive, builds uGUI from the `com.unity.ugui` 2.0.0 source, then `Assembly-CSharp`
  (editor and player defines) and `Assembly-CSharp-Editor`. Setup (a 4.1 GB download) is in the script's
  header. It replaces the old NuGet check's "one expected error": the Unity 6 overload compiles.
- **Hazard bugs fixed** (found by reading the code; none of them shows up in a compile):
  - The "No Rival Divers" sign was inverted: `rivalTimer -= dt / RivalFreqMult` made each level bring Chad
    ~43% *more* often. It now slows his clock (`dt * RivalFreqMult`).
  - With the Honorary Deputy Badge (fines × 0) Officer Doug still whistled and "fined" you $0.00 with a toast.
    He now tips his cap instead.
  - Aiming at a goldfish said "[E] Pick up Live Goldfish $0.00" (or "Hands full" when your container was
    full, although E returns it either way). It now says "Put the goldfish back in the water +1 ✦".
- **The build catalogue ran off the panel** (since M5): eight processors in one column of 142 px cards in a
  760 px panel put the palletiser, melter and compressor below the panel and partly off-screen (the tour's
  `18_build_catalogue`). It now has six columns (processing split into "wash & sort" and "bundling", output
  shares "logistics & output"), 120 px cards and a 1760 × 960 panel that shrinks to fit smaller canvases; the
  longest column holds 5 cards (6 fit). Check it in the tour shot.
- Officer Doug no longer snaps to face north whenever he pauses on patrol (heading from `atan2(0, 0)`).
- **Balance.** Before the fixes, on session 4's fit: engaged seeds 1–5 took 26.29–26.57 h, casual 27.46 h.
  The Chad fix changes a rate, so `fit --apply` ran again: crusts 175,300 / 24.8M / 93.7M / 206.3M / 330.2M /
  488.1M scoops (fit passes 3.35, 3.73, 4.23, 4.77, 5.28, 5.17 h). Full runs on it: engaged seed 1234
  **26.58 h** (3.18 · 3.95 · 4.15 · 4.80 · 5.31 · 5.19), seeds 1–5 26.51, 27.02, 26.43, 26.24, 26.61 h; casual
  seed 1234 **27.74 h**. Lines at the end: 2 claws and 12 dig lines (rigs in Crestview, borers after).
- Longest gap between purchases (engaged, seeds 1–5 and 1234): 20–24 min in Crestview (starting around 2h10–2h30,
  just before the bot builds out its dig lines), 13–14.5 min in Neon Galaxy, 5.5–7.5 min in Aurelia, under
  4 min after that; casual 26 min in Crestview. Session 4's docs said 12 min for Crestview: that was measured
  before its final bot changes.

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
- Session 5 ran the seed and casual checks and re-fitted after its fixes: see "Session 5" above for the
  current numbers.
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
