param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\work\bio-build\ReframedUI.dll'),
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
    $flags = [Reflection.BindingFlags]'Public,NonPublic,Static,DeclaredOnly'
    $contracts = @(
        @{Type='RimWorld.CharacterCardUtility';Name='WorkTagsFrom';Return='System.Collections.Generic.IEnumerable`1[Verse.WorkTags]';Args='Verse.WorkTags'},
        @{Type='RimWorld.CharacterCardUtility';Name='GetWorkTypeDisabledCausedBy';Return='System.String';Args='Verse.Pawn,Verse.WorkTags'},
        @{Type='RimWorld.CharacterCardUtility';Name='GetWorkTypesDisabledByWorkTag';Return='System.String';Args='Verse.WorkTags'},
        @{Type='RimWorld.CharacterCardUtility';Name='GetDisabledWorkTagLabelColor';Return='UnityEngine.Color';Args='Verse.Pawn,Verse.WorkTags'},
        @{Type='RimWorld.CharacterCardUtility';Name='GetTitleTipString';Return='System.String';Args='Verse.Pawn,RimWorld.Faction,RimWorld.RoyalTitle,System.Int32'},
        @{Type='RimWorld.SkillUI';Name='GetSkillDescription';Return='System.String';Args='RimWorld.SkillRecord'},
        @{Type='RimWorld.CharacterCardUtility';Name='PawnCardSize';Return='UnityEngine.Vector2';Args='Verse.Pawn'},
        @{Type='RimWorld.CharacterCardUtility';Name='DrawCharacterCard';Return='System.Void';Args='UnityEngine.Rect,Verse.Pawn,System.Action,UnityEngine.Rect,System.Boolean'}
    )
    foreach ($row in $contracts) {
        $matches = @($game.GetType($row.Type, $true).GetMethods($flags) | Where-Object {
            $_.Name -eq $row.Name -and $_.ReturnType.ToString() -eq $row.Return -and
            (($_.GetParameters() | ForEach-Object { $_.ParameterType.ToString() }) -join ',') -eq $row.Args
        })
        if ($matches.Count -ne 1) { throw ('Bio native member changed: ' + $row.Type + '.' + $row.Name) }
    }
    $panel = $mod.GetType('ReframedUI.PawnBioPanel', $true)
    $read = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    $target = [HarmonyLib.AccessTools]::Method($game.GetType('RimWorld.CharacterCardUtility', $true), 'DrawCharacterCard')
    $il = @($read.Invoke($null, @($target, $null)))
    $topIndex = -1
    for ($i=0; $i -lt $il.Count; $i++) {
        if ($il[$i].operand -is [Reflection.MethodInfo] -and $il[$i].operand.Name -eq 'DoTopStack') { $topIndex = $i; break }
    }
    if ($topIndex -lt 12) { throw 'Native Bio body boundary not found.' }
    # Native body starts at nameRect.height(30) + 10, below the 32px header.
    $boundary = @($il[($topIndex-11)..($topIndex-7)])
    if ($boundary[0].opcode.Name -ne 'call' -or $boundary[0].operand.Name -ne 'get_height' -or
        $boundary[1].opcode.Name -ne 'ldc.r4' -or $boundary[1].operand -ne 10 -or
        $boundary[2].opcode.Name -ne 'add' -or $boundary[3].opcode.Name -ne 'stloc.s') {
        throw 'Native Bio header/body spacing changed; clipped header needs another audit.'
    }
    function Copy-Codes($codes) {
        $list = New-Object 'System.Collections.Generic.List[HarmonyLib.CodeInstruction]'
        foreach ($code in $codes) { $list.Add([HarmonyLib.CodeInstruction]::new($code)) }
        return ,$list
    }
    function Same-Markers($a, $b) {
        if ($a.labels.Count -ne $b.labels.Count -or $a.blocks.Count -ne $b.blocks.Count) { throw 'Changed native control-flow marker count.' }
        for ($j=0; $j -lt $a.labels.Count; $j++) { if (-not [object]::Equals($a.labels[$j],$b.labels[$j])) { throw 'Changed native label.' } }
        for ($j=0; $j -lt $a.blocks.Count; $j++) {
            if ($a.blocks[$j].blockType -ne $b.blocks[$j].blockType -or $a.blocks[$j].catchType -ne $b.blocks[$j].catchType) { throw 'Changed native exception block.' }
        }
    }
    function Same-CallStack($a, $b) {
        if (-not $a.IsStatic -or -not $b.IsStatic -or $a.ReturnType -ne $b.ReturnType -or
            (($a.GetParameters().ParameterType.FullName -join '|') -ne ($b.GetParameters().ParameterType.FullName -join '|'))) { throw 'Bio wrapper changed evaluation stack.' }
    }
    $static = [Reflection.BindingFlags]'Static,NonPublic'
    $auditRows = @(
        @{Type='Verse.Dialog_NamePawn';Method='DoWindowContents';Patch='PawnBioActionsNameContentsPatch';Expected=2;Names=@('Footer','Footer')},
        @{Type='Verse.Dialog_NamePawn+NameContext';Method='MakeRow';Patch='PawnBioActionsNameRowsPatch';Expected=4;Names=@('TaggedLabel','RowLabel','RowButton','RowButton')},
        @{Type='RimWorld.CharacterCardUtility';Method='DoNameInputRect';Patch='PawnBioActionsNameInputPatch';Expected=1;Names=@('TextField')}
    )
    foreach ($row in $auditRows) {
        $method = [HarmonyLib.AccessTools]::Method($game.GetType($row.Type,$true),$row.Method)
        $before = @($read.Invoke($null,@($method,$null)))
        $inputCodes = Copy-Codes $before
        $arguments = New-Object 'object[]' 1; $arguments[0] = $inputCodes.PSObject.BaseObject
        $after = @($mod.GetType(('ReframedUI.'+$row.Patch),$true).GetMethod('Transpiler',$static).Invoke($null,$arguments))
        if ($before.Count -ne $after.Count) { throw ('Unexpected name instructions: '+$row.Method) }
        $wrappers = @()
        for ($i=0; $i -lt $before.Count; $i++) {
            Same-Markers $before[$i] $after[$i]
            if ($before[$i].opcode -ne $after[$i].opcode) { throw 'Unexpected naming opcode change.' }
            if ([object]::Equals($before[$i].operand,$after[$i].operand)) { continue }
            $a=$before[$i].operand; $b=$after[$i].operand
            if (-not ($a -is [Reflection.MethodInfo]) -or -not ($b -is [Reflection.MethodInfo]) -or $b.DeclaringType.FullName -ne 'ReframedUI.PawnBioActionsNaming') { throw 'Unexpected naming operand change.' }
            Same-CallStack $a $b; $wrappers += $b.Name
        }
        if ($wrappers.Count -ne $row.Expected -or (($wrappers | Sort-Object) -join '|') -ne (($row.Names | Sort-Object) -join '|')) { throw ('Wrong name substitutions: '+$row.Method) }
        Write-Output ('PASS exact native naming seam '+$row.Method+': '+$wrappers.Count+' typed substitutions; other instructions/control flow unchanged')
    }
    $generator = [Reflection.Emit.DynamicMethod]::new('BioHeaderAudit',[void],[Type[]]@(),$true).GetILGenerator()
    $before = @($read.Invoke($null,@($target,$generator)))
    $inputCodes = Copy-Codes $before
    $arguments = New-Object 'object[]' 2; $arguments[0]=$inputCodes.PSObject.BaseObject; $arguments[1]=$generator
    $after = @($mod.GetType('ReframedUI.PawnBioActionsHeaderPatch',$true).GetMethod('Transpiler',$static).Invoke($null,$arguments))
    if (-not $mod.GetType('ReframedUI.PawnBioActionsNative',$true).GetField('Ready',$static).GetValue($null)) { throw 'Header seam rejected.' }
    $boundaryIndex=$topIndex-12
    if ($after.Count -ne $before.Count+4 -or $before[$boundaryIndex].blocks.Count -ne 0) { throw 'Header isolation insertion/boundary changed.' }
    $insert=@($after[$boundaryIndex..($boundaryIndex+3)])
    if ($insert[0].operand.Name -ne 'StopBeforeBody' -or $insert[1].opcode.Name -ne 'brfalse' -or $insert[2].operand.Name -ne 'EndGroup' -or $insert[3].opcode.Name -ne 'ret') { throw 'Header isolation does not end native group and return before body.' }
    if ($after[$boundaryIndex+4].labels.Count -ne 1 -or -not [object]::Equals($insert[1].operand,$after[$boundaryIndex+4].labels[0])) { throw 'Header outside-scope resume target changed.' }
    if ($insert[0].labels.Count -ne $before[$boundaryIndex].labels.Count) { throw 'Header incoming branches were lost.' }
    for ($i=0; $i -lt $insert[0].labels.Count; $i++) { if (-not [object]::Equals($insert[0].labels[$i],$before[$boundaryIndex].labels[$i])) { throw 'Header incoming branch changed.' } }
    $substitutions=0
    for ($i=0; $i -lt $before.Count; $i++) {
        $a=$before[$i]; $b=$after[$i+$(if($i -ge $boundaryIndex){4}else{0})]
        if ($i -ne $boundaryIndex) { Same-Markers $a $b }
        if ($a.opcode -ne $b.opcode) { throw 'Header original opcode changed.' }
        if ([object]::Equals($a.operand,$b.operand)) { continue }
        if (-not ($a.operand -is [Reflection.MethodInfo]) -or $a.operand.DeclaringType.FullName -ne 'Verse.Widgets' -or $a.operand.Name -ne 'ButtonImage' -or
            $b.operand.DeclaringType.FullName -ne 'ReframedUI.PawnBioActionsNative' -or $b.operand.Name -ne 'Button') { throw 'Unexpected header substitution.' }
        Same-CallStack $a.operand $b.operand; $substitutions++
    }
    if ($substitutions -ne 4) { throw 'Expected four exact native action adapters.' }
    if (-not $mod.GetType('ReframedUI.PawnBioActionsNaming',$true).GetProperty('Ready',$static).GetValue($null,$null)) { throw 'Naming adapters did not all bind.' }
    if (-not $panel.GetProperty('Ready', $static).GetValue($null, $null)) { throw 'Bio private native tooltip/data/action delegates did not bind.' }
    Write-Output 'PASS4 native action adapters + exact body isolation; naming accepts/title updates remain native. No patches installed or Unity rendering executed.'
    Write-Output 'PASS8 installed native Bio signatures and all private data delegates.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
