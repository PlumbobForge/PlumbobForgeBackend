using System;

namespace Sims3.SimIFace;

public struct Quaternion
{
	public static readonly Quaternion Identity = new Quaternion(0f, 0f, 0f, 1f);

	public Vector3 v;

	public float n;

	public float Magnitude => (float)Math.Sqrt(n * n + v.x * v.x + v.y * v.y + v.z * v.z);

	public Vector3 Vector => v;

	public float Scaler => n;

	public Quaternion(float _x, float _y, float _z, float _n)
	{
		n = _n;
		v = new Vector3(_x, _y, _z);
	}

	public Matrix44 ToMatrix()
	{
		Matrix44 gIdentMatrix = MathConstants.gIdentMatrix44;
		gIdentMatrix.right.x = n * n + v.x * v.x - v.y * v.y - v.z * v.z;
		gIdentMatrix.right.y = 2f * v.x * v.y - 2f * v.z * n;
		gIdentMatrix.right.z = 2f * v.x * v.z + 2f * v.y * n;
		gIdentMatrix.up.x = 2f * v.x * v.y + 2f * v.z * n;
		gIdentMatrix.up.y = n * n - v.x * v.x + v.y * v.y - v.z * v.z;
		gIdentMatrix.up.z = 2f * v.y * v.z - 2f * v.x * n;
		gIdentMatrix.at.x = 2f * v.z * v.x - 2f * v.y * n;
		gIdentMatrix.at.y = 2f * v.z * v.y + 2f * v.x * n;
		gIdentMatrix.at.z = n * n - v.x * v.x - v.y * v.y + v.z * v.z;
		return gIdentMatrix;
	}

	public static Quaternion operator ~(Quaternion a)
	{
		return new Quaternion(0f - a.v.x, 0f - a.v.y, 0f - a.v.z, a.n);
	}

	public static Quaternion operator +(Quaternion a, Quaternion b)
	{
		return new Quaternion(a.v.x + b.v.x, a.v.y + b.v.y, a.v.z + b.v.z, a.n + b.n);
	}

	public static Quaternion operator -(Quaternion a, Quaternion b)
	{
		return new Quaternion(a.v.x - b.v.x, a.v.y - b.v.y, a.v.z - b.v.z, a.n - b.n);
	}

	public static Quaternion operator *(Quaternion a, Quaternion b)
	{
		return new Quaternion(a.n * b.v.x + a.v.x * b.n + a.v.y * b.v.z - a.v.z * b.v.y, a.n * b.v.y + a.v.y * b.n + a.v.z * b.v.x - a.v.x * b.v.z, a.n * b.v.z + a.v.z * b.n + a.v.x * b.v.y - a.v.y * b.v.x, a.n * b.n - a.v.x * b.v.x - a.v.y * b.v.y - a.v.z * b.v.z);
	}

	public static Quaternion operator *(Quaternion a, float s)
	{
		return new Quaternion(a.v.x * s, a.v.y * s, a.v.z * s, a.n * s);
	}

	public static Quaternion operator *(float s, Quaternion a)
	{
		return new Quaternion(a.v.x * s, a.v.y * s, a.v.z * s, a.n * s);
	}

	public static Quaternion operator *(Quaternion q, Vector3 v)
	{
		return new Quaternion(q.n * v.x + q.v.y * v.z - q.v.z * v.y, q.n * v.y + q.v.z * v.x - q.v.x * v.z, q.n * v.z + q.v.x * v.y - q.v.y * v.x, 0f - (q.v.x * v.x + q.v.y * v.y + q.v.z * v.z));
	}

	public static Quaternion operator *(Vector3 v, Quaternion q)
	{
		return new Quaternion(q.n * v.x + q.v.z * v.y - q.v.y * v.z, q.n * v.y + q.v.x * v.z - q.v.z * v.x, q.n * v.z + q.v.y * v.x - q.v.x * v.y, 0f - (q.v.x * v.x + q.v.y * v.y + q.v.z * v.z));
	}

	public static Quaternion operator /(Quaternion q, float s)
	{
		return new Quaternion(q.v.x / s, q.v.y / s, q.v.z / s, q.n / s);
	}

	public static Quaternion operator /(float s, Quaternion q)
	{
		return new Quaternion(q.v.x / s, q.v.y / s, q.v.z / s, q.n / s);
	}

	public static float GetAngle(Quaternion q)
	{
		return (float)(2.0 * Math.Acos(q.n));
	}

	public static Vector3 GetAxis(Quaternion q)
	{
		Vector3 vector = q.v;
		float num = vector.Length();
		if (num <= 0.0001f)
		{
			return new Vector3(0f, 0f, 0f);
		}
		return vector / num;
	}

	public static Quaternion Rotate(Quaternion q1, Quaternion q2)
	{
		return q1 * q2 * ~q1;
	}

	public static Vector3 VRotate(Quaternion q, Vector3 v)
	{
		return (q * v * ~q).v;
	}

	public static Quaternion MakeFromEulerAngles(float x, float y, float z)
	{
		double num = x;
		double num2 = y;
		double num3 = z;
		double num4 = Math.Cos(0.5 * num3);
		double num5 = Math.Cos(0.5 * num2);
		double num6 = Math.Cos(0.5 * num);
		double num7 = Math.Sin(0.5 * num3);
		double num8 = Math.Sin(0.5 * num2);
		double num9 = Math.Sin(0.5 * num);
		double num10 = num4 * num5;
		double num11 = num7 * num8;
		double num12 = num4 * num8;
		double num13 = num7 * num5;
		return new Quaternion((float)(num10 * num9 - num11 * num6), (float)(num12 * num6 + num13 * num9), (float)(num13 * num6 - num12 * num9), (float)(num10 * num6 + num11 * num9));
	}

	public static Quaternion MakeFromForwardVector(Vector3 forward)
	{
		forward = forward.Normalize();
		Vector3 unitY = Vector3.UnitY;
		Vector3 b = Vector3.CrossProduct(unitY, forward);
		Vector3 vector = Vector3.CrossProduct(forward, b);
		Matrix44 xf = default(Matrix44);
		xf.right = new Vector4(b);
		xf.up = new Vector4(vector);
		xf.at = new Vector4(forward);
		xf.pos = new Vector4(0f, 0f, 0f, 1f);
		return MakeFromMatrix44(xf);
	}

	public static Quaternion MakeFromMatrix44(Matrix44 xf)
	{
		float x = 0f;
		float y = 0f;
		float z = 0f;
		float num = 0f;
		float num2 = xf.right.x + xf.up.y + xf.at.z;
		if (num2 >= 0f)
		{
			float num3 = (float)Math.Sqrt(num2 + 1f);
			num = 0.5f * num3;
			num3 = 0.5f / num3;
			x = (xf.up.z - xf.at.y) * num3;
			y = (xf.at.x - xf.right.z) * num3;
			z = (xf.right.y - xf.up.x) * num3;
		}
		else
		{
			int num4 = 0;
			if (xf.up.y > xf.right.x)
			{
				num4 = 1;
				if (xf.at.z > xf.up.y)
				{
					num4 = 2;
				}
			}
			else if (xf.at.z > xf.right.x)
			{
				num4 = 2;
			}
			switch (num4)
			{
			case 0:
			{
				float num3 = (float)Math.Sqrt(xf.right.x - (xf.up.y + xf.at.z) + 1f);
				x = 0.5f * num3;
				num3 = 0.5f / num3;
				y = (xf.up.x + xf.right.y) * num3;
				z = (xf.right.z + xf.at.x) * num3;
				num = (xf.up.z - xf.at.y) * num3;
				break;
			}
			case 1:
			{
				float num3 = (float)Math.Sqrt(xf.up.y - (xf.at.z + xf.right.x) + 1f);
				y = 0.5f * num3;
				num3 = 0.5f / num3;
				z = (xf.at.y + xf.up.z) * num3;
				x = (xf.up.x + xf.right.y) * num3;
				num = (xf.at.x - xf.right.z) * num3;
				break;
			}
			case 2:
			{
				float num3 = (float)Math.Sqrt(xf.at.z - (xf.right.x + xf.up.y) + 1f);
				z = 0.5f * num3;
				num3 = 0.5f / num3;
				x = (xf.right.z + xf.at.x) * num3;
				y = (xf.at.y + xf.up.z) * num3;
				num = (xf.right.y - xf.up.x) * num3;
				break;
			}
			}
		}
		return new Quaternion(x, y, z, num);
	}

	public Quaternion Normalize()
	{
		if (Scaler < 0f)
		{
			return new Quaternion(0f - v.x, 0f - v.y, 0f - v.z, 0f - Scaler);
		}
		return new Quaternion(v.x, v.y, v.z, Scaler);
	}

	public override string ToString()
	{
		return $"V:{Vector} M:{Scaler}";
	}
}
