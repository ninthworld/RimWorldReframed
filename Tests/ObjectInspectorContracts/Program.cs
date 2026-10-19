using System;
using ReframedUI;

int checks = 0;
void Check(bool value, string message) { if (!value) throw new Exception(message); checks++; }
Check(!ObjectInspectorDescriptionLayout.Overflow(28, 99), "Two compact inspect lines fit beneath the native health row without a rail");
Check(!ObjectInspectorDescriptionLayout.Overflow(99, 99), "Exact-fit text does not create a phantom rail");
Check(ObjectInspectorDescriptionLayout.Overflow(100, 99), "Real wrapped text overflow enables scrolling");
float x = 80, y = 999;
ObjectInspectorDescriptionLayout.Clamp(ref x, ref y, 300, 99);
Check(x == 0 && y == 201, "Long text clamps to its exact measured overflow with no horizontal drift");
ObjectInspectorDescriptionLayout.Clamp(ref x, ref y, 28, 99);
Check(x == 0 && y == 0, "Content shrink/switch clears an old scroll offset");
y = -50;
ObjectInspectorDescriptionLayout.Clamp(ref x, ref y, 300, 99);
Check(y == 0, "Negative wheel positions stay at the first line");
Check(ObjectInspectorDescriptionLayout.Ratio(1) == 1 && ObjectInspectorDescriptionLayout.Ratio(.5f) == .5f, "Real health fractions retain their values");
Check(ObjectInspectorDescriptionLayout.Ratio(-1) == 0 && ObjectInspectorDescriptionLayout.Ratio(2) == 1, "Malformed fractions cannot paint beyond the bar");
Check(ObjectInspectorDescriptionLayout.Ratio(float.NaN) == 0 && ObjectInspectorDescriptionLayout.Ratio(float.PositiveInfinity) == 0, "Invalid health data has a drawable zero fallback");
for (int content = 0; content <= 1000; content += 5)
for (int viewport = 20; viewport <= 200; viewport += 10)
{
    float sx = 21, sy = 1200;
    ObjectInspectorDescriptionLayout.Clamp(ref sx, ref sy, content, viewport);
    Check(sx == 0 && sy >= 0 && sy <= Math.Max(0, content - viewport), "Measured scroll stays within current text bounds");
}
Console.WriteLine($"PASS {checks} object inspect overflow, shrink and health contracts.");
