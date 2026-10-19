param([string]$RimWorldDir = 'C:\Program Files (x86)\Steam\steamapps\common\RimWorld')
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -eq 'Core') { throw 'Run with Windows PowerShell 5.1.' }
$managedPath = Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed'
$resolver = [ResolveEventHandler] {
    param($sender, $args)
    $dependency = Join-Path $managedPath (([Reflection.AssemblyName]$args.Name).Name + '.dll')
    if (Test-Path -LiteralPath $dependency) { return [Reflection.Assembly]::LoadFrom($dependency) }
    return $null
}
[AppDomain]::CurrentDomain.add_AssemblyResolve($resolver)
try {
    $game = [Reflection.Assembly]::LoadFrom((Join-Path $managedPath 'Assembly-CSharp.dll'))
    $alert = $game.GetType('RimWorld.Alert', $true)
    $readout = $game.GetType('RimWorld.AlertsReadout', $true)
    $priority = $game.GetType('RimWorld.AlertPriority', $true)
    $flags = [Reflection.BindingFlags]'Instance,Public,NonPublic,DeclaredOnly'
    $count = 0
    function Check($value, [string]$label) {
        if (-not $value) { throw $label }
        $script:count++
    }
    function Method($type, [string]$name, [string]$parameters, [string]$returns) {
        $method = @($type.GetMethods($flags) | Where-Object {
            $_.Name -eq $name -and ($_.GetParameters().ParameterType.FullName -join ',') -eq $parameters
        })
        Check ($method.Count -eq 1 -and $method[0].ReturnType.FullName -eq $returns) ($type.FullName + '.' + $name)
        return $method[0]
    }
    Check (([Enum]::GetNames($priority) -join ',') -eq 'Medium,High,Critical') 'AlertPriority names'
    foreach ($pair in @(@('Medium',0), @('High',1), @('Critical',2))) {
        Check ([Convert]::ToInt32([Enum]::Parse($priority,$pair[0])) -eq $pair[1]) ('priority ' + $pair[0])
    }
    $draw = Method $alert 'DrawAt' 'System.Single,System.Boolean' 'UnityEngine.Rect'
    Check ($draw.IsPublic -and $draw.IsVirtual -and -not $draw.IsStatic) 'base native DrawAt virtual seam'
    $click = Method $alert 'OnClick' '' 'System.Void'
    Check ($click.IsFamily -and $click.IsVirtual -and -not $click.IsStatic) 'protected native virtual click seam'
    foreach ($property in @(@('Active','System.Boolean'), @('Label','System.String'), @('Priority','RimWorld.AlertPriority'))) {
        $getter = $alert.GetProperty($property[0],$flags).GetGetMethod($true)
        Check ($getter -and $getter.IsPublic -and $getter.ReturnType.FullName -eq $property[1]) $property[0]
    }
    $null = Method $alert 'DrawInfoPane' '' 'System.Void'
    $null = Method $alert 'GetReport' '' 'RimWorld.AlertReport'
    $null = Method $readout 'CheckAddOrRemoveAlert' 'RimWorld.Alert,System.Boolean' 'System.Void'
    $field = $readout.GetField('mouseoverAlertIndex',$flags)
    Check ($field -and $field.FieldType -eq [int] -and $field.IsPrivate) 'native hover index'
    $field = $readout.GetField('activeAlerts',$flags)
    Check ($field -and $field.FieldType.GetGenericArguments()[0] -eq $alert -and $field.IsPrivate) 'native active list'

    # The same protected open virtual delegate shape used by ColonyIssues, tested under the game CLR family.
    Add-Type -TypeDefinition @'
using System;
using System.Reflection;
public class IssueNativeLike { public int Count; protected virtual void OnClick() { Count++; } }
public class IssueOverrideLike : IssueNativeLike { protected override void OnClick() { Count += 10; } }
public static class IssueVirtualProof {
    public static bool Verify() {
        MethodInfo method = typeof(IssueNativeLike).GetMethod("OnClick", BindingFlags.NonPublic | BindingFlags.Instance);
        Action<IssueNativeLike> click = (Action<IssueNativeLike>)Delegate.CreateDelegate(typeof(Action<IssueNativeLike>), method);
        IssueNativeLike a = new IssueNativeLike(); IssueOverrideLike b = new IssueOverrideLike();
        click(a); click(b); return a.Count == 1 && b.Count == 10;
    }
}
'@
    Check ([IssueVirtualProof]::Verify()) 'protected virtual delegate dispatch on .NET Framework'
    Write-Output "PASS $count installed metadata/.NET Framework dispatch checks. No game objects, patch application or game actions."
}
finally { [AppDomain]::CurrentDomain.remove_AssemblyResolve($resolver) }
