using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.GameFiles.ResourceCFG;

public class ResourceCFG
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	public string FileName { get; private set; }

	public List<ResourceCFGEntry> Entries { get; private set; }

	public override string ToString()
	{
		return $"{Entries.Count} entries in {FileName}";
	}

	public ResourceCFG(string FileName)
	{
		this.FileName = FileName;
		Entries = new List<ResourceCFGEntry>();
		Parse();
	}

	private void ExpandAndAdd(int Priority, string VarPart)
	{
		string text = VarPart.Replace('/', '\\');
		if (text.Contains("\\*\\"))
		{
		}
		string text2 = text.Substring(0, text.IndexOf("\\"));
		if (text2[1] == ':')
		{
			text2 += "\\";
		}
		string varPart = text.Substring(text.IndexOf("\\") + 1);
		ExpandAndAdd(Priority, text2, varPart);
	}

	private void ExpandAndAdd(int Priority, string FixedPart, string VarPart)
	{
		if (VarPart.Contains("\\"))
		{
			string searchPattern = VarPart.Substring(0, VarPart.IndexOf("\\"));
			string varPart = VarPart.Substring(VarPart.IndexOf("\\") + 1);
			string[] directories = Directory.GetDirectories(FixedPart, searchPattern);
			string[] array = directories;
			foreach (string fixedPart in array)
			{
				ExpandAndAdd(Priority, fixedPart, varPart);
			}
		}
		else
		{
			string[] files = Directory.GetFiles(FixedPart, VarPart);
			string[] array = files;
			foreach (string fileName in array)
			{
				Entries.Add(new ResourceCFGEntry(Priority, fileName));
			}
		}
	}

	private void Parse()
	{
		if (!File.Exists(FileName))
		{
			return;
		}
		List<string> list = new List<string>(File.ReadLines(FileName));
		int priority = 0;
		foreach (string item in list)
		{
			if (item.ToLower().StartsWith("priority"))
			{
				string s = item.Substring(9).Trim();
				try
				{
					priority = int.Parse(s);
				}
				catch (FormatException)
				{
				}
			}
			else if (item.ToLower().StartsWith("packedfile"))
			{
				string text = Path.Combine(Path.GetDirectoryName(FileName), item.Substring(11).Trim());
				if (text.Contains("*"))
				{
					ExpandAndAdd(priority, text);
				}
				else
				{
					Entries.Add(new ResourceCFGEntry(priority, text));
				}
			}
			else if (!item.ToLower().StartsWith("filetype") && !item.ToLower().StartsWith("directoryfiles") && !item.ToLower().StartsWith("group") && !item.ToLower().StartsWith("scan"))
			{
			}
		}
	}
}
