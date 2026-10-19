param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\work\bottom31-build\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Use Windows PowerShell 5.1.' }
$managed=Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver=[ResolveEventHandler]{param($sender,$args) $path=Join-Path $managed (([Reflection.AssemblyName]$args.Name).Name+'.dll'); if(Test-Path -LiteralPath $path){return [Reflection.Assembly]::LoadFrom($path)};return $null}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path)|Out-Null
    $game=[Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $mod=[Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags=[Reflection.BindingFlags]'Static,NonPublic'
    $adapter=$mod.GetType('ReframedUI.KnowledgeMenusNative',$true)
    foreach($ready in @('ResearchReady','QuestsReady')) { if(-not $adapter.GetProperty($ready,$flags).GetValue($null,$null)){throw ('Native adapter not ready: '+$ready)} }
    $count=0
    foreach($name in @('research','quests')) {
        $dictionary=$adapter.GetField($name,$flags).GetValue($null)
        foreach($pair in $dictionary.GetEnumerator()) {
            if($null -eq $pair.Value -or $pair.Value.Name -ne $pair.Key -or $pair.Value.IsStatic){throw ('Invalid native method binding: '+$pair.Key)}
            if($pair.Value.DeclaringType.FullName -ne ('RimWorld.MainTabWindow_'+$(if($name -eq 'research'){'Research'}else{'Quests'}))){throw 'Wrong native declaring type'}
            $count++
        }
    }
    foreach($name in @('ResearchSelected','QuestSelected','QuestTab','QuestShowAll','QuestList','QuestSearch','ResearchSearch','ResearchMatches','BeginResearch','SearchResearch','SortQuests','QuestTime')) {
        if($null -eq $adapter.GetField($name,$flags).GetValue($null)){throw ('Missing native field/method: '+$name)}
        $count++
    }
    $utf8=[Text.UTF8Encoding]::new($false,$true)
    foreach($file in Get-ChildItem -LiteralPath (Join-Path $PSScriptRoot '..\..\Source') -Filter 'KnowledgeMenus*.cs') { $null=$utf8.GetString([IO.File]::ReadAllBytes($file.FullName)) }
    $reader=[HarmonyLib.PatchProcessor].GetMethods()|Where-Object{$_.Name -eq 'GetOriginalInstructions' -and $_.GetParameters().Count -eq 2 -and -not $_.GetParameters()[1].ParameterType.IsByRef}|Select-Object -First 1
    function Calls([string]$owner,[string]$method) {
        $native=[HarmonyLib.AccessTools]::Method($game.GetType($owner,$true),$method)
        return @($reader.Invoke($null,@($native,$null))|Where-Object{$_.operand -is [Reflection.MethodInfo]}|ForEach-Object{$_.operand.DeclaringType.FullName+'.'+$_.operand.Name})
    }
    foreach($axis in @('X','Y')) {
        $position=Calls 'RimWorld.MainTabWindow_Research' ('Pos'+$axis)
        if(-not ($position -contains ('Verse.ResearchProjectDef.get_ResearchView'+$axis)) -or -not ($position -contains ('RimWorld.MainTabWindow_Research.CoordToPixels'+$axis))){throw ('Native graph coordinate path changed: '+$axis)}
    }
    $start=Calls 'RimWorld.MainTabWindow_Research' 'AttemptBeginResearch'
    if(-not ($start -contains 'RimWorld.MainTabWindow_Research.ComputeUnlockedDefsThatHaveMissingMemes') -or -not ($start -contains 'Verse.Dialog_MessageBox.CreateConfirmation')){throw 'Native missing-meme confirmation path changed'}
    $button=Calls 'RimWorld.MainTabWindow_Research' 'DrawStartButton'
    foreach($reason in @('get_PrerequisitesCompleted','get_TechprintRequirementMet','get_PlayerHasAnyAppropriateResearchBench','get_PlayerMechanitorRequirementMet','get_InspectionRequirementsMet','get_AnalyzedThingsRequirementsMet')) { if(-not ($button -contains ('Verse.ResearchProjectDef.'+$reason))){throw ('Native locked reason changed: '+$reason)} }
    if(-not ($button -contains 'Verse.TooltipHandler.TipRegion') -or -not ($button -contains 'RimWorld.MainTabWindow_Research.AttemptBeginResearch')){throw 'Native start control tooltip/action path changed'}
    $project=Calls 'RimWorld.MainTabWindow_Research' 'DrawProjectInfo'
    foreach($action in @('SetCurrentProject','FinishProject','ApplyTechprint')) { if(-not ($project -contains ('RimWorld.ResearchManager.'+$action))){throw ('Native developer action changed: '+$action)} }
    $graph=Calls 'RimWorld.MainTabWindow_Research' 'DrawRightRect'
    if(-not ($graph -contains 'UnityEngine.GUIUtility.set_systemCopyBuffer') -or -not ($graph -contains 'RimWorld.MainTabWindow_Research.ListProjects')){throw 'Native graph editing/export path changed'}
    $accept=Calls 'RimWorld.MainTabWindow_Quests' 'AcceptQuestByInterface'
    if(-not ($accept -contains 'RimWorld.QuestUtility.CanAcceptQuest') -or -not ($accept -contains 'RimWorld.QuestUtility.CanPawnAcceptQuest')){throw 'Native acceptance eligibility path changed'}
    $sort=Calls 'RimWorld.MainTabWindow_Quests' 'SortQuestsByTab'
    if(-not ($sort -contains 'RimWorld.MainTabWindow_Quests.ShouldListNow')){throw 'Native list filter path changed'}
    $search=Calls 'RimWorld.MainTabWindow_Research' 'UpdateSearchResults'
    if(-not ($search -contains 'Verse.ResearchProjectDef.get_IsHidden')){throw 'Native hidden research search gate changed'}
    Write-Output ('PASS '+$count+' exact native adapter method/field bindings + original research confirmation, quest eligibility/filter, hidden-search/locked-start/developer-action/graph-position IL paths + source UTF8 checks.')
    Write-Output 'No patches installed, native window constructors run, game launched or Unity GUI rendered.'
} finally {[AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver)}
