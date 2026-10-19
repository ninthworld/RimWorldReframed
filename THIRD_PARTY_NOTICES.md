# Attribution and license scope

The [MIT license](LICENSE) applies to Chris's original project code, documentation and original authored assets. It does not grant rights in third-party material. “Authored reconstruction” describes the production method; it does not establish ownership of a referenced design. No blanket MIT claim is made for the authoritative local repository or its history.

## Design reference and local evidence

[RimWorld UI Reworked](https://www.behance.net/gallery/232485291/RimWorld-UI-Reworked) is a concept by **yarich SA**. Reframed UI is independently implemented and is not an official collaboration. The local `Docs/UI-Reworked/source-manifest.json` records downloaded reference boards and an attached alternative. Boards, crops, measurements containing reference pixels, comparison atlases and other third-party reference media are local evidence; no redistribution permission is recorded. Curated source exports omit these files rather than relicensing them. Textual specifications and audit records identify observed/measured/inferred status and may refer to omitted local evidence.

Current project icon masters and PNGs include authored reconstructions guided by concept silhouettes, plus inferred family extensions. The SaveBrowser asset record explicitly describes hand-authored traces; earlier copied raster glyphs were superseded but remain in local history. The Cassandra rhythm curve follows measured reference points; other curves are authored qualitative extensions. These provenance facts must be retained when reviewing redistribution scope. Original contributions are MIT only to the extent the project owns them; no third-party rights or reference permission are implied.

The original/reference material, historical extracted glyphs, gameplay screenshots/captures and logs in the full local evidence archive are excluded from the project's MIT grant. Keep the authoritative repository/history locally. A curated source snapshot excludes that evidence and does not carry the full historical object database. Review current reconstructed assets separately if a distribution requires additional permission; attribution is not such permission.

## RimWorld and Unity

RimWorld and its game artwork, fonts, assemblies and other content belong to their respective rights holders, including Ludeon Studios. Unity assemblies/content remain subject to their owners' terms. This repository references the user's installed game read-only and resolves native artwork at runtime. No game/Unity assemblies, extracted fonts or standalone game artwork should be included in source exports, CI artifacts or mod packages. Gameplay captures are evidence, not project-owned artwork. RimWorld names and trademarks do not indicate endorsement.

## Build and runtime dependencies

- [Harmony](https://github.com/pardeike/Harmony), maintained by Andreas Pardeike and contributors: MIT. The project references `Lib.Harmony` 2.3.6 for compilation; the separately installed RimWorld Harmony mod is the runtime dependency. Its assembly is not bundled.
- `Microsoft.NETFramework.ReferenceAssemblies.net472` 1.0.3 is a compile-time NuGet dependency; restored package contents remain under their own package terms and are not committed or packaged.
- Developer-only tools use markdown2, Pillow and sharp under their respective upstream licenses. Their packages are not vendored or shipped in the mod. Installing a tool dependency does not change the rights of the media processed by that tool.

See [source/package policy](Docs/UI-Reworked/07-github-readiness.md) for exclusions. Preserve this notice and MIT license with original project distributions. No public upload or release has been performed by preparing these files.
