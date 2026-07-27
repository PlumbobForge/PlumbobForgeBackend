using System;
using System.Text;

namespace Sims3.SimIFace;

public struct Vector2
{
	public static readonly Vector2 Empty = new Vector2(0f, 0f);

	public static readonly Vector2 Invalid = new Vector2(float.PositiveInfinity, float.PositiveInfinity);

	public static readonly Vector2 OutOfWorld = new Vector2(-20000f, -20000f);

	public static readonly Vector2 Zero = Empty;

	public static readonly Vector2 Origin = Empty;

	public static readonly Vector2 UnitX = new Vector2(1f, 0f);

	public static readonly Vector2 UnitY = new Vector2(0f, 1f);

	public static readonly Vector2 UnitZ = UnitY;

	public float x;

	public float y;

	public Vector2(float _x, float _y)
	{
		x = _x;
		y = _y;
	}

	public static bool operator ==(Vector2 a, Vector2 b)
	{
		return a.x == b.x && a.y == b.y;
	}

	public static bool operator !=(Vector2 a, Vector2 b)
	{
		return a.x != b.x || a.y != b.y;
	}

	public override bool Equals(object obj)
	{
		if (obj == null || obj.GetType() != GetType())
		{
			return false;
		}
		Vector2 vector = (Vector2)obj;
		return x == vector.x && y == vector.y;
	}

	public override int GetHashCode()
	{
		return x.GetHashCode() ^ y.GetHashCode();
	}

	public override string ToString()
	{
		StringBuilder stringBuilder = new StringBuilder();
		stringBuilder.Append("(");
		stringBuilder.Append(x.ToString("0.0000"));
		stringBuilder.Append(", ");
		stringBuilder.Append(y.ToString("0.0000"));
		stringBuilder.Append(")");
		return stringBuilder.ToString();
	}

	public static bool TryParse(string values, out Vector2 vector2)
	{
		vector2 = default(Vector2);
		if (values == null)
		{
			return false;
		}
		string text = values.Replace(" ", "");
		text = text.Replace("f", "");
		string[] array = text.Split(',');
		return array.Length == 2 && float.TryParse(array[0], out vector2.x) && float.TryParse(array[1], out vector2.y);
	}

	public void Set(float _x, float _y)
	{
		x = _x;
		y = _y;
	}

	public static Vector2 operator -(Vector2 vec)
	{
		return new Vector2(0f - vec.x, 0f - vec.y);
	}

	public static Vector2 operator +(Vector2 vec, float scaler)
	{
		return new Vector2(vec.x + scaler, vec.y + scaler);
	}

	public static Vector2 operator -(Vector2 vec, float scaler)
	{
		return new Vector2(vec.x - scaler, vec.y - scaler);
	}

	public static Vector2 operator +(Vector2 a, Vector2 b)
	{
		return new Vector2(a.x + b.x, a.y + b.y);
	}

	public static Vector2 operator -(Vector2 a, Vector2 b)
	{
		return new Vector2(a.x - b.x, a.y - b.y);
	}

	public static Vector2 operator *(Vector2 vec, float scaler)
	{
		return new Vector2(vec.x * scaler, vec.y * scaler);
	}

	public static Vector2 operator /(Vector2 vec, float scaler)
	{
		return new Vector2(vec.x / scaler, vec.y / scaler);
	}

	public static float operator *(Vector2 a, Vector2 b)
	{
		return a.x * b.x + a.y * b.y;
	}

	public Vector2 Multiply(Vector2 b)
	{
		return new Vector2(x * b.x, y * b.y);
	}

	public static Vector2 operator /(Vector2 a, Vector2 b)
	{
		return new Vector2(a.x / b.x, a.y / b.y);
	}

	public static Vector2 Min(Vector2 a, Vector2 b)
	{
		return new Vector2(Math.Min(a.x, b.x), Math.Min(a.y, b.y));
	}

	public static Vector2 Max(Vector2 a, Vector2 b)
	{
		return new Vector2(Math.Max(a.x, b.x), Math.Max(a.y, b.y));
	}

	public static Vector2 Floor(Vector2 v)
	{
		return new Vector2(MathUtils.Floor(v.x), MathUtils.Floor(v.y));
	}

	public static Vector2 Clamp(Vector2 a, Vector2 clampMin, Vector2 clampMax)
	{
		return new Vector2(MathUtils.Clamp(a.x, clampMin.x, clampMax.x), MathUtils.Clamp(a.y, clampMin.y, clampMax.y));
	}

	public float Length()
	{
		return (float)Math.Sqrt(LengthSqr());
	}

	public float LengthSqr()
	{
		return x * x + y * y;
	}

	public static Vector2 Normalize(Vector2 vec, out float length)
	{
		length = vec.Length();
		if (Math.Abs(length) < 1E-05f)
		{
			return Zero;
		}
		float num = 1f / length;
		return new Vector2(vec.x * num, vec.y * num);
	}

	public static Vector2 Normalize(Vector2 vec)
	{
		float length;
		return Normalize(vec, out length);
	}

	public float Normalize()
	{
		this = Normalize(this, out var length);
		return length;
	}
}
