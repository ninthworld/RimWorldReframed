using System;
using ReframedUI;

int cases = 0;
foreach (float w in new[] { -10f, 0, .1f, 1, 3, 100, float.NaN, float.PositiveInfinity })
foreach (float h in new[] { -2f, 0, .1f, 2, 80, float.NaN, float.NegativeInfinity })
foreach (float r in new[] { -1f, 0, .1f, 4, 10000, float.NaN, float.PositiveInfinity })
{
    float radius = PrimitiveGeometry.Radius(r,w,h);
    if (!PrimitiveGeometry.Finite(radius) || radius < 0) throw new Exception("Invalid radius escaped the boundary");
    if (w > 0 && h > 0 && float.IsFinite(w) && float.IsFinite(h) && (2*radius > w || 2*radius > h)) throw new Exception("Corners cross opposing edges");
    if ((!float.IsFinite(w) || !float.IsFinite(h) || w<=0 || h<=0) && PrimitiveGeometry.Drawable(0,0,w,h)) throw new Exception("Invalid surface accepted");
    cases++;
}
foreach (float fraction in new[] {-1f,0,.5f,1,2,float.NaN,float.PositiveInfinity})
{
    float fill=PrimitiveGeometry.Fraction(fraction);
    if (!float.IsFinite(fill) || fill<0 || fill>1) throw new Exception("Fill can escape track");
    cases++;
}
if (PrimitiveGeometry.Fraction(.5f)!=.5f || PrimitiveGeometry.Radius(4,100,80)!=4) throw new Exception("Valid input was changed");
Console.WriteLine($"PASS: {cases} invalid-input, bounded-corner and fill containment cases. Rendering/GUI-state requires in-game checks.");
