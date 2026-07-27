using System;
using System.Collections.Generic;

namespace s3pi.Interfaces;

public abstract class AResourceKey : AHandlerElement, IResourceKey, IEqualityComparer<IResourceKey>, IEquatable<IResourceKey>, IComparable<IResourceKey>
{
	protected uint resourceType;

	protected uint resourceGroup;

	protected ulong instance;

	[ElementPriority(1)]
	public virtual uint ResourceType
	{
		get
		{
			return resourceType;
		}
		set
		{
			if (resourceType != value)
			{
				resourceType = value;
				OnElementChanged();
			}
		}
	}

	[ElementPriority(2)]
	public virtual uint ResourceGroup
	{
		get
		{
			return resourceGroup;
		}
		set
		{
			if (resourceGroup != value)
			{
				resourceGroup = value;
				OnElementChanged();
			}
		}
	}

	[ElementPriority(3)]
	public virtual ulong Instance
	{
		get
		{
			return instance;
		}
		set
		{
			if (instance != value)
			{
				instance = value;
				OnElementChanged();
			}
		}
	}

	public AResourceKey(int APIversion, EventHandler handler)
		: base(APIversion, handler)
	{
	}

	public AResourceKey(int APIversion, EventHandler handler, IResourceKey basis)
		: this(APIversion, handler, basis.ResourceType, basis.ResourceGroup, basis.Instance)
	{
	}

	public AResourceKey(int APIversion, EventHandler handler, uint resourceType, uint resourceGroup, ulong instance)
		: base(APIversion, handler)
	{
		this.resourceType = resourceType;
		this.resourceGroup = resourceGroup;
		this.instance = instance;
	}

	public bool Equals(IResourceKey x, IResourceKey y)
	{
		return x.Equals(y);
	}

	public int GetHashCode(IResourceKey obj)
	{
		return obj.GetHashCode();
	}

	public override int GetHashCode()
	{
		return ResourceType.GetHashCode() ^ ResourceGroup.GetHashCode() ^ Instance.GetHashCode();
	}

	public bool Equals(IResourceKey other)
	{
		return CompareTo(other) == 0;
	}

	public int CompareTo(IResourceKey other)
	{
		int num = ResourceType.CompareTo(other.ResourceType);
		if (num != 0)
		{
			return num;
		}
		num = ResourceGroup.CompareTo(other.ResourceGroup);
		if (num != 0)
		{
			return num;
		}
		return Instance.CompareTo(other.Instance);
	}

	public static implicit operator string(AResourceKey value)
	{
		return $"0x{value.ResourceType:X8}-0x{value.ResourceGroup:X8}-0x{value.Instance:X16}";
	}

	public override string ToString()
	{
		return this;
	}
}
