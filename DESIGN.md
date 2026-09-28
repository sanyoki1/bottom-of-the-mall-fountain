# Wish Extractor — Design

A first-person fountain factory game. The progression arc follows *Find The Needle* (bare hands → hand tools →
bigger containers → conveyors, robots and processors until it runs itself); the twist is the crowd: shoppers
throw things into your fountain, and making the fountain fancier makes them throw more, and fancier, things.
`PIVOT_FPS.md` records Nico's decisions and the milestone plan; this file describes what the game does.

## Pillars

1. **Carry more per trip.** One penny in your bare hands, then a paper cup, a sand pail, a mop bucket … a
   walking coin silo. Then machines carry it for you.
2. **The fountain gets fancier, the tosses get sillier.** Beautification raises *wishability*: a bigger
   crowd, faster tosses, better coins, and eventually dentures, timeshares and vending machines.
3. **A finite goal per mall.** Under the water is forty years of crust. Dig it to bare concrete, find the
   bottom treasure, sign with the next (deeper, richer) mall.
4. **Humour everywhere.** Receipt footers, shopper barks, 107 wish texts, relics, a jobsworth security guard,
   a rival diver in a wetsuit, and a goldfish that must go back in the water.

## Core loop

1. **Collect.** Coins land in the water and settle on the crust. Look at one, press E. The container decides
   how many you can hold; grab tools add reach and area.
2. **Deposit.** Walk them to the COIN-O-MATIC 3000 by the entrance. Cash, a ka-ching and a receipt joke.
3. **Spend.** The Fountain Improvement Plan (an easel by the rim) sells the next beautification; the
   Maintenance Terminal (a laptop running MAINT-OS 95) sells everything else.
4. **Automate.** Build mode (3): generators, intakes at the rim, belts, processors, deposit hoppers.
5. **Dig.** Hand tools (2) and dig rigs break the crust. The top layer is loose change; below it everything is
   glued into gunk that has to be washed and sorted before it's worth much.
6. **Bare concrete → the next contract.** Lucky Pennies buy Head Office perks that last forever; cash, techs
   and the factory start over in the next mall.

## Systems

- **Carrying** — 10 containers (hands 1 → cup 5 → pail 20 → bucket 60 → fanny pack 150 → wheelbarrow 500 →
  shopping cart 2,000 → floor scrubber 10,000 → shop-vac 40,000 with auto-pickup → hopper suit 250,000).
  Heavy oddities take two slots. Bundles (rolls, bags, pallets) let one slot hold many coins.
- **Tools** — 7 grab tools (reach, area, grab rate: fingers → litter grabber → net → rake → detector magnet,
  which makes valuables glint → reverse leaf blower → magnet glove) and 6 dig tools (sandbox shovel → handheld
  borer). Hotbar: 1 grab, 2 dig, 3 build.
- **The crowd** — 10 archetypes (mall walker, teen, grandma, toddler, businessman, tourist, influencer,
  heartbroken proposer, bodybuilder, billionaire) unlock with wishability. Each toss picks a coin tier from a
  Gaussian around `wishability × 0.08` (9 tiers: penny … diamond) or one of 20 oddities. Tosses sometimes carry
  a **True Wish**: it rises where the coin lands; catch it (E) for cash, Wish Tokens and a journal entry.
- **The crust** — 8 named layers per mall (48 in all). The loose top layer comes up as coins; deeper layers
  break into gunk chunks worth 10% as they are, 35% washed (tumbler) and 100% sorted (pigeons or the coin
  sorter). Sorting sometimes turns up a **relic** (72, 12 per mall). The water follows the crust down and a
  scaffold ramp spirals down the wall. Rubble matters: dig rigs stop when their output is full, and hand
  digging stops while 300 gunk chunks are lying around or in your hands.
- **The factory** — 21 buildables on a 1 m grid: 4 generators (hamster wheel … skylight solar), 3 coin
  intakes (skimmer bot, drain pump, claw crane), 2 dig intakes (jackhammer rig, tunnel borer), 8 processors
  (tumbler, pigeon sorter, coin sorter, roller, bagger, palletiser, gold melter, wish compressor), belts,
  splitters and 2 deposit hoppers. Intakes must stand at the rim facing the fountain. One power budget:
  short on power, everything slows by the same ratio. The simulation steps at a fixed 60 Hz.
- **The tech tree** — 81 nodes in 9 branches (Carry, Tools, Fountain, Power, Intake, Logistics, Processing,
  Security, Head Office), paid in cash, Wish Tokens or Lucky Pennies. Levelled nodes repeat at growing prices;
  **Bigger Chunks** never maxes out (each level makes chunks 25% of their original size bigger, at a
  polynomial price), so cash always has something to speed up the dig.
- **Hazards (always on)** — Officer Doug patrols the plaza and now and then catches you wading: step out
  within five seconds or pay a fine (4% of cash). Chad the rival diver shows up every 5–9 minutes and pockets
  the richest loose items until you get close or press E on him; he drops everything when he runs. The
  Security branch softens both.
- **Wishability** — 13 one-off fountain upgrades (scrub the grime → wormhole to other fountains, some paid in
  Wish Tokens), plus levelled tile polish, free mints, coin polish and wish-catcher's patience.
- **Mall events** — one per mall, every 7–13 minutes for a minute (Mall Walker Rush Hour, Neon Hour, Black Card
  Hour, Exchange Rate Spike, Jackpot Hour with gold coins raining in, Wishing Hour).
- **Goals and collections** — a 32-step objective chain (a tutorial through the first mall, then one per
  mall), 53 achievements (+1% deposit value each), the Wish Journal (+1% per wish found), relic sets (+10%
  each) and bottom treasures (+25% each).
- **Head Office** — clearing a mall pays Lucky Pennies (6 → 200). 8 perks: seed money, a bigger starting
  container, the basic factory pre-researched, seniority (value), sneakers, wishful thinking, relic radar,
  an excavation grant.
- **Remodel contracts** — after Eternity Plaza every mall repeats, values ×3 and crust ×1.6 per lap.

## The six malls

| # | Mall | Depth | Event | Bottom treasure |
|---|---|---|---|---|
| 1 | Crestview Commons | 30 ft | Mall Walker Rush Hour (tosses ×3) | The Founder's Penny |
| 2 | Neon Galaxy Mega-Mall | 45 ft | Neon Hour (wishes ×3) | Golden Arcade Token #0001 |
| 3 | Galleria Aurelia | 60 ft | Black Card Hour (relics ×4) | The Platinum Membership Card |
| 4 | Skyport Terminal C | 75 ft | Exchange Rate Spike (value ×2.5) | The Lost Passport of Everyone |
| 5 | The Lucky Lagoon | 90 ft | Jackpot Hour (gold coins rain in) | The Lucky Die |
| 6 | Eternity Plaza | 120 ft | Wishing Hour (everything ×2) | The First Wish (ending) |

Each mall has its own theme, storefront signs, music loop, loot table, 17–22 wishes and 12 relics.

## Economy (how 24+ hours is guaranteed)

- **Same prices everywhere.** Prices don't scale per mall (numbers stay modest early on). Later malls are
  richer (loot, permanent bonuses, Head Office), so their early game flies by; they are *deeper* instead.
- **Fitted crusts.** `Tools/BalanceSim -- fit --apply` bisects each mall's crust size until the engaged bot
  clears it in the planned hours (3.25, 3.75, 4.25, 4.75, 5.25, 5.25 = 26.5 h), and moves the layer boundaries
  so the bot spends about as long in every layer below the loose one. Inside a layer, the depth gauge follows
  the square root of the scoops dug.
- **Throughput, not multipliers, sets the pace.** Rigs only dig what their line can take, hand digging is
  capped by rubble, and the factory ticks at a fixed step, so a mall takes as long as its factory needs.
- **Something to buy all the way down.** Bigger Chunks (polynomial price, linear effect) keeps cash useful
  after the rest of the tree is bought, without the runaway growth an exponential sink causes in rich malls.
- **Measured pacing** (engaged bot, seed 1234, a full run on the current fit): Crestview 3.18 h · Neon Galaxy
  3.95 h · Galleria 4.15 h · Skyport 4.80 h · Lucky Lagoon 5.31 h · Eternity 5.19 h = **26.58 h**. Seeds 1–5
  spread 26.24–27.02 h; the casual bot took 27.74 h. Longest gap between purchases: 20–24 min in Crestview
  (around 2h15), 13–14 min in Neon Galaxy, under 8 min from the third mall on.
- **Early game** (Crestview, engaged bot, median of six seeds): cup and first beautification in under 30 s,
  bucket ~7 min, hamster wheel ~10 min, skimmer ~21 min, first skimmer line ~37 min.

## Presentation

- **All art is procedural** (no asset files): low-poly meshes with vertex colours, where vertex alpha is an
  emission mask (neon, screens and LEDs glow and bloom). Built-in render pipeline, linear colour, custom
  shaders (`Assets/Resources/Shaders`: lit, crust, water, glow, ghost, soft, scroll, text, bloom).
- **Loose items and belt items** are drawn with GPU instancing per item type, so thousands of coins cost
  little. Shoppers, Doug and Chad are jointed low-poly people with walk and throw animations.
- **UI** is uGUI built from code: a first-person HUD (crosshair, carry meter, cash, depth, hotbar, prompts,
  receipts), speech bubbles, toasts and banners, MAINT-OS 95 (a green-on-black 1995 laptop), the build
  catalogue, the Wish Journal and settings.
- **Audio is synthesised at startup**: footsteps and wading plops, coin clinks, a ka-ching register, wish
  chimes, a whistle, fanfares, and a lo-fi "dead mall muzak" loop per mall.
