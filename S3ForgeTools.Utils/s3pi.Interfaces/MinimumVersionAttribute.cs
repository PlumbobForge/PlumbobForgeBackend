using System;

namespace s3pi.Interfaces;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
public class MinimumVersionAttribute : VersionAttribute
{
	public MinimumVersionAttribute(int Version)
		: base(Version)
	{
	}
}
