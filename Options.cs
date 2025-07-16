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

        // Mod Options
        [OptionFlag] public static bool EnableRegenPopups;
        [OptionFlag] public static bool EnableRegenPopupsForEquipped;
        [OptionFlag] public static bool EnableRegenPopupsForInventory;
        [OptionFlag] public static bool EnableRegenPopupsForImportant;
        [OptionFlag] public static bool EnableRestorePopups;
        [OptionFlag] public static bool EnableRestorePopupsForEquipped;
        [OptionFlag] public static bool EnableRestorePopupsForInventory;
        [OptionFlag] public static bool EnableRestorePopupsForImportant;

    } //!-- public static class Options
}
