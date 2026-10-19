using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ReframedUI
{
    internal sealed class NotificationIdentityComparer<T> : IEqualityComparer<T> where T : class
    {
        internal static readonly NotificationIdentityComparer<T> Instance = new NotificationIdentityComparer<T>();
        public bool Equals(T left, T right) => ReferenceEquals(left, right);
        public int GetHashCode(T value) => RuntimeHelpers.GetHashCode(value);
    }
    internal static class NotificationPolicy
    {
        internal readonly struct Style
        {
            internal readonly string Icon;
            internal readonly uint? Tint;
            internal Style(string icon, uint? tint = null) { Icon = icon; Tint = tint; }
        }
        internal static string IncidentIcon(string incident)
        {
            switch (incident)
            {
                case "ShortCircuit": case "SolarFlare": return "power";
                case "HeatWave": return "heat";
                case "Flashstorm": return "warning";
                case "ColdSnap": return "cold";
                case "RaidEnemy": return "danger";
                case "ManhunterPack": case "AnimalInsanityMass": case "AnimalInsanitySingle": case "FarmAnimalsWanderIn": return "animal";
                case "TraderCaravanArrival": case "OrbitalTraderArrival": return "trade";
                case "VisitorGroup": case "WandererJoin": case "RaidFriendly": return "person";
                case "CropBlight": return "food";
                case "ToxicFallout": case "Disease_Flu": case "Disease_Plague": case "Disease_Malaria":
                case "Disease_SleepingSickness": case "Disease_FibrousMechanites": case "Disease_SensoryMechanites":
                case "Disease_GutWorms": case "Disease_MuscleParasites": case "Disease_AnimalFlu": case "Disease_AnimalPlague": case "Disease_OrganDecay": return "health";
                default: return null;
            }
        }
        // Stable LetterDef identities, never localized labels. Null tint keeps
        // native severity/color, including ordinary unknown mod data.
        internal static Style Presentation(string definition, bool quest)
        {
            Style style;
            switch (definition)
            {
                case "ThreatBig": case "Bossgroup": style = new Style("danger", 0xE45F66); break;
                case "NewQuest_ThreatBig": style = new Style("quest", 0xE45F66); break;
                case "ThreatSmall": style = new Style("warning", 0xEBA45A); break;
                case "NegativeEvent": case "RitualOutcomeNegative": style = new Style("warning", 0xD7C86B); break;
                case "Death": style = new Style("health", 0xE45F66); break;
                case "PositiveEvent": case "RitualOutcomePositive": style = new Style("check", 0x25D977); break;
                case "NewQuest": case "RelicHuntInstallationFound": style = new Style("quest", 0x399DDD); break;
                case "BabyBirth": case "HumanPregnancy": style = new Style("health", 0x25D977); break;
                case "BabyToChild": case "ChildBirthday": case "ChildToAdult":
                case "RansomDemand": case "ChoosePawn": case "AcceptVisitors": case "AcceptJoiner": case "BetrayVisitors":
                    style = new Style("person"); break;
                case "GameEnded": case "NeutralEvent": style = new Style("info"); break;
                case "BundleLetter": style = new Style("envelope"); break;
                default: style = new Style(null); break;
            }
            if (!quest) return style;
            return new Style("quest", definition == "NeutralEvent" || definition == "PositiveEvent" ? 0x399DDD : style.Tint);
        }
        internal const float Row = 30, Gap = 5;
        internal static bool Supported(float width, float height)
            => !float.IsNaN(width) && !float.IsInfinity(width) && !float.IsNaN(height) && !float.IsInfinity(height) && width >= 80 && height >= Row;
        internal static int Capacity(float height)
            => float.IsNaN(height) || float.IsInfinity(height) || height < Row ? 0 : Math.Max(1, (int)Math.Min(int.MaxValue, Math.Floor(((double)height + Gap) / (Row + Gap))));
        // Oldest entries form one native bundle. Recent entries keep native order.
        internal static int Bundled(int count, int capacity)
            => capacity <= 0 ? count : count > capacity ? count - capacity + 1 : 0;
        internal static int AgeTicks(int current, int arrival) => (int)Math.Min(int.MaxValue, Math.Max(0L, (long)current - arrival));
        internal static double Step(double now, double previous)
            => double.IsNaN(now) || double.IsInfinity(now) || double.IsNaN(previous) || double.IsInfinity(previous) ? 0 : Math.Max(0, Math.Min(.25, now - previous));
        internal static double Move(double value, double target, double amount)
            => value < target ? Math.Min(target, value + amount) : Math.Max(target, value - amount);
        internal static double Ease(double value) { value = Math.Max(0, Math.Min(1, value)); return value * value * (3 - 2 * value); }
        internal static bool CanActivate(bool input, bool enabled, bool live, bool settled, bool member, bool mouseDown, int button)
            => input && enabled && live && settled && member && mouseDown && (button == 0 || button == 1);
    }
}
