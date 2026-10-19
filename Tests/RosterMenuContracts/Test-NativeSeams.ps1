param(
 [string]$RimWorldDir='C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
 [string]$ModAssembly=(Join-Path $PSScriptRoot '../../work/bottom31-build/ReframedUI.dll'),
 [string]$HarmonyAssembly=(Join-Path $env:USERPROFILE '.nuget/packages/lib.harmony/2.3.6/lib/net472/0Harmony.dll')
)
$ErrorActionPreference='Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run Windows PowerShell5.1.' }
$managed=Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
$resolver=[ResolveEventHandler] { param($s,$a) $p=Join-Path $managed (([Reflection.AssemblyName]$a.Name).Name+'.dll'); if(Test-Path -LiteralPath $p){return [Reflection.Assembly]::LoadFrom($p)};return $null }
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
 [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path)|Out-Null
 $game=[Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
 $mod=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
 $all=[Reflection.BindingFlags]'Instance,Static,Public,NonPublic,DeclaredOnly'
 $script:count=0
 function Check($ok,[string]$message){if(-not $ok){throw $message};$script:count++}
 function Method([string]$type,[string]$name,[string]$arguments,[string]$returns){
  $rows=@($game.GetType($type,$true).GetMethods($all)|Where-Object {$_.Name -eq $name -and (($_.GetParameters()|ForEach-Object {$_.ParameterType.FullName})-join '|') -eq $arguments})
  Check ($rows.Count -eq 1 -and $rows[0].ReturnType.FullName -eq $returns) ($type+'.'+$name)
  return $rows[0]
 }
 $field=$game.GetType('RimWorld.MainTabWindow_PawnTable').GetField('table',$all)
 Check ($field.IsPrivate -and $field.FieldType.FullName -eq 'RimWorld.PawnTable') 'native initialized table field'
 $null=Method 'RimWorld.PawnColumnWorker_Label' 'GetLabel' 'Verse.Pawn' 'Verse.TaggedString'
 foreach($method in @('DoCell','DoHeader','GetMinWidth','GetOptimalWidth','GetMaxWidth','GetMinCellHeight','GetMinHeaderHeight')){
  $type=$game.GetType('RimWorld.PawnColumnWorker');$rows=@($type.GetMethods($all)|Where-Object Name -eq $method)
  Check ($rows.Count -eq 1 -and $rows[0].IsPublic -and $rows[0].IsVirtual) ('native worker '+$method)
 }
 foreach($property in @('Columns','PawnsListForReading')){
  Check ($null -ne $game.GetType('RimWorld.PawnTable').GetProperty($property,$all).GetGetMethod()) ('native table '+$property)
 }
 $null=Method 'RimWorld.TimeAssignmentSelector' 'DrawTimeAssignmentSelectorGrid' 'UnityEngine.Rect' 'System.Void'
 $null=Method 'RimWorld.TimeAssignmentSelector' 'DrawTimeAssignmentSelectorFor' 'UnityEngine.Rect|RimWorld.TimeAssignmentDef' 'System.Void'
 $null=Method 'RimWorld.PawnColumnWorker' 'HeaderClicked' 'UnityEngine.Rect|RimWorld.PawnTable' 'System.Void'
 $null=Method 'RimWorld.PawnColumnWorker' 'GetHeaderTip' 'RimWorld.PawnTable' 'System.String'
 $null=Method 'RimWorld.PawnColumnDef' 'get_HeaderIcon' '' 'UnityEngine.Texture2D'
 $null=Method 'RimWorld.PawnColumnDef' 'get_HeaderInteractable' '' 'System.Boolean'
 foreach($name in @('MainTabWindow_Schedule','MainTabWindow_Assign','MainTabWindow_Animals','MainTabWindow_Wildlife')){
  Check ($game.GetType('RimWorld.'+$name).BaseType.FullName -eq 'RimWorld.MainTabWindow_PawnTable') ('native lifecycle '+$name)
 }
 $reader=[HarmonyLib.PatchProcessor].GetMethods()|Where-Object {$_.Name -eq 'GetOriginalInstructions' -and $_.GetParameters().Count -eq 2 -and -not $_.GetParameters()[1].ParameterType.IsByRef}|Select-Object -First 1
 function IL([Reflection.MethodBase]$method){return @($reader.Invoke($null,@($method,$null)))}
 $nativeInput=Method 'RimWorld.PawnColumnWorker_Timetable' 'DoTimeAssignment' 'UnityEngine.Rect|Verse.Pawn|System.Int32' 'System.Void'
 $nativeCalls=@(IL $nativeInput|Where-Object {$_.operand -is [Reflection.MethodInfo]}|ForEach-Object operand)
 Check (@($nativeCalls|Where-Object {$_.Name -eq 'GetMouseButton'}).Count -eq 1) 'native schedule uses held mouse state'
 Check (@($nativeCalls|Where-Object {$_.Name -eq 'SetAssignment'}).Count -eq 1) 'native schedule remains sole assignment callback'
 $owned=$mod.GetType('ReframedUI.RosterMenus',$true)
 $calls=@()
 foreach($type in $mod.GetTypes()|Where-Object {$_.FullName.StartsWith('ReframedUI.RosterMenus')}){
  foreach($method in $type.GetMethods($all)){
   if($null -ne $method.GetMethodBody()){$calls+=@(IL $method|Where-Object {$_.operand -is [Reflection.MethodInfo]}|ForEach-Object operand)}
  }
 }
 Check (@($calls|Where-Object Name -eq 'PawnTableOnGUI').Count -eq 0) 'custom renderer never wraps unchanged native table body'
 Check (@($calls|Where-Object Name -eq 'DoCell').Count -ge 1 -and @($calls|Where-Object Name -eq 'DoHeader').Count -ge 1) 'real native cell/header callbacks retained'
 Check (@($calls|Where-Object Name -in @('SetAssignment','SetWantedRecursive','SetPriority')).Count -eq 0) 'renderer does not recreate scheduling/training mechanics'
 $scope=$owned.GetMethod('Native',$all)
 Check (@($scope.GetMethodBody().ExceptionHandlingClauses|Where-Object Flags -eq Finally).Count -gt 0) 'native GUI/pointer scope has finally cleanup'
 $scopeCalls=@(IL $scope|Where-Object {$_.operand -is [Reflection.MethodInfo]}|ForEach-Object operand)
 Check (@($scopeCalls|Where-Object Name -eq 'set_mousePosition').Count -ge 2 -and @($scopeCalls|Where-Object Name -eq 'set_enabled').Count -ge 1) 'disabled native input suppresses pointer and restores it'
 Check (@($calls|Where-Object {$_.Name -eq 'GetLabel' -and $_.DeclaringType.FullName -eq 'RimWorld.PawnColumnWorker_Label'}).Count -eq 0) 'private label accessed through guarded reflection only'
 Write-Output "PASS $count installed native metadata/IL roster contracts; no game objects, callbacks, GUI or patches executed."
}
finally {[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)}
