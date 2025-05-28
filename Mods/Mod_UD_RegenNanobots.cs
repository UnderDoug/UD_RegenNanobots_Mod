using HarmonyLib;

using System;
using System.Collections.Generic;
using System.Text;

using XRL.UI;
using XRL.World;
using XRL.Rules;
using XRL.Language;
using XRL.World.Capabilities;
using XRL.World.Tinkering;

using UD_RegenNanobots_Mod;
using static UD_RegenNanobots_Mod.Const;
using static UD_RegenNanobots_Mod.Utils;

using Debug = UD_RegenNanobots_Mod.Debug;
using Options = UD_RegenNanobots_Mod.Options;
using XRL.World.Effects;

namespace XRL.World.Parts
{
    [Serializable]
    public class Mod_UD_RegenNanobots : IModification
    {
        private static readonly bool doDebug = true;

        private static bool DebugDuctapeModDescriptions => Options.DebugRegenNanobotsModDescriptions;

        private Statistic Hitpoints => ParentObject?.GetStat(nameof(Hitpoints));

        private GameObject Equipper => ParentObject?.Equipped;

        private GameObject Holder => ParentObject?.Holder;

        public static readonly string EQ_FRAME_COLORS = "rRgG";

        public static readonly string MOD_NAME = "Regenerative Nanobots";

        public static readonly string MOD_NAME_COLORED = "{{regenerative|Regenerative}} {{nanobots|Nanobots}}";

        public DieRoll RegenDie = new("1d30");

        public DieRoll RestoreDie = new("1d120");

        public int CumulativeRegen = 0;

        public int TimesRestored = 0;

        public bool Regened = false;

        private double StoredTimeTick = 0;

        public bool isDamaged => ParentObject != null && ParentObject.isDamaged();
        public bool isBusted => ParentObject != null && ParentObject.IsBroken();
        public bool isRusted => ParentObject != null && ParentObject.IsRusted();
        public int ObjectTier => ParentObject.GetTechTier();

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
        public override bool ModificationApplicable(GameObject Object)
        {
            Debug.Entry(4,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(ModificationApplicable)}(" +
                $"{Object?.DebugName ?? NULL})",
                Indent: Debug.LastIndent, Toggle: doDebug);

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
        public bool CanRegen(string Context = "")
        {
            return CanRegen(ParentObject, Context);
        }

        public static string GetDescription()
        {
            return $"{MOD_NAME_COLORED}: while powered, this item will gradually regenerate HP and has a small chance to be restored from being rusted or broken.";
        }
        public override void ApplyModification(GameObject Object)
        {
            Debug.Entry(4,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(ApplyModification)}(" +
                $"{Object?.DebugName ?? NULL})",
                Indent: Debug.LastIndent, Toggle: doDebug);

            if (Object != null)
            {
                Object.RequirePart<EnergyCellSocket>();
                IncreaseDifficultyAndComplexityIfComplex(2, 2);
                ApplyEquipmentFrameColors();
            }
            base.ApplyModification();
        }

        public static bool ApplyEquipmentFrameColors(GameObject Object)
        {
            Debug.Entry(4,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(ApplyEquipmentFrameColors)}(" +
                $"{Object?.DebugName ?? NULL})",
                Indent: Debug.LastIndent, Toggle: doDebug);

            if (Object != null && !Object.HasTagOrProperty("EquipmentFrameColors"))
            {
                Object.SetEquipmentFrameColors(EQ_FRAME_COLORS);
            }
            return Object.GetPropertyOrTag("EquipmentFrameColors") == EQ_FRAME_COLORS;
        }
        public bool ApplyEquipmentFrameColors()
        {
            return ApplyEquipmentFrameColors(ParentObject);
        }

        public static int GetRegenChargeUse(Statistic Hitpoints, int Tier)
        {
            if (Hitpoints == null) return 99999999;
            return GetRegenAmount(Hitpoints) * Tier;
        }
        public int GetRegenChargeUse()
        {
            return GetRegenChargeUse(Hitpoints, Tier);
        }

        public static int GetRestoreChargeUse(Statistic Hitpoints, int Tier)
        {
            if (Hitpoints == null) return 99999999;
            return Hitpoints.BaseValue * Tier;
        }
        public int GetRestoreChargeUse()
        {
            return GetRestoreChargeUse(Hitpoints, Tier);
        }

        public static bool HaveChargeToRegen(GameObject Item, int LessAmount = 0)
        {
            if (Item != null && Item.HasStat(nameof(Hitpoints)))
            {
                Statistic hitpoints = Item.GetStat(nameof(Hitpoints));
                int tier = Item.GetTier();
                return GetRegenChargeUse(hitpoints, tier) < (Item.QueryCharge() - LessAmount);
            }
            return false;
        }
        public  bool HaveChargeToRegen(int LessAmount = 0)
        {
            return HaveChargeToRegen(ParentObject, LessAmount);
        }

        public static bool HaveChargeToRestore(GameObject Item, int LessAmount = 0)
        {
            if (Item != null && Item.HasStat(nameof(Hitpoints)))
            {
                Statistic hitpoints = Item.GetStat(nameof(Hitpoints));
                int tier = Item.GetTier();
                return GetRestoreChargeUse(hitpoints, tier) < (Item.QueryCharge() - LessAmount);
            }
            return false;
        }
        public  bool HaveChargeToRestore(int LessAmount = 0)
        {
            return HaveChargeToRestore(ParentObject, LessAmount);
        }

        public static int GetRegenAmount(Statistic Hitpoints)
        {
            if (Hitpoints == null) 
                return 0;

            int high = (int)Math.Ceiling(Hitpoints.BaseValue * 0.01);
            int low = (int)Math.Floor(Hitpoints.BaseValue * 0.01);

            if (Hitpoints.BaseValue < 100)
            {
                return FiftyFifty() ? high : low;
            }

            return high;
        }
        public int GetRegenAmount()
        {
            return GetRegenAmount(Hitpoints);
        }

        public bool Regenerate(out int RegenAmount, MinEvent FromEvent = null, Event FromSEvent = null)
        {
            int indent = Debug.LastIndent + 1;
            Debug.Entry(4, 
                $"* {nameof(Regenerate)}("
                + $" FromEvent: {FromEvent?.GetType()?.Name ?? NULL},"
                + $" FromSEvent: {FromSEvent?.ID ?? NULL})",
                Indent: indent, Toggle: true);

            RegenAmount = 0;
            if (ParentObject != null && isDamaged)
            {
                int regenMax = RegenDie.Max();
                int regenMaxPadding = regenMax.ToString().Length;
                int roll = RegenDie.Resolve();
                string rollString = roll.ToString().PadLeft(regenMaxPadding, ' ');
                bool byChance = roll == RegenDie.Max();
                RegenAmount = GetRegenAmount();
                if (byChance && RegenAmount > 0)
                {
                    bool isEquipped = Equipper != null;
                    string equipped = isEquipped ? "equipped " : "";
                    string message = $"=object.T's= {equipped}=subject.T's= {Grammar.MakeLowerCase(MOD_NAME_COLORED)} =verb:regenerate= from {RegenAmount} damage!";
                    message = GameText.VariableReplace(message, Subject: ParentObject, Object: Holder);

                    Debug.Entry(4,
                        $"({rollString}/{regenMax})" +
                        $" {message}",
                        Indent: indent + 1, Toggle: true
                        );

                    bool didRegen = ParentObject.Heal(RegenAmount) > 0;
                    if (didRegen)
                    {
                        AddPlayerMessage(message);

                        string fullyRegenMessage = $"=object.T's= {equipped}=subject.T's= {Grammar.MakeLowerCase(MOD_NAME_COLORED)} have fully regenerated =subject.pronoun=!";

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
                    return didRegen;
                }
                else
                {
                    Debug.Entry(4, 
                        $"({rollString}/{regenMax})" +
                        $" {ParentObject?.DebugName ?? NULL}' {Grammar.MakeLowerCase(MOD_NAME_COLORED)}" +
                        $" remained innactive!", 
                        Indent: indent + 1, Toggle: true);
                }
            }
            return false;
        }
        public bool Regenerate(MinEvent FromEvent = null, Event FromSEvent = null)
        {
            return Regenerate(out _, FromEvent, FromSEvent);
        }
        public bool TryRegenerate(out bool DidRegen, out int RegenAmount, MinEvent FromEvent = null, Event FromSEvent = null)
        {
            DidRegen = false;
            RegenAmount = 0;
            if (ParentObject != null)
            {
                DidRegen = true;
                return Regenerate(out RegenAmount, FromEvent, FromSEvent);
            }
            return false;
        }
        public bool TryRegenerate(out int RegenAmount, MinEvent FromEvent = null, Event FromSEvent = null)
        {
            if (TryRegenerate(out _, out RegenAmount, FromEvent, FromSEvent))
            {
                CumulativeRegen += RegenAmount;
                return true;
            }
            return false;
        }
        public bool TryRegenerate(out bool DidRegen, MinEvent FromEvent = null, Event FromSEvent = null)
        {
            return TryRegenerate(out DidRegen, out _, FromEvent, FromSEvent);
        }

        public bool Restore(out string Condition, MinEvent FromEvent = null, Event FromSEvent = null)
        {
            int indent = Debug.LastIndent + 1;
            Debug.Entry(4, 
                $"* {nameof(Restore)}("
                + $" FromEvent: {FromEvent?.GetType()?.Name ?? NULL},"
                + $" FromSEvent: {FromSEvent?.ID ?? NULL})",
                Indent: indent, Toggle: true);

            Condition = null;
            if (ParentObject != null && (isBusted || isRusted))
            {
                int regenMax = RestoreDie.Max();
                int regenMaxPadding = regenMax.ToString().Length;
                int roll = RestoreDie.Resolve();
                string rollString = roll.ToString().PadLeft(regenMaxPadding, ' ');
                bool byChance = roll == RestoreDie.Max();
                if (byChance)
                {
                    bool isEquipped = Equipper != null;
                    string equipped = isEquipped ? "equipped " : "";
                    Rusted rusted = ParentObject?.GetEffect<Rusted>();
                    Broken busted = ParentObject?.GetEffect<Broken>();
                    Condition = rusted?.DisplayName ?? busted?.DisplayName;
                    string message = $"=object.T's= {equipped}=subject.name's= {Grammar.MakeLowerCase(MOD_NAME_COLORED)} restored =subject.pronoun= from being {Condition}!";

                    message = GameText.VariableReplace(message, Subject: ParentObject, Object: Holder);

                    rusted?.Remove(ParentObject);
                    busted?.Remove(ParentObject);

                    Debug.Entry(4,
                        $"({rollString}/{regenMax})" +
                        $" {message}",
                        Indent: indent + 1, Toggle: true
                        );

                    bool didRestore = !(isRusted || isBusted);
                    if (didRestore)
                    {
                        if (Holder.IsPlayer())
                        {
                            Popup.Show(GameText.VariableReplace(message, Subject: ParentObject, Object: Holder));
                        }
                        else
                        {
                            AddPlayerMessage(message);
                        }
                    }
                    return didRestore;
                }
                else
                {
                    Debug.Entry(4, 
                        $"({rollString}/{regenMax})" +
                        $" {ParentObject?.DebugName ?? NULL}' {Grammar.MakeLowerCase(MOD_NAME_COLORED)}" +
                        $" remained innactive!", 
                        Indent: indent + 1, Toggle: true);
                }
            }
            return false;
        }
        public bool TryRestore(out string Condition, MinEvent FromEvent = null, Event FromSEvent = null)
        {
            Condition = null;
            bool DidRestore = false;
            if (ParentObject != null && Restore(out Condition, FromEvent, FromSEvent))
            {
                TimesRestored++;
                DidRestore = true;
            }
            return DidRestore;
        }
        public bool TryRestore(MinEvent FromEvent = null, Event FromSEvent = null)
        {
            return TryRestore(out _, FromEvent, FromSEvent);
        }

        public override bool WantTurnTick()
        {
            Debug.Entry(4,
                $"{nameof(Mod_UD_RegenNanobots)}." +
                $"{nameof(WantTurnTick)}()",
                Indent: Debug.LastIndent, Toggle: doDebug);

            return base.WantTurnTick()
                || true;
        }
        public override void TurnTick(long TimeTick, int Amount)
        {
            int chargeUse = 0;
            bool turnsOverride = TimeTick - StoredTimeTick > 3;

            Debug.Entry(4,
                $"@ {nameof(Mod_UD_RegenNanobots)}"
                + $"{nameof(TurnTick)}"
                + $"(long TimeTick: {TimeTick}, int Amount: {Amount})",
                Indent: 0, Toggle: doDebug);

            bool inZone =
                ParentObject != null
             && ParentObject.CurrentZone == The.ActiveZone
             && !ParentObject.IsInGraveyard();

            if (inZone)
            {
                Debug.CheckYeh(4, $"In Zone", Indent: 1, Toggle: doDebug);
                
                if (Regened && turnsOverride)
                {
                    Debug.CheckYeh(4, $"Turns Overriden", Indent: 1, Toggle: doDebug);
                    Regened = false;
                    StoredTimeTick = TimeTick;
                }

                if ((isRusted || isBusted) && HaveChargeToRestore(chargeUse) && TryRestore(out string condition))
                {
                    Debug.CheckYeh(4, $"{condition} Restored", Indent: 1, Toggle: doDebug);
                    chargeUse += GetRestoreChargeUse();
                }
                else
                {
                    Debug.CheckNah(4, $"No Restore", Indent: 1, Toggle: doDebug);
                    chargeUse += Tier;
                }

                if (!Regened && isDamaged && HaveChargeToRegen(chargeUse) && TryRegenerate(out Regened, out int regenAmount))
                {
                    StoredTimeTick = TimeTick;
                    Debug.CheckYeh(4, $"Regened {regenAmount}", Indent: 1, Toggle: doDebug);
                    chargeUse += GetRegenChargeUse();
                }
                else
                {
                    Debug.CheckNah(4, $"No Regen", Indent: 1, Toggle: doDebug);
                    chargeUse += Tier;
                }

                bool usedCharge = false;
                if (chargeUse > 0 && ParentObject.UseCharge(chargeUse))
                {
                    usedCharge = true;
                }
                Debug.LoopItem(4,
                    $"{nameof(usedCharge)}", $"{usedCharge}",
                    Good: usedCharge, Indent: 2, Toggle: doDebug);
            }
            else
            {
                Debug.CheckNah(4, $"Not in Zone", Indent: 1, Toggle: doDebug);
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
                || ID == GetItemElementsEvent.ID
                || ID == GetDisplayNameEvent.ID
                || ID == GetShortDescriptionEvent.ID;
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
                E.AddWithClause(Grammar.MakeLowerCase(MOD_NAME_COLORED));
            }
            return base.HandleEvent(E);
        }
        public override bool HandleEvent(GetShortDescriptionEvent E)
        {
            E.Postfix.AppendRules(GetDescription());

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

                SB.AppendColored("M", MOD_NAME).Append(": ");
                SB.AppendLine();
                SB.AppendColored("W", $"State").AppendLine();
                SB.Append(VANDR).Append($"[{Regened.YehNah(true)}]{HONLY}{nameof(Regened)}: ")
                    .AppendColored("B", $"{Regened}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("C", $"{StoredTimeTick}-{The.Game.TimeTicks}|{The.Game.TimeTicks-StoredTimeTick}")
                    .Append($"){HONLY}Current{nameof(The.Game.TimeTicks)}-{nameof(StoredTimeTick)}|Difference");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("o", $"{RegenDie}")
                    .Append($"){HONLY}{nameof(RegenDie)}");
                SB.AppendLine();
                SB.Append(VANDR).Append("(").AppendColored("o", $"{RestoreDie}")
                    .Append($"){HONLY}{nameof(RestoreDie)}");
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
                SB.AppendColored("W", $"Bools").AppendLine();
                SB.Append(VANDR).Append($"[{isDamaged.YehNah(true)}]{HONLY}{nameof(isDamaged)}: ")
                    .AppendColored("B", $"{isDamaged}");
                SB.AppendLine();
                SB.Append(VANDR).Append($"[{isRusted.YehNah(true)}]{HONLY}{nameof(isRusted)}: ")
                    .AppendColored("B", $"{isRusted}");
                SB.AppendLine();
                SB.Append(TANDR).Append($"[{isBusted.YehNah(true)}]{HONLY}{nameof(isBusted)}: ")
                    .AppendColored("B", $"{isBusted}");
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
