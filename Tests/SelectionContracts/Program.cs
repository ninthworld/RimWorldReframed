using System;
using ReframedUI;

int checks = 0;
void Check(bool condition, string description) { if (!condition) throw new Exception(description); checks++; }
var scope = new SelectionPaintContext();
Check(!scope.Drawing, "No paint ownership outside map selection");
var unrelated = scope.EnterCommand(true);
Check(!scope.Drawing, "Native command outside selection scope remains native");
scope.Restore(unrelated);
var world = scope.EnterMap(false);
var worldCommand = scope.EnterCommand(true);
Check(!scope.Drawing, "Unsupported map/world context remains native");
scope.Restore(worldCommand); scope.Restore(world);
var map = scope.EnterMap(true);
Check(!scope.Drawing, "Map ownership alone cannot restyle arbitrary gizmos");
var command = scope.EnterCommand(true);
Check(scope.Drawing, "Native command inside map scope shares the tile painter");
var custom = scope.EnterCommand(false);
Check(!scope.Drawing, "Nested custom subclass restores its full native drawing");
scope.Restore(custom);
Check(scope.Drawing, "Custom nested return restores previous native-command ownership");
var nestedMap = scope.EnterMap(false);
Check(!scope.Drawing, "Nested ineligible dispatcher suspends inherited ownership");
scope.Restore(nestedMap);
Check(scope.Drawing, "Nested dispatcher restores command ownership");
scope.Restore(command);
Check(!scope.Drawing, "Command finalizer releases paint ownership before group processing");
try
{
    var throwing = scope.EnterCommand(true);
    try { throw new InvalidOperationException("fixture"); }
    finally { scope.Restore(throwing); }
}
catch (InvalidOperationException) { }
Check(!scope.Drawing, "Exceptional command exit cannot leak paint ownership");
scope.Restore(map);
Check(!scope.Drawing, "Map finalizer releases ownership");
for (int i = 0; i < 200; i++)
{
    var eventScope = scope.EnterMap(i % 2 == 0);
    var eventCommand = scope.EnterCommand(true);
    Check(scope.Drawing == (i % 2 == 0), "Alternating contexts retain exact ownership");
    scope.Restore(eventCommand); scope.Restore(eventScope);
}
Check(!scope.Drawing, "No accumulated ownership after repeated GUI events");
Console.WriteLine($"PASS {checks} selection paint ownership contracts; no game/UI acceptance claimed.");
