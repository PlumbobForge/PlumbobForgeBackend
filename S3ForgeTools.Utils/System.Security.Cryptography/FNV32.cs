namespace System.Security.Cryptography;

public class FNV32 : FNVHash
{
	public override byte[] Hash => BitConverter.GetBytes((uint)hash);

	public override int HashSize => 32;

	public FNV32()
		: base(16777619uL, 2166136261uL)
	{
	}

	public static uint GetHash(string text)
	{
		return BitConverter.ToUInt32(new FNV32().ComputeHash(text), 0);
	}
}
