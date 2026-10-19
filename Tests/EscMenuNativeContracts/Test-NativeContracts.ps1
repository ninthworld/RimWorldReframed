param(
    [string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld',
    [string]$ModAssembly = (Join-Path $PSScriptRoot '../../work/esc28-build/ReframedUI.dll'),
    [string]$HarmonyAssembly = (Join-Path $env:USERPROFILE '.nuget/packages/lib.harmony/2.3.6/lib/net472/0Harmony.dll')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Use Windows PowerShell5.1 for the game CLR.' }
$managed = Join-Path $RimWorldDir 'RimWorldWin64_Data/Managed'
$resolver = [ResolveEventHandler] {
    param($sender,$args)
    $path = Join-Path $managed (([Reflection.AssemblyName]$args.Name).Name + '.dll')
    if (Test-Path -LiteralPath $path) { return [Reflection.Assembly]::LoadFrom($path) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $harmony = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $HarmonyAssembly).Path)
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $managed 'Assembly-CSharp.dll'))
    $mod = [Reflection.Assembly]::LoadFrom((Resolve-Path -LiteralPath $ModAssembly).Path)
    $native = $mod.GetType('ReframedUI.EscMenuNative',$true)
    $static = [Reflection.BindingFlags]'NonPublic,Static'
    $all = [Reflection.BindingFlags]'NonPublic,Public,Instance,Static,DeclaredOnly'
    $script:checks = 0
    function Check($value,[string]$message) { if (-not $value) { throw $message }; $script:checks++ }
    function NativeMethod($type,[string]$name,[string]$parameters,[string]$returns) {
        $matches = @($type.GetMethods($all) | Where-Object { $_.Name -eq $name -and (($_.GetParameters() | ForEach-Object { $_.ParameterType.FullName }) -join '|') -eq $parameters })
        Check ($matches.Count -eq 1 -and $matches[0].ReturnType.FullName -eq $returns) ($type.FullName+'.'+$name)
        return $matches[0]
    }
    function Instructions([Reflection.MethodBase]$method) {
        $reader = [HarmonyLib.PatchProcessor].GetMethods() | Where-Object { $_.Name -eq 'GetOriginalInstructions' -and $_.GetParameters().Count -eq 2 -and -not $_.GetParameters()[1].ParameterType.IsByRef } | Select-Object -First 1
        return @($reader.Invoke($null,@($method,$null)))
    }
    $menu = $game.GetType('RimWorld.MainTabWindow_Menu',$true)
    $drawer = $game.GetType('RimWorld.MainMenuDrawer',$true)
    $listing = $game.GetType('Verse.OptionListingUtility',$true)
    $option = $game.GetType('Verse.ListableOption',$true)
    $web = $game.GetType('Verse.ListableOption_WebLink',$true)
    # Build the closed native List type; do not hard-code the installed assembly version.
    $list = [Collections.Generic.List``1].MakeGenericType($option)
    $drawListing = NativeMethod $listing 'DrawOptionListing' ('UnityEngine.Rect|'+$list.FullName) 'System.Single'
    $controller = NativeMethod $drawer 'DoMainMenuControls' 'UnityEngine.Rect|System.Boolean' 'System.Void'
    $body = Instructions $controller
    $calls = @($body | Where-Object { $_.operand -is [Reflection.MethodInfo] -and $_.operand -eq $drawListing })
    Check ($calls.Count -eq 2) 'Native controller must have exactly2 listing calls'
    # Icon roles follow native construction keys, not English display-label heuristics.
    # Confirm the installed playing-branch key sequence before relying on that policy.
    $literals = @($body | Where-Object { $_.opcode -eq [Reflection.Emit.OpCodes]::Ldstr } | ForEach-Object operand)
    $keyIndex = -1
    foreach ($key in @('Save','LoadGame','ReviewScenario','Options','SaveAndQuitToMainMenu','SaveAndQuitToOS','QuitToMainMenu','QuitToOS')) {
        $keyIndex = [Array]::IndexOf($literals, $key, $keyIndex + 1)
        Check ($keyIndex -ge 0) ('Native action construction order: '+$key)
    }
    $nativeCalls = @($body | Where-Object { $_.operand -is [Reflection.MethodInfo] } | ForEach-Object operand)
    Check (-not ($nativeCalls | Where-Object { $_.DeclaringType -eq $option -and $_.Name -eq 'DrawOption' })) 'Controller must not bypass listing interception'
    $null = NativeMethod $menu 'DoWindowContents' 'UnityEngine.Rect' 'System.Void'
    $null = NativeMethod $menu 'PreOpen' '' 'System.Void'
    foreach ($row in @(@($menu,'anyGameFiles',[bool]),@($drawer,'webRect',$game.GetType('UnityEngine.Rect')),@($drawer,'webBackgroundYMax',[single]),@($option,'uiHighlightTag',[string]))) {
        $field=$row[0].GetField($row[1],$all)
        if ($row[1] -eq 'webRect') { Check ($field.FieldType.FullName -eq 'UnityEngine.Rect') 'native web rect' }
        else { Check ($field -and $field.FieldType -eq $row[2]) ('native field '+$row[1]) }
    }
    $baseDraw=NativeMethod $option 'DrawOption' 'UnityEngine.Vector2|System.Single' 'System.Single'
    $webDraw=NativeMethod $web 'DrawOption' 'UnityEngine.Vector2|System.Single' 'System.Single'
    Check ($baseDraw.IsVirtual -and $webDraw.IsVirtual) 'Both native draw methods virtual'
    $baseCalls=@(Instructions $baseDraw | Where-Object { $_.operand -is [Reflection.MethodInfo] } | ForEach-Object operand)
    $webCalls=@(Instructions $webDraw | Where-Object { $_.operand -is [Reflection.MethodInfo] } | ForEach-Object operand)
    Check (@($baseCalls | Where-Object { $_.DeclaringType -eq [Action] -and $_.Name -eq 'Invoke' }).Count -eq 1) 'Original plain option invokes its action once'
    Check (@($webCalls | Where-Object { $_.DeclaringType -eq [Action] -and $_.Name -eq 'Invoke' }).Count -eq 1) 'Original web option invokes its action once'
    Check (@($webCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Application' -and $_.Name -eq 'OpenURL' }).Count -eq 1) 'Original web URL branch present'

    # Only test-owned callbacks are invoked. No game objects, GUI or actual URLs.
    Add-Type -ReferencedAssemblies @($game.Location,(Join-Path $managed 'UnityEngine.CoreModule.dll'),(Join-Path $managed 'netstandard.dll')) -TypeDefinition @'
using System;
using UnityEngine;
using Verse;
public static class EscOptionFixture {
    public static int Calls;
    public static string Url;
    public static ListableOption Plain() { return new ListableOption("Test action", () => Calls++, "TestTag"); }
    public static ListableOption LinkAction() { return new ListableOption_WebLink("Test link action", () => Calls += 10, null); }
    public static ListableOption LinkUrl() { return new ListableOption_WebLink("Test URL", "fixture://not-opened", null); }
    public static Action<string> OpenUrl { get { return s => Url = s; } }
}
public class EscUnknownOption : ListableOption {
    public EscUnknownOption() : base("Custom controls", () => EscOptionFixture.Calls += 100) {}
    public override float DrawOption(Vector2 pos, float width) { throw new Exception("Must not draw custom option while collecting"); }
}
'@
    $dispatch=$native.GetMethod('DispatchOption',$static)
    $supported=$native.GetMethod('Supported',$static)
    $plain=[EscOptionFixture]::Plain(); $link=[EscOptionFixture]::LinkAction(); $url=[EscOptionFixture]::LinkUrl()
    Check ($dispatch.Invoke($null,@($plain,[EscOptionFixture]::OpenUrl)) -and [EscOptionFixture]::Calls -eq 1) 'Plain original action invoked once'
    Check ($dispatch.Invoke($null,@($link,[EscOptionFixture]::OpenUrl)) -and [EscOptionFixture]::Calls -eq 11 -and $null -eq [EscOptionFixture]::Url) 'Web action takes precedence over URL'
    Check ($dispatch.Invoke($null,@($url,[EscOptionFixture]::OpenUrl)) -and [EscOptionFixture]::Calls -eq 11 -and [EscOptionFixture]::Url -eq 'fixture://not-opened') 'Original URL passed to test-owned receiver unchanged'
    $unknown=(New-Object EscUnknownOption).PSObject.BaseObject
    Check (-not $supported.Invoke($null,@($unknown))) 'Custom drawing rejected atomically'
    Check (-not $dispatch.Invoke($null,@($unknown,[EscOptionFixture]::OpenUrl)) -and [EscOptionFixture]::Calls -eq 11) 'Rejected custom option cannot dispatch'
    Check (-not $supported.Invoke($null,@($null))) 'Null option rejected'
    $foreign=$native.GetMethod('ForeignPatches',$static)
    $own=$mod.GetType('ReframedUI.EscMenuOptionListingPatch',$true).GetMethod('Prefix',$static)
    $foreignMethod=[EscOptionFixture].GetMethod('Plain')
    $ownPatch=[HarmonyLib.Patch]::new($own,0,'test-only',400,[string[]]@(),[string[]]@(),$false)
    $otherPatch=[HarmonyLib.Patch]::new($foreignMethod,1,'foreign-test-only',400,[string[]]@(),[string[]]@(),$false)
    $empty=[HarmonyLib.Patch[]]@()
    Check (-not $foreign.Invoke($null,@($null))) 'Unpatched native surface is eligible'
    $info=[HarmonyLib.Patches]::new([HarmonyLib.Patch[]]@($ownPatch),$empty,$empty,$empty)
    Check (-not $foreign.Invoke($null,@($info))) 'Only own listing prefix remains eligible'
    foreach ($kind in 0..3) {
        $groups=@($empty,$empty,$empty,$empty); $groups[$kind]=[HarmonyLib.Patch[]]@($otherPatch)
        $info=[HarmonyLib.Patches]::new($groups[0],$groups[1],$groups[2],$groups[3])
        Check ($foreign.Invoke($null,@($info))) ('Foreign patch category '+$kind+' triggers atomic native fallback')
    }
    $scopeType=$native.GetNestedType('Capture',[Reflection.BindingFlags]'NonPublic')
    $scope=[Activator]::CreateInstance($scopeType,$true)
    $slot=$native.GetField('capture',$static)
    $capture=$native.GetMethod('CaptureListing',$static)
    $batch=[Activator]::CreateInstance($list); $batch.Add($plain); $batch.Add($link)
    $args=@($batch,[single]99)
    Check ($capture.Invoke($null,$args)) 'Listing prefix is transparent outside capture'
    try {
        $slot.SetValue($null,$scope)
        $args=@($batch,[single]99)
        Check (-not $capture.Invoke($null,$args) -and $args[1] -eq 0 -and [EscOptionFixture]::Calls -eq 11) 'Capture suppresses option drawing and activation'
        $batches=$scopeType.GetField('Batches',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($scope)
        Check ([object]::ReferenceEquals($batches[0][0],$plain) -and [object]::ReferenceEquals($batches[0][1],$link)) 'Original option identities retained'
        $bad=[Activator]::CreateInstance($list); $bad.Add($unknown)
        $args=@($bad,[single]99); $null=$capture.Invoke($null,$args)
        Check ($scopeType.GetField('Invalid',[Reflection.BindingFlags]'Instance,NonPublic').GetValue($scope)) 'Unknown custom draw marks complete collection invalid without invoking it'
    }
    finally { $slot.SetValue($null,$null) }
    $collect=$native.GetMethod('TryCollect',$static)
    Check (@($collect.GetMethodBody().ExceptionHandlingClauses | Where-Object Flags -eq Finally).Count -gt 0) 'Capture has finally cleanup'
    $collectCalls=@(Instructions $collect | Where-Object { $_.operand -is [Reflection.MethodInfo] } | ForEach-Object operand)
    Check (-not ($collectCalls | Where-Object { $_.DeclaringType.FullName -eq 'UnityEngine.Event' -and $_.Name -in @('Use','set_current','set_type','set_mousePosition') })) 'Capture never mutates current Event or synthesizes input'
    Check (-not ($collectCalls | Where-Object { $_.DeclaringType -eq [Action] -and $_.Name -eq 'Invoke' })) 'Collection contains no action dispatch'
    Write-Output "PASS $checks native metadata/IL/capture/dispatch contracts; no game actions, GUI, actual URL or patches invoked."
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
