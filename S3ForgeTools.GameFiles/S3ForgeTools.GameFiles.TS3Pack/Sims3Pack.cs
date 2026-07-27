using System;
using System.Collections.Generic;
using System.Dynamic;
using System.IO;
using System.Text;
using System.Xml;
using S3ForgeTools.GameFiles.Package;

namespace S3ForgeTools.GameFiles.TS3Pack;

public class Sims3Pack : IDisposable
{
	private Stream DataStore;

	private bool disposed = false;

	public List<DBPFPackage> Packages { get; private set; }

	public Stream Thumbnail { get; private set; }

	public List<Stream> Thumbnails { get; private set; }

	public XmlDocument Manifest { get; private set; }

	public bool IsCorrupt { get; private set; }

	public bool IsEncrypted { get; private set; }

	public string Type { get; private set; }

	public string SubType { get; private set; }

	public Sims3Pack()
	{
		Packages = new List<DBPFPackage>();
		Thumbnails = new List<Stream>();
	}

	public Sims3Pack(Stream SourceStream)
		: this()
	{
		DataStore = SourceStream;
		Import();
	}

	public Sims3Pack(string FileName)
		: this()
	{
		IsEncrypted = false;
		DataStore = File.Open(FileName, FileMode.Open, FileAccess.Read, FileShare.Read);
		try
		{
			Import();
		}
		catch (InvalidDataException ex)
		{
			DataStore.Close();
			if (ex.Message == "DBPP Not Supported")
			{
				IsEncrypted = true;
			}
		}
		catch (XmlException)
		{
			DataStore.Close();
			IsCorrupt = true;
		}
		catch (EndOfStreamException)
		{
			DataStore.Close();
			IsCorrupt = true;
		}
	}

	private void Import()
	{
		BinaryReader binaryReader = new BinaryReader(DataStore);
		int count = binaryReader.ReadInt32();
		string text = new string(binaryReader.ReadChars(count));
		if (text != "TS3Pack")
		{
			throw new InvalidDataException($"Invalid Magic: Expected [TS3Pack], Found [{text}]");
		}
		ushort num = binaryReader.ReadUInt16();
		int count2 = binaryReader.ReadInt32();
		Manifest = new XmlDocument();
		string @string = Encoding.UTF8.GetString(binaryReader.ReadBytes(count2));
		MemoryStream memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(@string), writable: false);
		Manifest.Load(memoryStream);
		memoryStream.Close();
		long position = DataStore.Position;
		XmlNode firstChild = Manifest.FirstChild;
		XmlElement xmlElement = (XmlElement)firstChild.NextSibling;
		dynamic val = new ExpandoObject();
		val.Type = xmlElement.GetAttribute("Type");
		val.SubType = xmlElement.GetAttribute("SubType");
		Type = val.Type;
		SubType = val.SubType;
		val.Localized = new Dictionary<string, object>();
		val.Packages = new List<object>();
		for (XmlElement xmlElement2 = xmlElement.FirstChild as XmlElement; xmlElement2 != null; xmlElement2 = xmlElement2.NextSibling as XmlElement)
		{
			if (xmlElement2.Name == "LocalizedNames")
			{
				for (XmlElement xmlElement3 = xmlElement2.FirstChild as XmlElement; xmlElement3 != null; xmlElement3 = xmlElement3.NextSibling as XmlElement)
				{
					string attribute = xmlElement3.GetAttribute("Language");
					if ((!val.Localized.ContainsKey(attribute)))
					{
						val.Localized.Add(attribute, new ExpandoObject());
					}
					val.Localized[attribute].Name = xmlElement3.InnerText;
				}
			}
			else if (xmlElement2.Name == "LocalizedDescriptions")
			{
				for (XmlElement xmlElement3 = xmlElement2.FirstChild as XmlElement; xmlElement3 != null; xmlElement3 = xmlElement3.NextSibling as XmlElement)
				{
					string attribute = xmlElement3.GetAttribute("Language");
					if ((!val.Localized.ContainsKey(attribute)))
					{
						val.Localized.Add(attribute, new ExpandoObject());
					}
					val.Localized[attribute].Description = xmlElement3.InnerText;
				}
			}
			else if (xmlElement2.Name == "PackagedFile")
			{
				XmlElement xmlElement3 = xmlElement2.FirstChild as XmlElement;
				dynamic val2 = new ExpandoObject();
				while (xmlElement3 != null)
				{
					if (xmlElement3.Name == "Length")
					{
						val2.Length = long.Parse(xmlElement3.InnerText);
					}
					else if (xmlElement3.Name == "Offset")
					{
						val2.Offset = long.Parse(xmlElement3.InnerText);
					}
					else if (xmlElement3.Name == "Name")
					{
						val2.Name = xmlElement3.InnerText;
						if ((val2.Name as string).EndsWith(".package"))
						{
							val2.IsPackage = true;
						}
						else
						{
							val2.IsPackage = false;
						}
					}
					else
					{
						(val2 as IDictionary<string, object>).Add(xmlElement3.Name, xmlElement3.InnerText);
					}
					xmlElement3 = xmlElement3.NextSibling as XmlElement;
				}
				val.Packages.Add(val2);
			}
			else
			{
				(val as IDictionary<string, object>).Add(xmlElement2.Name, xmlElement2.InnerText);
			}
		}
		IsCorrupt = false;
		DBPFPackage dBPFPackage = null;
		foreach (dynamic item in val.Packages)
		{
			if (item.IsPackage)
			{
				try
				{
					dBPFPackage = new DBPFPackage();
					dBPFPackage.Import(new SubStream(DataStore, position + item.Offset, item.Length), UseBuffer: false);
					dBPFPackage.GUID = Path.GetFileNameWithoutExtension(item.Name);
					if (position + item.Offset + item.Length > DataStore.Length)
					{
						IsCorrupt = true;
					}
				}
				catch (InvalidDataException)
				{
					dBPFPackage = null;
					IsEncrypted = true;
				}
				if (dBPFPackage != null)
				{
					Packages.Add(dBPFPackage);
				}
			}
			else
			{
				Thumbnails.Add(new SubStream(DataStore, position + item.Offset, item.Length));
			}
		}
	}

	public void Close()
	{
		Dispose();
	}

	public void Clear()
	{
		foreach (DBPFPackage package in Packages)
		{
			package.Close();
		}
		Packages.Clear();
		DataStore.Close();
		DataStore = null;
	}

	public void Dispose()
	{
		Dispose(disposing: true);
		GC.SuppressFinalize(this);
	}

	protected virtual void Dispose(bool disposing)
	{
		if (!disposed)
		{
			if (disposing)
			{
				Clear();
			}
			disposed = true;
		}
	}

	~Sims3Pack()
	{
		Dispose(disposing: false);
	}
}
