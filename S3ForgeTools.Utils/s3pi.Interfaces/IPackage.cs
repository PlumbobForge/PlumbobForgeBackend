using System;
using System.Collections.Generic;
using System.IO;

namespace s3pi.Interfaces;

public interface IPackage : IApiVersion, IContentFields
{
	byte[] Magic { get; }

	int Major { get; }

	int Minor { get; }

	byte[] Unknown1 { get; }

	int Indexcount { get; }

	byte[] Unknown2 { get; }

	int Indexsize { get; }

	byte[] Unknown3 { get; }

	int Indexversion { get; }

	int Indexposition { get; }

	byte[] Unknown4 { get; }

	Stream HeaderStream { get; }

	uint Indextype { get; }

	IList<IResourceIndexEntry> GetResourceList { get; }

	event EventHandler ResourceIndexInvalidated;

	void SavePackage();

	void SaveAs(Stream s);

	void SaveAs(string path);

	IResourceIndexEntry Find(uint flags, IResourceIndexEntry values);

	IResourceIndexEntry Find(string[] names, TypedValue[] values);

	IList<IResourceIndexEntry> FindAll(uint flags, IResourceIndexEntry values);

	IList<IResourceIndexEntry> FindAll(string[] names, TypedValue[] values);

	IResourceIndexEntry AddResource(IResourceKey rk, Stream stream, bool rejectDups);

	void ReplaceResource(IResourceIndexEntry rc, IResource res);

	void DeleteResource(IResourceIndexEntry rc);
}
