param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\work\object-build\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
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
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Static,Instance,DeclaredOnly'
    $guards = Get-Content -Raw (Join-Path $PSScriptRoot 'NativeGuards.json') | ConvertFrom-Json
    foreach ($row in $guards.methods) {
        $matches = @($game.GetType($row.type, $true).GetMethods($flags) | Where-Object {
            $_.Name -eq $row.name -and $_.IsStatic -eq $row.static -and $_.ReturnType.ToString() -eq $row.returns -and
            (($_.GetParameters() | ForEach-Object { $_.ParameterType.ToString() }) -join ',') -eq ($row.args -join ',')
        })
        if ($matches.Count -ne 1) { throw ('Native object method changed: ' + $row.type + '.' + $row.name) }
    }
    $read = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    foreach ($row in @(
        @{Type='RimWorld.InspectPaneFiller';Method='DrawInspectString';Patch='ObjectInspectorDescriptionStringPatch';Adapter='InspectString'},
        @{Type='RimWorld.InspectPaneFiller';Method='DrawHealth';Patch='ObjectInspectorDescriptionHealthPatch';Adapter='HealthMeter'},
        @{Type='Verse.WidgetRow';Method='FillableBar';Patch='ObjectInspectorDescriptionBarPatch';Adapter='Fill'}
    )) {
        $target = [HarmonyLib.AccessTools]::Method($game.GetType($row.Type, $true), $row.Method)
        $original = @($read.Invoke($null, @($target, $null)))
        $transpiler = $mod.GetType(('ReframedUI.' + $row.Patch), $true).GetMethod('Transpiler', [Reflection.BindingFlags]'Static,NonPublic')
        $input = [System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
        foreach ($instruction in $original) { $input.Add([HarmonyLib.CodeInstruction]::new($instruction)) }
        $changed = @($transpiler.Invoke($null, @(,$input)))
        if ($changed.Count -ne $original.Count) { throw 'Native instruction count changed.' }
        $count = 0
        for ($i=0; $i -lt $original.Count; $i++) {
            if ($original[$i].labels.Count -ne $changed[$i].labels.Count -or $original[$i].blocks.Count -ne $changed[$i].blocks.Count) {
                throw 'Native control-flow or exception blocks changed.'
            }
            if (-not [object]::Equals($original[$i].operand, $changed[$i].operand)) {
                $count++; $before = $original[$i].operand; $after = $changed[$i].operand
                if ($after.DeclaringType.FullName -ne 'ReframedUI.ObjectInspectorDescription' -or $after.Name -ne $row.Adapter) { throw 'Unexpected native operation replaced.' }
                $beforeArgs = @($before.GetParameters() | ForEach-Object { $_.ParameterType.FullName })
                if (-not $before.IsStatic) { $beforeArgs = @($before.DeclaringType.FullName) + $beforeArgs }
                if (($beforeArgs -join '|') -ne ($after.GetParameters().ParameterType.FullName -join '|') -or $before.ReturnType -ne $after.ReturnType) {
                    throw 'Native adapter stack signature changed.'
                }
                if ($changed[$i].opcode.Name -ne 'call') { throw 'Adapter is not a direct static call.' }
            }
            elseif ($original[$i].opcode -ne $changed[$i].opcode) { throw 'Unapproved native opcode changed.' }
        }
        if ($count -ne 1) { throw 'Expected exactly one native paint substitution.' }
        Write-Output ('PASS ' + $row.Method + ': one scoped paint substitution; native availability, slot allocation, row advance and callbacks retained.')
    }
    if (-not $mod.GetType('ReframedUI.ObjectInspectorDescription', $true).GetProperty('Ready', [Reflection.BindingFlags]'Static,NonPublic').GetValue($null, $null)) {
        throw 'Object description adapters are not ready.'
    }
    Write-Output 'PASS 10 installed members and 3 exact native paint seams. No patches installed, game callbacks or Unity rendering executed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
