Run `dotnet run --project Tests/WorkPanelViewContracts/WorkPanelViewContracts.csproj`.

These pure contracts exercise fractional font metrics, two-digit skill/passion
spacing, constrained tag/identity bounds, and unavailable/disabled/non-left input.
They do not claim visual or native gameplay acceptance. `WorkPanelView` returns
intents; the owning controller supplies real skill/passion, permissions, selection,
clipped-row input gating, and native callbacks. The view has no work mutations.

Assigned tags use a filled face; unassigned tags retain a fine outline and category
stripe. Inability mutes text and category. Selection uses a persistent cool border,
background and filled identity marker. These added states and category RGB values
are inferred extensions of P13; no priorities are displayed as skill numbers.
