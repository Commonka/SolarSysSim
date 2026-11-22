using UnityEngine;
using UnityEngine.InputSystem;

public class SimulationManager : MonoBehaviour
{
    public enum IntegratorType { Euler, Leapfrog, RK4 }

    [Header("Визуализация")]
    public Transform[] visuals;            // по порядку: Sun, Earth
    public SpaceCamera spaceCamera;
    public Light sunLight;
    public int followIndex = 1;            // камера едет вместе с этим телом (-1 = нет)

    const double UnitsPerKm = 0.001;       // 1 единица Unity = 1000 км

    [Header("Время")]
    public IntegratorType integratorType = IntegratorType.Leapfrog;
    public double timeScale = 60.0;        // симулируемых секунд за реальную секунду
    public double maxStepSeconds = 3600.0;
    public int maxStepsPerFrame = 2000;

    public CelestialBody[] Bodies { get; private set; }
    IIntegrator integrator;
    double simTime, initialEnergy;
    Vector3d lastFollowPos;

    // Кнопки скорости времени: подпись и значение (симулируемых секунд за реальную)
    static readonly string[] speedLabels = { "Пауза", "1 мин/с", "1 час/с", "1 сут/с", "10 сут/с" };
    static readonly double[] speedValues = { 0, 60, 3600, 86400, 864000 };

    void Start()
    {
        Bodies = new[]
        {
            new CelestialBody("Sun", Constants.SunMass, 695700, Vector3d.zero, Vector3d.zero),
            new CelestialBody("Earth", Constants.EarthMass, 6371,
                new Vector3d(Constants.AU, 0, 0), new Vector3d(0, 0, 29.78))
        };
        RemoveDrift();
        initialEnergy = Gravity.TotalEnergy(Bodies);
        SetIntegrator(integratorType);

        // Реальные размеры (меш сферы Unity имеет диаметр 1)
        for (int i = 0; i < visuals.Length && i < Bodies.Length; i++)
            visuals[i].localScale = Vector3.one * (float)(2 * Bodies[i].radius * UnitsPerKm);

        // Стартовая точка: рядом с Землёй, со стороны Солнца
        var earth = Bodies[1];
        spaceCamera.position = earth.position + new Vector3d(-25000, 8000, 0);
        spaceCamera.LookAt(earth.position);
        if (followIndex >= 0) lastFollowPos = Bodies[followIndex].position;

        var cam = spaceCamera.GetComponent<Camera>();
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1e7f;
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

    // Камера начинает ехать вместе с телом i (-1 = свободный полёт)
    void SetFollow(int i)
    {
        followIndex = i;
        if (i >= 0) lastFollowPos = Bodies[i].position;
    }

    // Перенести камеру к телу i, повернуть на него и ехать вместе с ним
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

        // Горячие клавиши: 1 = Солнце, 2 = Земля, 0 = свободная камера
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.digit1Key.wasPressedThisFrame) SetFollow(0);
            if (kb.digit2Key.wasPressedThisFrame) SetFollow(1);
            if (kb.digit0Key.wasPressedThisFrame) SetFollow(-1);
        }

        double advance = timeScale * Time.deltaTime;
        if (advance > 0)
        {
            int steps = Mathf.Clamp((int)System.Math.Ceiling(advance / maxStepSeconds), 1, maxStepsPerFrame);
            double dt = advance / steps;
            for (int i = 0; i < steps; i++) integrator.Step(Bodies, dt);
            simTime += advance;
        }

        // Камера следует за выбранным телом
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
        for (int i = 0; i < visuals.Length && i < Bodies.Length; i++)
        {
            Vector3d rel = (Bodies[i].position - spaceCamera.position) * UnitsPerKm;
            visuals[i].position = rel.ToVector3();
        }
        spaceCamera.transform.position = Vector3.zero;

        // Свет идёт от Солнца к Земле (направление берём из позиций самих сфер)
        if (sunLight != null && visuals.Length > 1)
        {
            Vector3 sunToEarth = visuals[1].position - visuals[0].position;
            if (sunToEarth.sqrMagnitude > 0f)
                sunLight.transform.rotation = Quaternion.LookRotation(sunToEarth);
        }
    }

    void OnGUI()
    {
        if (Bodies == null) return;

        // Масштабируем интерфейс под высоту экрана, чтобы было читаемо при любом разрешении
        float s = Mathf.Max(0.5f, Screen.height / 1080f);
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        float virtualWidth = Screen.width / s;

        double e = Gravity.TotalEnergy(Bodies);
        double drift = (e - initialEnergy) / System.Math.Abs(initialEnergy);
        GUI.Label(new Rect(10, 10, 500, 90),
            $"Integrator: {integrator.Name}\nДни: {simTime / Constants.Day:F1}\nДрейф энергии: {drift:E3}");

        GUILayout.BeginArea(new Rect(virtualWidth - 290, 10, 280, 520), GUI.skin.box);

        GUILayout.Label("Камера следует за:");
        for (int i = 0; i < Bodies.Length; i++)
        {
            GUILayout.BeginHorizontal();
            bool on = followIndex == i;
            if (GUILayout.Toggle(on, Bodies[i].name, GUI.skin.button) && !on) SetFollow(i);
            if (GUILayout.Button("К телу", GUILayout.Width(80))) GoTo(i);
            GUILayout.EndHorizontal();
        }
        bool free = followIndex < 0;
        if (GUILayout.Toggle(free, "Свободная камера", GUI.skin.button) && !free) SetFollow(-1);

        GUILayout.Space(12);
        GUILayout.Label($"Скорость времени: {timeScale / Constants.Day:F3} сут/с");
        for (int i = 0; i < speedLabels.Length; i++)
        {
            if (GUILayout.Button(speedLabels[i])) timeScale = speedValues[i];
        }

        GUILayout.EndArea();
    }
}