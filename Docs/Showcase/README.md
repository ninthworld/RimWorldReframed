# Showcase captures

These are actual captures of **Reframed UI v0.23.1** running in **RimWorld 1.6.4871** on Windows, at UI scale **1.5**, on **19 October 2026**. They show implemented screens, not concept mockups. The main menu, storyteller, pawn overview/needs, Work, Schedule and Research screenshots retain their original 2064 × 1268 window captures.

The colony captures use an isolated, paused test profile. No save was written and no mod build or deployment was needed. The installed DLL SHA256 was `1851fcaf146b322cad9aa6a630708c897a071d5bf5e8dd3d7600b690a48915b8`, built from runtime source `d5a8099c56e9dc2850875c17c908743fc5d493f4`.

## Tools animation

[Watch the loop](tools-motion.gif) · [View the expanded panel without animation](tools-open.png)

The GIF combines **16 real opening frames and 16 real closing frames**, captured from Unity's end-of-frame render target using the mod's existing opt-in diagnostics. The C shortcut opened and closed the panel. Both sequences completed without capture errors, with shell progress reaching 1 and 0 respectively.

Frames are cropped to the Tools area (522 × 836 pixels), converted to a shared 256-color palette and encoded as a looping GIF. Transition timing follows the recorded intervals, rounded to GIF's 10 ms precision. Extra settled-frame holds make the expanded controls readable. Identical frames are merged by the encoder: the 4.16-second loop contains 13 encoded frames and is approximately 466 KiB. There are no invented frames, interpolated motion or reversed footage. Capture readback can affect timing; this is a visual demonstration, not a performance benchmark.

[The manifest](manifest.json) records media hashes, crop coordinates and the source-frame hashes/timestamps. Raw full-screen frames and machine diagnostics stay in the local evidence archive. This demonstrates the Tools panel only; it does not establish animation coverage or acceptance for every window.

## Artwork and attribution

`logo.svg` is an original vector wordmark and open-frame R emblem, authored for this project and covered by the [MIT license](../../LICENSE). It uses the mod's charcoal, pale gold and blue palette.

The screenshots and animation contain RimWorld game artwork and UI content belonging to their respective rights holders, including Ludeon Studios. Those elements are **not relicensed under MIT**. Reframed's design inspiration is [RimWorld UI Reworked by yarich SA](https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked); the downloaded concept boards are not included in this showcase. See [third-party notices](../../THIRD_PARTY_NOTICES.md).
