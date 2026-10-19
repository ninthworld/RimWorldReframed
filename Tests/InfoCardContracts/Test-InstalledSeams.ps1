param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\Assemblies\ReframedUI.dll'),
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
    $checks = 0
    $guards = Get-Content -Raw (Join-Path $PSScriptRoot 'NativeGuards.json') | ConvertFrom-Json
    foreach ($row in $guards.methods) {
        $matches = @($game.GetType($row.type, $true).GetMethods($flags) | Where-Object {
            $_.Name -eq $row.name -and $_.IsStatic -eq $row.static -and $_.ReturnType.ToString() -eq $row.returns -and
            (($_.GetParameters() | ForEach-Object { $_.ParameterType.ToString() }) -join ',') -eq ($row.args -join ',')
        })
        if ($matches.Count -ne 1) { throw ('Native InfoCard method changed: ' + $row.type + '.' + $row.name) }; $checks++
    }
    foreach ($row in $guards.fields) {
        $member = $game.GetType($row.type, $true).GetField($row.name, $flags)
        if (-not $member -or $member.IsStatic -ne $row.static -or $member.FieldType.ToString() -ne $row.fieldType) {
            throw ('Native InfoCard field changed: ' + $row.type + '.' + $row.name)
        }; $checks++
    }
    $read = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    foreach ($row in @(
        @{Type='Verse.Dialog_InfoCard';Method='DoWindowContents';Patch='InfoCardContentsPatch';Count=4;Adapter='ReframedUI.InfoCardPanel';Allowed='DrawTitle,DrawTabs,CompactIcon,DrawSubjectIcon'},
        @{Type='Verse.Dialog_InfoCard';Method='FillCard';Patch='InfoCardFillPatch';Count=2;Adapter='ReframedUI.InfoCardPanel';Allowed='DrawBio,DrawHealth'},
        @{Type='RimWorld.HealthCardUtility';Method='DrawHealthSummary';Patch='InfoCardHealthSummaryPatch';Count=1;Adapter='ReframedUI.InfoCardPanel';Allowed='DrawHealthTabs'},
        @{Type='RimWorld.PermitsCardUtility';Method='DrawRecordsCard';Patch='InfoCardPermitDispatchPatch';Count=1;Adapter='ReframedUI.InfoCardPermits';Allowed='Draw'},
        @{Type='RimWorld.PermitsCardUtility';Method='DoLeftRect';Patch='InfoCardPermitDetailPatch';Count=4;Adapter='ReframedUI.InfoCardPermits';Allowed='Label,AcceptHeight'}
    )) {
        $target = [HarmonyLib.AccessTools]::Method($game.GetType($row.Type, $true), $row.Method)
        $original = @($read.Invoke($null, @($target, $null)))
        $transpiler = $mod.GetType(('ReframedUI.' + $row.Patch), $true).GetMethod('Transpiler', [Reflection.BindingFlags]'Static,NonPublic')
        $input = [System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
        foreach ($instruction in $original) { $input.Add([HarmonyLib.CodeInstruction]::new($instruction)) }
        $changed = @($transpiler.Invoke($null, @(,$input)))
        if ($changed.Count -ne $original.Count) { throw 'InfoCard native instruction count changed.' }
        $substitutions = 0
        for ($i=0; $i -lt $original.Count; $i++) {
            if ($original[$i].opcode -ne $changed[$i].opcode -or $original[$i].labels.Count -ne $changed[$i].labels.Count -or
                $original[$i].blocks.Count -ne $changed[$i].blocks.Count) { throw 'InfoCard control flow or exception blocks changed.' }
            if (-not [object]::Equals($original[$i].operand, $changed[$i].operand)) {
                $substitutions++
                $before = $original[$i].operand; $after = $changed[$i].operand
                if ($after.DeclaringType.FullName -ne $row.Adapter -or $after.Name -notin $row.Allowed.Split(',')) {
                    throw 'InfoCard adapter replaced an unapproved operation.'
                }
                $beforeArgs = @($before.GetParameters() | ForEach-Object { $_.ParameterType.FullName })
                if (-not $before.IsStatic) {
                    $receiver = $before.DeclaringType
                    if ($receiver.IsValueType) { $receiver = $receiver.MakeByRefType() }
                    $beforeArgs = @($receiver.FullName) + $beforeArgs
                }
                $beforeReturn = if ($before -is [Reflection.ConstructorInfo]) { [void] } else { $before.ReturnType }
                if (($beforeArgs -join '|') -ne ($after.GetParameters().ParameterType.FullName -join '|') -or
                    $beforeReturn -ne $after.ReturnType) { throw 'InfoCard adapter changed a native stack signature.' }
            }
        }
        if ($substitutions -ne $row.Count) { throw 'InfoCard replacement count changed.' }
        Write-Output ('PASS ' + $row.Method + ': ' + $substitutions + ' scoped paint/body substitutions; all native dispatch/history/material/tab/callback instructions remain.'); $checks++
    }
    $panel = $mod.GetType('ReframedUI.InfoCardPanel', $true)
    if (-not $panel.GetProperty('Ready', [Reflection.BindingFlags]'Static,NonPublic').GetValue($null, $null)) { throw 'InfoCard scoped adapters are not ready.' }
    Write-Output ('PASS ' + $checks + ' installed member/seam contracts. No patches installed, game callbacks or Unity rendering executed.')
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
