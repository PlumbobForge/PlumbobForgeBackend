using System;
using System.Collections.Generic;
using System.IO;

namespace s3pi.Interfaces;

public interface IResourceIndexEntry : IApiVersion, IContentFields, IResourceKey, IEqualityComparer<IResourceKey>, IEquatable<IResourceKey>, IComparable<IResourceKey>
{
	uint Chunkoffset { get; set; }

	uint Filesize { get; set; }

	uint Memsize { get; set; }

	ushort Compressed { get; set; }

	ushort Unknown2 { get; set; }

	Stream Stream { get; }

	bool IsDeleted { get; set; }
}
