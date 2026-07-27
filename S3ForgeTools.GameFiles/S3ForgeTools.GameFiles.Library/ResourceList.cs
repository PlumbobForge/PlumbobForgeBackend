using System;
using System.Collections.Generic;
using System.Linq;
using S3ForgeTools.GameFiles.Package;

namespace S3ForgeTools.GameFiles.Library;

public class ResourceList
{
	private List<ResourceEntry> Duplicates;

	private Dictionary<int, ResourceEntry> List;

	public ResourceEntry Value => GetValue();

	public TGI_Key Key { get; private set; }

	private ResourceEntry GetValue()
	{
		int key = ((IEnumerable<int>)List.Keys).Max<int>();
		return List[key];
	}

	public ResourceList(TGI_Key Key)
	{
		List = new Dictionary<int, ResourceEntry>();
		Duplicates = new List<ResourceEntry>();
		this.Key = Key;
	}

	public void AddResource(ResourceEntry Entry, int Priority)
	{
		if (Entry.Key != Key)
		{
			throw new ArgumentException();
		}
		if (List.ContainsKey(Priority))
		{
			Duplicates.Add(Entry);
		}
		else
		{
			List.Add(Priority, Entry);
		}
	}
}
