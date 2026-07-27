using System;
using System.Collections.Generic;
using System.Reflection;
using S3ForgeTools.GameFiles.Package;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.GameFiles.Library;

public class ResourceIndex : IDisposable
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	private Dictionary<TGI_Key, ResourceList> ResourceData;

	private bool disposed = false;

	public IList<ResourceEntry> Entries => GetResourceList();

	public ResourceIndex()
	{
		ResourceData = new Dictionary<TGI_Key, ResourceList>();
	}

	private IList<ResourceEntry> GetResourceList()
	{
		List<ResourceEntry> list = new List<ResourceEntry>();
		foreach (ResourceList value in ResourceData.Values)
		{
			list.Add(value.Value);
		}
		return list;
	}

	public void Close()
	{
		Dispose();
	}

	public void Clear()
	{
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposed)
		{
			if (disposing)
			{
				Clear();
			}
			disposed = true;
		}
	}

	~ResourceIndex()
	{
		Dispose(disposing: false);
	}

	public void AddResources(List<ResourceEntry> Resources, int Priority)
	{
		foreach (ResourceEntry Resource in Resources)
		{
			if (ResourceData.ContainsKey(Resource.Key))
			{
				ResourceData[Resource.Key].AddResource(Resource, Priority);
				continue;
			}
			ResourceList resourceList = new ResourceList(Resource.Key);
			resourceList.AddResource(Resource, Priority);
			ResourceData.Add(Resource.Key, resourceList);
		}
	}

	public ResourceEntry GetResource(TGI_Key Key)
	{
		if (!ResourceData.ContainsKey(Key))
		{
			throw new ArgumentException();
		}
		return ResourceData[Key].Value;
	}
}
