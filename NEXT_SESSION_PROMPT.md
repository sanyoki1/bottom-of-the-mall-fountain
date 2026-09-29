Paste this into a new Claude Code session opened in `C:\Users\niczm\Bottom of the Mall Fountain`:

---

Continue Wish Extractor (Unity 6000.4.2f1, this repo). Read `CLAUDE.md`, then `HANDOFF.md`, then `PIVOT_FPS.md` if you need the settled design decisions (don't ask me those again).

Where things stand: M1–M5 are on `master`. M6 and M7 are on the branch `claude/wish-extractor-m6-verify-m6ww7w`, verified in Unity at `99db1e5`. On top of that, session 7 (a cloud session with no Unity license) acted on my first playtest: a quiet HUD (no cards, one-line goal, contextual depth/hotbar/controls, one toast at a time), aim assist plus hold-E-to-sweep for picking up coins, and Officer Doug's statue check (red light, green light: freeze while he looks through his binoculars, or get fined). It compiles with Unity 6's own compiler and the balance is re-fitted, but **nothing from session 7 has been built or run in Unity yet**.

What's next (`HANDOFF.md`, "NEXT"):
1. Build, run `-autotour` (61 shots) and look at every screenshot, especially the HUD in the early shots and `31_guard_look`, `32_guard_fine`, `37_statue_tip`. Nothing new has been rendered: fix positions, sizes, alphas and Doug's pose.
2. Run `-uitest` (expect 123/123), then `-loadtest`.
3. Commit session 7 as verified, then ask me to play the first 20 minutes again.
4. Merging into `master` and the parallel branch `claude/gracious-feynman-izvxcg` are my calls: ask me before touching either.

How to work:
- You're authorized to commit a verified milestone (build + tour + uitest pass); ask before committing anything unverified.
- Follow the CLAUDE.md rules: vertex alpha = emission; mesh particles need byte vertex colours; keep the HUD quiet; after any price, rate or multiplier change re-fit with the balance bot; no commands that trigger permission prompts (file changes through Write/Edit only, no rm, no `sed -i`, no `cd` chains); never kill processes.
- If the project is open in the Unity Editor, build in a worktree (CLAUDE.md) instead of asking me to close it.
- Give me a one-line status update when each step is done.
