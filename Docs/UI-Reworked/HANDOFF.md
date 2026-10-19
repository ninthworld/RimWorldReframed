# Current checkpoint — v0.23.2 world-entry freeze fix

Tools preparation no longer reads play-only tab services from the new-colony world-selection entry root, including when the master switch is disabled. It resets only owned state and preserves native UI. Work closing-state cleanup uses the same context-before-service ordering. Regression coverage compiles the production Tools context code against throwing entry-root seams. Release build and all 31 pure suites pass; full live regression of both world entry paths remains open.

[Post-generation UI supplement](Audit32/post-generation-ui.md) adds six P1 tasks UI-036–041 from user screenshots. All 41 UI TODO groups remain open; this fix does not implement those redesigns.

## Previous checkpoint

# Current identity and showcase update

The active package and Harmony identities now use ninthworld.reframedui, including save-preview hooks and compatibility ownership checks. Re-enable the renamed package in existing active mod lists. Historical evidence retains observed IDs. Release build and all 31 pure contract projects pass; no new live game acceptance is claimed. The first README panel has its mouse cursor/glow removed only from an empty header region; original crop and repair provenance are preserved.

## Previous checkpoint

# Current presentation — actual UI and RimWorld Reframed branding

The README now isolates actual rendered UI panels and uses a combined RimWorld Reframed logo built from the original game wordmark. The Tools animation is unchanged. The rejected vector UI approximations were never published. See [provenance](../Showcase/README.md) and [exact crop records](../Showcase/panel-crops.json). No runtime changes or broader acceptance claims. The existing package/Harmony identity remains unchanged.

## Previous checkpoint — retained

# Continue development — v0.23.1

The public source repository now includes a visual README, original Reframed logo, seven actual v0.23.1 screenshots and a Tools opening/closing GIF. See [showcase provenance](../Showcase/README.md) and the explicit `Tools/showcase-files.json` media allowlist. The demonstration does not change runtime code or close broader UI acceptance gates.

The initial [Windows/Ubuntu pure-contract CI run](https://github.com/ninthworld/RimWorldReframed/actions/runs/39284624015) passed on both platforms at source import `275816bb521269879ce5b85774ce3c0724e7a5a6`. Validate subsequent commits separately. No game DLL or live game is included in this checkout.

Read [the current UI audit](Audit32/README.md), [the TODO list](Audit32/ui-todo.csv), [contribution workflow](../../CONTRIBUTING.md) and [publication policy](07-github-readiness.md). Next runtime work remains UI-004 missing settings controls, then frequent native editors and motion/material gaps. All earlier visual, functional, localization, DLC and compatibility gates remain open.

The original local history/evidence is preserved separately; see [omissions](LOCAL_EVIDENCE.md). No workers or game processes are assigned here. Verify your own checkout status and DLL before tests. Every window and nested panel must open and contract on dismissal with a slightly translucent background, while native cleanup remains immediate.
