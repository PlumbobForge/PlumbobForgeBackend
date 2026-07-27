namespace s3pi.Interfaces;

public static class StringTableSingleton
{
	private static string[] stringTable = null;

	public static string[] Table
	{
		get
		{
			if (stringTable == null)
			{
				stringTable = new string[111]
				{
					"", "filename", "X:", "-1", "assetRoot", "daeFileName", "daeFilePath", "Color", "ObjectRgbMask", "rgbmask",
					"specmap", "Background Image", "HSVShift Bg", "H Bg", "V Bg", "S Bg", "Base H Bg", "Base V Bg", "Base S Bg", "Mask",
					"Multiplier", "Dirt Layer", "1X Multiplier", "Specular", "Overlay", "Face", "partType", "gender", "bodyType", "age",
					"A", "M", "Stencil A", "Stencil B", "Stencil C", "Stencil D", "Stencil A Enabled", "Stencil B Enabled", "Stencil C Enabled", "Stencil D Enabled",
					"Stencil A Tiling", "Stencil B Tiling", "Stencil C Tiling", "Stencil D Tiling", "Stencil A Rotation", "Stencil B Rotation", "Stencil C Rotation", "Stencil D Rotation", "Pattern A", "Pattern B",
					"Pattern C", "Pattern A Enabled", "Pattern B Enabled", "Pattern C Enabled", "Pattern A Linked", "Pattern B Linked", "Pattern C Linked", "Pattern A Rotation", "Pattern B Rotation", "Pattern C Rotation",
					"Pattern A Tiling", "Pattern B Tiling", "Pattern C Tiling", "", "MaskWidth", "MaskHeight", "ObjectRgbaMask", "RndColors", "Flat Color", "Alpha",
					"Color 0", "Color 1", "Color 2", "Color 3", "Color 4", "Channel 1", "Channel 2", "Channel 3", "Pattern D", "Pattern D Tiling",
					"Pattern D Enabled", "Pattern D Linked", "Pattern D Rotation", "HSVShift 1", "HSVShift 2", "HSVShift 3", "Channel 1 Enabled", "Channel 2 Enabled", "Channel 3 Enabled", "Base H 1",
					"Base V 1", "Base S 1", "Base H 2", "Base V 2", "Base S 2", "Base H 3", "Base V 3", "Base S 3", "H 1", "S 1",
					"V 1", "H 2", "S 2", "V 2", "H 3", "V 3", "S 3", "true", "1,0,0,0", "defaultFlatColor",
					"solidColor_1"
				};
			}
			return stringTable;
		}
	}
}
