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

## Decisions to confirm with Nico at session start

- Keep v1 (auto-clicker) playable as a mode, or replace it? (Recommend: replace; keep code in git history.)
- Co-op? (Recommend: no, single player.)
- Hazards (security guard, rival diver)? (Recommend: yes, light, toggleable.)
