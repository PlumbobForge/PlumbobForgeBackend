namespace System.Security.Cryptography;

public class FNV64 : FNVHash
{
	public override byte[] Hash => BitConverter.GetBytes(hash);

	public override int HashSize => 64;

	public FNV64()
		: base(1099511628211uL, 14695981039346656037uL)
	{
	}

	public static ulong GetHash(string text)
	{
		return BitConverter.ToUInt64(new FNV64().ComputeHash(text), 0);
	}
}
