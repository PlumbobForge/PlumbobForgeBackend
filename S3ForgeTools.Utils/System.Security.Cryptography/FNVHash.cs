using System.Text;

namespace System.Security.Cryptography;

public abstract class FNVHash : HashAlgorithm
{
	private ulong prime;

	private ulong offset;

	protected ulong hash;

	protected FNVHash(ulong prime, ulong offset)
	{
		this.prime = prime;
		this.offset = offset;
		hash = offset;
	}

	public byte[] ComputeHash(string value)
	{
		return ComputeHash(Encoding.ASCII.GetBytes(value.ToLowerInvariant()));
	}

	public override void Initialize()
	{
	}

	protected override void HashCore(byte[] array, int ibStart, int cbSize)
	{
		for (int i = ibStart; i < ibStart + cbSize; i++)
		{
			hash *= prime;
			hash ^= array[i];
		}
	}

	protected override byte[] HashFinal()
	{
		return BitConverter.GetBytes(hash);
	}
}
