using System;

namespace s3pi.Interfaces;

[AttributeUsage(AttributeTargets.All, AllowMultiple = false, Inherited = true)]
public class MaximumVersionAttribute : VersionAttribute
{
	public MaximumVersionAttribute(int Version)
		: base(Version)
	{
	}
}
