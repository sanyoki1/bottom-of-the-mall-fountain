# HANDOFF — Wish Extractor

_Last updated 2026-09-28, end of session 5 (a Claude Code cloud session: Linux, .NET 10, no Unity)._

## NEXT (session 6): the View/UI for the new late game, then the Unity checks

Session 5 redesigned the late game in **Core** (Wonders, buried finds, frenzies, wishes that scale
with income) and re-fitted the balance. The Core compiles and runs in `Tools/BalanceSim`. **Nothing
new has a view yet**, and nothing on this branch has been compiled by Unity. Branch:
**`claude/gracious-feynman-izvxcg`** (`master` untouched). Do these in order:

1. **View/UI for Wonders, finds and frenzies** (the plan is below under "View/UI plan"). Pattern-match
   existing code carefully if you have no Unity (it can't be compiled here); rules in CLAUDE.md.
2. **Tour and uitest coverage** for the new systems (list below), then docs: DESIGN.md needs a
   "Late game" section (Wonders, finds, frenzies, wishes); README's systems paragraph.
3. **On the Windows PC:** build, `-autotour`, `-uitest -fresh` (expect 96 + the new checks), `-loadtest`;
   look at every screenshot. Then merge into `master` and curate `Docs/Screenshots`.

Until step 1 lands, a Unity build compiles (all Core changes are additive), but in play Wonders
can't be approved (no easel yet), so the guided objective chain stops at "Build the first stage of
the Penny Chandelier". Finds are plain loose items: picking one up opens it, but there's no prompt
and no feedback yet.

## What session 5 did

### The late-game redesign (Core, done)

- **Wonders** (`SimWonder.cs`, content in `ContentWonders.cs`): each mall has a three-stage
  megaproject hung from the skylight over the fountain: the Penny Chandelier, the Mirrorball of
  Tomorrow, the Chandelier of Unnecessary Diamonds, the Departures Mobile, the Wheel of Fountain
  Fortune, the Wishing Star.
  - Stages are TechDefs in `TechBranch.Wonder`: `OnlyMall`, and `Needs` = goods plus Wish Tokens.
    `CanAfford` checks the goods and `BuyTech` approves a stage.
  - Goods reaching **any hopper or the kiosk** while the next stage still wants them are set aside
    instead of sold (`TakeForWonder`, `S.wonderGoods`).
  - Needs push a different chain in each mall:
    - Crestview: crowd oddities, then rolls and relics;
    - Neon Galaxy: tokens and wish bricks;
    - Aurelia: rare relics and diamonds (keep them away from the melter);
    - Skyport: bags and pallets;
    - Lucky Lagoon: gold bars and pallets;
    - Eternity: everything.
  - Each stage grants a mall-only bonus (value, digging, tosses, wish life or value, relic rate,
    find rate, frenzy length).
  - Finishing all three adds the mall to `S.wonders`, a permanent perk applied in `Recalc`.
  - New objective `wonder1` after `relic`. The HUD goal card should show the Wonder (`GoalIsWonder`)
    once the guided chain reaches `wonder1`, `concrete` or `mall*`.
- **Buried finds** (`SimFinds.cs`, per-mall content in `ContentWonders.cs` → `Mall.FindTypes`,
  `ItemCat.Find`):
  - Spawning: once digging is past the loose layer, a find surfaces every 3–6 min (at most 2
    waiting), plus one guaranteed on each new stratum from the third down.
  - Who takes them: machines ignore them, the overflow never deletes them, and Chad goes straight
    for them (and shows up 3× sooner while one is waiting).
  - Picking one up **opens** it (`OpenFind`), paying one of:
    - a jackpot (1–2 min of steady income);
    - a coin burst (0.5–1 min, as coins spilled in the water);
    - a frenzy;
    - a swarm of 4–6 wishes;
    - a relic dropped in the water;
    - 10–30 tokens;
    - rarely, a Lucky Penny.
  - The first find is always a jackpot.
- **Frenzies**, 60 s each: Golden Hour (sell ×2), Crust Quake (dig ×3, folded into `DigMult`), Flash
  Sale (tosses ×3). They are folded into `EventMult`.
- **Wishes**:
  - Tossed wishes come at most every 8 s (`Balance.WishGap`, shortened by the "wish" event).
  - A wish also pays `WishIncomeSeconds` {0.5, 1, 3, 8, 30} × `SteadyIncome`. That is a 5-minute
    windowed average of hopper and kiosk income; windfalls are excluded so there's no feedback.
- **Coin Roller** also rolls the crust's coins and casino chips (not diamonds), so dig lines can feed
  roller → bagger → palletiser in every mall.
- New `TechKind`s (`FindRate`, `WishValue`, `FrenzyTime`), achievements (`find_1`, `find_50`,
  `frenzy_1`, `wonder_stage`, `wonder_1`, `wonder_6`), SaveData (`wonderGoods`, `wonders`,
  `findsOpened`, `frenzies`, `wonderStages`, `wonderItems`).

### Bugs found and fixed (they affected the M6 fit too)

- **The factory depended on tick length.**
  - A belt hopped one item per tick and a machine handed over one item per output port per tick.
    The bot ticks at 0.5 s, so its late dig lines moved ~4 items/s where the game at 60 fps moves
    ~20. The M6 fit therefore overstated late malls by several hours.
  - Fix: `UpdateFactory` now runs in steps of at most `Balance.FactoryStep` (1/30 s). This also
    keeps slow frames honest.
  - Also fixed: the crowd started only one toss per tick; now it starts every toss that's due.
- **Wish bricks double-counted the value multiplier.** The compressor stored the wish's cash value
  (already × ValueMult) on the brick, and the hopper multiplied again. Items must carry base value.
- **The roller rolled tossed diamonds.** It now takes only coin- and chip-shaped money.

### Bot and fitter

- The bot opens finds and approves Wonder stages when their goods are in (it keeps the Wish Tokens a
  stage needs).
- It builds what the Wonder needs:
  - melters on the coin lines (skipped while the Wonder wants diamonds);
  - roller, bagger and palletiser stages on dig lines, with the first lines stopping at rolls or
    bags while those are wanted;
  - a Wish Compressor with a hopper in the yard.
- Reports add: the Wonder stage times, what the next stage still wants, finds by kind, the lines
  built, value ×, processing ×, steady income. `BOT_SINKS=1` adds sink levels and item rates.
- **`fit` now judges each crust by the mean of three seeds** (`BOT_FIT_SEEDS`, default 1234,42,7);
  one seed's luck swung Crestview ±15%. The fit range goes up to 1e16 scoops.
- Levelled sinks (Crust Softener, Diamond-Tipped Bits, Pigeon Performance Bonuses, Gold-Plated
  Chutes, Donor Plaques) keep +10%/+7% at ×1.9 but now allow 400 levels, so late malls never run
  out of things to buy.

## Verified this session (measured with the bot)

- Fitted crusts (`<fitted-scoops>`): 2,583,300 · 366,894,100 · 1,131,659,300 · 3,490,524,100 ·
  4,893,765,200 · 8,594,697,700.
- **Engaged: 24.92 h** (seed 1234), **25.19 h** (42) and **25.10 h** (7).
  - Per mall (seed 1234): 2.83, 3.47, 4.29, 4.36, 5.08, 4.90 h.
- **Casual: 25.96 h** (3.85 / 3.90 / 4.01 / 4.45 / 5.07 / 4.68).
- Wonder stages spread across each mall (engaged, seed 1234):

  | Mall | Stage 1 | Stage 2 | Stage 3 | Mall length |
  |---|---|---|---|---|
  | Crestview | 1h27 | 2h05 | 2h39 | 2.83 h |
  | Neon Galaxy | 1h01 | 1h56 | 3h00 | 3.47 h |
  | Aurelia | 0h59 | 2h20 | 3h46 | 4.29 h |
  | Skyport | 1h11 | 2h37 | 3h49 | 4.36 h |
  | Lucky Lagoon | 0h45 | 2h53 | 4h07 | 5.08 h |
  | Eternity | 1h00 | 2h33 | 3h52 | 4.90 h |

- Late income: wishes 7–26% and finds 1–24% (they were ~0% before). Each late mall opens 50–90
  finds.
- Early milestones are unchanged on average but **vary a lot by seed**, before and after this
  session: bucket 12–22 min, skimmer 17–32 min, first line 37–57 min. The single-seed figures in
  earlier notes overstated the precision.
- `smoke`, `factory`, `crust` and `counts` run clean (tech nodes 103, achievements 59,
  objectives 33, items 233).

## View/UI plan (for session 6)

Core API to use: `Sim.WonderNext`, `WonderStagesDone`, `WonderStageCount`, `Wonder` (a `WonderDef`:
`Name`, `Desc`, `Crown`/`CrownColor`, `Charm`/`CharmColor`, `PerkText`, `Stages`),
`WonderHave(need)`, `WonderGoodsReady(i)`, `WonderNeedsLine(i, sep)`, `Sim.NeedName`,
`Sim.NeedHint`, `Sim.RewardText(kind, value)`, `GoalIsWonder`, `LastDepositDiverted`; events
`OnWonderGood(need, have, want, building-or-null)`, `OnWonderStage(tech)`, `OnWonderComplete(mall)`,
`OnFindUnearthed(item)`, `OnFindOpened(FindResult)`, `OnFrenzyChanged(frenzy, on)`; `IsFind(type)`,
`FindsWaiting`, `Frenzy`/`FrenzyRemaining`/`FrenzyLength`, `SteadyIncome`; debug `DebugSpawnFind`,
`DebugFrenzy`, `DebugFillWonder`.

1. **Wonder Plan easel** (a `FountainBoard`-style class):
   - Placement: at `Layout.WonderX/Z` ≈ (-2.8, -14.6); add it to `Layout.Obstacles` (r 0.9). It is
     clear of Doug's r = 13.5 patrol, the kiosk and the entrance path. Face it toward (1.5, -16).
   - Text: Wonder name, "STAGE n OF 3: <name>", `WonderNeedsLine(i, "\n")`, the reward
     (`RewardText`), and READY / the fee.
   - Interaction: collider plus a `ClickTarget` of kind `"wonder"` → new `TargetKind.Wonder` in
     GameView.
   - `Use()`: if the goods aren't in, message what's missing; if the fee is short, message it;
     otherwise `BuyTech(WonderNext)`.
   - HUD prompt for it.
2. **The Wonder over the fountain** (`WonderView`, synced like `FountainDecor`), per completed stage:
   - Stage 1 is the frame: a torus ring of r ≈ 4.5 at y ≈ 13 plus a smaller ring, on four cables
     to the ceiling (y = 18).
   - Stage 2 is the dressing: ~24 `Loot.MakeItemMesh(Charm)` charms on strings, swaying, plus
     glowing bulbs (vertex alpha > 0).
   - Stage 3 is the crown: a big `MakeItemMesh(Crown, …, ~14)` at y ≈ 11.5, turning slowly, with a
     glow halo.
   - Keep it clear of the wormhole portal (0, 7.2, 0) and the light-show beams (y 17.5). Confetti
     on each stage.
   - Remember: vertex alpha = emission; use `MeshKit.Hex`.
3. **HUD**:
   - Goal card: when `GoalIsWonder`, kicker "WONDER", text "<Name>: <stage> (n/3)", hint
     `WonderNeedsLine` plus "· approve at the Wonder Plan easel".
   - A frenzy chip under the event chip: name and seconds left.
   - Item prompt for finds ("[E] Open the …") and the goldfish ("[E] Put the goldfish back").
   - Exclude `TechBranch.Wonder` from the terminal's "N upgrades affordable".
   - Receipt line "SET ASIDE FOR THE WONDER: n" when `LastDepositDiverted > 0`.
4. **Feedback** (GameRoot/GameView):
   - Finds: a beacon or glint on waiting finds, always (not only with the detector); ripple, sfx and
     toast on unearthing. On opening, a banner or toast from `FindResult.Title/Detail` plus a float
     text.
   - Frenzies: a banner on start.
   - Wonder goods: a toast when a need completes; an occasional float "+1 → Wonder" at the hopper
     (`FactoryView.WorldCenter(from)`) or kiosk.
   - Stages: a banner per stage with its reward; a big banner with the perk on completion.
   - Add the new `TechKind`s to `TerminalPanel.EffectText`.
5. **Tour and uitest**:
   - Tour shots: a find waiting and opened, a frenzy chip, the easel, the Wonder after each stage
     (`DebugFillWonder` + approve).
   - uitest: aim + E opens a find (`findsOpened` goes up); aim + E on the easel approves a stage
     (`WonderStagesDone` goes up); the goal card shows the Wonder; the frenzy chip shows.
   - Dev keys: F9 spawn a find, F10 fill the Wonder.

## Open design issues (for Nico)

1. **Pallets stall income.** A pallet is 40 bags (40,000 coins), about 28 min of one dig line.
   While the Wonder wants pallets the bot palletises every line, so income waits inside half-built
   pallets. That's why Eternity shows a 1h24m purchase gap. Options: `PalletSize` 10–20 (update
   the pallet's `Units`) and re-tune the pallet needs; or palletise only on some lines. Re-fit
   afterwards.
2. **Casual ≈ engaged (1.04×).** After the first hour the factory does nearly everything. Finds and
   wishes reward engagement, but the casual bot still opens 60% of finds and catches 45% of wishes.
3. **Big late numbers.** With the factory running at its true speed, Eternity ends near $40Qa earned
   (M6: $80T). The knobs are the same as before (sink effect and growth, target hours).
4. **Wonder fees** are set to a few minutes of the expected income at each stage, so the goods are
   the real gate. Revisit once people play it.
5. The bot fits only 5 dig lines; a player who fits more goes faster than the bot.
6. Old saves: no migration issues (new fields default to empty), but crusts changed size again.
7. No offline progress in v2 (`lastSaveUnix` is saved but unused).

## Lessons (also in CLAUDE.md)

- The player is on Ignore Raycast; wishes and Chad are triggers on layer 4 (Water).
- A zero-scale lit mesh makes NaN pixels; `TextMesh` colours need alpha 1; `WE/Glow` can't draw matte
  meshes (ghosts use `WE/Ghost`).
- Levelled multipliers compound: keep the sum of log(effect)/log(cost growth) over the nodes being
  bought well below 1; use several steep sinks in parallel rather than one gentle one.
- Never loop per scoop or per item over quantities that grow with the economy.
- **Nothing may depend on tick length.** The sim ticks at 0.5 s and the game at ~1/60 s. Anything that
  moves "one per tick" (belts, ports, splitters, wind-ups) must be sub-stepped or loop until done.
- **Items carry base value.** Multipliers are applied once, when something is sold. Never store an
  already-multiplied value on an item (the brick bug).
- Anything that scales with income must not feed back into the income it measures (steady income
  excludes windfalls and uses a 5-minute window).

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
rival diver, the goldfish, the Security branch; footsteps and the whistle in `Synth`; the balance bot
and fitter; an economy rework (rigs throttle to their output, per-mall value scale, cheaper first
hour, five parallel sinks); tour and uitest coverage of the hazards; docs rewritten for the
first-person game. The tour/uitest additions (96 checks expected) are still unverified in Unity.

**Session 5 (M7 in progress):** the late-game redesign in Core (Wonders, finds, frenzies, wishes that
scale with income), the factory tick-length fix, the brick fix, a three-seed re-fit. The View/UI is
next.
