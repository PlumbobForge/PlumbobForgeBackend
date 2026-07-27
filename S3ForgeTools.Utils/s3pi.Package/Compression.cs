using System;
using System.IO;
using Tiger;
using s3pi.Settings;

namespace s3pi.Package;

internal static class Compression
{
	private static bool checking = s3pi.Settings.Settings.Checking;

	public static byte[] UncompressStream(Stream stream, int filesize, int memsize)
	{
		BinaryReader binaryReader = new BinaryReader(stream);
		long num = stream.Position + filesize;
		byte[] array = new byte[memsize];
		BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream(array));
		byte[] array2 = binaryReader.ReadBytes(2);
		if (checking && array2.Length != 2)
		{
			throw new InvalidDataException("Hit unexpected end of file at " + stream.Position);
		}
		int num2 = (((array2[0] & 0x80) != 0) ? 4 : 3) * (((array2[0] & 1) == 0) ? 1 : 2);
		array2 = binaryReader.ReadBytes(num2);
		if (checking && array2.Length != num2)
		{
			throw new InvalidDataException("Hit unexpected end of file at " + stream.Position);
		}
		long num3 = 0L;
		for (int i = 0; i < array2.Length; i++)
		{
			num3 = (num3 << 8) + array2[i];
		}
		if (checking && num3 != memsize)
		{
			throw new InvalidDataException(string.Format("Resource data indicates size does not match index at 0x{0}.  Read 0x{1}.  Expected 0x{2}.", stream.Position.ToString("X8"), num3.ToString("X8"), memsize.ToString("X8")));
		}
		while (stream.Position < num)
		{
			Dechunk(stream, binaryWriter);
		}
		if (checking && binaryWriter.BaseStream.Position != memsize)
		{
			throw new InvalidDataException($"Read 0x{binaryWriter.BaseStream.Position:X8} bytes.  Expected 0x{memsize:X8}.");
		}
		binaryWriter.Close();
		return array;
	}

	public static void Dechunk(Stream stream, BinaryWriter bw)
	{
		BinaryReader binaryReader = new BinaryReader(stream);
		int num = 0;
		int num2 = 0;
		byte b = binaryReader.ReadByte();
		int num3;
		if (b < 128)
		{
			byte[] array = binaryReader.ReadBytes(1);
			if (checking && array.Length != 1)
			{
				throw new InvalidDataException("Hit unexpected end of file at " + stream.Position);
			}
			num3 = b & 3;
			num = ((b >> 2) & 7) + 3;
			num2 = (((b << 3) & 0x300) | array[0]) + 1;
		}
		else if (b < 192)
		{
			byte[] array = binaryReader.ReadBytes(2);
			if (checking && array.Length != 2)
			{
				throw new InvalidDataException("Hit unexpected end of file at " + stream.Position);
			}
			num3 = (array[0] >> 6) & 3;
			num = (b & 0x3F) + 4;
			num2 = (((array[0] << 8) & 0x3F00) | array[1]) + 1;
		}
		else if (b >= 224)
		{
			num3 = ((b >= 252) ? (b & 3) : ((b & 0x1F) + 1 << 2));
		}
		else
		{
			byte[] array = binaryReader.ReadBytes(3);
			if (checking && array.Length != 3)
			{
				throw new InvalidDataException("Hit unexpected end of file at " + stream.Position);
			}
			num3 = b & 3;
			num = (((b << 6) & 0x300) | array[2]) + 5;
			num2 = (((b << 12) & 0x10000) | (array[0] << 8) | array[1]) + 1;
		}
		if (num3 > 0)
		{
			byte[] array = binaryReader.ReadBytes(num3);
			if (checking && array.Length != num3)
			{
				throw new InvalidDataException("Hit unexpected end of file at " + stream.Position);
			}
			bw.Write(array);
		}
		if (checking && num2 > bw.BaseStream.Position)
		{
			throw new InvalidDataException($"Invalid copy offset 0x{num2:X8} at {stream.Position}.");
		}
		if (num < num2 && num2 > 8)
		{
			CopyA(bw.BaseStream, num2, num);
		}
		else
		{
			CopyB(bw.BaseStream, num2, num);
		}
	}

	private static void CopyA(Stream s, int offset, int len)
	{
		while (len > 0)
		{
			long position = s.Position;
			byte[] array = new byte[Math.Min(offset, len)];
			len -= array.Length;
			s.Position -= offset;
			s.Read(array, 0, array.Length);
			s.Position = position;
			s.Write(array, 0, array.Length);
		}
	}

	private static void CopyB(Stream s, int offset, int len)
	{
		while (len > 0)
		{
			long position = s.Position;
			len--;
			s.Position -= offset;
			byte value = (byte)s.ReadByte();
			s.Position = position;
			s.WriteByte(value);
		}
	}

	public static byte[] CompressStream(byte[] data)
	{
		byte[] compressed;
		return DBPFCompression.Compress(data, out compressed) ? compressed : data;
	}
}
