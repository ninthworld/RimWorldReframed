param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\Assemblies\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run with Windows PowerShell 5.1 (.NET Framework), not pwsh.' }
$runtimeManaged = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $dependencyName = ([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll'
    $dependencyPath = Join-Path $runtimeManaged $dependencyName
    if (Test-Path -LiteralPath $dependencyPath) { return [Reflection.Assembly]::LoadFrom($dependencyPath) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path) | Out-Null
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $bootstrap = $mod.GetType('ReframedUI.RuntimePatchBootstrap', $true)
    $flags = [Reflection.BindingFlags]'Static,NonPublic'
    $bootstrap.GetMethod('VerifyRequiredMembers', $flags).Invoke($null, @()) | Out-Null
    $guards = @($bootstrap.GetField('GuardResults', $flags).GetValue($null))
    $failures = @($guards | Where-Object { $_.StartsWith('FAIL ') })
    $guards | Write-Output
    if ($guards.Count -ne 550 -or $failures.Count -ne 0) { throw "Member guard audit failed: $($guards.Count) checks, $($failures.Count) failures." }
    Write-Output 'PASS 550 member contracts against the installed game metadata. No patches installed, game launched or live integration acceptance claimed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
