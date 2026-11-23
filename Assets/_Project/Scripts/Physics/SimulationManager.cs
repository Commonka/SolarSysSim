using UnityEngine;
using UnityEngine.Rendering;

public class SimulationManager : MonoBehaviour
{
    public enum IntegratorType { Euler, Leapfrog, RK4 }

    [Header("Визуализация")]
    public SpaceCamera spaceCamera;
    public Light sunLight;
    public Material sunMaterial;     // Sun.mat
    public Material planetMaterial;  // обычный URP Lit, копия на каждое тело
    public Material earthMaterial;   // Earth.mat (шейдер SolarSys/Earth); если пусто, Земля как остальные
    public bool useTestGrid = true;  // ВРЕМЕННО: сетка координат на планетах без своего материала

    const double UnitsPerKm = 0.001; // 1 единица Unity = 1000 км

    [Header("Время")]
    public IntegratorType integratorType = IntegratorType.Leapfrog;
    public double timeScale = 60.0;  // симулируемых секунд за реальную секунду
    public double maxStepSeconds = 3600.0;
    public int maxStepsPerFrame = 2000;

    public CelestialBody[] Bodies { get; private set; }
    IIntegrator integrator;
    double simTime, initialEnergy;
    int followIndex = -1;
    Vector3d lastFollowPos;

    static readonly string[] speedLabels = { "Пауза", "1 мин/с", "1 час/с", "1 сут/с", "10 сут/с", "1 год/с" };
    static readonly double[] speedValues = { 0, 60, 3600, 86400, 864000, 31557600 };

    void Start()
    {
        Bodies = SolarSystemLoader.BuildCircular();
        RemoveDrift();
        initialEnergy = Gravity.TotalEnergy(Bodies);
        SetIntegrator(integratorType);
        CreateVisuals();

        // Старт: рядом с Землёй, со стороны Солнца
        int earthIdx = System.Array.FindIndex(Bodies, b => b.name == "Earth");
        var earth = Bodies[earthIdx];
        spaceCamera.position = earth.position + new Vector3d(-25000, 8000, 0);
        spaceCamera.LookAt(earth.position);
        SetFollow(earthIdx);

        var cam = spaceCamera.GetComponent<Camera>();
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1e7f;
    }

    void CreateVisuals()
    {
        for (int i = 0; i < Bodies.Length; i++)
        {
            var b = Bodies[i];
            var go = new GameObject();
            go.AddComponent<MeshFilter>().sharedMesh = GetUnitSphereMesh();
            go.AddComponent<MeshRenderer>();
            go.name = b.name + "Visual";
            Destroy(go.GetComponent<Collider>());
            go.transform.localScale = Vector3.one * (float)(2 * b.radius * UnitsPerKm);

            var r = go.GetComponent<MeshRenderer>();
            if (i == 0)
            {
                r.sharedMaterial = sunMaterial;
                r.shadowCastingMode = ShadowCastingMode.Off; // иначе Солнце затенит планеты
                r.receiveShadows = false;
            }
            else if (b.name == "Earth" && earthMaterial != null)
            {
                r.material = new Material(earthMaterial); // свой экземпляр (пригодится для карты повреждений)
            }
            else
            {
                var m = new Material(planetMaterial); // свой экземпляр на тело
                if (useTestGrid)
                {
                    m.SetColor("_BaseColor", Color.white);
                    m.SetTexture("_BaseMap", MakeGridTexture(b.color));
                }
                else
                {
                    m.SetColor("_BaseColor", b.color);
                }
                r.material = m;
            }
            b.visual = go.transform;
        }
    }
    // ===== ЕДИНСТВЕННОЕ ИЗМЕНЕНИЕ: гладкая сфера вместо примитива Unity =====
    static Mesh unitSphereMesh; // общий меш на все тела (radius 0.5 = диаметр 1, как у примитива)

    static Mesh GetUnitSphereMesh(int widthSeg = 96, int heightSeg = 48)
    {
        if (unitSphereMesh != null) return unitSphereMesh;

        int vCount = (widthSeg + 1) * (heightSeg + 1);
        var verts = new Vector3[vCount];
        var norms = new Vector3[vCount];
        var uvs   = new Vector2[vCount];
        int k = 0;
        for (int y = 0; y <= heightSeg; y++)
        {
            float v = (float)y / heightSeg;
            float pitch = v * Mathf.PI;
            for (int x = 0; x <= widthSeg; x++, k++)
            {
                float u = (float)x / widthSeg;
                float yaw = u * Mathf.PI * 2f;
                var n = new Vector3(
                    Mathf.Sin(pitch) * Mathf.Cos(yaw),
                    Mathf.Cos(pitch),
                    Mathf.Sin(pitch) * Mathf.Sin(yaw));
                verts[k] = n * 0.5f;
                norms[k] = n;
                uvs[k] = new Vector2(u, v);
            }
        }

        var tris = new int[widthSeg * heightSeg * 6];
        k = 0;
        for (int y = 0; y < heightSeg; y++)
            for (int x = 0; x < widthSeg; x++)
            {
                int i = y * (widthSeg + 1) + x;
                tris[k++] = i;             tris[k++] = i + 1;           tris[k++] = i + widthSeg + 1;
                tris[k++] = i + 1;         tris[k++] = i + widthSeg + 2; tris[k++] = i + widthSeg + 1;
            }

        // Автостраховка: если ориентация треугольников получилась внутрь — разворачиваем
        if (Vector3.Dot(Vector3.Cross(verts[tris[1]] - verts[tris[0]], verts[tris[2]] - verts[tris[0]]), verts[tris[0]]) < 0f)
            for (int t = 0; t < tris.Length; t += 3)
            { int tmp = tris[t + 1]; tris[t + 1] = tris[t + 2]; tris[t + 2] = tmp; }

        var mesh = new Mesh { name = "UnitSphereHighPoly" };
        mesh.SetVertices(verts);
        mesh.SetNormals(norms);
        mesh.SetUVs(0, uvs);
        mesh.SetIndices(tris, MeshTopology.Triangles, 0);
        mesh.RecalculateBounds();
        unitSphereMesh = mesh;
        return mesh;
    }
// ===== конец изменения =====
    // Тестовая сетка: клетки 30°, красный шов u=0, зелёный экватор,
    // белая северная шапка, тёмная южная.
    static Texture2D MakeGridTexture(Color baseCol)
    {
        const int W = 576, H = 288; // 48 px = 30°
        var tex = new Texture2D(W, H, TextureFormat.RGBA32, true);
        var px = new Color[W * H];
        for (int y = 0; y < H; y++)
        {
            for (int x = 0; x < W; x++)
            {
                Color c = baseCol;
                if (((x / 48) + (y / 48)) % 2 == 0) c *= 0.75f;
                if (x % 48 < 2 || y % 48 < 2) c = baseCol * 0.2f;
                if (Mathf.Abs(y - 144) < 3) c = Color.green;
                if (x < 4) c = Color.red;
                if (y >= H - 14) c = Color.white;
                if (y < 14) c = new Color(0.1f, 0.1f, 0.1f);
                c.a = 1f;
                px[y * W + x] = c;
            }
        }
        tex.SetPixels(px);
        tex.wrapMode = TextureWrapMode.Repeat;
        tex.Apply(true);
        return tex;
    }

    // Поворот тела: наклон оси (фиксирован в пространстве) * вращение вокруг своей оси.
    // Угол берём из simTime в double, поэтому он не накапливает ошибку при любой скорости.
    Quaternion BodyRotation(CelestialBody b)
    {
        if (b.rotationPeriod <= 0) return Quaternion.identity;
        double turns = simTime / b.rotationPeriod;
        double frac = turns - System.Math.Floor(turns);
        float angle = (float)(b.rotationPhaseDeg - frac * 360.0);
        Quaternion tilt = Quaternion.AngleAxis((float)-b.axialTilt, Vector3.forward);
        return tilt * Quaternion.AngleAxis(angle, Vector3.up);
    }

    
    void RemoveDrift()
    {
        Vector3d p = Vector3d.zero; double mTotal = 0;
        foreach (var b in Bodies) { p += b.velocity * b.mass; mTotal += b.mass; }
        Vector3d vCm = p / mTotal;
        foreach (var b in Bodies) b.velocity -= vCm;
    }

    void SetIntegrator(IntegratorType t)
    {
        integratorType = t;
        integrator = t switch
        {
            IntegratorType.Euler => new EulerIntegrator(),
            IntegratorType.RK4 => new RK4Integrator(),
            _ => new LeapfrogIntegrator()
        };
    }

    void SetFollow(int i)
    {
        followIndex = i;
        if (i >= 0) lastFollowPos = Bodies[i].position;
    }

    void GoTo(int i)
    {
        var b = Bodies[i];
        spaceCamera.position = b.position + new Vector3d(-b.radius * 4, b.radius * 1.5, 0);
        spaceCamera.LookAt(b.position);
        SetFollow(i);
    }

    void Update()
    {
        if (integrator == null) return;
        if (integrator.Name != integratorType.ToString()) SetIntegrator(integratorType);

        double advance = timeScale * Time.deltaTime;
        if (advance > 0)
        {
            int steps = Mathf.Clamp((int)System.Math.Ceiling(advance / maxStepSeconds), 1, maxStepsPerFrame);
            double dt = advance / steps;
            for (int i = 0; i < steps; i++) integrator.Step(Bodies, dt);
            simTime += advance;
        }

        if (followIndex >= 0)
        {
            Vector3d p = Bodies[followIndex].position;
            spaceCamera.position += p - lastFollowPos;
            lastFollowPos = p;
        }
        spaceCamera.Tick(Bodies);

        UpdateVisuals();
    }

    // Floating origin: камера в нуле, объекты считаются относительно неё в double
    void UpdateVisuals()
    {
        for (int i = 0; i < Bodies.Length; i++)
        {
            Vector3d rel = (Bodies[i].position - spaceCamera.position) * UnitsPerKm;
            Bodies[i].visual.position = rel.ToVector3();
            Bodies[i].visual.rotation = BodyRotation(Bodies[i]);
        }
        spaceCamera.transform.position = Vector3.zero;

        // Свет: от Солнца к ближайшему к камере телу (направление считаем в double)
        if (sunLight != null)
        {
            int nearest = 1;
            double best = double.MaxValue;
            for (int i = 1; i < Bodies.Length; i++)
            {
                double d = (Bodies[i].position - spaceCamera.position).SqrMagnitude;
                if (d < best) { best = d; nearest = i; }
            }
            Vector3d dir = (Bodies[nearest].position - Bodies[0].position).Normalized;
            if (dir.SqrMagnitude > 0) sunLight.transform.rotation = Quaternion.LookRotation(dir.ToVector3());
        }
        // Передаём позицию Солнца в шейдеры планет (для корректного освещения)
        Vector3d sunRel = (Bodies[0].position - spaceCamera.position) * UnitsPerKm;
        Shader.SetGlobalVector("_SunPositionWS", sunRel.ToVector3());
    }

    void OnGUI()
    {
        if (Bodies == null) return;

        float s = Mathf.Max(0.5f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float virtualWidth = Screen.width / s;

        double e = Gravity.TotalEnergy(Bodies);
        double drift = (e - initialEnergy) / System.Math.Abs(initialEnergy);
        string follow = followIndex >= 0 ? Bodies[followIndex].name : "свободная";
        GUI.Label(new Rect(10, 10, 500, 110),
            $"Integrator: {integrator.Name}\nДни: {simTime / Constants.Day:F1}\nДрейф энергии: {drift:E3}\nКамера: {follow}");

        GUILayout.BeginArea(new Rect(virtualWidth - 290, 10, 280, 800), GUI.skin.box);

        GUILayout.Label("Камера следует за:");
        for (int i = 0; i < Bodies.Length; i++)
        {
            GUILayout.BeginHorizontal();
            bool on = followIndex == i;
            if (GUILayout.Toggle(on, Bodies[i].name, GUI.skin.button) && !on) SetFollow(i);
            if (GUILayout.Button("К телу", GUILayout.Width(70))) GoTo(i);
            GUILayout.EndHorizontal();
        }
        bool free = followIndex < 0;
        if (GUILayout.Toggle(free, "Свободная камера", GUI.skin.button) && !free) SetFollow(-1);

        GUILayout.Space(12);
        GUILayout.Label($"Скорость времени: {timeScale / Constants.Day:F3} сут/с");
        for (int i = 0; i < speedLabels.Length; i++)
            if (GUILayout.Button(speedLabels[i])) timeScale = speedValues[i];

        GUILayout.EndArea();
    }
}