param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '..\..\work\selection-actions-build\ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget\packages\lib.harmony\2.3.6\lib\net472\0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run in Windows PowerShell 5.1.' }
$runtimeManaged = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $eventArgs)
    $dependency = Join-Path $runtimeManaged (([Reflection.AssemblyName]$eventArgs.Name).Name + '.dll')
    if (Test-Path -LiteralPath $dependency) { return [Reflection.Assembly]::LoadFrom($dependency) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path) | Out-Null
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $runtimeManaged 'Assembly-CSharp.dll'))
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $flags = [Reflection.BindingFlags]'Static,NonPublic'
    $map = [HarmonyLib.AccessTools]::Method($game.GetType('RimWorld.MapGizmoUtility', $true), 'MapUIOnGUI')
    if (-not $map.IsStatic -or $map.ReturnType -ne [void] -or $map.GetParameters().Length -ne 0) { throw 'Map scope signature changed.' }
    $command = [HarmonyLib.AccessTools]::Method($game.GetType('Verse.Command', $true), 'GizmoOnGUIInt')
    if ($command.IsStatic -or $command.ReturnType.FullName -ne 'Verse.GizmoResult' -or
        (($command.GetParameters().ParameterType.FullName -join ',') -ne 'UnityEngine.Rect,Verse.GizmoRenderParms')) { throw 'Command scope signature changed.' }
    $readInstructions = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object {
        $_.Name -eq 'GetOriginalInstructions' -and -not $_.GetParameters()[1].ParameterType.IsByRef
    } | Select-Object -First 1
    $original = $readInstructions.Invoke($null, @($map, $null))
    $gridCalls = @($original | Where-Object {
        $_.operand -is [Reflection.MethodInfo] -and $_.operand.DeclaringType.FullName -eq 'Verse.GizmoGridDrawer' -and $_.operand.Name -eq 'DrawGizmoGridFor'
    })
    if ($gridCalls.Count -ne 1) { throw 'Map selection native grid call changed.' }
    foreach ($name in @('SelectionMapCommandsPatch', 'SelectionCommandPaintPatch')) {
        $patch = $mod.GetType(('ReframedUI.' + $name), $true)
        if ($patch.GetMethod('Transpiler', $flags)) { throw ('Duplicate transpiler added: ' + $name) }
        foreach ($methodName in @('Prefix', 'Finalizer')) {
            $method = $patch.GetMethod($methodName, $flags)
            if (-not $method -or $method.ReturnType -ne [void] -or @($method.GetParameters() | Where-Object Name -eq '__result').Count) {
                throw ('Scope must not replace a result or skip/suppress native behavior: ' + $name + '.' + $methodName)
            }
        }
    }
    $supports = $mod.GetType('ReframedUI.SelectionCommands', $true).GetMethod('SupportsType', $flags)
    foreach ($name in @('Verse.Command_Action', 'Verse.Command_Toggle')) {
        if (-not $supports.Invoke($null, @($game.GetType($name, $true)))) { throw ('Native command refused: ' + $name) }
    }
    if ($supports.Invoke($null, @($game.GetType('Verse.Gizmo', $true))) -or $supports.Invoke($null, @([string])) -or $supports.Invoke($null, @($null))) {
        throw 'Non-command/foreign/null type accepted.'
    }
    $assemblyName = New-Object Reflection.AssemblyName('SelectionScopeFixture')
    $fixture = [AppDomain]::CurrentDomain.DefineDynamicAssembly($assemblyName, [Reflection.Emit.AssemblyBuilderAccess]::Run)
    $module = $fixture.DefineDynamicModule('SelectionScopeFixture')
    $builder = $module.DefineType('FixtureModCommand', [Reflection.TypeAttributes]::Public, $game.GetType('Verse.Command_Action', $true))
    $customType = $builder.CreateType()
    if ($supports.Invoke($null, @($customType))) { throw 'Mod-provided command subclass accepted.' }
    Write-Output 'PASS installed map/command signatures, sole map-grid call, non-replacing prefix/finalizer ownership, native Action/Toggle eligibility and custom/non-command fallback.'
    Write-Output 'No patches installed, game launched, native callbacks or Unity rendering executed.'
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
