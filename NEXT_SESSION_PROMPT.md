Paste this into a new Claude Code session opened in `C:\Users\niczm\Bottom of the Mall Fountain`:

---

Continue Wish Extractor (Unity 6000.4.2f1, this repo). Read `CLAUDE.md`, then `HANDOFF.md`, then `PIVOT_FPS.md` if you need the settled design decisions (don't ask me those again).

Where things stand: M1–M5 are verified and on `master`. M6 (hazards, the Security branch, the goldfish, the balance bot, the economy rebalance, Bigger Chunks, fitted layers) was written in cloud sessions with no Unity license and lives on the branch `claude/wish-extractor-m6-verify-m6ww7w`: fetch and check it out. Session 5 compiled every script with Unity 6000.4.2f1's own compiler and assemblies (0 errors), fixed four bugs found by reading the code (Chad's sign was inverted, Doug fined deputies $0, the goldfish prompt, the build catalogue running off the panel), re-fitted, and measured every engaged seed at 26.2–27.0 h. **Nothing from M6 has been built or run in Unity yet.**

What's left for M6 (`HANDOFF.md`, "NEXT"):
1. Build, and fix any compile errors (none expected).
2. Run `-autotour` and look at every screenshot, especially 18 (the new six-column build catalogue) and 30–36 (Security tab, Officer Doug, Chad, the goldfish). Fix what's wrong.
3. Run `-uitest` (expect 96/96), then `-loadtest`.
4. Play the first 20 minutes and the start of a dig by hand.
5. Refresh `Docs/Screenshots` from the tour, update HANDOFF.md, and commit M6 on the branch. Ask me before merging into `master`.

How to work:
- You're authorized to commit a verified milestone (build + tour + uitest pass); ask before committing anything unverified.
- Follow the CLAUDE.md rules: vertex alpha = emission; after any price, rate or multiplier change re-fit with the balance bot; no commands that trigger permission prompts (file changes through Write/Edit only, no rm, no `sed -i`, no `cd` chains); never kill processes.
- Give me a one-line status update when M6 is done.
