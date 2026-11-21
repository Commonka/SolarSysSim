using System;

public static class Gravity
{
    // Ускорения всех тел при заданных позициях. Каждая пара считается один раз (3-й закон Ньютона).
    public static void ComputeAccelerations(Vector3d[] pos, double[] mass, Vector3d[] acc)
    {
        int n = pos.Length;
        for (int i = 0; i < n; i++) acc[i] = Vector3d.zero;

        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                Vector3d d = pos[j] - pos[i];
                double r2 = d.SqrMagnitude;
                double invR3 = 1.0 / (r2 * Math.Sqrt(r2));
                acc[i] += d * (Constants.G * mass[j] * invR3);
                acc[j] -= d * (Constants.G * mass[i] * invR3);
            }
        }
    }

    public static double TotalEnergy(CelestialBody[] b)
    {
        double e = 0;
        for (int i = 0; i < b.Length; i++)
        {
            e += 0.5 * b[i].mass * b[i].velocity.SqrMagnitude;
            for (int j = i + 1; j < b.Length; j++)
                e -= Constants.G * b[i].mass * b[j].mass / (b[j].position - b[i].position).Magnitude;
        }
        return e;
    }
}