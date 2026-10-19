using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    internal static class PawnSocialPanel
    {
        private static readonly MethodInfo CheckSocial = AccessTools.Method(typeof(SocialCardUtility), "CheckRecache");
        private static readonly MethodInfo KnownPawns = AccessTools.Method(typeof(SocialCardUtility), "PawnsForSocialInfo");
        private static readonly MethodInfo ShowRelation = AccessTools.Method(typeof(SocialCardUtility), "ShouldShowPawnRelations");
        private static readonly MethodInfo Role = AccessTools.Method(typeof(SocialCardUtility), "DrawPawnRoleSelection");
        private static readonly MethodInfo Romance = AccessTools.Method(typeof(SocialCardUtility), "DrawTryRomance");
        private static readonly MethodInfo CanRomance = AccessTools.Method(typeof(SocialCardUtility), "CanDrawTryRomance");
        private static readonly MethodInfo RomanceTip = AccessTools.Method(typeof(SocialCardUtility), "RomanceExplanation");
        private static readonly List<Pawn> relations = new List<Pawn>();
        private static readonly List<EventCard> events = new List<EventCard>();
        private static readonly Color IdeologyInk = Theme.Rgb(0x22B8D0);
        private static Pawn cachedPawn;
        private static Vector2 relationScroll, eventScroll;
        private static double refreshAt;
        private static int logTick = -1;
        private static string nativeButton;
        private static Rect nativeButtonRect;
        internal static Action OpenOptions = null;
        internal static bool RoleReady, RomanceReady;
        internal static bool Ready => RoleReady && RomanceReady;

        private sealed class EventCard
        {
            internal LogEntry Entry;
            internal string Title, Description;
            private float width = -1f, height;
            internal float Height(float available)
            {
                if (width != available)
                { width = available; height = Math.Max(51f, PawnSocialText.Height(Description, Math.Max(1f, available - 16f)) + 29f); }
                return height;
            }
        }

        internal static void Reset()
        {
            cachedPawn = null; relations.Clear(); events.Clear();
            relationScroll = eventScroll = Vector2.zero; refreshAt = 0; logTick = -1;
        }

        internal static void Draw(Rect rect, Pawn pawn)
        {
            if (pawn == null) return;
            if (cachedPawn != pawn) { Reset(); cachedPawn = pawn; }
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                Text.Font = GameFont.Small; Text.Anchor = TextAnchor.UpperLeft; Text.WordWrap = true;
                CheckSocial.Invoke(null, new object[] { pawn }); // Native role cache and its expiry stay authoritative.
                DrawHeader(rect, pawn);
                Refresh(pawn);
                Rect content = new Rect(rect.x, rect.y + PawnSocialLayout.HeaderHeight, rect.width,
                    Math.Max(1f, rect.height - PawnSocialLayout.HeaderHeight));
                float leftWidth = PawnSocialLayout.RelationsWidth(rect.width);
                Rect left = new Rect(content.x, content.y, leftWidth, content.height);
                Rect right = new Rect(left.xMax + PawnSocialLayout.Gap, content.y,
                    content.width - leftWidth - PawnSocialLayout.Gap, content.height);
                DrawRelations(left, pawn);
                DrawEvents(right, pawn);
            }
        }

        private static void DrawHeader(Rect rect, Pawn pawn)
        {
            const float roleWidth = 95f, romanceWidth = 86f, cogWidth = 24f, gap = 6f;
            Rect cog = new Rect(rect.xMax - cogWidth, rect.y + 2f, cogWidth, 24f);
            Rect romance = new Rect(cog.x - gap - romanceWidth, cog.y, romanceWidth, 24f);
            Rect role = new Rect(romance.x - gap - roleWidth, cog.y, roleWidth, 24f);
            float labelWidth = Math.Max(1f, role.x - rect.x - 8f);
            Ideo ideo = pawn.Ideo;
            if (ModsConfig.IdeologyActive && ideo != null)
            {
                using (new Theme.GuiScope(true))
                { GUI.color = IdeologyInk; GUI.DrawTexture(new Rect(rect.x + 1f, rect.y + 3f, 14f, 14f), InspectorIcons.Get("ideology")); }
                string certainty = pawn.ideo?.Certainty.ToStringPercent("F0") ?? "";
                float percentWidth = PawnSocialText.Width(certainty) + 3f;
                float nameWidth = Math.Max(1f, Math.Min(PawnInspectorView.Measure(ideo.name) + 3f, labelWidth - percentWidth - 25f));
                PawnInspectorView.Label(new Rect(rect.x + 22f, rect.y, nameWidth, 20f), ideo.name, Theme.Ink);
                PawnInspectorView.SmallLabel(new Rect(rect.x + 25f + nameWidth, rect.y + 1f, percentWidth, 19f), certainty, Theme.Muted);
                Precept_Role assigned = ideo.GetRole(pawn);
                string subtitle = assigned == null ? "NoRoleAssigned".Translate().ToString() : assigned.LabelForPawn(pawn).CapitalizeFirst();
                PawnInspectorView.SmallLabel(new Rect(rect.x, rect.y + 19f, labelWidth, 17f), subtitle, Theme.Muted);
                TooltipHandler.TipRegion(new Rect(rect.x, rect.y, labelWidth, 35f), ideo.name + " " + certainty + "\n" + subtitle);
            }
            else PawnInspectorView.SmallLabel(new Rect(rect.x, rect.y + 5f, labelWidth, 20f), "No ideology", Theme.Muted);

            bool canRole = !pawn.Dead && ModsConfig.IdeologyActive && ideo != null && pawn.IsFreeNonSlaveColonist;
            if (canRole && RoleReady) InvokeNativeButton("role", role, () => Role.Invoke(null, new object[] { pawn, rect }));
            else PawnInspectorWidgets.Button(role, "Assign role", "plus", false);
            if (!pawn.Dead && (bool)CanRomance.Invoke(null, new object[] { pawn }) && RomanceReady)
                InvokeNativeButton("romance", romance, () => Romance.Invoke(null, new object[] { romance, pawn }));
            else PawnInspectorWidgets.Button(romance, "Romance", "health", false);
            if (PawnInspectorWidgets.IconButton(cog, "settings", "Social options", OpenOptions != null)) OpenOptions?.Invoke();
        }

        private static void InvokeNativeButton(string kind, Rect rect, Action draw)
        {
            string previous = nativeButton; Rect previousRect = nativeButtonRect;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white; Text.Font = GameFont.Small;
                nativeButton = kind; nativeButtonRect = rect;
                try { draw(); }
                finally { nativeButton = previous; nativeButtonRect = previousRect; }
            }
        }

        // The native methods still compute eligibility, cooldowns, explanations,
        // menu contents and callbacks. Only their one button-paint/input call changes.
        internal static bool DrawNativeButton(Rect rect, string label, bool drawBackground, bool doMouseoverSound,
            bool active, TextAnchor? anchor)
        {
            if (nativeButton == null) return Widgets.ButtonText(rect, label, drawBackground, doMouseoverSound, active, anchor);
            bool muted = GUI.color.r < .95f || GUI.color.g < .95f || GUI.color.b < .95f;
            bool input = active && GUI.enabled;
            using (new Theme.GuiScope(true))
            {
                GUI.color = GUI.contentColor = Color.white;
                string text = nativeButton == "role" ? "Assign role" : "Romance";
                string icon = nativeButton == "role" ? "plus" : "health";
                TooltipHandler.TipRegion(nativeButtonRect, label);
                if (muted)
                {
                    PawnInspectorWidgets.Button(nativeButtonRect, text, icon, false);
                    // Native Romance uses a muted yet clickable button to explain
                    // cooldown, missing partners or restrictions on an actual click.
                    return input && Widgets.ButtonInvisible(nativeButtonRect, doMouseoverSound);
                }
                return PawnInspectorWidgets.Button(nativeButtonRect, text, icon, input);
            }
        }

        private static void Refresh(Pawn pawn)
        {
            if (Time.realtimeSinceStartup < refreshAt && logTick == Find.PlayLog.LastTick) return;
            refreshAt = Time.realtimeSinceStartup + .75;
            relations.Clear();
            if (pawn.relations != null)
            {
                var family = pawn.relations.RelatedPawns.Where(other => other != null &&
                    (bool)ShowRelation.Invoke(null, new object[] { other, pawn }));
                var known = KnownPawns.Invoke(null, new object[] { pawn }) as IEnumerable<Pawn> ?? Enumerable.Empty<Pawn>();
                relations.AddRange(family.Concat(known).Distinct());
            }
            logTick = Find.PlayLog.LastTick;
            events.Clear();
            foreach (LogEntry entry in Find.PlayLog.AllEntries.Where(entry =>
                (entry is PlayLogEntry_Interaction || entry is PlayLogEntry_InteractionSinglePawn) && entry.Concerns(pawn)).OrderByDescending(entry => entry.Tick))
            {
                var interaction = AccessTools.Field(entry.GetType(), "intDef")?.GetValue(entry) as InteractionDef;
                events.Add(new EventCard { Entry = entry, Title = interaction?.LabelCap ?? entry.def.LabelCap,
                    Description = entry.ToGameStringFromPOV(pawn) });
            }
        }

        private static void DrawRelations(Rect viewport, Pawn pawn)
        {
            float height = Math.Max(viewport.height, PawnSocialLayout.RelationsHeight(relations.Count));
            float width = PawnInspectorWidgets.BeginScroll(viewport, ref relationScroll, height);
            try
            {
                float cardWidth = PawnSocialLayout.CardWidth(width);
                for (int i = 0; i < relations.Count; i++)
                {
                    Rect card = new Rect((i % 3) * (cardWidth + PawnSocialLayout.CardGap),
                        (i / 3) * (PawnSocialLayout.CardHeight + PawnSocialLayout.CardGap), cardWidth, PawnSocialLayout.CardHeight);
                    if (card.yMax < relationScroll.y || card.y > relationScroll.y + viewport.height) continue;
                    DrawRelation(card, pawn, relations[i]);
                }
                if (relations.Count == 0) PawnInspectorView.SmallLabel(new Rect(3, 3, width - 6f, 22), "No known relations", Theme.Muted);
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref relationScroll, height, 192715); }
        }

        private static void DrawRelation(Rect card, Pawn pawn, Pawn other)
        {
            Theme.RoundRect(card, Theme.Border, 4f);
            Theme.RoundRect(card.ContractedBy(.7f), GUI.enabled && Mouse.IsOver(card) ? Theme.Hover : Theme.Surface, 3.3f);
            using (new Theme.GuiScope(true))
            {
                GUI.color = Color.white;
                Rect portrait = new Rect(card.x + 3f, card.y + 1f, card.width - 6f, 45f);
                GUI.DrawTexture(portrait, PortraitsCache.Get(other, portrait.size, Rot4.South,
                    ColonistBarColonistDrawer.PawnTextureCameraOffset, 1.65f));
            }
            PawnInspectorView.Label(new Rect(card.x + 6f, card.y + 43f, card.width - 12f, 18f), other.LabelShortCap, Theme.Ink);
            string relation = string.Join(", ", pawn.GetRelations(other).Select(def => def.GetGenderSpecificLabel(other)));
            bool opinions = pawn.RaceProps.Humanlike && other.RaceProps.Humanlike && other.relations != null;
            int outgoing = opinions ? pawn.relations.OpinionOf(other) : 0, incoming = opinions ? other.relations.OpinionOf(pawn) : 0;
            if (string.IsNullOrEmpty(relation)) relation = (outgoing < -20 ? "Rival" : outgoing > 20 ? "Friend" : "Acquaintance").Translate();
            PawnInspectorView.SmallLabel(new Rect(card.x + 6f, card.y + 61f, card.width - 12f, 16f), relation, Theme.Muted);
            if (opinions)
            {
                float half = (card.width - 12f) / 2f;
                PawnInspectorView.SmallLabel(new Rect(card.x + 6f, card.y + 77f, half, 16f), outgoing.ToStringWithSign(), OpinionInk(outgoing));
                Color incomingInk = OpinionInk(incoming); incomingInk.a = .75f;
                PawnInspectorView.SmallLabel(new Rect(card.x + 6f + half, card.y + 77f, half, 16f), incoming.ToStringWithSign(), incomingInk, right: true);
            }
            string situation = SocialCardUtility.GetPawnSituationLabel(other, pawn);
            string tip = other.LabelCap + (string.IsNullOrEmpty(situation) ? "" : " (" + situation + ")") + "\n" + relation;
            if (opinions) tip += "\n\n" + pawn.LabelShort + " → " + other.LabelShort + ": " + outgoing + "\n" + pawn.relations.OpinionExplanation(other) +
                "\n\n" + other.LabelShort + " → " + pawn.LabelShort + ": " + incoming + "\n" + other.relations.OpinionExplanation(pawn);
            if (opinions)
            {
                string explanation = RomanceTip.Invoke(null, new object[] { pawn, other }) as string;
                if (!string.IsNullOrEmpty(explanation)) tip += "\n\n" + explanation;
            }
            TooltipHandler.TipRegion(card, tip);
            if (Widgets.ButtonInvisible(card)) CameraJumper.TryJumpAndSelect(other);
        }
        private static Color OpinionInk(int opinion) => opinion == 0 ? Theme.Muted : opinion > 0 ? Theme.Good : Theme.Danger;

        private static void DrawEvents(Rect viewport, Pawn pawn)
        {
            float height = Math.Max(viewport.height, events.Sum(card => card.Height(Math.Max(1f, viewport.width - 10f)) + 5f));
            float width = PawnInspectorWidgets.BeginScroll(viewport, ref eventScroll, height);
            try
            {
                float y = 0;
                foreach (EventCard item in events)
                {
                    float cardHeight = item.Height(width);
                    if (y + cardHeight >= eventScroll.y && y <= eventScroll.y + viewport.height)
                    {
                        Rect card = new Rect(0, y, width, cardHeight);
                        Theme.RoundRect(card, PawnInspectorView.Content, 4f);
                        using (new Theme.GuiScope(true))
                        {
                            GUI.color = item.Entry.IconColorFromPOV(pawn) ?? Theme.Muted;
                            Texture2D icon = item.Entry.IconFromPOV(pawn);
                            if (icon != null) GUI.DrawTexture(new Rect(8, y + 5f, 13, 13), icon);
                        }
                        string age = item.Entry.Age.ToStringTicksToPeriod() + " ago";
                        float ageWidth = PawnSocialLayout.TimestampWidth(PawnSocialText.Width(age), width);
                        PawnInspectorView.Label(new Rect(27f, y + 1f, Math.Max(1f, width - ageWidth - 38f), 19f), item.Title, Theme.Ink);
                        PawnInspectorView.SmallLabel(new Rect(width - ageWidth - 8f, y + 2f, ageWidth, 18f), age, Theme.Muted, right: true);
                        PawnSocialText.Paragraph(new Rect(8f, y + 23f, width - 16f, cardHeight - 27f), item.Description, Theme.Muted);
                        TooltipHandler.TipRegion(card, item.Entry.GetTipString());
                        if (item.Entry.CanBeClickedFromPOV(pawn) && Widgets.ButtonInvisible(card)) item.Entry.ClickedFromPOV(pawn);
                    }
                    y += cardHeight + 5f;
                }
                if (events.Count == 0) PawnInspectorView.SmallLabel(new Rect(8f, 4f, width - 16f, 22f), "No social events", Theme.Muted);
            }
            finally { PawnInspectorWidgets.EndScroll(viewport, ref eventScroll, height, 192716); }
        }
    }

    internal static class PawnSocialButtonSeam
    {
        internal static IEnumerable<CodeInstruction> Replace(IEnumerable<CodeInstruction> instructions, bool role)
        {
            var source = instructions.ToList();
            MethodInfo native = AccessTools.Method(typeof(Widgets), nameof(Widgets.ButtonText),
                new[] { typeof(Rect), typeof(string), typeof(bool), typeof(bool), typeof(bool), typeof(TextAnchor?) });
            bool ready = native != null && source.Count(instruction => instruction.Calls(native)) == 1;
            if (role) PawnSocialPanel.RoleReady = ready; else PawnSocialPanel.RomanceReady = ready;
            if (!ready)
            {
                Log.Warning("[Reframed UI] Native Social action button changed; native Social body retained.");
                return source;
            }
            return source.Select(original =>
            {
                var instruction = new CodeInstruction(original);
                if (instruction.Calls(native)) instruction.operand = AccessTools.Method(typeof(PawnSocialPanel), nameof(PawnSocialPanel.DrawNativeButton));
                return instruction;
            }).ToList();
        }
    }
    [HarmonyPatch(typeof(SocialCardUtility), "DrawPawnRoleSelection")]
    internal static class PawnSocialRoleButtonPatch
    { private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => PawnSocialButtonSeam.Replace(instructions, true); }
    [HarmonyPatch(typeof(SocialCardUtility), "DrawTryRomance")]
    internal static class PawnSocialRomanceButtonPatch
    { private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions) => PawnSocialButtonSeam.Replace(instructions, false); }
}
