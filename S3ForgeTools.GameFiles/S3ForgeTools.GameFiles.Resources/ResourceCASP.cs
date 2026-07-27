using System.IO;
using System.Text;

namespace S3ForgeTools.GameFiles.Resources;

public class ResourceCASP
{
	public string Name { get; private set; }

	public uint ClothingType { get; private set; }

	public uint TypeFlags { get; private set; }

	public uint AgeGender { get; private set; }

	public uint Category { get; private set; }

	public ResourceCASP(string Filename)
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

	public ResourceCASP(Stream Source)
	{
		Import(Source);
	}

	public ResourceCASP(byte[] buffer)
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
		uint num = binaryReader.ReadUInt32();
		binaryReader.ReadUInt32();
		uint num2 = binaryReader.ReadUInt32();
		for (int i = 0; i < num2; i++)
		{
			uint num3 = binaryReader.ReadUInt32();
			string @string = Encoding.Unicode.GetString(binaryReader.ReadBytes((int)(num3 * 2)));
			binaryReader.ReadUInt32();
		}
		ushort count = binaryReader.ReadUInt16();
		Name = Encoding.Unicode.GetString(binaryReader.ReadBytes(count));
		binaryReader.ReadSingle();
		ClothingType = binaryReader.ReadUInt32();
		TypeFlags = binaryReader.ReadUInt32();
		AgeGender = binaryReader.ReadUInt32();
		Category = binaryReader.ReadUInt32();
	}
}
