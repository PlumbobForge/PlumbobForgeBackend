namespace S3ForgeTools.GameFiles;

public static class EndianExtension
{
	public static ushort Swap(this ushort inValue)
	{
		return (ushort)(((inValue & 0xFF00) >> 8) | ((inValue & 0xFF) << 8));
	}

	public static uint Swap(this uint inValue)
	{
		return ((inValue & 0xFF000000u) >> 24) | ((inValue & 0xFF0000) >> 8) | ((inValue & 0xFF00) << 8) | ((inValue & 0xFF) << 24);
	}
}
