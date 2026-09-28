Paste this into a new Claude Code session opened in `C:\Users\niczm\Bottom of the Mall Fountain`:

---

Continue the first-person pivot of Wish Extractor (Unity 6000.4.2f1, this repo). Read `CLAUDE.md`, then `HANDOFF.md`, then `PIVOT_FPS.md`. `PIVOT_FPS.md` is the design and milestone plan and the source of truth. Its "Decisions" and "Spec decided in session 2" sections are settled, so don't ask me those again.

Where things stand: session 2 stopped partway through Milestone 1. The working tree has uncommitted work and **does not compile yet**. `HANDOFF.md` lists what's done (Core `Defs.cs`, `Balance.cs`, `ContentWorld.cs`, trimmed `ContentMalls.cs`) and exactly what's left before M1 compiles. Start there. Build on the existing WIP; don't redo it.

What I want (unchanged): a first-person fountain factory game like Find The Needle. Pick up coins by hand one at a time and carry them to the COIN-O-MATIC 3000 deposit kiosk. Buy equipment that carries more per trip. A tech tree on the Maintenance Terminal unlocks tools, generators, machines and conveyor belts for automated assembly lines. NPC shoppers constantly throw coins into the fountain, and beautification makes them throw more often and better things (nickels → dimes → quarters → loonies → gold → diamonds → ridiculous items). Lots of humour. Hazards (security guard, rival diver) are always on. Keep the six malls, strata, wishes and relics from v1, and a total length of at least 24 hours (verify with a rewritten balance bot). You have creative liberty.

How to work:
- Finish M1, then go milestone by milestone (M2–M6 in `PIVOT_FPS.md`).
- Reuse the v1 pieces the plan lists (procedural meshes, shaders, mall builder, fountain, audio synth, UI kit, save, test flags) instead of rewriting them.
- After each milestone: batch build, run `-autotour` screenshots (tour updated for first person) and an updated `-uitest`, look at the screenshots, and fix what's wrong. Then commit (you're authorized to commit each verified milestone; ask before committing anything unverified), and continue.
- Follow the CLAUDE.md rules: vertex alpha = emission; no commands that trigger permission prompts (file changes through Write/Edit only, no rm, no `sed -i`, no `cd` chains); never kill processes.
- Give me a one-line status update at each milestone.
- At the end, update HANDOFF.md, CLAUDE.md, DESIGN.md and README.md for the new game.
