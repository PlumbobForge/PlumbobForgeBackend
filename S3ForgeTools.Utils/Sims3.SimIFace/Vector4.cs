using System;
using System.Text;

namespace Sims3.SimIFace;

public struct Vector4
{
	public static readonly Vector4 Empty = new Vector4(0f, 0f, 0f, 0f);

	public static readonly Vector4 Invalid = new Vector4(float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity, float.PositiveInfinity);

	public static readonly Vector4 OutOfWorld = new Vector4(-20000f, -20000f, -20000f, 1f);

	public static readonly Vector4 Zero = Empty;

	public static readonly Vector4 Origin = Empty;

	public static readonly Vector4 UnitX = new Vector4(1f, 0f, 0f, 0f);

	public static readonly Vector4 UnitY = new Vector4(0f, 1f, 0f, 0f);

	public static readonly Vector4 UnitZ = new Vector4(0f, 0f, 1f, 0f);

	public static readonly Vector4 UnitW = new Vector4(0f, 0f, 0f, 1f);

	public float x;

	public float y;

	public float z;

	public float w;

	public Vector3 V3
	{
		get
		{
			return new Vector3(this);
		}
		set
		{
			x = value.x;
			y = value.y;
			z = value.z;
		}
	}

	public Vector4(Vector4 v)
	{
		x = v.x;
		y = v.y;
		z = v.z;
		w = v.w;
	}

	public Vector4(Vector3 v)
	{
		x = v.x;
		y = v.y;
		z = v.z;
		w = 1f;
	}

	public Vector4(Vector2 v)
	{
		x = v.x;
		y = v.y;
		z = 0f;
		w = 1f;
	}

	public static Vector4 CreateWorldVector4FromVector2(Vector2 v)
	{
		return new Vector4(v.x, 0f, v.y, 1f);
	}

	public Vector4(float _x, float _y, float _z, float _w)
	{
		x = _x;
		y = _y;
		z = _z;
		w = _w;
	}

	public Vector4(float _x, float _y, float _z)
		: this(_x, _y, _z, 1f)
	{
	}

	public void Set(float _x, float _y, float _z, float _w)
	{
		x = _x;
		y = _y;
		z = _z;
		w = _w;
	}

	public void Set(float _x, float _y, float _z)
	{
		x = _x;
		y = _y;
		z = _z;
		w = 1f;
	}

	public static Vector4 operator -(Vector4 vec)
	{
		return new Vector4(0f - vec.x, 0f - vec.y, 0f - vec.z, vec.w);
	}

	public static Vector4 operator +(Vector4 a, Vector4 b)
	{
		return new Vector4(a.x + b.x, a.y + b.y, a.z + b.z, a.w + b.w);
	}

	public static Vector4 operator -(Vector4 a, Vector4 b)
	{
		return new Vector4(a.x - b.x, a.y - b.y, a.z - b.z, a.w - b.w);
	}

	public static Vector4 operator *(Vector4 vec, float scaler)
	{
		return new Vector4(vec.x * scaler, vec.y * scaler, vec.z * scaler, vec.w * scaler);
	}

	public static Vector4 operator /(Vector4 vec, float scaler)
	{
		return new Vector4(vec.x / scaler, vec.y / scaler, vec.z / scaler, vec.w / scaler);
	}

	public static float operator *(Vector4 a, Vector4 b)
	{
		return a.x * b.x + a.y * b.y + a.z * b.z + a.w * b.w;
	}

	public float Length()
	{
		return (float)Math.Sqrt(LengthSqr());
	}

	public float LengthSqr()
	{
		return x * x + y * y + z * z + w * w;
	}

	public Vector4 Normalize()
	{
		float num = Length();
		if (Math.Abs(num) < 1E-05f)
		{
			x = (y = (z = (w = 0f)));
			return this;
		}
		num = 1f / num;
		x *= num;
		y *= num;
		z *= num;
		w *= num;
		return this;
	}

	public static Vector4 CrossProduct(Vector4 a, Vector4 b)
	{
		return new Vector4(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x, 0f);
	}

	public bool IsSimilarTo(Vector4 v)
	{
		return (this - v).LengthSqr() < 9.9999994E-11f;
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
		stringBuilder.Append(", ");
		stringBuilder.Append(w.ToString("0.0000"));
		stringBuilder.Append(")");
		return stringBuilder.ToString();
	}
}
