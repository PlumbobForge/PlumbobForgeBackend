using System.Collections.Generic;
using System.IO;
using System.Text;

namespace S3ForgeTools.GameFiles.Exportable;

public class ExportableReader
{
	private Dictionary<uint, uint> LookupTable;

	private BinaryReader Reader;

	public ExportableReader(byte[] Buffer)
	{
		LookupTable = new Dictionary<uint, uint>();
		Reader = new BinaryReader(new MemoryStream(Buffer, writable: false));
		ushort num = Reader.ReadUInt16();
		uint num2 = Reader.ReadUInt32();
		Reader.BaseStream.Position = num2;
		ushort num3 = Reader.ReadUInt16();
		for (int i = 0; i < num3; i++)
		{
			LookupTable.Add(Reader.ReadUInt32(), Reader.ReadUInt32());
		}
	}

	public ulong ReadUint64(uint Name)
	{
		uint key = Name ^ 0xEE28814Fu;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		return Reader.ReadUInt64();
	}

	public uint ReadUint32(uint Name)
	{
		uint key = Name ^ 0xF1288606u;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		return Reader.ReadUInt32();
	}

	public ushort ReadUint16(uint Name)
	{
		uint key = Name ^ 0xF328896Cu;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		return Reader.ReadUInt16();
	}

	public long ReadInt64(uint Name)
	{
		uint key = Name ^ 0x71568E6;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		return Reader.ReadInt64();
	}

	public int ReadInt32(uint Name)
	{
		uint key = Name ^ 0x415642B;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		return Reader.ReadInt32();
	}

	public short ReadInt16(uint Name)
	{
		uint key = Name ^ 0x21560C5;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		return Reader.ReadInt16();
	}

	public float ReadFloat(uint Name)
	{
		uint key = Name ^ 0x4EDCD7A9;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		return Reader.ReadSingle();
	}

	public string ReadString(uint Name)
	{
		uint key = Name ^ 0x15196597;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		uint num2 = Reader.ReadUInt32();
		return Encoding.Unicode.GetString(Reader.ReadBytes((int)(num2 * 2)));
	}

	public int[] ReadInt32List(uint Name)
	{
		uint key = Name ^ 0xA4744BF2u;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		uint num2 = Reader.ReadUInt32();
		int[] array = new int[num2];
		for (int i = 0; i < num2; i++)
		{
			array[i] = Reader.ReadInt32();
		}
		return array;
	}

	public long[] ReadInt64List(uint Name)
	{
		uint num = Name ^ 0xA4744BF2u;
		uint num2 = 4100045846u;
		uint num3 = num2 ^ num;
		uint num4 = LookupTable[num];
		Reader.BaseStream.Position = num4;
		uint num5 = Reader.ReadUInt32();
		long[] array = new long[num5];
		for (int i = 0; i < num5; i++)
		{
			array[i] = Reader.ReadInt64();
		}
		return array;
	}

	public uint[] ReadUInt32List(uint Name)
	{
		uint key = Name ^ 0xA4744BF2u;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		uint num2 = Reader.ReadUInt32();
		uint[] array = new uint[num2];
		for (int i = 0; i < num2; i++)
		{
			array[i] = Reader.ReadUInt32();
		}
		return array;
	}

	public ulong[] ReadUInt64List(uint Name)
	{
		uint key = Name ^ 0xBB744CBBu;
		uint num = LookupTable[key];
		Reader.BaseStream.Position = num;
		uint num2 = Reader.ReadUInt32();
		ulong[] array = new ulong[num2];
		for (int i = 0; i < num2; i++)
		{
			array[i] = Reader.ReadUInt64();
		}
		return array;
	}
}
