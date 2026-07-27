using System;

namespace s3pi.Interfaces;

public class VersionAttribute : Attribute
{
	private int version;

	public int Version
	{
		get
		{
			return version;
		}
		set
		{
			version = value;
		}
	}

	public VersionAttribute(int Version)
	{
		version = Version;
	}
}
