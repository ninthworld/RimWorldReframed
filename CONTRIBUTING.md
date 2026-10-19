# Contributing

Start with [README.md](README.md), the [current audit](Docs/UI-Reworked/Audit32/README.md), [UI work inventory](Docs/UI-Reworked/Audit32/ui-todo.csv) and [repository policy](Docs/UI-Reworked/07-github-readiness.md). Choose a bounded item, state its scope and preserve native behavior and fallback paths.

For the authoritative installed checkout, follow `AGENTS.md`, the current handoff/execution state and team ownership. One integrator owns live builds, deployment, the game and saves. Editing workers belong in isolated worktrees outside Steam Mods. Build privately with `Build.ps1 -OutputDir work/private-build`; the default writes directly into this checkout's `Assemblies` directory.

## Development checks

Use a stable .NET 10 SDK accepted by `global.json`. Run `Tools/Test.ps1` from PowerShell; it runs the explicit pure projects in `Tools/pure-tests.json`, fails on the first error and requires no game installation. The runner restores these package-free suites from an empty local feed and uses a private compiler process; no NuGet connection or compiler-server session is needed. Update the manifest when adding a project; every test project must be classified as included or excluded. The CI matrix exercises these checks on Windows and Linux. This verifies the pure layer, not game/runtime portability.

Build the `net472` runtime on Windows using your own RimWorld 1.6 assemblies. Set `RIMWORLD_DIR`, pass `-RimWorldDir`, or supply `-ManagedDir` when the managed assembly directory differs. Do not commit game, Unity or Harmony DLLs, caches, saves or logs. NuGet restores the compile-time Harmony/reference-assembly dependencies. Do not upload game assemblies to CI artifacts.

Native metadata CLI projects (`Tests/CaptureContracts/NativeMetadata` and `Tests/WorkPanelNativeContracts/NativeAudit.csproj`) take game assembly and output paths. `InspectorExtrasContracts` and `Selection17Health` take freshly extracted JSON from the actual installed version. Their checks cannot be substituted with stale or invented JSON. Native PowerShell seam/member checks also require the installed game and may require its existing cached development dependencies. `Selection16Gear` runs its pure checks without arguments; passing native evidence opts into additional checks.

Reference checks (`Tools/reference_audit.py`, `Tools/icon_reconstruction.py`, `Tools/font_calibration.py`, `Tests/IconContracts` and `Tests/TypographyContracts`) and documentation regeneration use the local excluded `Docs/UI-Reworked` image archive. They are separate from public CI. Python tooling uses `Tools/requirements-visual.txt` or `Tools/requirements-docs.txt`; offline SVG export scripts use Node.js with `sharp` installed on the module search path. Rebuilding assets is optional for ordinary C# changes. Preserve attribution and distinguish authored reconstructions from extracted or measured reference pixels.

## UI changes and reports

Every window/panel, including nested and secondary panels, must animate on opening **and closing** and use a slightly translucent background. Preserve native close/input cleanup; motion is presentation only. Test reversal, clipping, input while moving, modal stacking and a visible backing beneath translucent fills. Keep older uncovered surfaces explicit.

Report expected and actual behavior, reproduction steps, version/commit, UI scale/resolution, enabled DLC/mods, and opening/closing behavior where relevant. Use synthetic fixtures or a disposable test save. Review screenshots before sharing and remove personal information; do not attach saves or proprietary binaries. Identify what is observed, measured or inferred. A build, pure checks or startup cannot establish live visual/functional acceptance.

Changes should include a concise problem/behavior description, affected checks and remaining limitations. Use the pull request template. Include original contributions under MIT; list any independently sourced material and its permission in `THIRD_PARTY_NOTICES.md`. A link or attribution alone does not establish redistribution permission for third-party images. Preserve the local evidence archive and history; prepare public source using the curated export policy.
