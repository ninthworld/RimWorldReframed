[CmdletBinding()]
param(
    [string]$RimWorldDir = $env:RIMWORLD_DIR,
    [string]$ManagedDir,
    [string]$OutputDir
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'A .NET 10 SDK is required. Install it and retry.'
}
if ([string]::IsNullOrWhiteSpace($RimWorldDir)) {
    $RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld'
}
if ([string]::IsNullOrWhiteSpace($ManagedDir)) {
    $ManagedDir = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
}
$ManagedDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($ManagedDir)
foreach ($assembly in @('Assembly-CSharp.dll', 'UnityEngine.CoreModule.dll', 'UnityEngine.IMGUIModule.dll',
    'UnityEngine.TextRenderingModule.dll', 'UnityEngine.InputLegacyModule.dll',
    'UnityEngine.ScreenCaptureModule.dll', 'UnityEngine.ImageConversionModule.dll')) {
    if (-not (Test-Path -LiteralPath (Join-Path $ManagedDir $assembly) -PathType Leaf)) {
        throw "Missing game assembly '$assembly' in '$ManagedDir'. Set -RimWorldDir, RIMWORLD_DIR or -ManagedDir to your installed game."
    }
}
$buildArgs = @('build', (Join-Path $PSScriptRoot 'Source/ReframedUI.csproj'), '-c', 'Release',
    "-p:RimWorldDir=$RimWorldDir", "-p:ManagedDir=$ManagedDir")
if (-not [string]::IsNullOrWhiteSpace($OutputDir)) {
    if (-not [IO.Path]::IsPathRooted($OutputDir)) { $OutputDir = Join-Path $PSScriptRoot $OutputDir }
    $OutputDir = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutputDir)
    # MSBuild OutputPath requires a directory separator; slash also works on Windows.
    $buildArgs += "-p:OutputPath=$($OutputDir.TrimEnd([char[]]'\/'))/"
    Write-Host "Private build output: $OutputDir"
} else {
    Write-Host "Build output: $(Join-Path $PSScriptRoot 'Assemblies') (live deployment when this is the installed checkout)"
}
Push-Location $PSScriptRoot
try {
    & dotnet @buildArgs
    if ($LASTEXITCODE -ne 0) { throw "Build failed (exit $LASTEXITCODE)." }
} finally { Pop-Location }
