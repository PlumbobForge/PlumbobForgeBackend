namespace s3pi.Settings;

public static class Settings
{
	private static bool checking;

	private static bool asBytesWorkaround;

	public static bool Checking => checking;

	public static bool AsBytesWorkaround => asBytesWorkaround;

	static Settings()
	{
		checking = true;
		asBytesWorkaround = true;
	}
}
