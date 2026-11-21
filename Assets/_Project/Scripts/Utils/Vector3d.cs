using System;
using UnityEngine;

[Serializable]
public struct Vector3d
{
    public double x, y, z;

    public Vector3d(double x, double y, double z)
    {
        this.x = x; this.y = y; this.z = z;
    }

    public static readonly Vector3d zero = new Vector3d(0, 0, 0);

    public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.x + b.x, a.y + b.y, a.z + b.z);
    public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.x - b.x, a.y - b.y, a.z - b.z);
    public static Vector3d operator -(Vector3d a) => new Vector3d(-a.x, -a.y, -a.z);
    public static Vector3d operator *(Vector3d a, double d) => new Vector3d(a.x * d, a.y * d, a.z * d);
    public static Vector3d operator *(double d, Vector3d a) => a * d;
    public static Vector3d operator /(Vector3d a, double d) => new Vector3d(a.x / d, a.y / d, a.z / d);

    public double SqrMagnitude => x * x + y * y + z * z;
    public double Magnitude => Math.Sqrt(SqrMagnitude);
    public Vector3d Normalized
    {
        get
        {
            double m = Magnitude;
            return m > 0 ? this / m : zero;
        }
    }

    public static double Dot(Vector3d a, Vector3d b) => a.x * b.x + a.y * b.y + a.z * b.z;

    public static Vector3d Cross(Vector3d a, Vector3d b) => new Vector3d(
        a.y * b.z - a.z * b.y,
        a.z * b.x - a.x * b.z,
        a.x * b.y - a.y * b.x);

    public Vector3 ToVector3() => new Vector3((float)x, (float)y, (float)z);

    public override string ToString() => $"({x:G6}, {y:G6}, {z:G6})";
}
