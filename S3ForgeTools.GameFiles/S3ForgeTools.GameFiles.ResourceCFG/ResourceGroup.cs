using System.Collections.Generic;
using System.Reflection;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.GameFiles.ResourceCFG;

public class ResourceGroup
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	public List<ResourceCFG> ConfigFiles;

	public List<ResourceCFGEntry> Entries;

	public ResourceGroup()
	{
		log.Debug(".ctor");
		ConfigFiles = new List<ResourceCFG>();
		Entries = new List<ResourceCFGEntry>();
	}

	public void AddResourceCFG(ResourceCFG ResCFG)
	{
		ConfigFiles.Add(ResCFG);
		foreach (ResourceCFGEntry entry in ResCFG.Entries)
		{
			Entries.Add(entry);
		}
	}

	public ResourceCFG AddResourceCFG(string FileName)
	{
		ResourceCFG resourceCFG = new ResourceCFG(FileName);
		AddResourceCFG(resourceCFG);
		return resourceCFG;
	}
}
