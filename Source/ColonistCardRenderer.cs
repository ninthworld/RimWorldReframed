using System;
using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace ReframedUI
{
    // The card geometry is inferred from the reference; gameplay state comes from
    // the installed native drawer's predicates, textures and semantic colors.
    [StaticConstructorOnStartup]
    internal static class ColonistCardRenderer
    {
        private static readonly Texture2D MoodFill = FadeTexture(.38f);
        private static readonly Texture2D NameShade = FadeTexture(.8f, .48f);
        private static readonly Texture2D MoodRisk = ContentFinder<Texture2D>.Get("UI/Widgets/MoodGradient");
        private static readonly Texture2D RiskEdge = ContentFinder<Texture2D>.Get("UI/Widgets/SubtleGradient");
        private static readonly Texture2D Death = ContentFinder<Texture2D>.Get("UI/Misc/DeadColonist");
        private static readonly Texture2D FormingCaravan = Icon("FormingCaravan");
        private static readonly Texture2D MentalAggro = Icon("MentalStateAggro");
        private static readonly Texture2D MentalOther = Icon("MentalStateNonAggro");
        private static readonly Texture2D MedicalRest = Icon("MedicalRest");
        private static readonly Texture2D Sleeping = Icon("Sleeping");
        private static readonly Texture2D Fleeing = Icon("Fleeing");
        private static readonly Texture2D Attacking = Icon("Attacking");
        private static readonly Texture2D Idle = Icon("Idle");
        private static readonly Texture2D Burning = Icon("Burning");
        private static readonly Texture2D Inspired = Icon("Inspired");
        private static readonly List<Badge> Badges = new List<Badge>(5);
        private static GUIStyle nameStyle;
        private static GUIStyle locationStyle;

        internal static float EntryAlpha(Rect rect, Map map)
        {
            // The registered ColonistGroupAlpha adapter converts this local rect
            // to root coordinates while ColonistGroupsHud.Drawing is true.
            float alpha = Find.ColonistBar.GetEntryRectAlpha(rect);
            if (map == null ? !WorldRendererUtility.WorldSelected :
                map != Find.CurrentMap || WorldRendererUtility.WorldSelected)
                alpha = Mathf.Min(alpha, .4f);
            return alpha;
        }

        internal static void Draw(Rect rect, Pawn pawn, Map map, bool highlight, float unitScale)
        {
            if (pawn == null || Event.current.type != EventType.Repaint) return;
            PawnBarDiagnostics.RecordDraw(pawn, rect);
            float s = Mathf.Max(.1f, unitScale);
            float alpha = EntryAlpha(rect, map) * GUI.color.a;
            using (new Theme.GuiScope(true))
            {
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.contentColor = Color.white;
                bool showRisk = Prefs.VisibleMood && pawn.needs?.mood != null &&
                    pawn.mindState?.mentalBreaker != null && pawn.mindState.mentalBreaker.CanDoRandomMentalBreaks &&
                    !pawn.Dead && !pawn.Downed;
                MoodThreshold risk = showRisk ? MoodThresholdExtensions.CurrentMoodThresholdFor(pawn) : MoodThreshold.None;
                Color riskColor = risk.GetColor();
                // Like the native drawer, cover the atlas's opaque center with
                // the card face; only the expanded warning edge should remain.
                if (showRisk && alpha >= .999f && risk.EdgeExpansion() > 0)
                {
                    GUI.color = riskColor;
                    Widgets.DrawAtlas(rect.ExpandedBy(risk.EdgeExpansion() * s), RiskEdge);
                }
                GUI.color = new Color(1, 1, 1, alpha);
                // Keep the button rim in the warning's hue. Its opaque alpha
                // also keeps the shared rim cache independent of the native pulse.
                Color? rim = null;
                if (showRisk && risk != MoodThreshold.None)
                {
                    Color warningRim = Color.Lerp(Theme.Surface, riskColor, .65f);
                    warningRim.a = 1f;
                    rim = warningRim;
                }
                Theme.ButtonFace(rect, Mouse.IsOver(rect) && GUI.enabled ? Theme.Hover : Theme.Surface,
                    radius: 3f * s, rim: rim);

                Rect inset = rect.ContractedBy(1f * s);
                if (pawn.needs?.mood != null)
                {
                    Rect fill = inset;
                    fill.yMin = fill.yMax - fill.height * Mathf.Clamp01(pawn.needs.mood.CurLevelPercentage);
                    // The amount still rises from the bottom. The soft leading
                    // edge extends across the complete card, independent of its name fade.
                    PaintTexture(fill, MoodFill, new Color(.4f, .47f, .53f, alpha), 2f * s);
                }

                if (showRisk && alpha >= .999f)
                {
                    Color wash = riskColor;
                    wash.a *= risk < MoodThreshold.Major ? .1f : .15f;
                    GUI.color = Color.white;
                    Theme.RoundRect(inset, wash, 2f * s);
                }

                // PortraitsCache applies the native downed/dead rotation and offset.
                // Keep its real health state, apparel, headgear and South-facing pose.
                Rect portrait = new Rect(rect.x + s, rect.y + s, rect.width - 2f * s, 64f * s);
                bool rotated = pawn.health.State != PawnHealthState.Mobile && !pawn.ageTracker.CurLifeStage.alwaysDowned;
                GUI.color = new Color(1, 1, 1, alpha);
                GUI.DrawTexture(portrait, PortraitsCache.Get(pawn, new Vector2(58f * s, 64f * s),
                    Rot4.South, ColonistBarColonistDrawer.PawnTextureCameraOffset,
                    rotated ? ColonistBarColonistDrawer.PawnTextureCameraZoom : 1.65f));
                if (showRisk && risk != MoodThreshold.None)
                {
                    Color tint = riskColor;
                    tint.a *= alpha;
                    PaintTexture(inset, MoodRisk, tint, 2f * s);
                }

                Color footer = Theme.Surface;
                if (showRisk && risk != MoodThreshold.None)
                    footer = Color.Lerp(footer, riskColor, .25f);
                footer.a = alpha;
                DrawNameBackdrop(new Rect(inset.x, inset.yMax - 28f * s, inset.width, 28f * s), footer, 2f * s);
                if (pawn.Dead) PaintTexture(inset, Death, new Color(1, 1, 1, alpha), 2f * s);

                bool specificTip = DrawBadges(rect, pawn, s, alpha);
                Rect health = new Rect(rect.x + 5f * s, rect.yMax - 4f * s, rect.width - 10f * s, 2f * s);
                float remaining = pawn.health.summaryHealth.SummaryHealthPercent;
                if (remaining < .999f)
                {
                    GUI.color = new Color(1, 1, 1, alpha);
                    Theme.RoundRect(health, new Color(.22f, .05f, .05f, .8f), s);
                    Rect value = health;
                    value.width *= Mathf.Clamp01(remaining);
                    Theme.RoundRect(value, new Color(1f, .15f, .14f, .86f), s);
                    Rect hit = health.ExpandedBy(2f * s);
                    if (Mouse.IsOver(hit))
                    {
                        TooltipHandler.TipRegion(hit, "Health".Translate() + ": " + remaining.ToStringPercent());
                        specificTip = true;
                    }
                }
                DrawName(rect, pawn, s, alpha, remaining < .999f);
                if (!pawn.Dead && map != Find.CurrentMap) DrawLocation(rect, map, s, alpha);

                if (highlight)
                {
                    GUI.color = new Color(1, 1, 1, alpha);
                    Widgets.DrawBox(rect, Mathf.Max(1, Mathf.RoundToInt(2f * s)));
                }
                var caravan = pawn.GetCaravan();
                bool selected = WorldRendererUtility.WorldSelected ? caravan != null && Find.WorldSelector.IsSelected(caravan) :
                    pawn.Dead ? pawn.Corpse != null && Find.Selector.IsSelected(pawn.Corpse) : Find.Selector.IsSelected(pawn);
                if (selected)
                {
                    GUI.color = new Color(1, 1, 1, alpha);
                    ColonistGroupsHud.QueueSelectionOverlay(pawn, rect.ExpandedBy(2f * s));
                }
                if (!specificTip)
                {
                    string location = map == null ? "Caravan".Translate().ToString() :
                        map.Parent?.LabelCap.ToString() ?? "Map " + map.uniqueID;
                    TooltipHandler.TipRegion(rect, pawn.LabelShortCap + "\n" +
                        (pawn.Dead ? "Dead".Translate() + " • " : "") + location);
                }
            }
        }

        private static void DrawLocation(Rect rect, Map map, float s, float alpha)
        {
            Text.Font = GameFont.Small;
            if (locationStyle == null || locationStyle.font != Text.CurFontStyle.font)
            {
                locationStyle = new GUIStyle(Text.CurFontStyle) { alignment = TextAnchor.MiddleCenter,
                    wordWrap = false, richText = false, clipping = TextClipping.Clip,
                    padding = new RectOffset(), margin = new RectOffset() };
                locationStyle.normal.textColor = Color.white;
            }
            locationStyle.fontSize = Mathf.Max(5, Mathf.RoundToInt(9f * s));
            Rect marker = new Rect(rect.x + 3f * s, rect.y + 3f * s, rect.width - 6f * s, 11f * s);
            GUI.color = new Color(1, 1, 1, alpha);
            Theme.RoundRect(marker, new Color(.025f, .035f, .045f, .9f), 2f * s);
            GUI.color = new Color(.78f, .84f, .88f, alpha);
            GUI.Label(marker, map == null ? "Caravan".Translate().ToString() : "Other map", locationStyle);
        }

        private static void DrawName(Rect rect, Pawn pawn, float s, float alpha, bool hasHealth)
        {
            Text.Font = GameFont.Small;
            if (nameStyle == null || nameStyle.font != Text.CurFontStyle.font)
            {
                nameStyle = new GUIStyle(Text.CurFontStyle)
                {
                    alignment = TextAnchor.MiddleCenter,
                    wordWrap = false,
                    clipping = TextClipping.Clip,
                    padding = new RectOffset(0, 0, 0, 0),
                    margin = new RectOffset(0, 0, 0, 0),
                    richText = false
                };
                nameStyle.normal.textColor = Color.white;
            }
            nameStyle.fontSize = Mathf.Max(1, Mathf.RoundToInt(12f * s));
            Rect label = new Rect(rect.x + 3f * s, rect.yMax - (hasHealth ? 22f : 21f) * s,
                rect.width - 6f * s, 19f * s);
            string name = Ellipsis(pawn.LabelShortCap, label.width);
            Color ink = PawnNameColorUtility.PawnNameColorOf(pawn);
            ink.a *= alpha;
            GUI.color = ink;
            GUI.Label(label, name, nameStyle);
            if (pawn.Drafted)
            {
                float width = Mathf.Min(label.width, nameStyle.CalcSize(new GUIContent(name)).x);
                Widgets.DrawBoxSolid(new Rect(label.center.x - width * .5f, label.yMax - 3f * s,
                    width, Mathf.Max(.6f * s, 1f / Prefs.UIScale)), ink);
            }
        }

        private static string Ellipsis(string text, float width)
        {
            if (nameStyle.CalcSize(new GUIContent(text)).x <= width) return text;
            int length = text.Length;
            while (length > 0 && nameStyle.CalcSize(new GUIContent(text.Substring(0, length) + "…")).x > width) length--;
            return text.Substring(0, length) + "…";
        }

        private static bool DrawBadges(Rect rect, Pawn pawn, float s, float alpha)
        {
            Badges.Clear();
            if (pawn.Dead) return false;
            // Native simultaneous-state channels and activity precedence are kept;
            // these are distinct from both mood amount and remaining health.
            if (pawn.IsFormingCaravan()) Add(FormingCaravan, "ActivityIconFormingCaravan".Translate());
            if (pawn.InAggroMentalState) Add(MentalAggro, pawn.MentalStateDef.LabelCap);
            else if (pawn.InMentalState) Add(MentalOther, pawn.MentalStateDef.LabelCap);
            else if (pawn.InBed() && pawn.CurrentBed().Medical) Add(MedicalRest, "ActivityIconMedicalRest".Translate());
            else if (pawn.CurJob != null && pawn.jobs.curDriver.asleep || pawn.GetCaravan() != null && pawn.needs?.rest?.Resting == true)
                Add(Sleeping, "ActivityIconSleeping".Translate());
            else if (pawn.CurJob?.def == JobDefOf.FleeAndCower) Add(Fleeing, "ActivityIconFleeing".Translate());
            else if (Fighting(pawn)) Add(Attacking, "ActivityIconAttacking".Translate());
            else if (pawn.mindState.IsIdle && GenDate.DaysPassed >= 1) Add(Idle, "ActivityIconIdle".Translate());
            if (pawn.IsBurning()) Add(Burning, "ActivityIconBurning".Translate());
            if (pawn.Inspired) Add(Inspired, pawn.InspirationDef.LabelCap);
            if (pawn.IsSlaveOfColony) Add(pawn.guest.GetIcon());
            else
            {
                Precept_Role role = pawn.Ideo?.GetRole(pawn);
                if (role != null) Add(role.Icon, null, pawn.Ideo.Color);
                else
                {
                    Faction faction = pawn.HasExtraMiniFaction() ? pawn.GetExtraMiniFaction() :
                        pawn.HasExtraHomeFaction() ? pawn.GetExtraHomeFaction() : null;
                    if (faction != null) Add(faction.def.FactionIcon, null, faction.Color);
                }
            }
            bool hovered = false;
            const int perRow = 3;
            for (int i = 0; i < Badges.Count; i++)
            {
                int row = i / perRow;
                int count = Mathf.Min(perRow, Badges.Count - row * perRow);
                float width = count * 14f - 2f;
                Rect badge = new Rect(rect.center.x - width * .5f * s + i % perRow * 14f * s,
                    rect.yMax - (35f + row * 14f) * s, 12f * s, 12f * s);
                GUI.color = new Color(1, 1, 1, alpha);
                Theme.RoundRect(badge.ExpandedBy(s), new Color(.025f, .035f, .045f, .9f), 3f * s);
                Badge item = Badges[i];
                Color tint = item.Color;
                tint.a *= alpha;
                PaintTexture(badge, item.Texture, tint);
                if (Mouse.IsOver(badge.ExpandedBy(s)))
                {
                    hovered = item.Tooltip != null;
                    if (hovered) TooltipHandler.TipRegion(badge.ExpandedBy(s), item.Tooltip);
                }
            }
            return hovered;
        }

        private static bool Fighting(Pawn pawn) => pawn.CurJob != null &&
            (pawn.CurJob.def == JobDefOf.AttackMelee || pawn.CurJob.def == JobDefOf.AttackStatic ||
             pawn.CurJob.def == JobDefOf.Wait_Combat && pawn.stances.curStance is Stance_Busy busy && busy.focusTarg.IsValid);

        private static void Add(Texture2D texture, string tooltip = null, Color? color = null)
            => Badges.Add(new Badge { Texture = texture, Tooltip = tooltip, Color = color ?? Color.white });
        private static Texture2D Icon(string name) => ContentFinder<Texture2D>.Get("UI/Icons/ColonistBar/" + name);
        private static void PaintTexture(Rect rect, Texture2D texture, Color tint, float radius = 0f)
        {
            if (rect.height <= 0 || rect.width <= 0) return;
            GUI.color = Color.white;
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true, 0f, tint, 0f, radius);
        }
        internal static void DrawNameBackdrop(Rect rect, Color tint, float radius)
            => PaintTexture(rect, NameShade, tint, radius);

        private static Texture2D FadeTexture(float bottomOpacity, float solidFraction = 0f)
        {
            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color[64];
            for (int y = 0; y < pixels.Length; y++)
                pixels[y] = new Color(1, 1, 1, bottomOpacity * Mathf.SmoothStep(0, 1,
                    1f - Mathf.Clamp01((y / 63f - solidFraction) / (1f - solidFraction))));
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return texture;
        }
        private struct Badge
        {
            internal Texture2D Texture;
            internal string Tooltip;
            internal Color Color;
        }
    }
}
