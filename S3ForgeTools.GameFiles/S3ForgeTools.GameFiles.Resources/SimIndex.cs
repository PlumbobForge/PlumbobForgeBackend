using System.Collections.Generic;
using System.IO;
using System.Text;
using S3ForgeTools.GameFiles.Package;

namespace S3ForgeTools.GameFiles.Resources;

public class SimIndex
{
	private List<TGI_Key> Keys;

	public List<SimIndexEntry> Entries { get; private set; }

	public SimIndex()
	{
		Entries = new List<SimIndexEntry>();
		Keys = new List<TGI_Key>();
	}

	public SimIndex(byte[] Source)
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

	public SimIndex(Stream Source)
		: this()
	{
		Import(Source);
	}

	private void Import(Stream Source)
	{
		BinaryReader binaryReader = new BinaryReader(Source);
		ushort num = binaryReader.ReadUInt16();
		uint num2 = binaryReader.ReadUInt32();
		for (int i = 0; i < num2; i++)
		{
			TGI_Key sIME = ReadKey(binaryReader);
			TS3GUID gUID = ReadGUID(binaryReader);
			binaryReader.ReadUInt32();
			TGI_Key thumbnail = ReadKey(binaryReader);
			uint num3 = binaryReader.ReadUInt32();
			string @string = Encoding.Unicode.GetString(binaryReader.ReadBytes((int)(num3 * 2)));
			SimIndexEntry simIndexEntry = new SimIndexEntry(gUID, sIME);
			simIndexEntry.SetThumbnail(thumbnail);
			simIndexEntry.SetName(@string);
			Entries.Add(simIndexEntry);
			Keys.Add(simIndexEntry.SIME);
		}
	}

	public void Export(Stream Destination)
	{
		BinaryWriter binaryWriter = new BinaryWriter(Destination);
		binaryWriter.Write((ushort)3);
		binaryWriter.Write((uint)Entries.Count);
		foreach (SimIndexEntry entry in Entries)
		{
			WriteKey(entry.SIME, binaryWriter);
			WriteGUID(entry.GUID, binaryWriter);
			binaryWriter.Write(3u);
			WriteKey(entry.Thumbnail, binaryWriter);
			binaryWriter.Write((uint)entry.Name.Length);
			binaryWriter.Write(Encoding.Unicode.GetBytes(entry.Name));
		}
	}

	private TGI_Key ReadKey(BinaryReader Reader)
	{
		uint type = Reader.ReadUInt32();
		uint group = Reader.ReadUInt32();
		ulong instance = Reader.ReadUInt64();
		return new TGI_Key(type, group, instance);
	}

	private void WriteKey(TGI_Key Key, BinaryWriter Writer)
	{
		Writer.Write(Key.Type);
		Writer.Write(Key.Group);
		Writer.Write(Key.Instance);
	}

	private TS3GUID ReadGUID(BinaryReader Reader)
	{
		byte[] array = Reader.ReadBytes(16);
		byte[] array2 = new byte[16];
		for (int num = 7; num >= 0; num--)
		{
			array2[7 - num] = array[num];
			array2[15 - num] = array[num + 8];
		}
		return new TS3GUID(array2);
	}

	private void WriteGUID(TS3GUID GUID, BinaryWriter Writer)
	{
		byte[] value = GUID.Value;
		byte[] array = new byte[16];
		for (int num = 7; num >= 0; num--)
		{
			array[7 - num] = value[num];
			array[15 - num] = value[num + 8];
		}
		Writer.Write(value);
	}

	public bool ContainsKey(TGI_Key Key)
	{
		return Keys.Contains(Key);
	}
}
