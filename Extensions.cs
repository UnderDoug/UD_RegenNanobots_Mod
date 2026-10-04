using Genkit;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Linq;
using System.Reflection;
using System.Text;

using XRL;
using XRL.UI;
using XRL.Rules;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Capabilities;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Tinkering;
using XRL.World.ObjectBuilders;
using XRL.World.Parts.Skill;
using XRL.CharacterBuilds;
using XRL.CharacterBuilds.Qud;

using static UD_RegenNanobots_Mod.Const;

using Debug = UD_RegenNanobots_Mod.Debug;
using Options = UD_RegenNanobots_Mod.Options;

namespace UD_RegenNanobots_Mod
{
    public static class Extensions
    {
        private static bool doDebug => true;
        private static bool getDoDebug(string MethodName)
        {
            if (MethodName == nameof(UsesCharge))
                return false;

            return doDebug;
        }

        public static bool UsesCharge(this GameObject Object)
        {
            foreach (var part in Object?.GetPartsDescendedFrom<IActivePart>() ?? Enumerable.Empty<IActivePart>())
                if (part.ChargeUse > 0)
                    return true;

            return false;
        }

        public static string BonusOrPenalty(this int Int)
        {
            return Int >= 0 ? "bonus" : "penalty";
        }

        public static string BonusOrPenalty(this string SignedInt)
        {
            if (int.TryParse(SignedInt, out int Int))
                return Int >= 0 ? "bonus" : "penalty";
            throw new ArgumentException(
                $"{nameof(BonusOrPenalty)}(this string SignedInt): " +
                $"int.TryParse(SignedInt) failed to parse \"{SignedInt}\". " +
                $"SignedInt must be capable of conversion to int.");
        }

        public static StringBuilder AppendRegenerative(this StringBuilder sb, string value)
        {
            sb.AppendColored("regenerative", value);
            return sb;
        }

        public static StringBuilder AppendNanobots(this StringBuilder sb, string value)
        {
            sb.AppendColored("nanobots", value);
            return sb;
        }

        public static StringBuilder AppendGreyGoo(this StringBuilder sb, string value)
        {
            sb.AppendColored("greygoo", value);
            return sb;
        }

        public static StringBuilder AppendRule(this StringBuilder sb, string value)
        {
            // different from AppendRules (plural) since this doesn't force a new-line.
            sb.AppendColored("rules", value);
            return sb;
        }

        public static void SetEquipmentFrameColors(this GameObject Object, string TopLeft_Left_Right_BottomRight = null)
            => Object.SetStringProperty("EquipmentFrameColors", TopLeft_Left_Right_BottomRight, true)
            ;

        public static bool InheritsFrom(this GameObject Object, string Blueprint)
            => Object.Blueprint == Blueprint
            || Object.GetBlueprint().InheritsFrom(Blueprint)
            ;

        public static string Quote(this string String)
            => Utils.Quote($"{String}")
            ;

        public static string YehNah(this bool Condition, bool Flip = false)
            => Condition != Flip
            ? TICK.Color("G")
            : CROSS.Color("R")
            ;

    }
}
