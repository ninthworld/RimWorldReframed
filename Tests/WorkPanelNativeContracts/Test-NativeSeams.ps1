param([string]$AuditPath = "$PSScriptRoot/../../work/work-native/native.json")
$ErrorActionPreference = 'Stop'
$audit = Get-Content -Raw -LiteralPath $AuditPath | ConvertFrom-Json -AsHashtable
if ($audit.parse_failures -ne 0) { throw 'Native audit has parse failures' }
$script:checks = 0
function Assert($condition, [string]$name) { $script:checks++; if (!$condition) { throw $name } }
function Method([string]$type, [string]$name) {
    $t = @($audit.focused_types | Where-Object { $_['type'] -eq $type })
    $m = @($t[0].methods | Where-Object { $_['method'] -eq $name })
    Assert ($m.Count -eq 1) "Unique native method $type.$name"
    return $m[0]
}
function Calls($method, [string]$name) { return @($method.instructions | Where-Object { $_.operand -eq $name }).Count }
$box = Method 'RimWorld.WidgetsWork' 'DrawWorkBoxFor'
Assert (($box.parameterTypes -join ',') -eq 'Single,Single,Verse.Pawn,Verse.WorkTypeDef,Boolean') 'Exact native work box signature'
Assert ((Calls $box 'Verse.Widgets.ButtonInvisible') -eq 1) 'Unique scoped checkbox activation seam'
Assert ((Calls $box 'RimWorld.Pawn_WorkSettings.SetPriority') -eq 4) 'Exact four native setters for scoped one-click adapter'
foreach ($callback in @('Verse.Pawn.WorkTypeIsDisabled','RimWorld.PawnUtility.IsWorkTypeDisabledByAge',
    'RimWorld.Pawn_WorkSettings.SetPriority','RimWorld.Pawn_WorkSettings.WorkIsActive',
    'RimWorld.Pawn_SkillTracker.AverageOfRelevantSkillsFor','RimWorld.Ideo.IsWorkTypeConsideredDangerous',
    'RimWorld.PlayerKnowledgeDatabase.KnowledgeDemonstrated','UnityEngine.Event.Use')) {
    Assert ((Calls $box $callback) -gt 0) "Actual native input retains $callback"
}
foreach ($literal in @('RimWorld.SoundDefOf.DragSlider','RimWorld.SoundDefOf.Crunch',
    'RimWorld.SoundDefOf.DislikedWorkTypeActivated','RimWorld.SoundDefOf.Checkbox_TurnedOff',
    'RimWorld.SoundDefOf.Checkbox_TurnedOn','RimWorld.SoundDefOf.ClickReject')) {
    Assert ((Calls $box $literal) -gt 0) "Actual native input retains $literal"
}
$columns = Method 'RimWorld.PawnTable' 'get_Columns'
Assert ((Calls $columns 'RimWorld.PawnColumnWorker.get_VisibleCurrently') -eq 1) 'Table columns already preserve native visibility'
$roster = Method 'RimWorld.PawnTable' 'get_PawnsListForReading'
Assert ((Calls $roster 'RimWorld.PawnTable.RecacheIfDirty') -eq 1) 'Roster uses native cached sorted pawns'
$post = Method 'RimWorld.MainTabWindow_PawnTable' 'PostOpen'
Assert ((Calls $post 'RimWorld.MainTabWindow_PawnTable.CreateTable') -eq 1) 'Original PostOpen owns table creation'
$setter = Method 'RimWorld.Pawn_WorkSettings' 'SetPriority'
Assert (($setter.parameterTypes -join ',') -eq 'Verse.WorkTypeDef,Int32') 'Exact priority observation signature'
Assert (($setter.parameters[0].name -eq 'w')) 'Harmony argument binding verified'
Assert ((Calls $setter 'Verse.AI.Pawn_JobTracker.Notify_WorkTypeDisabled') -eq 1) 'SetPriority retains native disable notifications'
$get = Method 'RimWorld.Pawn_WorkSettings' 'GetPriority'
Assert ((Calls $get 'RimWorld.PlaySettings.useWorkPriorities') -gt 0) 'Simple mode effective priorities are natively masked'
$copy = Method 'RimWorld.PawnColumnWorker_CopyPasteWorkPriorities' 'CopyFrom'
$paste = Method 'RimWorld.PawnColumnWorker_CopyPasteWorkPriorities' 'PasteTo'
Assert (($copy.attributes -match 'Family') -and ($paste.attributes -match 'Family')) 'Native protected copy/paste seam'
Assert ((Calls $copy 'Verse.Pawn.WorkTypeIsDisabled') -eq 1) 'Copy native capability mask'
Assert ((Calls $paste 'Verse.Pawn.WorkTypeIsDisabled') -eq 1) 'Paste native capability mask'
Assert ((Calls $paste 'RimWorld.Pawn_WorkSettings.SetPriority') -eq 1) 'Paste native setter owns notifications'
$mode = Method 'RimWorld.MainTabWindow_Work' 'DoManualPrioritiesCheckbox'
Assert ((Calls $mode 'RimWorld.PawnsFinder.get_AllMapsWorldAndTemporary_Alive') -eq 1) 'Manual toggle notifies native global pawn scope'
Assert ((Calls $mode 'RimWorld.Pawn_WorkSettings.Notify_UseWorkPrioritiesChanged') -eq 1) 'Manual toggle invalidates work-giver cache'
$header = Method 'RimWorld.PawnColumnWorker_WorkPriority' 'HeaderClicked'
Assert (($header.parameterTypes -join ',') -eq 'UnityEngine.Rect,RimWorld.PawnTable') 'Exact native bulk action signature'
Assert ((Calls $header 'UnityEngine.Event.get_shift') -eq 1) 'Bulk action requires original Shift modifier'
Assert ((Calls $header 'RimWorld.PawnTable.get_PawnsListForReading') -eq 1) 'Bulk action uses native roster'
Assert ((Calls $header 'Verse.Pawn.WorkTypeIsDisabled') -eq 1) 'Bulk action retains native disabled masks'
$baseHeader = Method 'RimWorld.PawnColumnWorker' 'HeaderClicked'
Assert ((Calls $baseHeader 'RimWorld.PawnTable.SortBy') -eq 6) 'Both directions preserve native three-state sort cycles'
$tip = Method 'RimWorld.WidgetsWork' 'TipForPawnWorker'
Assert ((Calls $tip 'RimWorld.Pawn_WorkSettings.GetPriority') -gt 0) 'Native tooltip explicitly reports actual effective priority'
Assert ((Calls $tip 'Verse.Pawn.GetReasonsForDisabledWorkType') -eq 1) 'Native tooltip retains real disabled reasons'
$status = Method 'RimWorld.PawnColumnWorker_Icon' 'DoCell'
Assert ((Calls $status 'RimWorld.PawnColumnWorker_Icon.ClickedIcon') -eq 1) 'Status adapter retains actual native icon callbacks'
Assert ((Calls $status 'Verse.TooltipHandler.TipRegion') -eq 1) 'Status adapter retains native tooltips'
function Canonical([string]$name) {
    $name = $name.Replace('<','[').Replace('>',']')
    return [regex]::Replace($name, '(?<![\w.])(Void|Boolean|Int32|Single|String)(?![\w])', 'System.$1')
}
$guards = Get-Content -Raw -LiteralPath "$PSScriptRoot/NativeGuards.json" | ConvertFrom-Json -AsHashtable
foreach ($guard in $guards.methods) {
    $t = @($audit.focused_types | Where-Object { $_.type -eq $guard.type })
    $matches = @($t[0].methods | Where-Object {
        $_.method -eq $guard.name -and (Canonical ($_.parameterTypes -join '|')) -eq ($guard.args -join '|')
    })
    Assert ($matches.Count -eq 1) "Guard resolves exact overload $($guard.type).$($guard.name)"
    Assert ((Canonical $matches[0].returnType) -eq $guard.returns) "Guard exact return $($guard.type).$($guard.name)"
    Assert (($matches[0].attributes -match 'Static') -eq $guard.static) "Guard exact staticness $($guard.type).$($guard.name)"
}
foreach ($guard in $guards.fields) {
    $t = @($audit.focused_types | Where-Object { $_.type -eq $guard.type })
    $f = @($t[0].fields | Where-Object { $_.name -eq $guard.name })
    Assert ($f.Count -eq 1) "Guard exact field $($guard.type).$($guard.name)"
    Assert ((Canonical $f[0].type) -eq $guard.fieldType) "Guard exact field type $($guard.type).$($guard.name)"
    Assert (($f[0].attributes -match 'Static') -eq $guard.static) "Guard exact field staticness $($guard.type).$($guard.name)"
}
Write-Output "PASS $script:checks installed Work metadata/IL seam contracts; target assembly never executed."

