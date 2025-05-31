using System;
using System.Collections.Generic;
using System.Text;

using XRL.UI;
using XRL.Rules;
using XRL.Language;
using XRL.World;
using XRL.World.Effects;
using XRL.World.Tinkering;
using XRL.World.Capabilities;

using UD_RegenNanobots_Mod;
using static UD_RegenNanobots_Mod.Const;
using static UD_RegenNanobots_Mod.Utils;
using Debug = UD_RegenNanobots_Mod.Debug;
using Options = UD_RegenNanobots_Mod.Options;

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
            };

            if (what != null && doList.Contains(what))
                return true;

            if (what != null && dontList.Contains(what))
                return false;

            return doDebug;
        }

        private static bool DebugDuctapeModDescriptions => Options.DebugRegenNanobotsModDescriptions;

        private Statistic Hitpoints => ParentObject?.GetStat(nameof(Hitpoints));

        public Examiner Examiner => ParentObject?.GetPart<Examiner>();

        private GameObject Equipper => ParentObject?.Equipped;

        private GameObject Holder => ParentObject?.Holder;

        public static readonly string EQ_FRAME_COLORS = "RWGY";

        public static readonly string REGENERATIVE = "regenerative";

        public static readonly string NANOBOTS = "nanobots";

        public static readonly string MOD_NAME = $"{REGENERATIVE} {NANOBOTS}";

        public static readonly string MOD_NAME_COLORED = "{{regenerative|" + REGENERATIVE + "}} {{nanobots|" + NANOBOTS + "}}";

        public static DieRoll RegenDie = new($"1d30");

        public static DieRoll RestoreDie = new($"1d120");

        public int ObjectTechTier => ParentObject.GetTechTier();

        public float RegenFactor = 0.01f;

        public int CumulativeRegen = 0;

        public int TimesRestored = 0;

        public bool Regened = false;

        private double StoredTimeTick = 0;

        public bool isDamaged => ParentObject != null && ParentObject.isDamaged();
        public bool isBusted => ParentObject != null && ParentObject.IsBroken();
        public bool isRusted => ParentObject != null && ParentObject.IsRusted();
        public bool isShattered => ParentObject != null && ParentObject.HasEffect<ShatteredArmor>();

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
            WorksOnSelf = true;
            IsBreakageSensitive = false;
            IsRustSensitive = false;
            IsEMPSensitive = true;
            IsPowerLoadSensitive = true;
            IsTechScannable = true;
            NameForStatus = "RegenerativeNanobots";
        }
        public override void TierConfigure()
        {
            ChargeUse = CalculateBaseChargeUse();
            ChargeMinimum = CalculateBaseChargeUse();
        }
        public override bool ModificationApplicable(GameObject Object)
        {
            return CanRegen(Object);
        }
        public static bool CanRegen(GameObject Object, string Context = "")
        {
            if (Context == "Internal")
            {
                Debug.Entry(4,
                    $"{nameof(Mod_UD_RegenNanobots)}." +
                    $"{nameof(CanRegen)}(" +
                    $"{Object?.DebugName ?? NULL})",
                    Indent: Debug.LastIndent, Toggle: doDebug);
            }
            return Object != null && Object.HasStat("Hitpoints");
        }

        public static int CalculateBaseChargeUse(int Tier = 1, int ObjectTechTier = 0, int Complexity = 0)
        {
            int multiplier = 1;
            multiplier += ObjectTechTier;
            multiplier += Complexity;
            return Tier * multiplier;
        }
        public int CalculateBaseChargeUse()
        {
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
            return CalculateBaseChargeUse(Tier, objectTechTier, complexity);
        }

        public static string GetDescription(int Tier)
        {
            return $"{MOD_NAME_COLORED}: while powered, this item will gradually regenerate HP and has a small chance to be restored from being rusted or broken. Higher tier items require more charge to function.";
        }
        public static string GetDescription(GameObject Item, int Tier)
        {
            Statistic hitpoints = Item.GetStat("Hitpoints");
            if (hitpoints != null)
            {
                Mod_UD_RegenNanobots modRegenNanobots = new();
                float regenFactor = modRegenNanobots.RegenFactor;
                int regenAmount = GetRegenAmount(hitpoints, regenFactor, Max: true);

                StringBuilder SB = Event.NewStringBuilder();
                SB.Append(GetDynamicModName(Item, Tier)).Append(": ");
                SB.Append("while powered, this item ");
                SB.Append("has a ").Append(RegenDie.Min()).Append(" in ").Append(RegenDie.Max()).Append(" chance per turn to ");
                SB.Append("regenerate ").Append(regenAmount).Append(" HP and ");
                SB.Append("has a ").Append(RestoreDie.Min()).Append(" in ").Append(RestoreDie.Max()).Append(" chance per turn to ");
                SB.Append("be restored from being rusted or broken. ");
                SB.Append("Higher tier items require more charge to function.");

                return Event.FinalizeString(SB);
            }
            return GetDescription(Tier);
        }
        public string GetInstanceDescription()
        {
            return GetDescription(ParentObject, Tier);
        }
        public static string GetDynamicModName(GameObject Item, int Tier, bool LowerCase = false)
        {
            int indent = Debug.LastIndent;

            string regenerative = LowerCase ? Grammar.MakeLowerCase(REGENERATIVE) : Grammar.MakeTitleCase(REGENERATIVE);
            string nanobots = LowerCase ? Grammar.MakeLowerCase(NANOBOTS) : Grammar.MakeTitleCase(NANOBOTS);
            Statistic Hitpoints = Item.GetStat("Hitpoints");

            int remainingHP = Hitpoints.Value;
            int maxHP = Hitpoints.BaseValue;
            float percentHP = (float)remainingHP / (float)maxHP;
            int breakPoint = (int)Math.Floor(regenerative.Length * percentHP);

            Debug.Entry(4, $"{remainingHP}/{maxHP} = {percentHP}; {nameof(breakPoint)}: {breakPoint}", 
                Indent: indent + 1, Toggle: getDoDebug(nameof(GetDynamicModName)));

            if (breakPoint < regenerative.Length - 1)
            {
                int brightPoint = Math.Max(0, breakPoint - 1);
                int dullPoint = Math.Max(1, Math.Min(breakPoint, regenerative.Length - 1));
                int whitePoint = brightPoint;

                Debug.Entry(4, $"{nameof(brightPoint)}: {brightPoint}",
                    Indent: indent + 2, Toggle: getDoDebug(nameof(GetDynamicModName)));
                Debug.Entry(4, $"{nameof(dullPoint)}: {dullPoint}", 
                    Indent: indent + 2, Toggle: getDoDebug(nameof(GetDynamicModName)));
                Debug.Entry(4, $"{nameof(whitePoint)}: {whitePoint}", 
                    Indent: indent + 2, Toggle: getDoDebug(nameof(GetDynamicModName)));

                string regenBright = brightPoint > 0 ? regenerative[..brightPoint].Color("regenerating") : "";
                string regenDull = regenerative[dullPoint..].Color("greygoo");
                string regenWhite = regenerative.Substring(whitePoint, 1).Color("Y");

                regenerative = regenBright + regenWhite + regenDull;
                nanobots = nanobots.Color("greygoo");
            }
            else
            {
                regenerative = regenerative.Color("regenerative");
                nanobots = nanobots.Color("nanobots");
            }
            Debug.LastIndent = indent;
            return $"{regenerative} {nanobots}";
        }
        public string GetDynamicModName(bool LowerCase = false)
        {
            return GetDynamicModName(ParentObject, Tier, LowerCase);
        }

        public override void Attach()
        {
            ApplyEquipmentFrameColors();
            base.Attach();
        }
        public override void ApplyModification(GameObject Object)
        {
            Debug.Entry(4,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(ApplyModification)}(" +
                $"{Object?.DebugName ?? NULL})",
                Indent: Debug.LastIndent, Toggle: doDebug);

            if (ChargeUse > 0)
            {
                if (!Object.HasPartDescendedFrom<IEnergyCell>())
                {
                    Object.RequirePart<EnergyCellSocket>();
                }
            }
            IncreaseDifficultyAndComplexity(3, 2);
            ApplyEquipmentFrameColors();
        }
        public bool ApplyEquipmentFrameColors()
        {
            Debug.Entry(4,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(ApplyEquipmentFrameColors)}(" +
                $"{ParentObject?.DebugName ?? NULL})",
                Indent: Debug.LastIndent, Toggle: doDebug);

            if (ParentObject != null && !ParentObject.HasTagOrProperty("EquipmentFrameColors"))
            {
                ParentObject.SetEquipmentFrameColors(EQ_FRAME_COLORS);
            }
            return ParentObject.GetPropertyOrTag("EquipmentFrameColors") == EQ_FRAME_COLORS;
        }

        public int GetRegenChargeUse()
        {
            if (Hitpoints == null)
            {
                return 0;
            }
            return 10 * CalculateBaseChargeUse() * GetRegenAmount(Max: true);
        }

        public int GetRestoreChargeUse()
        {
            if (Hitpoints == null)
            {
                return 0;
            }
            return CalculateBaseChargeUse() * Hitpoints.BaseValue;
        }

        public bool HaveChargeToRegen(int LessAmount = 0)
        {
            if (ParentObject != null && Hitpoints != null && GetRegenChargeUse() > 0)
            {
                return GetRegenChargeUse() < (ParentObject.QueryCharge() - LessAmount);
            }
            return false;
        }
        public bool HaveChargeToRestore(int LessAmount = 0)
        {
            if (ParentObject != null && Hitpoints != null && GetRestoreChargeUse() > 0)
            {
                return GetRestoreChargeUse() < (ParentObject.QueryCharge() - LessAmount);
            }
            return false;
        }

        public static int GetRegenAmount(Statistic Hitpoints, float RegenFactor, bool Max = false)
        {
            if (Hitpoints == null) 
                return 0;

            int amount = Math.Max(1, (int)Math.Ceiling(Hitpoints.BaseValue * RegenFactor));
            amount = Max ? amount : Math.Min(Hitpoints.Penalty, amount);
            return amount;
        }
        public int GetRegenAmount(bool Max = false)
        {
            return GetRegenAmount(Hitpoints, RegenFactor, Max);
        }

        public bool Regenerate(out int RegenAmount)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, 
                $"* {nameof(Regenerate)}(out int RegenAmount)",
                Indent: indent, Toggle: doDebug);

            RegenAmount = 0;
            bool didRegen = false;
            if (ParentObject != null && IsReady(UseCharge: true) && isDamaged && !Regened && HaveChargeToRegen())
            {
                int regenMax = RegenDie.Max();
                int regenMaxPadding = regenMax.ToString().Length;
                int roll = RegenDie.Resolve();
                string rollString = roll.ToString().PadLeft(regenMaxPadding, ' ');
                bool byChance = roll == RegenDie.Max();
                RegenAmount = GetRegenAmount();
                if (byChance && RegenAmount > 0)
                {
                    string equipped = Equipper != null ? "equipped " : "";
                    string message = $"=object.T's= {equipped}{ParentObject.BaseDisplayName}'s {Grammar.MakeLowerCase(MOD_NAME_COLORED)} =verb:regenerate= {RegenAmount} HP!";
                    message = GameText.VariableReplace(message, Subject: ParentObject, Object: Holder);

                    Debug.Entry(4,
                        $"({rollString}/{regenMax})" +
                        $" {message}",
                        Indent: indent + 1, Toggle: doDebug);

                    didRegen = ParentObject.Heal(RegenAmount) > 0;
                    if (didRegen)
                    {
                        ParentObject.UseCharge(GetRegenChargeUse());

                        CumulativeRegen += RegenAmount;

                        AddPlayerMessage(message);

                        string fullyRegenMessage = $"=object.T's= {equipped}{ParentObject.BaseDisplayName}'s {Grammar.MakeLowerCase(MOD_NAME_COLORED)} have regenerated {ParentObject.it} fully!";

                        if (!isDamaged)
                        {
                            if (Holder.IsPlayer())
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
                    Debug.Entry(4, 
                        $"({rollString}/{regenMax})" +
                        $" {ParentObject?.DebugName ?? NULL}' {Grammar.MakeLowerCase(MOD_NAME_COLORED)}" +
                        $" remained innactive!", 
                        Indent: indent + 1, Toggle: doDebug);
                }
            }
            Regened = true;

            Debug.LastIndent = indent;

            return didRegen;
        }
        public bool Regenerate()
        {
            return Regenerate(out _);
        }

        public bool Restore(out string Condition)
        {
            int indent = Debug.LastIndent;
            Debug.Entry(4, 
                $"* {nameof(Restore)}(out string Condition)",
                Indent: indent, Toggle: doDebug);

            Condition = null;
            bool didRestore = false;
            if (ParentObject != null && IsReady(UseCharge: true) && wantsRestore && HaveChargeToRestore())
            {
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

                    string message = $"=object.T's= {equipped}{ParentObject?.ShortDisplayNameWithoutTitlesStripped}'s {Grammar.MakeLowerCase(MOD_NAME_COLORED)} restored {ParentObject.it} from being {Condition}!";

                    message = GameText.VariableReplace(message, Subject: ParentObject, Object: Holder);

                    int existingPenalty = Hitpoints.Penalty;
                    RepairedEvent.Send(ParentObject, ParentObject, ParentObject);
                    Hitpoints.Penalty = existingPenalty;

                    Debug.Entry(4,
                        $"({rollString}/{regenMax})" +
                        $" {message}",
                        Indent: indent + 1, Toggle: doDebug
                        );

                    didRestore = !wantsRestore;
                    if (didRestore)
                    {
                        ParentObject.UseCharge(GetRegenChargeUse());
                        TimesRestored++;
                        if (Holder.IsPlayer())
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
                        $" {ParentObject?.DebugName ?? NULL}' {Grammar.MakeLowerCase(MOD_NAME_COLORED)}" +
                        $" remained innactive!", 
                        Indent: indent + 1, Toggle: doDebug);
                }
            }

            Debug.LastIndent = indent;

            return didRestore;
        }
        public bool Restore()
        {
            return Restore(out _);
        }

        public override bool WantTurnTick()
        {
            Debug.Entry(4,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(WantTurnTick)}()",
                Indent: Debug.LastIndent, Toggle: doDebug);

            return true;
        }
        public override void TurnTick(long TimeTick, int Amount)
        {
            bool turnsOverride = TimeTick - StoredTimeTick > 1;

            if (ParentObject != null && Holder != null && Holder.CurrentZone == The.ActiveZone && !ParentObject.IsInGraveyard())
            {
                Debug.Entry(4,
                    $"@ {nameof(Mod_UD_RegenNanobots)}."
                    + $"{nameof(TurnTick)}"
                    + $"(long TimeTick: {TimeTick}, int Amount: {Amount})",
                    Indent: 0, Toggle: doDebug);

                Debug.CheckYeh(4, $"In Zone", Indent: 1, Toggle: doDebug);
                
                if (Regened && turnsOverride)
                {
                    Debug.CheckYeh(4, $"Turns Overriden", Indent: 1, Toggle: doDebug);
                    Regened = false;
                    StoredTimeTick = TimeTick;
                }
                Restore();
                Regenerate();
            }
            base.TurnTick(TimeTick, Amount);
        }
        private List<string> StringyRegenEventIDs => new()
        {
            // "CommandFireMissile",
            // "BeforeThrown",
        };
        private Dictionary<Func<bool>, int> EquipperRegenEventIDs => new()
        {
            { delegate(){ return true; }, EnteredCellEvent.ID },
            { delegate(){ return true; }, GetDefenderHitDiceEvent.ID },
        };
        public override void Register(GameObject Object, IEventRegistrar Registrar)
        {
            if (false && !StringyRegenEventIDs.IsNullOrEmpty())
            {
                foreach (string eventID in StringyRegenEventIDs)
                {
                    Registrar.Register(eventID);
                }
            }
            base.Register(Object, Registrar);
        }
        public override bool WantEvent(int ID, int cascade)
        {
            return base.WantEvent(ID, cascade)
                || ID == ModificationAppliedEvent.ID
                || ID == GetItemElementsEvent.ID
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID;
        }
        public override bool HandleEvent(ModificationAppliedEvent E)
        {
            if (E.Object == ParentObject && E.Object.TryGetPart(out EnergyCell energyCell))
            {
                TierConfigure();
                int baseChargeRate = (int)(ChargeUse * 1.5);
                int combinedChargeRate = baseChargeRate;

                if (!E.Object.TryGetPart(out ZeroPointEnergyCollector zPECollector))
                {
                    zPECollector = E.Object.RequirePart<ZeroPointEnergyCollector>();
                }
                zPECollector.ChargeRate = baseChargeRate;
                zPECollector.World = "*";
                zPECollector.IsBootSensitive = false;
                zPECollector.IsPowerSwitchSensitive = false;
                zPECollector.IsBreakageSensitive = false;
                zPECollector.IsRustSensitive = false;
                zPECollector.WorksOnSelf = true;

                if (E.Object.TryGetPart(out BroadcastPowerReceiver broadcastPowerReceiver))
                {
                    combinedChargeRate += broadcastPowerReceiver.ChargeRate;
                }
                if (E.Object.TryGetPart(out SolarArray solarArray))
                {
                    combinedChargeRate += solarArray.ChargeRate;
                }
                IntegralRecharger integralRecharger = E.Object.RequirePart<IntegralRecharger>();
                if (integralRecharger.ChargeRate != 0 && integralRecharger.ChargeRate < combinedChargeRate)
                {
                    integralRecharger.ChargeRate = combinedChargeRate;
                }
                if (energyCell.ChargeRate < combinedChargeRate)
                {
                    energyCell.ChargeRate = combinedChargeRate;
                }
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetItemElementsEvent E)
        {
            if (E.IsRelevantObject(ParentObject))
            {
                E.Add("circuitry", 10);
                E.Add("scholarship", 2);
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetDisplayNameEvent E)
        {
            if (E.Understood() && !E.Object.HasProperName)
            {
                E.AddWithClause(GetDynamicModName(LowerCase: true));
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            E.Postfix.AppendRules(GetInstanceDescription());

            if (DebugDuctapeModDescriptions)
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
                SB.Append(VANDR).Append($"[{Regened.YehNah(true)}]{HONLY}{nameof(Regened)}: ")
                    .AppendColored("B", $"{Regened}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("o", $"{RegenDie}")
                    .Append($"){HONLY}{nameof(RegenDie)}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("o", $"{RestoreDie}")
                    .Append($"){HONLY}{nameof(RestoreDie)}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("g", $"{GetRegenAmount(Max: true)}")
                    .Append($"){HONLY}{nameof(GetRegenAmount)}()");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("W", $"{ParentObject.QueryCharge()}")
                    .Append($"){HONLY}{nameof(ParentObject.QueryCharge)}()");
                SB.AppendLine();
                SB.Append(VONLY).Append(VANDR).Append("(")
                    .AppendColored("W", $"{Tier}").Append(" * ")
                    .Append("(")
                    .AppendColored("W", $"{1}").Append(" + ")
                    .AppendColored("W", $"{ObjectTechTier}").Append(" + ")
                    .AppendColored("W", $"{complexity}")
                    .Append(")")
                    .Append($"){HONLY}Charge Figures");
                SB.AppendLine();
                if (ParentObject.TryGetPart(out ZeroPointEnergyCollector zPECollector))
                {
                    SB.Append(VONLY).Append(VANDR).Append("(").AppendColored("W", $"{zPECollector.ChargeRate}")
                        .Append($"){HONLY}{nameof(ZeroPointEnergyCollector)}");
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

                SB.AppendColored("W", $"TimeTick")
                    .AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("y", $"{The.Game.TimeTicks}")
                    .Append($"){HONLY}Current{nameof(The.Game.TimeTicks)}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("y", $"{StoredTimeTick}")
                    .Append($"){HONLY}{nameof(StoredTimeTick)}");
                SB.AppendLine();
                SB.Append(TANDR).Append("(").AppendColored("y", $"{The.Game.TimeTicks - StoredTimeTick}")
                    .Append($"){HONLY}Difference");
                SB.AppendLine();

                SB.AppendColored("W", $"Bools")
                    .AppendLine();
                SB.Append(VANDR).Append($"[{isDamaged.YehNah(true)}]{HONLY}{nameof(isDamaged)}: ")
                    .AppendColored("B", $"{isDamaged}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isShattered.YehNah(true)}]{HONLY}{nameof(isShattered)}: ")
                    .AppendColored("B", $"{isShattered}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isRusted.YehNah(true)}]{HONLY}{nameof(isRusted)}: ")
                    .AppendColored("B", $"{isRusted}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isBusted.YehNah(true)}]{HONLY}{nameof(isBusted)}: ")
                    .AppendColored("B", $"{isBusted}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{haveChargeToRegen.YehNah()}]{HONLY}{nameof(HaveChargeToRegen)}(): ")
                    .AppendColored("B", $"{haveChargeToRegen}");
                SB.AppendLine();
                SB.Append(TANDR).Append($"[{haveChargeToRestore.YehNah()}]{HONLY}{nameof(HaveChargeToRestore)}(): ")
                    .AppendColored("B", $"{haveChargeToRestore}");
                SB.AppendLine();

                E.Infix.AppendLine().AppendRules(Event.FinalizeString(SB));
            }

            return base.HandleEvent(E);
        }
        public override bool FireEvent(Event E)
        {
            if (false && !StringyRegenEventIDs.IsNullOrEmpty() && StringyRegenEventIDs.Contains(E.ID))
            {
                
            }
            return base.FireEvent(E);
        }
    }
}
