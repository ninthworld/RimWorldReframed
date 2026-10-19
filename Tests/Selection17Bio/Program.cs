using System;
using ReframedUI;
int checks = 0;
void Check(bool condition, string name) { checks++; if (!condition) throw new Exception(name); }
for (int count = 1; count <= 4; count++)
{
    float bottom = PawnBioActionsLayout.FieldStart + (count - 1) * PawnBioActionsLayout.FieldStep + PawnBioActionsLayout.FieldHeight;
    Check(bottom + 15 == PawnBioActionsLayout.NamingHeight(count) - 39, "All native name/title field variants leave15px before accept/cancel");
    Check(PawnBioActionsLayout.NamingHeight(count) < 300, "Up to four native fields remain in compact naming dialog");
    float lastAction = 69 + (count - 1) * 31 + 26;
    Check(lastAction < PawnBioActionsLayout.ActionsHeight(count, true) - 37, "All native actions stay above information button");
    Check(lastAction < PawnBioActionsLayout.ActionsHeight(count, false), "Information-owned action window contains all native actions");
}
Check(PawnBioActionsLayout.NamingHeight(0) == PawnBioActionsLayout.NamingHeight(1), "Invalid zero count cannot collapse window below one row");
Check(PawnBioActionsLayout.NamingHeight(100) == PawnBioActionsLayout.NamingHeight(4), "Unknown oversized field schema does not create an unbounded window");
Check(PawnBioActionsLayout.NamingWidth - 90 - 88 - 46 > 240, "Actual input field keeps space for native max16-character names and caret");
Check(PawnBioActionsLayout.NamingAuthorized(true, true, true, true, false, false), "Information-origin naming works in entry/world contexts without Active map inspector");
Check(!PawnBioActionsLayout.NamingAuthorized(true, true, true, false, true, true), "InformationCards setting independently restores native naming");
Check(!PawnBioActionsLayout.NamingAuthorized(true, true, false, true, false, true), "Inspector-origin naming does not extend beyond actual active map context");
Check(!PawnBioActionsLayout.NamingAuthorized(true, false, true, true, true, true), "Global disable restores original naming controls");
Check(!PawnBioActionsLayout.NamingAuthorized(false, true, true, true, true, true), "Failed runtime patches do not authorize naming interception");
Console.WriteLine("PASS " + checks + " compact Bio actions/name field layout contracts.");
