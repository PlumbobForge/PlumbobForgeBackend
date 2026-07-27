using System;
using System.IO;
using s3pi.Interfaces;

namespace s3pi.DefaultResource;

public class DefaultResource : AResource
{
	private const int recommendedApiVersion = 1;

	public override int RecommendedApiVersion => 1;

	public override Stream Stream
	{
		get
		{
			stream.Position = 0L;
			return stream;
		}
	}

	public DefaultResource(int APIversion, Stream s)
		: base(APIversion, s)
	{
		if (stream == null)
		{
			stream = new MemoryStream();
			dirty = true;
		}
	}

	protected override Stream UnParse()
	{
		throw new NotImplementedException();
	}
}
