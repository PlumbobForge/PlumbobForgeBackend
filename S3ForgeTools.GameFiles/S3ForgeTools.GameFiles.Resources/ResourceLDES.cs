using System.IO;
using System.Text;

namespace S3ForgeTools.GameFiles.Resources;

public class ResourceLDES
{
	public string Name { get; private set; }

	public string LotName { get; private set; }

	public string LotDescription { get; private set; }

	public string LotAddress { get; private set; }

	public uint LotWidth { get; private set; }

	public uint LotHeight { get; private set; }

	public uint LotType { get; private set; }

	public uint LotSubType { get; private set; }

	public float BeautifulVistaBuf { get; private set; }

	public uint LotValue { get; private set; }

	public ResourceLDES(string Filename)
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

	public ResourceLDES(Stream Source)
	{
		Import(Source);
	}

	public ResourceLDES(byte[] buffer)
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
		BinaryReader binaryReader = new BinaryReader(Source);
		ushort num = binaryReader.ReadUInt16();
		TS3GUID tS3GUID = new TS3GUID(binaryReader.ReadBytes(8));
		TS3GUID tS3GUID2 = new TS3GUID(binaryReader.ReadBytes(8));
		TS3GUID tS3GUID3 = new TS3GUID(binaryReader.ReadBytes(8));
		uint num2 = binaryReader.ReadUInt32();
		Name = Encoding.Unicode.GetString(binaryReader.ReadBytes((int)(num2 * 2)));
		if (num >= 32)
		{
			float num3 = binaryReader.ReadSingle();
			float num4 = binaryReader.ReadSingle();
		}
		float num5 = binaryReader.ReadSingle();
		float num6 = binaryReader.ReadSingle();
		float num7 = binaryReader.ReadSingle();
		float num8 = binaryReader.ReadSingle();
		LotWidth = binaryReader.ReadUInt32();
		LotHeight = binaryReader.ReadUInt32();
		uint num9 = binaryReader.ReadUInt32();
		uint num10 = binaryReader.ReadUInt32();
		float num11 = binaryReader.ReadSingle();
		float num12 = binaryReader.ReadSingle();
		uint num13 = binaryReader.ReadUInt32();
		uint num14 = binaryReader.ReadUInt32();
		num2 = binaryReader.ReadUInt32();
		LotName = Encoding.Unicode.GetString(binaryReader.ReadBytes((int)(num2 * 2)));
		num2 = binaryReader.ReadUInt32();
		LotDescription = Encoding.Unicode.GetString(binaryReader.ReadBytes((int)(num2 * 2)));
		num2 = binaryReader.ReadUInt32();
		LotAddress = Encoding.Unicode.GetString(binaryReader.ReadBytes((int)(num2 * 2)));
		LotType = binaryReader.ReadUInt32();
		if (num >= 43)
		{
			LotSubType = binaryReader.ReadUInt32();
		}
		BeautifulVistaBuf = binaryReader.ReadSingle();
		float num15 = binaryReader.ReadSingle();
		LotValue = binaryReader.ReadUInt32();
		if (num > 32)
		{
			uint num16 = binaryReader.ReadUInt32();
			byte[] array = binaryReader.ReadBytes(5);
		}
	}
}
