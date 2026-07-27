using System.Collections.Generic;
using S3ForgeTools.GameFiles.Package;

namespace S3ForgeTools.GameFiles.Resources;

public class DepListEntry
{
	public TS3GUID PackageID { get; private set; }

	public uint PackageType { get; private set; }

	public uint PackageSubType { get; private set; }

	public List<TS3GUID> Dependencies { get; private set; }

	public List<TGI_Key> Resources { get; private set; }

	public List<string> ExtraData { get; private set; }

	public string Name { get; set; }

	public TGI_Key Thumbnail { get; set; }

	public DepListEntry(TS3GUID PackageID)
	{
		this.PackageID = PackageID;
		Dependencies = new List<TS3GUID>();
		Resources = new List<TGI_Key>();
		ExtraData = new List<string>();
	}

	public void SetPackageType(uint Type, uint SubType)
	{
		PackageType = Type;
		PackageSubType = SubType;
	}

	public void AddDependency(TS3GUID GUID)
	{
		Dependencies.Add(GUID);
	}

	public void AddResource(TGI_Key Key1, TGI_Key Key2)
	{
		Resources.Add(Key1);
	}

	public void AddExtraData(string Value)
	{
		ExtraData.Add(Value);
	}
}
