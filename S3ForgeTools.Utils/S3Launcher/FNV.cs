using System.Text;

namespace S3Launcher;

public static class FNV
{
	private static ulong Prime64 = 1099511628211uL;

	private static ulong Offset64 = 14695981039346656037uL;

	private static uint Prime32 = 16777619u;

	private static uint Offset32 = 2166136261u;

	public static uint FNV32(string Value)
	{
		uint num = Offset32;
		byte[] bytes = Encoding.ASCII.GetBytes(Value.ToLower());
		byte[] array = bytes;
		foreach (byte b in array)
		{
			num *= Prime32;
			num ^= b;
		}
		return num;
	}

	public static ulong FNV64(string Value)
	{
		ulong num = Offset64;
		byte[] bytes = Encoding.ASCII.GetBytes(Value.ToLower());
		byte[] array = bytes;
		foreach (byte b in array)
		{
			num *= Prime64;
			num ^= b;
		}
		return num;
	}
}
