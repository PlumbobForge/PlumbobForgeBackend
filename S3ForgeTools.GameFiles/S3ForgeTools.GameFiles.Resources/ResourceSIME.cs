using System.Collections.Generic;
using System.IO;
using S3ForgeTools.GameFiles.Exportable;
using S3ForgeTools.GameFiles.Package;

namespace S3ForgeTools.GameFiles.Resources;

public class ResourceSIME
{
	public List<TGI_Key> TGITable { get; private set; }

	public int DefaultOutfitKey { get; private set; }

	public uint SimFlags { get; private set; }

	public string FirstName { get; private set; }

	public string LastName { get; private set; }

	public string Biography { get; private set; }

	public uint FavoriteMusic { get; private set; }

	public uint FavoriteFood { get; private set; }

	public uint FavoriteColor { get; private set; }

	public uint ZodiacSign { get; private set; }

	public List<ulong> Traits { get; private set; }

	public uint LifeTimeWish { get; private set; }

	public ResourceSIME(string Filename)
	{
		Stream stream = File.OpenRead(Filename);
		try
		{
			Import(stream);
		}
		finally
		{
			stream.Close();
		}
	}

	public ResourceSIME(Stream Source)
	{
		Import(Source);
	}

	public ResourceSIME(byte[] buffer)
	{
		Stream stream = new MemoryStream(buffer, writable: false);
		try
		{
			Import(stream);
		}
		finally
		{
			stream.Close();
		}
	}

	private void Import(Stream Source)
	{
		Traits = new List<ulong>();
		TGITable = new List<TGI_Key>();
		BinaryReader binaryReader = new BinaryReader(Source);
		ushort num = binaryReader.ReadUInt16();
		ushort num2 = binaryReader.ReadUInt16();
		for (int i = 0; i < num2; i++)
		{
			ulong instance = binaryReader.ReadUInt64();
			uint group = binaryReader.ReadUInt32();
			uint type = binaryReader.ReadUInt32();
			TGI_Key item = new TGI_Key(type, group, instance);
			TGITable.Add(item);
		}
		ushort num3 = binaryReader.ReadUInt16();
		uint count = binaryReader.ReadUInt32();
		ExportableReader exportableReader = new ExportableReader(binaryReader.ReadBytes((int)count));
		byte[] array = binaryReader.ReadBytes(5);
		SimFlags = exportableReader.ReadUint32(1758328370u);
		DefaultOutfitKey = exportableReader.ReadInt32(4180891374u);
		FirstName = exportableReader.ReadString(3947983776u);
		LastName = exportableReader.ReadString(1883753236u);
		FavoriteColor = exportableReader.ReadUint32(2418364207u);
		FavoriteFood = exportableReader.ReadUint32(904967806u);
		FavoriteMusic = exportableReader.ReadUint32(376622899u);
		try
		{
			ZodiacSign = exportableReader.ReadUint32(176483828u);
		}
		catch (KeyNotFoundException)
		{
			ZodiacSign = 255u;
		}
		LifeTimeWish = exportableReader.ReadUint32(2216711505u);
		try
		{
			_ = exportableReader.ReadUint32(1533688765u);
			ulong[] array2 = exportableReader.ReadUInt64List(1769582843u);
			ulong[] array3 = array2;
			foreach (ulong item2 in array3)
			{
				Traits.Add(item2);
			}
		}
		catch (KeyNotFoundException)
		{
			// Value missing, default behavior handled
		}
		if (Biography == "")
		{
			Biography = "Test Bio";
		}
	}
}
