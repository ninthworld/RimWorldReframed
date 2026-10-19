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
    foreach ($row in @(
        @{ Method='DrawPawnRoleSelection'; Patch='PawnSocialRoleButtonPatch'; Flag='RoleReady' },
        @{ Method='DrawTryRomance'; Patch='PawnSocialRomanceButtonPatch'; Flag='RomanceReady' }
    )) {
        $target = [HarmonyLib.AccessTools]::Method($game.GetType('RimWorld.SocialCardUtility', $true), $row.Method)
        $original = @($readInstructions.Invoke($null, @($target, $null)))
        $patch = $mod.GetType(('ReframedUI.' + $row.Patch), $true).GetMethod('Transpiler', $flags)
        $patched = @($patch.Invoke($null, @(,[System.Collections.Generic.List[HarmonyLib.CodeInstruction]]$original)))
        if ($patched.Count -ne $original.Count) { throw 'Social paint seam changed the instruction count.' }
        $changed = 0
        for ($i=0; $i -lt $original.Count; $i++) {
            if ($original[$i].opcode -ne $patched[$i].opcode) { throw 'Social paint seam changed an opcode.' }
            if ($original[$i].labels.Count -ne $patched[$i].labels.Count -or $original[$i].blocks.Count -ne $patched[$i].blocks.Count) {
                throw 'Social paint seam changed control flow or exception blocks.'
            }
            if (-not [object]::Equals($original[$i].operand, $patched[$i].operand)) {
                $changed++
                if ($patched[$i].operand.DeclaringType.FullName -ne 'ReframedUI.PawnSocialPanel' -or
                    $patched[$i].operand.Name -ne 'DrawNativeButton' -or $original[$i].operand.Name -ne 'ButtonText') {
                    throw 'Social paint seam replaced a non-button operation.'
                }
                $before = @($original[$i].operand.GetParameters().ParameterType.FullName)
                $after = @($patched[$i].operand.GetParameters().ParameterType.FullName)
                if (($before -join '|') -ne ($after -join '|') -or $original[$i].operand.ReturnType -ne $patched[$i].operand.ReturnType) {
                    throw 'Social button replacement changed its stack signature.'
                }
            }
        }
        $state = $mod.GetType('ReframedUI.PawnSocialPanel', $true)
        if ($changed -ne 1 -or -not $state.GetField($row.Flag, $flags).GetValue($null)) { throw 'Native Social seam not ready.' }
        Write-Output ('PASS ' + $row.Method + ': one paint/input call replaced; native branches, callbacks and all other instructions preserved.')
    }
    Write-Output 'PASS two exact installed Social button seams. No patches installed or Unity rendering executed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
