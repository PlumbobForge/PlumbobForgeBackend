using System;
using System.Collections.Generic;
using System.IO;
using s3pi.Interfaces;
using s3pi.Settings;

namespace s3pi.Package;

public class Package : APackage
{
	private class FlagMatch
	{
		private Boolset flags;

		private IResourceIndexEntry values;

		public FlagMatch(Boolset flags, IResourceIndexEntry values)
		{
			this.flags = flags;
			this.values = values;
		}

		public bool Match(IResourceIndexEntry rie)
		{
			if ((ushort)flags == 0)
			{
				return true;
			}
			if (rie.IsDeleted)
			{
				return false;
			}
			bool result = true;
			for (int i = 0; i < values.ContentFields.Count && i < flags.Length; i++)
			{
				if (flags[i])
				{
					string index = values.ContentFields[i];
					if (!values[index].Equals(rie[index]))
					{
						result = false;
						break;
					}
				}
			}
			return result;
		}
	}

	private class NameMatch
	{
		private string[] names;

		private TypedValue[] values;

		public NameMatch(string[] names, TypedValue[] values)
		{
			foreach (string text in names)
			{
				if (!AApiVersionedFields.GetContentFields(0, typeof(ResourceIndexEntry)).Contains(text))
				{
					throw new ArgumentOutOfRangeException("names", $"'{text}' is an invalid IResourceIndexEntry ContentField");
				}
			}
			this.names = names;
			this.values = values;
		}

		public bool Match(IResourceIndexEntry rie)
		{
			if (names.Length == 0)
			{
				return true;
			}
			if (rie.IsDeleted)
			{
				return false;
			}
			bool result = true;
			for (int i = 0; i < names.Length; i++)
			{
				if (!values[i].Equals(rie[names[i]]))
				{
					result = false;
					break;
				}
			}
			return result;
		}
	}

	private const int recommendedApiVersion = 1;

	private const string magic = "DBPF";

	private const int major = 2;

	private const int minor = 0;

	private static bool checking = s3pi.Settings.Settings.Checking;

	private Stream packageStream = null;

	private byte[] header = new byte[96];

	private BinaryReader headerReader = null;

	private PackageIndex index = null;

	public override int RecommendedApiVersion => 1;

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override byte[] Magic
	{
		get
		{
			headerReader.BaseStream.Position = 0L;
			return headerReader.ReadBytes(4);
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override int Major
	{
		get
		{
			headerReader.BaseStream.Position = 4L;
			return headerReader.ReadInt32();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override int Minor
	{
		get
		{
			headerReader.BaseStream.Position = 8L;
			return headerReader.ReadInt32();
		}
	}

	[MaximumVersion(1)]
	[MinimumVersion(1)]
	public override byte[] Unknown1
	{
		get
		{
			headerReader.BaseStream.Position = 12L;
			return headerReader.ReadBytes(24);
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override int Indexcount
	{
		get
		{
			headerReader.BaseStream.Position = 36L;
			return headerReader.ReadInt32();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override byte[] Unknown2
	{
		get
		{
			headerReader.BaseStream.Position = 40L;
			return headerReader.ReadBytes(4);
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override int Indexsize
	{
		get
		{
			headerReader.BaseStream.Position = 44L;
			return headerReader.ReadInt32();
		}
	}

	[MaximumVersion(1)]
	[MinimumVersion(1)]
	public override byte[] Unknown3
	{
		get
		{
			headerReader.BaseStream.Position = 48L;
			return headerReader.ReadBytes(12);
		}
	}

	[MaximumVersion(1)]
	[MinimumVersion(1)]
	public override int Indexversion
	{
		get
		{
			headerReader.BaseStream.Position = 60L;
			return headerReader.ReadInt32();
		}
	}

	[MaximumVersion(1)]
	[MinimumVersion(1)]
	public override int Indexposition
	{
		get
		{
			headerReader.BaseStream.Position = 64L;
			return headerReader.ReadInt32();
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override byte[] Unknown4
	{
		get
		{
			headerReader.BaseStream.Position = 68L;
			return headerReader.ReadBytes(28);
		}
	}

	[MaximumVersion(1)]
	[MinimumVersion(1)]
	public override Stream HeaderStream => headerReader.BaseStream;

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override uint Indextype => (GetResourceList as PackageIndex).Indextype;

	[MaximumVersion(1)]
	[MinimumVersion(1)]
	public override IList<IResourceIndexEntry> GetResourceList => Index;

	private PackageIndex Index
	{
		get
		{
			if (index == null)
			{
				index = new PackageIndex(packageStream, Indexposition, Indexsize, Indexcount);
				OnResourceIndexInvalidated(this, new EventArgs());
			}
			return index;
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override void SavePackage()
	{
		if (checking && packageStream == null)
		{
			throw new InvalidOperationException("Package has no stream to save to");
		}
		if (!packageStream.CanWrite)
		{
			throw new InvalidOperationException("Package is read-only");
		}
		string tempFileName = Path.GetTempFileName();
		SaveAs(tempFileName);
		FileStream fileStream = packageStream as FileStream;
		fileStream?.Lock(0L, header.Length);
		packageStream.Position = 0L;
		BinaryReader binaryReader = new BinaryReader(new FileStream(tempFileName, FileMode.Open));
		BinaryWriter binaryWriter = new BinaryWriter(packageStream);
		binaryWriter.Write(binaryReader.ReadBytes((int)binaryReader.BaseStream.Length));
		packageStream.SetLength(packageStream.Position);
		binaryWriter.Flush();
		fileStream?.Unlock(0L, header.Length);
		packageStream.Position = 0L;
		header = new BinaryReader(packageStream).ReadBytes(header.Length);
		headerReader = new BinaryReader(new MemoryStream(header));
		if (checking)
		{
			CheckHeader();
		}
		bool flag = index == null;
		index = null;
		if (!flag)
		{
			OnResourceIndexInvalidated(this, new EventArgs());
		}
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override void SaveAs(Stream s)
	{
		BinaryWriter binaryWriter = new BinaryWriter(s);
		binaryWriter.Write(header);
		PackageIndex packageIndex = new PackageIndex(((Indextype & 4) != 0) ? 4u : 0u);
		foreach (IResourceIndexEntry item in Index)
		{
			if (!item.IsDeleted)
			{
				ResourceIndexEntry resourceIndexEntry = (item as ResourceIndexEntry).Clone();
				((List<IResourceIndexEntry>)packageIndex).Add((IResourceIndexEntry)resourceIndexEntry);
				byte[] array = packedChunk(item as ResourceIndexEntry);
				resourceIndexEntry.Chunkoffset = (uint)s.Position;
				binaryWriter.Write(array);
				binaryWriter.Flush();
				if (array.Length < resourceIndexEntry.Memsize)
				{
					resourceIndexEntry.Compressed = ushort.MaxValue;
					resourceIndexEntry.Filesize = (uint)array.Length;
				}
				else
				{
					resourceIndexEntry.Compressed = 0;
					resourceIndexEntry.Filesize = resourceIndexEntry.Memsize;
				}
			}
		}
		long position = s.Position;
		packageIndex.Save(binaryWriter);
		setIndexcount(binaryWriter, packageIndex.Count);
		setIndexsize(binaryWriter, packageIndex.Size);
		setIndexposition(binaryWriter, (int)position);
		s.Flush();
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override void SaveAs(string path)
	{
		FileStream fileStream = new FileStream(path, FileMode.Create);
		SaveAs(fileStream);
		fileStream.Close();
	}

	public new static IPackage NewPackage(int APIversion)
	{
		return new Package(APIversion);
	}

	public new static IPackage OpenPackage(int APIversion, string packagePath)
	{
		return OpenPackage(APIversion, packagePath, readwrite: false);
	}

	public new static IPackage OpenPackage(int APIversion, string PackagePath, bool readwrite)
	{
		return new Package(APIversion, new FileStream(PackagePath, FileMode.Open, (!readwrite) ? FileAccess.Read : FileAccess.ReadWrite, FileShare.ReadWrite));
	}

	public new static void ClosePackage(int APIversion, IPackage pkg)
	{
		if (!(pkg is Package package))
		{
			return;
		}
		if (package.packageStream != null)
		{
			try
			{
				package.packageStream.Close();
			}
			catch
			{
			}
			package.packageStream = null;
		}
		package.header = null;
		package.headerReader = null;
		package.index = null;
	}

	[MaximumVersion(1)]
	[MinimumVersion(1)]
	public override IResourceIndexEntry Find(uint flags, IResourceIndexEntry values)
	{
		return Index.Find(new FlagMatch(flags, values).Match);
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override IResourceIndexEntry Find(string[] names, TypedValue[] values)
	{
		return Index.Find(new NameMatch(names, values).Match);
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override IList<IResourceIndexEntry> FindAll(uint flags, IResourceIndexEntry values)
	{
		return Index.FindAll(new FlagMatch(flags, values).Match);
	}

	[MinimumVersion(1)]
	[MaximumVersion(1)]
	public override IList<IResourceIndexEntry> FindAll(string[] names, TypedValue[] values)
	{
		return Index.FindAll(new NameMatch(names, values).Match);
	}

	public override IResourceIndexEntry AddResource(IResourceKey rk, Stream stream, bool rejectDups)
	{
		if (rejectDups && Index[rk] != null && !Index[rk].IsDeleted)
		{
			return null;
		}
		IResourceIndexEntry resourceIndexEntry = Index.Add(rk);
		if (stream != null)
		{
			(resourceIndexEntry as ResourceIndexEntry).ResourceStream = stream;
		}
		return resourceIndexEntry;
	}

	public override void ReplaceResource(IResourceIndexEntry rc, IResource res)
	{
		(rc as ResourceIndexEntry).ResourceStream = res.Stream;
	}

	public override void DeleteResource(IResourceIndexEntry rc)
	{
		if (!rc.IsDeleted)
		{
			(rc as ResourceIndexEntry).Delete();
		}
	}

	private Package(int requestedVersion)
	{
		requestedApiVersion = requestedVersion;
		header = new byte[96];
		headerReader = new BinaryReader(new MemoryStream(header));
		BinaryWriter binaryWriter = new BinaryWriter(new MemoryStream(header));
		binaryWriter.Write(stringToBytes("DBPF"));
		binaryWriter.Write(2);
		binaryWriter.Write(0);
		setIndexsize(binaryWriter, new PackageIndex().Size);
		setIndexversion(binaryWriter);
		setIndexposition(binaryWriter, header.Length);
	}

	private Package(int requestedVersion, Stream s)
	{
		requestedApiVersion = requestedVersion;
		packageStream = s;
		s.Position = 0L;
		header = new BinaryReader(s).ReadBytes(header.Length);
		headerReader = new BinaryReader(new MemoryStream(header));
		if (checking)
		{
			CheckHeader();
		}
	}

	private byte[] packedChunk(ResourceIndexEntry ie)
	{
		byte[] array = null;
		if (ie.IsDirty)
		{
			Stream resource = GetResource(ie);
			BinaryReader binaryReader = new BinaryReader(resource);
			resource.Position = 0L;
			array = binaryReader.ReadBytes((int)ie.Memsize);
			if (checking && array.Length != (int)ie.Memsize)
			{
				throw new OverflowException($"packedChunk, dirty resource - T: 0x{ie.ResourceType:X}, G: 0x{ie.ResourceGroup:X}, I: 0x{ie.Instance:X}: Length expected: 0x{ie.Memsize:X}, read: 0x{array.Length:X}");
			}
			byte[] array2 = ((ie.Compressed != 0) ? Compression.CompressStream(array) : array);
			if (array2.Length < array.Length)
			{
				array = array2;
			}
		}
		else
		{
			if (checking && packageStream == null)
			{
				throw new InvalidOperationException($"Clean resource with undefined \"current package\" - T: 0x{ie.ResourceType:X}, G: 0x{ie.ResourceGroup:X}, I: 0x{ie.Instance:X}");
			}
			packageStream.Position = ie.Chunkoffset;
			array = new BinaryReader(packageStream).ReadBytes((int)ie.Filesize);
			if (checking && array.Length != (int)ie.Filesize)
			{
				throw new OverflowException($"packedChunk, clean resource - T: 0x{ie.ResourceType:X}, G: 0x{ie.ResourceGroup:X}, I: 0x{ie.Instance:X}: Length expected: 0x{ie.Filesize:X}, read: 0x{array.Length:X}");
			}
		}
		return array;
	}

	private static byte[] stringToBytes(string s)
	{
		byte[] array = new byte[s.Length];
		int num = 0;
		foreach (char c in s)
		{
			array[num++] = (byte)c;
		}
		return array;
	}

	private static string bytesToString(byte[] bytes)
	{
		string text = "";
		foreach (byte b in bytes)
		{
			text += (char)b;
		}
		return text;
	}

	private void setIndexcount(BinaryWriter w, int c)
	{
		w.BaseStream.Position = 36L;
		w.Write(c);
	}

	private void setIndexsize(BinaryWriter w, int c)
	{
		w.BaseStream.Position = 44L;
		w.Write(c);
	}

	private void setIndexversion(BinaryWriter w)
	{
		w.BaseStream.Position = 60L;
		w.Write(3);
	}

	private void setIndexposition(BinaryWriter w, int c)
	{
		w.BaseStream.Position = 64L;
		w.Write(c);
	}

	private void CheckHeader()
	{
		if (headerReader.BaseStream.Length != 96)
		{
			throw new InvalidDataException("Hit unexpected end of file at " + headerReader.BaseStream.Position);
		}
		if (bytesToString(Magic) != "DBPF")
		{
			throw new InvalidDataException("Expected magic tag 'DBPF'.  Found '" + bytesToString(Magic) + "'.");
		}
		if (Major != 2)
		{
			throw new InvalidDataException("Expected major version '" + 2 + "'.  Found '" + Major.ToString() + "'.");
		}
		if (Minor != 0)
		{
			throw new InvalidDataException("Expected major version '" + 0 + "'.  Found '" + Minor.ToString() + "'.");
		}
	}

	public override Stream GetResource(IResourceIndexEntry rc)
	{
		if (!(rc is ResourceIndexEntry resourceIndexEntry))
		{
			return null;
		}
		if (resourceIndexEntry.ResourceStream != null)
		{
			return resourceIndexEntry.ResourceStream;
		}
		if (rc.Chunkoffset == uint.MaxValue)
		{
			return null;
		}
		packageStream.Position = rc.Chunkoffset;
		byte[] array = null;
		array = ((rc.Filesize != rc.Memsize) ? Compression.UncompressStream(packageStream, (int)rc.Filesize, (int)rc.Memsize) : new BinaryReader(packageStream).ReadBytes((int)rc.Filesize));
		MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(array, 0, array.Length);
		memoryStream.Position = 0L;
		return memoryStream;
	}
}
