using System;
using System.Collections.Generic;
using System.IO;
using XRL;

using static UD_RegenNanobots_Mod.Const;

namespace UD_RegenNanobots_Mod
{
    [HasModSensitiveStaticCache]
    [HasOptionFlagUpdate(Prefix = "Option_UD_RegenNanobots_Mod_")]
    public static class Options
    {
        // Debug Settings
        [OptionFlag] public static int DebugVerbosity;
        [OptionFlag] public static bool DebugIncludeInMessage;
        [OptionFlag] public static bool DebugRegenNanobotsModDescriptions;

    } //!-- public static class Options
}
