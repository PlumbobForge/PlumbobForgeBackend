using System.IO;
using System.Text;
using S3Launcher;
using Sims3.SimIFace;

namespace S3ForgeTools.GameFiles.Resources;

public class Resource_RIGBone
{
	private int ParentIndex;

	public Vector3 Position { get; set; }

	public Quaternion Rotation { get; set; }

	public Vector3 Scaling { get; set; }

	public string Name { get; set; }

	public uint NameHash { get; private set; }

	public Resource_RIGBone Parent { get; private set; }

	public uint Flags { get; set; }

	public int MirrorIndex { get; set; }

	public void SetParentIndex(int ParentIndex)
	{
		this.ParentIndex = ParentIndex;
	}

	public void Export(BinaryWriter Writer)
	{
		Writer.Write(Position.x);
		Writer.Write(Position.y);
		Writer.Write(Position.z);
		Writer.Write(Rotation.Vector.x);
		Writer.Write(Rotation.Vector.y);
		Writer.Write(Rotation.Vector.z);
		Writer.Write(Rotation.Scaler);
		Writer.Write(Scaling.x);
		Writer.Write(Scaling.y);
		Writer.Write(Scaling.z);
		Writer.Write(Name.Length);
		Writer.Write(Encoding.ASCII.GetBytes(Name));
		Writer.Write(MirrorIndex);
		Writer.Write(ParentIndex);
		Writer.Write(FNV.FNV32(Name));
		Writer.Write(Flags);
	}

	public void Import(BinaryReader Reader)
	{
		Position = new Vector3(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
		Rotation = new Quaternion(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
		Scaling = new Vector3(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
		uint count = Reader.ReadUInt32();
		Name = Encoding.ASCII.GetString(Reader.ReadBytes((int)count));
		MirrorIndex = Reader.ReadInt32();
		ParentIndex = Reader.ReadInt32();
		NameHash = Reader.ReadUInt32();
		Flags = Reader.ReadUInt32();
	}

	public void SetParent(Resource_RIGBone Parent)
	{
		this.Parent = Parent;
	}

	public int GetParentIndex()
	{
		return ParentIndex;
	}

	public override string ToString()
	{
		return $"{Name} P:{Position} R:{Rotation} S:{Scaling}";
	}
}
