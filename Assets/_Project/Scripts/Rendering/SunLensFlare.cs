using UnityEngine;

[DefaultExecutionOrder(110)]
public class SunLensFlare : MonoBehaviour
{
    public SimulationManager sim;
    public float intensity = 1.5f;

    struct Ghost
    {
        public float pos;     // 1 = на Солнце, 0 = центр кадра, <0 = за центром
        public float size;    // доля высоты экрана
        public int shape;
        public Color color;
        public float gain;
    }

    // Подберите вкус: позиции вдоль оси, размеры, формы и цвета
    static readonly Ghost[] ghosts =
    {
        new Ghost { pos =  1.00f, size = 0.55f, shape = 3, color = new Color(1.0f, 0.85f, 0.6f), gain = 0.5f },
        new Ghost { pos =  0.60f, size = 0.10f, shape = 2, color = new Color(0.4f, 0.8f, 1.0f),  gain = 0.6f },
        new Ghost { pos =  0.35f, size = 0.05f, shape = 0, color = new Color(1.0f, 0.5f, 0.3f),  gain = 0.7f },
        new Ghost { pos = -0.25f, size = 0.14f, shape = 1, color = new Color(0.6f, 1.0f, 0.7f),  gain = 0.6f },
        new Ghost { pos = -0.55f, size = 0.07f, shape = 2, color = new Color(1.0f, 0.6f, 0.9f),  gain = 0.7f },
        new Ghost { pos = -0.90f, size = 0.22f, shape = 1, color = new Color(0.5f, 0.6f, 1.0f),  gain = 0.5f },
        new Ghost { pos = -1.40f, size = 0.09f, shape = 0, color = new Color(1.0f, 0.8f, 0.4f),  gain = 0.6f },
    };

    Camera cam;
    Transform[] quads;
    Material[] mats;

    void Start()
    {
        cam = sim.spaceCamera.GetComponent<Camera>();
        var shader = Shader.Find("SolarSys/LensFlare");

        var mesh = new Mesh { name = "FlareQuad" };
        mesh.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
        mesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
        mesh.triangles = new[] { 0, 2, 1, 2, 3, 1 };
        mesh.bounds = new Bounds(Vector3.zero, Vector3.one * 1e6f); // не отсекать по границам

        quads = new Transform[ghosts.Length];
        mats = new Material[ghosts.Length];
        for (int i = 0; i < ghosts.Length; i++)
        {
            var go = new GameObject("Flare" + i);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            mats[i] = new Material(shader);
            mats[i].SetFloat("_Shape", ghosts[i].shape);
            r.sharedMaterial = mats[i];
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            quads[i] = go.transform;
        }
    }

    // Доля видимого диска Солнца (0 = закрыт телом)
    float SunVisibility(Vector3d camPos)
    {
        var bodies = sim.Bodies;
        Vector3d ts = bodies[0].position - camPos;
        double ds = ts.Magnitude;
        double sunAng = System.Math.Asin(System.Math.Min(1.0, bodies[0].radius / ds));
        Vector3d sd = ts / ds;
        float vis = 1f;
        for (int i = 1; i < bodies.Length; i++)
        {
            Vector3d tb = bodies[i].position - camPos;
            double db = tb.Magnitude;
            if (db <= bodies[i].radius) return 0f;     // камера внутри тела
            if (db > ds) continue;                      // тело дальше Солнца
            double bodyAng = System.Math.Asin(bodies[i].radius / db);
            double sep = System.Math.Acos(System.Math.Max(-1.0, System.Math.Min(1.0, Vector3d.Dot(sd, tb / db))));
            float f = Mathf.SmoothStep(0f, 1f, (float)((sep - (bodyAng - sunAng)) / (2 * sunAng)));
            vis = Mathf.Min(vis, f);
        }
        return vis;
    }

    void LateUpdate()
    {
        if (sim == null || sim.Bodies == null || quads == null) return;

        Vector3 vp = cam.WorldToViewportPoint(sim.Bodies[0].visual.position);
        float fade = 0f;
        if (vp.z > 0f)
        {
            Vector2 c = new Vector2(vp.x - 0.5f, vp.y - 0.5f);
            float edge = Mathf.Max(Mathf.Abs(c.x) * 2f, Mathf.Abs(c.y) * 2f);        // 1 = край кадра
            fade = 1f - Mathf.SmoothStep(0.85f, 1.35f, edge);
            fade *= SunVisibility(sim.spaceCamera.position);
        }

        // Блики стоят чуть дальше near-плоскости (она динамическая)
        float dist = cam.nearClipPlane * 1.5f + 0.001f;
        float worldH = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);

        for (int i = 0; i < ghosts.Length; i++)
        {
            bool on = fade > 0.001f;
            quads[i].gameObject.SetActive(on);
            if (!on) continue;

            Vector2 pos = new Vector2(0.5f, 0.5f) + (new Vector2(vp.x, vp.y) - new Vector2(0.5f, 0.5f)) * ghosts[i].pos;
            quads[i].position = cam.ViewportToWorldPoint(new Vector3(pos.x, pos.y, dist));
            quads[i].rotation = cam.transform.rotation;
            quads[i].localScale = Vector3.one * (worldH * ghosts[i].size);

            mats[i].SetColor("_Color", ghosts[i].color);
            mats[i].SetFloat("_Intensity", intensity * ghosts[i].gain * fade);
        }
    }
}
