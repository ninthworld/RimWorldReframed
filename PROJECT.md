# Reframed UI development project

Current source and installed development version: **0.23.1**. The authoritative local checkout is also the installed mod; one integrator owns its edits, builds, deployment and game session. A source export is a separate curated artifact. No public repository, release or external publication is implied by local preparation.

- [Onboarding and build instructions](README.md), [contribution guide](CONTRIBUTING.md) and [publication policy](Docs/UI-Reworked/07-github-readiness.md).
- [Current audit](Docs/UI-Reworked/Audit32/README.md), [UI work inventory](Docs/UI-Reworked/Audit32/ui-todo.csv) and [acceptance backlog](Docs/UI-Reworked/visual-completion.md).
- Local continuity: [AGENTS.md](AGENTS.md), [handoff](Docs/UI-Reworked/HANDOFF.md), [execution state](Docs/UI-Reworked/execution-state.json).

Read current records and actual Git status before resuming. Installed startup verification, offline tests and live acceptance are separate evidence. No complete source-fidelity or global window-coverage acceptance is claimed. Editing workers use isolated worktrees outside Steam's Mods directory and private outputs. The default build writes directly to this checkout's `Assemblies`; use `-OutputDir` for a private build.

Edit authoritative specifications under `Docs/UI-Reworked`; regenerate the illustrated index with `Tools/build-specification.py` only when the local reference archive is available. Preserve original evidence, observed/measured/inferred distinctions and unfinished gates. Original contributions use MIT; reference/media exclusions are recorded in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md).
