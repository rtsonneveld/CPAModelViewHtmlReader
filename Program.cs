using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace CPAModelViewHtmlReader {
	class Program {
		const int MAX_BEHAVIOURS = 500;
		static System.Text.Encoding enc;

		static void Main(string[] args) {
			Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
			enc = System.Text.Encoding.GetEncoding(1252);

			string path = args[0];
			string mainOutDir = null;
			if (args.Length > 1) {
				mainOutDir = args[1];
			}
			string dir = mainOutDir;
			string fName = Path.GetFileNameWithoutExtension(path);
			string fDir = Path.GetDirectoryName(path);

			// Don't replace any AI files if date < the HTML creation date
			string existingEDEFile = Path.Combine(fDir, $"{fName}.ede");
			if (File.Exists(existingEDEFile)) {
				var existingLines = File.ReadAllLines(existingEDEFile);
				if (existingLines.Length > 6) {
					string dateLine = existingLines[6];
					if (dateLine.StartsWith(";Save date:")) {
						dateLine = dateLine.Substring(";Save date:".Length);
						CultureInfo enUS = new CultureInfo("en-US");
						DateTime date = DateTime.ParseExact(dateLine, "dddd,MMMM,dd,yyyy,HH\\hmm\\m", enUS, DateTimeStyles.None);
						if (date < new DateTime(2006, 03, 13)) {
							Console.WriteLine("Not necessary to create new editor files, this model wasn't updated after HTML creation");
							return;
						}
					}
				}
			}


			string debugDir = mainOutDir == null ? null : Path.Combine(dir, fName);

			var text = File.ReadAllText(path, enc);
			if (text.Length < 5) {
				Console.WriteLine("Invalid HTML file");
				return;
			}

			List<AiViewItem> items = new List<AiViewItem>();
			var TableHeaders = Regex.Matches(text, "<TABLE BORDER COLS=1 WIDTH=\"100%\" BGCOLOR=\"#99AAAA\"><TR><TD><FONT FACE=\"arial\"><FONT SIZE=-1><FONT COLOR=\"#004400\"><i><u>(?<name>[A-Za-z0-9_ ]+)</u></i></FONT></FONT></FONT>");
			Console.WriteLine(TableHeaders.Count);
			foreach (Match th in TableHeaders) {
				var tableHeaderType = th.Groups["name"].Value;
				var tableHeaderEndIndex = text.IndexOf("</TABLE>", th.Index) + "</TABLE>".Length;
				var substr = text.Substring(th.Index, tableHeaderEndIndex - th.Index);
				var NamePattern = Regex.Match(substr, "<FONT FACE=\"arial\"(><FONT)? COLOR=\"#001133\"><FONT SIZE=4><B><CENTER>(\r\n)?(?<name>[A-Za-z0-9_\\- ]+)(\r\n)?</FONT></FONT>(</FONT>)?</B></U></I></CENTER>");
				var name = NamePattern.Groups["name"].Value;
				if(debugDir != null) Console.WriteLine(tableHeaderType + " - " + name);

				items.Add(new AiViewItem(tableHeaderType, name, th.Index, tableHeaderEndIndex));
			}

			if (debugDir != null && items.Count > 0) {
				Directory.CreateDirectory(Path.Combine(debugDir, "txt"));
			}
			for (int i = 0; i < items.Count; i++) {
				var item = items[i];
				var nextItem = (i + 1) < items.Count ? items[i+1] : null;
				var nextIndex = nextItem?.HeaderStartIndex ?? text.Length;
				item.SetContentFromHTML(text.Substring(item.ContentStartIndex, nextIndex - item.ContentStartIndex));
				if(debugDir != null)
					File.WriteAllLines(Path.Combine(debugDir, "txt", $"{item.Type}_{item.Name}.txt"), item.Content);
			}
			//var TableHeaderPattern = Regex.Matches(text, "<FONT FACE=\"arial\" COLOR=\"#001133\"><FONT SIZE=4><B><CENTER>\r\n(?<name>[A-Za-z0-9_\\- ]+)\r\n</FONT></FONT></B></U></I></CENTER>");
			//Console.WriteLine(headers.Count);

			EditorScript.Behaviour DsgVars = null;
			EditorScript.Behaviour MacrosHeader = null;
			var Macros = new List<EditorScript.Behaviour>();
			var Intelligence = new List<EditorScript.Behaviour>();
			var Reflex = new List<EditorScript.Behaviour>();
			var Subroutines = new List<EditorScript.Behaviour>();
			foreach (var item in items) {
				switch (item.Type) {
					case AiViewItem.ItemType.Declaration:
						switch (item.Name) {
							case "Variables designer":
								DsgVars = new EditorScript.Behaviour(item, EditorScript.Language.French, EditorScript.ScriptType.EditorDeclarationVariables, 0);
								break;
							case "Macros":
								MacrosHeader = new EditorScript.Behaviour(item, EditorScript.Language.French, EditorScript.ScriptType.EditorDeclarationMacros, 0);
								break;
							default:
								Macros.Add(new EditorScript.Behaviour(item, EditorScript.Language.French, EditorScript.ScriptType.EditorDeclarationMacros, Macros.Count));
								break;
						}
						break;
					case AiViewItem.ItemType.FamilyAllocation:
						break;
					case AiViewItem.ItemType.Intelligence:
						Intelligence.Add(new EditorScript.Behaviour(item, EditorScript.Language.French, EditorScript.ScriptType.EditorIntelligence, Intelligence.Count));
						break;
					case AiViewItem.ItemType.Reflex:
						Reflex.Add(new EditorScript.Behaviour(item, EditorScript.Language.French, EditorScript.ScriptType.EditorReflex, Reflex.Count));
						break;
					case AiViewItem.ItemType.Subroutine:
						Subroutines.Add(new EditorScript.Behaviour(item, EditorScript.Language.French, EditorScript.ScriptType.EditorSubr, Subroutines.Count));
						break;
				}
			}

			// Editor script files

			// Rules
			string eruContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorIntelligence, Intelligence.ToArray());

			// Reflexes
			string erfContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorReflex, Reflex.ToArray());

			// Subroutines
			string esbContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorSubr, Subroutines.ToArray());

			// DsgVar Declarations
			string edeContent = EditorScript.CreateEditorScript(EditorScript.ScriptType.EditorDeclaration, new EditorScript.Behaviour[] {
				DsgVars,
				MacrosHeader
			});

			// Editor script trees

			// Rules
			string rulContent = EditorScript.CreateEditorTreeFile(EditorScript.ScriptType.EditorIntelligence, Intelligence.ToArray());

			// Reflexes
			string rfxContent = EditorScript.CreateEditorTreeFile(EditorScript.ScriptType.EditorReflex, Reflex.ToArray());

			// Subroutines (MACros)
			string macContent = EditorScript.CreateEditorTreeFile(EditorScript.ScriptType.EditorSubr, Subroutines.ToArray());

			// DsgVars
			string decContent = string.Join(Environment.NewLine, DsgVars.EditorTreeFile);

			string modelName = Path.GetFileNameWithoutExtension(path);
			
			var outPath = debugDir ?? Path.GetDirectoryName(path);

			File.WriteAllText(Path.Combine(outPath, $"{modelName}.eru"), eruContent, enc);
			File.WriteAllText(Path.Combine(outPath, $"{modelName}.erf"), erfContent, enc);
			File.WriteAllText(Path.Combine(outPath, $"{modelName}.esb"), esbContent, enc);
			File.WriteAllText(Path.Combine(outPath, $"{modelName}.ede"), edeContent, enc);

			File.WriteAllText(Path.Combine(outPath, $"{modelName}.rul"), rulContent, enc);
			File.WriteAllText(Path.Combine(outPath, $"{modelName}.rfx"), rfxContent, enc);
			File.WriteAllText(Path.Combine(outPath, $"{modelName}.mac"), macContent, enc);
			File.WriteAllText(Path.Combine(outPath, $"{modelName}.dec"), decContent, enc);

		}

		private static List<string> ParseDsgVarLines(string dsgVarsRaw) {
			List<string> vars = new List<string>();

			string text = ParseBehaviour(dsgVarsRaw);
			string[] textLines = text.Split(Environment.NewLine);

			int subCounter = 0;
			string currentDsgVar = string.Empty;

			for (int i = 0; i < textLines.Length; i++) {
				if (textLines[i].Contains(";")) {
					vars.Add(textLines[i]); // always add comments as a new line
				} else {
					currentDsgVar += textLines[i] + " ";
					if (++subCounter == 4) {
						subCounter = 0;
						vars.Add(currentDsgVar.Trim());
						currentDsgVar = string.Empty; // fourth item, add the dsgvar
					}
				}
			}

			return vars;
		}

		private static string FindBehaviourOrSubroutineName(string behaviour) {
			var regex = new Regex(@"<FONT SIZE=4><B><CENTER>([^<]+)<\/FONT>");
			var match = regex.Match(behaviour);
			return match.Groups[1].Value.Trim();
		}

		private static string FindBehaviourType(string behaviour) {
			var regex = new Regex(@"<u>([^<]+)<\/u>");
			var match = regex.Match(behaviour);
			return match.Groups[1].Value.Trim();
		}

		private static string FindBehaviourHeader(string behaviour) {
			var regex = new Regex(@"<FONT COLOR=""#FF0000""><B>([^<]+)<\/FONT>");
			var match = regex.Match(behaviour);
			return match.Groups[1].Value.Trim();
		}

		private static string ParseBehaviour(string behaviour) {
			HtmlDocument html = new HtmlDocument();
			html.LoadHtml(behaviour);

			var innerHtml = html.DocumentNode.InnerHtml;
			var innerText = html.ParsedText;

			int indent = 0;

			string startUL = "<ul>";
			string endUL = "</ul>";

			List<string> lines = new List<string>();

			var htmlLines = innerHtml.Split('\n');
			var textLines = innerText.Split('\n');
			for (int i = 0; i < htmlLines.Length; i++) {
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
