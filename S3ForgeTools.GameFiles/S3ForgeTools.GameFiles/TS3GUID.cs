using System;
using System.Text;

namespace S3ForgeTools.GameFiles;

public class TS3GUID : IComparable<TS3GUID>, IComparable<string>
{
	public byte[] Value { get; private set; }

	public TS3GUID(byte[] Value)
	{
		this.Value = Value;
	}

	public TS3GUID(byte[] Left, byte[] right)
	{
		Left.CopyTo(Value, 8);
		right.CopyTo(Value, 0);
	}

	public TS3GUID(string Value)
	{
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder(48);
		for (int i = 8; i < 16; i++)
		{
			stringBuilder.Append($"{Value[i]:x}");
		}
		for (int i = 0; i < 8; i++)
		{
			stringBuilder.Append($"{Value[i]:x}");
		}
		return stringBuilder.ToString();
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is TS3GUID)
		{
			if ((obj as TS3GUID).Value != Value)
			{
				return false;
			}
			return true;
		}
		return false;
	}

	public override int GetHashCode()
	{
		return ToString().GetHashCode();
	}

	public static bool operator ==(TS3GUID a, TS3GUID b)
	{
		if (object.ReferenceEquals(a, b))
		{
			return true;
		}
		if ((object)a == null || (object)b == null)
		{
			return false;
		}
		return a.Value == b.Value;
	}

	public static bool operator !=(TS3GUID a, TS3GUID b)
	{
		return !(a == b);
	}

	public int CompareTo(string B)
	{
		return ToString().CompareTo(B);
	}

	public int CompareTo(TS3GUID B)
	{
		return ToString().CompareTo(B.ToString());
	}
}
