# Wish Extractor: Mall Fountain Tycoon — Design

A first-person fountain factory game, modelled on *Find The Needle* (start with your bare hands, end with
a factory) with a twist: the resource refills itself, because shoppers keep throwing things into the
fountain. Everything below describes what the game does now (v2). The v1 overhead auto-clicker lives
only in git history (`96a8f81`); `PIVOT_FPS.md` is the plan the pivot followed.

## Pillars

1. **Physical and silly.** Every coin is a thing you look at and pick up, carry and cash in. The things
   people throw get more ridiculous as the fountain gets fancier. Every machine has a joke.
2. **Hands to factory.** One penny at a time, then a cup, a bucket, a wheelbarrow, a shop-vac; then
   skimmer bots, drain pumps, conveyor belts, tumblers, pigeon sorters and tunnel borers.
3. **A finite goal.** Under the water is forty years of crust. Dig to bare concrete, find the mall's
   bottom treasure, sign the next contract. Six malls.

## Core loop

1. **Shoppers toss** coins (and oddities) into the fountain, sometimes with a True Wish attached.
2. **You collect** in first person: aim, press E or click. Your container decides how much one trip holds.
3. **You deposit** at the COIN-O-MATIC 3000 in the custodial corner: cash, plus a receipt with a joke.
4. **You spend** at the Maintenance Terminal (MAINT-OS 95, a laptop on a folding table) and on the
   Fountain Improvement Plan (an easel by the rim).
5. **You automate**: build intakes at the rim, belts to processors, processors to deposit hoppers,
   generators to power it all.
6. **You beautify** the fountain: more shoppers, more tosses, better tosses.
7. **You dig** the crust, by hand and with rigs, down to bare concrete; then prestige to the next mall.

## Systems

### Collecting and carrying
- **Containers (10):** bare hands 1 · paper cup 5 · sand pail 20 · mop bucket 60 · fanny pack 150 ·
  wheelbarrow 500 (slower) · shopping cart 2,000 (no jumping) · ride-on floor scrubber 10,000 (faster) ·
  shop-vac backpack 40,000 (vacuums everything within 1.8 m) · industrial hopper suit 250,000 (3.2 m).
  Capacity counts physical items; heavy oddities take two slots; bundles (rolls, bags, pallets) let the
  numbers scale.
- **Grab tools (7):** fingers, litter grabber (reach), pool skimmer net, coin rake, detector magnet,
  reverse leaf blower, industrial magnet glove: more reach, a bigger scoop area, faster grabs.
- **Dig tools (6), hotbar 2:** sandbox shovel (1 scoop/swing), snow shovel, pickaxe, jackhammer,
  handheld borer (90). A swing drops what it digs into the water around the aim point.
- Wading multiplies walking speed by 0.72.

### The crowd and the fountain
- **Wishability** comes from fountain upgrades: Scrub the Grime, Fix the Water Jets, Coloured Lights,
  'MAKE A WISH' Neon, Cherub Statue and Koi (paid in Wish Tokens), Mood Music, Lucky Penny Dispenser,
  Influencer Photo Spot (tokens), Golden Statue, Fountain Light Show (tokens), Official Wishing Fountain,
  Wormhole to Other Fountains (tokens), plus the levelled Polish the Tiles (+1 each), Free Mints (toss
  rate), Coin Polish (value), Wish Catcher's Patience (tokens) and Donor Plaques (value). Every one-off
  upgrade changes how the fountain looks.
- **Tosses:** one every 7 s ÷ (1 + 0.06 × wishability), shared by a crowd of 3–36 shoppers. The coin
  ladder is penny, nickel, dime, quarter, loonie, toonie, silver dollar ($5), gold coin ($20) and diamond
  ($100), each unlocked at a wishability threshold; the tier is drawn from a bell curve centred on
  wishability × 0.075. Then 20 oddities, cheap and silly early (rubber duck, toddler shoe, car keys,
  dentures, a timeshare contract) and ridiculous after diamonds (a crypto seed phrase on a napkin, a
  lottery ticket, a smart fridge, a vending machine, a gold bar, a pageant tiara, a moon rock).
- **Ten shopper archetypes** with their own barks and signature throws: mall walker, bored teen (throws
  gum), grandma (dentures), unsupervised toddler (shoes, ducks, goldfish), businessman (wallets),
  tourist (foreign coins), influencer (phones), heartbroken proposer (rings), bodybuilder (vending
  machines, bowling balls, fridges), billionaire (gold bars, tiaras, moon rocks, diamonds).
- **True Wishes (107 texts):** a toss carries one with chance 5% + 3% per coin tier. The wish rises
  where the toss lands and hovers for 14 s. Catch it for cash and Wish Tokens (1/2/4/8/25 by rarity);
  a new one goes in the Wish Journal (+1% value forever). Uncaught wishes drift into a Wish Compressor if
  you built one.
- **The wormhole** adds tosses of other malls' loot at a quarter of the crowd's rate.

### The crust
- Each mall's basin holds a crust of eight named strata (48 in total, each an era of mall culture). The
  top layer (400 scoops) is loose change: digging it drops coins. Every deeper layer comes up as gunk
  chunks: worth 10% of their loot at the kiosk, 35% once washed in a Rinse Tumbler and full price once
  sorted (Pigeon Sorter, then the Coin Sorter). Deeper layers are richer (×1 at the top, ×4 at the bottom).
- Stronger digging breaks off bigger chunks: a chunk is 1.6 scoops × the dig multiplier, so the items a
  line carries per second stay the same while the loot in each grows.
- **Dig rigs** (Crust Jackhammer Rig, Tunnel Borer) stand at the rim and dig on their own. A rig only
  digs what its output buffer can take, so a backed-up line slows its rig instead of wasting loot.
- **Relics (72, 12 per mall):** sorting sometimes turns one up (0.4% per item, more in deeper layers).
  A complete mall set gives +10% value forever.
- The crust visibly sinks (the loose layer linearly, deeper layers in log scoops), the water follows it
  down, and a scaffold ramp spirals down the basin wall. **Bare concrete** reveals the bottom treasure
  (+25% value forever) and opens the next contract.

### The factory
- A one-metre grid on the hall floor. Intakes must stand at the rim facing the fountain; the stepping
  stones and the entrance stay clear.
- **Power:** hamster wheel, diesel generator, fryer-oil generator, skylight solar. One global budget: if
  demand beats supply, every consumer slows by the same ratio.
- **Intakes:** pool skimmer bot (roams the water and docks to unload), fountain drain pump (sucks up
  what's near its hose), claw machine crane (goes for the most valuable thing), plus the two dig rigs.
- **Processing:** rinse tumbler (gunk → washed), pigeon sorter and coin sorter (washed → loot, relics),
  coin roller (50 coins → a roll, +10%), bagger (20 rolls → a bag, +15%), palletiser (40 bags → a pallet,
  +25%), gold melter (gold coins, rings, tiaras, trophies → bars, +35%), wish compressor (uncaught wishes
  → bricks at 60% plus half their tokens). Machines pass through items they can't use, so one line can
  carry a mixed stream.
- **Logistics and output:** conveyor belts (fast and express tiers), splitters, deposit hoppers and
  armoured hoppers, which pay straight into your account.
- Build mode (hotbar 3): a grid ghost, R to rotate, drag to lay belts, X to demolish (full refund).

### The Maintenance Terminal
85 nodes in nine branches: Carry, Tools, Fountain, Power, Intake, Logistics, Processing, Security and
Head Office. One-off unlocks and levelled nodes; levelled nodes cost more each level. The effect line
of each node is generated from its data (`TerminalPanel.EffectText`); descriptions are flavour only.

### Hazards (always on, deliberately light)
- **Officer Doug** walks a loop around the plaza. Every 60–150 s, if you're wading within 22 m, he blows
  his whistle; still in the water five seconds later, you're fined 4% of your cash (at least $0.05 × the
  mall's scale). Security branch: Donut Diplomacy (−30% fines per level) and the Honorary Deputy Badge
  (no more fines).
- **Chad, the rival diver** (wetsuit, snorkel, a sack), turns up every 5–9 minutes once 40 things have
  been tossed. He wades in and pockets the most valuable nearby items (up to 40, for up to 70 s). Come
  within 2.2 m or press E on him and he flees, dropping everything back in the water. The 'No Rival
  Divers' Sign makes him visit less often.
- **The live goldfish** (thrown by toddlers) can't be carried: picking it up puts it straight back, to
  applause and a Wish Token. Intakes and Chad leave it alone.

### Meta
- **Guided goals (32):** a tutorial chain from "Pick up a coin" to "Find the First Wish", each paying a
  small reward (× the mall's scale).
- **Achievements (53):** +1% value each, forever.
- **Mall events:** one per mall, every 7–13 minutes for 60 s (Mall Walker Rush Hour, Neon Hour, Black
  Card Hour, Exchange Rate Spike, Jackpot Hour's rain of gold coins, Wishing Hour).
- **Head Office (prestige):** clearing a mall pays Lucky Pennies (6, 14, 30, 60, 110, 200). Eight perks
  kept forever: Company Credit Card (start cash), Company Van (start container), Institutional Knowledge
  (start with the basic factory researched), Seniority (value), Union-Issue Sneakers, Wishful Thinking
  (wishability), Relic Radar, Excavation Grant (dig power). Everything else resets per mall.
- **Remodel contracts:** after Eternity Plaza every mall comes round again, prices and values ×3 and the
  crust ×1.6 per lap.

## The six malls

| # | Mall | Depth | Theme | Event | Bottom treasure | Scale |
|---|---|---|---|---|---|---|
| 1 | Crestview Commons | 30 ft | Suburban dead mall | Mall Walker Rush Hour (tosses ×3) | The Founder's Penny | ×1 |
| 2 | Neon Galaxy Mega-Mall | 45 ft | 1980s neon arcade | Neon Hour (wishes ×3) | Golden Arcade Token | ×1.5 |
| 3 | Galleria Aurelia | 60 ft | Luxury marble and gold | Black Card Hour (relics ×4) | The Platinum Membership Card | ×2.5 |
| 4 | Skyport Terminal C | 75 ft | Airport concourse | Exchange Rate Spike (value ×2.5) | The Lost Passport of Everyone | ×4 |
| 5 | The Lucky Lagoon | 90 ft | Casino resort | Jackpot Hour (gold coins rain) | The Lucky Die | ×6.5 |
| 6 | Eternity Plaza | 120 ft | 1956 atomic-age mall over a Roman well | Wishing Hour (everything ×2) | The First Wish (the ending) | ×10 |

Each mall has its own strata, crust loot table, 17–22 wishes, 12 relics, storefront signs, theme colours
and a procedural music loop.

## Economy structure (how 24+ hours is guaranteed)

- **Scale per mall.** Every price and toss value is authored in Crestview dollars and multiplied by the
  mall's `ValueScale` (1 → 10). Crust loot, wishes and relics are divided by the same factor when the
  malls are built, so their actual values don't change. Each mall is Crestview's economy at a bigger
  scale, and Head Office perks (not cheaper prices) are what make later malls go faster.
- **The first hour.** The fountain starts with 320 loose coins (about $21). The first skimmer line
  (hamster wheel, skimmer, belts, hopper) costs about $50 including research, and the tumbler, pigeon
  sorter, diesel and first dig rig follow within the next hour. Engaged bot: cup at 12 s, bucket at
  12 min, skimmer at 17 min, first line at 37 min.
- **Digging pays.** Packed crust holds five times the loose layer's loot per scoop (`CrustDensity`),
  and chunk size grows with dig power, so dig upgrades raise both depth and income.
- **Controlled growth.** Income multipliers compound, so each levelled multiplier must cost more per
  level than the income it adds. As a rule of thumb, the sum over the levelled nodes being bought of
  log(effect per level) / log(cost growth) must stay well under 1; otherwise income runs away.
- **Late-game sinks.** Once the one-off unlocks are bought, five 60-level nodes keep every visit to the
  terminal worthwhile: Crust Softener, Diamond-Tipped Bits (+10% dig each), Gold-Plated Chutes, Pigeon
  Performance Bonuses and Donor Plaques (+7% value each), all ×1.9 per level. Round-robin across five
  sinks, the next purchase is only ~14% dearer than the last, so purchases stay frequent.
- **Fitted crust sizes.** `Tools/BalanceSim -- fit --apply` bisects each mall's total crust (203K,
  8.7M, 29.7M, 118M, 235M and 546M scoops) until the engaged bot clears it in the planned hours (3, 3.5,
  4, 4.5, 5, 5). Strata grow geometrically from the 400-scoop loose layer to the total.
- **Measured pacing** (engaged / casual): Crestview 2h46m / 4h02m · Neon Galaxy 3h49m / 4h29m ·
  Galleria 3h46m / 4h29m · Skyport 4h28m / 5h20m · Lucky Lagoon 5h01m / 6h04m · Eternity 5h02m / 6h17m.
  Totals **24h50m / 30h40m**. The longest gap between purchases for the engaged bot is under 7 min in
  the first three malls and about 10–14 min at the very end of the last three.
- **Known limits.** In later malls the Head Office head start buys the whole fixed tree in 1–1.5 h,
  and the rest of the mall is the five sinks while the crust sinks. Late incomes are large
  (Eternity ends near $1T per minute). More late-game content (machines, mall-specific mechanics) would
  shorten that phase and let the numbers stay smaller.

## Presentation

- **All art is procedural** (no asset files): low-poly meshes with vertex colours built by `MeshKit`,
  where vertex alpha is an emission mask; built-in render pipeline, linear colour, custom shaders
  (`WE/Lit`, `WE/Crust`, `WE/Water`, `WE/Glow`, `WE/Ghost`, `WE/Soft`, `WE/Scroll`, `WE/Text`) and a
  small bloom pass (`Hidden/WE/Bloom`).
- **Loose items** are data in the simulation, drawn with `Graphics.RenderMeshInstanced` per item type,
  so thousands of coins cost little. First-person hands show the held item or the container's fill.
- **The mall hall** has a south wall with storefronts and the entrance, a skylight ceiling, a balcony,
  planters and benches; the crowd, Officer Doug and Chad are low-poly people with walk cycles and speech
  bubbles.
- **UI** is light glass cards: cash and Wish Tokens, the carry card, the goal card, the crust card, a
  three-slot hotbar, context prompts under the crosshair, receipts, toasts and banners; MAINT-OS 95 is a
  green-on-black 1995 terminal.
- **Audio is synthesised at startup** (`Synth`): coin clinks, the register, splashes and plops,
  footsteps and wading, Doug's whistle, wish chimes, fanfares and a lo-fi "dead mall muzak" loop per mall.
