using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;

namespace S3ForgeTools.Utils;

public static class FileTools
{
	public static bool SwapBackupChain(string FileName)
	{
		string text = Path.ChangeExtension(FileName, ".bak");
		bool result = false;
		if (File.Exists(text))
		{
			result = true;
			File.Delete(text);
		}
		if (File.Exists(FileName))
		{
			File.Move(FileName, text);
		}
		return result;
	}

	public static bool MoveOrRecycleDuplicate(string SourceName, string DestName, bool CheckHash)
	{
		string directoryName = Path.GetDirectoryName(DestName);
		string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(DestName);
		string extension = Path.GetExtension(DestName);
		bool flag = false;
		string NewDestName = DestName;
		if (CheckHash)
		{
			if (File.Exists(DestName))
			{
				string key = GenerateFileHash(SourceName);
				Dictionary<string, string> dictionary = GenerateHashForPattern(directoryName, fileNameWithoutExtension, extension);
				if (dictionary.ContainsKey(key))
				{
					flag = true;
				}
				else
				{
					bool flag2 = false;
					int num = 0;
					do
					{
						NewDestName = Path.Combine(directoryName, ExpandBaseToPattern(fileNameWithoutExtension, extension, num++));
					}
					while (dictionary.Values.Where((string item) => item.ToLower() == NewDestName.ToLower()).Count() != 0);
				}
			}
		}
		else
		{
			flag = true;
		}
		if (flag)
		{
			RecycleBin.SendSilent(SourceName);
		}
		else
		{
			File.Move(SourceName, NewDestName);
		}
		return !flag;
	}

	private static Dictionary<string, string> GenerateHashForPattern(string FolderName, string BaseName, string Extension)
	{
		string searchPattern = $"{BaseName}[*{Extension}".ToLower();
		string[] files = Directory.GetFiles(FolderName, searchPattern);
		Dictionary<string, string> dictionary = new Dictionary<string, string>();
		string[] array = files;
		foreach (string text in array)
		{
			try
			{
				dictionary.Add(GenerateFileHash(text), text);
			}
			catch (ArgumentException)
			{
			}
		}
		string text2 = Path.ChangeExtension(Path.Combine(FolderName, BaseName), Extension);
		try
		{
			dictionary.Add(GenerateFileHash(text2), text2);
		}
		catch (ArgumentException)
		{
		}
		return dictionary;
	}

	private static string ExpandBaseToPattern(string BaseName, string Extension, int Number)
	{
		return $"{BaseName}[{Number:X4}]{Extension}";
	}

	public static string GenerateFileHash(string FileName)
	{
		string result = "";
		if (!File.Exists(FileName))
		{
			return "";
		}
		Stream stream = File.OpenRead(FileName);
		try
		{
			result = GenerateFileHash(stream);
		}
		finally
		{
			stream.Close();
		}
		return result;
	}

	public static string GenerateFileHash(Stream Source)
	{
		SHA1 sHA = new SHA1CryptoServiceProvider();
		byte[] array = sHA.ComputeHash(Source);
		sHA.Dispose();
		StringBuilder stringBuilder = new StringBuilder();
		byte[] array2 = array;
		foreach (byte b in array2)
		{
			stringBuilder.Append($"{b:x2}");
		}
		return stringBuilder.ToString();
	}

	public static string FormatFileSize(long FileSize)
	{
		if (FileSize < 1024)
		{
			return $"{FileSize} bytes";
		}
		if (FileSize < 1048576)
		{
			return $"{FileSize / 1024} KB";
		}
		return $"{FileSize / 1048576} MB";
	}

	[DllImport("Shlwapi.dll", CharSet = CharSet.Unicode)]
	private static extern long StrFormatByteSize(long fileSize, [MarshalAs(UnmanagedType.LPTStr)] StringBuilder buffer, int bufferSize);

	public static string FormatFileSizeAPI(long FileSize)
	{
		StringBuilder stringBuilder = new StringBuilder(128);
		StrFormatByteSize(FileSize, stringBuilder, 128);
		return stringBuilder.ToString();
	}
}
