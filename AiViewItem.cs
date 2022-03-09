using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace CPAModelViewHtmlReader {
	public class AiViewItem {
		public ItemType Type { get; }
		public string Name { get; }
		public int HeaderStartIndex { get; }
		public int ContentStartIndex { get; }

		public AiViewItem(string type, string name, int headerStart, int contentStart) {
			Type = type switch {
				"DECLARATION" => ItemType.Declaration,
				"SUBROUTINE" => ItemType.Subroutine,
				"INTELLIGENCE BEHAVIOUR" => ItemType.Intelligence,
				"REFLEX BEHAVIOUR" => ItemType.Reflex,
				"FAMILY ALLOCATION" => ItemType.FamilyAllocation,
				_ => throw new NotImplementedException($"Unknown type {type}")
			};
			Name = name;
			HeaderStartIndex = headerStart;
			ContentStartIndex = contentStart;
		}

		public string[] Content { get; set; }

		public void SetContentFromHTML(string text) {
			text = Regex.Replace(text, "\r\n</TD></TR></TABLE>\r\n", "\r\n<br>\r\n");
			text = Regex.Replace(text, "\r\n.*<TR><TD>", "\r\n");
			text = Regex.Replace(text, "\r\n<UL>\r\n<br>\r\n", "\r\n<UL>\r\n");
			//text = Regex.Replace(text, "\r\n;(.*)\r\n", "\r\n<code style=\"white-space: pre\">;$1</code>\r\n");

			// Remove formatting tags
			text = Regex.Replace(text, "<(/)?FONT[^>]*>", "");
			text = Regex.Replace(text, "<(/)?[BbIiUu]>", "");
			text = Regex.Replace(text, "<(/)?CENTER>", "");
			text = Regex.Replace(text, "\r\n", "");
			text = Regex.Replace(text, "<UL></UL>", "<br>");
			text = Regex.Replace(text, "<br><br>", "<br>");
			text = Regex.Replace(text, "<br>", "\r\n");
			text = Regex.Replace(text, "<UL>", "\r\n<UL>");
			text = Regex.Replace(text, "</UL> *\r\n", "\r\n</UL>");
			//text = Regex.Replace(text, "</TD></TR></TABLE>", "");
			text = Regex.Replace(text, "[^\r\n]*<TABLE BORDER COLS=1 WIDTH=\"100%\" BGCOLOR=\".*\"><TR><TD>", "");


			text = Regex.Replace(text, "  ?<  ?>  ?", " <> ");
			text = Regex.Replace(text, "  >  ", " > ");
			text = Regex.Replace(text, "  <  ", " < ");
			text = Regex.Replace(text, "  =  ", " = ");
			text = Regex.Replace(text, "  \\?  ", " ? ");
			text = Regex.Replace(text, "\\?  ", "? ");
			text = Regex.Replace(text, "  <=  ", " <= ");
			text = Regex.Replace(text, "  >=  ", " >= ");
			text = Regex.Replace(text, "  :=  ", " := ");
			text = Regex.Replace(text, "  ou  ", " ou ");
			text = Regex.Replace(text, "  et  ", " et ");

			text = Regex.Replace(text, "\r\n", "\n");

			Content = text.Split("\n");

			// Create tabs
			int tabs = 0;
			int lastLine = -1;
			bool isBadTab = false;
			bool wasBadTab = false;
			for (int i = 0; i < Content.Length; i++) {
				var line = Content[i];
				wasBadTab = isBadTab;
				isBadTab = false;
				if (line.Contains("</TD></TR></TABLE>")) {
					isBadTab = true;
					line = line.Replace("</TD></TR></TABLE>", "");
				}
				while (line.StartsWith("</UL>")) {
					tabs--;
					line = line.Substring(5);
				}
				while (line.StartsWith("<UL>")) {
					tabs++;
					line = line.Substring(4);
				}
				if(wasBadTab) tabs--;
				if (line.StartsWith("<P>ShowPrivate(0)")) {
					tabs = 0;
					var content = Content;
					Array.Resize(ref content, i-1);
					Content = content;
					continue;
				}
				if (!string.IsNullOrEmpty(line))
					lastLine = i;
				if (tabs < 0) tabs = 0;
				if (!line.StartsWith("#define")) {
					line = $"{new string('\t', tabs)}{line}";
				}

				Content[i] = line;
			}
			if (lastLine != Content.Length - 1) {
				var content = Content;
				Array.Resize(ref content, lastLine + 1);
				Content = content;
			}
		}

		public enum ItemType {
			Declaration,
			Subroutine,
			Intelligence,
			Reflex,
			FamilyAllocation
		}

	}
}
