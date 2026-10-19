Run pure contracts with `dotnet run --project Tests/KnowledgeMenuContracts/KnowledgeMenuContracts.csproj`.

Build privately with `dotnet build Source/ReframedUI.csproj -p:OutputPath=../work/bottom31-build/ -p:NuGetAudit=false`.
Run installed-member/native-IL checks with Windows PowerShell:
`powershell.exe -NoProfile -File Tests/KnowledgeMenuContracts/Test-InstalledMembers.ps1 -ModAssembly work/bottom31-build/ReframedUI.dll`.

The pure suite covers hidden/status/action gating, narrow-body geometry, clipped-row
input, bounded graph zoom/pan and cursor anchoring, finite coordinate handling,
and the same generic native parent/subquest traversal used by the adapter.
The installed suite checks exact reflected members and retained native confirmation,
acceptance/filtering, original graph position/developer actions, and strict UTF8 decoding of every KnowledgeMenus source. Neither is live or visual acceptance.
