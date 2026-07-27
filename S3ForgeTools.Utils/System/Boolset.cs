using System.Collections;
using System.Collections.Generic;

namespace System;

public class Boolset : IEquatable<Boolset>, IEquatable<ulong>, IEquatable<string>, IEqualityComparer<Boolset>, IEqualityComparer<ulong>, IEqualityComparer<string>, IEnumerable<bool>, IEnumerable
{
	private bool[] bitset = null;

	public bool this[int i]
	{
		get
		{
			if (i > bitset.Length)
			{
				throw new ArgumentOutOfRangeException();
			}
			return bitset[i];
		}
		set
		{
			if (i > bitset.Length)
			{
				throw new ArgumentOutOfRangeException();
			}
			if (bitset[i] != value)
			{
				bitset[i] = value;
				OnBoolsetChanged(this, new EventArgs());
			}
		}
	}

	public int Length => bitset.Length;

	public event EventHandler BoolsetChanged;

	private Boolset(int size, ulong val)
	{
		bitset = new bool[size];
		for (int i = 0; i < size; i++)
		{
			bitset[i] = (val & (ulong)(1L << i)) != 0;
		}
	}

	public Boolset(ulong val)
		: this(64, val)
	{
	}

	public Boolset(uint val)
		: this(32, val)
	{
	}

	public Boolset(ushort val)
		: this(16, val)
	{
	}

	public Boolset(byte val)
		: this(8, val)
	{
	}

	public Boolset(string val)
	{
		bitset = new bool[val.Length];
		int num = 0;
		for (int num2 = val.Length - 1; num2 >= 0; num2--)
		{
			bitset[num++] = !val.Substring(num2, 1).Equals("0");
		}
	}

	public static implicit operator Boolset(ulong o)
	{
		return new Boolset(o);
	}

	public static implicit operator Boolset(uint o)
	{
		return new Boolset(o);
	}

	public static implicit operator Boolset(ushort o)
	{
		return new Boolset(o);
	}

	public static implicit operator Boolset(byte o)
	{
		return new Boolset(o);
	}

	public static implicit operator Boolset(string o)
	{
		return new Boolset(o);
	}

	private static ulong doOperator(Boolset t, int l)
	{
		ulong num = 0uL;
		for (int i = 0; i < l && i < t.bitset.Length; i++)
		{
			num += (ulong)((long)(t[i] ? 1 : 0) << i);
		}
		return num;
	}

	public static implicit operator ulong(Boolset t)
	{
		return doOperator(t, 64);
	}

	public static implicit operator uint(Boolset t)
	{
		return (uint)doOperator(t, 32);
	}

	public static implicit operator ushort(Boolset t)
	{
		return (ushort)doOperator(t, 16);
	}

	public static implicit operator byte(Boolset t)
	{
		return (byte)doOperator(t, 8);
	}

	public static implicit operator string(Boolset t)
	{
		string text = "";
		for (int i = 0; i < t.bitset.Length; i++)
		{
			text = (t.bitset[i] ? "1" : "0") + text;
		}
		return text;
	}

	public override string ToString()
	{
		return this;
	}

	protected virtual void OnBoolsetChanged(object sender, EventArgs e)
	{
		if (this.BoolsetChanged != null)
		{
			this.BoolsetChanged(sender, e);
		}
	}

	public bool Matches(string mask)
	{
		int num = mask.Length - 1;
		bool flag = true;
		int num2 = 0;
		while (flag && num > 0 && num2 < bitset.Length)
		{
			if (mask[num].Equals('0'))
			{
				flag = !bitset[num2];
			}
			else if (mask[num].Equals('1'))
			{
				flag = bitset[num2];
			}
			num--;
			num2++;
		}
		return flag;
	}

	public void flip(string bits)
	{
		if (bits.Length > bitset.Length)
		{
			throw new ArgumentOutOfRangeException();
		}
		for (int i = 0; i < bits.Length; i++)
		{
			if (!bits[i].Equals("0"))
			{
				flip(i);
			}
		}
	}

	public void flip(int[] bits)
	{
		foreach (int bit in bits)
		{
			flip(bit);
		}
	}

	public void flip(int bit)
	{
		bitset[bit] = !bitset[bit];
	}

	public bool Equals(Boolset other)
	{
		return ((ulong)this).Equals(other);
	}

	public bool Equals(ulong other)
	{
		return ((ulong)this).Equals(other);
	}

	public bool Equals(string other)
	{
		return Equals((Boolset)other);
	}

	public bool Equals(Boolset x, Boolset y)
	{
		return x.Equals(y);
	}

	public int GetHashCode(Boolset obj)
	{
		return ((ulong)obj).GetHashCode();
	}

	public bool Equals(ulong x, ulong y)
	{
		return x.Equals(y);
	}

	public int GetHashCode(ulong obj)
	{
		return obj.GetHashCode();
	}

	public bool Equals(string x, string y)
	{
		return ((Boolset)x).Equals((Boolset)y);
	}

	public int GetHashCode(string obj)
	{
		return ((Boolset)obj).GetHashCode();
	}

	public IEnumerator<bool> GetEnumerator()
	{
		return (IEnumerator<bool>)bitset.GetEnumerator();
	}

	IEnumerator IEnumerable.GetEnumerator()
	{
		return bitset.GetEnumerator();
	}
}
