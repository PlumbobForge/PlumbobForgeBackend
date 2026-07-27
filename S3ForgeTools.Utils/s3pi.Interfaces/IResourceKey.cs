using System;
using System.Collections.Generic;

namespace s3pi.Interfaces;

public interface IResourceKey : IEqualityComparer<IResourceKey>, IEquatable<IResourceKey>, IComparable<IResourceKey>
{
	uint ResourceType { get; set; }

	uint ResourceGroup { get; set; }

	ulong Instance { get; set; }
}
