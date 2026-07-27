using System.Collections.Generic;
using System.IO;
using System.Text;

namespace S3ForgeTools.GameFiles.Resources;

public class ResourceBONE
{
	public List<string> Names { get; private set; }

	public ResourceBONE()
	{
		Names = new List<string>();
	}

	public ResourceBONE(Stream Source)
		: this()
	{
		BinaryReader binaryReader = new BinaryReader(Source);
		uint num = binaryReader.ReadUInt32();
		uint num2 = binaryReader.ReadUInt32();
		for (int i = 0; i < num2; i++)
		{
			int count = binaryReader.ReadSByte();
			Names.Add(Encoding.BigEndianUnicode.GetString(binaryReader.ReadBytes(count)));
		}
	}
}
