using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CPAModelViewHtmlReader {
	public class EditorScript {

		public enum Language {
			French,
			English
		}

		public enum ScriptType {
			EditorIntelligence,
			EditorReflex,
			EditorSubr,
			EditorMacro,
			EditorDeclaration,
			// Bad
			EditorDeclarationVariables,
			EditorDeclarationMacros,
		}

		public class Behaviour {
			public string Name => Script.Name;
			public Language Language { get; set; }
			public ScriptType Type { get; set; }
			public AiViewItem Script { get; set; }
			public List<string> EditorScript { get; set; } = new List<string>();
			public List<string> EditorTreeFile { get; set; } = new List<string>();
			public int Index { get; set; }


			public Behaviour(AiViewItem script, Language language, ScriptType type, int index) {
				Language = language;
				Script = script;
				Type = type;
				Index = index;
				CreateEditorScript();
			}

			public override string ToString() {
				return Name;
			}

			public void CreateEditorScript() {
				// EditorScript
				EditorScript.Add($"{{CreateEditorBehaviour:");
				EditorScript.Add($"\tName({Name})");
				EditorScript.Add($"\tLanguage({Language})");
				foreach (var line in Script.Content) {
					EditorScript.Add($"\tText(\"{line.Replace("\"", "'")}\")");
				}

				EditorScript.Add($"}}");

				if (Type != ScriptType.EditorMacro && 
					Type != ScriptType.EditorDeclarationMacros &&
					Type != ScriptType.EditorDeclarationVariables) {
					// TreeFile
					string scriptLine = Type == ScriptType.EditorSubr ? "CreateMacro" : "CreateComport";
					EditorTreeFile.Add($"{{{scriptLine}:{Name}({Index},1)");

					if (Type == ScriptType.EditorSubr) {
						EditorTreeFile.Add($"\tProcedure(Proc_None,1)");
					} else {
						EditorTreeFile.Add($"\t{{CreateRule:(1,1)");
						EditorTreeFile.Add($"\t\tProcedure(Proc_None,1)");
						EditorTreeFile.Add($"\t}}");
					}

					EditorTreeFile.Add($"}}");
				} else if (Type == ScriptType.EditorDeclarationVariables) {
					List<string> dsgVarLines = new List<string>();
					int totalDsgVarSize = 0;
					int totalDsgVarCount = 0;
					int curDsgVarIndex = 0;
					foreach (var contentLine in Script.Content) {
						var l = Regex.Replace(contentLine, ";.*", "");
						if(string.IsNullOrWhiteSpace(l)) continue;
						var dsgvarMatch = Regex.Match(l, "[ \t]*(private[ \t]+)?(?<type>[A-Za-z0-9_\\-]+)[ \t]+(?<name>[A-Za-z0-9_\\-]+)[ \t]+:[ \t]+(?<value>[A-Za-z0-9_\\-]+)?.*", RegexOptions.IgnoreCase);
						if (dsgvarMatch.Success) {
							var editorType = dsgvarMatch.Groups["type"].Value;
							var name = dsgvarMatch.Groups["name"].Value;
							var value = dsgvarMatch.Groups["value"].Value;
							var treeType = ConvertEditorTypeToTreeType(editorType);
							var treeName = name.ToLowerInvariant();
							bool isArray = IsArray(treeType);
							string defaultValue;
							int dsgVarSize;
							if (isArray) {
								int numElements = int.Parse(value);
								defaultValue = DefaultValue(treeType, numElements: numElements);
								dsgVarSize = DsgVarSize(treeType, arrayElements: numElements);
							} else {
								defaultValue = DefaultValue(treeType);
								dsgVarSize = DsgVarSize(treeType);
							}
							string treeLine = $"{treeType}({curDsgVarIndex},{treeName},{defaultValue})";
							dsgVarLines.Add(treeLine);
							curDsgVarIndex++;
							totalDsgVarSize += dsgVarSize;
							totalDsgVarCount++;
						} else {
							throw new Exception($"Couldn't match line: {l}");
						}
					}
					// TreeFile
					EditorTreeFile.Add($"{{CreateVariables:({totalDsgVarSize},{totalDsgVarCount})");
					foreach (var l in dsgVarLines) {
						EditorTreeFile.Add($"\t{l}");
					}
					EditorTreeFile.Add($"}}");
				}
			}
		}

		public static string ConvertEditorTypeToTreeType(string editorType) {
			return editorType.ToLowerInvariant() switch {
				"reel" => "Float",
				"entier" => "Integer",
				"booleen" => "Boolean",
				"entier0to255" => "0To255",
				"perso" => "Perso",
				"typevecteur" => "Vector",
				"entier_128to127" => "_128To127",
				"entier_32768to32767" => "_32768To32767",
				"entier0to65535" => "0To65535",
				"liste" => "List",
				"typeaction" => "Action",
				"waypoint" => "WayPoint",
				"typereseau" => "Graph",
				"typetableauperso" => "PersoArray",
				"typetableauentier" => "IntegerArray",
				"typetableautexte" => "TextArray",
				_ => throw new NotImplementedException($"Unimplemented DsgVarType {editorType}")
			};
		}
		public static bool IsArray(string treeType) {
			return treeType switch {
				"PersoArray" => true,
				"IntegerArray" => true,
				"TextArray" => true,
				"List" => true,
				_ => false
			};
		}
		public static string DefaultValue(string treeType, int numElements = 1) {
			return treeType switch {
				"PersoArray" => numElements.ToString(),
				"IntegerArray" => numElements.ToString(),
				"TextArray" => numElements.ToString(),
				"List" => numElements.ToString(),

				"Vector" => "\"0.000000\",\"0.000000\",\"0.000000\"",
				"Perso" => "Nobody",
				"Float" => "\"0.000000\"",
				"WayPoint" => "Nowhere",
				"Graph" => "NoGraph",
				"Action" => "NoAction",
				_ => "0"
			};
		}
		public static int DsgVarSize(string treeType, int arrayElements = 1) {
			return treeType switch {
				"PersoArray" => 8 + 12 * arrayElements,
				"TextArray" => 8 + 12 * arrayElements,
				"IntegerArray" => 8 + 12 * arrayElements,
				"List" => 4 + 12 * arrayElements,

				"Boolean" => 1,
				"0To255" => 1,
				"_128To127" => 1,
				"_32768To32767" => 2,
				"0To65535" => 2,
				"Vector" => 12,
				_ => 4
			};
		}

		public static string CreateEditorScript(ScriptType scriptType, Behaviour[] behaviours) {
			List<string> lines = new List<string>();

			lines.Add($"{{Create{scriptType}:");
			foreach (var b in behaviours) {
				if(b == null) continue;
				foreach (var l in b.EditorScript) {
					lines.Add($"\t{l}");
				}
			}

			lines.Add($"}}");

			return string.Join(Environment.NewLine, lines);
		}

		// For the .rul, .rfx and .mac files
		public static string CreateEditorTreeFile(ScriptType scriptType, Behaviour[] behaviours) {
			List<string> lines = new List<string>();

			string headerLine = scriptType == ScriptType.EditorSubr ? "CreateListOfMacro" : "CreateIntelligence";

			lines.Add($"{{{headerLine}:({behaviours.Length})");

			foreach (var b in behaviours) {
				foreach (var l in b.EditorTreeFile) {
					lines.Add($"\t{l}");
				}
			}

			lines.Add($"}}");

			return string.Join(Environment.NewLine, lines);
		}

	}
}
