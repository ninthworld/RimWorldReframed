[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$repoRoot = Split-Path $PSScriptRoot -Parent
$manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'pure-tests.json') -Raw | ConvertFrom-Json
if ($manifest.schema_version -ne 1) { throw 'Unsupported pure-test manifest version.' }
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'A .NET 10 SDK is required.' }
$included = @($manifest.projects)
$excluded = @($manifest.excluded | ForEach-Object { $_.project })
$classified = @($included) + @($excluded)
if ($included.Count -eq 0) { throw 'Pure-test manifest is empty.' }
if (@($classified | Sort-Object -Unique).Count -ne $classified.Count) { throw 'Duplicate manifest project.' }
foreach ($project in $classified) {
    if ($project -notmatch '^Tests/[A-Za-z0-9_/-]+\.csproj$' -or $project.Contains('..')) {
        throw "Invalid test project path: $project"
    }
    if (-not (Test-Path -LiteralPath (Join-Path $repoRoot $project) -PathType Leaf)) {
        throw "Missing classified test project: $project"
    }
}
# Require deliberate classification when a new test project is introduced.
$allProjects = @(Get-ChildItem -LiteralPath (Join-Path $repoRoot 'Tests') -Filter '*.csproj' -Recurse |
    ForEach-Object { $_.FullName.Substring($repoRoot.Length + 1).Replace('\', '/') })
$differences = @(Compare-Object ($classified | Sort-Object) ($allProjects | Sort-Object))
if ($differences.Count -ne 0) { throw "Test project classification is incomplete: $($differences.InputObject -join ', ')" }
# These suites use only the SDK reference packs and linked original sources.
# An empty local feed makes first-run restore independent of NuGet availability.
$pureFeed = Join-Path $repoRoot 'work/pure-test-feed'
[void][IO.Directory]::CreateDirectory($pureFeed)
Push-Location $repoRoot
try {
    foreach ($project in $included) {
        Write-Host "Running $project"
        [xml]$projectXml = Get-Content -LiteralPath $project -Raw
        if ($projectXml.SelectNodes('//PackageReference | //Reference/HintPath | //ProjectReference').Count -ne 0) {
            throw "Pure suite acquired an external dependency; review classification: $project"
        }
        & dotnet restore $project --source $pureFeed --verbosity quiet -p:NuGetAudit=false
        if ($LASTEXITCODE -ne 0) { throw "Pure restore failed: $project (exit $LASTEXITCODE)." }
        & dotnet run --project $project --configuration Release --no-restore -p:UseSharedCompilation=false
        if ($LASTEXITCODE -ne 0) { throw "Pure contract failed: $project (exit $LASTEXITCODE)." }
    }
    Write-Host "PASS: $($included.Count) pure contract projects; $($excluded.Count) native/evidence projects excluded. No game build or live acceptance claimed."
} finally { Pop-Location }
