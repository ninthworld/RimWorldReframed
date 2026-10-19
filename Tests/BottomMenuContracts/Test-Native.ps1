param(
    [string]$RimWorldDir='C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly=(Join-Path $PSScriptRoot '../../work/bottom30-campaign/v0230-build/ReframedUI.dll'),
    [string]$HarmonyAssembly=(Join-Path $env:USERPROFILE '.nuget/packages/lib.harmony/2.3.6/lib/net472/0Harmony.dll')
)
$ErrorActionPreference='Stop'
if($PSVersionTable.PSEdition -eq 'Core'){throw 'Use Windows PowerShell 5.1.'}
$managed=Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
$resolve=[ResolveEventHandler]{param($sender,$eventArgs) $path=Join-Path $managed (([Reflection.AssemblyName]$eventArgs.Name).Name+'.dll');if(Test-Path -LiteralPath $path){return [Reflection.Assembly]::LoadFrom($path)};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path)|Out-Null
    $game=[Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $mod=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags=[Reflection.BindingFlags]'Static,NonPublic'
    $bootstrap=$mod.GetType('ReframedUI.RuntimePatchBootstrap',$true)
    $bootstrap.GetMethod('VerifyRequiredMembers',$flags).Invoke($null,@())|Out-Null
    $guards=@($bootstrap.GetField('GuardResults',$flags).GetValue($null))
    $failures=@($guards|Where-Object{$_.StartsWith('FAIL ')})
    if($failures.Count){throw ($failures -join "`n")}
    $surfaces=@($bootstrap.GetField('OwnedSurfaces',$flags).GetValue($null))
    $types=@($bootstrap.GetField('PatchTypes',$flags).GetValue($null))
    $checked=0
    foreach($type in $types|Where-Object{$_.Name -match '^(BottomMenu|WorldMenus|WorldHud|KnowledgeMenus)'}) {
        $dynamic=$type.GetMethod('TargetMethods',$flags)
        if($dynamic){$targets=@($dynamic.Invoke($null,@()))}
        else {
            $attribute=@($type.GetCustomAttributes([HarmonyLib.HarmonyPatch],$false)|Where-Object{$_.info.declaringType})[0]
            if(!$attribute){throw "Missing declared target: $($type.Name)"}
            $info=$attribute.info
            $target=[HarmonyLib.AccessTools]::Method($info.declaringType,$info.methodName,$info.argumentTypes)
            if(!$target){throw "Unresolved target: $($type.Name)"}
            $targets=@($target)
        }
        foreach($target in $targets){if($target -notin $surfaces){throw "Target absent from runtime guard: $($type.Name) -> $target"};$checked++}
    }
    if($checked -lt 30){throw "New module registration incomplete: $checked targets"}
    $reader=[HarmonyLib.PatchProcessor].GetMethods()|Where-Object{$_.Name -eq 'GetOriginalInstructions' -and $_.GetParameters().Count -eq 2 -and !$_.GetParameters()[1].ParameterType.IsByRef}|Select-Object -First 1
    $world=[HarmonyLib.AccessTools]::Method($game.GetType('RimWorld.Planet.WorldGlobalControls',$true),'WorldGlobalControlsOnGUI')
    $before=@($reader.Invoke($null,@($world,$null)))
    $instructions=New-Object 'System.Collections.Generic.List[HarmonyLib.CodeInstruction]'
    foreach($instruction in $before){$instructions.Add([HarmonyLib.CodeInstruction]::new($instruction))}
    $patch=$mod.GetType('ReframedUI.WorldHudClockPatch',$true).GetMethod('Transpiler',$flags)
    $arguments=New-Object 'object[]' 1;$arguments[0]=$instructions.PSObject.BaseObject
    $after=@($patch.Invoke($null,$arguments))
    if($before.Count -ne $after.Count){throw 'World clock changed instruction count'}
    $changed=0
    for($i=0;$i -lt $before.Count;$i++) {
        $a=$before[$i];$b=$after[$i]
        if($a.opcode -ne $b.opcode -or $a.labels.Count -ne $b.labels.Count -or $a.blocks.Count -ne $b.blocks.Count){throw "World instruction/control structure changed at $i"}
        if([object]::Equals($a.operand,$b.operand)){continue}
        if($a.operand -is [Array] -and $b.operand -is [Array] -and ($a.operand -join ',') -eq ($b.operand -join ',')){continue}
        if($a.operand -isnot [Reflection.MethodInfo] -or $b.operand -isnot [Reflection.MethodInfo]){throw "Unrelated world IL changed at $i"}
        $expected=@{DoTimespeedControls='TimeControls';DoDate='Date'}
        if($a.operand.DeclaringType.FullName -ne 'RimWorld.GlobalControlsUtility' -or !$expected.ContainsKey($a.operand.Name) -or $b.operand.Name -ne $expected[$a.operand.Name] -or $b.operand.DeclaringType.FullName -ne 'ReframedUI.WorldHud'){throw "Unrelated world callback changed at $i"}
        if(($a.operand.GetParameters().ParameterType.FullName -join '|') -ne ($b.operand.GetParameters().ParameterType.FullName -join '|')){throw 'Native world clock call signature changed'}
        $changed++
    }
    if($changed -ne 2){throw "Expected exact time/date substitutions, found $changed"}
    Write-Output "PASS $checked new patch target registrations; exact 1 time + 1 date replacement preserves all other world instructions. Reflection only; no patches installed or native UI callbacks invoked."
}
finally{[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
