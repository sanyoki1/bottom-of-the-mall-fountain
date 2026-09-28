Paste this into a new Claude Code session on this repo (branch `claude/gracious-feynman-izvxcg`):

---

Continue Wish Extractor (Unity 6000.4.2f1, this repo), the first-person fountain factory game. Read
`CLAUDE.md`, then `HANDOFF.md` (its NEXT section and "View/UI plan" are the task), then `DESIGN.md` if
you need more. `PIVOT_FPS.md` is the plan the pivot followed; its "Decisions" and "Spec decided in
session 2" sections are settled, so don't ask me those again.

Where things stand: session 5 redesigned the late game in Core. Each mall now has a three-stage
Wonder built from factory goods. Buried finds surface from the crust, frenzies last a minute, and
wishes scale with income. Session 5 also fixed a factory tick-length bug and re-fitted the balance
to 24.9–25.2 h (engaged bot, three seeds). All of it is verified with `Tools/BalanceSim`. None of it
has a view yet, and nothing on the branch has been compiled by Unity.

What's next:
1. Build the View/UI for Wonders, finds and frenzies exactly as HANDOFF's "View/UI plan" describes:
   - the Wonder Plan easel and `TargetKind.Wonder`;
   - the Wonder structure over the fountain;
   - the HUD goal card, frenzy chip, find prompts and receipt line;
   - GameRoot feedback;
   - `EffectText` for the new TechKinds.
2. Add tour shots and uitest checks for them, and dev keys F9 (spawn a find) and F10 (fill the
   Wonder).
3. Update DESIGN.md (a "Late game" section) and README.
4. If you're on the Windows PC with Unity: build, `-autotour` (look at every screenshot),
   `-uitest -fresh`, `-loadtest`, and fix what's wrong. Otherwise pattern-match existing code very
   carefully; it can't be compiled in a cloud container.
5. If there's time: HANDOFF's open issue 1 (pallets stall income), then re-fit with
   `dotnet run -c Release --project Tools/BalanceSim -- fit --apply` and re-check the 40 h report.

How to work:
- Commit to the branch at sensible checkpoints and push (cloud containers are ephemeral). Ask me
  before merging into `master`.
- Follow the CLAUDE.md rules: vertex alpha = emission, items carry base value, nothing may depend on
  tick length, never kill processes.
- Stop at a clean checkpoint before the context gets full: commit, push, update HANDOFF.md and this
  file.
- Give me a one-line status update when each step is done.
