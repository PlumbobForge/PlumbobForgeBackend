using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using S3ForgeTools.GameFiles.Package;
using S3ForgeTools.GameFiles.ResourceCFG;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.GameFiles.Library;

public class GameLibrary : IDisposable
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	private List<DBPFPackage> Packages;

	private bool disposed = false;

	public ResourceIndex Index { get; private set; }

	public List<string> PackageFilenames { get; private set; }

	public GameLibrary()
	{
		Index = new ResourceIndex();
		Packages = new List<DBPFPackage>();
		PackageFilenames = new List<string>();
	}

	public DBPFPackage Add(ResourceCFGEntry Entry)
	{
		return Add(Entry.PackageFileName, Entry.Priority);
	}

	public DBPFPackage Add(string FileName, int Prioirty = 0)
	{
		if (File.Exists(FileName))
		{
			try
			{
				DBPFPackage dBPFPackage = new DBPFPackage(FileName);
				Add(dBPFPackage, Prioirty);
				return dBPFPackage;
			}
			catch (InvalidDataException)
			{
				return null;
			}
		}
		return null;
	}

	public void Add(DBPFPackage Package, int Priority = 0)
	{
		Index.AddResources(Package.Resources, Priority);
		if (Package.FileName != null)
		{
			PackageFilenames.Add(Package.FileName);
		}
		Packages.Add(Package);
	}

	public bool ContainsPackage(string FileName)
	{
		return PackageFilenames.Contains(FileName);
	}

	public void Close()
	{
		Dispose();
	}

	public void Clear()
	{
		Index.Close();
		foreach (DBPFPackage package in Packages)
		{
			package.Close();
		}
		Packages.Clear();
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

	~GameLibrary()
	{
		Dispose(disposing: false);
	}

	public ResourceEntry GetResource(TGI_Key Key)
	{
		return Index.GetResource(Key);
	}
}
