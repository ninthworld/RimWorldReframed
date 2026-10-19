# Reframed UI

A RimWorld 1.6 UI overhaul inspired by [RimWorld UI Reworked by yarich SA](https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked). Independently implemented; this is an unfinished development build with no official affiliation to the concept designer or Ludeon Studios.

Current version: **0.23.1**. Main menu and save browsing, new-game setup, colony HUD, pawn inspection, Work, Architect, notifications, compact colony menus and World UI have implementation passes. Research has a node graph; History has a recorder chart. Startup and scoped offline checks are recorded. Complete visual and functional acceptance, compatibility, localization, small-screen behavior and older window animation/transparency coverage remain open. Passing CI does not establish in-game acceptance.

- [Current audit and development priorities](Docs/UI-Reworked/Audit32/README.md) and [UI work inventory](Docs/UI-Reworked/Audit32/ui-todo.csv).
- [Repository readiness and source export policy](Docs/UI-Reworked/07-github-readiness.md).
- [Contribution guide](CONTRIBUTING.md), [license](LICENSE) and [asset/reference notices](THIRD_PARTY_NOTICES.md).

## Install a runtime package

Use a prepared runtime package containing `About`, `Assemblies`, `Defs`, `Patches` and `Textures`. Put its folder under RimWorld's `Mods` directory. Enable **Harmony** (`brrainz.harmony`) before **Reframed UI** (`chris.reframedui`) and restart RimWorld after a DLL update. A source checkout does not include the compiled mod. Modules can be disabled in Mod settings when investigating conflicts.

The current implementation is developed against RimWorld 1.6 on Windows. Other operating systems and broad mod/DLC combinations have not been accepted. Keep backups of saves used for testing.

## Build on Windows

Install a .NET 10 SDK and use your own RimWorld installation. `global.json` accepts stable 10.0 feature bands from 10.0.100 onward. NuGet restore obtains Harmony 2.3.6 and .NET Framework reference assemblies; game/Unity DLLs are referenced locally and are never redistributed.

```powershell
# Private build; does not deploy to the installed mod.
.\Build.ps1 -RimWorldDir 'D:\Games\RimWorld' -OutputDir 'work\private-build'

# Alternatively set the game location for this shell.
$env:RIMWORLD_DIR = 'D:\Games\RimWorld'
.\Build.ps1 -OutputDir 'work\private-build'
```

Without `-OutputDir`, `Build.ps1` writes to **this checkout's `Assemblies` directory**. In the authoritative installed checkout that is a live mod update; coordinate with its integrator, close the game and restart after intentional deployment. Relative output directories resolve from the repository root. `-ManagedDir` overrides the usual `RimWorldWin64_Data/Managed` location when needed. This is a Windows build workflow, not a cross-platform support claim.

## Run checks without RimWorld

```powershell
.\Tools\Test.ps1
```

The explicit [pure test manifest](Tools/pure-tests.json) runs .NET 10 policy, geometry and contract executables without game DLLs, native metadata or reference boards. CI uses the same command on Windows and Linux and does not build the game-dependent runtime mod. Native metadata tools, `InspectorExtrasContracts` and `Selection17Health` require an installed game or freshly extracted evidence and are deliberately separate. Source-image/icon/font comparison tests require the local reference archive; see [CONTRIBUTING.md](CONTRIBUTING.md).

Original code and original project assets use MIT. Third-party boards, captures, reference crops, historical extracted glyphs and game artwork are excluded from that grant. The full local evidence archive and authoritative Git history must not be published as an all-MIT source repository; use the documented curated source export.
