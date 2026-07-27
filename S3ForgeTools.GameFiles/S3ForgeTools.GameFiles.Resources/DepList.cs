using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using S3ForgeTools.GameFiles.Package;

namespace S3ForgeTools.GameFiles.Resources;

public class DepList
{
	private Dictionary<TS3GUID, DepListEntry> Entries;

	public DepList()
	{
		Entries = new Dictionary<TS3GUID, DepListEntry>();
	}

	public DepList(byte[] Source)
		: this()
	{
		MemoryStream memoryStream = new MemoryStream(Source, writable: false);
		try
		{
			Import(memoryStream);
		}
		finally
		{
			memoryStream.Close();
		}
	}

	public DepList(Stream Source)
		: this()
	{
		Import(Source);
	}

	private void Import(Stream Source)
	{
		BinaryReader binaryReader = new BinaryReader(Source);
		uint num = binaryReader.ReadUInt32().Swap();
		for (int i = 0; i < num; i++)
		{
			byte[] array = binaryReader.ReadBytes(5);
			uint num2 = binaryReader.ReadUInt32().Swap();
			uint num3 = binaryReader.ReadUInt32().Swap();
			TS3GUID packageID = new TS3GUID(binaryReader.ReadBytes(16));
			DepListEntry depListEntry = new DepListEntry(packageID);
			for (int j = 0; j < num2; j++)
			{
				depListEntry.AddDependency(new TS3GUID(binaryReader.ReadBytes(16)));
			}
			depListEntry.SetPackageType(binaryReader.ReadUInt32().Swap(), binaryReader.ReadUInt32().Swap());
			ushort num4 = binaryReader.ReadUInt16();
			uint type;
			uint group;
			uint num5;
			uint num6;
			ulong instance;
			for (int j = 0; j < num3; j++)
			{
				type = binaryReader.ReadUInt32().Swap();
				group = binaryReader.ReadUInt32().Swap();
				num5 = binaryReader.ReadUInt32().Swap();
				num6 = binaryReader.ReadUInt32().Swap();
				instance = num5 + ((ulong)num6 << 32);
				TGI_Key key = new TGI_Key(type, group, instance);
				type = binaryReader.ReadUInt32().Swap();
				group = binaryReader.ReadUInt32().Swap();
				num5 = binaryReader.ReadUInt32().Swap();
				num6 = binaryReader.ReadUInt32().Swap();
				instance = num5 + ((ulong)num6 << 32);
				TGI_Key key2 = new TGI_Key(type, group, instance);
				depListEntry.AddResource(key, key2);
			}
			int num7 = (int)binaryReader.ReadUInt32().Swap();
			depListEntry.Name = Encoding.BigEndianUnicode.GetString(binaryReader.ReadBytes(num7 * 2));
			binaryReader.ReadBytes(4);
			type = binaryReader.ReadUInt32().Swap();
			group = binaryReader.ReadUInt32().Swap();
			num5 = binaryReader.ReadUInt32().Swap();
			num6 = binaryReader.ReadUInt32().Swap();
			instance = num5 + ((ulong)num6 << 32);
			depListEntry.Thumbnail = new TGI_Key(type, group, instance);
			Console.WriteLine(depListEntry.Thumbnail);
			byte b = binaryReader.ReadByte();
			uint num8;
			do
			{
				num8 = binaryReader.ReadUInt32().Swap();
				if (num8 != 0)
				{
					depListEntry.AddExtraData(Encoding.ASCII.GetString(binaryReader.ReadBytes((int)num8)));
				}
			}
			while (num8 != 0);
			Entries.Add(depListEntry.PackageID, depListEntry);
		}
	}

	public bool ContainsID(TS3GUID PackageID)
	{
		return Entries.ContainsKey(PackageID);
	}
}
