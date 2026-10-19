param([string]$RimWorldDir='C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference='Stop'
$managed=Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolve=[ResolveEventHandler]{param($sender,$args) $path=Join-Path $managed (([Reflection.AssemblyName]$args.Name).Name+'.dll'); if(Test-Path -LiteralPath $path){return [Reflection.Assembly]::LoadFrom($path)};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolve)
try {
    $game=[Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $flags=[Reflection.BindingFlags]'Public,NonPublic,Static,Instance'
    $script:checks=0
    function TypeOf([string]$name){if($name.StartsWith('System.')){return [Type]::GetType($name,$true)};if($name.StartsWith('UnityEngine.')){return [Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.CoreModule.dll')).GetType($name,$true)};return $game.GetType($name,$true)}
    function Method([string]$owner,[string]$name,[string]$result,[string[]]$parameters){
        $type=TypeOf $owner
        $matches=@($type.GetMethods($flags)|Where-Object{$_.Name -eq $name -and $_.ReturnType.FullName -eq $result -and (@($_.GetParameters()|ForEach-Object{$_.ParameterType.FullName}) -join '|') -eq ($parameters -join '|')})
        if($matches.Count -ne 1){throw "Method contract failed: $owner.$name($($parameters -join ',')) -> $result ($($matches.Count) matches)"}
        $script:checks++
    }
    function Field([string]$owner,[string]$name,[string]$result){$field=(TypeOf $owner).GetField($name,$flags);if($null -eq $field -or $field.FieldType.FullName -ne $result){throw "Field contract failed: $owner.$name -> $result"};$script:checks++}
    Method 'RimWorld.MainTabWindow_Factions' 'ScrollToFaction' 'System.Void' @('RimWorld.Faction')
    Field 'RimWorld.MainTabWindow_Factions' 'scrollToFaction' 'RimWorld.Faction'
    foreach($name in @('GetOngoingEvents','GetRecentEvents','GetNaturalGoodwillExplanation')) {Method 'RimWorld.FactionUIUtility' $name 'Verse.TaggedString' @('RimWorld.Faction')}
    Method 'RimWorld.FactionUIUtility' 'DrawFactionIconWithTooltip' 'System.Void' @('UnityEngine.Rect','RimWorld.Faction')
    Field 'RimWorld.MainTabWindow_History' 'historyAutoRecorderGroup' 'RimWorld.HistoryAutoRecorderGroup'
    Field 'RimWorld.MainTabWindow_History' 'graphSection' 'Verse.FloatRange'
    Field 'RimWorld.MainTabWindow_History' 'curTab' 'RimWorld.MainTabWindow_History+HistoryTab'
    Field 'RimWorld.MainTabWindow_History' 'showLetters' 'System.Boolean'
    Field 'RimWorld.MainTabWindow_History' 'showMessages' 'System.Boolean'
    $tabList=[System.Collections.Generic.List[object]].GetGenericTypeDefinition().MakeGenericType((TypeOf 'Verse.TabRecord')).FullName
    Field 'RimWorld.MainTabWindow_History' 'tabs' $tabList
    $curveList=[System.Collections.Generic.List[object]].GetGenericTypeDefinition().MakeGenericType((TypeOf 'Verse.CurveMark')).FullName
    Method 'RimWorld.HistoryAutoRecorderGroup' 'DrawGraph' 'System.Void' @('UnityEngine.Rect','UnityEngine.Rect','Verse.FloatRange',$curveList)
    Method 'RimWorld.Planet.WorldInspectPane' 'DoWindowContents' 'System.Void' @('UnityEngine.Rect')
    Method 'RimWorld.Planet.WorldInspectPane' 'DoPaneContents' 'System.Void' @('UnityEngine.Rect')
    Method 'RimWorld.Planet.WorldInspectPane' 'DoInspectPaneButtons' 'System.Void' @('UnityEngine.Rect','System.Single&')
    Method 'RimWorld.Planet.WorldInspectPane' 'GetLabel' 'System.String' @('UnityEngine.Rect')
    Method 'RimWorld.Planet.WorldInspectPane' 'CloseOpenTab' 'System.Void' @()
    Method 'RimWorld.InspectPaneUtility' 'ExtraOnGUI' 'System.Void' @('RimWorld.IInspectPane')
    Method 'RimWorld.InspectPaneUtility' 'PaneWidthFor' 'System.Single' @('RimWorld.IInspectPane')
    Method 'RimWorld.InspectPaneUtility' 'InterfaceToggleTab' 'System.Void' @('Verse.InspectTabBase','RimWorld.IInspectPane')
    Method 'RimWorld.InspectPaneUtility' 'UpdateTabs' 'System.Void' @('RimWorld.IInspectPane')
    Field 'Verse.InspectTabBase' 'size' 'UnityEngine.Vector2'
    foreach($name in @('FillTab','ExtraOnGUI','UpdateSize')){Method 'Verse.InspectTabBase' $name 'System.Void' @()}
    Method 'Verse.InspectTabBase' 'get_StillValid' 'System.Boolean' @()
    Method 'Verse.InspectTabBase' 'get_TabRect' 'UnityEngine.Rect' @()
    Method 'Verse.Window' 'PostClose' 'System.Void' @()
    Method 'Verse.Window' 'SetInitialSizeAndPosition' 'System.Void' @()
    Method 'Verse.WindowStack' 'WindowStackOnGUI' 'System.Void' @()
    Method 'Verse.WindowStack' 'GetsInput' 'System.Boolean' @('Verse.Window')
    Method 'RimWorld.Planet.WorldGizmoUtility' 'WorldUIOnGUI' 'System.Void' @()
    Method 'RimWorld.Planet.WorldGizmoUtility' 'GetJumpToGizmo' 'Verse.Gizmo' @()
    Method 'RimWorld.Planet.WorldGizmoUtility' 'TryGetCaravanGizmo' 'System.Boolean' @('Verse.Gizmo&')
    Method 'Verse.Command' 'GizmoOnGUIInt' 'Verse.GizmoResult' @('UnityEngine.Rect','Verse.GizmoRenderParms')
    # BOTTOM31 actual chart data and definition-controlled scales.
    $recorderList=[System.Collections.Generic.List[object]].GetGenericTypeDefinition().MakeGenericType((TypeOf 'RimWorld.HistoryAutoRecorder')).FullName
    $floatList=[System.Collections.Generic.List[object]].GetGenericTypeDefinition().MakeGenericType([single]).FullName
    Field 'RimWorld.HistoryAutoRecorderGroup' 'recorders' $recorderList
    Field 'RimWorld.HistoryAutoRecorderGroup' 'def' 'RimWorld.HistoryAutoRecorderGroupDef'
    Field 'RimWorld.HistoryAutoRecorder' 'records' $floatList
    Field 'RimWorld.HistoryAutoRecorder' 'def' 'RimWorld.HistoryAutoRecorderDef'
    Field 'RimWorld.HistoryAutoRecorderDef' 'recordTicksFrequency' 'System.Int32'
    Field 'RimWorld.HistoryAutoRecorderDef' 'graphColor' 'UnityEngine.Color'
    Field 'RimWorld.HistoryAutoRecorderDef' 'valueFormat' 'System.String'
    Field 'RimWorld.HistoryAutoRecorderGroupDef' 'useFixedScale' 'System.Boolean'
    Field 'RimWorld.HistoryAutoRecorderGroupDef' 'fixedScale' 'UnityEngine.Vector2'
    Field 'RimWorld.HistoryAutoRecorderGroupDef' 'integersOnly' 'System.Boolean'
    Field 'RimWorld.HistoryAutoRecorderGroupDef' 'onlyPositiveValues' 'System.Boolean'
    Method 'Verse.CurveMark' 'get_X' 'System.Single' @()
    Method 'Verse.CurveMark' 'get_Message' 'System.String' @()
    Method 'Verse.CurveMark' 'get_Color' 'UnityEngine.Color' @()
    Method 'Verse.SimpleCurveDrawerStyle' 'get_LabelX' 'System.String' @()
    Write-Output "PASS $script:checks installed native metadata contracts; no patches, static game initializers, gameplay or UI invoked."
}
finally{[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolve)}
