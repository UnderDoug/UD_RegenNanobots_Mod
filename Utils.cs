using System;
using System.Collections.Generic;
using System.Linq;

using ConsoleLib.Console;

using Kobold;

using XRL;
using XRL.UI;
using XRL.Rules;
using XRL.World;
using XRL.World.Anatomy;
using XRL.World.Parts;
using XRL.World.Parts.Mutation;
using XRL.World.Tinkering;
using XRL.World.ObjectBuilders;
using XRL.Language;

using XRL.World.Text.Delegates;
using XRL.World.Text.Attributes;

using static UD_RegenNanobots_Mod.Const;

using Debug = UD_RegenNanobots_Mod.Debug;
using Options = UD_RegenNanobots_Mod.Options;

namespace UD_RegenNanobots_Mod
{
    [HasVariableReplacer]
    public static class Utils
    {
        private static bool doDebug => true;
        private static bool getDoDebug (string MethodName)
        {
            if (MethodName == nameof(ExplodingDie))
                return false;

            return doDebug;
        }

        public static ModInfo ThisMod => ModManager.GetMod(MOD_ID);

        public static int ExplodingDie(int Number, DieRoll DieRoll, int Step = 1, int Limit = 0, int Indent = 0)
        {
            Debug.Entry(4,
                $"ExplodingDie(Number: {Number}, DieRoll: {DieRoll}, Step: {Step}, Limit: {Limit})",
                Indent: Indent, Toggle: getDoDebug(nameof(ExplodingDie)));

            int High = DieRoll.Max();
            int oldIndent = Indent;

            Debug.Entry(4, $"Explodes on High Roll: {High}", Indent: Indent, Toggle: getDoDebug(nameof(ExplodingDie)));
            Begin:
            if (Limit != 0 && High >= Limit)
            {
                Debug.Entry(4, "Limit 0 or DieRoll.Max() >= Limit", Indent: ++Indent, Toggle: getDoDebug(nameof(ExplodingDie)));
                Debug.Entry(4, "Exiting", Indent: Indent--, Toggle: getDoDebug(nameof(ExplodingDie)));
                Number = Limit;
                goto Exit;
            }

            Debug.Entry(4, $"Rollin' the Die!", Indent: Indent, Toggle: getDoDebug(nameof(ExplodingDie)));
            int Result = DieRoll.Resolve();
            if (Result == High)
            {
                Debug.Entry(4, $"continue: {Result}, Success!", Indent: ++Indent, Toggle: getDoDebug(nameof(ExplodingDie)));
                Debug.Entry(4, $"Increasing Number by {Step}", Indent: Indent, Toggle: getDoDebug(nameof(ExplodingDie)));
                Debug.Entry(4, $"Sending Number for another roll!", Indent: Indent, Toggle: getDoDebug(nameof(ExplodingDie)));
                Number += Step;
                Indent++;
                // Number = ExplodingDie(Number += Step, DieRoll, Step, Limit, ++Indent);
                goto Begin;
            }
            else
            {
                Debug.Entry(4, $"continue: {Result}, Failure!", Indent: ++Indent, Toggle: getDoDebug(nameof(ExplodingDie)));
            }

            Exit:
            Debug.Entry(4, $"Final Number: {Number}", Indent: oldIndent, Toggle: getDoDebug(nameof(ExplodingDie)));
            return Number;
        }

        public static int ExplodingDie(int Number, string DieRoll, int Step = 1, int Limit = 0, int Indent = 0)
        {
            DieRoll dieRoll = new(DieRoll);
            return ExplodingDie(Number, dieRoll, Step, Limit, Indent);
        }

        public static string MakeAndList(IReadOnlyList<string> Words, bool Serial = true, bool IgnoreCommas = false)
        {
            List<string> replacedList = new();
            foreach (string entry in Words)
            {
                replacedList.Add(entry.Replace(",", ";;"));
            }
            string andList = Grammar.MakeAndList(replacedList, Serial);
            return andList.Replace(";;", ",");
        }

        public static string Quote(string @string)
        {
            return $"\"{@string ?? "null"}\"";
        }

        public static BookInfo GetBook(string BookName)
        {
            BookUI.Books.TryGetValue(BookName, out BookInfo Book);
            return Book;
        }

        public static string GetBoolString(List<string> list, bool @bool)
        {
            string trueString = null;
            string falseString = null;
            string output = "";

            foreach(string @string in list)
            {
                if (@string.StartsWith("true;;"))
                {
                    trueString = @string.Replace("true;;", "");
                }
                else if (@string.StartsWith("false;;"))
                {
                    falseString = @string.Replace("false;;", "");
                }
                else
                {
                    output += @string;
                }
            }
            return output.Replace("%s", @bool ? trueString : falseString);
        }

        public static bool FiftyFifty()
        {
            return 5000.in10000();
        }

    } //!-- public static class Utils
}