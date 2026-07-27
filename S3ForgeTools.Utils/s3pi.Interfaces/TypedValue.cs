using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace s3pi.Interfaces;

[Serializable]
public class TypedValue : IComparable<TypedValue>, IEqualityComparer<TypedValue>, IEquatable<TypedValue>, IConvertible, ICloneable, ISerializable
{
	public readonly Type Type;

	public readonly object Value;

	private string format = "";

	private static readonly string[] LowNames = new string[32]
	{
		"NUL", "SOH", "STX", "ETX", "EOT", "ENQ", "ACK", "BEL", "BS", "HT",
		"LF", "VT", "FF", "CR", "SO", "SI", "DLE", "DC1", "DC2", "DC3",
		"DC4", "NAK", "SYN", "ETB", "CAN", "EM", "SUB", "ESC", "FS", "GS",
		"RS", "US"
	};

	public TypedValue(Type t, object v)
		: this(t, v, "")
	{
	}

	public TypedValue(Type t, object v, string f)
	{
		Type = t;
		Value = v;
		format = f;
	}

	public static implicit operator string(TypedValue tv)
	{
		return tv.ToString(tv.format);
	}

	public override string ToString()
	{
		return ToString(format);
	}

	public string ToString(string format)
	{
		if (format == "X")
		{
			if (Type == typeof(long))
			{
				return "0x" + ((long)Value).ToString("X16");
			}
			if (Type == typeof(ulong))
			{
				return "0x" + ((ulong)Value).ToString("X16");
			}
			if (Type == typeof(int))
			{
				return "0x" + ((int)Value).ToString("X8");
			}
			if (Type == typeof(uint))
			{
				return "0x" + ((uint)Value).ToString("X8");
			}
			if (Type == typeof(short))
			{
				return "0x" + ((short)Value).ToString("X4");
			}
			if (Type == typeof(ushort))
			{
				return "0x" + ((ushort)Value).ToString("X4");
			}
			if (Type == typeof(sbyte))
			{
				return "0x" + ((sbyte)Value).ToString("X2");
			}
			if (Type == typeof(byte))
			{
				return "0x" + ((byte)Value).ToString("X2");
			}
			if (typeof(Enum).IsAssignableFrom(Type))
			{
				TypedValue typedValue = new TypedValue(Enum.GetUnderlyingType(Type), Value, "X");
				return string.Format("{0} ({1})", ((string)typedValue) ?? "", string.Concat(Value));
			}
		}
		if (typeof(string).IsAssignableFrom(Type))
		{
			string text = (string)Value;
			if (text.IndexOf('\0') != -1)
			{
				return (text.Length % 2 == 0) ? ToANSIString(text) : ToDisplayString(text.ToCharArray());
			}
			return text.Normalize();
		}
		if (typeof(char[]).IsAssignableFrom(Type))
		{
			return ToDisplayString((char[])Value);
		}
		if (typeof(Array).IsAssignableFrom(Type))
		{
			return FromArray((Array)Value);
		}
		return Value.ToString();
	}

	private static string ToANSIString(string unicode)
	{
		StringBuilder stringBuilder = new StringBuilder();
		for (int i = 0; i < unicode.Length; i += 2)
		{
			stringBuilder.Append((char)(((uint)unicode[i] << 8) + unicode[i + 1]));
		}
		return stringBuilder.ToString().Normalize();
	}

	private static string FromArray(Array ary)
	{
		string text = "";
		int num = 0;
		foreach (object item in ary)
		{
			TypedValue typedValue = new TypedValue(item.GetType(), item, "X");
			text += string.Format(" [{0:X}:'{1}']", num++, ((string)typedValue) ?? "");
		}
		return text.TrimStart();
	}

	private static string ToDisplayString(char[] text)
	{
		string text2 = "";
		foreach (char c in text)
		{
			text2 = ((c >= ' ') ? ((c < '\u007f') ? (text2 + c) : (text2 + $"<U+{(int)c:X4}>")) : (text2 + $"<{LowNames[(uint)c]}>"));
		}
		return text2;
	}

	public int CompareTo(TypedValue other)
	{
		if (!Type.IsAssignableFrom(other.Type) || !(Type is IComparable) || !(other.Type is IComparable))
		{
			throw new NotImplementedException();
		}
		return ((IComparable)Value).CompareTo((IComparable)other.Value);
	}

	public bool Equals(TypedValue x, TypedValue y)
	{
		return x.Equals(y);
	}

	public int GetHashCode(TypedValue obj)
	{
		return obj.GetHashCode();
	}

	public bool Equals(TypedValue other)
	{
		return Value.Equals(other.Value);
	}

	public TypeCode GetTypeCode()
	{
		return TypeCode.String;
	}

	public bool ToBoolean(IFormatProvider provider)
	{
		if (typeof(bool).IsAssignableFrom(Type))
		{
			return (bool)Value;
		}
		throw new NotImplementedException();
	}

	public byte ToByte(IFormatProvider provider)
	{
		if (typeof(byte).IsAssignableFrom(Type))
		{
			return (byte)Value;
		}
		throw new NotImplementedException();
	}

	public char ToChar(IFormatProvider provider)
	{
		if (typeof(char).IsAssignableFrom(Type))
		{
			return (char)Value;
		}
		throw new NotImplementedException();
	}

	public DateTime ToDateTime(IFormatProvider provider)
	{
		if (typeof(DateTime).IsAssignableFrom(Type))
		{
			return (DateTime)Value;
		}
		throw new NotImplementedException();
	}

	public decimal ToDecimal(IFormatProvider provider)
	{
		if (typeof(decimal).IsAssignableFrom(Type))
		{
			return (decimal)Value;
		}
		throw new NotImplementedException();
	}

	public double ToDouble(IFormatProvider provider)
	{
		if (typeof(double).IsAssignableFrom(Type))
		{
			return (double)Value;
		}
		throw new NotImplementedException();
	}

	public short ToInt16(IFormatProvider provider)
	{
		if (typeof(short).IsAssignableFrom(Type))
		{
			return (short)Value;
		}
		throw new NotImplementedException();
	}

	public int ToInt32(IFormatProvider provider)
	{
		if (typeof(int).IsAssignableFrom(Type))
		{
			return (int)Value;
		}
		throw new NotImplementedException();
	}

	public long ToInt64(IFormatProvider provider)
	{
		if (typeof(long).IsAssignableFrom(Type))
		{
			return (long)Value;
		}
		throw new NotImplementedException();
	}

	public sbyte ToSByte(IFormatProvider provider)
	{
		if (typeof(sbyte).IsAssignableFrom(Type))
		{
			return (sbyte)Value;
		}
		throw new NotImplementedException();
	}

	public float ToSingle(IFormatProvider provider)
	{
		if (typeof(float).IsAssignableFrom(Type))
		{
			return (float)Value;
		}
		throw new NotImplementedException();
	}

	public string ToString(IFormatProvider provider)
	{
		if (typeof(string).IsAssignableFrom(Type))
		{
			return (string)Value;
		}
		throw new NotImplementedException();
	}

	public object ToType(Type conversionType, IFormatProvider provider)
	{
		if (conversionType.IsAssignableFrom(Type))
		{
			return Convert.ChangeType(Value, conversionType, provider);
		}
		throw new NotImplementedException();
	}

	public ushort ToUInt16(IFormatProvider provider)
	{
		if (typeof(ushort).IsAssignableFrom(Type))
		{
			return (ushort)Value;
		}
		throw new NotImplementedException();
	}

	public uint ToUInt32(IFormatProvider provider)
	{
		if (typeof(uint).IsAssignableFrom(Type))
		{
			return (uint)Value;
		}
		throw new NotImplementedException();
	}

	public ulong ToUInt64(IFormatProvider provider)
	{
		if (typeof(ulong).IsAssignableFrom(Type))
		{
			return (ulong)Value;
		}
		throw new NotImplementedException();
	}

	public object Clone()
	{
		if (typeof(ICloneable).IsAssignableFrom(Type))
		{
			return new TypedValue(Type, ((ICloneable)Value).Clone(), format);
		}
		throw new NotImplementedException();
	}

	public void GetObjectData(SerializationInfo info, StreamingContext context)
	{
		info.AddValue("Type", Type, typeof(Type));
		info.AddValue("Value", Value, Type);
		info.AddValue("format", format);
	}
}
