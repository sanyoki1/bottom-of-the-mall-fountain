# Wish Extractor: Mall Fountain Tycoon

A first-person fountain factory game in Unity (6000.4.2f1), in the spirit of *Find The Needle*. You are
the night-shift custodian of a dead mall's fountain. Shoppers toss coins in (and, as the fountain gets
fancier, dentures, phones, bowling balls, vending machines and moon rocks). You wade in, pick them up,
carry them to the COIN-O-MATIC 3000 and cash out. Bigger containers, better grabbers and dig tools come
from the Maintenance Terminal. Then generators, skimmer bots, drain pumps, conveyor belts, rinse tumblers,
pigeon sorters and tunnel borers take over. Under the water lies forty years of crust. Dig it to bare
concrete, find the mall's bottom treasure and sign the next contract. Six malls, each deeper and richer.

Officer Doug fines you for wading during mall hours. Chad, a rival diver in a wetsuit, sneaks in to
pocket your coins. The live goldfish goes back in the water.

Every mall also has a **Wonder**, a three-stage megaproject hung from the skylight over the fountain.
The Penny Chandelier, the Mirrorball of Tomorrow and the Wheel of Fountain Fortune are three of them.
Each stage asks for goods (coin rolls, bags, pallets, gold bars, wish bricks, relics, particular
things the crowd throws in) and pays a bonus; finishing one earns a perk forever. The crust gives up
**buried finds** (time capsules, strongboxes, lost suitcases) that you crack open for a jackpot, a
burst of coins, a swarm of wishes or a minute-long frenzy. (The Wonders and finds are in the game
rules and the balance bot; their visuals are the next step, see `HANDOFF.md`.)

## Play

- **Windows build:** `Builds/Windows/WishExtractor.exe` (windowed, resizable). Not committed; rebuild it
  with the command below.
- **Unity:** open this folder in Unity 6000.4.2f1 and press Play in `Assets/Scenes/Main.unity`. The whole
  game builds itself from code at runtime, so the scene is almost empty.

### Controls

| Input | Action |
|---|---|
| **WASD** / arrows · mouse | Walk · look (click the window to capture the mouse) |
| **Shift** · **Space** | Sprint · jump (no jumping with a shopping cart) |
| **E** / **F** or left click | Pick up what you're aiming at, deposit at the COIN-O-MATIC, catch a wish, open the Maintenance Terminal, approve a job on the Fountain Improvement Plan, shoo Chad |
| Hold left click | Keep grabbing (or keep digging with the dig tool) |
| **1** · **2** · **3** / **B** | Grab tool · dig tool · build mode |
| **Tab** · mouse wheel | Build catalogue · cycle the build selection |
| **R** · **X** (build mode) | Rotate · toggle demolish (click a building to remove it and get its cost back) |
| **J** · **Esc** · **C** | Journal (wishes, relics, achievements, stats) · settings · the next contract after bare concrete |

Progress autosaves every 30 s and on quit
(`%USERPROFILE%\AppData\LocalLow\Nico Macaraig\Wish Extractor\wishextractor_save.json`). There is no
offline progress: the fountain only runs while you play.

## How long is it?

Measured with the balance simulator (a bot playing the real game rules, see below):

| Mall | Engaged bot | Casual bot |
|---|---|---|
| 1. Crestview Commons | 2h 50m | 3h 51m |
| 2. Neon Galaxy Mega-Mall | 3h 28m | 3h 54m |
| 3. Galleria Aurelia | 4h 17m | 4h 01m |
| 4. Skyport Terminal C | 4h 22m | 4h 27m |
| 5. The Lucky Lagoon | 5h 05m | 5h 04m |
| 6. Eternity Plaza | 4h 54m | 4h 41m |
| **Campaign** | **24h 55m** | **25h 58m** |

Engaged = sprints, catches 80% of wishes, opens every buried find, shops like a player working down the
terminal and builds what each mall's Wonder needs. Casual = walks, catches 45%, opens 60% of finds, idles
after a quarter of its trips. Other seeds give 25.2 h and 25.1 h for the engaged bot.
After the sixth mall the game continues with Remodel contracts (every mall again, bigger and richer).

## Build

```bash
"C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -executeMethod WishExtractor.EditorTools.ProjectBuilder.BuildWindowsCI -logFile Logs/build.log
```

Or in the editor: **Wish Extractor → 2. Build Windows Player**. The build step also (re)creates
`Assets/Scenes/Main.unity` and applies the player settings.

## Self-tests in the player

| Flag | What it does |
|---|---|
| `-autotour -shots <dir>` | Scripted tour (51 screenshots): the kiosk, pickups, the crowd and every fountain upgrade, the terminal, Officer Doug, Chad, the goldfish, containers and tools, the factory, digging and processing, bare concrete, the menus and all six malls. Quits by itself. |
| `-uitest -savefile <name> -fresh` | Drives the real controller, crosshair targeting and uGUI through 96 checks and logs `[UITEST] PASS/FAIL` per step, ending with `[UITEST] done: 96 passed, 0 failed`. |
| `-loadtest -savefile <name>` | Loads the save, logs what came back (pose, cash, carry, loose items, buildings), quits. |
| `-savefile <name>` / `-fresh` | Use a different save file / erase it first |
| `-dev` | Debug keys: F5 cash, F6 finish the mall, F7 dig 10% deeper, F8 start the mall event |

```bash
Builds/Windows/WishExtractor.exe -autotour -shots Screenshots -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile Logs/player_tour.log
Builds/Windows/WishExtractor.exe -uitest -savefile wishextractor_uitest.json -fresh -screen-fullscreen 0 -screen-width 1920 -screen-height 1080 -logFile Logs/player_uitest.log
Builds/Windows/WishExtractor.exe -loadtest -savefile wishextractor_uitest.json -screen-fullscreen 0 -logFile Logs/player_loadtest.log
```

## Balance simulator

`Tools/BalanceSim` is a .NET 10 console app that compiles the game's pure C# rules
(`Assets/Scripts/Core`) together with a bot that plays them: walking is abstracted to travel times, but
every pickup, deposit, wish, dig swing, purchase and building goes through the same `Sim` calls as the
game.

```bash
dotnet run -c Release --project Tools/BalanceSim -- 40 1234 engaged     # play all six malls (also: casual)
dotnet run -c Release --project Tools/BalanceSim -- fit --apply         # re-fit every mall's crust size
```

The run prints, per mall: progress every hour (cash, dig rate, depth, wishability, carry, rigs and
multipliers, power, hopper income), the time of each milestone, where the money came from and went, and
the longest gap between purchases. It also writes `Tools/BalanceSim/report_<profile>.txt`. `fit` bisects
each mall's crust size until the engaged bot clears it in the planned hours (3, 3.5, 4, 4.5, 5, 5) and
`--apply` writes the result into the `<fitted-scoops>` block of `ContentMalls.cs`. Run it after any
change to prices, rates or multipliers.

Other commands: `counts` (content totals), `malls` (per-mall values and crust bounds), `techs` (the tech
tree by branch and grid cell, with overlap warnings), `crowd <wishability>`, `factory`, `crust` and
`smoke` (system checks). Environment knobs: `BOT_REPORT=<seconds>` (log interval), `BOT_MALLS=<n>` (stop
after n malls), `BOT_CRUST=a,b,c,…` (try crust sizes without editing the content).

See `DESIGN.md` for the game design and `HANDOFF.md` for the current state and next steps.
