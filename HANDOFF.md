# HANDOFF — Wish Extractor

_Last updated 2026-09-29 (session 5, a cloud session on Linux with no Unity license: M6 compile-checked
against the real Unity 6000.4.2f1 assemblies, fixed and re-fitted; then M7, one machine of their own for
each of the four later malls, written, compile-checked, taught to the balance bot and re-fitted. Neither
M6 nor M7 has been built or run in Unity yet)._

## NEXT: verify M6 and M7 in Unity on Windows, then commit them as milestones

Sessions 4 and 5 ran in cloud containers where the Unity editor can't start (it needs an activated
license, even in batch mode), so the checks that need Unity are still open:

1. Build (CLAUDE.md step 1). Every script compiles with Unity 6000.4.2f1's own compiler and assemblies
   (`Tools/UnityCompileCheck/unity6_compile.sh`: 0 errors, editor and player defines, `Assets/Editor`
   included), so the build should compile cleanly.
2. Run `-autotour` (60 shots now) and look at every screenshot, especially the ones nobody has seen yet:
   - M6: `18_build_catalogue` (six-column layout, see below), `30_terminal_security`, `31_guard_warning`,
     `32_guard_fine`, `33_chad`, `34_chad_chased`, `35_goldfish` (the prompt should read "Put the goldfish
     back in the water"), `36_goldfish_returned`.
   - M7: `40_aurelia_cannon` (a cork in flight over the rim, smoke at the muzzle), `41_aurelia_splash`
     (rinsed chunks and a champagne geyser in the water), `42_skyport_carousel` (drones between the borer at
     the rim and the carousel at the back), `43_lagoon_slots` (reels spinning), `44_lagoon_jackpot` (confetti,
     tokens flying into the fountain), `45_eternity_well` (a wish orb flying into the well, windlass turning),
     `46_terminal_mall_only` (Priority Tags marked "SKYPORT TERMINAL C ONLY · NOT KEPT"),
     `47_catalogue_skyport` (the carousel listed, the cannon not). The four models have never been rendered:
     check their scale and orientation (the cannon and the well face the fountain; the slot machine shows its
     reels on the sides of its line), what glows (vertex alpha), and that the carousel sign reads correctly
     from both sides.
3. Run `-uitest`: expect `[UITEST] done: 121 passed, 0 failed` (85 from M5, 11 from M6, 25 from M7: see
   "M7" below). Then `-loadtest`: the uitest now ends in Eternity Plaza with the Old Well built.
4. Play the first 20 minutes and the start of a dig by hand: session 4 changed the early economy and
   digging (see below), and only the bot has played it. With `-dev` (F6 finishes a mall, F5 adds cash),
   reach the later malls and try each machine by hand.
5. Refresh `Docs/Screenshots` from the tour (they are still v1's overhead shots; the README no longer uses them).

**Git:** branch `master` holds M1–M5 and the M6 WIP commit `4a99037`. Session 4's M6 work is commit
`93cae22` on `claude/upbeat-dijkstra-ypw7nn`; session 5 continued on `claude/wish-extractor-m6-verify-m6ww7w`
(M6 verification pass `0360e0d`, then M7 on top). Unverified in Unity: review before merging into `master`.

## M7: the later malls' own machines (session 5, continued)

Nico's brief: malls 3–6 each get one thing to build that exists only there (not carried into other malls),
with a procedural model and animation, a tech node or two and jokes in the mall's theme, and it has to change
how you build or dig there, not just add a multiplier. The four (rules and numbers in DESIGN.md, "The mall
machines"):

- **Galleria Aurelia: the Champagne Cork Cannon.** Stands anywhere within 20 m of the fountain, facing it,
  and lobs slabs of crust into the water, already rinsed. The rim goes to pumps and claws feeding sorters
  directly (no tumbler); the cannons stand in the second row. Vintage Reserve makes the slabs bigger.
- **Skyport Terminal C: the Baggage Claim Carousel.** Cargo drones empty every rim intake that has no belt
  behind it and fly the loads to the carousel, where the line starts. The rim fills with bare borers; the
  processing moves to the back of the hall. Priority Tags add drones.
- **The Lucky Lagoon: the Slot-Machine Sorter.** Raw gunk in, sorted loot out (×1, ×2, ×5) or nothing;
  7-7-7 sprays jackpot tokens over the fountain and drops a relic. Replaces tumbler + sorter at the house's
  price. Loosen the Reels shaves the house edge.
- **Eternity Plaza: the Old Well.** One per mall, at the rim. Wishes nobody catches within 3 s fall in, and
  24 chunks × the wish's token value of crust stop existing. Deeper Wishes is an endless polynomial sink.

How it's built:
- Core: `TechDef.MallOnly` (with `UnlockMall`); `Sim.TechInThisMall` / `BuildInThisMall` hide a mall's machine
  everywhere else (`TechUnlocked`, `BuildUnlocked`, `CanPlace`, the terminal, the catalogue), and it returns on
  that mall's remodel laps. `BuildDef.MaxRange` (the cannon; build mode turns it to face the fountain),
  `Unique` (the well), `IsCarousel`. New `TechKind`s `CannonSlab`, `DroneCount`, `SlotOdds`, `WellDepth`
  (with effect lines in `TerminalPanel.EffectText`), items "Champagne-Rinsed Chunk" and "Jackpot Token", save
  counters `cannonBlasts`, `droneTrips`, `slotSpins`, `jackpots`, `wellWishes`; a drone's cargo is saved into
  its carousel. All in `Core/SimMallMachines.cs` plus small hooks in `SimFactory` / `SimCrust` / `SimCrowd`.
- The terminal marks these nodes "<MALL> ONLY · NOT KEPT"; the catalogue lists only this mall's machine (the
  cannon and the well sit under DIGGING).
- View: `View/MallMachines.cs` builds the four models and animates them (recoil and a flying cork with a
  champagne geyser where it lands; the carousel's belt, bags and flip-sign plus its drones; reels that stop on
  the spin's symbols, lever and siren; the well's windlass and bucket). Wish orbs fly into the well. HUD hover
  text for each. Sounds (`Synth`): pop, reels, jackpot, drone whirr, well chime; speech lines for the
  sommelier, Elvis (the Lagoon's floor host) and the well.
- Fixed in passing: `FactoryView.Clear()` left the old mall's skimmer bots, pump arms and claw heads in the
  scene after a contract.

The bot (`Tools/BalanceSim/Bot.cs`):
- Aurelia: once two cannons stand, dig slots become pump → coin sorter → hopper lines, with 2 + (pump lines / 2)
  cannons (up to 10) on rings 14–18.5 m out. The Lagoon: slot lines (rig → slot machine → hopper). Skyport:
  bare borers where a whole line doesn't fit, the whole rim once four carousel lines run, and up to three
  carousel lines per bare intake at the back of the hall. Eternity: the Old Well takes a rim slot as soon as it's
  affordable, and the bot stops chasing common wishes it has already found.
- The report's "lines at the end" marks bare intakes `*` and slot lines `$` and counts cannons, carousel lines
  and the well's wishes; a new "this mall's machine" line says when each was researched and first built.
- `dotnet run -c Release --project Tools/BalanceSim -- machines` checks all four in the Core: sold only in their
  own mall (and back on its remodel), placement rules (range, facing, one well), and that each one works.

Balance: `fit --apply` with the machine-aware bot was still running when this was first committed; the fitted
crusts and the seed checks follow in the next commit. Unfitted (session 5's crusts, seed 1234) the machines
made Aurelia 3.18 h, Skyport 2.41 h, the Lagoon 2.10 h and Eternity 4.62 h: 19.4 h in all, so the re-fit is needed.

Tour and uitest (written, compile-checked, not run):
- Tour: 8 new shots (`40`–`47`, see NEXT), taken in each mall right after its overview shot
  (`GameRoot.MallMachineShots`). Watchdog 720 s.
- uitest: +25 checks after Head Office (`GameRoot.MallMachineChecks`): Neon Galaxy's terminal has no cannon;
  in Aurelia, Champagne Blasting is bought through the terminal, a cannon 23 m out is refused, one 16 m out is
  built through build mode facing the fountain, and it fires rinsed chunks; in Skyport the cannon is gone and
  the carousel is in the catalogue, built through build mode, and a drone flies a lineless borer's chunks to
  it; in the Lagoon a slot machine spins a rig's gunk and a jackpot sprays tokens; in Eternity the Old Well is
  built at the rim facing the fountain, a second one is refused, and an uncaught wish falls in and dissolves
  crust. Watchdog 780 s.

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
- Late malls still re-buy the early tree in minutes. M7 gives malls 3–6 a machine each; Neon Galaxy (mall 2)
  could get one too (an arcade claw that plays itself?), and the machines could get a second tech each once
  they've been played by hand.
- Remodel laps (after mall 6) aren't fitted; they reuse each mall's fitted shape with crust ×1.6 per lap. The
  mall machines come back on their mall's laps, so a lap plays like its mall did.

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
