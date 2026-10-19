param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [Parameter(Mandatory=$true)][string]$ModAssembly,
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
    $target = [HarmonyLib.AccessTools]::Method($game.GetType('RimWorld.WidgetsWork', $true), 'DrawWorkBoxFor')
    $reader = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    $original = $reader.Invoke($null, @($target, $null))
    $before = @($original)
    $transpiler = $mod.GetType('ReframedUI.WorkPanelCheckboxActivationPatch', $true).GetMethod('Transpiler', $flags)
    $patched = @($transpiler.Invoke($null, @(,$original)))
    $ready = $mod.GetType('ReframedUI.WorkPanelNative', $true).GetField('CheckboxSeamReady', $flags)
    if (-not $ready.GetValue($null) -or $before.Count -ne $patched.Count) { throw 'Native Work activation seam rejected or changed instruction count.' }
    $changed = 0; $checkboxes = 0; $setters = 0
    for ($i = 0; $i -lt $before.Count; $i++) {
        if ($before[$i].labels.Count -ne $patched[$i].labels.Count -or
            $before[$i].blocks.Count -ne $patched[$i].blocks.Count) { throw 'Work activation changed control flow.' }
        if ($before[$i].operand -eq $patched[$i].operand) {
            if ($before[$i].opcode -ne $patched[$i].opcode) { throw 'Unrelated instruction opcode changed' }
            continue
        }
        $changed++
        $a = $before[$i].operand; $b = $patched[$i].operand
        if ($a.Name -eq 'ButtonInvisible' -and $b.Name -eq 'NativeCheckbox') {
            if ($before[$i].opcode -ne $patched[$i].opcode -or -not $a.IsStatic -or -not $b.IsStatic -or
                $a.ReturnType -ne $b.ReturnType -or (($a.GetParameters().ParameterType.FullName -join '|') -ne ($b.GetParameters().ParameterType.FullName -join '|'))) {
                throw 'Checkbox adapter changed stack signature'
            }
            $checkboxes++
        } elseif ($a.Name -eq 'SetPriority' -and $b.Name -eq 'NativeSetPriority') {
            $expected = @($a.DeclaringType.FullName) + @($a.GetParameters().ParameterType.FullName)
            if ($a.IsStatic -or -not $b.IsStatic -or $a.ReturnType -ne $b.ReturnType -or
                $patched[$i].opcode -ne [Reflection.Emit.OpCodes]::Call -or
                (($expected -join '|') -ne ($b.GetParameters().ParameterType.FullName -join '|'))) {
                throw 'Setter adapter changed receiver/arguments/return stack signature'
            }
            $setters++
        } else {
            throw 'Work activation changed an unrelated callback.'
        }
    }
    if ($changed -ne 5 -or $checkboxes -ne 1 -or $setters -ne 4) { throw 'Work activation requires1checkbox+4setter substitutions.' }
    Write-Output 'PASS Work activation: exact1checkbox+4setters; other instructions, callbacks and stack signatures preserved.'

    # An already adapted body has zero matches: it must return unchanged and refuse ownership.
    $inputList = [System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    foreach ($instruction in $patched) { $inputList.Add($instruction) }
    $fallback = @($transpiler.Invoke($null, @(,$inputList)))
    if ($ready.GetValue($null) -or $fallback.Count -ne $patched.Count) { throw 'Work seam does not fail open.' }
    for ($i = 0; $i -lt $patched.Count; $i++) {
        if (-not [object]::ReferenceEquals($patched[$i], $fallback[$i])) { throw 'Rejected Work seam changed instructions.' }
    }
    Write-Output 'PASS missing Work seam retains original instructions and disables custom Work eligibility.'

    # A partial native body must also reject the entire adapter, including the checkbox substitution.
    $partial = [System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    $removed = $false
    foreach ($instruction in $original) {
        if (-not $removed -and $instruction.operand -is [Reflection.MethodInfo] -and $instruction.operand.Name -eq 'SetPriority') { $removed = $true; continue }
        $partial.Add($instruction)
    }
    $fallback = @($transpiler.Invoke($null, @(,$partial)))
    if ($ready.GetValue($null) -or $fallback.Count -ne $partial.Count) { throw 'Partial setter body did not fail open' }
    for ($i = 0; $i -lt $partial.Count; $i++) {
        if (-not [object]::ReferenceEquals($partial[$i], $fallback[$i])) { throw 'Rejected partial seam changed instructions' }
    }
    Write-Output 'PASS partial setter seam disables ownership and returns original instructions intact.'
    Write-Output 'PASS offline candidate seam audit. No game launch, rendering or patches installed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
