# HANDOFF — Wish Extractor

_Last updated 2026-09-27 (session 2, stopped early in Milestone 1 at Nico's request)._

## NEXT: first-person pivot — M1 done (session 3), continuing with M2

Nico wants the game reworked into a first-person, Find The Needle–style factory game with NPCs throwing
coins into the fountain. Plan and confirmed decisions: `PIVOT_FPS.md`. Session prompt: `NEXT_SESSION_PROMPT.md`.

**Git:** nested repo in this folder, baseline commit `96a8f81` = complete v1. Commit after each verified
milestone (authorized by Nico).

**M1 (verified, committed):** first-person controller (`View/FirstPerson.cs`, injectable `FPInput`),
view-model hands (`Hands.cs`), instanced loose items (`ItemRenderer.cs`), COIN-O-MATIC 3000 (`Kiosk.cs`),
WE/Water shader, south wall + skylight ceiling + colliders (`WorldBuilder`), water/rim/stepping-stone/crust
colliders (`FountainView`), FP HUD, Core `Sim` v2 (loose items, carry, deposit, tech levels), SaveData v2.
Tour: 18 screenshots; uitest 28/28; loadtest restores pose, cash, carry tier and loose items.
Lessons: a lit mesh at exactly zero scale makes NaN pixels that bloom turns into black squares; world
text colours must have alpha 1 (`Mats.NewText` now forces it — v1's coloured signs were invisible);
the player sits on the Ignore Raycast layer so the aim ray doesn't hit its own capsule.

**M2 (verified, committed):** shopper crowd in Core (`SimCrowd.cs`: 10 archetypes walk in from doors in
`Layout.cs`, stand at r = 10.7, wind up, toss coins/oddities/gum along the Gaussian tier curve, speak barks
or wish quotes, leave), True Wishes rising where wishful tosses land (catch with E: cash + Wish Tokens +
journal), the Fountain Improvement Plan easel (buys Fountain-branch techs), first 3 beautification upgrades
with visuals (scrubbed tiles, water jets, coloured LED ring), speech bubbles, positional splash/plop audio.
uitest 38/38. `dotnet run ... -- crowd <wishability>` prints what the crowd is doing headlessly.

**M3 (verified, committed):** Maintenance Terminal (`View/Terminal.cs` prop, `UI/TerminalPanel.cs` =
MAINT-OS 95: branch tabs, node graph by Col/Row with prerequisite lines, detail pane with generated effect
line). 36 nodes so far (carry ladder + Sturdier Bottoms/Comfy Sneakers, grab ladder + Longer Arms/Nimble
Fingers, 13 fountain uniques — 5 paid in Wish Tokens — plus 4 levelled fountain nodes). Held tool models and
floor-level carts/barrows (`Hands.cs`), area-grab ring, shop-vac auto pickup, detector glints, all fountain
decor (`Decor.cs`), wormhole tosses of other malls' loot. Dig tools are defined but not sold until M5.
uitest 49/49 (buys through the real terminal UI).

Session 2 notes (kept for history):
- Done (session 2):
  - `Core/Defs.cs` rewritten for v2: ItemType/ItemCat, CarryDef, ToolDef, ArchetypeDef, TechDef/TechKind/
    TechBranch, ObjectiveDef with a flat reward. MallDef now has CrustScoops, ValueScale, LootTypes and
    GunkTypes.
  - `Core/Balance.cs` rewritten (tosses, tier curve, wishes, crust, deposit rates).
  - `Core/ContentWorld.cs` added: item registry (9 coin tiers, 19 oddities, per-mall loot, gunk and relic
    types, processed goods), 10 carry tiers, 7 grab tools, 6 dig tools, receipt and "hands full" jokes.
  - `Core/ContentMalls.cs` trimmed: v1 fitted bounds gone, CrustScoops per mall (placeholders), Crestview
    event now boosts tosses.
- Still to do before M1 compiles:
  - `Content.cs`: after BuildMalls, call BuildCoins, BuildOddities and BuildMallTypes; expose the `types`
    list as an `Items` array; add Carry, GrabTools and DigTools; drop Machines, Tools, Upgrades and
    HeadOffice.
  - `ContentMeta.cs`: achievements and objectives against the new Sim.
  - `SaveData.cs` v2: player pose, carried and loose items with a type-id table, tech levels, stats,
    mouse sensitivity and FOV settings.
  - `Sim.cs` v2: loose items, toss scheduler, pickup, deposit, crust.
  - Remove the v1-only files with `git rm`, including their `.meta`: SimShop, ContentShop, MachineVisuals,
    Stations, CameraRig, Clickables (keep RarityColors), ShopPanel.
  - Rewrite GameView, HUD, Modals and GameRoot (FP autotour, uitest and loadtest).
  - New view code: first-person controller, first-person hands, instanced item renderer, COIN-O-MATIC kiosk,
    water shader.
  - WorldBuilder: south wall, ceiling, colliders. FountainView: water, crust collider, ramp.
  - Stub `Tools/BalanceSim/Program.cs` against the new Core so it compiles until M6.
- Note: `ContentMalls.cs` was edited once with a PowerShell `WriteAllText`. Use Write/Edit for file changes.

Everything below describes the v1 (overhead auto-clicker) build at commit `96a8f81`.

## State

Complete, playable game: six malls, endless Remodel contracts, full UI, procedural art and audio,
save/load and offline progress. Windows build at `Builds/Windows/WishExtractor.exe` (not in git).
No git commits yet (the folder sits inside the home-directory repo; ask Nico before `git init`/commit).

## Verified (measured, not assumed)

- Compiles clean in Unity 6000.4.2f1; Windows player builds (85 MB).
- `-uitest`: 39/39 checks pass (intro, dig, sell button and vending machine, buy tool/machine, journal tabs,
  settings, shop toggle, clear mall → contract → sign → new mall intro → buy a Head Office perk).
- `-loadtest`: save/load restores cash, tool, machines, clicks, depth, objective; offline 2 h simulated
  at 50% efficiency.
- `-autotour`: screenshots of every mall and every major screen; ~165 FPS at 1920×1080 in the busiest
  Crestview scene (RX 7800 XT).
- Balance sim: engaged bot 24h29m for the six malls, idle bot 40h55m; Remodel lap 1 ≈ 9 h (engaged).

## Not verified

- Audio was never listened to (the clips are synthesised and play without errors; mix levels are guesses).
- Real OS mouse input was not driven end-to-end (computer-use couldn't target the unpackaged exe); the
  uitest drives the same EventSystem raycasts and world picking a real click uses.
- Only 1600×900 and 1920×1080 windows were looked at; ultrawide/4K layouts are untested.

## Ideas / next steps

- Play it for real and tune the feel: click juice, sound levels, wish frequency, golden penny rate.
- The idle profile stalls in the Lucky Lagoon's last layers (12 h); if idle players matter, add a late
  Lucky Lagoon upgrade or soften its last two boundaries.
- Late-mall purchase gaps reach ~13 min for the engaged bot (malls 5–6); more late upgrades would help.
- Possible additions: per-mall gimmick mechanics beyond timed events, cosmetic worker hats, a statistics
  graph, controller support, Steam-style achievements.
