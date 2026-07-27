using System;
using System.Collections.Generic;
using System.IO;

namespace s3pi.Interfaces;

public abstract class APackage : AApiVersionedFields, IPackage, IApiVersion, IContentFields
{
	public override List<string> ContentFields => AApiVersionedFields.GetContentFields(requestedApiVersion, GetType());

	public abstract byte[] Magic { get; }

	public abstract int Major { get; }

	public abstract int Minor { get; }

	public abstract byte[] Unknown1 { get; }

	public abstract int Indexcount { get; }

	public abstract byte[] Unknown2 { get; }

	public abstract int Indexsize { get; }

	public abstract byte[] Unknown3 { get; }

	public abstract int Indexversion { get; }

	public abstract int Indexposition { get; }

	public abstract byte[] Unknown4 { get; }

	public abstract Stream HeaderStream { get; }

	public abstract uint Indextype { get; }

	public abstract IList<IResourceIndexEntry> GetResourceList { get; }

	public event EventHandler ResourceIndexInvalidated;

	public abstract void SavePackage();

	public abstract void SaveAs(Stream s);

	public abstract void SaveAs(string path);

	public abstract IResourceIndexEntry Find(uint flags, IResourceIndexEntry values);

	public abstract IResourceIndexEntry Find(string[] names, TypedValue[] values);

	public abstract IList<IResourceIndexEntry> FindAll(uint flags, IResourceIndexEntry values);

	public abstract IList<IResourceIndexEntry> FindAll(string[] names, TypedValue[] values);

	public abstract IResourceIndexEntry AddResource(IResourceKey rk, Stream stream, bool rejectDups);

	public abstract void ReplaceResource(IResourceIndexEntry rc, IResource res);

	public abstract void DeleteResource(IResourceIndexEntry rc);

	public static IPackage NewPackage(int APIversion)
	{
		throw new NotImplementedException();
	}

	public static IPackage OpenPackage(int APIversion, string packagePath)
	{
		throw new NotImplementedException();
	}

	public static IPackage OpenPackage(int APIversion, string packagePath, bool readwrite)
	{
		throw new NotImplementedException();
	}

	public static void ClosePackage(int APIversion, IPackage pkg)
	{
		throw new NotImplementedException();
	}

	public abstract Stream GetResource(IResourceIndexEntry rie);

	protected virtual void OnResourceIndexInvalidated(object sender, EventArgs e)
	{
		if (this.ResourceIndexInvalidated != null)
		{
			this.ResourceIndexInvalidated(sender, e);
		}
	}
}
