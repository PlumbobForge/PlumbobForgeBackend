using System;
using System.IO;
using s3pi.Interfaces;
using s3pi.Settings;

namespace s3pi.Package;

public class ResourceIndexEntry : AResourceIndexEntry
{
	private const int recommendedApiVersion = 2;

	private byte[] indexEntry = null;

	private MemoryStream ms = null;

	private BinaryReader indexReader = null;

	private BinaryWriter indexWriter = null;

	private bool isDeleted = false;

	private Stream resourceStream = null;

	public override int RecommendedApiVersion => 2;

	[MaximumVersion(2)]
	[MinimumVersion(1)]
	public override uint ResourceType
	{
		get
		{
			ms.Position = 4L;
			return indexReader.ReadUInt32();
		}
		set
		{
			ms.Position = 4L;
			indexWriter.Write(value);
			OnElementChanged();
		}
	}

	[MaximumVersion(2)]
	[MinimumVersion(1)]
	public override uint ResourceGroup
	{
		get
		{
			ms.Position = 8L;
			return indexReader.ReadUInt32();
		}
		set
		{
			ms.Position = 8L;
			indexWriter.Write(value);
			OnElementChanged();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(2)]
	public override ulong Instance
	{
		get
		{
			ms.Position = 12L;
			return ((ulong)indexReader.ReadUInt32() << 32) | indexReader.ReadUInt32();
		}
		set
		{
			ms.Position = 12L;
			indexWriter.Write((uint)(value >> 32));
			indexWriter.Write((uint)(value & 0xFFFFFFFFu));
			OnElementChanged();
		}
	}

	[MaximumVersion(2)]
	[MinimumVersion(1)]
	public override uint Chunkoffset
	{
		get
		{
			ms.Position = 20L;
			return indexReader.ReadUInt32();
		}
		set
		{
			ms.Position = 20L;
			indexWriter.Write(value);
			OnElementChanged();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(2)]
	public override uint Filesize
	{
		get
		{
			ms.Position = 24L;
			return indexReader.ReadUInt32() & 0x7FFFFFFF;
		}
		set
		{
			ms.Position = 24L;
			indexWriter.Write(value | 0x80000000u);
			OnElementChanged();
		}
	}

	[MaximumVersion(2)]
	[MinimumVersion(1)]
	public override uint Memsize
	{
		get
		{
			ms.Position = 28L;
			return indexReader.ReadUInt32();
		}
		set
		{
			ms.Position = 28L;
			indexWriter.Write(value);
			OnElementChanged();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(2)]
	public override ushort Compressed
	{
		get
		{
			ms.Position = 32L;
			return indexReader.ReadUInt16();
		}
		set
		{
			ms.Position = 32L;
			indexWriter.Write(value);
			OnElementChanged();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(2)]
	public override ushort Unknown2
	{
		get
		{
			ms.Position = 34L;
			return indexReader.ReadUInt16();
		}
		set
		{
			ms.Position = 34L;
			indexWriter.Write(value);
			OnElementChanged();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(2)]
	public override Stream Stream => ms;

	[MinimumVersion(1)]
	[MaximumVersion(2)]
	public override bool IsDeleted
	{
		get
		{
			return isDeleted;
		}
		set
		{
			if (isDeleted != value)
			{
				isDeleted = value;
				OnElementChanged();
			}
		}
	}

	internal Stream ResourceStream
	{
		get
		{
			return resourceStream;
		}
		set
		{
			if (resourceStream != value)
			{
				resourceStream = value;
				if (Memsize != (uint)resourceStream.Length)
				{
					Memsize = (uint)resourceStream.Length;
				}
				else
				{
					OnElementChanged();
				}
			}
		}
	}

	internal bool IsDirty => dirty;

	[MinimumVersion(1)]
	[MaximumVersion(2)]
	public override AHandlerElement Clone(EventHandler handler)
	{
		return new ResourceIndexEntry(indexEntry);
	}

	private ResourceIndexEntry(byte[] indexEntry)
	{
		this.indexEntry = (byte[])indexEntry.Clone();
		ms = new MemoryStream(this.indexEntry);
		indexReader = new BinaryReader(ms);
		indexWriter = new BinaryWriter(ms);
	}

	internal ResourceIndexEntry(int[] header, int[] entry)
	{
		indexEntry = new byte[(header.Length + entry.Length) * 4];
		ms = new MemoryStream(indexEntry);
		BinaryWriter binaryWriter = new BinaryWriter(ms);
		binaryWriter.Write(header[0]);
		int i = 1;
		int j = 0;
		Boolset boolset = (uint)header[0];
		binaryWriter.Write(boolset[0] ? header[i++] : entry[j++]);
		binaryWriter.Write(boolset[1] ? header[i++] : entry[j++]);
		binaryWriter.Write(boolset[2] ? header[i++] : entry[j++]);
		for (; i < header.Length - 1; i++)
		{
			binaryWriter.Write(header[i]);
		}
		for (; j < entry.Length; j++)
		{
			binaryWriter.Write(entry[j]);
		}
		indexReader = new BinaryReader(ms);
		indexWriter = new BinaryWriter(ms);
	}

	internal ResourceIndexEntry Clone()
	{
		return (ResourceIndexEntry)Clone(null);
	}

	internal void Delete()
	{
		if (s3pi.Settings.Settings.Checking && isDeleted)
		{
			throw new InvalidOperationException("Index entry already deleted!");
		}
		isDeleted = true;
		OnElementChanged();
	}
}
