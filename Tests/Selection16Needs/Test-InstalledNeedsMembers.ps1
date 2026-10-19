param([string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run in Windows PowerShell 5.1.' }
$managed = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
[Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.CoreModule.dll')) | Out-Null
[Reflection.Assembly]::LoadFrom((Join-Path $managed 'UnityEngine.IMGUIModule.dll')) | Out-Null
$game = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
$flags = [Reflection.BindingFlags]'Public,NonPublic,Instance,Static,DeclaredOnly'
$rows = @(
    @{ Type='RimWorld.PawnNeedsUIUtility'; Name='GetThoughtGroupsInDisplayOrder'; Static=$true; Return='System.Void'; Args='RimWorld.Need_Mood,System.Collections.Generic.List`1[RimWorld.Thought]' },
    @{ Type='RimWorld.PawnNeedsUIUtility'; Name='GetLeadingThoughtInGroup'; Static=$true; Return='RimWorld.Thought'; Args='System.Collections.Generic.List`1[RimWorld.Thought]' },
    @{ Type='RimWorld.PawnNeedsUIUtility'; Name='SortInDisplayOrder'; Static=$true; Return='System.Void'; Args='System.Collections.Generic.List`1[RimWorld.Need]' },
    @{ Type='RimWorld.ThoughtHandler'; Name='GetMoodThoughts'; Static=$false; Return='System.Void'; Args='RimWorld.Thought,System.Collections.Generic.List`1[RimWorld.Thought]' },
    @{ Type='RimWorld.ThoughtHandler'; Name='MoodOffsetOfGroup'; Static=$false; Return='System.Single'; Args='RimWorld.Thought' },
    @{ Type='RimWorld.Thought'; Name='GroupsWith'; Static=$false; Return='System.Boolean'; Args='RimWorld.Thought' },
    @{ Type='RimWorld.Thought'; Name='get_CurStageIndex'; Static=$false; Return='System.Int32'; Args='' },
    @{ Type='RimWorld.Thought'; Name='get_VisibleInNeedsTab'; Static=$false; Return='System.Boolean'; Args='' },
    @{ Type='RimWorld.Thought'; Name='get_CausedByBeliefInPrecept'; Static=$false; Return='System.String'; Args='' },
    @{ Type='RimWorld.Need'; Name='get_ShowOnNeedList'; Static=$false; Return='System.Boolean'; Args='' },
    @{ Type='RimWorld.IdeoUIUtility'; Name='OpenIdeoInfo'; Static=$true; Return='System.Void'; Args='RimWorld.Ideo' },
    @{ Type='RimWorld.IdeoUtility'; Name='IdeoCausesHumanMeatCravings'; Static=$true; Return='System.Boolean'; Args='RimWorld.Ideo' }
)
foreach ($row in $rows) {
    $matches = @($game.GetType($row.Type, $true).GetMethods($flags) | Where-Object {
        $_.Name -eq $row.Name -and $_.IsStatic -eq $row.Static -and $_.ReturnType.ToString() -eq $row.Return -and
        (($_.GetParameters() | ForEach-Object { $_.ParameterType.ToString() }) -join ',') -eq $row.Args
    })
    if ($matches.Count -ne 1) { throw ('Native Needs method changed: ' + $row.Type + '.' + $row.Name) }
}
foreach ($row in @(
    @{Type='RimWorld.Thought';Name='sourcePrecept';FieldType='RimWorld.Precept'},
    @{Type='RimWorld.ThoughtDef';Name='stagesStack';FieldType='System.Boolean'},
    @{Type='RimWorld.Thought_Memory';Name='age';FieldType='System.Int32'},
    @{Type='Verse.AI.Pawn_MindState';Name='lastHumanMeatIngestedTick';FieldType='System.Int32'}
)) {
    $field = $game.GetType($row.Type, $true).GetField($row.Name, $flags)
    if (-not $field -or $field.IsStatic -or $field.FieldType.ToString() -ne $row.FieldType) { throw ('Native Needs field changed: ' + $row.Type + '.' + $row.Name) }
}
Write-Output 'PASS 12 native Needs method and4field contracts. Metadata only; no patches, game callbacks or Unity rendering executed.'
