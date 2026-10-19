param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\Assemblies\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run in Windows PowerShell 5.1.' }
$runtimeManaged = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $dependencyPath = Join-Path $runtimeManaged (([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll')
    if (Test-Path -LiteralPath $dependencyPath) { return [Reflection.Assembly]::LoadFrom($dependencyPath) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path) | Out-Null
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $runtimeManaged 'Assembly-CSharp.dll'))
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags = [Reflection.BindingFlags]'Static,NonPublic'
    $seams = $mod.GetType('ReframedUI.ArchitectSeams', $true)
    $readInstructions = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    $rows = @(
        @{ Native = 'Verse.GizmoGridDrawer'; Method = 'DrawGizmoGrid'; Patch = 'ArchitectGridPatch'; Flag = 'GridReady'; Expected = 4 },
        @{ Native = 'Verse.Command'; Method = 'GizmoOnGUIInt'; Patch = 'ArchitectCommandFacePatch'; Flag = 'FaceReady'; Expected = 4 },
        @{ Native = 'Verse.FloatMenuOption'; Method = 'DoGUI'; Patch = 'ArchitectPopupOptionPatch'; Flag = 'Ready'; State = 'ArchitectPopup'; Helper = 'ArchitectPopup'; Expected = 4 },
        @{ Native = 'Verse.FloatMenu'; Method = 'DoWindowContents'; Patch = 'ArchitectPopupContentsPatch'; Helper = 'ArchitectPopup'; Expected = 2 }
    )
    foreach ($row in $rows) {
        $target = [HarmonyLib.AccessTools]::Method($game.GetType($row.Native, $true), $row.Method)
        $original = $readInstructions.Invoke($null, @($target, $null))
        $transpiler = $mod.GetType(('ReframedUI.' + $row.Patch), $true).GetMethod('Transpiler', $flags)
        $patched = @($transpiler.Invoke($null, @(,$original)))
        $stateType = if ($row.State) { $mod.GetType(('ReframedUI.' + $row.State), $true) } else { $seams }
        $helperType = if ($row.Helper) { 'ReframedUI.' + $row.Helper } else { 'ReframedUI.ArchitectPanel' }
        if ($row.Flag -and -not $stateType.GetField($row.Flag, $flags).GetValue($null)) { throw ('Native IL pattern rejected: ' + $row.Method) }
        $calls = @($patched | Where-Object { $_.operand -is [Reflection.MethodInfo] -and $_.operand.DeclaringType.FullName -eq $helperType })
        if ($calls.Count -ne $row.Expected) { throw ('Unexpected native IL substitution count: ' + $row.Method) }
        if ($row.Patch -eq 'ArchitectPopupContentsPatch') {
            $before = @($original)
            if ($before.Count -ne $patched.Count) { throw 'Dropdown scroll adaptation changed instruction count.' }
            $changed = 0
            for ($i = 0; $i -lt $before.Count; $i++) {
                if ($before[$i].opcode -ne $patched[$i].opcode -or
                    $before[$i].labels.Count -ne $patched[$i].labels.Count -or
                    $before[$i].blocks.Count -ne $patched[$i].blocks.Count) { throw 'Dropdown scroll adaptation changed control flow.' }
                if ($before[$i].operand -eq $patched[$i].operand) { continue }
                $changed++
                $a = $before[$i].operand; $b = $patched[$i].operand
                if (-not $a.IsStatic -or -not $b.IsStatic -or $a.ReturnType -ne $b.ReturnType -or
                    (($a.GetParameters().ParameterType.FullName -join '|') -ne ($b.GetParameters().ParameterType.FullName -join '|'))) {
                    throw 'Dropdown scroll adapter changed the evaluation stack signature.'
                }
            }
            if ($changed -ne 2) { throw 'Dropdown scroll adaptation changed unexpected instructions.' }
        }
        Write-Output ('PASS exact native IL seam ' + $row.Method + ': ' + $calls.Count + ' substitutions')
    }
    Write-Output 'PASS installed native seam audit. No patches installed, game launched or Unity rendering executed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
