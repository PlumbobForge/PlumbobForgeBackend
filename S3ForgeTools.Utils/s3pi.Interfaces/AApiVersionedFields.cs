using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace s3pi.Interfaces;

public abstract class AApiVersionedFields : IApiVersion, IContentFields
{
	private class PriorityComparer : IComparer<string>
	{
		private Type t;

		public PriorityComparer(Type t)
		{
			this.t = t;
		}

		public int Compare(string x, string y)
		{
			int num = GetPriority(t, x).CompareTo(GetPriority(t, y));
			if (num == 0)
			{
				num = x.CompareTo(y);
			}
			return num;
		}
	}

	public class Comparer<T> : IComparer<T> where T : IContentFields
	{
		private string field;

		public Comparer(string field)
		{
			this.field = field;
		}

		public int Compare(T x, T y)
		{
			return x[field].CompareTo(y[field]);
		}
	}

	protected int requestedApiVersion = 0;

	private static List<string> banlist;

	public int RequestedApiVersion => requestedApiVersion;

	public abstract int RecommendedApiVersion { get; }

	public abstract List<string> ContentFields { get; }

	public virtual TypedValue this[string index]
	{
		get
		{
			string[] array = index.Split('.');
			object obj = this;
			Type type = GetType();
			string[] array2 = array;
			foreach (string name in array2)
			{
				PropertyInfo property = type.GetProperty(name);
				if (property == null)
				{
					throw new ArgumentOutOfRangeException("index", "Unexpected value received in index: " + index);
				}
				type = property.PropertyType;
				obj = property.GetValue(obj, null);
			}
			return new TypedValue(type, obj, "X");
		}
		set
		{
			string[] array = index.Split('.');
			object obj = this;
			Type type = GetType();
			PropertyInfo propertyInfo = null;
			for (int i = 0; i < array.Length; i++)
			{
				propertyInfo = type.GetProperty(array[i]);
				if (propertyInfo == null)
				{
					throw new ArgumentOutOfRangeException("index", "Unexpected value received in index: " + index);
				}
				if (i < array.Length - 1)
				{
					type = propertyInfo.PropertyType;
					obj = propertyInfo.GetValue(obj, null);
				}
			}
			propertyInfo.SetValue(obj, value.Value, null);
		}
	}

	static AApiVersionedFields()
	{
		Type typeFromHandle = typeof(AApiVersionedFields);
		banlist = new List<string>();
		PropertyInfo[] properties = typeFromHandle.GetProperties();
		foreach (PropertyInfo propertyInfo in properties)
		{
			banlist.Add(propertyInfo.Name);
		}
	}

	private static int Version(Type attribute, Type type, string field)
	{
		object[] customAttributes = type.GetProperty(field).GetCustomAttributes(attribute, inherit: true);
		int num = 0;
		if (num < customAttributes.Length)
		{
			VersionAttribute versionAttribute = (VersionAttribute)customAttributes[num];
			return versionAttribute.Version;
		}
		return 0;
	}

	private static int MinimumVersion(Type type, string field)
	{
		return Version(typeof(MinimumVersionAttribute), type, field);
	}

	private static int MaximumVersion(Type type, string field)
	{
		return Version(typeof(MaximumVersionAttribute), type, field);
	}

	private static int getRecommendedApiVersion(Type t)
	{
		FieldInfo field = t.GetField("recommendedApiVersion", BindingFlags.Static | BindingFlags.NonPublic);
		if (field == null || field.FieldType != typeof(int))
		{
			return 0;
		}
		return (int)field.GetValue(null);
	}

	private static bool checkVersion(Type type, string field, int requestedApiVersion)
	{
		if (requestedApiVersion == 0)
		{
			return true;
		}
		int num = MinimumVersion(type, field);
		if (num != 0 && requestedApiVersion < num)
		{
			return false;
		}
		int num2 = MaximumVersion(type, field);
		if (num2 != 0 && requestedApiVersion > num2)
		{
			return false;
		}
		return true;
	}

	public static List<string> GetContentFields(int APIversion, Type t)
	{
		List<string> list = new List<string>();
		int recommendedApiVersion = getRecommendedApiVersion(t);
		PropertyInfo[] properties = t.GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (!banlist.Contains(propertyInfo.Name) && checkVersion(t, propertyInfo.Name, (APIversion == 0) ? recommendedApiVersion : APIversion))
			{
				list.Add(propertyInfo.Name);
			}
		}
		list.Sort(new PriorityComparer(t));
		return list;
	}

	public static int GetPriority(Type t, string index)
	{
		int result = int.MaxValue;
		PropertyInfo property = t.GetProperty(index);
		if (property != null)
		{
			object[] customAttributes = property.GetCustomAttributes(typeof(ElementPriorityAttribute), inherit: true);
			foreach (object obj in customAttributes)
			{
				result = (obj as ElementPriorityAttribute).Priority;
			}
		}
		return result;
	}

	public int CompareByPriority(string x, string y)
	{
		return new PriorityComparer(GetType()).Compare(x, y);
	}

	public static Dictionary<string, Type> GetContentFieldTypes(int APIversion, Type t)
	{
		Dictionary<string, Type> dictionary = new Dictionary<string, Type>();
		int recommendedApiVersion = getRecommendedApiVersion(t);
		PropertyInfo[] properties = t.GetProperties();
		PropertyInfo[] array = properties;
		foreach (PropertyInfo propertyInfo in array)
		{
			if (!banlist.Contains(propertyInfo.Name) && checkVersion(t, propertyInfo.Name, (APIversion == 0) ? recommendedApiVersion : APIversion))
			{
				dictionary.Add(propertyInfo.Name, propertyInfo.PropertyType);
			}
		}
		return dictionary;
	}

	public static void Write7BitStr(Stream s, string value, Encoding enc)
	{
		byte[] bytes = enc.GetBytes(value);
		BinaryWriter binaryWriter = new BinaryWriter(s, enc);
		int num = bytes.Length;
		do
		{
			bool flag = true;
			binaryWriter.Write((byte)((num & 0x7F) | ((num > 127) ? 128 : 0)));
			num >>= 7;
		}
		while (num != 0);
		binaryWriter.Write(bytes);
	}

	public static void Write7BitStr(Stream s, string value)
	{
		Write7BitStr(s, value, Encoding.Default);
	}

	public static ulong FOURCC(string s)
	{
		if (s.Length > 8)
		{
			throw new ArgumentLengthException("String", 8);
		}
		ulong num = 0uL;
		for (int num2 = s.Length - 1; num2 >= 0; num2--)
		{
			num += (uint)s[num2] << num2 * 8;
		}
		return num;
	}

	public static string FOURCC(ulong i)
	{
		string text = "";
		for (int num = 7; num >= 0; num--)
		{
			char c = (char)((i >> num * 8) & 0xFF);
			if (text.Length > 0 || c != 0)
			{
				text = c + text;
			}
		}
		return text;
	}

	public static string FlagNames(Type t)
	{
		string text = "";
		string[] names = Enum.GetNames(t);
		foreach (string text2 in names)
		{
			text = text + " " + text2;
		}
		return text.Trim();
	}

	public static bool ArrayCompare(IList x, IList y)
	{
		if (x.GetType() != y.GetType())
		{
			throw new ArgumentException();
		}
		if (x.Count != y.Count)
		{
			return false;
		}
		for (int i = 0; i < x.Count; i++)
		{
			if (x[i] != y[i])
			{
				return false;
			}
		}
		return true;
	}
}
