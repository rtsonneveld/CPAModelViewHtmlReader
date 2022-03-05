using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CPAModelViewHtmlReader {
   public class EditorScript
   {

      public enum Language
      {
         French,
         English
      }

      public enum ScriptType
      {
         EditorIntelligence,
         EditorReflex,
         EditorSubr,
         EditorDeclaration,
      }

      public struct Behaviour
      {
         public string Name;
         public Language Language;
         public string Script;

         public Behaviour(string name, Language language, string script)
         {
            Name = name;
            Language = language;
            Script = script;
         }

         public override string ToString()
         {
            return Name;
         }
      }

      public static string CreateEditorScript(ScriptType scriptType, Behaviour[] behaviours)
      {
         List<string> lines = new List<string>();

         lines.Add($"{{Create{scriptType}:");
         foreach (var b in behaviours) {
            lines.Add($"\t{{CreateEditorBehaviour:");
            lines.Add($"\t\tName({b.Name})");
            lines.Add($"\t\tLanguage({b.Language})");
            foreach (var line in b.Script.Split(Environment.NewLine)) {
               lines.Add($"\t\tText(\"{line.Replace("\"", "'")}\")");
            }

            lines.Add($"\t}}");
         }

         lines.Add($"}}");

         return string.Join(Environment.NewLine, lines);
      }

      // For the .rul, .rfx and .mac files
      public static string CreateEditorTreeFile(ScriptType scriptType, Behaviour[] behaviours)
      {
         List<string> lines = new List<string>();

         string headerLine = scriptType == ScriptType.EditorSubr ? "CreateListOfMacro" : "CreateIntelligence";
         string scriptLine = scriptType == ScriptType.EditorSubr ? "CreateMacro" : "CreateComport";

         lines.Add($"{{{headerLine}:({behaviours.Length})");

         int index = 0;

         foreach (var b in behaviours) {
            lines.Add($"\t{{{scriptLine}:{b.Name}({index++},1)");

            if (scriptType == ScriptType.EditorSubr) {
               lines.Add($"\t\tProcedure(Proc_None,1)");
            } else {
               lines.Add($"\t\t{{CreateRule:(1,1)");
               lines.Add($"\t\t\tProcedure(Proc_None,1)");
               lines.Add($"\t\t}}");
            }

            lines.Add($"\t}}");
         }

         lines.Add($"}}");

         return string.Join(Environment.NewLine, lines);
      }

   }
}
