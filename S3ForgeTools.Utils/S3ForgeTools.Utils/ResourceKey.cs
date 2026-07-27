using System;
using System.Collections.Generic;
using System.Globalization;
using s3pi.Interfaces;

namespace S3ForgeTools.Utils;

public class ResourceKey : AResourceKey
{
	public override List<string> ContentFields => null;

	public override int RecommendedApiVersion => 1;

	public ResourceKey(int APIversion, EventHandler handler)
		: base(APIversion, handler)
	{
	}

	public ResourceKey(int APIversion, EventHandler handler, IResourceKey basis)
		: this(APIversion, handler)
	{
		instance = basis.Instance;
		ResourceGroup = basis.ResourceGroup;
		ResourceType = basis.ResourceType;
	}

	public ResourceKey(int APIversion, EventHandler handler, uint resourceType, uint resourceGroup, ulong instance)
		: this(APIversion, handler)
	{
		Instance = instance;
		ResourceGroup = resourceGroup;
		ResourceType = resourceType;
	}

	public override AHandlerElement Clone(EventHandler handler)
	{
		return null;
	}

	private uint ToHex8(string Hex)
	{
		if (Hex.StartsWith("0x"))
		{
			Hex = Hex.Substring(3);
		}
		return uint.Parse(Hex, NumberStyles.HexNumber);
	}

	private ulong ToHex16(string Hex)
	{
		if (Hex.StartsWith("0x"))
		{
			Hex = Hex.Substring(3);
		}
		return ulong.Parse(Hex, NumberStyles.HexNumber);
	}

	public void SetTGI(string ResourceType, string ResourceGroup, string Instance)
	{
		SetTGI(ToHex8(ResourceType), ToHex8(ResourceGroup), ToHex16(Instance));
	}

	public void SetTGI(uint ResourceType, uint ResourceGroup, ulong Instance)
	{
		this.ResourceType = ResourceType;
		this.ResourceGroup = ResourceGroup;
		this.Instance = Instance;
	}
}
