using System;
using System.Collections.Generic;
using System.IO;
using s3pi.Interfaces;

namespace s3pi.Package;

internal class PackageIndex : List<IResourceIndexEntry>
{
	private const int numFields = 9;

	private Boolset indextype = 0u;

	public uint Indextype => indextype;

	private int Hdrsize
	{
		get
		{
			int num = 1;
			for (int i = 0; i < indextype.Length; i++)
			{
				if (indextype[i])
				{
					num++;
				}
			}
			return num;
		}
	}

	public int Size => (base.Count * (9 - Hdrsize) + Hdrsize) * 4;

	public IResourceIndexEntry this[uint type, uint group, ulong instance]
	{
		get
		{
			using (Enumerator enumerator = GetEnumerator())
			{
				while (enumerator.MoveNext())
				{
					ResourceIndexEntry resourceIndexEntry = (ResourceIndexEntry)enumerator.Current;
					if (resourceIndexEntry.ResourceType != type || resourceIndexEntry.ResourceGroup != group || resourceIndexEntry.Instance != instance)
					{
						continue;
					}
					return resourceIndexEntry;
				}
			}
			return null;
		}
	}

	public IResourceIndexEntry this[IResourceKey rk] => this[rk.ResourceType, rk.ResourceGroup, rk.Instance];

	public PackageIndex()
	{
	}

	public PackageIndex(uint type)
	{
		indextype = type;
	}

	public PackageIndex(Stream s, int indexposition, int indexsize, int indexcount)
	{
		if (s == null || indexposition == 0)
		{
			return;
		}
		BinaryReader binaryReader = new BinaryReader(s);
		s.Position = indexposition;
		indextype = binaryReader.ReadUInt32();
		int[] array = new int[Hdrsize];
		int[] array2 = new int[9 - Hdrsize];
		array[0] = (ushort)indextype;
		for (int i = 1; i < array.Length; i++)
		{
			array[i] = binaryReader.ReadInt32();
		}
		for (int i = 0; i < indexcount; i++)
		{
			for (int j = 0; j < array2.Length; j++)
			{
				array2[j] = binaryReader.ReadInt32();
			}
			base.Add((IResourceIndexEntry)new ResourceIndexEntry(array, array2));
		}
	}

	public IResourceIndexEntry Add(IResourceKey rk)
	{
		ResourceIndexEntry resourceIndexEntry = new ResourceIndexEntry(new int[Hdrsize], new int[9 - Hdrsize]);
		resourceIndexEntry.ResourceType = rk.ResourceType;
		resourceIndexEntry.ResourceGroup = rk.ResourceGroup;
		resourceIndexEntry.Instance = rk.Instance;
		resourceIndexEntry.Chunkoffset = uint.MaxValue;
		resourceIndexEntry.Unknown2 = 1;
		resourceIndexEntry.ResourceStream = null;
		base.Add((IResourceIndexEntry)resourceIndexEntry);
		return resourceIndexEntry;
	}

	public void Save(BinaryWriter w)
	{
		BinaryReader binaryReader = null;
		binaryReader = ((base.Count != 0) ? new BinaryReader(base[0].Stream) : new BinaryReader(new MemoryStream(new byte[36])));
		binaryReader.BaseStream.Position = 4L;
		w.Write((int)(ushort)indextype);
		if (indextype[0])
		{
			w.Write(binaryReader.ReadUInt32());
		}
		else
		{
			binaryReader.BaseStream.Position += 4L;
		}
		if (indextype[1])
		{
			w.Write(binaryReader.ReadUInt32());
		}
		else
		{
			binaryReader.BaseStream.Position += 4L;
		}
		if (indextype[2])
		{
			w.Write(binaryReader.ReadUInt32());
		}
		else
		{
			binaryReader.BaseStream.Position += 4L;
		}
		using Enumerator enumerator = GetEnumerator();
		while (enumerator.MoveNext())
		{
			IResourceIndexEntry current = enumerator.Current;
			binaryReader = new BinaryReader(current.Stream);
			binaryReader.BaseStream.Position = 4L;
			if (!indextype[0])
			{
				w.Write(binaryReader.ReadUInt32());
			}
			else
			{
				binaryReader.BaseStream.Position += 4L;
			}
			if (!indextype[1])
			{
				w.Write(binaryReader.ReadUInt32());
			}
			else
			{
				binaryReader.BaseStream.Position += 4L;
			}
			if (!indextype[2])
			{
				w.Write(binaryReader.ReadUInt32());
			}
			else
			{
				binaryReader.BaseStream.Position += 4L;
			}
			w.Write(binaryReader.ReadBytes((int)(current.Stream.Length - current.Stream.Position)));
		}
	}

	public void Sort(string index)
	{
		Sort(new AApiVersionedFields.Comparer<IResourceIndexEntry>(index));
	}
}
