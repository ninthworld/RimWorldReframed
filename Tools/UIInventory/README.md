# Native UI declaration inventory

This .NET 10 BCL-only helper reads installed game PE metadata without loading the game assembly or exporting method implementation. It writes stable CSV/JSON with no timestamps or machine paths. Run from the repository root, supplying the actual installed game directory:

```powershell
dotnet run --project Tools/UIInventory/UIInventory.csproj --configuration Release -- . 'C:/Program Files (x86)/Steam/steamapps/common/RimWorld' Docs/UI-Reworked/Audit32
```

Only this helper's `bin/` and `obj/` are built; the mod is not built or installed. Outputs are `native-ui-inventory.csv` and `native-ui-manifest.json`. The installed mod hash always comes from the specified game directory, including when the generator runs in a worker worktree. The manifest hashes all scanned Source C# files and generator inputs so mention results can be reproduced against their exact input content. No timestamp means rerunning unchanged inputs produces identical bytes.

Rows classify declarations, not styling status. Window descendants, Page/MainTabWindow subsets and both map/world inspector hierarchies are retained with abstract/root distinctions. Generic inheritance resolves through the generic definition so rename and policy dialogs are not silently omitted. Fully qualified nested type names use `+`. Families overlap; do not add their totals as unique screens.

The helper candidate sweep includes declared GUI method names, selected UI-adjacent inheritance families (gizmos/designators, pawn columns, letters, scenario parts, world objects, listings and tooltip windows), and a documented helper type-name rule for card utilities, widgets, UI helpers, stat entries and search controls. Its explicit `selection_reason` exposes why a row was included. Some are nonvisual support types; composited UI without a matching name or ancestor may be absent. This is an exhaustive declaration inventory only for the stated hierarchies in the one assembly, plus a bounded helper candidate sweep. It cannot certify all existing runtime UI.

Source hits are simple-name text occurrences with identifier boundaries, including comments and contract guards, reported as relative file/line references. **A mention is not coverage.** An inherited or global patch may affect a type with no direct mention; a guard can mention a type without replacing its UI. Root must separately inspect patch allow-list, target registration, owner/context predicates, native fallbacks and implementation to distinguish custom redesign, partial adaptation, generic shell/theme and native content.

Local `Data/*/About/About.xml` hashes and version declarations describe installed files, not ownership or activation. The common game assembly contains code for DLC not available locally. Mod assemblies, active ModsConfig and save/runtime-dependent reachability are not read. Screens created from generic message boxes, node trees, letters, immediate windows or mod callbacks require a separate flow/state inventory and live validation.

Verification: build/run this helper, inspect the category/family counts and compare CSV/manifest SHA256 before and after an unchanged rerun. Failures in metadata inheritance decoding stop extraction instead of silently dropping types. Preserve abstract classes as framework records; never count them as reachable screens. No visual or gameplay acceptance follows from a successful extraction.
