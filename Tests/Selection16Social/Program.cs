using System;
using ReframedUI;

internal static class Program
{
    private static int checks;
    private static void Check(bool value, string label) { checks++; if (!value) throw new Exception(label); }
    public static void Main()
    {
        // The normalized 560px shell supplies 536px of content. Both three-card
        // rows and native tooltips must remain accessible when a rail is needed.
        foreach (float body in new[] { 536f, 664f, 760f })
        {
            float left = PawnSocialLayout.RelationsWidth(body);
            foreach (float reserve in new[] { 0f, 10f })
            {
                float width = left - reserve;
                float card = PawnSocialLayout.CardWidth(width);
                Check(card >= 60f, "reference density leaves readable relation cards");
                Check(Math.Abs(3 * card + 2 * PawnSocialLayout.CardGap - width) < .001,
                    "last relation card stays inside its viewport with or without the rail");
            }
            float feed = body - left - PawnSocialLayout.Gap;
            float time = PawnSocialLayout.TimestampWidth(300, feed);
            Check(feed - time - 38 > 90, "long timestamp retains a useful event-title lane");
        }
        Check(PawnSocialLayout.RelationsHeight(0) == 0, "empty relations have no artificial card row");
        Check(PawnSocialLayout.RelationsHeight(3) == PawnSocialLayout.CardHeight, "three relations fit one row");
        Check(PawnSocialLayout.RelationsHeight(4) == 2 * PawnSocialLayout.CardHeight + PawnSocialLayout.CardGap,
            "fourth relation starts a reachable second row");
        Check(PawnSocialLayout.RelationsHeight(100) > 271 - PawnSocialLayout.HeaderHeight,
            "large known-family set requires scrolling instead of being dropped");
        Check(PawnSocialLayout.TimestampWidth(20, 300) == 23, "short age labels keep compact width");
        Console.WriteLine("PASS " + checks + " Social layout/accessibility contracts; no live UI acceptance claimed.");
    }
}
