using System.Reflection;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.GameFiles.ResourceCFG;

public class ResourceCFGEntry
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	public int Priority { get; private set; }

	public string PackageFileName { get; private set; }

	public override string ToString()
	{
		return $"[{Priority}] {PackageFileName}";
	}

	public ResourceCFGEntry(int Priority, string FileName)
	{
		this.Priority = Priority;
		PackageFileName = FileName;
	}
}
