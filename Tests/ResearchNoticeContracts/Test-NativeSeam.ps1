param(
    [Parameter(Mandatory=$true)][string]$ModAssembly,
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run in Windows PowerShell5.1.' }
$managed = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] { param($sender,$eventArgs)
    $path = Join-Path $managed (([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll')
    if (Test-Path -LiteralPath $path) { return [Reflection.Assembly]::LoadFrom($path) }; return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom($HarmonyAssembly) | Out-Null
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags = [Reflection.BindingFlags]'Static,NonPublic'
    $target = [HarmonyLib.AccessTools]::Method($game.GetType('RimWorld.ResearchManager'), 'FinishProject')
    $reader = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    $original = $reader.Invoke($null,@($target,$null)); $before = @($original)
    $patch = $mod.GetType('ReframedUI.ResearchNoticeCreationPatch').GetMethod('Transpiler',$flags)
    $ready = $mod.GetType('ReframedUI.ResearchNotice').GetField('SeamReady',$flags)
    $after = @($patch.Invoke($null,@(,$original)))
    if (-not $ready.GetValue($null) -or $after.Count -ne $before.Count+1) { throw 'exact creation seam not accepted' }
    $at=0; $adapted=0
    for($i=0;$i -lt $before.Count;$i++) {
        $a=$before[$i]; $b=$after[$at++]
        if ($a.opcode -eq [Reflection.Emit.OpCodes]::Newobj -and $a.operand.DeclaringType.FullName -eq 'Verse.Dialog_NodeTree') {
            $adapted++
            if ($b.opcode -ne [Reflection.Emit.OpCodes]::Ldarg_1) { throw 'actual project argument missing' }
            if (($a.labels -join '|') -ne ($b.labels -join '|') -or ($a.blocks -join '|') -ne ($b.blocks -join '|')) { throw 'incoming control flow changed' }
            $b=$after[$at++]
            if ($b.opcode -ne [Reflection.Emit.OpCodes]::Call -or $b.operand.Name -ne 'Create' -or $b.labels.Count -ne 0 -or $b.blocks.Count -ne 0) { throw 'constructor adapter malformed' }
            if ($b.operand.ReturnType.FullName -ne 'Verse.Dialog_NodeTree' -or
                ($b.operand.GetParameters().ParameterType.FullName -join '|') -ne 'Verse.DiaNode|System.Boolean|System.Boolean|System.String|Verse.ResearchProjectDef') { throw 'constructor stack contract changed' }
        } elseif ($a.opcode -ne $b.opcode -or $a.operand -ne $b.operand -or ($a.labels -join '|') -ne ($b.labels -join '|') -or ($a.blocks -join '|') -ne ($b.blocks -join '|')) {
            throw "Unrelated native instruction changed at$i"
        }
    }
    if($adapted -ne 1) { throw 'must adapt exactly one constructor' }
    Write-Output 'PASS exact1 research-dialog constructor adapter; all other native completion/discovery/control-flow instructions unchanged.'
    function Reject($instructions,$name) {
        $unchanged=@($patch.Invoke($null,@(,$instructions)))
        if($ready.GetValue($null) -or $unchanged.Count -ne $instructions.Count) { throw "$name did not fail open" }
        for($j=0;$j -lt $unchanged.Count;$j++) { if(-not [object]::ReferenceEquals($unchanged[$j],$instructions[$j])) { throw "$name mutated rejected IL" } }
        Write-Output "PASS $name retains original IL and refuses ownership."
    }
    $missing=[System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    foreach($code in $after){$missing.Add($code)}
    Reject $missing 'missing-constructor'
    $duplicate=[System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    foreach($code in $before){$duplicate.Add($code)}
    $ctor=$before | Where-Object { $_.opcode -eq [Reflection.Emit.OpCodes]::Newobj -and $_.operand.DeclaringType.FullName -eq 'Verse.Dialog_NodeTree' } | Select-Object -First 1
    $index=[Array]::IndexOf($before,$ctor)
    $duplicate.Add([HarmonyLib.CodeInstruction]::new($ctor));$duplicate.Add([HarmonyLib.CodeInstruction]::new($before[$index+1]))
    Reject $duplicate 'duplicate-constructor'
    $malformed=[System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    foreach($code in $before){$malformed.Add([HarmonyLib.CodeInstruction]::new($code))}
    $malformed.Insert($index+1,[HarmonyLib.CodeInstruction]::new([Reflection.Emit.OpCodes]::Nop))
    Reject $malformed 'nonadjacent-native-add'
    $boundary=[System.Collections.Generic.List[HarmonyLib.CodeInstruction]]::new()
    foreach($code in $before){$boundary.Add([HarmonyLib.CodeInstruction]::new($code))}
    $boundary[$index].blocks.Add([HarmonyLib.ExceptionBlock]::new([HarmonyLib.ExceptionBlockType]::BeginExceptionBlock))
    Reject $boundary 'constructor-exception-boundary'

    $contracts = @(
        @('Verse.Dialog_NodeTree','get_InitialSize','UnityEngine.Vector2',@()),
        @('Verse.Dialog_NodeTree','get_InteractiveNow','System.Boolean',@()),
        @('Verse.Dialog_NodeTree','DoWindowContents','System.Void',@('UnityEngine.Rect')),
        @('Verse.Dialog_NodeTree','PostClose','System.Void',@()),
        @('Verse.DiaOption','Activate','System.Void',@()),
        @('Verse.Window','get_Margin','System.Single',@()),
        @('Verse.Window','SetInitialSizeAndPosition','System.Void',@()),
        @('Verse.WindowStack','GetsInput','System.Boolean',@('Verse.Window'))
    )
    foreach($contract in $contracts) {
        $method=[HarmonyLib.AccessTools]::Method($game.GetType($contract[0]),$contract[1])
        if(-not $method -or $method.ReturnType.FullName -ne $contract[2] -or
            ($method.GetParameters().ParameterType.FullName -join '|') -ne ($contract[3] -join '|')) { throw "Native method contract changed: $($contract[0]).$($contract[1])" }
    }
    $fields = @(
        @('Verse.Dialog_NodeTree','curNode','Verse.DiaNode'),
        @('Verse.Dialog_NodeTree','screenFillColor','UnityEngine.Color'),
        @('Verse.DiaNode','options','System.Collections.Generic.List`1[[Verse.DiaOption, Assembly-CSharp, Version=1.6.9676.17735, Culture=neutral, PublicKeyToken=null]]'),
        @('Verse.DiaNode','text','Verse.TaggedString'),
        @('Verse.DiaOption','dialog','Verse.Window'), @('Verse.DiaOption','action','System.Action'),
        @('Verse.DiaOption','resolveTree','System.Boolean'), @('Verse.DiaOption','disabled','System.Boolean'),
        @('Verse.DiaOption','link','Verse.DiaNode'),
        @('Verse.DiaOption','linkLateBind','System.Func`1[[Verse.DiaNode, Assembly-CSharp, Version=1.6.9676.17735, Culture=neutral, PublicKeyToken=null]]'),
        @('Verse.DiaOption','hyperlink','Verse.Dialog_InfoCard+Hyperlink'),
        @('Verse.Window','doWindowBackground','System.Boolean'), @('Verse.Window','drawShadow','System.Boolean'),
        @('Verse.Window','doCloseX','System.Boolean'), @('Verse.Window','doCloseButton','System.Boolean'),
        @('Verse.Window','windowRect','UnityEngine.Rect')
    )
    foreach($contract in $fields) {
        $field=[HarmonyLib.AccessTools]::Field($game.GetType($contract[0]),$contract[1])
        if(-not $field -or $field.FieldType.FullName -ne $contract[2]) { throw "Native field contract changed: $($contract[0]).$($contract[1])" }
    }
    Write-Output "PASS $($contracts.Count) additional method/$($fields.Count) field metadata contracts."
} finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
