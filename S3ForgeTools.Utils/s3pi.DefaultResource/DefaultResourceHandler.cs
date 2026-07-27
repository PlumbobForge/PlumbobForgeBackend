using System.Collections.Generic;
using s3pi.Interfaces;

namespace s3pi.DefaultResource;

public class DefaultResourceHandler : AResourceHandler
{
	public DefaultResourceHandler()
	{
		Add(typeof(DefaultResource), new List<string>(new string[1] { "*" }));
	}
}
