using System.Collections.Generic;
using System.IO;
using System.Text;

namespace S3ForgeTools.GameFiles.Resources;

public class Resource_RIG
{
	public string Name { get; private set; }

	public List<Resource_RIGBone> Bones { get; private set; }

	public Resource_RIG()
	{
		Bones = new List<Resource_RIGBone>();
	}

	public Resource_RIG(string Name)
		: this()
	{
		this.Name = Name;
	}

	public Resource_RIG(Stream Source)
		: this()
	{
		BinaryReader reader = new BinaryReader(Source);
		Import(reader);
	}

	public void Export(BinaryWriter Writer)
	{
		Writer.Write(4u);
		Writer.Write(2u);
		Writer.Write(Bones.Count);
		foreach (Resource_RIGBone bone in Bones)
		{
			bone.Export(Writer);
		}
		Writer.Write(Name.Length);
		Writer.Write(Encoding.ASCII.GetBytes(Name));
		Writer.Write(0u);
	}

	public void Import(BinaryReader Reader)
	{
		Bones.Clear();
		uint num = Reader.ReadUInt32();
		uint num2 = Reader.ReadUInt32();
		if ((num != 4 || num2 != 2) && num > 4096)
		{
			return;
		}
		uint num3 = Reader.ReadUInt32();
		for (int i = 0; i < num3; i++)
		{
			Resource_RIGBone resource_RIGBone = new Resource_RIGBone();
			resource_RIGBone.Import(Reader);
			Bones.Add(resource_RIGBone);
		}
		foreach (Resource_RIGBone bone in Bones)
		{
			if (bone.GetParentIndex() != -1)
			{
				bone.SetParent(Bones[bone.GetParentIndex()]);
			}
		}
		uint count = Reader.ReadUInt32();
		Name = Encoding.ASCII.GetString(Reader.ReadBytes((int)count));
		uint num4 = Reader.ReadUInt32();
	}

	public void SaveToFile(string FileName)
	{
		using BinaryWriter writer = new BinaryWriter(File.OpenWrite(FileName));
		Export(writer);
	}

	public void SaveToStream(Stream OutputStream)
	{
		BinaryWriter writer = new BinaryWriter(OutputStream);
		Export(writer);
	}

	private int LookupBoneName(string Name)
	{
		int num = 0;
		foreach (Resource_RIGBone bone in Bones)
		{
			if (bone.Name == Name)
			{
				return num;
			}
			num++;
		}
		return 0;
	}

	public void FixupUnknown()
	{
		foreach (Resource_RIGBone bone in Bones)
		{
			if (bone.Name.Contains("_L_"))
			{
				string name = bone.Name.Replace("_L_", "_R_");
				bone.MirrorIndex = LookupBoneName(name);
			}
			else if (bone.Name.Contains("_R_"))
			{
				string name = bone.Name.Replace("_R_", "_L_");
				bone.MirrorIndex = LookupBoneName(name);
				bone.Flags = 63u;
			}
		}
	}
}
