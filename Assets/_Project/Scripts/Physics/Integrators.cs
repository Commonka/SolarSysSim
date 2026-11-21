public interface IIntegrator
{
    string Name { get; }
    void Step(CelestialBody[] bodies, double dt);
}

public abstract class IntegratorBase : IIntegrator
{
    public abstract string Name { get; }
    public abstract void Step(CelestialBody[] bodies, double dt);

    protected Vector3d[] x0, v0, xt, a1, a2, a3, a4, v2, v3, v4;
    protected double[] m;

    protected void Ensure(int n)
    {
        if (m != null && m.Length == n) return;
        m = new double[n];
        x0 = new Vector3d[n]; v0 = new Vector3d[n]; xt = new Vector3d[n];
        a1 = new Vector3d[n]; a2 = new Vector3d[n]; a3 = new Vector3d[n]; a4 = new Vector3d[n];
        v2 = new Vector3d[n]; v3 = new Vector3d[n]; v4 = new Vector3d[n];
    }

    protected void Load(CelestialBody[] b)
    {
        Ensure(b.Length);
        for (int i = 0; i < b.Length; i++)
        {
            x0[i] = b[i].position; v0[i] = b[i].velocity; m[i] = b[i].mass;
        }
    }

    protected void Store(CelestialBody[] b)
    {
        for (int i = 0; i < b.Length; i++)
        {
            b[i].position = x0[i]; b[i].velocity = v0[i];
        }
    }
}

// Явный Эйлер, 1-й порядок. Энергия растёт, орбиты раскручиваются.
public class EulerIntegrator : IntegratorBase
{
    public override string Name => "Euler";

    public override void Step(CelestialBody[] b, double dt)
    {
        Load(b);
        Gravity.ComputeAccelerations(x0, m, a1);
        for (int i = 0; i < b.Length; i++)
        {
            x0[i] += v0[i] * dt;
            v0[i] += a1[i] * dt;
        }
        Store(b);
    }
}

// Velocity Verlet (leapfrog), 2-й порядок, симплектический.
public class LeapfrogIntegrator : IntegratorBase
{
    public override string Name => "Leapfrog";

    public override void Step(CelestialBody[] b, double dt)
    {
        Load(b);
        Gravity.ComputeAccelerations(x0, m, a1);
        for (int i = 0; i < b.Length; i++)
            x0[i] += v0[i] * dt + a1[i] * (0.5 * dt * dt);
        Gravity.ComputeAccelerations(x0, m, a2);
        for (int i = 0; i < b.Length; i++)
            v0[i] += (a1[i] + a2[i]) * (0.5 * dt);
        Store(b);
    }
}

// Классический Рунге-Кутта 4-го порядка.
public class RK4Integrator : IntegratorBase
{
    public override string Name => "RK4";

    public override void Step(CelestialBody[] b, double dt)
    {
        Load(b);
        int n = b.Length;
        double h2 = dt * 0.5;

        // k1
        Gravity.ComputeAccelerations(x0, m, a1);
        // k2
        for (int i = 0; i < n; i++) { xt[i] = x0[i] + v0[i] * h2; v2[i] = v0[i] + a1[i] * h2; }
        Gravity.ComputeAccelerations(xt, m, a2);
        // k3
        for (int i = 0; i < n; i++) { xt[i] = x0[i] + v2[i] * h2; v3[i] = v0[i] + a2[i] * h2; }
        Gravity.ComputeAccelerations(xt, m, a3);
        // k4
        for (int i = 0; i < n; i++) { xt[i] = x0[i] + v3[i] * dt; v4[i] = v0[i] + a3[i] * dt; }
        Gravity.ComputeAccelerations(xt, m, a4);

        for (int i = 0; i < n; i++)
        {
            Vector3d dx = (v0[i] + 2.0 * v2[i] + 2.0 * v3[i] + v4[i]) * (dt / 6.0);
            Vector3d dv = (a1[i] + 2.0 * a2[i] + 2.0 * a3[i] + a4[i]) * (dt / 6.0);
            x0[i] += dx;
            v0[i] += dv;
        }
        Store(b);
    }
}