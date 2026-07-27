using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using S3ForgeTools.GameFiles.Package;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.GameFiles.TS3Pack;

internal class Sims3PackBuilder : IDisposable
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	private Stream DataStore;

	private bool _IsEncrypted;

	private bool disposed = false;

	public bool IsEncrypted => _IsEncrypted;

	public bool IsModified { get; private set; }

	public long PackageSize => GetPackageSize();

	public List<ResourceEntry> Resources { get; private set; }

	public Sims3PackBuilder(string FileName, bool IsEncrypted = false)
	{
		Resources = new List<ResourceEntry>();
		_IsEncrypted = IsEncrypted;
		DataStore = File.Open(FileName, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
		GenerateBlank();
	}

	public Sims3PackBuilder(Stream SourceStream, bool IsEncrypted)
	{
		Resources = new List<ResourceEntry>();
		_IsEncrypted = IsEncrypted;
		DataStore = SourceStream;
		GenerateBlank();
	}

	public void Close()
	{
		Dispose();
	}

	private void CloseState()
	{
		if (IsModified)
		{
			GenerateIndex();
		}
		foreach (ResourceEntry resource in Resources)
		{
			resource.Close();
		}
		Resources.Clear();
		DataStore.Close();
		DataStore = null;
	}

	private long GetPackageSize()
	{
		if (DataStore == null)
		{
			return 0L;
		}
		return DataStore.Length;
	}

	public void AddResource(ResourceEntry Resource)
	{
		if (IsEncrypted != Resource.IsEncrypted)
		{
			throw new InvalidDataException("Encryption State Mismatch");
		}
		byte[] array = Resource.ReadRaw();
		long length = DataStore.Length;
		int num = array.Length;
		int resourceLength = (int)Resource.Length;
		DataStore.Position = DataStore.Length;
		DataStore.Write(array, 0, num);
		DataStore.Flush();
		ResourceEntry item = new ResourceEntry(DataStore, Resource.Key, length, num, resourceLength, Resource.IsCompressed, Resource.IsEncrypted);
		Resources.Add(item);
		IsModified = true;
	}

	private void GenerateBlank()
	{
		byte[] array = new byte[96];
		for (int i = 0; i < 96; i++)
		{
			array[i] = 0;
		}
		BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream(array, 0, 96, writable: true));
		binaryWriter.Write(new char[4] { 'D', 'B', 'P', 'F' });
		binaryWriter.Write(2u);
		binaryWriter.BaseStream.Position = 60L;
		binaryWriter.Write(3u);
		if (IsEncrypted)
		{
			for (int i = 0; i < 96; i++)
			{
				array[i] ^= DBPFPackage.EncryptKey[i];
			}
		}
		DataStore.Write(array, 0, 96);
		DataStore.Flush();
		binaryWriter.Close();
	}

	private void GenerateIndex()
	{
		uint num = (uint)DataStore.Length;
		DataStore.Position = DataStore.Length;
		BinaryWriter binaryWriter = new BinaryWriter(DataStore);
		binaryWriter.Write(0u);
		foreach (ResourceEntry resource in Resources)
		{
			resource.Export(binaryWriter, 0u);
		}
		uint num2 = (uint)(DataStore.Position - num);
		binaryWriter.BaseStream.Position = 36L;
		if (IsEncrypted)
		{
			binaryWriter.Write((uint)((ulong)Resources.Count ^ 0x5A05ADEAuL));
		}
		else
		{
			binaryWriter.Write((uint)Resources.Count);
		}
		binaryWriter.BaseStream.Position = 44L;
		if (IsEncrypted)
		{
			binaryWriter.Write(num2 ^ 0xD41EDA7Fu);
		}
		else
		{
			binaryWriter.Write(num2);
		}
		binaryWriter.BaseStream.Position = 64L;
		if (IsEncrypted)
		{
			binaryWriter.Write(num ^ 0xBD831F5Cu);
		}
		else
		{
			binaryWriter.Write(num);
		}
		IsModified = false;
		binaryWriter.Flush();
		binaryWriter = null;
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
				CloseState();
			}
			disposed = true;
		}
	}

	~Sims3PackBuilder()
	{
		Dispose(disposing: false);
	}
}
