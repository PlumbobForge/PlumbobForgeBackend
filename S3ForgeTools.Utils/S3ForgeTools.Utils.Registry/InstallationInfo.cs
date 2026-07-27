using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.Win32;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.Utils.Registry;

public sealed class InstallationInfo
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	private static readonly Lazy<InstallationInfo> _instance = new Lazy<InstallationInfo>(() => new InstallationInfo());

	public static InstallationInfo Instance => _instance.Value;

	public List<InstalledGameEntry> Packs { get; private set; }

	public InstalledGameEntry BaseGame { get; private set; }

	public InstalledGameEntry NewestGame { get; private set; }

	public string DocumentBaseDir { get; private set; }

	public string Locale { get; private set; }

	public bool IsSteam { get; private set; }

	private InstallationInfo()
	{
		log.Debug("InstallationInfo .ctor");
		log.Info(Environment.OSVersion.ToString());
		if (Environment.Is64BitOperatingSystem)
		{
			log.Info("64 Windows");
		}
		else
		{
			log.Info("32 Windows");
		}
		if (Environment.Is64BitProcess)
		{
			log.Info("64 Process");
		}
		else
		{
			log.Info("32 Process");
		}
		Packs = new List<InstalledGameEntry>();
		LoadRegistry();
		InitEntryProperties();
		if (BaseGame == null)
		{
			Locale = "en-US";
		}
		else
		{
			Locale = BaseGame.Locale;
		}
		DocumentBaseDir = InitBaseDir(Locale);
		if (!Directory.Exists(DocumentBaseDir))
		{
			DocumentBaseDir = InitBaseDir("en-US");
			log.Warn("Locale Base Directory Not Found!  Using default en-US");
		}
	}

	private string InitBaseDir(string Locale)
	{
		log.Debug($"InitBaseDir Locale:{Locale}");
		string text = "The Sims 3";
		switch (Locale)
		{
		case "en-US":
			text = "The Sims 3";
			break;
		case "cs-CZ":
			text = "The Sims 3";
			break;
		case "da-DK":
			text = "The Sims 3";
			break;
		case "nl-NL":
			text = "De Sims 3";
			break;
		case "fi-FI":
			text = "The Sims 3";
			break;
		case "fr-FR":
			text = "Les Sims 3";
			break;
		case "de-DE":
			text = "Die Sims 3";
			break;
		case "el-GR":
			text = "The Sims 3";
			break;
		case "hu-HU":
			text = "The Sims 3";
			break;
		case "it-IT":
			text = "The Sims 3";
			break;
		case "ja-JP":
			text = "ザ・シムズ３";
			break;
		case "ko-KR":
			text = "심즈 3";
			break;
		case "no-NO":
			text = "The Sims 3";
			break;
		case "pl-PL":
			text = "The Sims 3";
			break;
		case "pt-BR":
			text = "The Sims 3";
			break;
		case "pt-PT":
			text = "Os Sims 3";
			break;
		case "ru-RU":
			text = "The Sims 3";
			break;
		case "es-ES":
			text = "Los Sims 3";
			break;
		case "es-MX":
			text = "The Sims 3";
			break;
		case "sv-SE":
			text = "The Sims 3";
			break;
		case "th-TH":
			text = "เดอะซ\u0e34มส\u0e4c 3";
			break;
		case "zh-CN":
			text = "模拟人生3";
			break;
		case "zh-TW":
			text = "模擬市民3";
			break;
		default:
			log.Warn($"Unknown Locale {Locale}");
			break;
		}
		log.Info($"Result = {text}");
		string text2 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Electronic Arts", text);
		log.Info($"Result[full] = {text2}");
		return text2;
	}

	internal void LoadRegistry()
	{
		log.Debug("LoadRegistry");
		RegistryKey registryKey = Microsoft.Win32.Registry.LocalMachine.OpenSubKey("SOFTWARE");
		try
		{
			RegistryKey registryKey2 = registryKey.OpenSubKey("Sims");
			IsSteam = false;
			if (registryKey2 == null)
			{
				registryKey2 = registryKey.OpenSubKey("Sims(Steam)");
				IsSteam = true;
			}
			if (registryKey2 == null)
			{
				return;
			}
			log.Debug("Registry Open");
			try
			{
				string[] subKeyNames = registryKey2.GetSubKeyNames();
				log.Info($"LoadRegistry -- Found {subKeyNames.Length} Game Packs");
				string[] array = subKeyNames;
				foreach (string keyName in array)
				{
					Packs.Add(new InstalledGameEntry(registryKey2, keyName));
				}
			}
			finally
			{
				registryKey2.Close();
				log.Debug("Registry Close");
			}
		}
		finally
		{
			registryKey.Close();
		}
	}

	internal void InitEntryProperties()
	{
		log.Debug("InitEntryProperties");
		if (Packs == null)
		{
			log.Debug("Packs == null");
		}
		else
		{
			log.Debug($"  {Packs.Count} Packs found");
		}
		foreach (InstalledGameEntry pack in Packs)
		{
			if (pack.IsBaseGame)
			{
				BaseGame = pack;
			}
			if (NewestGame == null)
			{
				NewestGame = pack;
			}
			else if ((pack.ProductID > NewestGame.ProductID) & (pack.ProductID < 100))
			{
				NewestGame = pack;
			}
		}
		if (BaseGame == null)
		{
			log.Debug("BaseGame == null");
		}
		else
		{
			log.Info($"BaseGame = {BaseGame.DisplayName}");
		}
		if (NewestGame == null)
		{
			log.Debug("NewestGame == null");
		}
		else
		{
			log.Info($"NewestGame = {NewestGame.DisplayName}");
		}
	}
}
