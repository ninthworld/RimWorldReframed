# Installed project and fast test loop

## Current preparation checkpoint — 19 October 2026

See [GitHub readiness](07-github-readiness.md) and [Audit32](Audit32/README.md) for the current source publication boundary. `Build.ps1 -OutputDir work/private-build` builds privately; omitting the option still writes to this checkout's live Assemblies folder. Set `-RimWorldDir`, `RIMWORLD_DIR` or `-ManagedDir` to resolve a local game installation. `Tools/Test.ps1` runs 31 explicitly classified pure .NET 10 suites without game DLLs. The original setup below is retained as historical context; its foundation/version/test details do not supersede current checks.


Revision 2 · 17 October 2026 · User-selected location

## Authoritative location

`C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RimWorldReframed`

Git is initialized on `main`. The original experimental v0.2 source is preserved in commit `d97d309551f4dcc50efd0711f63867d4c4dcb658`. Repository initialization does not mean any reconstruction feature has passed acceptance. No remote was configured. Initial setup commits are attributed to Codex using command-local identity; the user's global Git identity was not changed.

The main working copy is also the installed mod. Documentation lives in `Docs/UI-Reworked/`; `AGENTS.md` at the repository root tells resumed agents where to start. The original chat outputs are a review export, not a second editable project. Keep that distinction when continuing from another chat.

## Build and test cycle

From the project root in PowerShell:

```powershell
.\Build.ps1
dotnet run --project .\Tests\LayoutTests.csproj -c Release
```

Build.ps1 uses the existing .NET project and writes `Assemblies/ReframedUI.dll` directly under this checkout. A .NET SDK and the referenced NuGet packages are required. A first build may restore those packages. The test command runs the existing prototype layout checks; it does not implement the new comprehensive test regimen.

For an intentional live build, the integrator closes RimWorld, integrates reviewed source, runs the affected checks/build, records the commit and resulting DLL hash, then relaunches the game and loads the designated test save. Restart after DLL changes; rebuilding on disk does not reload an assembly already in memory. No deployment-copy step is required from the primary checkout. A failed build is not a new installed version.

The current setup did not rebuild or launch the game. Existing source, mod metadata and DLL bytes were checked against the pre-setup hashes.

## Worker isolation

Each editing worker receives a separate worktree outside the game's Mods directory and an exact branch/base commit. The existing relative OutputPath naturally gives each checkout its own Assemblies folder. Do not override a worker build output to the live directory. Only the integrator changes/builds the primary checkout. Git worktrees require a committed starting point, which now exists.

Keep worker commits small. Integrate sequentially into main, then verify the combined result. Separate worktrees do not protect the shared game window, saves or external output paths; those still have explicit single ownership.

## Backup and restoration

The pre-Git installed folder was archived to:

`<LOCAL_USER>\Documents\Codex\2026-10-17\i\outputs\RimWorldReframed-PreGit-2026-10-17.zip`

[prototype-baseline.json — local evidence omitted](LOCAL_EVIDENCE.md) records all original relative paths, file hashes and the ZIP hash. Generated DLLs are intentionally ignored by Git; checking out source alone does not restore an installed DLL. To return to an earlier build, rebuild the intended source commit or restore the matching preserved binary while the game is closed. Do not discard current work or extract an old ZIP over an active checkout without first preserving its changes.

Git tracks source, tests, project metadata, specifications and reference evidence. It ignores build outputs, game/Harmony DLLs, caches and temporary logs. The designer's boards under Docs are reference material for this local project; exclude Docs, Tools, source caches and repository metadata from a player-facing mod release.

## Documentation maintenance

Edit the Markdown and registers under `Docs/UI-Reworked/`, then run `Tools/build-specification.py --root Docs/UI-Reworked` with Python and the pinned dependency in `Tools/requirements-docs.txt`. The generator preserves project statuses and checks links/source hashes. Only promote visual baselines after the relevant in-game evidence is reviewed. Export the checked documentation to the original chat outputs when a refreshed shareable artifact is needed.

On resume, read AGENTS.md, HANDOFF.md and execution-state.json, then inspect `git status`, `git log` and `git worktree list`. The integration commit recorded in the handoff is the last code checkpoint; later documentation-only commits may follow. Resolve the actual HEAD instead of assuming the stored checkpoint is current.
