Paste this into a new Claude Code session opened in `C:\Users\niczm\Bottom of the Mall Fountain`:

---

We're pivoting Wish Extractor (Unity 6000.4.2f1, this repo) from an overhead auto-clicker into a **first-person fountain factory game like Find The Needle**. Read `CLAUDE.md`, `HANDOFF.md` and `PIVOT_FPS.md` first; `PIVOT_FPS.md` is the design and milestone plan and is the source of truth.

Summary of what I want: first-person; pick up coins by hand one at a time and carry them to a coin deposit kiosk; buy equipment that carries more per trip; a tech tree that unlocks tools, generators, machines and conveyor belts to build automated assembly lines; NPC shoppers constantly throw coins into the fountain, and fountain beautification upgrades make them throw more often and better things (nickels → dimes → quarters → dollars → gold → diamonds → ridiculous items). Lots of humour. Keep the six malls, strata, wishes and relics from v1 and the ≥ 24-hour total length (verify with a rewritten balance bot). You have creative liberty.

How to work:
- Ask me the "Decisions to confirm" in PIVOT_FPS.md up front, then work milestone by milestone.
- Reuse the v1 pieces the plan lists (procedural meshes, shaders, mall builder, fountain, audio synth, UI kit, save, test flags) instead of rewriting them.
- After each milestone: batch build, run `-autotour` screenshots (update the tour for first person) and an updated `-uitest`, look at the screenshots, fix what's wrong, then continue.
- Follow CLAUDE.md rules (vertex alpha = emission, no commands that trigger permission prompts, never kill processes, ask before git commits).
- At the end, update HANDOFF.md, CLAUDE.md, DESIGN.md and README.md for the new game.
