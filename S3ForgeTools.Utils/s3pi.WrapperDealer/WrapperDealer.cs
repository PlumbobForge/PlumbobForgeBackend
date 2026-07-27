using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using s3pi.DefaultResource;
using s3pi.Interfaces;
using s3pi.Settings;

namespace s3pi.WrapperDealer;

public static class WrapperDealer
{
	private static List<KeyValuePair<string, Type>> typeMap;

	public static IResource CreateNewResource(int APIversion, string resourceType)
	{
		return WrapperForType(resourceType, APIversion, null);
	}

	public static IResource GetResource(int APIversion, IPackage pkg, IResourceIndexEntry rie)
	{
		return GetResource(APIversion, pkg, rie, AlwaysDefault: false);
	}

	public static IResource GetResource(int APIversion, IPackage pkg, IResourceIndexEntry rie, bool AlwaysDefault)
	{
		return WrapperForType(AlwaysDefault ? "*" : ((string)rie["ResourceType"]), APIversion, (pkg as APackage).GetResource(rie));
	}

	static WrapperDealer()
	{
		typeMap = null;
		string directoryName = Path.GetDirectoryName(typeof(WrapperDealer).Assembly.Location);
		typeMap = new List<KeyValuePair<string, Type>>();
		try
		{
			AddTypeMap(new DefaultResourceHandler());
		}
		catch
		{
		}
		string[] files = Directory.GetFiles(directoryName, "*.dll");
		foreach (string path in files)
		{
			try
			{
				Assembly assembly = Assembly.LoadFile(path);
				Type[] types = assembly.GetTypes();
				Type[] array = types;
				foreach (Type type in array)
				{
					if (type.IsSubclassOf(typeof(AResourceHandler)))
					{
						AddTypeMap((AResourceHandler)type.GetConstructor(new Type[0]).Invoke(new object[0]));
					}
				}
			}
			catch
			{
			}
		}
	}

	private static void AddTypeMap(AResourceHandler arh)
	{
		if (arh == null)
		{
			return;
		}
		foreach (Type key in arh.Keys)
		{
			foreach (string item in arh[key])
			{
				typeMap.Add(new KeyValuePair<string, Type>(item, key));
			}
		}
	}

	private static IResource WrapperForType(string type, int APIversion, Stream s)
	{
		Type type2 = null;
		foreach (KeyValuePair<string, Type> item in typeMap)
		{
			if (item.Key == type)
			{
				type2 = item.Value;
				break;
			}
		}
		if (type2 == null)
		{
			foreach (KeyValuePair<string, Type> item2 in typeMap)
			{
				if (item2.Key == "*")
				{
					type2 = item2.Value;
					break;
				}
			}
		}
		if (s3pi.Settings.Settings.Checking && type2 == null)
		{
			throw new InvalidOperationException("Could not find a resource handler");
		}
		return (IResource)type2.GetConstructor(new Type[2]
		{
			typeof(int),
			typeof(Stream)
		}).Invoke(new object[2] { APIversion, s });
	}
}
