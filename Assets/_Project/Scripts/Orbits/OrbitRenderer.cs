using UnityEngine;
using UnityEngine.InputSystem;

// Выполняется ПОСЛЕ SimulationManager (у него порядок 0), чтобы линии орбит
// не отставали на кадр от планет при движении камеры.
[DefaultExecutionOrder(100)]
public class OrbitRenderer : MonoBehaviour
{
    public SimulationManager sim;
    public bool showOrbits = false;
    public float widthFactor = 0.0015f;   // тоньше
    public float alpha = 0.18f;           // менее заметные
    public float opacityMul = 1f;
    public int dashesPlanet = 220;        // штрихов на орбиту
    public int dashesMoon = 80;    
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
            lr.numCapVertices = 4;
            lr.textureMode = LineTextureMode.Stretch;
            var pb = new MaterialPropertyBlock();
            pb.SetFloat("_Dashes", isMoon ? dashesMoon : dashesPlanet);
            lr.SetPropertyBlock(pb);

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

    public void Overview()
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
        mat.SetColor("_Color", new Color(1, 1, 1, opacityMul));

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


}