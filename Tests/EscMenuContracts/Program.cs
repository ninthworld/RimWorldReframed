using System;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string detail) { checks++; if (!value) throw new Exception(detail); }
    private static void Main()
    {
        foreach (int rate in new[] { 18, 30, 60, 144 })
        {
            var motion = new ToolsPanelMotion();
            motion.SetOpen(true, 0);
            float previous = 31;
            for (int frame = 0; frame <= rate; frame++)
            {
                motion.Tick((double)frame / rate);
                float width = EscMenuPolicy.Reveal(31, EscMenuPolicy.Width, motion.Shell);
                Check(width >= previous && width <= EscMenuPolicy.Width, "opening cannot reverse/overshoot");
                Check(!EscMenuPolicy.RowInput(true, motion.Settled, true, true, 0, 26, 0, 100) || motion.Shell == 1, "moving rows inactive");
                previous = width;
            }
            Check(motion.Settled && motion.Shell == 1, "settled despite frame rate");
            motion.SetOpen(false, 1); motion.Tick(1.09);
            float closing = motion.Shell;
            motion.SetOpen(true, 1.09);
            Check(motion.Shell == closing, "reopening during native close ghost remains continuous");
            motion.Tick(1.4); Check(motion.Shell == 1, "reopen completes");
            motion.SetSubmenu(true, 1.4); motion.Tick(1.48);
            Check(!motion.Settled && !EscMenuPolicy.RowInput(true, motion.Settled, true, true, 0, 26, 0, 100), "sliding content inactive");
            motion.Tick(1.7); motion.SetSubmenu(false, 1.7); motion.Tick(2);
            Check(motion.Slide == 0 && motion.Settled, "Back completes");
            motion.SetOpen(false, 2); motion.Tick(2.3);
            Check(motion.Shell == 0 && motion.Settled, "close reaches launcher");
        }
        for (int bits = 0; bits < 16; bits++)
        {
            bool open=(bits&1)!=0, settled=(bits&2)!=0, own=(bits&4)!=0, same=(bits&8)!=0;
            Check(EscMenuPolicy.RowInput(open, settled, own, same, 0, 26, 0, 100) == (bits == 15), "row gate combination");
        }
        Check(!EscMenuPolicy.RowInput(true,true,true,true,-1,25,0,100), "top-clipped input");
        Check(!EscMenuPolicy.RowInput(true,true,true,true,80,106,0,100), "bottom-clipped input");
        Check(EscMenuPolicy.RowInput(true,true,true,true,26,52,26,100), "scrolled visible row");
        Check(EscMenuPolicy.ConsumeOutside(true,true,true,false), "outside map click consumed");
        Check(!EscMenuPolicy.ConsumeOutside(true,true,true,true), "covered C region belongs to menu");
        Check(!EscMenuPolicy.ConsumeOutside(true,false,true,false), "modal owns click");
        Check(!EscMenuPolicy.ConsumeOutside(false,true,true,false), "closing ghost has no click handler");
        Check(!EscMenuPolicy.ConsumeOutside(true,true,false,false), "hover cannot dismiss");
        Check(EscMenuPolicy.BlocksTools(true,true,0), "native opening blocks C before first animated frame");
        Check(EscMenuPolicy.BlocksTools(false,true,.01f), "closing shell still blocks C");
        Check(!EscMenuPolicy.BlocksTools(false,true,0), "C returns after exact close");
        Check(!EscMenuPolicy.BlocksTools(false,false,1), "disabled stale shell cannot hide C");
        Check(EscMenuPolicy.BlocksTools(true,false,0), "native fallback menu also blocks C");
        for (int bits=0; bits<16; bits++)
            Check(EscMenuPolicy.NavigationInput((bits&1)!=0,(bits&2)!=0,(bits&4)!=0,(bits&8)!=0)==(bits==15), "footer waits for settled destination repaint and input ownership");
        float navigationTop=EscMenuPolicy.NavigationTop(EscMenuPolicy.Height);
        Check(EscMenuPolicy.NavigationWidth < (EscMenuPolicy.Width-2*EscMenuPolicy.Padding)/2, "navigation is compact");
        Check(navigationTop >= EscMenuPolicy.Height-EscMenuPolicy.Footer, "navigation stays outside page content");
        Check(navigationTop+EscMenuPolicy.NavigationHeight <= EscMenuPolicy.Height, "navigation fits shell");
        Check(EscMenuPolicy.Padding+EscMenuPolicy.NavigationWidth < EscMenuPolicy.Width-31, "navigation never overlaps Esc launcher");
        foreach (bool savingDisabled in new[]{false,true})
        foreach (bool commitment in new[]{false,true})
        foreach (bool files in new[]{false,true})
        {
            var keys=EscMenuPolicy.ActionKeys(savingDisabled,commitment,files);
            Check((Array.IndexOf(keys,"Save")>=0) == (!savingDisabled&&!commitment), "save role tracks native availability");
            Check((Array.IndexOf(keys,"LoadGame")>=0) == (files&&!commitment), "load role tracks native availability");
            int end=keys.Length-4;
            Check(keys[end]=="ReviewScenario" && keys[end+1]=="Options", "stable review/options roles");
            bool saveQuit=commitment&&!savingDisabled;
            Check(keys[end+2]==(saveQuit?"SaveAndQuitToMainMenu":"QuitToMainMenu") && keys[end+3]==(saveQuit?"SaveAndQuitToOS":"QuitToOS"), "commitment quit roles retain save semantics");
        }
        Check(!EscMenuPolicy.Supported(639,480) && !EscMenuPolicy.Supported(640,479) && EscMenuPolicy.Supported(640,480), "viewport fallback boundary");
        Console.WriteLine($"PASS {checks} Esc menu reveal, reversal, submenu, clipping and input contracts.");
    }
}
