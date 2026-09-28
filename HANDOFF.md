# HANDOFF — Wish Extractor

_Last updated 2026-09-28, end of session 4 (a Claude Code cloud session: Linux, .NET 10, no Unity)._

## NEXT: run the Unity checks on the M6 branch, then merge it

M6 is written and pushed to branch **`claude/gracious-feynman-izvxcg`**, on top of `4a99037` (the M6 WIP
commit). `master` is untouched. The balance work is verified with the bot. The Unity side is **not**:
this session had no Unity, so nothing on the branch has been compiled by Unity or run as a player.

On the Windows PC:

1. `git fetch origin` and `git checkout claude/gracious-feynman-izvxcg`.
2. Build (CLAUDE.md step 1). Core changed (`SimCrust.cs`, content files) and compiles in the .NET sim
   with C# 9; the new tour/uitest code in `GameRoot.cs` was written against the existing APIs but never
   compiled. Fix any `error CS` first.
3. `-autotour` (51 screenshots). New shots to look at: `07j2_terminal_security`, `07q_guard_warning`,
   `07r_rival`, `07s_rival_fleeing`, `07t_goldfish`, `07u_goldfish_returned`, plus the
   `[TOUR] hazards: ...` log line. Also check the factory and processing shots, since prices and rig
   behaviour changed (below).
4. `-uitest -fresh`: expect **`[UITEST] done: 96 passed, 0 failed`** (85 before; 11 new checks for the
   Security tab, Officer Doug, Chad and the goldfish). If a new check fails, the likely causes are: the
   goldfish check aims at a coin lying in front of the fish; Chad wandered more than 7 m from the rim
   before the E check (the aim SphereCast reaches `Balance.WishReach` = 7 m); or the approach helper got
   stuck on the rim (it enters over the nearest stepping stone, like `EnterFountain`).
5. `-loadtest` on the uitest save.
6. If all pass: merge the branch into `master` (or ask Claude to open a PR) and replace the v1 images in
   `Docs/Screenshots/` with tour shots (the README no longer links them).

## State

- M1–M5: first-person slice, crowd and wishes, Maintenance Terminal, factory, crust and processing.
  All verified in Unity and committed (`f37e470` … `053f424`).
- M6 (hazards, balance bot, economy, docs): hazards and content were verified in Unity at WIP stage
  (uitest 85/85 then). The economy rework, the new sinks and the tour/uitest additions are unverified in
  Unity (see NEXT).
- Commits on the branch: `b0d4ea8` (economy + bot), then the tour/uitest additions and these docs.

## Verified this session (measured, not assumed)

- `Tools/BalanceSim` builds and runs against the real Core (.NET 10, C# 9).
- Engaged bot, fitted content: **24.84 h** (seed 1234), 25.09 h (seed 42), 25.16 h (seed 7). Per mall
  (seed 1234): Crestview 2.76 h, Neon Galaxy 3.82, Aurelia 3.76, Skyport 4.46, Lucky Lagoon 5.01,
  Eternity 5.03. Casual bot: **30.67 h** (4.03 / 4.48 / 4.48 / 5.34 / 6.06 / 6.28).
- Early pacing (engaged, Crestview) meets PIVOT_FPS: scrub 6 s, cup 12 s, pail 22 s, jets 35 s,
  **bucket 11m38s**, hamster research 1m51s, **skimmer 17m03s**, **first skimmer line 36m51s**, lights
  47 min, tumbler 1h03m, neon + cherub + koi 1h13m, pigeons 1h14m, diesel 1h17m, fanny pack 1h25m.
- Longest gap between purchases (engaged): 5m35s, 3m49s, 6m45s, 9m40s, 11m10s, 13m58s (the last three
  at the very end of their malls).
- The sim's system checks (`smoke`, `factory`, `crust`, `counts`) run clean.

## What changed in session 4 (the economy rework)

The unfitted WIP gave 11.1 h, and crust fitting alone couldn't fix it. Mall time barely depended on
crust size: later malls owned every tech within an hour, then sat at a fixed dig rate with hundreds of
millions in unspendable cash. The first hour was also far too slow (first line at 1h39m). Changes:

- **Digging.** Rigs now throttle to their output buffer instead of discarding chunks that don't fit.
  That was lossy and tick-length dependent, so the sim's 0.5 s ticks overstated rigs versus 60 fps.
  Chunk size = 1.6 scoops × dig multiplier, so dig upgrades raise depth and income together.
  `DigCrust` works per stratum instead of per scoop (6× faster sim, and safe at millions of scoops/s),
  and a hand swing drops at most 24 heavier chunks. `Balance.CrustDensity = 5` makes dug gunk worth
  processing.
- **Per-mall scale.** `ValueScale` 1, 1.5, 2.5, 4, 6.5, 10, with loot, wish and relic values divided back
  in `BuildMalls`. Each mall is Crestview's economy at a bigger scale; Head Office perks make later
  malls faster. Seniority is now +8%/level, Excavation Grant +10%/level.
- **The first hour.** 320 seed coins (some loonies, ~$21). Bucket $10. Cheaper first line: hamster $6
  + $2, skimmer $10 + $6, belts $5, hopper $12 + $8. Tumbler $30 + $25, pigeon sorter $60 + $50, diesel
  $100 + $60, dig rig $300 + $250, neon $50.
- **Growth control.** Coin Polish ×2.2 and Stronger Shoulders ×2.5 per level. Five late sinks, 60 levels
  each at ×1.9: Crust Softener (Tools, +10% dig, needs the jackhammer), Diamond-Tipped Bits (Intake,
  +10% dig, needs the borer), Gold-Plated Chutes (Logistics, +7% value, needs the armoured hopper),
  Pigeon Performance Bonuses (Processing, +7% value, needs the coin sorter), Donor Plaques (Fountain,
  +7% value, needs the golden statue).
- **Fitted crusts** (`<fitted-scoops>`): 203,300 · 8,700,400 · 29,663,400 · 117,892,300 · 235,027,100 ·
  546,179,600 scoops.
- **Bot:** shops like a player (saves for the next one-off unlock, grabs levelled nodes costing ≤ 25% of
  it; tokens and Lucky Pennies are separate budgets). Coin lines wash and sort hand-dug gunk. It logs
  income and spend splits, dig rate, rigs and multipliers, and the longest gap between purchases.
  Fitter: search range up to 1e12 scoops, `Math.Round(x, -2)` crash fixed. New: `malls`, `techs`,
  `BOT_CRUST`, `BOT_MALLS`.

## Open design issues (for Nico)

1. **Late-game content.** From Aurelia on, the Head Office head start buys the whole fixed tree in
   1–1.5 h. The rest of each mall (2.5–3.5 h in malls 4–6) is the five sinks while the crust sinks.
   It has no dead stretches, but it's repetitive. More late machines or tiers, or per-mall mechanics,
   would give that phase something to build. The fitter will then shrink the crusts.
2. **Big late numbers.** Steady purchases mean steady exponential growth: Eternity ends near $1T/min
   and $80T earned. Knobs: the sinks' effect per level (+7–10%) and growth (×1.9), and the malls'
   target hours (shorter late malls mean smaller numbers).
3. **The bot fits only 5 dig lines** (its planner runs straight radial lines; many of its 16 slots
   collide). A player who fits more lines will go faster than the bot.
4. **Casual is only ~1.25× engaged**, because the factory does most of the work after the first hour.
5. **Saves from before this branch** keep their `dug` count while the crusts got much bigger, so a
   mid-mall save appears shallower. Fine for a dev build.
6. **No offline progress** in v2 (`lastSaveUnix` is saved but unused). v1 had 2 h at 50%.

## Lessons (also in CLAUDE.md)

- The player is on Ignore Raycast; wishes and Chad are triggers on layer 4 (Water).
- A zero-scale lit mesh makes NaN pixels; `TextMesh` colours need alpha 1; `WE/Glow` can't draw matte
  meshes (ghosts use `WE/Ghost`).
- Levelled multipliers compound: keep the sum of log(effect)/log(cost growth) over the nodes being
  bought well below 1; use several steep sinks in parallel rather than one gentle one.
- Never loop per scoop or per item over quantities that grow with the economy.
- Rig behaviour must not depend on tick length (the sim ticks at 0.5 s, the game at ~1/60 s).

## Milestone notes (history)

**M1:** first-person controller (`FirstPerson.cs`, injectable `FPInput`), view-model hands, instanced
loose items, COIN-O-MATIC 3000 kiosk, `WE/Water`, south wall, skylight ceiling and colliders, FP HUD,
Core `Sim` v2 (loose items, carry, deposit, tech levels), SaveData v2.

**M2:** the shopper crowd (`SimCrowd.cs`: 10 archetypes, doors in `Layout.cs`, stand at r = 10.7,
tosses along the tier curve, barks and wish quotes), True Wishes rising where tosses land, the Fountain
Improvement Plan easel, the first three beautification upgrades with visuals, speech bubbles,
positional splash and plop audio.

**M3:** the Maintenance Terminal (`Terminal.cs` prop, `TerminalPanel.cs` = MAINT-OS 95: branch tabs,
node graph by `Col`/`Row`, detail pane with the generated effect line), carry and grab ladders,
Longer Arms, Nimble Fingers, Sturdier Bottoms, Comfy Sneakers, fountain uniques and levelled nodes,
held tool models, area grabs, the shop-vac's auto pickup, detector glints, all fountain decor, wormhole
tosses.

**M4:** the factory. Core `SimFactory.cs`: 1 m grid, rim-only intakes that must face the fountain,
belts with 0.25-spaced items, splitter, skimmer bot, drain pump, claw crane, hoppers, one global power
budget, save/load of buildings, buffers and belt items. `ContentBuild.cs`: buildables and their tech
nodes. View: `FactoryView.cs`, `BuildMode.cs` (grid ghost with `WE/Ghost`, R, auto-facing, belt drag,
X demolish), `BuildMenu.cs` (Tab catalogue).

**M5:** the crust (`SimCrust.cs`): digging by hand (hotbar 2) and with rigs, strata and depth mapping
driving `FountainView` (the crust sinks, water follows, colliders rebuild, a scaffold ramp spirals down),
processors (tumbler, pigeon sorter, coin sorter, roller, bagger, palletiser, melter, compressor), relics
from sorting, mall events (Jackpot Hour rains gold coins), bare concrete → treasure → contract →
`Prestige()` (Lucky Pennies, Head Office perks in `S.headOffice`).

**M6:** hazards (`SimHazards.cs`, `View/Hazards.cs`): Officer Doug's patrol, whistle and fine, Chad the
rival diver, the goldfish, the Security branch; footsteps and the whistle in `Synth`; more receipt
jokes; the balance bot and fitter; the economy rework above; tour and uitest coverage of the hazards;
docs rewritten for the first-person game.
