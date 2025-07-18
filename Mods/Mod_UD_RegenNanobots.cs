using System;
using System.Collections.Generic;
using System.Text;

using XRL.Language;
using XRL.Rules;
using XRL.UI;
using XRL.World;
using XRL.World.Capabilities;
using XRL.World.Effects;
using XRL.World.Parts.Skill;
using XRL.World.Tinkering;

using UD_RegenNanobots_Mod;

using static UD_RegenNanobots_Mod.Const;
using static UD_RegenNanobots_Mod.Utils;
using Debug = UD_RegenNanobots_Mod.Debug;
using Options = UD_RegenNanobots_Mod.Options;

using SerializeField = UnityEngine.SerializeField;

namespace XRL.World.Parts
{
    [Serializable]
    public class Mod_UD_RegenNanobots : IModification
    {
        private static readonly bool doDebug = true;
        private static bool getDoDebug(object what = null)
        {
            List<object> doList = new()
            {
                'V',    // Vomit
            };
            List<object> dontList = new()
            {
                nameof(GetDynamicModName),
                'R',    // Removal
                'S',    // Serialisation
                'x',    // trace
            };

            if (what != null && doList.Contains(what))
                return true;

            if (what != null && dontList.Contains(what))
                return false;

            return doDebug;
        }

        private static bool DebugRegenNanobotsModDescriptions => Options.DebugRegenNanobotsModDescriptions;

        private Statistic Hitpoints => ParentObject?.GetStat(nameof(Hitpoints));

        public Examiner Examiner => ParentObject?.GetPart<Examiner>();

        private GameObject Equipper => ParentObject?.Equipped;

        private GameObject Holder => ParentObject?.Holder;

        public static readonly string EQ_FRAME_COLORS = "RWGY";

        public static readonly string REGENERATIVE = "regenerative";

        public static readonly string NANOBOTS = "nanobots";

        public static readonly string MOD_NAME = $"{REGENERATIVE} {NANOBOTS}";

        public static readonly string MOD_NAME_COLORED = "{{regenerative|" + REGENERATIVE + "}} {{nanobots|" + NANOBOTS + "}}";

        public static int BaseChargeMultiplier => 5;

        public static DieRoll RegenDie => new($"1d40");

        public static DieRoll RestoreDie => new($"1d120");

        public int ObjectTechTier => ParentObject.GetTechTier();

        public float RegenFactor = 0.01f;

        public int CumulativeRegen = 0;

        public int TimesRestored = 0;

        public int CumulativeChargeUse = 0;

        public float HitpointPercent => Hitpoints != null 
            ? (float)(Hitpoints.BaseValue - Hitpoints.Penalty) / (float)Hitpoints.BaseValue 
            : 1;

        public int RegenRolls => Math.Max(1, 4 - (int)Math.Ceiling(HitpointPercent / 0.25f));

        private bool isDamaged => ParentObject != null && ParentObject.isDamaged();
        private bool isBusted => ParentObject != null && ParentObject.IsBroken();
        private bool isRusted => ParentObject != null && ParentObject.IsRusted();
        private bool isShattered => ParentObject != null && ParentObject.HasEffect<ShatteredArmor>();

        private bool isHeld => Holder != null;
        private bool isEquipped => Equipper != null;
        private bool isInInventory => isHeld && !isEquipped;
        private bool isImportant => ParentObject != null && ParentObject.IsImportant();

        private bool wantsRestore => isBusted || isRusted || isShattered;

        public Mod_UD_RegenNanobots()
            : base()
        {
        }

        public Mod_UD_RegenNanobots(int Tier)
            : base(Tier)
        {
        }
        public override bool AllowStaticRegistration()
        {
            return true;
        }
        public override int GetModificationSlotUsage()
        {
            return 1;
        }
        public override void Configure()
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(Configure)}",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            WorksOnSelf = true;
            IsBreakageSensitive = false;
            IsRustSensitive = false;
            IsEMPSensitive = true;
            IsPowerLoadSensitive = false;
            IsTechScannable = true;
            NameForStatus = "RegenerativeNanobots";

            Debug.LastIndent = indent;
        }
        public override void TierConfigure()
        {
            int indent = Debug.LastIndent;

            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(TierConfigure)}",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            ChargeUse = CalculateBaseChargeUse();
            ChargeMinimum = CalculateBaseChargeUse();

            Debug.LastIndent = indent;
        }
        public override bool ModificationApplicable(GameObject Object)
        {
            return CanRegen(Object);
        }
        public static bool CanRegen(GameObject Object, string Context = "")
        {
            if (Context == "Internal")
            {
                int indent = Debug.LastIndent;

                Debug.Entry(4,
                    $"{nameof(Mod_UD_RegenNanobots)}." +
                    $"{nameof(CanRegen)}(" +
                    $"{Object?.DebugName ?? NULL})",
                    Indent: indent + 1, Toggle: doDebug);

                Debug.LastIndent = indent;
            }
            return Object != null && Object.HasStat("Hitpoints");
        }

        public static int CalculateBaseChargeUse(int Tier = 1, int ObjectTechTier = 0, int Complexity = 0)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(CalculateBaseChargeUse)}() [static]", 
                Indent: indent, Toggle: getDoDebug('x'));

            int multiplier = BaseChargeMultiplier;
            multiplier += ObjectTechTier;
            multiplier += Complexity;

            Debug.LastIndent = indent;
            return Tier * multiplier;
        }
        public int CalculateBaseChargeUse()
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(CalculateBaseChargeUse)}() [instance]",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            int complexity = 1;
            int objectTechTier = 1;
            if (ParentObject != null)
            {
                objectTechTier = ObjectTechTier;
            }
            if (Examiner != null)
            {
                complexity = Examiner.Complexity;
            }

            int output = CalculateBaseChargeUse(Tier, objectTechTier, complexity);

            Debug.LastIndent = indent;
            return output;
        }

        public static string GetDescription(int Tier)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetDescription)}({nameof(Tier)}: {Tier}) [static]",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            Debug.LastIndent = indent;
            return $"{MOD_NAME_COLORED}: while powered, this item will gradually regenerate HP and has a small chance to be restored from being cracked, rusted, or broken. Higher tier items require more charge to function. Higher damage results in faster regeneration but a higher charge draw.";
        }
        public static string GetDescription(GameObject Item, int Tier)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetDescription)}(GameObject, {nameof(Tier)}: {Tier}) [static]",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            string output = null;

            Statistic hitpoints = Item.GetStat("Hitpoints");
            if (hitpoints != null)
            {
                if (!Item.TryGetPart(out Mod_UD_RegenNanobots modRegenNanobots))
                {
                    modRegenNanobots = new();
                }
                float regenFactor = modRegenNanobots.RegenFactor;
                int regenAmount = GetRegenAmount(hitpoints, regenFactor, ForceMax: true);
                float percentHP = modRegenNanobots.HitpointPercent;
                int regenRolls = modRegenNanobots.RegenRolls;

                StringBuilder SB = Event.NewStringBuilder();
                SB.Append(GetDynamicModName(Item, Tier, false, percentHP)).Append(": ");
                SB.Append("while powered, this item ");
                SB.Append("has a ").Append(RegenDie.Min() * regenRolls).Append(" in ").Append(RegenDie.Max()).Append(" chance per turn to ");
                SB.Append("regenerate ").Append(regenAmount).Append(" HP and ");
                SB.Append("has a ").Append(RestoreDie.Min()).Append(" in ").Append(RestoreDie.Max()).Append(" chance per turn to ");
                SB.Append("be restored from being cracked, rusted, or broken. ");
                SB.Append("Higher tier items require more charge to function. ");
                SB.Append("Higher damage results in faster regeneration but a higher charge draw.");

                output = Event.FinalizeString(SB);
            }
            else
            {
                output = GetDescription(Tier);
            }

            Debug.LastIndent = indent;
            return output;
        }
        public string GetInstanceDescription()
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetInstanceDescription)}",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            string output = GetDescription(ParentObject, Tier);

            Debug.LastIndent = indent;
            return output;
        }
        public static string GetDynamicModName(GameObject Item, int Tier, bool LowerCase = false, float percentHP = 0f)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetDynamicModName)} (static)",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            string regenerative = LowerCase ? Grammar.MakeLowerCase(REGENERATIVE) : Grammar.MakeTitleCase(REGENERATIVE);
            string nanobots = LowerCase ? Grammar.MakeLowerCase(NANOBOTS) : Grammar.MakeTitleCase(NANOBOTS);

            bool needsRestored = Item.HasEffect<ShatteredArmor>() || Item.HasEffect<Rusted>() || Item.HasEffect<Broken>();

            string output = $"{(regenerative).Color("regenerative")} {(nanobots).Color(needsRestored ? "greygoo" : "nanobots")}";

            Statistic Hitpoints = Item?.GetStat("Hitpoints");

            if (Item  != null && Hitpoints != null)
            {
                int remainingHP = Hitpoints.Value;
                int maxHP = Hitpoints.BaseValue;
                bool isPercentHPZero = percentHP == 0f;
                percentHP = isPercentHPZero ? (float)remainingHP / (float)maxHP : percentHP;
                int breakPoint = (int)Math.Floor(regenerative.Length * percentHP);

                bool doDebug = getDoDebug(nameof(GetDynamicModName));
                Debug.Entry(4, $"Item: {Item?.DebugName ?? NULL} | {remainingHP}/{maxHP} = {percentHP}; {nameof(breakPoint)}: {breakPoint}",
                    Indent: indent + 2, Toggle: doDebug);

                if (breakPoint < regenerative.Length - 1 || Hitpoints.Penalty != 0)
                {
                    int brightPoint = Math.Max(0, breakPoint - 1);
                    int whitePoint = brightPoint;
                    int dullPoint = Math.Max(1, Math.Min(whitePoint + 1, regenerative.Length - 1));

                    Debug.Entry(4, $"{nameof(brightPoint)}: {brightPoint}",
                        Indent: indent + 3, Toggle: doDebug);
                    Debug.Entry(4, $"{nameof(whitePoint)}: {whitePoint}",
                        Indent: indent + 3, Toggle: doDebug);
                    Debug.Entry(4, $"{nameof(dullPoint)}: {dullPoint}",
                        Indent: indent + 3, Toggle: doDebug);

                    string regenBright = brightPoint > 0 ? regenerative[..brightPoint] : "";
                    if (!regenBright.IsNullOrEmpty())
                    {
                        regenBright = regenBright.Color("regenerating");
                    }
                    string regenWhite = regenerative.Substring(whitePoint, 1);
                    if (!regenWhite.IsNullOrEmpty())
                    {
                        regenWhite = regenWhite.Color("Y");
                    }
                    string regenDull = regenerative[dullPoint..];

                    Debug.Entry(4, $"{nameof(regenBright)}", $"{regenBright}",
                        Indent: indent + 3, Toggle: doDebug);
                    Debug.Entry(4, $"{nameof(regenWhite)}", $"{regenWhite}",
                        Indent: indent + 3, Toggle: doDebug);
                    Debug.Entry(4, $"{nameof(regenDull)}", $"{(regenDull).Color("greygoo")}",
                        Indent: indent + 3, Toggle: doDebug);

                    output = regenBright + regenWhite + (regenDull + " " + nanobots).Color("greygoo");
                }
            }

            Debug.LastIndent = indent;
            return output;
        }
        public string GetDynamicModName(bool LowerCase = false)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetDynamicModName)} [instance]",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            string output = GetDynamicModName(ParentObject, Tier, LowerCase, HitpointPercent);

            Debug.LastIndent = indent;
            return output;
        }

        public override void Attach()
        {
            int indent = Debug.LastIndent;
            Debug.Entry(3, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(Attach)}",
                Indent: indent + 1, Toggle: getDoDebug());

            bool didApplyEquipmentFrameColors = ApplyEquipmentFrameColors();

            Debug.LastIndent = indent;
            base.Attach();
        }
        public override void ApplyModification(GameObject Object)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(3,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(ApplyModification)}(" +
                $"{Object?.DebugName ?? NULL})",
                Indent: indent + 1, Toggle: getDoDebug());

            if (ChargeUse > 0)
            {
                if (!Object.HasPartDescendedFrom<IEnergyCell>())
                {
                    Object.RequirePart<EnergyCellSocket>();
                }
            }
            IncreaseDifficultyAndComplexity(3, 2);
            TierConfigure();

            Debug.LastIndent = indent;
            base.ApplyModification(Object);
        }
        public bool ApplyEquipmentFrameColors()
        {
            int indent = Debug.LastIndent;
            Debug.Entry(3,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(ApplyEquipmentFrameColors)}(" +
                $"{ParentObject?.DebugName ?? NULL})",
                Indent: indent + 1, Toggle: getDoDebug());

            if (ParentObject != null && !ParentObject.HasTagOrProperty("EquipmentFrameColors"))
            {
                ParentObject.SetEquipmentFrameColors(EQ_FRAME_COLORS);
            }

            bool didApplyEquipmentFrameColors = ParentObject.GetPropertyOrTag("EquipmentFrameColors") == EQ_FRAME_COLORS;

            Debug.LoopItem(4, $"{nameof(didApplyEquipmentFrameColors)}: {didApplyEquipmentFrameColors}",
                Good: didApplyEquipmentFrameColors, Indent: indent + 2, Toggle: getDoDebug());

            Debug.LastIndent = indent;
            return didApplyEquipmentFrameColors;
        }

        public int GetRegenChargeUse()
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetRegenChargeUse)}",
                Indent: indent + 1, Toggle: getDoDebug('x'));
            
            int output = 0;

            if (Hitpoints != null)
            {
                output = 10 * CalculateBaseChargeUse() * GetRegenAmount(Max: true);
            }

            Debug.LastIndent = indent;
            return output;
        }

        public int GetRestoreChargeUse()
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetRestoreChargeUse)}",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            int output = 0;

            if (Hitpoints != null)
            {
                output = CalculateBaseChargeUse() * Hitpoints.BaseValue;
            }

            Debug.LastIndent = indent;
            return output;
        }

        public bool HaveChargeToRegen(int LessAmount = 0, int MultiplyBy = 1)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, 
                $"{nameof(HaveChargeToRegen)}(" +
                $"{nameof(LessAmount)}: {LessAmount}, " +
                $"{nameof(MultiplyBy)}: {MultiplyBy})",
                Indent: indent + 1, Toggle: getDoDebug());

            if (ParentObject != null && Hitpoints != null && GetRegenChargeUse() > 0)
            {
                return (GetRegenChargeUse() * Math.Max(1, MultiplyBy)) < (ParentObject.QueryCharge() - LessAmount);
            }

            Debug.LastIndent = indent;
            return false;
        }
        public bool HaveChargeToRestore(int LessAmount = 0, int MultiplyBy = 1)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, 
                $"{nameof(HaveChargeToRestore)}(" +
                $"{nameof(LessAmount)}: {LessAmount}, " +
                $"{nameof(MultiplyBy)}: {MultiplyBy})",
                Indent: indent + 1, Toggle: getDoDebug());

            bool output = false;

            if (ParentObject != null && Hitpoints != null && GetRestoreChargeUse() > 0)
            {
                output = (GetRestoreChargeUse() * Math.Max(1, MultiplyBy)) < (ParentObject.QueryCharge() - LessAmount);
            }

            Debug.LastIndent = indent;
            return output;
        }

        public static int GetRegenAmount(Statistic Hitpoints, float RegenFactor, bool ForceMax = false)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetRegenAmount)} [static]",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            int amount = 0;

            if (Hitpoints != null)
            {
                amount = Math.Max(1, (int)Math.Ceiling(Hitpoints.BaseValue * RegenFactor));
                amount = ForceMax ? amount : Math.Min(Hitpoints.Penalty, amount);
            }

            Debug.LastIndent = indent;
            return amount;
        }
        public int GetRegenAmount(bool Max = false)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(GetRegenAmount)} [instance]",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            int output = GetRegenAmount(Hitpoints, RegenFactor, Max);

            Debug.LastIndent = indent;
            return output;
        }

        public bool Regenerate(out int RegenAmount, bool Silent = false)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(2, 
                $"* {nameof(Regenerate)}(out {nameof(RegenAmount)}, {nameof(Silent)}: {Silent})",
                Indent: indent + 1, Toggle: getDoDebug());

            RegenAmount = 0;
            bool didRegen = false;

            Debug.Entry(4, $"{nameof(Hitpoints)}.{nameof(Hitpoints.Value)}: {Hitpoints.Value}",
                Indent: indent + 2, Toggle: getDoDebug());

            Debug.Entry(4, $"{nameof(Hitpoints)}.{nameof(Hitpoints.BaseValue)}: {Hitpoints.BaseValue}",
                Indent: indent + 2, Toggle: getDoDebug());

            Debug.Entry(4, $"{nameof(HitpointPercent)}: {HitpointPercent}% ( / 0.25f = {(int)Math.Ceiling(HitpointPercent / 0.25f)})",
                Indent: indent + 2, Toggle: getDoDebug());

            if (ParentObject != null && HaveChargeToRegen(MultiplyBy: RegenRolls) && IsReady(UseCharge: true, MultipleCharge: RegenRolls) && isDamaged)
            {
                CumulativeChargeUse += (ChargeUse * RegenRolls);

                int regenMax = RegenDie.Max();
                int testRoll = 0;
                int roll = -1;
                bool byChance = false;

                Debug.Entry(3, $"Rolling with {RegenRolls - 1}x Advantage for whether to regen or not...",
                    Indent: indent + 2, Toggle: getDoDebug());

                int regenMaxPadding = regenMax.ToString().Length;
                string rollString = "";
                
                for (int i = 0; i < RegenRolls; i++)
                {
                    testRoll = RegenDie.Resolve();
                    rollString = testRoll.ToString().PadLeft(regenMaxPadding, ' ');
                    Debug.LoopItem(4, $"{i}] {nameof(roll)}: ({rollString}/{regenMax})", Indent: indent + 2, Toggle: getDoDebug());
                    if (!byChance)
                    {
                        byChance = testRoll == regenMax;
                        if (byChance)
                        {
                            roll = testRoll;
                            Debug.CheckYeh(4, $"Ding!", Indent: indent + 3, Toggle: getDoDebug());
                        }
                    }
                }
                if (roll == -1)
                {
                    roll = testRoll;
                }
                rollString = roll.ToString().PadLeft(regenMaxPadding, ' ');

                RegenAmount = GetRegenAmount();
                if (byChance && RegenAmount > 0)
                {
                    string equipped = Equipper != null ? "equipped " : "";
                    string message = $"=object.T's= {equipped}{ParentObject.BaseDisplayName}'s {GetDynamicModName(LowerCase: true)} =verb:regenerate= {RegenAmount} HP!";
                    message = GameText.VariableReplace(message, Subject: ParentObject, Object: Holder);

                    Debug.Entry(3,
                        $"({rollString}/{regenMax})" +
                        $" {message}",
                        Indent: indent + 2, Toggle: getDoDebug());

                    didRegen = ParentObject.Heal(RegenAmount) > 0;
                    if (didRegen)
                    {
                        ParentObject.UseCharge(GetRegenChargeUse());
                        CumulativeChargeUse += GetRegenChargeUse();

                        CumulativeRegen += RegenAmount;

                        AddPlayerMessage(message);

                        string fullyRegenMessage = $"=object.T's= {equipped}{ParentObject.BaseDisplayName}'s {GetDynamicModName(LowerCase: true)} have regenerated =subject.objective= fully!";

                        if (!isDamaged)
                        {
                            if (ShouldPopupRegen(Silent) && Holder.IsPlayer())
                            {
                                Popup.Show(GameText.VariableReplace(fullyRegenMessage, Subject: ParentObject, Object: Holder));
                            }
                            else
                            {
                                AddPlayerMessage(fullyRegenMessage);
                            }
                        }
                    }
                }
                else
                {
                    Debug.Entry(3, 
                        $"({rollString}/{regenMax})" +
                        $" {ParentObject?.DebugName ?? NULL}' {GetDynamicModName(LowerCase: true)}" +
                        $" remained innactive!", 
                        Indent: indent + 2, Toggle: getDoDebug());
                }
            }

            Debug.LastIndent = indent;
            return didRegen;
        }
        public bool Regenerate(bool Silent = false)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(Regenerate)}({nameof(Silent)}: {Silent})",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            bool didRegen = Regenerate(out _, Silent);

            Debug.LastIndent = indent;
            return didRegen;
        }

        public bool Restore(out string Condition, bool Silent = false)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(2, 
                $"* {nameof(Restore)}(out {nameof(Condition)}, {nameof(Silent)}: {Silent})",
                Indent: indent + 1, Toggle: getDoDebug());

            Condition = null;
            bool didRestore = false;

            Debug.LoopItem(4, $"{nameof(wantsRestore)}: {wantsRestore}",
                Good: wantsRestore, Indent: indent + 2, Toggle: getDoDebug());

            Debug.LoopItem(4, $"{nameof(isShattered)}: {isShattered}",
                Good: isShattered, Indent: indent + 3, Toggle: getDoDebug());

            Debug.LoopItem(4, $"{nameof(isRusted)}: {isRusted}",
                Good: isRusted, Indent: indent + 3, Toggle: getDoDebug());

            Debug.LoopItem(4, $"{nameof(isBusted)}: {isBusted}",
                Good: isBusted, Indent: indent + 3, Toggle: getDoDebug());

            if (ParentObject != null && wantsRestore && IsReady(UseCharge: true) && HaveChargeToRestore())
            {
                CumulativeChargeUse += ChargeUse;
                int regenMax = RestoreDie.Max();
                int regenMaxPadding = regenMax.ToString().Length;
                int roll = RestoreDie.Resolve();
                string rollString = roll.ToString().PadLeft(regenMaxPadding, ' ');
                bool byChance = roll == RestoreDie.Max();
                if (byChance)
                {
                    string equipped = Equipper != null ? "equipped " : "";

                    ShatteredArmor shattered = ParentObject?.GetEffect<ShatteredArmor>();
                    Rusted rusted = ParentObject?.GetEffect<Rusted>();
                    Broken busted = ParentObject?.GetEffect<Broken>();
                    Condition = shattered?.DisplayName ?? rusted?.DisplayName ?? busted?.DisplayName;

                    Debug.Entry(3, $"{nameof(Condition)}: {Condition}",
                        Indent: indent + 2, Toggle: getDoDebug());

                    string message = $"=object.T's= {equipped}{ParentObject.BaseDisplayName}'s {GetDynamicModName(LowerCase: true)} restored =subject.objective= from being {Condition}!";

                    message = GameText.VariableReplace(message, Subject: ParentObject, Object: Holder);

                    int existingPenalty = Math.Min(Hitpoints.Penalty, (int)Math.Floor(Hitpoints.BaseValue * 0.75f));

                    Debug.Entry(3, $"{nameof(existingPenalty)} for {nameof(Hitpoints)} set to {existingPenalty}",
                        Indent: indent + 2, Toggle: getDoDebug());

                    RepairedEvent.Send(ParentObject, ParentObject, ParentObject);
                    Hitpoints.Penalty = existingPenalty;

                    Debug.Entry(3, $"{nameof(Hitpoints)}.{nameof(Hitpoints.Penalty)} set to {nameof(existingPenalty)} ({existingPenalty})",
                        Indent: indent + 2, Toggle: getDoDebug());

                    Debug.Entry(4,
                        $"({rollString}/{regenMax})" +
                        $" {message}",
                        Indent: indent + 2, Toggle: getDoDebug());

                    didRestore = !wantsRestore; // wantsRestore returns true if the object has any of the above 3 conditions
                    if (didRestore)
                    {
                        if (ParentObject.UseCharge(GetRestoreChargeUse()))
                        {
                            CumulativeChargeUse += GetRestoreChargeUse();
                            TimesRestored++;
                        }

                        if (ShouldPopupRestore(Silent) && Holder.IsPlayer())
                        {
                            Popup.Show(GameText.VariableReplace(message, Subject: ParentObject, Object: Holder));
                        }
                        else
                        {
                            AddPlayerMessage(message);
                        }
                    }
                }
                else
                {
                    Debug.Entry(4, 
                        $"({rollString}/{regenMax})" +
                        $" {ParentObject?.DebugName ?? NULL}' {GetDynamicModName(LowerCase: true)}" +
                        $" remained innactive!", 
                        Indent: indent + 2, Toggle: getDoDebug());
                }
            }

            Debug.LastIndent = indent;
            return didRestore;
        }
        public bool Restore(bool Silent = false)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(Restore)}()",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            bool didRestore = Restore(out _, Silent);

            Debug.LastIndent = indent;
            return didRestore;
        }

        public static bool ShouldPopupRegen(GameObject Item, bool IsEquipped, bool IsInInventory, bool IsImportant, bool Silent = false)
        {
            if (Item == null || Silent || !Options.EnableRegenPopups)
            {
                return false;
            }
            if (IsImportant && Options.EnableRegenPopupsForImportant)
            {
                return true;
            }
            if (IsInInventory && !Options.EnableRegenPopupsForInventory)
            {
                return false;
            }
            if (IsEquipped && !Options.EnableRegenPopupsForEquipped)
            {
                return false;
            }
            return true;
        }
        public bool ShouldPopupRegen(bool IsEquipped, bool IsInInventory, bool IsImportant, bool Silent = false)
        {
            return ShouldPopupRegen(ParentObject, IsEquipped, IsInInventory, IsImportant, Silent);
        }
        public bool ShouldPopupRegen(bool Silent = false)
        {
            return ShouldPopupRegen(ParentObject, isEquipped, isInInventory, isImportant, Silent);
        }

        public static bool ShouldPopupRestore(GameObject Item, bool IsEquipped, bool IsInInventory, bool IsImportant, bool Silent = false)
        {
            if (Item == null || Silent || !Options.EnableRestorePopups)
            {
                return false;
            }
            if (IsImportant && Options.EnableRestorePopupsForImportant)
            {
                return true;
            }
            if (IsInInventory && !Options.EnableRestorePopupsForInventory)
            {
                return false;
            }
            if (IsEquipped && !Options.EnableRestorePopupsForEquipped)
            {
                return false;
            }
            return true;
        }
        public bool ShouldPopupRestore(bool IsEquipped, bool IsInInventory, bool IsImportant, bool Silent = false)
        {
            return ShouldPopupRestore(ParentObject, IsEquipped, IsInInventory, IsImportant, Silent);
        }
        public bool ShouldPopupRestore(bool Silent = false)
        {
            return ShouldPopupRestore(ParentObject, isEquipped, isInInventory, isImportant, Silent);
        }

        private List<string> StringyRegenEventIDs => new()
        {
            "UD_JostleObjectEvent",
            "UD_GetJostleActivityEvent",
        };
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(Register)}({nameof(Object)}: {Object?.DebugName ?? NULL})",
                Indent: indent, Toggle: getDoDebug('x'));

            Registrar.Register(EndTurnEvent.ID, EventOrder.EXTREMELY_EARLY);
            Registrar.Register(ModificationAppliedEvent.ID, EventOrder.LATE);
            Registrar.Register(LateBeforeApplyDamageEvent.ID, EventOrder.EXTREMELY_LATE);
            if (!StringyRegenEventIDs.IsNullOrEmpty())
            {
                foreach (string eventID in StringyRegenEventIDs)
                {
                    Registrar.Register(eventID);
                }
            }

            Debug.LastIndent = indent;
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == GetItemElementsEvent.ID
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID
                || ID == GetDebugInternalsEvent.ID;
        }
        public override bool HandleEvent(EndTurnEvent E)
        {
            if (ParentObject != null && Holder != null && Holder.CurrentZone == The.ActiveZone && !ParentObject.IsInGraveyard())
            {
                Debug.Entry(2,
                    $"@ {nameof(Mod_UD_RegenNanobots)}."
                    + $"{nameof(HandleEvent)}("
                    + $"{nameof(EndTurnEvent)} E) "
                    + $"Item: {ParentObject?.DebugName ?? NULL}",
                    Indent: 0, Toggle: getDoDebug());

                Restore();
                Regenerate();

            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(ModificationAppliedEvent E)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(2, 
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(HandleEvent)}(" +
                $"{nameof(ModificationAppliedEvent)} E." +
                $"{nameof(E.Object)}: {E.Object?.DebugName ?? NULL})",
                Indent: indent + 1, Toggle: getDoDebug());

            if (E.Object == ParentObject)
            {
                TierConfigure();
                if (E.Object.TryGetPart(out EnergyCell energyCell))
                {
                    Debug.Entry(3, $"{nameof(E.Object)} is an {nameof(EnergyCell)}",
                        Indent: indent + 2, Toggle: getDoDebug());

                    int baseChargeRate = (int)(ChargeUse * 1.5);
                    int combinedChargeRate = baseChargeRate;

                    Debug.LoopItem(4, $"{nameof(baseChargeRate)}: {baseChargeRate}",
                        Indent: indent + 3, Toggle: getDoDebug());

                    Debug.LoopItem(4, $"{nameof(combinedChargeRate)}: {combinedChargeRate}",
                        Indent: indent + 3, Toggle: getDoDebug());

                    Debug.Entry(2, $"Requiring {nameof(ZeroPointEnergyCollector)}...",
                        Indent: indent + 2, Toggle: getDoDebug());

                    ZeroPointEnergyCollector zPECollector = E.Object.RequirePart<ZeroPointEnergyCollector>();
                    zPECollector.ChargeRate = baseChargeRate;
                    zPECollector.World = "*";
                    zPECollector.IsBootSensitive = false;
                    zPECollector.IsPowerSwitchSensitive = false;
                    zPECollector.IsBreakageSensitive = false;
                    zPECollector.IsRustSensitive = false;
                    zPECollector.WorksOnSelf = true;

                    Debug.Entry(2, $"{nameof(zPECollector)} {nameof(zPECollector.ChargeRate)} set to {nameof(baseChargeRate)} ({zPECollector.ChargeRate})...",
                        Indent: indent + 2, Toggle: getDoDebug());

                    Debug.Entry(2, $"Checking for {nameof(BroadcastPowerReceiver)}...",
                        Indent: indent + 3, Toggle: getDoDebug());
                    if (E.Object.TryGetPart(out BroadcastPowerReceiver broadcastPowerReceiver))
                    {
                        combinedChargeRate += broadcastPowerReceiver.ChargeRate;
                        Debug.CheckYeh(3, 
                            $"{nameof(broadcastPowerReceiver)}.{nameof(broadcastPowerReceiver.ChargeRate)} ({broadcastPowerReceiver.ChargeRate}) " +
                            $"added to {nameof(combinedChargeRate)} ({combinedChargeRate})",
                            Indent: indent + 3, Toggle: getDoDebug());
                    }
                    else
                    {
                        Debug.CheckNah(3, $"No {nameof(BroadcastPowerReceiver)}",
                            Indent: indent + 3, Toggle: getDoDebug());
                    }

                    Debug.Entry(2, $"Checking for {nameof(SolarArray)}...",
                        Indent: indent + 3, Toggle: getDoDebug());
                    if (E.Object.TryGetPart(out SolarArray solarArray))
                    {
                        combinedChargeRate += solarArray.ChargeRate;
                        Debug.CheckYeh(3,
                            $"{nameof(solarArray)}.{nameof(solarArray.ChargeRate)} ({solarArray.ChargeRate}) " +
                            $"added to {nameof(combinedChargeRate)} ({combinedChargeRate})",
                            Indent: indent + 3, Toggle: getDoDebug());
                    }
                    else
                    {
                        Debug.CheckNah(3, $"No {nameof(SolarArray)}",
                            Indent: indent + 3, Toggle: getDoDebug());
                    }

                    Debug.Entry(2, $"Requiring {nameof(IntegralRecharger)}...",
                        Indent: indent + 2, Toggle: getDoDebug());
                    IntegralRecharger integralRecharger = E.Object.RequirePart<IntegralRecharger>();
                    if (integralRecharger.ChargeRate < combinedChargeRate)
                    {
                        integralRecharger.ChargeRate = combinedChargeRate;
                        Debug.CheckYeh(3,
                            $"{nameof(integralRecharger)}.{nameof(integralRecharger.ChargeRate)} boosted to {nameof(combinedChargeRate)}",
                            Indent: indent + 3, Toggle: getDoDebug());
                    }
                    if (energyCell.ChargeRate < combinedChargeRate)
                    {
                        energyCell.ChargeRate = combinedChargeRate;
                        Debug.CheckYeh(3,
                            $"{nameof(energyCell)}.{nameof(energyCell.ChargeRate)} boosted to {nameof(combinedChargeRate)}",
                            Indent: indent + 3, Toggle: getDoDebug());
                    }
                }
            }
            Debug.LastIndent = indent;
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetItemElementsEvent E)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(HandleEvent)}({nameof(GetItemElementsEvent)})",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            if (E.IsRelevantObject(ParentObject))
            {
                E.Add("circuitry", 10);
                E.Add("scholarship", 2);
            }
            Debug.LastIndent = indent;
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(HandleEvent)}({nameof(GetDisplayNameEvent)})",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            if (E.Understood() && !E.Object.HasProperName)
            {
                E.AddWithClause(GetDynamicModName(LowerCase: true));
            }
            Debug.LastIndent = indent;
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, $"{nameof(Mod_UD_RegenNanobots)}.{nameof(HandleEvent)}({nameof(GetShortDescriptionEvent)})",
                Indent: indent + 1, Toggle: getDoDebug('x'));

            E.Postfix.AppendRules(GetInstanceDescription());

            if (DebugRegenNanobotsModDescriptions)
            {
                StringBuilder SB = Event.NewStringBuilder();

                string equipmentFrame = ParentObject.GetPropertyOrTag("EquipmentFrameColors");

                if (equipmentFrame.IsNullOrEmpty())
                {
                    equipmentFrame = "none";
                }
                else
                {
                    string coloredEquipmentFrame = "{{y|";
                    foreach (char c in equipmentFrame)
                    {
                        coloredEquipmentFrame += $"&{c}{c}";
                    }
                    equipmentFrame = coloredEquipmentFrame += "}}"; 
                }

                bool haveChargeToRegen = HaveChargeToRegen();
                bool haveChargeToRestore = HaveChargeToRestore();

                int complexity = Examiner != null ? Examiner.Complexity : 0;

                SB.AppendColored("M", Grammar.MakeTitleCase(MOD_NAME)).Append(": ");
                SB.AppendLine();

                SB.AppendColored("W", $"State")
                    .AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("o", $"{RegenDie}")
                    .Append($"){HONLY}{nameof(RegenDie)}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("o", $"{RestoreDie}")
                    .Append($"){HONLY}{nameof(RestoreDie)}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("g", $"{GetRegenAmount(Max: true)}")
                    .Append($"){HONLY}{nameof(GetRegenAmount)}()");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("g", $"{HitpointPercent}")
                    .Append($"){HONLY}{nameof(HitpointPercent)}()");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("C", $"{RegenRolls}")
                    .Append($"){HONLY}{nameof(RegenRolls)}()");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("W", $"{ParentObject.QueryCharge()}")
                    .Append($"){HONLY}{nameof(ParentObject.QueryCharge)}()");
                SB.AppendLine();
                SB.Append(VONLY).Append(VANDR).Append("(")
                    .AppendColored("W", $"{Tier}").Append(" * ")
                    .Append("(")
                    .AppendColored("W", $"{BaseChargeMultiplier}").Append(" + ")
                    .AppendColored("W", $"{ObjectTechTier}").Append(" + ")
                    .AppendColored("W", $"{complexity}")
                    .Append(")")
                    .Append($"){HONLY}Charge Figures");
                SB.AppendLine();
                if (ParentObject.TryGetPart(out SolarArray solarArray))
                {
                    SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{solarArray.ChargeRate}")
                        .Append($"){HONLY}{nameof(SolarArray)}");
                    SB.AppendLine();
                }
                if (ParentObject.TryGetPart(out BroadcastPowerReceiver broadcastPowerReceiver))
                {
                    SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{broadcastPowerReceiver.ChargeRate}")
                        .Append($"){HONLY}{nameof(BroadcastPowerReceiver)}");
                    SB.AppendLine();
                }
                if (ParentObject.TryGetPart(out ZeroPointEnergyCollector zPECollector))
                {
                    SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{zPECollector.ChargeRate}")
                        .Append($"){HONLY}{nameof(ZeroPointEnergyCollector)}");
                    SB.AppendLine();
                }
                if (ParentObject.TryGetPart(out EnergyCell energyCell))
                {
                    SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{energyCell.ChargeRate}")
                        .Append($"){HONLY}{nameof(EnergyCell)}");
                    SB.AppendLine();
                }
                if (ParentObject.TryGetPart(out IntegralRecharger integralRecharger))
                {
                    SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{integralRecharger.ChargeRate}")
                        .Append($"){HONLY}{nameof(IntegralRecharger)}");
                    SB.AppendLine();
                }
                SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{ChargeUse}")
                    .Append($"){HONLY}{nameof(ChargeUse)}");
                SB.AppendLine();
                SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{GetRegenChargeUse()}")
                    .Append($"){HONLY}{nameof(GetRegenChargeUse)}()");
                SB.AppendLine();
                SB.Append(VONLY).Append(TANDR).Append("(").AppendColored("W", $"{GetRestoreChargeUse()}")
                    .Append($"){HONLY}{nameof(GetRestoreChargeUse)}()");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("g", $"{CumulativeRegen}")
                    .Append($"){HONLY}{nameof(CumulativeRegen)}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("g", $"{TimesRestored}")
                    .Append($"){HONLY}{nameof(TimesRestored)}");
                SB.AppendLine();
                SB.Append(TANDR).Append("(").AppendColored("y", $"{equipmentFrame}")
                    .Append($"){HONLY}EquipmentFrameColors");
                SB.AppendLine();

                SB.AppendColored("W", $"Bools")
                    .AppendLine();
                SB.Append(VANDR).Append($"[{isDamaged.YehNah()}]{HONLY}{nameof(isDamaged)}: ")
                    .AppendColored("B", $"{isDamaged}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{wantsRestore.YehNah()}]{HONLY}{nameof(wantsRestore)}: ")
                    .AppendColored("B", $"{wantsRestore}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isShattered.YehNah()}]{HONLY}{nameof(isShattered)}: ")
                    .AppendColored("B", $"{isShattered}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isRusted.YehNah()}]{HONLY}{nameof(isRusted)}: ")
                    .AppendColored("B", $"{isRusted}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isBusted.YehNah()}]{HONLY}{nameof(isBusted)}: ")
                    .AppendColored("B", $"{isBusted}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{haveChargeToRegen.YehNah()}]{HONLY}{nameof(HaveChargeToRegen)}(): ")
                    .AppendColored("B", $"{haveChargeToRegen}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{haveChargeToRestore.YehNah()}]{HONLY}{nameof(HaveChargeToRestore)}(): ")
                    .AppendColored("B", $"{haveChargeToRestore}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isHeld.YehNah()}]{HONLY}{nameof(isHeld)}: ")
                    .AppendColored("B", $"{isHeld}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isEquipped.YehNah()}]{HONLY}{nameof(isEquipped)}: ")
                    .AppendColored("B", $"{isEquipped}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isInInventory.YehNah()}]{HONLY}{nameof(isInInventory)}: ")
                    .AppendColored("B", $"{isInInventory}");
                SB.AppendLine();
                SB.Append(TANDR).Append($"[{isImportant.YehNah()}]{HONLY}{nameof(isImportant)}: ")
                    .AppendColored("B", $"{isImportant}");
                SB.AppendLine();

                E.Infix.AppendLine().AppendRules(Event.FinalizeString(SB));
            }

            Debug.LastIndent = indent;
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetDebugInternalsEvent E)
        {
            int complexity = Examiner != null ? Examiner.Complexity : 0;

            string equipmentFrame = ParentObject.GetPropertyOrTag("EquipmentFrameColors", "none");

            E.AddEntry(this, $"{nameof(RegenDie)}", $"{RegenDie}");
            E.AddEntry(this, $"{nameof(RestoreDie)}", $"{RestoreDie}");
            E.AddEntry(this, $"{nameof(GetRegenAmount)}", $"{GetRegenAmount(Max: true)}");
            E.AddEntry(this, $"{nameof(HitpointPercent)}", $"{HitpointPercent}");
            E.AddEntry(this, $"{nameof(RegenRolls)}", $"{RegenRolls}");
            E.AddEntry(this, $"{nameof(isDamaged)}", $"{isDamaged}");
            E.AddEntry(this, $"{nameof(wantsRestore)}", $"{wantsRestore}");
            E.AddEntry(this, $"{nameof(isShattered)}", $"{isShattered}");
            E.AddEntry(this, $"{nameof(isRusted)}", $"{isRusted}");
            E.AddEntry(this, $"{nameof(isBusted)}", $"{isBusted}");
            E.AddEntry(this, $"{nameof(Tier)}", $"{Tier}");
            E.AddEntry(this, $"{nameof(BaseChargeMultiplier)}", $"{BaseChargeMultiplier}");
            E.AddEntry(this, $"{nameof(ObjectTechTier)}", $"{ObjectTechTier}");
            E.AddEntry(this, $"{nameof(complexity)}", $"{complexity}");
            E.AddEntry(this, $"Charge Calcs", $"{Tier} * ({BaseChargeMultiplier} + {ObjectTechTier} + {complexity})");
            E.AddEntry(this, $"{nameof(ParentObject.QueryCharge)}", $"{ParentObject.QueryCharge()}");
            if (ParentObject.TryGetPart(out SolarArray solarArray))
            {
                E.AddEntry(this, $"{nameof(SolarArray)} {nameof(solarArray.ChargeRate)}", $"{solarArray.ChargeRate}");
            }
            if (ParentObject.TryGetPart(out BroadcastPowerReceiver broadcastPowerReceiver))
            {
                E.AddEntry(this, $"{nameof(BroadcastPowerReceiver)} {nameof(broadcastPowerReceiver.ChargeRate)}", $"{broadcastPowerReceiver.ChargeRate}");
            }
            if (ParentObject.TryGetPart(out ZeroPointEnergyCollector zPECollector))
            {
                E.AddEntry(this, $"{nameof(ZeroPointEnergyCollector)} {nameof(zPECollector.ChargeRate)}", $"{zPECollector.ChargeRate}");
            }
            if (ParentObject.TryGetPart(out EnergyCell energyCell))
            {
                E.AddEntry(this, $"{nameof(EnergyCell)} {nameof(energyCell.ChargeRate)}", $"{energyCell.ChargeRate}");
            }
            if (ParentObject.TryGetPart(out IntegralRecharger integralRecharger))
            {
                E.AddEntry(this, $"{nameof(IntegralRecharger)} {nameof(integralRecharger.ChargeRate)}", $"{integralRecharger.ChargeRate}");
            }
            E.AddEntry(this, $"{nameof(ChargeUse)}", $"{ChargeUse}");
            E.AddEntry(this, $"{nameof(GetRegenChargeUse)}", $"{GetRegenChargeUse()}");
            E.AddEntry(this, $"{nameof(HaveChargeToRegen)}", $"{HaveChargeToRegen()}");
            E.AddEntry(this, $"{nameof(GetRestoreChargeUse)}", $"{GetRestoreChargeUse()}");
            E.AddEntry(this, $"{nameof(HaveChargeToRestore)}", $"{HaveChargeToRestore()}");
            E.AddEntry(this, $"{nameof(CumulativeRegen)}", $"{CumulativeRegen}");
            E.AddEntry(this, $"{nameof(TimesRestored)}", $"{TimesRestored}");
            E.AddEntry(this, $"{nameof(equipmentFrame)}", $"{equipmentFrame.Quote()}");
            E.AddEntry(this, $"{nameof(isHeld)}", $"{isHeld}");
            E.AddEntry(this, $"{nameof(isEquipped)}", $"{isEquipped}");
            E.AddEntry(this, $"{nameof(isInInventory)}", $"{isInInventory}");
            E.AddEntry(this, $"{nameof(isImportant)}", $"{isImportant}");
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(LateBeforeApplyDamageEvent E)
        {
            if (E.Object != null && E.Object == ParentObject && E.Damage.Attributes.Contains("Jostle") && isBusted && IsReady(UseCharge: true))
            {
                int indent = Debug.LastIndent;
                Debug.Entry(3,
                    $"@ {nameof(Mod_UD_RegenNanobots)}."
                    + $"{nameof(HandleEvent)}({nameof(LateBeforeApplyDamageEvent)} E)",
                    Indent: indent, Toggle: getDoDebug());

                string equipped = Equipper != null ? "equipped " : "";
                string jostled = "{{utilitape|jostled}}";
                string message = $"=object.T's= {equipped}{ParentObject.BaseDisplayName}'s {GetDynamicModName(LowerCase: true)} kept =subject.objective= from taking {E.Damage.Amount} damage after being {jostled}!";

                message = GameText.VariableReplace(message, Subject: ParentObject, Object: Holder);

                if (Holder.IsPlayer())
                {
                    Popup.Show(message);
                }
                else
                {
                    AddPlayerMessage(message);
                }

                Debug.Entry(4, message, Indent: indent + 1, Toggle: getDoDebug());

                E.Damage = new(0);

                Debug.LastIndent = indent;
                return false;
            }
            return base.HandleEvent(E);
        }
        public override bool FireEvent(Event E)
        {
            if (!StringyRegenEventIDs.IsNullOrEmpty() && StringyRegenEventIDs.Contains(E.ID))
            {
                int indent = Debug.LastIndent;
                Debug.Entry(2, 
                    $"@ {nameof(Mod_UD_RegenNanobots)}." 
                    + $"{nameof(FireEvent)}({nameof(Event)} " 
                    + $"E.ID: {E.ID})",
                    Indent: indent + 1, Toggle: getDoDebug());

                if (E.ID == "UD_JostleObjectEvent" && isBusted && IsReady(UseCharge: true))
                {
                    Debug.Entry(3, $"Used charge while busted and jostled", Indent: indent + 2, Toggle: getDoDebug());
                }
                if (E.ID == "UD_GetJostleActivityEvent" 
                    && E.GetParameter("FromEvent") is MinEvent fromEvent
                    && ParentObject.HasPart<EnergyCell>()
                    && (fromEvent.GetType() == typeof(ChargeUsedEvent) || fromEvent.GetType() == typeof(UseChargeEvent)))
                {
                    if (fromEvent is ChargeUsedEvent chargeUsedEvent && chargeUsedEvent.Amount == GetRegenChargeUse()
                        || fromEvent is UseChargeEvent useChargeEvent && useChargeEvent.Amount == GetRegenChargeUse())
                    {
                        E.SetParameter("Activity", 0);
                        Debug.Entry(3, 
                            $"Blocked {nameof(EnergyCell)} from being jostled when using its own charge to regenerate", 
                            Indent: indent + 2, Toggle: getDoDebug());
                    }
                }

                Debug.LastIndent = indent;
            }
            return base.FireEvent(E);
        }
    }
}
