using System.IO;
using System.Text;

namespace Sims3.SimIFace;

public struct Matrix44
{
	public Vector4 right;

	public Vector4 up;

	public Vector4 at;

	public Vector4 pos;

	public static readonly Matrix44 Invalid = new Matrix44(Vector4.Invalid, Vector4.Invalid, Vector4.Invalid, Vector4.Invalid);

	public Matrix44(BinaryReader Reader)
	{
		right = new Vector4(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
		up = new Vector4(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
		at = new Vector4(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
		pos = new Vector4(Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle(), Reader.ReadSingle());
	}

	public Matrix44(Vector4 r, Vector4 u, Vector4 a, Vector4 p)
	{
		right = r;
		up = u;
		at = a;
		pos = p;
	}

	public void SetIdentity()
	{
		right.Set(1f, 0f, 0f, 0f);
		up.Set(0f, 1f, 0f, 0f);
		at.Set(0f, 0f, 1f, 0f);
		pos.Set(0f, 0f, 0f, 1f);
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("r: [");
		stringBuilder.Append(right.ToString());
		stringBuilder.Append("] u:[");
		stringBuilder.Append(up.ToString());
		stringBuilder.Append("] a:[");
		stringBuilder.Append(at.ToString());
		stringBuilder.Append("]");
		return stringBuilder.ToString();
	}

	public void DebugDrawMatrixTimed(float time)
	{
	}

	public Vector3 TransformVector(Vector3 vec)
	{
		return right.V3 * vec.x + up.V3 * vec.y + at.V3 * vec.z;
	}

	public Vector3 InverseTransformPoint(Vector3 vec)
	{
		return InverseTransformVector(vec - pos.V3);
	}

	public Vector3 InverseTransformVector(Vector3 vec)
	{
		return new Vector3(right.V3 * vec, up.V3 * vec, at.V3 * vec);
	}

	public static Matrix44 operator *(Matrix44 a, Matrix44 b)
	{
		Matrix44 gIdentMatrix = MathConstants.gIdentMatrix44;
		gIdentMatrix.right.V3 = b.right.V3 * a.right.V3.x + b.up.V3 * a.right.V3.y + b.at.V3 * a.right.V3.z;
		gIdentMatrix.up.V3 = b.right.V3 * a.up.V3.x + b.up.V3 * a.up.V3.y + b.at.V3 * a.up.V3.z;
		gIdentMatrix.at.V3 = b.right.V3 * a.at.V3.x + b.up.V3 * a.at.V3.y + b.at.V3 * a.at.V3.z;
		gIdentMatrix.pos.V3 = b.right.V3 * a.pos.V3.x + b.up.V3 * a.pos.V3.y + b.at.V3 * a.pos.V3.z + b.pos.V3;
		return gIdentMatrix;
	}
}
