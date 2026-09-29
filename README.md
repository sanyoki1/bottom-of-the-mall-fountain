# Wish Extractor

A first-person fountain factory game in Unity (6000.4.2f1), in the spirit of *Find The Needle*. You clock in
at the bottom of a dead mall's fountain with nothing but your hands. Pick up the pennies shoppers throw
in, one at a time, and carry them to the COIN-O-MATIC 3000. Buy a paper cup, then a sand pail, a mop
bucket, a wheelbarrow, a shopping cart. Research hamster wheels, skimmer bots, conveyor belts and deposit
hoppers at the Maintenance Terminal. Beautify the fountain so the crowd throws more, and fancier, things
(quarters, gold coins, diamonds, then car keys, dentures, a whole vending machine). Then dig through forty
years of crust to bare concrete and sign with the next mall.

Officer Doug, mall security, doesn't like wading: when he raises his binoculars, freeze like a statue (or
hide behind the centrepiece) until he looks away, and a passing shopper might even tip the statue; move and
he fines you. A rival diver named Chad pockets your coins, and every goldfish has to go back in the water.

The later malls each sell one machine you can only build there: Galleria Aurelia's Champagne Cork Cannon
blasts rinsed slabs of crust into the water from behind the rim, Skyport's Baggage Claim Carousel sends cargo
drones to empty rim intakes that have no belt behind them, the Lucky Lagoon's Slot-Machine Sorter gambles raw
gunk into sorted loot (or nothing, or a jackpot), and Eternity Plaza's Old Well grants the wishes nobody
catches and makes that much crust disappear.

## Play

- **Windows build:** `Builds/Windows/WishExtractor.exe` (windowed 1600×900, resizable). No install needed.
- **Unity:** open this folder in Unity 6000.4.2f1 and press Play in `Assets/Scenes/Main.unity`
  (the whole game builds itself at runtime from code, so the scene is almost empty).

The Windows build is not committed; rebuild it with the command below.

### Controls

| Input | Action |
|---|---|
| **WASD** / mouse | Walk / look · **Shift** sprint · **Space** jump |
| **E** or left click | Pick up what you're looking at (aim assist: close is good enough), deposit at the kiosk, use the terminal or the easel, catch a wish, chase Chad |
| Hold **E** or left click | Keep grabbing: sweep your view over the coins (nets, rakes and magnets scoop everything in their circle) |
| **1** / **2** / **3** (or **B**) | Grab tool / dig tool / build mode |
| In build mode | Click build (drag for belt lines) · **R** rotate · wheel next item · **Tab** catalogue · **X** demolish |
| **J** / **Esc** | Journal (wishes, relics, achievements, stats) / settings |
| **C** | Sign the next contract once you hit bare concrete |

Progress autosaves every 30 seconds and on quit
(`%USERPROFILE%\AppData\LocalLow\Nico Macaraig\Wish Extractor\wishextractor_save.json`).

## How long is it?

Measured with the balance simulator (a bot playing the real game rules, see below):

| Player profile | Six malls (the campaign) |
|---|---|
| Engaged bot (sprints, catches 80% of wishes, saves for the next big upgrade, fills the rim with lines, builds each mall's own machine) | **26h 33m** (seed 1234; 26.3–27.0 h over seeds 1–5) |
| Casual bot (walks, reacts slower, catches 45% of wishes, idles now and then; builds the same factory) | 27h 55m (seed 1234) |

After the sixth mall the game continues with Remodel contracts (every mall again, deeper and richer).

## Build

```bash
"C:\Program Files\Unity\Hub\Editor\6000.4.2f1\Editor\Unity.exe" -batchmode -nographics -projectPath . -executeMethod WishExtractor.EditorTools.ProjectBuilder.BuildWindowsCI -logFile Logs/build.log
```

Or in the editor: **Wish Extractor → 2. Build Windows Player**. The build step also (re)creates
`Assets/Scenes/Main.unity` and applies player settings (linear colour, windowed, no splash).

No Unity at hand (a cloud or Linux session)? `bash Tools/UnityCompileCheck/unity6_compile.sh <extracted editor>`
compiles every script with Unity 6000.4.2f1's own compiler and assemblies, taken from the Linux editor archive
(download and extract steps are in the script's header). The quicker `dotnet build Tools/UnityCompileCheck`
uses Unity 2022.3 reference assemblies from NuGet and reports one expected error (a Unity 6-only overload in
`GameRoot.cs`). Both catch compile errors only.

## Self-tests in the player

| Flag | What it does |
|---|---|
| `-autotour -shots <dir>` | A scripted walk through every system and mall; saves 61 screenshots, then quits |
| `-uitest -savefile <name> -fresh` | Drives the real controller, crosshair and uGUI through 123 checks (walk, aim assist, pick up, sweep, deposit, terminal, crowd, wishes, the goldfish, Officer Doug's statue check, Chad, build mode, digging, the contract, Head Office, the four mall machines, menus, save) and logs PASS/FAIL |
| `-loadtest -savefile <name>` | Loads a save and logs what came back (pose, cash, carried items, buildings) |
| `-savefile <name>` / `-fresh` | Use a different save file / erase it first |
| `-dev` | Debug keys: F5 cash, F6 finish the mall, F7 dig 10% deeper, F8 start the mall event |

Example (screenshots land in `Screenshots/`):

```bash
Builds/Windows/WishExtractor.exe -autotour -shots Screenshots -screen-fullscreen 0 -screen-width 1920 -screen-height 1080
```

## Balance simulator

`Tools/BalanceSim` is a .NET 10 console app that compiles the game's pure C# rules
(`Assets/Scripts/Core`) together with a bot player.

```bash
dotnet run -c Release --project Tools/BalanceSim -- 40 1234 engaged
dotnet run -c Release --project Tools/BalanceSim -- fit --apply
```

The first plays all six malls and writes `report_<profile>.txt`: hourly income, dig rate and depth; when
each layer was reached; the first time each key upgrade was bought; the longest gap between purchases;
where the money came from; and which rim slots got a line. The second fits every mall's crust size and
layer boundaries to the planned hours and writes them into `ContentMalls.cs`; run it after any change to
prices, rates or multipliers. `counts`, `crowd <wishability>`, `factory`, `crust`, `smoke`, `slots`,
`machines` (the four mall machines, each in its own mall) and `guard` (Officer Doug's statue check) check
individual systems.

See `DESIGN.md` for the game design and `HANDOFF.md` for the current state and next steps.
