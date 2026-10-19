using System;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    private static bool Near(float a, float b) => Math.Abs(a-b)<.001f;
    private static void Main()
    {
        Check(RosterMenusLayout.Extent(0,32)==0 && RosterMenusLayout.Extent(1,32)==32, "short roster has no trailing false overflow");
        Check(RosterMenusLayout.Extent(3,32)==100, "three compact rows occupy100px");
        foreach (float top in new[] {-33f,-1,0,1,64,65,68,99,100,101})
        {
            bool full=RosterMenusLayout.FullRow(top,32,0,100);
            Check(full==(top>=-.01f && top+32<=100.01f), "clipped held-input timeline never owns pointer");
            if(full) Check(RosterMenusLayout.Visible(top,32,0,100), "input region is painted");
        }
        Check(!RosterMenusLayout.FullRow(0,83,0,82), "clipped management controls remain inert");
        Check(RosterMenusLayout.FullRow(0,83,0,83), "complete management controls accept input");
        Check(RosterMenusLayout.FullRow(0,16,0,81) && RosterMenusLayout.FullRow(17,30,0,81), "small-screen detail heading and cell remain usable");
        Check(RosterMenusLayout.FullRow(51,32,2,81), "small-screen management button becomes fully reachable at scroll end");
        foreach (float width in new[] {1f,340,560,744})
        foreach (float height in new[] {32f,100,270})
        foreach (float delta in new[] {-100f,-.02f,0,.02f,1000})
        {
            float total=Math.Max(0,height+delta);
            float usable=RosterMenusLayout.ContentWidth(width,height,total);
            Check(usable>=1 && usable<=width, "vertical-only content remains within viewport");
            Check(usable==Math.Max(1,width-(total>height+.01f?10:0)), "fixed hour header matches actual scroll width");
            float end=RosterMenusLayout.ClampScroll(float.MaxValue,total,height);
            Check(total<=height || Near(total-end,height), "last roster or detail pixel is reachable");
            Check(RosterMenusLayout.ClampScroll(end,32,100)==0, "shorter selection releases prior scroll");
        }
        Check(RosterMenusLayout.ClampScroll(float.NaN,100,32)==0, "invalid scroll is bounded");
        foreach (int rate in new[] {15,30,60,144})
        {
            var motion=new RosterMenusMotion(); motion.Set(200,0);
            Check(motion.Settled && motion.Value==200, "initial panel lets common shell own opening");
            motion.Set(400,0); Check(!motion.Settled && motion.Value==200, "opening details preserves current height");
            float prior=200;
            for(int frame=1; frame<=rate; frame++)
            {
                motion.Set(400,(double)frame/rate);
                Check(motion.Value>=prior && motion.Value<=400, "opening is bounded and monotonic"); prior=motion.Value;
            }
            Check(motion.Settled && Near(motion.Value,400), "opening converges independent of repaint rate");
            motion.Set(200,1); Check(!motion.Settled && Near(motion.Value,400), "closing starts without snap");
            motion.Set(200,1.08); float partial=motion.Value;
            Check(partial>200 && partial<400, "closing is a real tween");
            motion.Set(400,1.08); Check(Near(motion.Value,partial), "reversal preserves currently painted height");
            motion.Set(400,2); Check(motion.Settled && Near(motion.Value,400), "reversal settles at new target");
            motion.Set(200,2); motion.Set(200,3);
            Check(motion.Settled && Near(motion.Value,200), "closed details release all occupied space");
            motion.Set(float.NaN,4); motion.Set(500,double.NaN);
            Check(motion.Settled && Near(motion.Value,200), "invalid frame cannot corrupt bounds");
            motion.Set(300,4); motion.Set(300,3);
            Check(Near(motion.Value,200), "clock regression cannot advance or reverse progress");
            motion.Set(300,5); Check(motion.Settled && Near(motion.Value,300), "clock recovers safely");
        }
        Console.WriteLine($"PASS {checks} compact roster sizing, input clipping, vertical overflow and reversible motion contracts.");
    }
}
