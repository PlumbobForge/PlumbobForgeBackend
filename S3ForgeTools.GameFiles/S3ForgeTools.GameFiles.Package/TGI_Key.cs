using System;
using System.Globalization;

namespace S3ForgeTools.GameFiles.Package;

public class TGI_Key : IComparable<TGI_Key>, IComparable<string>
{
	public uint Type { get; private set; }

	public uint Group { get; private set; }

	public ulong Instance { get; private set; }

	public TGI_Key(uint Type, uint Group, ulong Instance)
	{
		this.Type = Type;
		this.Group = Group;
		this.Instance = Instance;
	}

	public TGI_Key(string Keyvalue)
	{
		if (!(Keyvalue == ""))
		{
			if (Keyvalue.ToLower().StartsWith("key:"))
			{
				Keyvalue = Keyvalue.Substring(4);
			}
			string s = Keyvalue.Substring(0, 8);
			string s2 = Keyvalue.Substring(9, 8);
			string text = Keyvalue.Substring(18);
			string s3 = text.Substring(0, 8);
			string s4 = text.Substring(8);
			Type = uint.Parse(s, NumberStyles.HexNumber);
			Group = uint.Parse(s2, NumberStyles.HexNumber);
			Instance = (ulong.Parse(s3, NumberStyles.HexNumber) << 32) | ulong.Parse(s4, NumberStyles.HexNumber);
		}
	}

	public override bool Equals(object obj)
	{
		if (obj == null)
		{
			return false;
		}
		if (obj is TGI_Key)
		{
			if ((obj as TGI_Key).Type != Type)
			{
				return false;
			}
			if ((obj as TGI_Key).Group != Group)
			{
				return false;
			}
			if ((obj as TGI_Key).Instance != Instance)
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

	public static bool operator ==(TGI_Key a, TGI_Key b)
	{
		if (object.ReferenceEquals(a, b))
		{
			return true;
		}
		if ((object)a == null || (object)b == null)
		{
			return false;
		}
		return a.Type == b.Type && a.Group == b.Group && a.Instance == b.Instance;
	}

	public static bool operator !=(TGI_Key a, TGI_Key b)
	{
		return !(a == b);
	}

	public override string ToString()
	{
		return $"{Type:x8}-{Group:x8}-{Instance:x16}";
	}

	public int CompareTo(string B)
	{
		return ToString().CompareTo(B);
	}

	public int CompareTo(TGI_Key B)
	{
		int num = Type.CompareTo(B.Type);
		if (num != 0)
		{
			return num;
		}
		num = Group.CompareTo(B.Group);
		if (num != 0)
		{
			return num;
		}
		return Instance.CompareTo(B.Instance);
	}
}
