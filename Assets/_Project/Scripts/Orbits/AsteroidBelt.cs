using System;
using UnityEngine;
using UnityEngine.InputSystem;

// Пояс астероидов: пробные частицы на кеплеровых орбитах вокруг Солнца.
// Позиции считаются в вершинном шейдере (уравнение Кеплера), CPU только задаёт параметры.
[DefaultExecutionOrder(105)]
public class AsteroidBelt : MonoBehaviour
{
    public SimulationManager sim;
    public Shader shader;                       // SolarSys/Asteroids (если пусто, ищется по имени)
    [Range(1000, 500000)] public int count = 100000;
    public int seed = 12345;
    public bool show = true;

    [Header("Распределение (а.е.)")]
    public float innerAU = 2.1f;
    public float outerAU = 3.3f;
    public float meanEcc = 0.12f, maxEcc = 0.35f;
    public float inclSigmaDeg = 7f, maxInclDeg = 30f;

    [Header("Щели Кирквуда")]
    public float gapWidthAU = 0.015f;
    [Range(0f, 1f)] public float gapDepth = 0.97f;

    [Header("Размеры")]
    public float minRadiusKm = 0.5f, maxRadiusKm = 120f;

    [Header("Вид")]
    public float sizeExaggeration = 1f;         // множитель реального размера
    public float minPixels = 1.5f;              // минимальный диаметр на экране
    [Range(0f, 1f)] public float farAlpha = 0.45f;
    public float brightness = 2f;
    public Color tint = new Color(0.55f, 0.50f, 0.45f);

    struct Ast
    {
        public Vector3 P; public float a;       // a в ед. сцены (1 ед. = 1000 км)
        public Vector3 Q; public float e;
        public float phase0, periodDays, radiusKm, seed;
    }

    const double UnitsPerKm = 0.001;
    GraphicsBuffer buffer;
    Material mat;
    Mesh quad;
    MaterialPropertyBlock mpb;

    void Build()
    {
        // Юпитер из симуляции: щели строятся по его реальной полуоси
        double aJ = 5.2038;
        foreach (var b in sim.Bodies)
            if (b.name == "Jupiter") aJ = (b.position - sim.Bodies[0].position).Magnitude / Constants.AU;
        double[] gaps =
        {
            aJ * Math.Pow(1.0 / 3.0, 2.0 / 3.0),   // 3:1
            aJ * Math.Pow(2.0 / 5.0, 2.0 / 3.0),   // 5:2
            aJ * Math.Pow(3.0 / 7.0, 2.0 / 3.0),   // 7:3
            aJ * Math.Pow(1.0 / 2.0, 2.0 / 3.0),   // 2:1
        };

        double mu = Constants.G * Constants.SunMass;              // км³/с²
        double sigE = meanEcc / 1.2533;                           // сигма Рэлея из среднего
        double sigI = inclSigmaDeg * Math.PI / 180.0;
        double maxI = maxInclDeg * Math.PI / 180.0;
        var rng = new System.Random(seed);
        var data = new Ast[count];

        for (int i = 0; i < count; i++)
        {
            double a;
            do { a = innerAU + rng.NextDouble() * (outerAU - innerAU); }
            while (rng.NextDouble() > Density(a, gaps));

            double e; do { e = sigE * Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())); } while (e > maxEcc);
            double inc; do { inc = sigI * Math.Sqrt(-2 * Math.Log(1 - rng.NextDouble())); } while (inc > maxI);
            double Om = rng.NextDouble() * 2 * Math.PI;
            double w = rng.NextDouble() * 2 * Math.PI;

            Basis(Om, inc, w, out Vector3 P, out Vector3 Q);

            double aKm = a * Constants.AU;
            double T = 2 * Math.PI * Math.Sqrt(aKm * aKm * aKm / mu) / Constants.Day;   // сутки
            double rad = minRadiusKm / Math.Sqrt(1 - rng.NextDouble());                 // степенной закон
            if (rad > maxRadiusKm) rad = maxRadiusKm;

            data[i] = new Ast
            {
                P = P, Q = Q,
                a = (float)(aKm * UnitsPerKm),
                e = (float)e,
                phase0 = (float)rng.NextDouble(),
                periodDays = (float)T,
                radiusKm = (float)rad,
                seed = (float)rng.NextDouble(),
            };
        }

        buffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, count, System.Runtime.InteropServices.Marshal.SizeOf<Ast>());
        buffer.SetData(data);

        mat = new Material(shader != null ? shader : Shader.Find("SolarSys/Asteroids"));
        mpb = new MaterialPropertyBlock();
        mpb.SetBuffer("_Asteroids", buffer);

        quad = new Mesh { name = "AsteroidQuad" };
        quad.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0) };
        quad.uv = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(-1, 1), new Vector2(1, 1) };
        quad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        quad.bounds = new Bounds(Vector3.zero, Vector3.one * 2e7f);
    }

    // Относительная плотность по большой полуоси: горб + щели у резонансов
    double Density(double a, double[] gaps)
    {
        double d = 0.35 + 0.65 * Math.Exp(-Math.Pow((a - 2.75) / 0.4, 2));
        foreach (var g in gaps) d *= 1 - gapDepth * Math.Exp(-Math.Pow((a - g) / gapWidthAU, 2));
        return d;
    }

    // Базис орбиты: P на перигелий, Q на 90° вперёд по движению. Нормаль плоскости эклиптики = Y.
    // Вращение вокруг Y так, чтобы (1,0,0) -> (cos,0,sin): тот же против часовой, что у планет.
    static void Basis(double Om, double inc, double w, out Vector3 P, out Vector3 Q)
    {
        double[] p = { 1, 0, 0 }, q = { 0, 0, 1 };
        RotY(p, w); RotY(q, w);
        RotX(p, inc); RotX(q, inc);
        RotY(p, Om); RotY(q, Om);
        P = new Vector3((float)p[0], (float)p[1], (float)p[2]);
        Q = new Vector3((float)q[0], (float)q[1], (float)q[2]);
    }
    static void RotY(double[] v, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        double x = v[0] * c - v[2] * s, z = v[0] * s + v[2] * c;
        v[0] = x; v[2] = z;
    }
    static void RotX(double[] v, double a)
    {
        double c = Math.Cos(a), s = Math.Sin(a);
        double y = v[1] * c - v[2] * s, z = v[1] * s + v[2] * c;
        v[1] = y; v[2] = z;
    }

    void Update()
    {
        if (sim == null || sim.Bodies == null) return;
        if (buffer == null) Build();

        if (Keyboard.current != null && Keyboard.current.bKey.wasPressedThisFrame) show = !show;
        if (!show) return;

        Vector3d sunRel = (sim.Bodies[0].position - sim.spaceCamera.position) * UnitsPerKm;
        mpb.SetVector("_SunRel", sunRel.ToVector3());
        mpb.SetFloat("_SimDays", (float)(sim.SimTime / Constants.Day));
        mpb.SetFloat("_SizeExag", sizeExaggeration);
        mpb.SetFloat("_MinPixels", minPixels);
        mpb.SetFloat("_FarAlpha", farAlpha);
        mpb.SetFloat("_Brightness", brightness);
        mpb.SetColor("_Tint", tint);

        var rp = new RenderParams(mat)
        {
            worldBounds = new Bounds(Vector3.zero, Vector3.one * 2e7f),
            matProps = mpb,
            shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off,
            receiveShadows = false
        };
        Graphics.RenderMeshPrimitives(rp, quad, 0, count);
    }


    void OnDestroy() { buffer?.Release(); buffer = null; }
}