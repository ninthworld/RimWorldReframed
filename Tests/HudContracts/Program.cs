using System;
using ReframedUI;

static void Require(bool value, string message) { if (!value) throw new Exception(message); }
var none = Array.Empty<HudArea>();
Require(HudGeometry.TryPlace(100, 12, 176, 88, 500, none, out float y) && y == 12, "Clear anchor moved");
var unsorted = new[] { new HudArea(90, 100, 200, 70), new HudArea(90, 0, 200, 90) };
Require(HudGeometry.TryPlace(100, 12, 176, 88, 500, unsorted, out y) && y == 178, "Chained learner/colonist collision");
Require(!HudGeometry.TryPlace(100, 12, 176, 88, 250, unsorted, out y), "Overlaps lower reserved stack");
Require(HudGeometry.TryPlace(100, 12, 176, 88, 500, new[] {new HudArea(0,0,99,200)}, out y) && y == 12, "Unrelated resource column moved clock");
Require(!HudGeometry.TryPlace(100, 12, float.NaN, 88, 500, none, out y), "Invalid geometry accepted");
Require(HudGeometry.TryPlace(100, 12, 176, 88, 100, none, out y), "Exact bottom boundary rejected");
Console.WriteLine("Passed 6 HUD placement cases: clear, chained collisions, lower-stack fallback, separate column, invalid geometry, exact boundary.");
