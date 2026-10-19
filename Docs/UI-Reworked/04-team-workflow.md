# Team workflow and continuity

## Current delivery policy — user direction, 17 October 2026

Implement a complete, bounded feature batch from the reference, then run a comprehensive review and correction campaign before moving on. The user's latest clarification is explicit: implement, inspect the entire result, list all problems, fix those problems together, then inspect again for unresolved and newly discovered issues. Reducing the number of test cycles must not reduce the depth of each review. A functional smoke pass followed by an indefinite visual backlog is not this workflow.

Treat game launches, captures and testing as expensive. Batch independent features and known fixes into one integrated build and shared game session. Reuse each saved capture to inspect all visible components and details, not just the feature that prompted the capture. Finish the discrepancy inventory before starting the correction pass; fix related findings together rather than repeatedly launching the game after individual cosmetic adjustments. Independent reviewers can analyze the same evidence without additional game sessions. The lead remains responsible for the complete review, rather than relying on the user to discover visible mismatches.

Use focused build/static checks during implementation. Preserve native loading, confirmation and data safety. Known reference details belong in the implementation: panel shape and proportions, hierarchy, typography roles and brightness, icon silhouettes, grouping, colors, borders, shading, spacing and navigation. Include fine details in the review and consolidated fixes; small size alone is not grounds for silently deferring a defect. Unknown source properties and deliberately postponed work require explicit reasons, owners and a next checkpoint. Exhaustive pixel certification, broad compatibility matrices and performance/soak runs remain planned milestones unless a concrete regression warrants them. They do not replace the comprehensive screen review required in every implementation campaign. The precise loop and finding record are in [the test regimen](02-test-regimen.md#required-implementation-and-correction-campaign).

Functional progress and visual completion are independent gates. The [project register](project-register.csv) records functional status/evidence, visual status/backlog/evidence and final acceptance for all 21 projects; the durable [visual completion backlog](visual-completion.md) links source-specific design work and required comparisons to each project. Passing a functional test or integrating a usable increment does not imply that its layout, colors, type, icons, states or proportions match the concept. Source-shown states require matched-scale comparison and review; states absent from the source remain explicitly inferred and follow the inference register. At this checkpoint W01/W02, W03/W19 and W04/W05 have scoped functional evidence only. No project's visual completion or final acceptance is recorded. The current correction batch covers W01/W02/W04/W06/W07. W06 uses placeholder headings only; a separate grouping system was rejected. See Features/Batch04 for the current evidence and pending checks. Existing calibration artifacts remain available; they are not to be regenerated automatically. This policy supersedes stricter sequencing language in the original specifications and test plan wherever it would block incremental feature delivery.

Revision 3 · 17 October 2026 · Standing operating structure for future implementation

The team should parallelize independent work while preserving a single coherent design and an identifiable tested build. The lead owns integration and design consistency. Workers receive bounded work orders and explicit ownership of files or modules. The [extension policy](05-inferred-design-policy.md) governs designs that the concept does not show.

## 1. Roles and initial model routing

| Role | Initial recommendation | Owns |
|---|---|---|
| Lead and integrator | GPT-6 Astra, extra high | Architecture, work-order contracts, shared interfaces, cross-module decisions, integration and final evidence review |
| Feature worker | GPT-6.1 Sol, high | A substantial bounded module, its tests and evidence |
| Bounded support worker | GPT-6 Luna, medium or high | Mechanical measurements, inventories, repetitive implementation against a fixed contract, narrowly scoped tests |
| Independent reviewer | Sol for ordinary changes; Astra for difficult logic or visual ambiguity | Findings against the specification and extension decisions; verification that does not merely repeat the author's assumptions |

These are starting recommendations, not measured performance rankings for this project. Compare total time and usage to an accepted result, including corrections. Route ambiguous typography, Harmony behavior and cross-module architecture to a capable model. A detailed prompt does not turn an unresolved design question into a mechanical task. Official guidance supports choosing models by task complexity and testing the tradeoffs: [model selection](https://developers.openai.com/api/docs/guides/model-selection).

At the time this structure was documented, the session allowed **four concurrent agents including the lead**. A useful configuration is one lead, two feature workers and one reviewer/investigator. Recheck actual capacity and model availability when resuming; this observation is not a permanent product limit. A cheaper model does not itself increase concurrency. Do not change models or spawn additional user-visible chats merely to fill slots.

The user's standing preference is to delegate useful independent work as much as practical. Do not delegate dependent fragments whose coordination costs exceed their value. During implementation, explicitly assign an available model through the supported agent controls and give the worker a self-contained work order; do not assume it inherited the entire conversation.

## 2. Division of work

| Stage | Parallel candidates | Single-owner responsibilities |
|---|---|---|
| Evidence and foundations | Reference measurements, game API audit, fixture/capture development, notification inventory | Shared typography decisions, design tokens, rendering interfaces and ownership of native surfaces |
| Features | Independent panels after dependencies stabilize; independent data adapters with defined contracts | Shared pawn header, global layout/input model, shared settings/schema changes |
| Verification | Code review, tests in isolated directories, analysis of saved screenshots/diffs | Installed build, live game window, shared test save/session and final integration verdict |

Use the dependencies in the work-order plan as a ready queue. An order becomes dispatchable when its specific inputs and interfaces are ready. There is no requirement to wait for every unrelated foundation to finish. Conversely, do not launch all 21 feature projects against changing shared controls.

Example: once P14's pawn header, tab interface and data ownership are established, P16 Needs, P17 Social and P18 Gear can proceed independently. If a worker needs a header change, it requests a contract revision from the lead and continues unaffected work. Three private header rewrites are not an acceptable way to unblock the panels.

## 3. Git and worktree policy

The user selected the installed mod directory as the authoritative project: `C:\Program Files (x86)\Steam\steamapps\common\RimWorld\Mods\RimWorldReframed`. Git is initialized there on `main`. Source, tests and the documentation under `Docs/UI-Reworked/` belong to this repository. The existing Build.ps1 writes directly to this checkout's Assemblies folder, so a build in the primary checkout is a live mod update. Generated DLLs, caches, build intermediates and temporary outputs are ignored. Game and Harmony assemblies are referenced read-only, not committed. The pre-Git prototype is preserved in a ZIP with a hash manifest; see [repository setup](06-repository-setup.md). A remote backup is optional and none has been configured.

Record the existing prototype as an experimental starting point, not an accepted replica. Preserve it before structural changes. Keep the integration branch releasable to the project's current acceptance level; do not claim that early foundations already meet the whole-product specification.

For each editing work order:

1. Record its base integration commit and create or select a suitable isolated worktree outside the Steam Mods directory, so the game cannot discover duplicate worker copies. Keep `main` checked out in the installed primary directory. Create a task branch for each editing order; record a detached HEAD explicitly if a managed worktree starts that way.
2. Give the worker the exact absolute worktree directory. Subagents share the underlying filesystem by default; spawning one does not automatically create isolation.
3. Use worktree-local build, package and test-output directories. Reference the installed game's assemblies read-only. Do not send worker output directly into Steam's Mods directory.
4. Limit edits to the assigned paths and agreed interfaces. Any exception is coordinated through the lead and logged before overlapping edits proceed.
5. Return a small, reviewable change with commit identifiers, tests, known gaps and evidence. Do not rewrite another worker's history or discard its changes.
6. The lead integrates one change at a time, resolves conflicts deliberately and runs the affected suite on the combined result. A conflict-free merge can still have semantic or visual regressions.
7. Rebase or update remaining workers onto the accepted integration point when their dependencies require it. Archive worktrees only after needed work and evidence are preserved.

Worktrees isolate working copies; they do not isolate the running game, external files or shared output paths. Codex's [worktree documentation](https://learn.chatgpt.com/docs/environments/git-worktrees) describes the Git requirement and separate checkouts. No production save or game installation becomes a worker scratch space.

## 4. File ownership and shared resources

Before dispatch, the lead records each active worker, order, base commit, worktree, owned paths and shared-resource access in [execution-state.json](execution-state.json). This is an explicit coordination record, not an operating-system lock. Workers must honor it; the lead resolves overlapping requests.

Only the integrator may edit the installed primary checkout, build its Assemblies output or control the shared live game during a coordinated run. Integrate reviewed worker commits into the primary checkout, build there, then restart RimWorld before checking DLL changes. A source edit or Git merge alone does not update the loaded DLL. Workers can prepare fixtures, test scripts and analyze already captured evidence. Independent game instances would require separate savedata, output locations and explicit allocation first; they are not assumed available.

Shared primitives, font/icon registries, global layout, input routing, settings migrations and approved baselines always have one current editing owner. Others can review or propose patches without editing those files concurrently. An ownership transfer records the handoff point and outstanding changes.

If an agent disappears, do not immediately overwrite its directory. Inspect its actual Git state and outputs, preserve useful work, then explicitly reassign ownership. Agent names and IDs are temporary; work-order IDs and repository state carry continuity.

## 5. Worker instruction contract

Every worker receives the following information directly or through short, specific file links:

- Order ID, purpose, scope and exact worktree/base commit.
- Owned files/modules; shared interfaces and their versions; resources it may not mutate.
- Relevant project requirements, source crops and data fixtures, including known discrepancies.
- Required interaction states and live native behavior; applicable extension decisions and fallback rules.
- Acceptance tests, expected evidence and affected regression families.
- Permitted implementation discretion and the situations that require coordination.
- Required return: changed files/commits, actual test results, screenshot/diff paths, unresolved questions, suggested next step and clean or dirty working state.

Routine choices inside the established design system can be inferred and implemented without repeatedly asking the user. The worker records the rationale and the lead checks consistency. Shared-contract changes, contradictory interpretations and actions that alter gameplay semantics must be raised to the lead. The lead asks the user only when a material ambiguity cannot be resolved from the stated goal and existing decisions.

Instructions should be specific to the task. Do not require every worker to reread the full project history or all reference boards before a small edit. The goal is enough context for correct work, not maximum prompt length.

## 6. Integration and completion

An order progresses through: planned → ready → active → review → integrated → verified → accepted. It can also be held with a named dependency. Record the last completed gate and remaining obligations; “code written” is not completion.

Before accepting a worker's result, the lead verifies:

- Scope and ownership were respected; no hidden native feature or unresolved gameplay mutation was introduced.
- The implementation matches the relevant reference or recorded inferred design.
- Tests and captures correspond to the returned build/commit, not an earlier run.
- The combined build passes affected component, shared-primitive, input and persistence regression.
- New assets and tokens have stable IDs; extension decisions and work-order state are updated.

Only the lead promotes a new visual baseline, with a reason and before/after/diff evidence. A changed screenshot is never approved solely because a worker regenerated the golden. One integrated build is produced in the primary checkout for the shared in-game test; record its commit, DLL hash and game restart. Verify that a failed build did not leave an older DLL which is then mistaken for the new result.

## 7. Stop and resume procedure

Write a checkpoint at each accepted order and before a planned stop. If interrupted unexpectedly, the next lead reconstructs it from files and tool state before continuing.

**Before stopping:** gather worker status; save or commit changes without discarding unrelated work; record worktree/branch/commit and dirty files; record tests actually run and where the evidence lives; record active game/process/resource ownership; update execution-state.json and [HANDOFF.md](HANDOFF.md). Preserve unfinished worktrees and list the next smallest concrete action. Do not mark a feature accepted merely to leave a clean handoff.

**When resuming:** read HANDOFF.md and execution-state.json, then only the relevant order/specification sections. Verify repository root, branch/commit, dirty files, worktrees and actual live agents/processes. Treat stale ownership entries as claims to verify. Recheck model/concurrency availability. Confirm which DLL is installed before interpreting a game screenshot. Resume from the earliest unfinished dependency instead of repeating completed work.

Do not assume that a prior agent, local preview server, game session or tool handle still exists. Do not rerun expensive suites without a new change or missing evidence; do rerun checks whose environment, build or inputs have changed.

## 8. Authoritative project records

| Record | Purpose |
|---|---|
| 01-design-specification.md and source manifests | What is observed/measured and the reconstruction scope |
| 05-inferred-design-policy.md | How absent states are designed consistently |
| inference-register.csv | Open and adopted extension decisions, linked to owning projects and tests |
| 02-test-regimen.md | Acceptance gates, capture discipline and regression obligations |
| 03-work-order-plan.md | Foundation/feature sequencing and the order template |
| 04-team-workflow.md | Team roles, ownership, worktrees and handoffs |
| execution-state.json | Current phase, active assignments and shared-resource ownership |
| HANDOFF.md | Concise human-readable checkpoint and next actions |
| decision-log.md | Durable cross-project decisions and their rationale |

The editable documents in the installed repository's `Docs/UI-Reworked/` directory are authoritative. The original chat's outputs folder is an exported review copy after this setup; refresh it from the repository, never edit both independently. index.html is generated from the documents by `Tools/build-specification.py`. Rebuild the report after changes; do not edit HTML alone. The project register must preserve real statuses when the report is regenerated. The original v1 ZIP remains a historical snapshot; newer bundles receive new revision names.
