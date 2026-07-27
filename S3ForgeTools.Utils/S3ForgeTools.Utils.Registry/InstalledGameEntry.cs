using System.Reflection;
using Microsoft.Win32;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.Utils.Registry;

public class InstalledGameEntry
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	public string RegistryName { get; private set; }

	public string Country { get; private set; }

	public string DisplayName { get; private set; }

	public string ExePath { get; private set; }

	public string InstallDir { get; private set; }

	public int InstallStart { get; private set; }

	public string Locale { get; private set; }

	public int ProductID { get; private set; }

	public int SKU { get; private set; }

	public int Telemetery { get; private set; }

	public bool IsTool => GetIsTool();

	public bool IsGame => GetIsGame();

	public bool IsBaseGame => GetIsBaseGame();

	public InstalledGameEntry(RegistryKey Key, string KeyName)
	{
		log.Debug("InstalledGameEntry .ctor");
		RegistryKey registryKey = Key.OpenSubKey(KeyName);
		try
		{
			RegistryName = KeyName;
			Country = (string)registryKey.GetValue("Country", "");
			DisplayName = (string)registryKey.GetValue("DisplayName", "");
			ExePath = (string)registryKey.GetValue("ExePath", "");
			InstallDir = (string)registryKey.GetValue("Install Dir");
			Locale = (string)registryKey.GetValue("Locale", "");
			InstallStart = (int)registryKey.GetValue("InstallStart", 0);
			ProductID = (int)registryKey.GetValue("ProductID", 0);
			SKU = (int)registryKey.GetValue("SKU", 0);
			Telemetery = (int)registryKey.GetValue("Telemetry", 0);
			if (!RegistryName.StartsWith("The Sims 3 Create a") && ((ProductID == 0) | (ProductID == 1000) | (ProductID == 1001)))
			{
				ProductID = 1;
			}
		}
		finally
		{
			registryKey.Close();
		}
		log.Info($"Loaded Game Pack {DisplayName}");
	}

	internal bool GetIsTool()
	{
		return SKU == 0;
	}

	internal bool GetIsGame()
	{
		log.Info("GetIsGame()");
		if (RegistryName.StartsWith("The Sims 3 Create a"))
		{
			return false;
		}
		return RegistryName.StartsWith("The Sims 3");
	}

	internal bool GetIsBaseGame()
	{
		return IsGame & ((ProductID == 0) | (ProductID == 1) | (ProductID == 1000) | (ProductID == 1001));
	}
}
