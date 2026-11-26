using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

// Оскулирующая орбита: эллипс или гипербола из текущих r и v относительно главного тела.
// Меняешь массу или скорость — линия перестраивается мгновенно.
[DefaultExecutionOrder(100)]
public class OrbitRenderer : MonoBehaviour
{
    public static CelestialBody Highlight;   // выбранное тело, его орбита ярче

    public SimulationManager sim;
    public bool showOrbits = true;
    public float widthFactor = 0.003f;
    public float alpha = 0.35f;
    public float opacityMul = 1f;            // множитель прозрачности для слайдера HUD
    public Color tint = new Color(0.60f, 0.65f, 0.70f);

    const double UnitsPerKm = 0.001;
    const int Seg = 4096;

    class Orbit { public LineRenderer lr; public Transform root; public Color baseColor; }
    readonly Dictionary<CelestialBody, Orbit> orbits = new Dictionary<CelestialBody, Orbit>();
    readonly Vector3[] pts = new Vector3[Seg];
    readonly double[] cosT = new double[Seg], sinT = new double[Seg];
    Material mat;
    int syncedVersion = -1;

    void Awake()
    {
        for (int k = 0; k < Seg; k++)
        {
            double a = k / (double)Seg * 2 * System.Math.PI;
            cosT[k] = System.Math.Cos(a); sinT[k] = System.Math.Sin(a);
        }
    }

    Orbit Create(CelestialBody b)
    {
        var go = new GameObject(b.name + "Orbit");
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.alignment = LineAlignment.View;
        lr.numCornerVertices = 0;
        lr.sharedMaterial = mat;
        lr.startWidth = lr.endWidth = 1f;
        return new Orbit { lr = lr, root = go.transform, baseColor = Color.Lerp(b.color, tint, 0.55f) };
    }

    void Sync()
    {
        var present = new HashSet<CelestialBody>(sim.Bodies);
        var dead = new List<CelestialBody>();
        foreach (var kv in orbits) if (!present.Contains(kv.Key)) dead.Add(kv.Key);
        foreach (var b in dead) { Destroy(orbits[b].root.gameObject); orbits.Remove(b); }
        for (int i = 1; i < sim.Bodies.Length; i++)
            if (!orbits.ContainsKey(sim.Bodies[i])) orbits[sim.Bodies[i]] = Create(sim.Bodies[i]);
    }

    int ComputeOrbit(CelestialBody b, out CelestialBody primary, out bool closed)
    {
        closed = true;
        primary = sim.FindPrimary(b);
        if (primary == null) return 0;

        Vector3d r = b.position - primary.position;
        Vector3d v = b.velocity - primary.velocity;
        double rm = r.Magnitude;
        double mu = Constants.G * (primary.mass + b.mass);
        Vector3d h = Vector3d.Cross(r, v);
        double h2 = h.SqrMagnitude;
        if (rm <= 0 || h2 < 1e-10 * mu * rm) return 0;      // чисто радиальное падение

        Vector3d ev = Vector3d.Cross(v, h) / mu - r / rm;   // вектор эксцентриситета
        double e = ev.Magnitude, p = h2 / mu;
        Vector3d P = e > 1e-6 ? ev / e : r / rm;
        Vector3d Q = Vector3d.Cross(h / System.Math.Sqrt(h2), P);
        closed = e < 1.0;

        if (closed)
        {
            for (int k = 0; k < Seg; k++)
            {
                double rr = p / (1.0 + e * cosT[k]);
                pts[k] = ((P * (rr * cosT[k]) + Q * (rr * sinT[k])) * UnitsPerKm).ToVector3();
            }
            return Seg;
        }

        // Гипербола/парабола: рисуем до расстояния 30 * текущего
        double cosMax = System.Math.Max(-1.0, System.Math.Min(1.0, (p / (rm * 30.0) - 1.0) / e));
        double nuMax = System.Math.Acos(cosMax);
        for (int k = 0; k < Seg; k++)
        {
            double nu = -nuMax + 2.0 * nuMax * k / (Seg - 1);
            double c = System.Math.Cos(nu), s = System.Math.Sin(nu);
            double rr = p / (1.0 + e * c);
            pts[k] = ((P * (rr * c) + Q * (rr * s)) * UnitsPerKm).ToVector3();
        }
        return Seg;
    }

    void Update()
    {
        if (sim == null || sim.Bodies == null) return;
        if (mat == null) mat = new Material(Shader.Find("SolarSys/OrbitLine"));
        if (syncedVersion != sim.Version) { Sync(); syncedVersion = sim.Version; }

        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            showOrbits = !showOrbits;

        Vector3d camPos = sim.spaceCamera.position;
        foreach (var b in sim.Bodies)
        {
            if (!orbits.TryGetValue(b, out var o)) continue;

            CelestialBody primary = null; bool closed = true; int n = 0;
            if (showOrbits) n = ComputeOrbit(b, out primary, out closed);

            bool on = n > 0;
            if (o.root.gameObject.activeSelf != on) o.root.gameObject.SetActive(on);
            if (!on) continue;

            o.lr.loop = closed;
            o.lr.positionCount = n;
            o.lr.SetPositions(pts);
            o.root.position = ((primary.position - camPos) * UnitsPerKm).ToVector3();

            double d = (b.position - camPos).Magnitude * UnitsPerKm;
            o.lr.widthMultiplier = Mathf.Max(0.02f, (float)(d * widthFactor));

            Color c = o.baseColor;
            c.a = (b == Highlight ? 0.95f : alpha) * opacityMul;
            o.lr.startColor = o.lr.endColor = c;
        }
    }

    // Публичный: вызывается чипом «Обзор системы» из HUD
    public void Overview()
    {
        var sun = sim.Bodies[0];
        double h = 0;
        foreach (var b in sim.Bodies) h = System.Math.Max(h, (b.position - sun.position).Magnitude);
        h *= 1.15;
        sim.spaceCamera.position = sun.position + new Vector3d(0, h, -h * 0.35);
        sim.spaceCamera.LookAt(sun.position);
    }

    void OnGUI()
    {
        float s = Mathf.Max(0.5f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        showOrbits = GUI.Toggle(new Rect(10, 130, 220, 28), showOrbits, "  Орбиты (O)", GUI.skin.button);
        if (GUI.Button(new Rect(10, 165, 220, 28), "  Обзор системы")) Overview();
    }
}