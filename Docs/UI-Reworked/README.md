# RimWorld UI Reworked planning package

Current work: [Remaining UI audit and TODO](Audit32/README.md) · [GitHub readiness](07-github-readiness.md). Audit32 adds a complete native hierarchy inventory and 35 actionable work groups. Local evidence/history is preserved; the publication snapshot excludes it. Runtime UI implementation remains v0.23.1.

Latest feature: [Compact colony menus and research graph — local evidence omitted](LOCAL_EVIDENCE.md). v0.23.1 is installed and startup verified; user tests compact menus, research, History and animations. Live acceptance remains pending.


Revision 35 · 19 October 2026

The target is a 1:1 reconstruction of [yarich SA's Behance concept](https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked), with working RimWorld controls and repeatable in-game verification.

Start with [the offline illustrated report — local evidence omitted](LOCAL_EVIDENCE.md), or use the editable documents:

1. [Design specification and 21 component projects](01-design-specification.md)
2. [In-game acceptance and regression regimen](02-test-regimen.md)
3. [Foundation and feature work-order sequence](03-work-order-plan.md)
4. [Team workflow, model routing and stop/resume procedure](04-team-workflow.md)
5. [Inferred designs and complete UI coverage](05-inferred-design-policy.md)
6. [Current handoff and next actions](HANDOFF.md)
7. [Installed Git project and fast test loop](06-repository-setup.md)
8. [Pixel measurements and typography crops — local evidence omitted](LOCAL_EVIDENCE.md)

The [inference register](inference-register.csv) records extensions such as additional colonist groups and unshown notification families. The [notification definition inventory — local evidence omitted](LOCAL_EVIDENCE.md) is a static starting audit of the installed game, with limits recorded in its [manifest — local evidence omitted](LOCAL_EVIDENCE.md). The [decision log](decision-log.md) and [execution state](execution-state.json) preserve project continuity.

Latest build evidence: [Compact colony menus — Batch30 — local evidence omitted](LOCAL_EVIDENCE.md); installed and private builds and pending tests are distinct.

## Progress and visual completion

Functional delivery and visual acceptance are separate records. The [21-project register](project-register.csv) gives each project its functional status and evidence, visual status and evidence, remaining visual backlog, and final acceptance state. The [visual completion backlog](visual-completion.md) describes the source-specific design work and acceptance evidence still required. A usable increment or passing functional check does not establish a visual match; no project has visual completion or final acceptance recorded at this checkpoint.

The package includes four original PNG boards, the supplied alternate WebP, 15 component inspection crops, a pixel atlas, 32 exact pixel samples, nine complete scanline profiles, source hashes and region coordinates. Images remain attributed to yarich SA and are reference evidence for this analysis, not shipped mod artwork.

Exact font identity, vector paths, some rendering layers and precise animation timings remain unresolved. The user directs the team to infer unshown states from the concept's established patterns. Routine inferences proceed with recorded rationale and tests; they are not presented as recovered source facts. The extension inventory grows as new cases are discovered.

Foundation wave 2 is implemented and integrated as v0.4.2, including the user-requested native Small button labels and fractional-scale seam fix. Read the [integration report and test instructions — local evidence omitted](LOCAL_EVIDENCE.md), [foundation status register](foundation-register.csv) and [verification summary — local evidence omitted](LOCAL_EVIDENCE.md). The [wave 1 report — local evidence omitted](LOCAL_EVIDENCE.md) remains historical evidence. The test regimen remains the complete acceptance plan; only the named foundation checks and colony smoke have run. All parent foundation orders remain partial and no Behance fidelity gate is accepted. The installed Git project remains authoritative; source and runtime changes are recorded there, with the earlier prototype preserved in history.

## Current feature checkpoint

**v0.23.1 installed; audit spot checks and all unfinished acceptance gates are recorded in Audit32.** See [Batch29 — local evidence omitted](LOCAL_EVIDENCE.md) and [HANDOFF](HANDOFF.md) for exact identities and pending gates. [Batch28 — local evidence omitted](LOCAL_EVIDENCE.md) retains the previous deployment. No overall visual acceptance is promoted.
