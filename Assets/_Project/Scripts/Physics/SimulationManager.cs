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
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere); // диаметр 1
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
            else
            {
                var m = new Material(planetMaterial); // свой экземпляр на тело
                m.SetColor("_BaseColor", b.color);
                r.material = m;
            }
            b.visual = go.transform;
        }
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