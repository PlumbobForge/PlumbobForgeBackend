using System;
using System.Text;

namespace Sims3.SimIFace;

public struct Vector3
{
	public static readonly Vector3 Empty = new Vector3(0f, 0f, 0f);

	public static readonly Vector3 Invalid = new Vector3(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

	public static readonly Vector3 OutOfWorld = new Vector3(-20000f, -20000f, -20000f);

	public static readonly Vector3 Zero = Empty;

	public static readonly Vector3 Origin = new Vector3(0f, 0f, 0f);

	public static readonly Vector3 UnitX = new Vector3(1f, 0f, 0f);

	public static readonly Vector3 UnitY = new Vector3(0f, 1f, 0f);

	public static readonly Vector3 UnitZ = new Vector3(0f, 0f, 1f);

	public float x;

	public float y;

	public float z;

	public Vector4 P4 => new Vector4(x, y, z, 1f);

	public Vector4 V4 => new Vector4(x, y, z, 0f);

	public Vector3(Vector4 v)
	{
		x = v.x;
		y = v.y;
		z = v.z;
	}

	public Vector3(Vector3 v)
	{
		x = v.x;
		y = v.y;
		z = v.z;
	}

	public Vector3(Vector2 v)
	{
		x = v.x;
		y = v.y;
		z = 0f;
	}

	public static Vector3 CreateWorldVector3FromVector2(Vector2 v)
	{
		return new Vector3(v.x, 0f, v.y);
	}

	public Vector3(float _x, float _y, float _z)
	{
		x = _x;
		y = _y;
		z = _z;
	}

	public static bool operator ==(Vector3 a, Vector3 b)
	{
		return a.x == b.x && a.y == b.y && a.z == b.z;
	}

	public static bool operator !=(Vector3 a, Vector3 b)
	{
		return a.x != b.x || a.y != b.y || a.z != b.z;
	}

	public override bool Equals(object obj)
	{
		if (obj == null || obj.GetType() != GetType())
		{
			return false;
		}
		Vector3 vector = (Vector3)obj;
		return x == vector.x && y == vector.y && z == vector.z;
	}

	public override int GetHashCode()
	{
		return x.GetHashCode() ^ y.GetHashCode() ^ z.GetHashCode();
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("(");
		stringBuilder.Append(x.ToString("0.0000"));
		stringBuilder.Append(", ");
		stringBuilder.Append(y.ToString("0.0000"));
		stringBuilder.Append(", ");
		stringBuilder.Append(z.ToString("0.0000"));
		stringBuilder.Append(")");
		return stringBuilder.ToString();
	}

	public static bool TryParse(string values, out Vector3 vector3)
	{
		vector3 = default(Vector3);
		if (values == null)
		{
			return false;
		}
		string text = values.Replace(" ", "");
		text = text.Replace("f", "");
		string[] array = text.Split(',');
		return array.Length == 3 && float.TryParse(array[0], out vector3.x) && float.TryParse(array[1], out vector3.y) && float.TryParse(array[2], out vector3.z);
	}

	public void Set(float _x, float _y, float _z)
	{
		x = _x;
		y = _y;
		z = _z;
	}

	public static Vector3 operator -(Vector3 vec)
	{
		return new Vector3(0f - vec.x, 0f - vec.y, 0f - vec.z);
	}

	public static Vector3 operator +(Vector3 vec, float scaler)
	{
		return new Vector3(vec.x + scaler, vec.y + scaler, vec.z + scaler);
	}

	public static Vector3 operator -(Vector3 vec, float scaler)
	{
		return new Vector3(vec.x - scaler, vec.y - scaler, vec.z - scaler);
	}

	public static Vector3 operator +(Vector3 a, Vector3 b)
	{
		return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
	}

	public static Vector3 operator -(Vector3 a, Vector3 b)
	{
		return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
	}

	public static Vector3 operator *(Vector3 vec, float scaler)
	{
		return new Vector3(vec.x * scaler, vec.y * scaler, vec.z * scaler);
	}

	public static Vector3 operator /(Vector3 vec, float scaler)
	{
		return new Vector3(vec.x / scaler, vec.y / scaler, vec.z / scaler);
	}

	public static float operator *(Vector3 a, Vector3 b)
	{
		return a.x * b.x + a.y * b.y + a.z * b.z;
	}

	public Vector3 Multiply(Vector3 b)
	{
		return new Vector3(x * b.x, y * b.y, z * b.z);
	}

	public static Vector3 operator /(Vector3 a, Vector3 b)
	{
		return new Vector3(a.x / b.x, a.y / b.y, a.z / b.z);
	}

	public static Vector3 Min(Vector3 a, Vector3 b)
	{
		return new Vector3(Math.Min(a.x, b.x), Math.Min(a.y, b.y), Math.Min(a.z, b.z));
	}

	public static Vector3 Max(Vector3 a, Vector3 b)
	{
		return new Vector3(Math.Max(a.x, b.x), Math.Max(a.y, b.y), Math.Max(a.z, b.z));
	}

	public static Vector3 Floor(Vector3 v)
	{
		return new Vector3(MathUtils.Floor(v.x), MathUtils.Floor(v.y), MathUtils.Floor(v.z));
	}

	public static Vector3 Lerp(Vector3 a, Vector3 b, float t)
	{
		return new Vector3(a.x + (b.x - a.x) * t, a.y + (b.y - a.y) * t, a.z + (b.z - a.z) * t);
	}

	public static Vector3 Clamp(Vector3 a, Vector3 clampMin, Vector3 clampMax)
	{
		return new Vector3(MathUtils.Clamp(a.x, clampMin.x, clampMax.x), MathUtils.Clamp(a.y, clampMin.y, clampMax.y), MathUtils.Clamp(a.z, clampMin.z, clampMax.z));
	}

	public static Vector3 CrossProduct(Vector3 a, Vector3 b)
	{
		return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x);
	}

	public Vector3 Normalize()
	{
		float num = Length();
		if (Math.Abs(num) < 1E-05f)
		{
			x = (y = (z = 0f));
			return this;
		}
		num = 1f / num;
		x *= num;
		y *= num;
		z *= num;
		return this;
	}

	public float Length()
	{
		return (float)Math.Sqrt(LengthSqr());
	}

	public float LengthSqr()
	{
		return x * x + y * y + z * z;
	}

	public static Vector3 operator *(Matrix44 xf, Vector3 vec)
	{
		Vector3 vector = default(Vector3);
		vector = xf.right.V3 * vec.x;
		vector += xf.up.V3 * vec.y;
		vector += xf.at.V3 * vec.z;
		return vector + xf.pos.V3;
	}

	public bool IsSimilarTo(Vector3 v)
	{
		return (this - v).LengthSqr() < 9.9999994E-11f;
	}
}
