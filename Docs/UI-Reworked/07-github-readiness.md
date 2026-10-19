# GitHub preparation and source publication boundary

19 October 2026 · AUDIT32 · source published on explicit user request

The public repository is [ninthworld/RimWorldReframed](https://github.com/ninthworld/RimWorldReframed), with default branch `main` rooted in verified source import `275816bb521269879ce5b85774ce3c0724e7a5a6`. Only the separate source export was pushed. The first [Windows/Ubuntu CI run](https://github.com/ninthworld/RimWorldReframed/actions/runs/39284624015) passed on both platforms, verified during the README showcase update on19 October2026; the [original publication receipt — local evidence omitted](LOCAL_EVIDENCE.md) preserves its earlier queued state. The sections below describe the preparation boundary and repeatable export process.

The project is prepared as a source repository for continued development. The existing authoritative installed checkout and its complete local evidence/history are preserved. A separate allowlisted source snapshot is the intended publication input; **do not push all branches/tags or the original `.git` directory** as a substitute for that snapshot.

## Included development support

- Current v0.23.1 onboarding, build requirements, contribution workflow and known limitations.
- MIT license for original code/assets, with separate reference/dependency attribution and exclusions.
- .NET SDK selection, explicit private build output, installed-game path override, and a reproducible pure-test runner.
- GitHub CI for tests that do not need game DLLs; UI-gap/bug reporting and pull-request templates. Hosted CI has not run before publication.
- The [remaining UI audit](Audit32/README.md), [35-group TODO](Audit32/ui-todo.csv), metadata discovery tool, source and native inventory hashes, textual specs and decision/status registers.
- Source export and runtime-package allowlists. No automatic publishing or deployment workflow.

## Repository audit

Before preparation, the tracked tree held 1,598 files / 886,155,800 bytes (about 845 MiB). The Docs corpus accounted for 883,838,006 bytes. Across all local refs, 359 commits and 2,834 unique blobs were inspected; largest blob 5,110,442 bytes. No tracked or reachable DLL/save/archive filename was found. Thirteen tracked historical `.log` files remain in the original local history despite the ignore rule.

A heuristic scan of 2,249 reachable non-image blobs found no private-key or common credential-pattern hits. This is a bounded scan, not a guarantee. Historical logs include local machine paths and hardware details. The fresh source snapshot omits these logs and machine execution history.

Downloaded concept boards, supplied reference media, derived crops and old copied glyph rasters occur in the local history. The user's MIT choice covers original contributions and does not grant rights in those third-party files. They stay local. Current authored SVGs and their runtime exports are included with transparent attribution; see [third-party notices](../../THIRD_PARTY_NOTICES.md). Original reference art is not shipped in player packages.

## Create a fresh source snapshot

Install the pinned documentation dependency in an environment of your choice:

```powershell
python -m pip install -r Tools/requirements-docs.txt
python Tools/prepare-github.py --output work/github-ready/RimWorldReframed
```

Use a new empty output directory; the exporter refuses to delete or replace an existing export. It selects source/tests/tools, authored runtime assets, onboarding and curated text docs. The user's requested README showcase is a narrow media exception: only files explicitly named in `Tools/showcase-files.json` are included, with [provenance](../Showcase/README.md) and separate game-art attribution. The named original RimWorld logo is also included for the user-requested README header, with third-party ownership preserved; it is documentation only. Other gameplay captures, Git history, reference rasters, game logs, raw native implementation/IL extracts, saves, DLLs and build caches stay local. Source/asset bytes are unchanged. Personal home paths in exported Markdown are replaced with a local-path marker. Missing local evidence links lead to a clear omission notice; no source-fidelity or past acceptance result is fabricated.

`SOURCE_SNAPSHOT.json` records the input commit, dirty-state flag, exclusions and a hash for every exported file. `HANDOFF.md`, execution state and contributor instructions become portable source-workspace records. Local source evidence remains authoritative here; after work begins in the publication repository, deliberately choose an integration workflow rather than independently editing two copies.

For deliberate updates to the existing public source checkout, refresh hashes for the curated changed/new files and record the update's source commit in a `revisions` entry. Preserve the original import commit and historical preparation flags. The manifest verifies the current curated file set; its `published: false` records the export's original preparation state, not the current GitHub visibility. Keep the public Git history rooted in its source import.

The source-only illustrated report is rebuilt with `--source-only`, reporting **zero reference images verified**. The local illustrated report continues verifying the preserved original image hashes. Reference/pixel/font fixture tests still require local evidence and are outside pure CI. The source snapshot can build/test without downloading concept art.

Before publishing, verify the snapshot independently:

```powershell
Set-Location work/github-ready/RimWorldReframed
pwsh -File Tools/Test.ps1
.\Build.ps1 -RimWorldDir 'C:\Games\RimWorld' -OutputDir '.\work\private-build'
```

The exporter produces files only. For this preparation the integrator initialized the final snapshot as a separate local Git repository on `main`, verified it and created an initial import commit. The user subsequently authorized public publication; `origin` now points to `https://github.com/ninthworld/RimWorldReframed.git` in that separate repository. When repeating the export, perform initialization explicitly after verification and reconcile later source changes deliberately. Its initial commit is a source import, not the original private evidence history. Do not use `git push --mirror` from the installed checkout.

## Runtime package boundary

Build and test a candidate privately before any intentional installation. To stage only player files:

```powershell
python Tools/package-mod.py --assembly work/private-build/ReframedUI.dll --output artifacts/player-package
```

The package includes `About`, `Defs`, `Patches`, `Textures`, the explicitly supplied `ReframedUI.dll`, README/license/notices and a hash manifest. It excludes Docs, source, SVG masters, tests, tools, repository metadata, saves and all other DLLs. Harmony remains an external dependency. Packaging is not release acceptance and does not publish or install anything.

## Validation and remaining gates

Exact local results are in [Audit32/verification.json](Audit32/verification.json). The installed DLL stays byte-identical through preparation and publication. Pure checks establish their stated policy/geometry contracts; private compilation establishes buildability with the locally installed game. Neither establishes complete UI/gameplay compatibility or visual fidelity. All previous acceptance gates remain open. Repository creation/source push and the initial hosted Windows/Ubuntu pure-test run are verified.
