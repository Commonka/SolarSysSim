using UnityEngine;

[DefaultExecutionOrder(100)]
public class SunGlare : MonoBehaviour
{
    public SimulationManager sim;
    public float minHalfAngleDeg = 10f;   // минимальный угловой радиус свечения
    public float sizeInDisks = 10f;       // радиус свечения в угловых радиусах диска
    [ColorUsage(false, true)] public Color color = new Color(1f, 0.95f, 0.85f);
    public float intensity = 5f;

    const double UnitsPerKm = 0.001;
    Transform quad;
    Material mat;
    Camera cam;

    void Start()
    {
        cam = sim.spaceCamera.GetComponent<Camera>();
        mat = new Material(Shader.Find("SolarSys/SunGlare"));

        var mesh = new Mesh { name = "GlareQuad" };
        mesh.vertices = new[] { new Vector3(-1, -1, 0), new Vector3(1, -1, 0), new Vector3(-1, 1, 0), new Vector3(1, 1, 0) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(2, 2, 2));

        var go = new GameObject("SunGlareQuad");
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        quad = go.transform;
    }

    void LateUpdate()
    {
        if (sim == null || sim.Bodies == null || quad == null) return;
        var sun = sim.Bodies[0];
        if (sun.visual == null) return;

        Vector3 sp = sun.visual.position;          // камера в нуле
        float d = sp.magnitude;
        float R = (float)(sun.radius * UnitsPerKm);
        if (d < R * 1.05f) { quad.gameObject.SetActive(false); return; }
        quad.gameObject.SetActive(true);

        Vector3 dir = sp / d;
        float tanA = R / Mathf.Sqrt(d * d - R * R);                 // видимый радиус диска
        float a = Mathf.Atan(tanA);
        float H = Mathf.Clamp(Mathf.Max(a * sizeInDisks, minHalfAngleDeg * Mathf.Deg2Rad), 0.01f, 1.3f);

        // Квад чуть впереди поверхности Солнца: ядро ложится поверх диска,
        // а планеты, стоящие перед Солнцем, его по-прежнему перекрывают.
        float dq = Mathf.Max(d - R * 1.02f, 0.01f);
        quad.position = dir * dq;
        quad.rotation = Quaternion.LookRotation(dir, cam.transform.up);
        quad.localScale = Vector3.one * (dq * Mathf.Tan(H));

        mat.SetFloat("_Disk", tanA / Mathf.Tan(H));
        mat.SetFloat("_Fade", 1f - 0.85f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.5f, a)));
        mat.SetFloat("_Intensity", intensity);
        mat.SetColor("_Color", color);
        mat.SetFloat("_DiskGain", 0.9f * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.01f, 0.1f, a))));
    }
}