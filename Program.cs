using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CPAModelViewHtmlReader {
   class Program
   {
      const int MAX_BEHAVIOURS = 500;
      static System.Text.Encoding enc;

      static void Main(string[] args)
      {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            enc = System.Text.Encoding.GetEncoding(1252);

            string path = args[0];

         var text = File.ReadAllText(path, enc);

         int startOfVariables = text.IndexOf("</TABLE>", StringComparison.InvariantCulture);
         int startOfMacros = text.IndexOf("#k1\"", StringComparison.InvariantCulture);
         int startOfSubroutines = text.IndexOf("#k10\"", StringComparison.InvariantCulture);
         int startOfBehaviours = text.IndexOf("#k100\"", StringComparison.InvariantCulture);
         int endOfBehaviours = text.IndexOf("MINI-STRUCTURES", StringComparison.InvariantCulture);

         int selectionOffsetDsgVar = 89;
         int selectionOffsetDsgVarEnd = -228;
         int selectionOffsetSubroutine = -200;
         int selectionOffsetBehavior = -200;

         int dsgVarIndexStart = text.IndexOf("Variables designer", StringComparison.InvariantCulture) +
                                selectionOffsetDsgVar;

         string dsgVarsRaw = text.Substring(dsgVarIndexStart, startOfMacros - dsgVarIndexStart + selectionOffsetDsgVarEnd);
         List<string> dsgVarLines = ParseDsgVarLines(dsgVarsRaw);

         List<string> subroutines = new List<string>();
         List<string> behaviours = new List<string>();

         bool readingSubroutines = true;

         for (int i = 1; i < MAX_BEHAVIOURS; i++) {
            
            int indexStart = text.IndexOf($"#k{i}\"", StringComparison.InvariantCulture);
            int indexEnd = text.IndexOf($"#k{i+1}\"", StringComparison.InvariantCulture);

            if (indexStart < 0 && indexEnd < 0) {
               break;
            }

            if (readingSubroutines) {
               Debug.WriteLine($"Subroutine from {indexStart} to {indexEnd}");

               if (indexEnd >= 0) {
                  var check = (text.Substring(indexStart, indexEnd - indexStart));
                  if (check.Contains("INTELLIGENCE BEHAVIOUR") || check.Contains("REFLEX BEHAVIOUR")) {
                     readingSubroutines = false;
                  }
               }

               if (indexStart >= 0) {
                  if (indexEnd >= 0) {
                     subroutines.Add(text.Substring(indexStart + selectionOffsetSubroutine, indexEnd - indexStart));
                  } else {
                     subroutines.Add(text.Substring(indexStart + selectionOffsetSubroutine, startOfBehaviours));
                  }
               }
            } else {

               Debug.WriteLine($"Behaviour from {indexStart} to {indexEnd}");

               if (indexStart >= 0) {
                  if (indexEnd >= 0) {
                     behaviours.Add(text.Substring(indexStart + selectionOffsetBehavior, indexEnd - indexStart));
                  } else {
                     behaviours.Add(text.Substring(indexStart + selectionOffsetBehavior, endOfBehaviours - indexStart));
                  }
               }
            }
         }


         //string behaviours = text.Substring(startOfBehaviours, endOfBehaviours - startOfBehaviours);

         var regex = new Regex(@"<font size\=\""-1\"">(([\s\S])+)</font>");

         List<EditorScript.Behaviour> Subroutines = new List<EditorScript.Behaviour>();
         List<EditorScript.Behaviour> Rules = new List<EditorScript.Behaviour>();
         List<EditorScript.Behaviour> Reflexes = new List<EditorScript.Behaviour>();
         EditorScript.Behaviour Declarations = new EditorScript.Behaviour("Variables designer",
            EditorScript.Language.French,
            string.Join(Environment.NewLine, dsgVarLines));

         int subroutineIndex = 0;
         foreach (var subroutineHtml in subroutines) {
            string subroutineName = FindBehaviourOrSubroutineName(subroutineHtml);

            int endTable = subroutineHtml.ToLower().IndexOf("</table>");
            // 
            string codeHtml = subroutineHtml.Substring(endTable);

            Debug.WriteLine(subroutineName);
            File.WriteAllText($"subroutine_{subroutineIndex}_{subroutineName}.html", codeHtml);

            string subroutineCode = ParseBehaviour(codeHtml);
            File.WriteAllText($"subroutine_{subroutineIndex++}_{subroutineName}.cpa", subroutineCode);

            Subroutines.Add(new EditorScript.Behaviour(subroutineName, EditorScript.Language.French, subroutineCode));
         }

         int behaviourIndex = 0;
         foreach (var behaviourHtml in behaviours) {
            string behaviourName = FindBehaviourOrSubroutineName(behaviourHtml);

            string type = FindBehaviourType(behaviourHtml);
            string header = FindBehaviourHeader(behaviourHtml);

            // 
            string codeHtml = behaviourHtml.Substring(behaviourHtml.IndexOf(header));

            Debug.WriteLine($"behaviour, name = {behaviourName}, type = {type}, header = {header}");

            string fname = type == "INTELLIGENCE BEHAVIOUR" ? "rule" : "reflex";

            File.WriteAllText($"{fname}_{behaviourIndex}_{behaviourName}.html", codeHtml);

            string behaviourCode = ParseBehaviour(codeHtml);

            File.WriteAllText($"{fname}_{behaviourIndex++}_{behaviourName}.cpa", behaviourCode);

            var behaviour = new EditorScript.Behaviour(behaviourName, EditorScript.Language.French, behaviourCode);
            if (fname == "rule") {
               Rules.Add(behaviour);
            } else {
               Reflexes.Add(behaviour);
            }

         }

         // Editor script files

         // Rules
         string eruContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorIntelligence, Rules.ToArray());

         // Reflexes
         string erfContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorReflex, Reflexes.ToArray());

         // Subroutines
         string esbContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorSubr, Subroutines.ToArray());

         // DsgVar Declarations
         string edeContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorDeclaration, new EditorScript.Behaviour[]
         {
            Declarations
         });

         // Editor script trees

         // Rules
         string rulContent = EditorScript.CreateEditorTreeFile(EditorScript.ScriptType.EditorIntelligence, Rules.ToArray());
         
         // Reflexes
         string rfxContent = EditorScript.CreateEditorTreeFile(EditorScript.ScriptType.EditorReflex, Reflexes.ToArray());
         
         // Subroutines (MACros)
         string macContent = EditorScript.CreateEditorTreeFile(EditorScript.ScriptType.EditorSubr, Subroutines.ToArray());

         string modelName = Path.GetFileNameWithoutExtension(path);

         File.WriteAllText($"{modelName}.eru", eruContent, enc);
         File.WriteAllText($"{modelName}.erf", erfContent, enc);
         File.WriteAllText($"{modelName}.esb", esbContent, enc);
         File.WriteAllText($"{modelName}.ede", edeContent, enc);

         File.WriteAllText($"{modelName}.rul", rulContent, enc);
         File.WriteAllText($"{modelName}.rfx", rfxContent, enc);
         File.WriteAllText($"{modelName}.mac", macContent, enc);

      }

      private static List<string> ParseDsgVarLines(string dsgVarsRaw)
      {
         List<string> vars = new List<string>();

         string text = ParseBehaviour(dsgVarsRaw);
         string[] textLines = text.Split(Environment.NewLine);

         int subCounter = 0;
         string currentDsgVar = string.Empty;

         for (int i = 0; i < textLines.Length; i++) {
            if (textLines[i].Contains(";")) {
               vars.Add(textLines[i]); // always add comments as a new line
            } else {
               currentDsgVar += textLines[i]+" ";
               if (++subCounter == 4) {
                  subCounter = 0;
                  vars.Add(currentDsgVar.Trim());
                  currentDsgVar = string.Empty; // fourth item, add the dsgvar
               }
            }
         }

         return vars;
      }

      private static string FindBehaviourOrSubroutineName(string behaviour)
      {
         var regex = new Regex(@"<FONT SIZE=4><B><CENTER>([^<]+)<\/FONT>");
         var match = regex.Match(behaviour);
         return match.Groups[1].Value.Trim();
      }

      private static string FindBehaviourType(string behaviour)
      {
         var regex = new Regex(@"<u>([^<]+)<\/u>");
         var match = regex.Match(behaviour);
         return match.Groups[1].Value.Trim();
      }

      private static string FindBehaviourHeader(string behaviour)
      {
         var regex = new Regex(@"<FONT COLOR=""#FF0000""><B>([^<]+)<\/FONT>");
         var match = regex.Match(behaviour);
         return match.Groups[1].Value.Trim();
      }

      private static string ParseBehaviour(string behaviour)
      {
         HtmlDocument html = new HtmlDocument();
         html.LoadHtml(behaviour);

         var innerHtml = html.DocumentNode.InnerHtml;
         var innerText = html.DocumentNode.InnerText;

         int indent = 0;

         string startUL = "<ul>";
         string endUL = "</ul>";

         List<string> lines = new List<string>();

         var htmlLines = innerHtml.Split('\n');
         var textLines = innerText.Split('\n');
         for (int i=0;i<htmlLines.Length;i++) {
            string lineHtml = htmlLines[i];
            string lineText = textLines[i];
            indent += lineHtml.Contains(startUL) ? 1 : 0;
            indent -= lineHtml.Contains(endUL) ? 1 : 0;

            if (indent < 0) {
               indent = 0;
            }

            string lineBreak = (lineHtml.Contains(@"<ul></ul>")) ? Environment.NewLine : string.Empty;

            lines.Add(new string('\t', indent) + lineText.Trim() + lineBreak);
         }

         var filledLines = lines.Where(l => l.EndsWith(Environment.NewLine) || !string.IsNullOrWhiteSpace(l));
         return string.Join(Environment.NewLine, filledLines);
      }
   }

}
