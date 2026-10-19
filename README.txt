REFRAMED UI - v0.23.1 development build
RimWorld 1.6; developed and tested on Windows

Enable Harmony before Reframed UI. A source checkout requires compilation;
a runtime package contains About, Assemblies, Defs, Patches and Textures.
Restart RimWorld after updating ReframedUI.dll. Modules can be disabled
under Mod settings when investigating conflicts.

Compact colony menus, research graph, History chart, World UI and earlier
menu/HUD/inspection features have implementation passes. Startup and scoped
offline checks are recorded. Complete visual and functional acceptance,
compatibility, localization, small-screen behavior and older window motion
and transparency coverage remain open. This is unfinished software.

Build on Windows with .NET 10 and your own RimWorld installation:
  .\Build.ps1 -RimWorldDir 'D:\Games\RimWorld' -OutputDir 'work\private-build'
RIMWORLD_DIR may supply the game path. Without -OutputDir, builds write
directly to this checkout's Assemblies directory; coordinate deployment
with the live checkout integrator and restart the game after DLL changes.
Pure checks without game DLLs:
  .\Tools\Test.ps1

Read README.md and CONTRIBUTING.md for prerequisites, tests and limitations.
Current audit: Docs/UI-Reworked/Audit32/README.md and ui-todo.csv.
Publication policy: Docs/UI-Reworked/07-github-readiness.md.
Local development continuity: AGENTS.md and Docs/UI-Reworked/HANDOFF.md.

Inspired by yarich SA's RimWorld UI Reworked concept:
https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked
Independently implemented; no official collaboration or affiliation.
MIT covers original project contributions. THIRD_PARTY_NOTICES.md explains
excluded artwork and evidence. Reference boards and the full local history
are local evidence, not a player package or an all-MIT public source export.
