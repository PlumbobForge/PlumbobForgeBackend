using System;
using System.IO;

namespace S3ForgeTools.GameFiles.Package;

public class ResourceEntry : IDisposable
{
	private long _ChunkOffset;

	private int _ChunkLength;

	private int _ResourceLength;

	private Stream DataStore;

	private bool _IsEncrypted;

	private bool disposed = false;

	public TGI_Key Key { get; private set; }

	public bool IsCompressed { get; private set; }

	public bool IsEncrypted => _IsEncrypted;

	public long Length => _ResourceLength;

	public int Priority { get; set; }

	public ResourceEntry(Stream Datastore, TGI_Key Key, long ChunkOffset, int ChunkLength, int ResourceLength, bool IsCompressed, bool IsEncrypted)
	{
		DataStore = Datastore;
		this.Key = Key;
		_ChunkOffset = ChunkOffset;
		_ChunkLength = ChunkLength;
		_ResourceLength = ResourceLength;
		this.IsCompressed = IsCompressed;
		_IsEncrypted = IsEncrypted;
	}

	public ResourceEntry(Stream DataStore, TGI_Key Key)
	{
		this.DataStore = DataStore;
		this.Key = Key;
		_ChunkOffset = 0L;
		_ChunkLength = (int)DataStore.Length;
		_ResourceLength = (int)DataStore.Length;
		IsCompressed = false;
		_IsEncrypted = false;
	}

	public void Close()
	{
		Dispose();
	}

	public void Clear()
	{
		throw new NotImplementedException();
	}

	public byte[] Read()
	{
		if (!IsCompressed)
		{
			return ReadRaw();
		}
		byte[] array = Compression.UncompressStream(new MemoryStream(ReadRaw(), writable: false), _ChunkLength, _ResourceLength);
		if (array.Length != _ResourceLength)
		{
			throw new InvalidDataException("Decompression Failure");
		}
		return array;
	}

	public byte[] ReadRaw()
	{
		byte[] array = new byte[_ChunkLength];
		lock (DataStore)
		{
			DataStore.Position = _ChunkOffset;
			if (DataStore.Read(array, 0, _ChunkLength) != _ChunkLength)
			{
				throw new InvalidDataException("Unexpected End of Data");
			}
		}
		return array;
	}

	public Stream GetStream(bool WantRaw = false)
	{
		if (WantRaw)
		{
			return new MemoryStream(ReadRaw(), writable: false);
		}
		return new MemoryStream(Read(), writable: false);
	}

	public void Export(BinaryWriter Writer, uint IndexType)
	{
		if (IndexType != 0)
		{
			throw new ArgumentException();
		}
		Writer.Write(Key.Type);
		Writer.Write(Key.Group);
		uint value = (uint)(Key.Instance & 0xFFFFFFFFu);
		uint value2 = (uint)((Key.Instance & 0xFFFFFFFF00000000uL) >> 32);
		Writer.Write(value2);
		Writer.Write(value);
		Writer.Write((uint)_ChunkOffset);
		Writer.Write((uint)(_ChunkLength | int.MinValue));
		Writer.Write((uint)Length);
		if (IsCompressed)
		{
			Writer.Write(ushort.MaxValue);
		}
		else
		{
			Writer.Write((ushort)0);
		}
		Writer.Write((ushort)1);
	}

	public bool Compress(int level = 1)
	{
		return ChangeCompression(true, level);
	}

	public bool Decompress()
	{
		return ChangeCompression(false, 1);
	}

	public void ChangeStream(Stream NewStream)
	{
		DataStore = NewStream;
		IsCompressed = false;
		_ResourceLength = (int)DataStore.Length;
		_ChunkLength = (int)DataStore.Length;
		_ChunkOffset = 0L;
	}

	private bool ChangeCompression(bool Compress, int level)
	{
		if (Compress == IsCompressed)
		{
			return false;
		}
		if (Compress)
		{
			byte[] array = Read();
			byte[] buffer = Compression.CompressStream(array, level);
			DataStore = new MemoryStream(buffer, writable: false);
			IsCompressed = true;
			_ChunkOffset = 0L;
			_ChunkLength = (int)DataStore.Length;
			_ResourceLength = array.Length;
			return true;
		}
		Stream stream = GetStream();
		DataStore = stream;
		IsCompressed = false;
		_ChunkOffset = 0L;
		_ChunkLength = (int)DataStore.Length;
		_ResourceLength = (int)DataStore.Length;
		return true;
	}

	public override string ToString()
	{
		if (Key != null)
		{
			return Key.ToString();
		}
		return base.ToString();
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
			}
			disposed = true;
		}
	}

	~ResourceEntry()
	{
		Dispose(disposing: false);
	}
}
