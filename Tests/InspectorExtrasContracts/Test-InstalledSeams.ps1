param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\Assemblies\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
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
        $_.Name -eq 'GetOriginalInstructions' -and $_.GetParameters().Count -eq 2 -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    if ($null -eq $readInstructions) { throw 'Harmony original-instruction reader is unavailable.' }

    function Copy-Instructions($instructions) {
        $list = New-Object 'System.Collections.Generic.List[HarmonyLib.CodeInstruction]'
        foreach ($instruction in $instructions) {
            # Independent opcode/operand/list containers protect the baseline even
            # if an older candidate transpiler mutates its input instructions.
            $copy = [HarmonyLib.CodeInstruction]::new($instruction)
            if ($instruction.operand -is [Array]) { $copy.operand = $instruction.operand.Clone() }
            $copy.blocks.Clear()
            foreach ($block in $instruction.blocks) {
                $copy.blocks.Add([HarmonyLib.ExceptionBlock]::new($block.blockType, $block.catchType))
            }
            $list.Add($copy)
        }
        return ,$list
    }
    function Same-Operand($a, $b) {
        if ($a -is [Array] -or $b -is [Array]) {
            if (-not ($a -is [Array]) -or -not ($b -is [Array]) -or $a.Length -ne $b.Length) { return $false }
            for ($j = 0; $j -lt $a.Length; $j++) {
                if (-not [object]::Equals($a[$j], $b[$j])) { return $false }
            }
            return $true
        }
        return [object]::Equals($a, $b)
    }
    function Assert-ControlFlow($a, $b, $location) {
        if ($a.labels.Count -ne $b.labels.Count -or $a.blocks.Count -ne $b.blocks.Count) { throw "Changed control-flow markers at $location" }
        for ($j = 0; $j -lt $a.labels.Count; $j++) {
            if (-not [object]::Equals($a.labels[$j], $b.labels[$j])) { throw "Changed branch label at $location" }
        }
        for ($j = 0; $j -lt $a.blocks.Count; $j++) {
            if ($a.blocks[$j].blockType -ne $b.blocks[$j].blockType -or $a.blocks[$j].catchType -ne $b.blocks[$j].catchType) {
                throw "Changed exception block at $location"
            }
        }
    }
    function Assert-StackSignature([Reflection.MethodInfo]$native, [Reflection.MethodInfo]$wrapper, $location) {
        if (-not $wrapper.IsStatic -or $native.ReturnType -ne $wrapper.ReturnType -or $native.ContainsGenericParameters -or $wrapper.ContainsGenericParameters) {
            throw "Changed return/static/generic stack contract at $location"
        }
        $nativeStack = New-Object 'System.Collections.Generic.List[Type]'
        if (-not $native.IsStatic) {
            # A reference-type receiver occupies one ordinary argument slot.
            if ($native.DeclaringType.IsValueType) { $nativeStack.Add($native.DeclaringType.MakeByRefType()) }
            else { $nativeStack.Add($native.DeclaringType) }
        }
        foreach ($parameter in $native.GetParameters()) { $nativeStack.Add($parameter.ParameterType) }
        $wrapperStack = @($wrapper.GetParameters() | ForEach-Object { $_.ParameterType })
        if ($nativeStack.Count -ne $wrapperStack.Count) { throw "Changed argument stack size at $location" }
        for ($j = 0; $j -lt $nativeStack.Count; $j++) {
            if ($nativeStack[$j] -ne $wrapperStack[$j]) { throw "Changed argument stack type at $location slot $j" }
        }
    }
    $rows = @(
        @{
            Native = 'RimWorld.HealthCardUtility'; Method = 'DoDebugOptions'; Patch = 'PawnInspectorExtrasHealthMenuPatch'
            Helper = 'PawnInspectorExtrasHealth'; Flag = 'MenuReady'
            Calls = @{ 'Verse.Widgets.ButtonText' = 'OpenButton' }
        },
        @{
            Native = 'RimWorld.SocialCardUtility'; Method = 'DrawPawnCertainty'; Patch = 'PawnInspectorExtrasCertaintyPatch'
            Helper = 'PawnInspectorExtrasCertainty'; Flag = 'Ready'
            Calls = @{ 'RimWorld.Ideo.DrawIcon' = 'Icon'; 'Verse.Widgets.Label' = 'Label'; 'Verse.Widgets.DrawHighlight' = 'Highlight'; 'Verse.Widgets.FillableBar' = 'Fill' }
        }
    )
    $total = 0
    foreach ($row in $rows) {
        $target = [HarmonyLib.AccessTools]::Method($game.GetType($row.Native, $true), $row.Method)
        if ($null -eq $target) { throw ('Missing native target ' + $row.Native + '.' + $row.Method) }
        $original = @($readInstructions.Invoke($null, @($target, $null)))
        $before = Copy-Instructions $original
        $inputInstructions = Copy-Instructions $original
        $transpiler = $mod.GetType(('ReframedUI.' + $row.Patch), $true).GetMethod('Transpiler', $flags)
        $invokeArguments = New-Object 'object[]' 1
        $invokeArguments[0] = $inputInstructions.PSObject.BaseObject
        $patched = @($transpiler.Invoke($null, $invokeArguments))
        $helper = $mod.GetType(('ReframedUI.' + $row.Helper), $true)
        if (-not $helper.GetField($row.Flag, $flags).GetValue($null)) { throw ('Native IL pattern rejected by ' + $row.Patch) }
        if ($before.Count -ne $patched.Count) { throw ('Instruction count changed in ' + $row.Method) }
        $seen = @{}
        $changed = 0
        for ($i = 0; $i -lt $before.Count; $i++) {
            $a = $before[$i]; $b = $patched[$i]; $location = $row.Method + '[' + $i + ']'
            Assert-ControlFlow $a $b $location
            if (Same-Operand $a.operand $b.operand) {
                if ($a.opcode -ne $b.opcode) { throw "Unexpected opcode-only change at $location" }
                continue
            }
            if (-not ($a.operand -is [Reflection.MethodInfo]) -or -not ($b.operand -is [Reflection.MethodInfo])) { throw "Unexpected non-method operand change at $location" }
            $native = $a.operand; $wrapper = $b.operand
            $key = $native.DeclaringType.FullName + '.' + $native.Name
            if (-not $row.Calls.ContainsKey($key) -or $wrapper.DeclaringType -ne $helper -or $wrapper.Name -ne $row.Calls[$key]) {
                throw "Unexpected method substitution at $location : $native -> $wrapper"
            }
            if ($a.opcode -ne [Reflection.Emit.OpCodes]::Call -and $a.opcode -ne [Reflection.Emit.OpCodes]::Callvirt) { throw "Native seam was not a call at $location" }
            if ($b.opcode -ne [Reflection.Emit.OpCodes]::Call) { throw "Static wrapper is not called with call at $location" }
            Assert-StackSignature $native $wrapper $location
            if ($seen.ContainsKey($key)) { throw "Repeated native substitution $key" }
            $seen[$key] = $true; $changed++
        }
        if ($changed -ne $row.Calls.Count -or $seen.Count -ne $row.Calls.Count) { throw ('Wrong exact substitution count in ' + $row.Method) }
        foreach ($key in $row.Calls.Keys) { if (-not $seen.ContainsKey($key)) { throw "Missing required substitution $key" } }
        $total += $changed
        Write-Output ('PASS exact installed extras IL ' + $row.Method + ': ' + $changed + ' substitutions, unchanged other instructions/control flow, compatible wrapper stack')
    }
    if ($total -ne 5) { throw 'Extras audit did not observe exact 1+4 substitutions.' }
    Write-Output 'PASS installed extras seam audit: exact 1+4. No patches installed, game launched or Unity rendering executed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
