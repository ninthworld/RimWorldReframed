param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\work\notifications-build\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run in Windows PowerShell 5.1.' }
$managed = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $path = Join-Path $managed (([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll')
    if (Test-Path -LiteralPath $path) { return [Reflection.Assembly]::LoadFrom($path) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path) | Out-Null
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags = [Reflection.BindingFlags]'Static,NonPublic'
    $reader = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object { $_.Name -eq 'GetOriginalInstructions' -and $_.GetParameters().Count -eq 2 -and -not $_.GetParameters()[1].ParameterType.IsByRef } | Select-Object -First 1
    function Copy-Instructions($source) {
        $list = New-Object 'System.Collections.Generic.List[HarmonyLib.CodeInstruction]'
        foreach ($item in $source) { $list.Add([HarmonyLib.CodeInstruction]::new($item)) }
        return ,$list
    }
    function Same-Operand($a, $b) {
        if ($a -is [Array] -or $b -is [Array]) {
            if (-not ($a -is [Array]) -or -not ($b -is [Array]) -or $a.Length -ne $b.Length) { return $false }
            for ($k=0; $k -lt $a.Length; $k++) { if (-not [object]::Equals($a[$k],$b[$k])) { return $false } }
            return $true
        }
        return [object]::Equals($a,$b)
    }
    function Same-Control($a, $b) {
        if ($a.labels.Count -ne $b.labels.Count -or $a.blocks.Count -ne $b.blocks.Count) { throw 'Changed control-flow marker count' }
        for ($k=0; $k -lt $a.labels.Count; $k++) { if ($a.labels[$k] -ne $b.labels[$k]) { throw 'Changed branch label' } }
        for ($k=0; $k -lt $a.blocks.Count; $k++) { if ($a.blocks[$k].blockType -ne $b.blocks[$k].blockType -or $a.blocks[$k].catchType -ne $b.blocks[$k].catchType) { throw 'Changed exception boundary' } }
    }
    $rows = @(
        @{ Native='RimWorld.IncidentWorker'; Method='SendIncidentLetter'; Patch='NotificationIncidentContextPatch'; Incident=$true; Wrapper='ReceiveIncident' },
        @{ Native='RimWorld.ShortCircuitUtility'; Method='DoShortCircuit'; Patch='NotificationShortCircuitContextPatch'; Incident=$false; Wrapper='ReceiveElectrical' },
        @{ Native='RimWorld.ShortCircuitUtility'; Method='TryShortCircuitInRain'; Patch='NotificationRainShortCircuitContextPatch'; Incident=$false; Wrapper='ReceiveElectrical' }
    )
    foreach ($row in $rows) {
        $target = [HarmonyLib.AccessTools]::Method($game.GetType($row.Native,$true),$row.Method)
        $before = @($reader.Invoke($null,@($target,$null)))
        $inputList = Copy-Instructions $before
        $patch = $mod.GetType(('ReframedUI.'+$row.Patch),$true).GetMethod('Transpiler',$flags)
        $invoke = New-Object 'object[]' 1; $invoke[0] = $inputList.PSObject.BaseObject
        $after = @($patch.Invoke($null,$invoke))
        $expected = $before.Count; if ($row.Incident) { $expected++ }
        if ($after.Count -ne $expected) { throw ('Unexpected instruction count: '+$row.Method) }
        $changed=0; $j=0
        for ($i=0; $i -lt $before.Count; $i++) {
            $a=$before[$i]; $b=$after[$j]
            $isCall = $a.operand -is [Reflection.MethodInfo] -and $a.operand.DeclaringType.FullName -eq 'Verse.LetterStack' -and $a.operand.Name -eq 'ReceiveLetter'
            if (-not $isCall) {
                Same-Control $a $b
                if ($a.opcode -ne $b.opcode -or -not (Same-Operand $a.operand $b.operand)) { throw ('Unrelated IL changed: '+$row.Method+'['+$i+']') }
                $j++; continue
            }
            $changed++
            if ($row.Incident) {
                if ($b.opcode -ne [Reflection.Emit.OpCodes]::Ldarg_S -or [int]$b.operand -ne 5) { throw 'Missing exact IncidentDef argument load' }
                Same-Control $a $b; $j++; $b=$after[$j]
                if ($b.labels.Count -ne 0 -or $b.blocks.Count -ne 0) { throw 'Injected call duplicated control-flow markers' }
            } else { Same-Control $a $b }
            if ($b.opcode -ne [Reflection.Emit.OpCodes]::Call -or $b.operand.DeclaringType.FullName -ne 'ReframedUI.NotificationContext' -or $b.operand.Name -ne $row.Wrapper) { throw 'Wrong receive substitution' }
            $nativeStack = @($a.operand.DeclaringType) + @($a.operand.GetParameters() | ForEach-Object ParameterType)
            if ($row.Incident) { $nativeStack += $game.GetType('RimWorld.IncidentDef',$true) }
            $wrapperStack = @($b.operand.GetParameters() | ForEach-Object ParameterType)
            if (-not $b.operand.IsStatic -or $a.operand.ReturnType -ne $b.operand.ReturnType -or $nativeStack.Count -ne $wrapperStack.Count) { throw 'Incompatible wrapper stack shape' }
            for ($k=0; $k -lt $nativeStack.Count; $k++) { if ($nativeStack[$k] -ne $wrapperStack[$k]) { throw 'Incompatible wrapper stack type' } }
            $j++
        }
        if ($changed -ne 1 -or $j -ne $after.Count) { throw 'Expected exactly one bounded receive seam' }
        # An unsupported call pattern must return unchanged IL.
        $broken = Copy-Instructions $before
        foreach ($item in $broken) { if ($item.operand -is [Reflection.MethodInfo] -and $item.operand.DeclaringType.FullName -eq 'Verse.LetterStack' -and $item.operand.Name -eq 'ReceiveLetter') { $item.operand=$null } }
        $invoke[0]=$broken.PSObject.BaseObject; $fallback=@($patch.Invoke($null,$invoke))
        if ($fallback.Count -ne $broken.Count) { throw 'Fallback changed instruction count' }
        for ($k=0;$k -lt $broken.Count;$k++) { Same-Control $broken[$k] $fallback[$k]; if ($broken[$k].opcode -ne $fallback[$k].opcode -or -not (Same-Operand $broken[$k].operand $fallback[$k].operand)) { throw 'Fallback modified unsupported IL' } }
        Write-Output ('PASS exact receive seam + stack/control-flow/fallback: '+$row.Method)
    }
    $context=$mod.GetType('ReframedUI.NotificationContext',$true)
    if (-not ($context.GetMethod('ReceiveElectrical',$flags).GetMethodBody().ExceptionHandlingClauses | Where-Object Flags -eq Finally)) { throw 'Electrical context lacks finally cleanup' }
    $nested=$context.GetNestedType('ElectricalScope',[Reflection.BindingFlags]'NonPublic')
    $slot=$context.GetField('current',$flags)
    $tagType=$game.GetType('Verse.TaggedString',$true)
    $tagCtor=$tagType.GetConstructor([Type[]]@([string]))
    function Tag([string]$text) { return $tagCtor.Invoke([object[]]@($text)) }
    $definition=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($game.GetType('Verse.LetterDef',$true))
    $otherDefinition=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($game.GetType('Verse.LetterDef',$true))
    $targets=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($game.GetType('Verse.LookTargets',$true))
    $otherTargets=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($game.GetType('Verse.LookTargets',$true))
    $scopeCtor=$nested.GetConstructors([Reflection.BindingFlags]'Instance,NonPublic') | Select-Object -First 1
    function Scope([string]$label,[string]$text) { return $scopeCtor.Invoke([object[]]@((Tag $label),(Tag $text),$definition,$targets)) }
    function Native-Letter([string]$label,[string]$text,$def,$look) {
        # Allocate data only: no native constructor, game/DefOf initializer or Unity call.
        $letter=[Runtime.Serialization.FormatterServices]::GetUninitializedObject($game.GetType('Verse.StandardLetter',$true))
        $game.GetType('Verse.Letter',$true).GetField('def').SetValue($letter,$def)
        $game.GetType('Verse.Letter',$true).GetField('lookTargets').SetValue($letter,$look)
        $game.GetType('Verse.Letter',$true).GetProperty('Label').SetValue($letter,(Tag $label),$null)
        $game.GetType('Verse.ChoiceLetter',$true).GetProperty('Text').SetValue($letter,(Tag $text),$null)
        return $letter
    }
    $outer=Scope 'expected label' 'expected body'
    $capture=$context.GetMethod('CaptureElectrical',$flags)
    $captured=$nested.GetField('Captured',[Reflection.BindingFlags]'Instance,NonPublic')
    $icon=$context.GetMethod('Icon',$flags)
    $unrelated=Native-Letter 'unrelated label' 'unrelated body' $definition $targets
    $capture.Invoke($null,[object[]]@($unrelated)) | Out-Null
    if ($captured.GetValue($outer) -or $null -ne $icon.Invoke($null,[object[]]@($unrelated))) { throw 'Unrelated nested receipt stole electrical context' }
    foreach ($wrong in @(
        (Native-Letter 'expected label' 'different body' $definition $targets),
        (Native-Letter 'expected label' 'expected body' $otherDefinition $targets),
        (Native-Letter 'expected label' 'expected body' $definition $otherTargets)
    )) {
        $capture.Invoke($null,[object[]]@($wrong)) | Out-Null
        if ($captured.GetValue($outer)) { throw 'Mismatched text/definition/target consumed electrical context' }
    }
    $intended=Native-Letter 'expected label' 'expected body' $definition $targets
    $capture.Invoke($null,[object[]]@($intended)) | Out-Null
    if (-not $captured.GetValue($outer) -or $icon.Invoke($null,[object[]]@($intended)) -ne 'power') { throw 'Intended normalized native letter did not bind' }
    Write-Output 'PASS unrelated/mismatched native letters do not consume scope; intended normalized object binds power context.'
    $inner=Scope 'nested label' 'nested body'
    if (-not [object]::ReferenceEquals($slot.GetValue($null),$inner)) { throw 'Nested context was not installed' }
    ([IDisposable]$inner).Dispose()
    if (-not [object]::ReferenceEquals($slot.GetValue($null),$outer)) { throw 'Nested context failed to restore parent' }
    ([IDisposable]$outer).Dispose()
    if ($null -ne $slot.GetValue($null)) { throw 'Context escaped its final scope' }
    Write-Output 'PASS nested electrical scope and native finally cleanup; no patches installed or Unity/game input executed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
