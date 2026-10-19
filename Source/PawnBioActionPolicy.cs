namespace ReframedUI
{
    // Hidden native-card controls receive no keyboard/outside-pointer events.
    internal static class PawnBioActionPolicy
    {
        internal static bool ForwardHeader(bool layout, bool repaint, bool inside, bool pointer) =>
            layout || repaint || (inside && pointer);
    }
}
