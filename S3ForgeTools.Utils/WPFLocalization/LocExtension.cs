using System;
using System.Collections.Generic;
using System.Reflection;
using System.Resources;
using System.Windows;
using System.Windows.Markup;

namespace WPFLocalization;

[MarkupExtensionReturnType(typeof(object))]
[ContentProperty("Key")]
public class LocExtension : MarkupExtension
{
	private object _targetProperty;

	private WeakReference _targetObject;

	private List<WeakReference> _targetObjects;

	public string Key { get; set; }

	public string Format { get; set; }

	internal bool IsAlive
	{
		get
		{
			if (_targetObjects != null)
			{
				foreach (WeakReference targetObject in _targetObjects)
				{
					if (targetObject.IsAlive)
					{
						return true;
					}
				}
				return false;
			}
			return _targetObject.IsAlive;
		}
	}

	public LocExtension()
	{
	}

	public LocExtension(string key)
	{
		Key = key;
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		if (serviceProvider.GetService(typeof(IProvideValueTarget)) is IProvideValueTarget provideValueTarget)
		{
			if (provideValueTarget.TargetProperty is DependencyProperty)
			{
				_targetProperty = provideValueTarget.TargetProperty;
				if (!(provideValueTarget.TargetObject is DependencyObject))
				{
					_targetObjects = new List<WeakReference>();
					LocalizationManager.AddLocalization(this);
					return this;
				}
				WeakReference weakReference = new WeakReference(provideValueTarget.TargetObject);
				if (_targetObjects != null)
				{
					_targetObjects.Add(weakReference);
				}
				else
				{
					_targetObject = weakReference;
					LocalizationManager.AddLocalization(this);
				}
			}
			else if (provideValueTarget.TargetProperty is PropertyInfo)
			{
				_targetProperty = provideValueTarget.TargetProperty;
				_targetObject = new WeakReference(provideValueTarget.TargetObject);
				LocalizationManager.AddLocalization(this);
			}
		}
		return GetValue(Key, Format);
	}

	internal void UpdateTargetValue()
	{
		object targetProperty = _targetProperty;
		if (targetProperty == null)
		{
			return;
		}
		if (targetProperty is DependencyProperty)
		{
			if (_targetObject != null)
			{
				if (_targetObject.Target is DependencyObject dependencyObject)
				{
					dependencyObject.SetValue((DependencyProperty)targetProperty, GetValue(Key, Format));
				}
			}
			else
			{
				if (_targetObjects == null)
				{
					return;
				}
				foreach (WeakReference targetObject in _targetObjects)
				{
					if (targetObject.Target is DependencyObject dependencyObject2)
					{
						dependencyObject2.SetValue((DependencyProperty)targetProperty, GetValue(Key, Format));
					}
				}
			}
		}
		else if (targetProperty is PropertyInfo)
		{
			object target = _targetObject.Target;
			if (target != null)
			{
				((PropertyInfo)targetProperty).SetValue(target, GetValue(Key, Format), null);
			}
		}
	}

	private static object GetValue(string key, string format)
	{
		if (string.IsNullOrEmpty(key))
		{
			return string.Empty;
		}
		ResourceManager resourceManager = LocalizationManager.ResourceManager;
		object obj;
		if (resourceManager == null)
		{
			obj = "";
		}
		else
		{
			obj = resourceManager.GetObject(key);
			if (obj == null)
			{
				throw new ArgumentOutOfRangeException("key", key, "Resource not found.");
			}
		}
		if (string.IsNullOrEmpty(format))
		{
			return obj;
		}
		return string.Format(format, obj);
	}
}
