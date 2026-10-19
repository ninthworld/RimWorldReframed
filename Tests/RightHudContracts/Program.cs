using System;
using ReframedUI;
int checks=0;
void Check(bool value,string what){checks++;if(!value)throw new Exception(what);}
Check(!RightHudGeometry.Supported(127),"short stack falls back before hiding native contents");
Check(RightHudGeometry.Supported(128),"minimum leaves reachable headers and notification bundle");
foreach(float available in new[]{128f,180f,320f,600f,900f})
{
    float issue=RightHudGeometry.IssueLimit(available,true);
    Check(issue>=0 && issue+74<=available,"issues leave space for native letter bundle and gap");
    float empty=RightHudGeometry.IssueLimit(available,false);
    Check(empty+38<=available,"outgoing letter ghost keeps one row reserved");
    Check(empty>=issue,"issue area can use empty letter space");
}
Check(!RightHudGeometry.Supported(179,true,true),"short three-part stack keeps whole native presentation");
Check(RightHudGeometry.Supported(180,true,true),"minimum independent alerts and issue groups");
Check(RightHudGeometry.Supported(128,true,false),"critical-only stack remains reachable at original minimum");
foreach(float available in new[]{180f,240f,320f,600f,900f})
{
    float critical = RightHudGeometry.CriticalLimit(available,true);
    Check(critical >= 30,"critical viewport always exposes a full card");
    float remaining = available-critical-8;
    Check(remaining>=128,"separate critical cards preserve ordinary stack minimum");
    float issues = RightHudGeometry.IssueLimit(remaining,true);
    float letters = remaining-issues-8;
    Check(letters>=30,"three-part stack preserves a native letter or bundle");
    Check(critical+issues+letters+16<=available+.001f,"all three regions and gaps fit");
    Check(RightHudGeometry.CriticalLimit(available,false)+38<=available,"critical-only overflow keeps bounded outgoing letter reserve");
}
Console.WriteLine($"PASS {checks} right-HUD allocation and minimum-reachability contracts.");
