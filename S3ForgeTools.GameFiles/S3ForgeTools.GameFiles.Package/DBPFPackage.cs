using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using S3ForgeTools.Utils.Logging;

namespace S3ForgeTools.GameFiles.Package;

public class DBPFPackage : IDisposable
{
	private static readonly ILog log = LogManager.GetLogger(MethodBase.GetCurrentMethod().DeclaringType.ToString());

	private Stream DataStore;

	private bool _IsEncrypted;

	public static readonly byte[] EncryptKey = new byte[96]
	{
		0, 0, 0, 22, 173, 32, 164, 122, 42, 238,
		220, 183, 77, 94, 169, 53, 91, 117, 38, 34,
		51, 186, 96, 88, 153, 162, 194, 136, 112, 59,
		89, 5, 138, 180, 247, 135, 234, 173, 5, 90,
		175, 140, 62, 24, 127, 218, 30, 212, 162, 133,
		172, 108, 95, 113, 100, 176, 205, 3, 114, 80,
		40, 88, 195, 99, 92, 31, 131, 189, 116, 246,
		107, 238, 191, 84, 70, 168, 220, 236, 223, 150,
		36, 225, 162, 99, 193, 251, 29, 255, 107, 5,
		186, 191, 64, 136, 95, 251
	};

	private bool disposed = false;

	public bool IsEncrypted => _IsEncrypted;

	public List<ResourceEntry> Resources { get; private set; }

	public string FileName { get; private set; }

	public string GUID { get; set; }

	public DBPFPackage()
	{
		Resources = new List<ResourceEntry>();
	}

	public DBPFPackage(Stream SourceStream)
		: this()
	{
		Import(SourceStream);
	}

	public DBPFPackage(string FileName)
		: this()
	{
		Import(FileName);
	}

	public void Close()
	{
		Dispose();
	}

	public void Clear()
	{
		if (DataStore != null)
		{
			DataStore.Close();
			DataStore = null;
		}
		foreach (ResourceEntry resource in Resources)
		{
			resource.Close();
		}
	}

	public void Export(string FileName)
	{
		Stream stream = File.Create(FileName);
		try
		{
			Export(stream);
		}
		finally
		{
			stream.Close();
		}
	}

	public void Export(Stream OutStream)
	{
		DataStore.Seek(0L, SeekOrigin.Begin);
		DataStore.CopyTo(OutStream);
	}

	public void Import(string FileName)
	{
		DataStore = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
		try
		{
			this.FileName = FileName;
			Import();
		}
		catch (InvalidDataException)
		{
			DataStore.Close();
			throw;
		}
	}

	public void Import(Stream SourceStream)
	{
		FileName = "<Stream>";
		Import(SourceStream, UseBuffer: true);
	}

	public void Import(Stream SourceStream, bool UseBuffer)
	{
		if (UseBuffer)
		{
			DataStore = new MemoryStream();
			SourceStream.CopyTo(DataStore);
		}
		else
		{
			DataStore = SourceStream;
		}
		Import();
	}

	private void Import()
	{
		DataStore.Seek(0L, SeekOrigin.Begin);
		byte[] array = new byte[96];
		DataStore.ReadExactly(array, 0, 96);
		string @string = Encoding.ASCII.GetString(array, 0, 4);
		if ((@string != "DBPF") & (@string != "DBPP"))
		{
			throw new InvalidDataException("Unknown Magic Number: " + @string);
		}
		if (@string == "DBPP")
		{
			_IsEncrypted = true;
			throw new InvalidDataException("DBPP Not Supported");
		}
		if (IsEncrypted)
		{
			for (int i = 0; i < 96; i++)
			{
				array[i] ^= EncryptKey[i];
			}
		}
		BinaryReader binaryReader = new BinaryReader(new MemoryStream(array, writable: false));
		binaryReader.BaseStream.Position = 4L;
		uint num = binaryReader.ReadUInt32();
		uint num2 = binaryReader.ReadUInt32();
		uint num3 = binaryReader.ReadUInt32();
		uint num4 = binaryReader.ReadUInt32();
		uint num5 = binaryReader.ReadUInt32();
		uint num6 = binaryReader.ReadUInt32();
		uint num7 = binaryReader.ReadUInt32();
		uint num8 = binaryReader.ReadUInt32();
		uint num9 = binaryReader.ReadUInt32();
		uint num10 = binaryReader.ReadUInt32();
		uint num11 = binaryReader.ReadUInt32();
		uint num12 = binaryReader.ReadUInt32();
		uint num13 = binaryReader.ReadUInt32();
		uint num14 = binaryReader.ReadUInt32();
		uint num15 = binaryReader.ReadUInt32();
		uint num16 = binaryReader.ReadUInt32();
		if (num != 2 || num2 != 0)
		{
			log.Warn($"Unknown DBPF Version {num}.{num2}, Expected 2.0 -- {FileName}");
			throw new InvalidDataException($"Unknown File Version: {num}.{num2}, Expected 2.0");
		}
		if ((num8 != 0 && num8 != 7) || num15 != 3)
		{
			log.Warn($"Unknown Index Version {num8}.{num15}, Expected 0.3 -- {FileName}");
		}
		if (num3 != 0 || num4 != 0 || num5 != 0 || num6 != 0 || num7 != 0 || num12 != 0 || num14 != 0)
		{
			log.Warn($"Unused Header Value not set to 0 -- {FileName}");
		}
		if (num10 != 0 && num10 != num16)
		{
			log.Warn($"Header Offset mismatch -- Not loadable by Game! -- {FileName}");
		}
		array = new byte[num11];
		DataStore.Position = num16;
		DataStore.ReadExactly(array, 0, (int)num11);
		binaryReader = new BinaryReader(new MemoryStream(array, writable: false));
		uint type = 0u;
		uint group = 0u;
		ulong num17 = 0uL;
		uint num18 = uint.MaxValue;
		uint num19 = uint.MaxValue;
		if (num9 == 0)
		{
			log.Info("Empty Package -- No Index Entries");
			return;
		}
		uint num20 = binaryReader.ReadUInt32();
		int num21 = 0;
		if ((num20 & 1) == 1)
		{
			num21 += 4;
		}
		if ((num20 & 2) == 2)
		{
			num21 += 4;
		}
		if ((num20 & 4) == 4)
		{
			num21 += 4;
		}
		if ((num20 & 8) == 8)
		{
			num21 += 4;
		}
		int num22 = num21 + 4;
		num22 += (32 - num21) * (int)num9;
		if (num22 != num11)
		{
			log.Warn($"DBPF Format Error, IndexType vs IndexSize mismatch: Actual Size {num11}, Calculated Size {num22}, IndexType {num20} -- {FileName}");
			throw new InvalidDataException("DBPF Format Error, IndexType vs IndexSize mismatch");
		}
		if ((num20 | 0xF) != 15)
		{
			log.Fatal("Import not implemented");
			throw new NotImplementedException();
		}
		if ((num20 & 1) == 1)
		{
			type = binaryReader.ReadUInt32();
		}
		if ((num20 & 2) == 2)
		{
			group = binaryReader.ReadUInt32();
		}
		if ((num20 & 4) == 4)
		{
			num18 = binaryReader.ReadUInt32();
		}
		if ((num20 & 8) == 8)
		{
			num19 = binaryReader.ReadUInt32();
		}
		for (int i = 0; i < num9; i++)
		{
			if ((num20 & 1) == 0)
			{
				type = binaryReader.ReadUInt32();
			}
			if ((num20 & 2) == 0)
			{
				group = binaryReader.ReadUInt32();
			}
			if ((num20 & 4) == 0)
			{
				num18 = binaryReader.ReadUInt32();
			}
			if ((num20 & 8) == 0)
			{
				num19 = binaryReader.ReadUInt32();
			}
			num17 = num19 | ((ulong)num18 << 32);
			uint num23 = binaryReader.ReadUInt32();
			uint chunkLength = binaryReader.ReadUInt32() & 0x7FFFFFFF;
			uint resourceLength = binaryReader.ReadUInt32();
			ushort num24 = binaryReader.ReadUInt16();
			ushort num25 = binaryReader.ReadUInt16();
			TGI_Key key = new TGI_Key(type, group, num17);
			ResourceEntry item = new ResourceEntry(DataStore, key, num23, (int)chunkLength, (int)resourceLength, num24 == ushort.MaxValue, IsEncrypted);
			Resources.Add(item);
		}
	}

	public void CopyTo(Stream OutputStream)
	{
		DataStore.Position = 0L;
		DataStore.CopyTo(OutputStream);
	}

	public void CopyTo(string FileName)
	{
		Stream stream = File.Open(FileName, FileMode.OpenOrCreate, FileAccess.Write);
		try
		{
			CopyTo(stream);
		}
		finally
		{
			stream.Close();
		}
	}

	public ResourceEntry GetResource(TGI_Key Key)
	{
		IEnumerable<ResourceEntry> enumerable = Resources.Where((ResourceEntry Item) => Item.Key == Key);
		using (IEnumerator<ResourceEntry> enumerator = enumerable.GetEnumerator())
		{
			if (enumerator.MoveNext())
			{
				return enumerator.Current;
			}
		}
		return null;
	}

	public TGI_Key GetCompositionResource()
	{
		Dictionary<TGI_Key, int> compositionResources = GetCompositionResources();
		TGI_Key result = new TGI_Key(0u, 0u, 0uL);
		int num = -1;
		foreach (KeyValuePair<TGI_Key, int> item in compositionResources)
		{
			if (item.Value > num)
			{
				num = item.Value;
				result = item.Key;
			}
		}
		return result;
	}

	public Dictionary<TGI_Key, int> GetCompositionResources()
	{
		Dictionary<TGI_Key, int> dictionary = new Dictionary<TGI_Key, int>();
		foreach (ResourceEntry resource in Resources)
		{
			try
			{
				if ((resource.Key.Type == 107542056) | (resource.Key.Type == 83396964) | (resource.Key.Type == 103306152))
				{
					dictionary.Add(resource.Key, 750);
				}
				else if ((resource.Key.Type == 3496170587u) | (resource.Key.Type == 832458525) | (resource.Key.Type == 3571055589u) | (resource.Key.Type == 55242443))
				{
					dictionary.Add(resource.Key, 500);
				}
				else if ((resource.Key.Type == 53690476) | (resource.Key.Type == 62078431) | (resource.Key.Type == 121612807))
				{
					dictionary.Add(resource.Key, 250);
				}
			}
			catch (ArgumentException)
			{
			}
		}
		return dictionary;
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

	~DBPFPackage()
	{
		Dispose(disposing: false);
	}
}
