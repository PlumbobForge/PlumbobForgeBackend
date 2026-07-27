using System.Collections.Generic;
using System.IO;

namespace S3ForgeTools.Utils;

public class IniFile
{
	public string FileName { get; private set; }

	public Dictionary<string, Dictionary<string, string>> Entries { get; private set; }

	public IniFile(string FileName)
	{
		this.FileName = FileName;
		Entries = new Dictionary<string, Dictionary<string, string>>();
		Load();
	}

	public void Load()
	{
		string[] array = File.ReadAllLines(FileName);
		string text = "";
		Dictionary<string, string> dictionary = null;
		string[] array2 = array;
		foreach (string text2 in array2)
		{
			string text3 = text2.Trim();
			if (text3 == "")
			{
				continue;
			}
			if (text3.StartsWith("["))
			{
				text = text3.Trim("[]".ToCharArray());
				if (!Entries.ContainsKey(text))
				{
					Entries.Add(text, new Dictionary<string, string>());
				}
				dictionary = Entries[text];
			}
			else
			{
				string key = text3.Substring(0, text3.IndexOf('=') - 1).Trim();
				string value = text3.Substring(text3.IndexOf('=') + 1).Trim();
				dictionary?.Add(key, value);
			}
		}
	}

	public void Save()
	{
		List<string> list = new List<string>();
		foreach (KeyValuePair<string, Dictionary<string, string>> entry in Entries)
		{
			list.Add($"[{entry.Key}]");
			foreach (KeyValuePair<string, string> item in entry.Value)
			{
				list.Add($"{item.Key} = {item.Value}");
			}
		}
		FileTools.SwapBackupChain(FileName);
		File.WriteAllLines(FileName, list);
	}

	public string GetValue(string Section, string Key)
	{
		if (!Entries.ContainsKey(Section))
		{
			return null;
		}
		if (!Entries[Section].ContainsKey(Key))
		{
			return null;
		}
		return Entries[Section][Key];
	}

	public void SetValue(string Section, string Key, string Value)
	{
		Dictionary<string, string> dictionary = null;
		if (!Entries.ContainsKey(Section))
		{
			dictionary = new Dictionary<string, string>();
			Entries.Add(Section, dictionary);
		}
		else
		{
			dictionary = Entries[Section];
		}
		if (!dictionary.ContainsKey(Key))
		{
			dictionary.Add(Key, Value);
		}
		else
		{
			dictionary[Key] = Value;
		}
	}
}
