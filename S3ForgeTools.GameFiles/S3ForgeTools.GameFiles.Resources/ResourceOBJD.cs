using System.IO;
using System.Text;
using S3ForgeTools.GameFiles.Package;

namespace S3ForgeTools.GameFiles.Resources;

public class ResourceOBJD
{
	public string InstanceName { get; private set; }

	public string Name { get; private set; }

	public string Description { get; private set; }

	public float Price { get; private set; }

	public TGI_Key ThumbKey { get; private set; }

	public ResourceOBJD(string Filename)
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

	public ResourceOBJD(Stream Source)
	{
		Import(Source);
	}

	public ResourceOBJD(byte[] buffer)
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
		BinaryReader binaryReader = new BinaryReader(Source, Encoding.BigEndianUnicode);
		uint num = binaryReader.ReadUInt32();
		binaryReader.ReadUInt32();
		binaryReader.ReadUInt32();
		ReadMatBlock(binaryReader);
		if (num >= 22)
		{
			InstanceName = binaryReader.ReadString();
		}
		ReadCommonBlock(binaryReader);
	}

	private void ReadMatBlock(BinaryReader Reader, bool IsWallFloor = false)
	{
		uint num = Reader.ReadUInt32();
		for (int i = 0; i < num; i++)
		{
			byte b = Reader.ReadByte();
			if (b != 1)
			{
				Reader.ReadUInt32();
			}
			uint num2 = Reader.ReadUInt32();
			Reader.BaseStream.Seek(num2, SeekOrigin.Current);
			Reader.ReadUInt32();
			if (IsWallFloor)
			{
				Reader.ReadUInt32();
				Reader.ReadUInt32();
				Reader.ReadUInt32();
			}
		}
	}

	private void ReadCommonBlock(BinaryReader Reader)
	{
		uint num = Reader.ReadUInt32();
		Reader.ReadUInt64();
		Reader.ReadUInt64();
		Name = Reader.ReadString();
		Description = Reader.ReadString();
		Name = Name.Replace("CatalogObjects/Name:", "");
		Description = Description.Replace("CatalogObjects/Description:", "");
		Price = Reader.ReadSingle();
		Reader.ReadSingle();
		Reader.ReadSingle();
		Reader.ReadByte();
	}
}
