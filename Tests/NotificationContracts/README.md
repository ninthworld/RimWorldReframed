Run `dotnet run --project Tests/NotificationContracts/NotificationContracts.csproj`.
Run the installed seam audit with Windows PowerShell5.1:
`powershell.exe -NoProfile -File Tests/NotificationContracts/Test-InstalledSeams.ps1 -ModAssembly work/notifications-build/ReframedUI.dll`.

Contracts exercise identity collisions, zero/repeated/regressed real-time samples,
entry/removal/reversal, native order, overflow capacity, stale/moving input gates,
timestamp arithmetic and releasing outgoing references. They do not claim Unity
visual or native callback acceptance.

Integration: call `CanHandle(letters,width,maxHeight)` before suppressing the native
stack, then `Prepare` each GUI pass with monotonic real time and `Draw` at the
returned occupied height. Anchor the bounds' bottom above the issue groups. The
coordinator owns map/modal context, collision regions and native LastTopY.
Reset on disable/game-context teardown or native fallback. No queue mutation or
letter expiry policy lives in this module. Letters remain actual native objects;
oldest overflow uses the actual native BundleLetter. Unknown overridden draw/hover
methods keep the whole native stack. Unknown ordinary data uses native icon/color.

Mapping/motion dimensions and durations are inferred, not frame-measured. Timestamp
uses native arrivalTick/game ticks and thus stops while the game is paused. The
first-second English 'Just now' text requires future localization; older periods
reuse native TimeAgo/period localization. Research completion and transient message
channels remain separate native/coordinator adapters, not fake letters.

Typed incident/electrical context is optional enhancement. Three exact installed
call-site substitutions preserve original native ReceiveLetter calls. The incident
wrapper takes the actual constructed Letter and IncidentDef; the two electrical
wrappers scope only native receipt, not the enclosing explosion/fire behavior.
Their nested thread-local context restores in finally; capture is limited to core
StandardLetter with the supplied definition. ConditionalWeakTable keys keep native queued letters
identified without retaining them after native removal. Unknown custom rendering
and unknown electrical letter classes retain native/family fallback. Register the
four Notification*ContextPatch types only after the installed seam audit passes.

Electrical capture validates the call's exact normalized native Label/Text,
definition identity and LookTargets reference (native LetterMaker assigns that
reference directly). Unrelated nested receipts remain unconsumed; the actual
intended letter binds after them. The installed seam script exercises actual
uninitialized native data objects without constructors, game initialization or
Unity rendering. This is argument validation, never language/title classification.
An intentionally identical nested duplicate of all expected call arguments is a
remaining mod ambiguity; it changes an icon only, never native gameplay or lifetime.
