param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\work\log-build\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run with Windows PowerShell 5.1.' }
$runtimeManaged = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $path = Join-Path $runtimeManaged (([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll')
    if (Test-Path -LiteralPath $path) { return [Reflection.Assembly]::LoadFrom($path) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path) | Out-Null
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $runtimeManaged 'Assembly-CSharp.dll'))
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags = [Reflection.BindingFlags]'Static,Instance,Public,NonPublic'
    $read = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    function Instructions($type, $method) {
        $target = [HarmonyLib.AccessTools]::Method($type, $method)
        if ($null -eq $target) { throw "Missing method $type.$method" }
        return @($read.Invoke($null, @($target, $null)))
    }
    function RequiresCall($instructions, $owner, $name) {
        $found = @($instructions | Where-Object {
            ($_.operand -is [Reflection.MethodInfo]) -and $_.operand.DeclaringType.FullName -eq $owner -and $_.operand.Name -eq $name
        })
        if ($found.Count -eq 0) { throw "Missing native callback $owner.$name" }
    }
    $utility = $game.GetType('RimWorld.ITab_Pawn_Log_Utility', $true)
    $generate = [HarmonyLib.AccessTools]::Method($utility, 'GenerateLogLinesFor')
    if (($generate.GetParameters().ParameterType.FullName -join '|') -ne 'Verse.Pawn|System.Boolean|System.Boolean|System.Boolean|System.Int32') {
        throw 'Native filter/POV/cap signature changed.'
    }
    $native = Instructions $utility 'GenerateLogLinesFor'
    RequiresCall $native 'Verse.PlayLog' 'get_AllEntries'
    RequiresCall $native 'Verse.BattleLog' 'get_Battles'
    RequiresCall $native 'Verse.Battle' 'Concerns'
    RequiresCall $native 'Verse.LogEntry' 'Concerns'
    RequiresCall $native 'Verse.LogEntry' 'ShowInCompactView'
    RequiresCall $native 'Verse.Battle' 'GetName'
    # Show all's native combat-detail branch must bypass only the compact test.
    $compact = -1
    for ($i=0; $i -lt $native.Count; $i++) {
        if ($native[$i].operand -is [Reflection.MethodInfo] -and $native[$i].operand.Name -eq 'ShowInCompactView') { $compact=$i; break }
    }
    $allTarget = -1; $compactTarget = -2
    if ($compact -ge 3) {
        # Harmony can use distinct labels at the same destination instruction.
        for ($i=0; $i -lt $native.Count; $i++) {
            if ($native[$i].labels.Contains($native[$compact-2].operand)) { $allTarget=$i }
            if ($native[$i].labels.Contains($native[$compact+1].operand)) { $compactTarget=$i }
        }
    }
    if ($compact -lt 3 -or $native[$compact-3].opcode.Name -ne 'ldarg.1' -or
        $native[$compact-2].opcode.FlowControl -ne 'Cond_Branch' -or $allTarget -ne $compactTarget) {
        throw 'Native show-all no longer bypasses the compact combat-detail test.'
    }
    Write-Output 'PASS native combined combat/play history, POV concern filters, battle grouping and show-all compact-detail semantics.'

    foreach ($field in @(
        @('RimWorld.ITab_Pawn_Log_Utility+LogLineDisplayableHeader','text','Verse.TaggedString'),
        @('RimWorld.ITab_Pawn_Log_Utility+LogLineDisplayableLog','log','Verse.LogEntry'),
        @('RimWorld.ITab_Pawn_Log_Utility+LogLineDisplayableLog','pawn','Verse.Pawn'),
        @('RimWorld.ITab_Pawn_Log_Utility','HighlightAlpha','System.Single'),
        @('RimWorld.ITab_Pawn_Log_Utility','HighlightDuration','System.Single'),
        @('Verse.PlayLogEntry_Interaction','intDef','RimWorld.InteractionDef'),
        @('Verse.PlayLogEntry_InteractionSinglePawn','intDef','RimWorld.InteractionDef')
    )) {
        $member = $game.GetType($field[0], $true).GetField($field[1], $flags)
        if ($null -eq $member -or $member.FieldType.FullName -ne $field[2]) { throw ('Log adapter field changed: ' + ($field -join '.')) }
    }
    $panel = $mod.GetType('ReframedUI.PawnLogPanel', $true)
    $refresh = Instructions $panel 'Refresh'
    RequiresCall $refresh 'RimWorld.ITab_Pawn_Log_Utility' 'GenerateLogLinesFor'
    if (@($refresh | Where-Object { $_.opcode.Name -eq 'ldc.i4' -and $_.operand -eq 300 }).Count -ne 1) { throw 'Log native 300-entry cap changed.' }
    $row = Instructions $panel 'DrawRow'
    foreach ($method in @('IconFromPOV','IconColorFromPOV','CanBeClickedFromPOV','ClickedFromPOV','GetTipString','ToGameStringFromPOV')) {
        RequiresCall $row 'Verse.LogEntry' $method
    }
    RequiresCall $row 'RimWorld.ITab_Pawn_Log_Utility+LogLineDisplayable' 'Draw'
    $draw = Instructions $panel 'Draw'
    RequiresCall $draw 'RimWorld.ITab_Pawn_Log_Utility+LogLineDisplayable' 'Matches'
    RequiresCall $draw 'RimWorld.ITab_Pawn_Log_Utility+LogDrawData' 'StartNewDraw'
    $toolbar = Instructions $panel 'DrawToolbar'
    RequiresCall $toolbar 'RimWorld.ITab_Pawn_Log_Utility+LogLineDisplayable' 'AppendTo'
    RequiresCall $toolbar 'UnityEngine.GUIUtility' 'set_systemCopyBuffer'
    Write-Output 'PASS exact native adapter fields, 300-entry generator, click/tooltip/icon/debug callbacks, unknown-row fallback, seek/highlight and full native Copy.'
    Write-Output 'PASS Log native contracts. No patches installed, history generated or Unity rendering executed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
