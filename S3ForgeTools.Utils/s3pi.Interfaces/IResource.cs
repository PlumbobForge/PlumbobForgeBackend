using System;
using System.IO;

namespace s3pi.Interfaces;

public interface IResource : IApiVersion, IContentFields
{
	Stream Stream { get; }

	byte[] AsBytes { get; }

	event EventHandler ResourceChanged;
}
