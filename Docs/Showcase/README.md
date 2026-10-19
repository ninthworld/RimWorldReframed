# Actual UI showcase

The README uses **actual rendered UI panels**, not illustrated or reconstructed controls. The source is the v0.23.1 game captures described below. `Tools/build-showcase-panels.py` crops the original RGB pixels, masks only the rounded outside corners, and adds transparent padding. The Work detail and roster panels are stacked together. Nothing inside a panel is repainted, rescaled or replaced; faint world detail visible through the mod's translucent panels is retained.

[Crop rectangles and hashes](panel-crops.json) record the exact source images and processing. The original full-window captures remain here as evidence, but are no longer the README gallery. The Tools GIF is unchanged. The rejected vector UI approximations were never published.

## Original RimWorld logo

`rimworld-logo.png` is the original RimWorld wordmark, unchanged, from [Epic's CDN](https://cdn2.unrealengine.com/rimworldlogocleanoutline3000px-3000x545-129817624.png). It replaces the invented mod logo. The README uses `reframed-logo.png`: the original wordmark combined with a coordinated Reframed subtitle in one transparent graphic. `reframed-logo.svg` is the editable composition and embeds the unchanged original PNG; the output scales it without redrawing it. The logo belongs to Ludeon Studios and is excluded from MIT. Ludeon's [FAQ](https://ludeon.com/blog/faq/) describes use of its logos for RimWorld-related content. This mod is independently developed and is not endorsed by Ludeon.

## Source captures

These are actual captures of **Reframed UI v0.23.1** running in **RimWorld 1.6.4871** on Windows, at UI scale **1.5**, on **19 October 2026**. They show implemented screens, not concept mockups. The main menu, storyteller, pawn overview/needs, Work, Schedule and Research screenshots retain their original 2064 × 1268 window captures.

The colony captures use an isolated, paused test profile. No save was written and no mod build or deployment was needed. The installed DLL SHA256 was `1851fcaf146b322cad9aa6a630708c897a071d5bf5e8dd3d7600b690a48915b8`, built from runtime source `d5a8099c56e9dc2850875c17c908743fc5d493f4`.

## Tools animation

[Watch the loop](tools-motion.gif) · [View the expanded panel without animation](tools-open.png)

The GIF combines **16 real opening frames and 16 real closing frames**, captured from Unity's end-of-frame render target using the mod's existing opt-in diagnostics. The C shortcut opened and closed the panel. Both sequences completed without capture errors, with shell progress reaching 1 and 0 respectively.

Frames are cropped to the Tools area (522 × 836 pixels), converted to a shared 256-color palette and encoded as a looping GIF. Transition timing follows the recorded intervals, rounded to GIF's 10 ms precision. Extra settled-frame holds make the expanded controls readable. Identical frames are merged by the encoder: the 4.16-second loop contains 13 encoded frames and is approximately 466 KiB. There are no invented frames, interpolated motion or reversed footage. Capture readback can affect timing; this is a visual demonstration, not a performance benchmark.

[The manifest](manifest.json) records media hashes, crop coordinates and the source-frame hashes/timestamps. Raw full-screen frames and machine diagnostics stay in the local evidence archive. This demonstrates the Tools panel only; it does not establish animation coverage or acceptance for every window.

## Artwork and attribution

The UI panels retain the original game and mod rendering. The original RimWorld logo is third-party artwork; no ownership or MIT grant is claimed for it.

The screenshots and animation contain RimWorld game artwork and UI content belonging to their respective rights holders, including Ludeon Studios. Those elements are **not relicensed under MIT**. Reframed's design inspiration is [RimWorld UI Reworked by yarich SA](https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked); the downloaded concept boards are not included in this showcase. See [third-party notices](../../THIRD_PARTY_NOTICES.md).
