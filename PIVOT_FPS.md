# PIVOT_FPS — Wish Extractor becomes a first-person fountain factory

_Written 2026-09-27 at Nico's request. Next session implements this; read CLAUDE.md and HANDOFF.md first._

## What Nico asked for (verbatim intent)

- A **first-person game like Find The Needle**, not an auto-clicker.
- Start by **picking up and carrying one penny at a time**; buy equipment that **carries more** so you **sell more per trip**.
- A **progression/tech tree** unlocking better equipment, then **generators, machines and conveyor belts** to build **assembly lines** that automate extraction.
- **A place to deposit pennies.**
- Twist vs Find The Needle: **NPC shoppers throw coins into the fountain.** **Fountain upgrades / beautification** make NPCs throw **more often** and **better stuff**: nickels, dimes, quarters, dollars, gold pieces, diamonds, then ridiculous things. **Humour throughout.**
- Creative liberty to add a twist.

Reference (Find The Needle, Steam 2026): first person; start by hand with a metal detector; shovels, buckets, wheelbarrows, pitchforks, vacuums; then conveyor belts, robots, drones, sorters, processors; 300+ upgrade tech tree; raw hay sells cheap, processed products sell more; phases Bare Hands → Hand Tools → Vacuum + early conveyors → robot arms/drones full factory.

## Core loop (v2)

1. **NPCs toss** coins/items into the fountain water (live, visible, with a wish speech bubble). Coins sink and settle on the basin floor. The basin also still holds the old **crust** (finite, layered, richer the deeper you dig) from v1.
2. **You collect** in first person: look at a coin → click/E to pick up (hands hold 1). Carry limit is the key stat.
3. **You deposit** at the **Coin Deposit** (a grimy coin-counting kiosk "COIN-O-MATIC 3000" in the custodial corner). Deposit = cash. Cha-ching, receipt pops out with a joke line.
4. **You spend** cash at the **Maintenance Terminal** (laptop on a folding table) = the tech tree.
5. **You automate**: place intake machines at the fountain edge, conveyors to processors, processors to the deposit. Generators power them.
6. **You beautify** the fountain → more NPCs, more throws, better tiers → more to automate → bigger machines.

Finite depth goal (keep v1's strength): digging the crust to bare concrete finds the bottom treasure and lets you sign with the next mall (prestige), same six malls/themes/wishes/relics as v1.

## Carry equipment ladder (per-trip capacity)

Bare hands 1 → Paper cup 5 → Sand pail 20 → Mop bucket 60 → Fanny pack of holding 150 → Wheelbarrow 500 (slows walk) → Shopping cart 2,000 (push, can't jump) → Golf cart / floor scrubber 10,000 (drivable) → Personal shop-vac backpack (auto-pickup radius) → Grabber claw / magnet glove (pick range, pick rate).
Pickup tools separately: fingers → litter grabber (range) → pool skimmer net (area scoop in water) → shovel (crust chunks) → metal detector (highlights rare items through water/crust) → jackhammer (crust) → leaf-blower-in-reverse (suction cone).

## Automation (build mode, grid snap around the fountain)

Power: Hamster wheel (joke, tiny) → Diesel generator → Food-court fryer-oil generator → Solar skylight → Wish reactor (burns uncaught wishes).
Intake: Pool skimmer bot (floating Roomba) → Fountain drain pump → Dredge crane / claw machine → Crust jackhammer rig → Borer.
Transport: Conveyor (straight/turn/merge/split, tiers by speed), lift, chute, pneumatic mall tube (late).
Processing (raises value, like hay → bales): Rinse tumbler (wet sticky coins → clean) → Pigeon sorter → Coin sorter (by denomination) → Coin roller/wrapper (rolls sell ×) → Bagger/palletiser → Melter (gold bars) → Wish compressor (bricks of nostalgia) → Appraiser bot (relics).
Output: Deposit hoppers anywhere once "Wired Deposit" is researched; armored truck pickup late.
Keep it simple: items on belts are pooled GameObjects/instanced meshes, each machine has input/output buffers and a rate; simulation ticks on data, visuals follow (see Tech).

## Fountain beautification (the twist) — "Wishability"

Each upgrade raises a **Wishability** score → NPC spawn rate, throw rate, crowd size, and **coin tier weights**.
Tiers: penny → nickel → dime → quarter → loonie/toonie → dollar coin → silver dollar → gold piece → diamond → ridiculous (car keys, dentures, a wedding ring mid-argument, an entire phone mid-call, bowling ball, timeshare contract, a live goldfish (must be returned to the fountain, free rep), a crypto seed phrase on a napkin, a trophy, a toaster, a Beanie Baby, a vending machine (thrown by a bodybuilder)).
Upgrades: scrub the grime → fix the water jets → coloured lights → "MAKE A WISH" neon sign → cherub statue → koi → mood music → lucky-penny dispenser (sell coins to NPCs to throw!) → influencer photo spot → golden statue → fountain light show → "Official Wishing Fountain" certification → wormhole to other malls' fountains.
NPC archetypes (humour + mechanics): mall walker (steady pennies), teen (throws gum, sometimes quarters), influencer (only throws when filming; drops phone sometimes), businessman (tosses a whole wallet), grandma (dentures), toddler (shoes), proposer (ring; if proposal fails, throws it harder), tourist (foreign coins), billionaire (gold, rare), security guard (fines you if you wade during mall hours — optional hazard), competitor fountain-diver rival NPC who steals coins (chase him off).
NPC wish bubbles reuse v1's 107 wishes; catching a wish orb in first person still works (it rises off the water after big tosses).

## Tech tree

Node graph UI on the terminal: branches Carry, Tools, Power, Intake, Logistics, Processing, Fountain, Mall/Prestige. Nodes cost cash (early) and later **Wish Tokens** (from caught wishes / compressor) so beautification and automation compete. Target 150–300 nodes (per-tier speed/capacity levels are cheap to author as data).

## Pacing target

Keep ≥ 24 h to finish six malls (Nico's requirement). First 10 minutes: hands + cup + bucket, first beautification, first generator + skimmer bot by ~15–20 min, first full line by ~45 min. Re-use the BalanceSim approach: rewrite the bot for the new rules (throughput-based), fit per-mall depth/targets.

## Tech plan / reuse

Reuse: Core content (malls, strata, wishes, relics, themes, achievements), MeshKit/TexKit/Mats/shaders/BloomFX, WorldBuilder (mall hall), FountainView (basin, crust heightfield; make crust diggable in FP and add water surface), MachineVisuals models (adapt as placeable machines), Actors (people → NPCs, add walk/throw animation), Synth/AudioHub, UIKit, SaveSystem, test harness style (-autotour, -uitest, -loadtest).
Replace: Sim pipeline (abstract rates) → factory sim (entities: coins in world, items on belts, machine buffers, power network); CameraRig → FirstPersonController (CharacterController, mouse look, head bob, interact raycast); HUD → FPS HUD (crosshair, carry meter, cash, interact prompts, build hotbar); ShopPanel → tech tree terminal + build menu.
New systems: interaction/pickup, inventory/carry, deposit, NPC crowd (spawn, path around fountain, throw arcs, wish bubbles), coin physics (cheap: ballistic arc → sink → rest; no rigidbody per coin beyond a small active pool; settled coins become instanced data), build mode (grid ghost preview, rotate R, delete, power/belt connection validation), factory tick, beautification, tech tree data + UI.
Performance: thousands of coins → instanced rendering (Graphics.RenderMeshInstanced) from data arrays; only nearby/active ones are colliders.

## Milestones (each ends with a build + screenshots + tests)

1. FP controller in the existing mall/fountain; pick up coins by hand; carry limit; deposit kiosk pays; save/load. (Playable vertical slice.)
2. NPC crowd throwing coins with wish bubbles; coin settling and instanced rendering; first 3 beautification upgrades changing spawn/tier weights.
3. Tech tree terminal + carry/tool ladder.
4. Build mode: generators, conveyors, skimmer/pump intake, deposit hopper; factory tick; power.
5. Processing chain + crust digging/strata/prestige to next mall (port v1 content).
6. Humour/juice pass (NPC archetypes, receipts, barks, sounds), balance bot + fit to ≥ 24 h, docs.

## Decisions (confirmed by Nico, 2026-09-27; do not re-ask)

- **Replace v1.** v1 lives on only in git history (baseline commit `96a8f81`).
- **Single player.**
- **Hazards always on**: security guard and rival diver, no settings toggle. Keep them light and funny.
- **Git**: this folder is now its own nested repo (branch `master`). Nico authorized **one commit after each
  verified milestone** (build + tour + uitest pass). WIP or unverified states still need his OK.

## Spec decided in session 2 (build on this, don't re-derive)

- **Modest numbers.** No v1-style ×1e4 cost scale per mall. `MallDef.ValueScale` (default 1) stays as a
  tunable for the balance bot. Malls differ by crust size (`CrustScoops`), content and Head Office perks.
- **Tosses**: a global coin ladder: penny, nickel, dime, quarter, loonie, toonie, silver dollar, gold coin,
  diamond, each unlocked at a Wishability threshold. Then 19 oddities (`ContentWorld.cs`). The tier weight
  is a Gaussian around mu = Wishability × `TierPerWish` (+ NPC archetype bias).
- **Crust = the finite goal per mall.** Stratum 0 is loose: shovelling it yields coins directly. Deeper
  strata yield gunk chunks. Gunk deposits at 10% of value, rinsed ("washed") at 35%, sorted at 100%. This is
  v1's Dredge → Dissolve → Sort → Sell made physical. Relics come from sorting.
- **Item values**: each item stores its base value (stratum and mall included) when it's created.
  Multipliers apply at deposit.
- **Carry capacity counts physical items.** Bundles (roll = 50 coins, bag = 1,000, pallet = 40,000) let carry
  and belts scale.
- **Two tool ladders**: grab tools (reach, area, rate) and dig tools (scoops per swing). Hotbar: [1] grab,
  [2] dig, [3] build (M4).
- **Layout**: player spawns at (0, 0, −18) facing the fountain. COIN-O-MATIC 3000 kiosk at (−6.5, 0, −15).
  Maintenance Terminal (laptop on a folding table) at (6.5, 0, −15).
- **v1's hall has no south wall and no ceiling** (the camera was overhead). FP needs both: a south wall with
  storefronts and the mall entrance, and a skylight ceiling with shadows off so the sun still lights the hall.
- **Colliders** (v1 had none): floor = MeshCollider of the grout annulus; rim and basin wall; coarse crust
  heightfield MeshCollider rebuilt when the depth changes; capsule for the centrepiece; boxes for walls,
  planters, benches, balcony, escalator ramp and props.
- **Water** is a shallow layer (0.4 m) that follows the crust down. It needs a new transparent `WE/Water`
  shader. Wading multiplies speed by 0.72.
- **Getting in and out**: stepping stones outside the rim, and a spiral scaffold ramp along the inner wall
  that lengthens as the crust sinks.
- **Coins read at about 10 cm** in first person (Loot mesh scale 0.36–0.6).
- **Loose items live in Core** (x, z, and an airborne → sinking → resting state driven by timers). The view
  derives heights from `FountainView.HeightAt`.
- **Rendering**: `Graphics.RenderMeshInstanced` per item type. Materials are created at runtime, so set
  `m_InstancingStripping: 2` (Keep All) in `ProjectSettings/GraphicsSettings.asset`, and check that `WE/Lit`
  gets instancing variants (read the player log for errors).
- **Factory (M4) lives in Core** as a pure-data grid sim: 1 m cells, belts with a direction, machines with
  footprints, ports and buffers, and a global power budget. That way BalanceSim can run it.
- **First-person input** goes through an injectable input struct, so `-uitest` can walk, aim and interact
  through the real code path. The project uses the legacy Input Manager (`activeInputHandler: 0`).
