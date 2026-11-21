using UnityEngine;

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

    void Update()
    {
        if (integrator == null) return;
        if (integrator.Name != integratorType.ToString()) SetIntegrator(integratorType);

        double advance = timeScale * Time.deltaTime;
        int steps = Mathf.Clamp((int)System.Math.Ceiling(advance / maxStepSeconds), 1, maxStepsPerFrame);
        double dt = advance / steps;
        for (int i = 0; i < steps; i++) integrator.Step(Bodies, dt);
        simTime += advance;

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

        if (sunLight != null)
        {
            Vector3d dir = Bodies[1].position - Bodies[0].position;
            sunLight.transform.rotation = Quaternion.LookRotation(dir.ToVector3());
        }
    }

    void OnGUI()
    {
        if (Bodies == null) return;
        double e = Gravity.TotalEnergy(Bodies);
        double drift = (e - initialEnergy) / System.Math.Abs(initialEnergy);
        GUI.Label(new Rect(10, 10, 500, 90),
            $"Integrator: {integrator.Name}\nДни: {simTime / Constants.Day:F1}\nДрейф энергии: {drift:E3}");
    }
}