using System.IO;
using System.Reflection;
using S3ForgeTools.GameFiles.ResourceCFG;
using S3ForgeTools.Utils.Logging;
using S3ForgeTools.Utils.Registry;

namespace S3ForgeTools.GameFiles.Library;

public static class LibraryBuilder
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	public static GameLibrary GetLibrary(bool BasegameOnly = false)
	{
		log.Debug("GetLibrary");
		ResourceGroup resourceGroup = new ResourceGroup();
		foreach (InstalledGameEntry pack in InstallationInfo.Instance.Packs)
		{
			if (pack.IsGame && (pack.IsBaseGame || !BasegameOnly))
			{
				log.Debug($"Added Game/Pack {pack.DisplayName}");
				resourceGroup.AddResourceCFG(Path.Combine(pack.InstallDir, "game", "bin", "resource.cfg"));
				resourceGroup.AddResourceCFG(Path.Combine(pack.InstallDir, "gamedata", "win32", "resource.cfg"));
				resourceGroup.AddResourceCFG(Path.Combine(pack.InstallDir, "gamedata", "shared", "resource.cfg"));
			}
		}
		GameLibrary gameLibrary = new GameLibrary();
		foreach (ResourceCFGEntry entry in resourceGroup.Entries)
		{
			gameLibrary.Add(entry);
		}
		gameLibrary.Add(Path.Combine(InstallationInfo.Instance.BaseGame.InstallDir, "Gameplay", "GameplayData.package"));
		log.Debug($"Added {gameLibrary.PackageFilenames.Count} Packages containing {gameLibrary.Index.Entries.Count} resources");
		return gameLibrary;
	}
}
