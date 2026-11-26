using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SimulationManager : MonoBehaviour
{
    public enum IntegratorType { Euler, Leapfrog, RK4 }
    Camera cam;

    [Header("Визуализация")]
    public SpaceCamera spaceCamera;
    public Light sunLight;
    public Material sunMaterial;
    public Material planetMaterial;
    public Material earthMaterial;
    public bool useTestGrid = true;

    const double UnitsPerKm = 0.001;

    [Header("Время")]
    public IntegratorType integratorType = IntegratorType.Leapfrog;
    public double timeScale = 60.0;
    public double maxStepSeconds = 3600.0;
    public int maxStepsPerFrame = 2000;

    public CelestialBody[] Bodies { get; private set; }
    public int Version { get; private set; }        // растёт при добавлении/удалении тел
    public double SimTime => simTime;
    public int FollowIndex => followIndex;
    public string IntegratorName => integrator != null ? integrator.Name : "";
    public double EnergyDrift => (Gravity.TotalEnergy(Bodies) - initialEnergy) / System.Math.Abs(initialEnergy);

    IIntegrator integrator;
    double simTime, initialEnergy;
    int followIndex = -1;
    Vector3d lastFollowPos;

    static readonly string[] speedLabels = { "Пауза", "1 мин/с", "1 час/с", "1 сут/с", "10 сут/с", "1 год/с" };
    static readonly double[] speedValues = { 0, 60, 3600, 86400, 864000, 31557600 };

    void Start()
    {
        Bodies = SolarSystemLoader.BuildCircular();
        Bodies[0].isStar = true;
        ApplyDefaultTemperatures();
        RemoveDrift();
        initialEnergy = Gravity.TotalEnergy(Bodies);
        SetIntegrator(integratorType);
        CreateVisuals();

        int earthIdx = System.Array.FindIndex(Bodies, b => b.name == "Earth");
        var earth = Bodies[earthIdx];
        spaceCamera.position = earth.position + new Vector3d(-25000, 8000, 0);
        spaceCamera.LookAt(earth.position);
        SetFollow(earthIdx);

        cam = spaceCamera.GetComponent<Camera>();
        cam.farClipPlane = 1e7f;
    }

    void ApplyDefaultTemperatures()
    {
        foreach (var b in Bodies)
            b.temperature = b.name switch
            {
                "Sun" => 5772.0, "Mercury" => 440.0, "Venus" => 737.0, "Earth" => 288.0,
                "Mars" => 210.0, "Jupiter" => 165.0, "Saturn" => 134.0, "Uranus" => 76.0,
                "Neptune" => 72.0, "Moon" => 250.0, _ => b.temperature
            };
    }

    // ===================== Визуалы =====================
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
                r.material = new Material(sunMaterial);
                r.shadowCastingMode = ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            else if (b.name == "Earth" && earthMaterial != null)
            {
                r.material = new Material(earthMaterial);
            }
            else
            {
                var m = new Material(planetMaterial);
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

    static Mesh unitSphereMesh;

    static Mesh GetUnitSphereMesh(int widthSeg = 96, int heightSeg = 48)
    {
        if (unitSphereMesh != null) return unitSphereMesh;

        int vCount = (widthSeg + 1) * (heightSeg + 1);
        var verts = new Vector3[vCount];
        var norms = new Vector3[vCount];
        var uvs = new Vector2[vCount];
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
                tris[k++] = i; tris[k++] = i + 1; tris[k++] = i + widthSeg + 1;
                tris[k++] = i + 1; tris[k++] = i + widthSeg + 2; tris[k++] = i + widthSeg + 1;
            }

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

    static Texture2D MakeGridTexture(Color baseCol)
    {
        const int W = 576, H = 288;
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

    // ===================== API для BodyInspector =====================
    public void SetFollow(int i)
    {
        followIndex = i;
        if (i >= 0) lastFollowPos = Bodies[i].position;
    }

    public void GoTo(int i)
    {
        var b = Bodies[i];
        spaceCamera.position = b.position + new Vector3d(-b.radius * 4, b.radius * 1.5, 0);
        spaceCamera.LookAt(b.position);
        SetFollow(i);
    }

    public void Follow(CelestialBody b) => SetFollow(System.Array.IndexOf(Bodies, b));
    public void GoToBody(CelestialBody b) { int i = System.Array.IndexOf(Bodies, b); if (i >= 0) GoTo(i); }

    public void NotifyChanged() { initialEnergy = Gravity.TotalEnergy(Bodies); }

    public void AddBody(CelestialBody b)
    {
        var list = new List<CelestialBody>(Bodies) { b };
        Bodies = list.ToArray();
        CreateVisual(b);
        ApplyThermal(b);
        Version++;
        NotifyChanged();
    }

    public void RemoveBody(CelestialBody b)
    {
        int idx = System.Array.IndexOf(Bodies, b);
        if (idx <= 0) return;
        var list = new List<CelestialBody>(Bodies);
        list.RemoveAt(idx);
        Bodies = list.ToArray();
        if (b.visual != null) Destroy(b.visual.gameObject);
        if (followIndex == idx) followIndex = -1;
        else if (followIndex > idx) followIndex--;
        Version++;
        NotifyChanged();
    }

    void CreateVisual(CelestialBody b)
    {
        var go = new GameObject(b.name + "Visual");
        go.AddComponent<MeshFilter>().sharedMesh = GetUnitSphereMesh();
        var r = go.AddComponent<MeshRenderer>();

        if (b.isStar)
        {
            r.sharedMaterial = new Material(sunMaterial);
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
        }
        else if (b.name == "Earth" && earthMaterial != null)
        {
            r.material = new Material(earthMaterial);
        }
        else
        {
            var m = new Material(planetMaterial);
            if (useTestGrid && !b.isUser)
            {
                m.SetColor("_BaseColor", Color.white);
                m.SetTexture("_BaseMap", MakeGridTexture(b.color));
            }
            else m.SetColor("_BaseColor", b.color);
            r.material = m;
        }
        b.visual = go.transform;
        ApplyScale(b);
    }

    public void ApplyScale(CelestialBody b)
    {
        if (b.visual != null) b.visual.localScale = Vector3.one * (float)(2 * b.radius * UnitsPerKm);
    }

    public void ApplyThermal(CelestialBody b)
    {
        var r = b.visual != null ? b.visual.GetComponent<MeshRenderer>() : null;
        if (r == null) return;
        var m = r.sharedMaterial ? r.sharedMaterial : r.material;
        if (m == null) return;

        float T = (float)b.temperature;
        if (b.isStar)
        {
            if (!b.recolor) return;
            if (m.HasProperty("_HotColor")) m.SetColor("_HotColor", Blackbody.FromKelvin(T));
            if (m.HasProperty("_MidColor")) m.SetColor("_MidColor", Blackbody.FromKelvin(T * 0.72f));
            if (m.HasProperty("_CoolColor")) m.SetColor("_CoolColor", Blackbody.FromKelvin(T * 0.45f) * 0.55f);
            return;
        }

        float glow = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(700f, 3000f, T));
        Color e = Blackbody.FromKelvin(T) * (glow * 3f);
        if (m.HasProperty("_HeatGlow")) m.SetColor("_HeatGlow", e);
        else { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", e); }
    }

    // ===================== OrbitRenderer API =====================
    public CelestialBody FindPrimary(CelestialBody b) =>
        b == Bodies[0] ? null : FindPrimary(b.position, b.mass, b);

    public CelestialBody FindPrimary(Vector3d pos, double mass, CelestialBody self)
    {
        var root = Bodies[0];
        CelestialBody best = root;
        double bestHill = double.MaxValue;
        foreach (var j in Bodies)
        {
            if (j == self || j == root || j.mass <= mass) continue;
            double dj = (j.position - root.position).Magnitude;
            double hill = dj * System.Math.Pow(j.mass / (3.0 * root.mass), 1.0 / 3.0);
            double r = (pos - j.position).Magnitude;
            if (r < hill && hill < bestHill) { best = j; bestHill = hill; }
        }
        return best;
    }

    // ===================== Столкновения =====================
    void HandleCollisions()
    {
        for (int i = 0; i < Bodies.Length; i++)
            for (int j = i + 1; j < Bodies.Length; j++)
                if ((Bodies[i].position - Bodies[j].position).Magnitude < Bodies[i].radius + Bodies[j].radius)
                { Merge(Bodies[i], Bodies[j]); return; }
    }

    void Merge(CelestialBody a, CelestialBody b)
    {
        CelestialBody big;
        if (a == Bodies[0]) big = a;
        else if (b == Bodies[0]) big = b;
        else if (a.isStar != b.isStar) big = a.isStar ? a : b;
        else big = a.mass >= b.mass ? a : b;
        var small = big == a ? b : a;

        double M = a.mass + b.mass;
        Vector3d vcm = (a.velocity * a.mass + b.velocity * b.mass) / M;
        Vector3d pcm = (a.position * a.mass + b.position * b.mass) / M;

        double vrel = (a.velocity - b.velocity).Magnitude * 1000.0;
        double mu = a.mass * b.mass / M;
        double dT = 0.5 * mu * vrel * vrel / (M * 1000.0);
        double T = (a.mass * a.temperature + b.mass * b.temperature) / M;
        big.temperature = big.isStar ? T : System.Math.Min(8000.0, T + dT);

        big.radius = System.Math.Pow(a.radius * a.radius * a.radius + b.radius * b.radius * b.radius, 1.0 / 3.0);
        big.mass = M; big.velocity = vcm; big.position = pcm;

        var followed = followIndex >= 0 ? Bodies[followIndex] : null;
        RemoveBody(small);
        if (followed == small) SetFollow(System.Array.IndexOf(Bodies, big));
        ApplyScale(big);
        ApplyThermal(big);
        NotifyChanged();
    }

    // ===================== Основной цикл =====================
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
            HandleCollisions();
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

    void UpdateVisuals()
    {
        double minSurf = double.MaxValue;
        foreach (var b in Bodies)
        {
            double d = ((b.position - spaceCamera.position).Magnitude - b.radius) * UnitsPerKm;
            if (d < minSurf) minSurf = d;
        }
        cam.nearClipPlane = (float)System.Math.Clamp(minSurf * 0.05, 0.01, 1000.0);

        for (int i = 0; i < Bodies.Length; i++)
        {
            Vector3d rel = (Bodies[i].position - spaceCamera.position) * UnitsPerKm;
            Bodies[i].visual.position = rel.ToVector3();
            Bodies[i].visual.rotation = BodyRotation(Bodies[i]);
        }
        spaceCamera.transform.position = Vector3.zero;

        CelestialBody nearest = null; double best = double.MaxValue;
        foreach (var b in Bodies)
        {
            if (b.isStar) continue;
            double d = (b.position - spaceCamera.position).SqrMagnitude;
            if (d < best) { best = d; nearest = b; }
        }

        CelestialBody star = Bodies[0]; double bestFlux = -1;
        Vector3d refPos = nearest != null ? nearest.position : Bodies[0].position;
        foreach (var s in Bodies)
        {
            if (!s.isStar) continue;
            double d2 = System.Math.Max((s.position - refPos).SqrMagnitude, 1.0);
            double T = s.temperature;
            double flux = s.radius * s.radius * T * T * T * T / d2;
            if (flux > bestFlux) { bestFlux = flux; star = s; }
        }

        if (sunLight != null && nearest != null)
        {
            Vector3d dir = (nearest.position - star.position).Normalized;
            if (dir.SqrMagnitude > 0) sunLight.transform.rotation = Quaternion.LookRotation(dir.ToVector3());
        }
        Vector3d sunRel = (star.position - spaceCamera.position) * UnitsPerKm;
        Shader.SetGlobalVector("_SunPositionWS", sunRel.ToVector3());
    }
}