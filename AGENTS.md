# Contributor continuity

Read `Docs/UI-Reworked/HANDOFF.md` and `execution-state.json`, then inspect actual Git status/HEAD and relevant audit entries. The checkout you are in is the source workspace; do not assume an installed game or inherited agent/process exists. Read `CONTRIBUTING.md` before building.

Use useful independent subagents with explicit ownership. Put editing workers in isolated worktrees outside any Steam Mods directory. One integrator controls shared source integration, deployment, game session, saves and baseline promotion.

Every window/panel, including secondary panels, must animate open and contract closed with a slightly translucent background. Keep native close/input cleanup immediate; motion is presentation only. Record older gaps instead of claiming global coverage. Preserve observed/measured/inferred distinctions and do not call inferred designs exact source matches.

Update the handoff and execution state at meaningful checkpoints. Edit authoritative documentation under `Docs/UI-Reworked`; preserve status/decision registers. Store disposable outputs under ignored `work/` or `artifacts/`. Never commit game/Harmony DLLs, saves, logs, credentials or reference artwork. Publication and external messaging require explicit user scope.
