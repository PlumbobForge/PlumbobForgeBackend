using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Threading;
using System.Windows;

namespace WPFLocalization;

public static class LocalizationManager
{
	private static ResourceManager _resourceManager;

	private static bool _resourceManagerLoaded;

	private static List<LocExtension> _localizations = new List<LocExtension>();

	private static int _localizationPurgeCount;

	public static ResourceManager ResourceManager
	{
		get
		{
			if (_resourceManager == null && !_resourceManagerLoaded)
			{
				_resourceManager = GetResourceManager();
				_resourceManagerLoaded = true;
			}
			return _resourceManager;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			_resourceManager = value;
			UpdateLocalizations();
		}
	}

	public static CultureInfo UICulture
	{
		get
		{
			return Thread.CurrentThread.CurrentUICulture;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			Thread.CurrentThread.CurrentUICulture = value;
			UpdateLocalizations();
		}
	}

	internal static void AddLocalization(LocExtension localization)
	{
		if (localization == null)
		{
			throw new ArgumentNullException("localization");
		}
		if (_localizationPurgeCount > 50)
		{
			List<LocExtension> list = new List<LocExtension>(_localizations.Count);
			foreach (LocExtension localization2 in _localizations)
			{
				if (localization2.IsAlive)
				{
					list.Add(localization2);
				}
			}
			_localizations = list;
			_localizationPurgeCount = 0;
		}
		_localizations.Add(localization);
		_localizationPurgeCount++;
	}

	private static ResourceManager GetResourceManager()
	{
		Assembly assembly = Assembly.GetEntryAssembly();
		if (assembly != null && string.Compare(assembly.GetName().Name, "Blend", StringComparison.InvariantCultureIgnoreCase) == 0)
		{
			assembly = null;
		}
		if (assembly == null)
		{
			Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
			Assembly[] array = assemblies;
			foreach (Assembly assembly2 in array)
			{
				if (assembly2.EntryPoint != null)
				{
					Type type = assembly2.GetType(assembly2.GetName().Name + ".App", throwOnError: false);
					if (type != null && typeof(Application).IsAssignableFrom(type) && string.Compare(assembly2.GetName().Name, "Blend", StringComparison.InvariantCultureIgnoreCase) != 0)
					{
						assembly = assembly2;
						break;
					}
				}
			}
		}
		if (assembly != null)
		{
			try
			{
				return new ResourceManager(assembly.GetName().Name + ".Properties.Resources", assembly);
			}
			catch (MissingManifestResourceException)
			{
			}
		}
		return null;
	}

	private static void UpdateLocalizations()
	{
		foreach (LocExtension localization in _localizations)
		{
			localization.UpdateTargetValue();
		}
	}
}
