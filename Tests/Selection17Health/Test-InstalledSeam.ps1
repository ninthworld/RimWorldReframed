param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\work\social-private\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run in Windows PowerShell 5.1.' }
$runtimeManaged = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $dependencyPath = Join-Path $runtimeManaged (([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) { return [Reflection.Assembly]::LoadFrom($dependencyPath) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path) | Out-Null
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $runtimeManaged 'Assembly-CSharp.dll'))
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags = [Reflection.BindingFlags]'Static,NonPublic'
    $readInstructions = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    $target = [HarmonyLib.AccessTools]::Method($game.GetType('RimWorld.HealthCardUtility', $true), 'DrawMedOperationsTab')
    $original = @($readInstructions.Invoke($null, @($target, $null)))
    $patch = $mod.GetType('ReframedUI.PawnHealthOperationsPatch', $true).GetMethod('Transpiler', $flags)
    $patched = @($patch.Invoke($null, @(,[System.Collections.Generic.List[HarmonyLib.CodeInstruction]]$original)))
    if ($patched.Count -ne $original.Count) { throw 'Health adapter changed instruction count.' }
    $changed = 0
    for ($i=0; $i -lt $original.Count; $i++) {
        $a = $original[$i]; $b = $patched[$i]
        if ($a.labels.Count -ne $b.labels.Count -or $a.blocks.Count -ne $b.blocks.Count) { throw 'Health adapter changed control flow.' }
        if ([object]::Equals($a.operand, $b.operand)) {
            if ($a.opcode -ne $b.opcode) { throw 'Health adapter changed an unrelated opcode.' }
            continue
        }
        $changed++
        if ($a.operand.DeclaringType.FullName -ne 'RimWorld.BillStack' -or $a.operand.Name -ne 'DoListing' -or
            $b.operand.DeclaringType.FullName -ne 'ReframedUI.PawnHealthPanel' -or $b.operand.Name -ne 'DrawBillListing' -or
            $b.opcode.Name -ne 'call' -or $b.operand.ReturnType -ne $a.operand.ReturnType) { throw 'Health adapter changed a non-listing operation.' }
        $before = @($a.operand.DeclaringType.FullName) + @($a.operand.GetParameters().ParameterType.FullName)
        $after = @($b.operand.GetParameters().ParameterType.FullName)
        if (($before -join '|') -ne ($after -join '|')) { throw 'Health adapter changed the evaluation stack signature.' }
    }
    if ($changed -ne 1 -or -not $mod.GetType('ReframedUI.PawnHealthPanel', $true).GetField('OperationsReady', $flags).GetValue($null)) {
        throw 'Health operations seam not ready.'
    }
    Write-Output 'PASS one exact Health listing substitution; native recipe factory, branches, callbacks and stack signature retained. No patches installed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
