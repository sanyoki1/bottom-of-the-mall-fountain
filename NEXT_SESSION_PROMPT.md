Paste this into a new Claude Code session opened in `C:\Users\niczm\Bottom of the Mall Fountain`:

---

Continue the first-person pivot of Wish Extractor (Unity 6000.4.2f1, this repo). Read `CLAUDE.md`, then `HANDOFF.md`, then `PIVOT_FPS.md`. `PIVOT_FPS.md` is the design and milestone plan and the source of truth. Its "Decisions" and "Spec decided in session 2" sections are settled, so don't ask me those again.

Where things stand: milestones M1–M5 are done, verified and committed (`f37e470` … `053f424`). M6 is in progress as **uncommitted work that compiles and passes the existing uitest (85/85)**: hazards (Officer Doug, Chad the rival diver), the Security tech branch, the goldfish, footsteps and more jokes, a rewritten balance bot with a `fit` command, and a round of economy fixes. `HANDOFF.md` ("NEXT: finish M6") lists exactly what's done and what's left. Build on that WIP; don't redo it.

What's left for M6:
1. Run the fitter (`dotnet run -c Release --project Tools/BalanceSim -- fit --apply`), then the 40 h engaged and casual reports. The engaged bot must total at least 24 hours (unfitted it's 11.1 h). Read the per-mall logs for runaway income or dead stretches, and fix the economy if needed, not just the crust sizes.
2. Check the early pacing against `PIVOT_FPS.md`.
3. Extend `-autotour` and `-uitest` to cover the hazards and the goldfish; build, run tour, uitest and loadtest, look at every screenshot, and fix what's wrong.
4. Commit M6 once verified.
5. Update HANDOFF.md, CLAUDE.md, DESIGN.md and README.md for the new game (HANDOFF lists the lessons to add to CLAUDE.md).

What I want (unchanged): a first-person fountain factory game like Find The Needle. Pick up coins by hand and carry them to the COIN-O-MATIC 3000; buy equipment that carries more per trip; a Maintenance Terminal tech tree unlocking tools, generators, machines and conveyor belts; NPC shoppers throwing ever-better (and more ridiculous) things as the fountain gets fancier; lots of humour; hazards always on; the six malls, strata, wishes and relics from v1; at least 24 hours total, verified by the balance bot. You have creative liberty.

How to work:
- You're authorized to commit a verified milestone (build + tour + uitest pass); ask before committing anything unverified.
- Follow the CLAUDE.md rules: vertex alpha = emission; no commands that trigger permission prompts (file changes through Write/Edit only, no rm, no `sed -i`, no `cd` chains); never kill processes.
- Give me a one-line status update when M6 is done.
