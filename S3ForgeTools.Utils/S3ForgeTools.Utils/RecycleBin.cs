using System;
using System.Runtime.InteropServices;

namespace S3ForgeTools.Utils;

public class RecycleBin
{
	[Flags]
	public enum FileOperationFlags : ushort
	{
		FOF_SILENT = 4,
		FOF_NOCONFIRMATION = 0x10,
		FOF_ALLOWUNDO = 0x40,
		FOF_SIMPLEPROGRESS = 0x100,
		FOF_NOERRORUI = 0x400,
		FOF_WANTNUKEWARNING = 0x4000
	}

	public enum FileOperationType : uint
	{
		FO_MOVE = 1u,
		FO_COPY,
		FO_DELETE,
		FO_RENAME
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto, Pack = 1)]
	private struct SHFILEOPSTRUCT_x86
	{
		public IntPtr hwnd;

		[MarshalAs(UnmanagedType.U4)]
		public FileOperationType wFunc;

		public string pFrom;

		public string pTo;

		public FileOperationFlags fFlags;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fAnyOperationsAborted;

		public IntPtr hNameMappings;

		public string lpszProgressTitle;
	}

	[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
	private struct SHFILEOPSTRUCT_x64
	{
		public IntPtr hwnd;

		[MarshalAs(UnmanagedType.U4)]
		public FileOperationType wFunc;

		public string pFrom;

		public string pTo;

		public FileOperationFlags fFlags;

		[MarshalAs(UnmanagedType.Bool)]
		public bool fAnyOperationsAborted;

		public IntPtr hNameMappings;

		public string lpszProgressTitle;
	}

	[DllImport("shell32.dll", CharSet = CharSet.Auto, EntryPoint = "SHFileOperation")]
	private static extern int SHFileOperation_x86(ref SHFILEOPSTRUCT_x86 FileOp);

	[DllImport("shell32.dll", CharSet = CharSet.Auto, EntryPoint = "SHFileOperation")]
	private static extern int SHFileOperation_x64(ref SHFILEOPSTRUCT_x64 FileOp);

	private static bool IsWOW64Process()
	{
		return IntPtr.Size == 8;
	}

	public static bool Send(string path, FileOperationFlags flags)
	{
		try
		{
			if (IsWOW64Process())
			{
				SHFILEOPSTRUCT_x64 FileOp = default(SHFILEOPSTRUCT_x64);
				FileOp.wFunc = FileOperationType.FO_DELETE;
				FileOp.pFrom = path + '\0' + '\0';
				FileOp.fFlags = FileOperationFlags.FOF_ALLOWUNDO | flags;
				SHFileOperation_x64(ref FileOp);
			}
			else
			{
				SHFILEOPSTRUCT_x86 FileOp2 = default(SHFILEOPSTRUCT_x86);
				FileOp2.wFunc = FileOperationType.FO_DELETE;
				FileOp2.pFrom = path + '\0' + '\0';
				FileOp2.fFlags = FileOperationFlags.FOF_ALLOWUNDO | flags;
				SHFileOperation_x86(ref FileOp2);
			}
			return true;
		}
		catch
		{
			return false;
		}
	}

	public static bool Send(string path)
	{
		return Send(path, FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_WANTNUKEWARNING);
	}

	public static bool SendSilent(string path)
	{
		return Send(path, FileOperationFlags.FOF_SILENT | FileOperationFlags.FOF_NOCONFIRMATION | FileOperationFlags.FOF_NOERRORUI);
	}
}
