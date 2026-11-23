using UnityEngine;
using UnityEngine.InputSystem;

// Выполняется ПОСЛЕ SimulationManager (у него порядок 0), чтобы линии орбит
// не отставали на кадр от планет при движении камеры.
[DefaultExecutionOrder(100)]
public class OrbitRenderer : MonoBehaviour
{
    public SimulationManager sim;
    public bool showOrbits = true;
    public float widthFactor = 0.003f;   // толщина относительно расстояния
    public float alpha = 0.35f;          // прозрачность (приглушённость)
    public Color tint = new Color(0.60f, 0.65f, 0.70f); // холодный серый оттенок

    const double UnitsPerKm = 0.001;
    // Хорда провисает на r*(1-cos(pi/N)). Для орбиты Земли при N = 8192 это ~11 км
    // (было 45 000 км при N = 128).
    const int SegPlanet = 8192;
    const int SegMoon = 720;

    LineRenderer[] lines;
    Transform[] roots;
    int[] centerIndex;
    bool built;
    Material mat;

    void Build()
    {
        var bodies = sim.Bodies;
        int n = bodies.Length;
        mat = new Material(Shader.Find("SolarSys/OrbitLine"));
        lines = new LineRenderer[n];
        roots = new Transform[n];
        centerIndex = new int[n];
        int earth = System.Array.FindIndex(bodies, b => b.name == "Earth");

        for (int i = 1; i < n; i++)
        {
            bool isMoon = bodies[i].name == "Moon" && earth >= 0;
            centerIndex[i] = isMoon ? earth : 0;
            int seg = isMoon ? SegMoon : SegPlanet;
            double r = (bodies[i].position - bodies[centerIndex[i]].position).Magnitude;

            var go = new GameObject(bodies[i].name + "Orbit");
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.alignment = LineAlignment.View;
            lr.positionCount = seg;
            lr.numCornerVertices = 4;
            lr.sharedMaterial = mat;
            lr.startWidth = lr.endWidth = 1f;

            Color c = Color.Lerp(bodies[i].color, tint, 0.55f);
            c.a = alpha;
            lr.startColor = lr.endColor = c;

            var pts = new Vector3[seg];
            for (int k = 0; k < seg; k++)
            {
                double a = k / (double)seg * 2 * System.Math.PI;
                pts[k] = new Vector3(
                    (float)(System.Math.Cos(a) * r * UnitsPerKm), 0,
                    (float)(System.Math.Sin(a) * r * UnitsPerKm));
            }
            lr.SetPositions(pts);
            lines[i] = lr; roots[i] = go.transform;
        }
        built = true;
    }

    void Overview()
    {
        var sun = sim.Bodies[0];
        double h = 0;
        foreach (var b in sim.Bodies)
            h = System.Math.Max(h, (b.position - sun.position).Magnitude);
        h *= 1.15;
        // сверху + лёгкий наклон, чтобы орбиты читались как эллипсы
        sim.spaceCamera.position = sun.position + new Vector3d(0, h, -h * 0.35);
        sim.spaceCamera.LookAt(sun.position);
    }

    void Update()
    {
        if (sim == null || sim.Bodies == null) return;
        if (!built) Build();

        if (Keyboard.current != null && Keyboard.current.oKey.wasPressedThisFrame)
            showOrbits = !showOrbits;

        Vector3d cam = sim.spaceCamera.position;
        for (int i = 1; i < sim.Bodies.Length; i++)
        {
            if (roots[i].gameObject.activeSelf != showOrbits)
                roots[i].gameObject.SetActive(showOrbits);
            if (!showOrbits) continue;

            Vector3d center = (sim.Bodies[centerIndex[i]].position - cam) * UnitsPerKm;
            roots[i].position = center.ToVector3();

            double d = (sim.Bodies[i].position - cam).Magnitude * UnitsPerKm;
            lines[i].widthMultiplier = Mathf.Max(0.02f, (float)(d * widthFactor));
        }
    }

    void OnGUI()
    {
        float s = Mathf.Max(0.5f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        showOrbits = GUI.Toggle(new Rect(10, 130, 220, 28), showOrbits,
            "  Орбиты (O)", GUI.skin.button);
        if (GUI.Button(new Rect(10, 165, 220, 28), "  Обзор системы")) Overview();
    }
}