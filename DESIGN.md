# Wish Extractor: Mall Fountain Tycoon — Design

Source concept: *Wish_Extractor_Game_Concept.pdf* ("The Bottom of the Mall Fountain"), modelled on the
progression arc of *Find The Needle* (one enormous finite pile, dig it by hand, sell the raw material,
then belts, scanners and robots until it runs itself). Everything below is what the game actually does.

## Pillars

1. **Gross nostalgia, big numbers.** Forty years of mall-fountain gunk, dug out with increasingly absurd
   industrial machinery. Every layer is a joke about an era of mall culture.
2. **A visible factory.** Every machine you buy appears around the fountain and animates while its stage
   is busy. The crust visibly sinks; the basin walls show faint bands of every layer you dug through.
3. **Always something to click.** Wishes float up, golden pennies glint, the mall rat runs off with loot,
   machines can be whacked, the combo meter rewards fast clicking.

## Core loop: Dredge → Dissolve → Sort → Sell

| Stage | Input → output | Buffer on screen | Sells for if dumped early |
|---|---|---|---|
| **Dredge** | basin crust → raw clumps | Hopper ("RAW GUNK" bin) | 10% of full value |
| **Dissolve** | raw clumps → washed loot (+ True Wishes) | Washed pile on the sorting table | 35% |
| **Sort** | washed loot → sorted loot (+ relic finds) | Coin pile by the vending machine | 100% |
| **Sell** | sorted loot → cash | Greasy vending machine | (manual until the Coin-Op Hookup) |

The top layer of every mall is *loose*: digging drops sorted coins straight into your pocket (the
"Sticky String" manual-grind phase). From the second layer down everything is glued together, so washing
and sorting become necessary. The pipeline card flags the bottleneck stage. Value is stored per item at
dig time (deeper layers are richer) and multipliers apply at sale.

### The three phases in each mall

1. **Manual grind** — bubblegum on a string (1 item/click) → butter knife → sandbox shovel; pogo sticks
   bounce in the basin. Sell by clicking the vending machine.
2. **Soda wash** — the Syrup Seal layer: Diet Cola Tumblers, Pigeon Perches, Mall Walker Brigades,
   Food Court Dishwashers, Coin Star Junctions. True Wishes start floating up. Coin-Op Hookup automates selling.
3. **Industrial devastation** — Jackhammer Excavators, the glowing Acid-Wash River, Optical Laser Scanners,
   the Wish Compressor; later malls add claw cranes, car washes, prize-counter robots, gold tunnel borers,
   champagne jacuzzis, quantum sieves and three megaprojects.

## Systems

- **Tools (11)** — each multiplies items per click (1 → 1.6M). Reset every mall (Head Office can start you
  with better ones). Clicks build a **combo** (×2 at full meter, raised by upgrades/perks); holding the
  button repeats digs at 6/s.
- **Machines (19)** — 5 tiers per stage (Dig/Wash/Sort) + Wish Compressor + 3 megaprojects. Price grows
  ×1.15 per unit; owning 25/50/100/150/… of one machine doubles its output (milestones). Tiers unlock by
  depth, earlier in later malls (`Balance.TierUnlock`).
- **Upgrades (251)** — 10 per machine (at 1…400 owned), 26 general (value, click, wishes, relics, golden
  pennies, stage multipliers, auto-sell), and 15 themed upgrades that exist only in their own mall.
- **True Wishes (107 texts)** — spawn every ~11 s while washing; five rarities; drift up for ~13 s. Value =
  (authored base × sale multipliers) + (a few seconds to minutes of automatic income by rarity). Catching
  a new one records it in the **Wish Journal** (+1% sale value each, forever). Missed wishes can be caught
  by the **Wish Compressor** (pressed into purple bricks at 80% value).
- **Relics (72) + bottom treasures (6)** — rolled while sorting (~1 per 80 s), 12 per mall; completing a
  mall's set gives +10% value forever; each bottom treasure (the Founder's Penny … the First Wish) +25%.
- **Golden pennies** — every 75–170 s; 13 s to click. Effects: Lucky Streak (sale ×7), Change Avalanche
  (cash), Dig Frenzy (clicks ×15), Wish Storm (5–7 wishes), Machine Overdrive (all machines ×3).
- **Mall rat** — runs along the rim every 200–380 s; catching it drops a relic with boosted rarity.
- **Mall events** — one per mall, every 5–7 minutes for 40 s (Mall Walker Rush Hour, Neon Hour, Black Card
  Hour, Exchange Rate Spike, Jackpot Hour, Wishing Hour).
- **Guided goals (38)** — a tutorial chain through the whole first mall, then milestones across the game,
  each with a cash reward; after the chain ends the card shows the next layer to reach.
- **Achievements (71)** — +1% sale value each, forever.
- **Head Office (prestige)** — clearing a mall to bare concrete pays Lucky Pennies (6 → 200). 18 perks,
  265 levels: machine speed, sale value, click power, seed money, starting tools, veteran crews, auto-sell
  from the start, wish/relic/golden boosts, cheaper machines, longer and better offline earnings, and a
  Compressor patent.
- **Remodel contracts (endless)** — after Eternity Plaza, every mall repeats with prices ×1e24 per lap,
  its own fitted first-lap layer sizes, and deeper layers ×3 per further lap.
- **Offline progress** — up to 2 h at 50% efficiency by default (up to 24 h at 100% with perks).

## The six malls

| # | Mall | Depth | Theme | Event | Bottom treasure | New machinery |
|---|---|---|---|---|---|---|
| 1 | Crestview Commons | 30 ft | Suburban dead mall, teal & peach | Mall Walker Rush Hour (dig ×3) | The Founder's Penny | Tiers 1–3, Wish Compressor |
| 2 | Neon Galaxy Mega-Mall | 45 ft | 1980s neon, black & white checker | Neon Hour (wishes ×3) | Golden Arcade Token #0001 | Tier 4 (claw crane, car wash, prize bots) |
| 3 | Galleria Aurelia | 60 ft | Luxury marble & gold | Black Card Hour (relics ×4) | The Platinum Membership Card | Tier 5 (tunnel borer, champagne jacuzzi, quantum sieve) |
| 4 | Skyport Terminal C | 75 ft | Airport concourse | Exchange Rate Spike (sale ×2.5) | The Lost Passport of Everyone | Megaproject: Baggage Carousel Loop |
| 5 | The Lucky Lagoon | 90 ft | Casino resort, red & gold | Jackpot Hour (golden penny rain) | The Lucky Die | Megaproject: Slot Machine of Fortune |
| 6 | Eternity Plaza | 120 ft | 1956 atomic-age mall over a Roman well | Wishing Hour (everything ×2) | The First Wish (ending) | Megaproject: The Wish Engine |

Each mall has 8 named layers with flavour text (48 total), its own loot table (57 item kinds in all),
12 relics, 17–22 wishes, 15 themed upgrades, storefront signs, signature props and a procedural music loop.

## Economy structure (how 24+ hours is guaranteed)

- **Per-mall price scale.** Every price is authored in Crestview dollars and multiplied by the mall's
  `CostScale` (×1e4 per mall). Sale value is scaled the same way through the mall's contract rate, times a
  generosity factor (1 → 4), so every mall is a fresh climb with bigger numbers rather than instantly cheap.
- **Mall-exclusive themed upgrades** stop multipliers from stacking across the whole game (an early
  version had ×245,000 by mall 6 and cleared it in seconds).
- **Fitted stratum boundaries.** Each layer's size in items is fitted from the bot's actual trajectory so
  each layer takes a planned share of the mall (`Tools/BalanceSim -- fit --apply`). The depth meter moves
  linearly in log(items) within a layer, so it keeps moving while rates grow exponentially.
- **Measured pacing** (engaged bot / idle bot): Crestview 2h31m / 4h22m · Neon Galaxy 3h21m / 5h23m ·
  Galleria 3h39m / 5h03m · Skyport 4h15m / 6h10m · Lucky Lagoon 4h30m / 11h56m · Eternity 6h13m / 8h01m.
  Totals: **24h29m / 40h55m**. Remodel lap 1: ~1.3–1.7 h per mall (engaged).
- Longest gap between purchases for the engaged bot: 3 min (mall 1) to 13 min (mall 6).

## Presentation

- **All art is procedural** (no asset files): low-poly meshes with vertex colours, where vertex alpha is an
  emission mask (neon, screens, lasers glow and bloom). Built-in render pipeline, linear colour, custom
  surface shaders (`Assets/Resources/Shaders`), a small bloom/grade post effect, a floodlight over the dig.
- **The crust** is a heightfield with a tiled procedural coin texture tinted per layer; clicks punch
  craters that relax over a few seconds; junk props sit on top and are swapped per layer.
- **UI** is light "glass": translucent rounded cards, soft shadows, one teal accent, gold for money
  (Segoe UI system fonts). Wallet, depth gauge, pipeline card, goal card, shop (Tools / Machines /
  Upgrades / Head Office), toasts, centre banners for new layers and legendary finds, wish quote bubbles,
  the Wish Journal (wishes, relic museum, achievements, stats), settings, offline report, contract signing.
- **Audio is synthesised at startup**: digs, coin clinks, a ka-ching register, wish chimes, fanfares, rat
  squeaks, and a 16-bar lo-fi "dead mall muzak" loop per mall (key, tempo and progression from the theme).
