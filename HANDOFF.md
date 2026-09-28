# HANDOFF — Wish Extractor

_Last updated 2026-09-27 (first build session)._

## NEXT: first-person pivot

Nico wants the game reworked into a first-person, Find The Needle–style factory game with NPCs throwing
coins into the fountain. Plan: `PIVOT_FPS.md`. Ready-to-paste session prompt: `NEXT_SESSION_PROMPT.md`.
Everything below describes the current (v1, overhead auto-clicker) build.

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
