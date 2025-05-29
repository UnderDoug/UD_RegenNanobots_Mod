using System;
using System.Collections.Generic;
using System.IO;
using XRL;

using static UD_RegenNanobots_Mod.Const;

namespace UD_RegenNanobots_Mod
{
    [HasModSensitiveStaticCache]
    public static class Options
    {
        private static string Label(string Option = null)
        {
            string Label = "Option_UD_RegenNanobots_Mod";
            if (Option == null)
                return Label;
            return $"{Label}_{Option}";
        }
        private static Dictionary<string, string> Directory => new()
        {
            { nameof(DebugVerbosity), Label("DebugVerbosity") },
            { nameof(DebugIncludeInMessage), Label("DebugIncludeInMessage") },
            { nameof(DebugRegenNanobotsModDescriptions), Label("DebugRegenNanobotsModDescriptions") },
        };

        private static string GetStringOption(string ID, string Default = "")
        {
            if (Directory.ContainsKey(ID))
            {
                return XRL.UI.Options.GetOption(Directory[ID], Default: Default);
            }
            return Default;
        }
        private static bool GetBoolOption(string ID, bool Default = false)
        {
            return GetStringOption(ID, Default ? "Yes" : "No").EqualsNoCase("Yes");
        }
        private static int GetIntOption(string ID, int Default = 0)
        {
            return int.Parse(GetStringOption(ID, $"{Default}"));
        }

        private static void SetBoolOption(string ID, bool Value)
        {
            if (Directory.ContainsKey(ID))
                XRL.UI.Options.SetOption(Directory[ID], Value);
        }
        private static void SetStringOption(string ID, string Value)
        {
            if (Directory.ContainsKey(ID))
                XRL.UI.Options.SetOption(Directory[ID], Value);
        }
        private static void SetIntOption(string ID, int Value)
        {
            SetStringOption(Directory[ID], $"{Value}");
        }

        // Debug Settings
        public static int DebugVerbosity
        {
            get
            {
                return GetIntOption(nameof(DebugVerbosity), 0);
            }
            set
            {
                SetIntOption(nameof(DebugVerbosity), value);
            }
        }

        public static bool DebugIncludeInMessage
        {
            get
            {
                return GetBoolOption(nameof(DebugIncludeInMessage), false);
            }
            set
            {
                SetBoolOption(nameof(DebugIncludeInMessage), value);
            }
        }

        public static bool DebugRegenNanobotsModDescriptions
        {
            get
            {
                return GetBoolOption($"{nameof(DebugRegenNanobotsModDescriptions)}", false);
            }
            set
            {
                SetBoolOption(nameof(DebugRegenNanobotsModDescriptions), value);
            }
        }

    } //!-- public static class Options
}
