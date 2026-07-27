using System;
using System.Collections;
using System.Collections.Generic;

namespace s3pi.Interfaces;

internal interface IResourceHandler : IDictionary<Type, List<string>>, ICollection<KeyValuePair<Type, List<string>>>, IEnumerable<KeyValuePair<Type, List<string>>>, IEnumerable
{
}
