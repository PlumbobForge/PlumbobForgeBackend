using System;
using System.Collections;
using System.Collections.Generic;

namespace s3pi.Interfaces;

public abstract class AResourceHandler : Dictionary<Type, List<string>>, IResourceHandler, IDictionary<Type, List<string>>, ICollection<KeyValuePair<Type, List<string>>>, IEnumerable<KeyValuePair<Type, List<string>>>, IEnumerable
{
	public AResourceHandler()
	{
	}
}
