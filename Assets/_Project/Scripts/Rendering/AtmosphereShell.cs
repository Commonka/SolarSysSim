using UnityEngine;

// Оболочка-сфера вокруг тела. Шейдер сам считает рассеяние, сфера нужна только как область рисования.
[DefaultExecutionOrder(100)]
public class AtmosphereShell : MonoBehaviour
{
    public SimulationManager sim;
    public string bodyName = "Earth";
    public bool show = true;              // тумблер из HUD

    [Header("Геометрия")]
    public float heightKm = 80f;          // толщина оболочки
    public float heightR = 0.010f;        // шкала высоты Рэлея, ед. (10 км)
    public float heightM = 0.0025f;       // шкала высоты Ми, ед.

    [Header("Вид")]
    public float sunIntensity = 8f;
    public float rayleighScale = 1f;
    public float mieScale = 0.4f;
    [Range(0f, 0.99f)] public float mieG = 0.76f;
    [Range(0f, 1f)] public float extinction = 0.8f;

    const double UnitsPerKm = 0.001;
    CelestialBody body;
    Transform shell;
    Material mat;

    void Build()
    {
        foreach (var b in sim.Bodies) if (b.name == bodyName) body = b;
        if (body == null) { enabled = false; return; }

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = bodyName + "Atmosphere";
        Destroy(go.GetComponent<Collider>());
        mat = new Material(Shader.Find("SolarSys/Atmosphere"));
        var r = go.GetComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        shell = go.transform;
    }

    void LateUpdate()
    {
        if (sim == null || sim.Bodies == null) return;
        if (shell == null) { Build(); if (shell == null) return; }
        if (shell.gameObject.activeSelf != show) shell.gameObject.SetActive(show);
        if (!show) return;
        if (body.visual == null) return;

        float rp = (float)(body.radius * UnitsPerKm);
        float ra = rp + heightKm * (float)UnitsPerKm;

        shell.position = body.visual.position;
        shell.localScale = Vector3.one * (ra * 2f * 1.06f);   // запас: примитив многогранный, шейдер режет по точной сфере

        mat.SetFloat("_PlanetRadius", rp);
        mat.SetFloat("_AtmoRadius", ra);
        mat.SetFloat("_HeightR", heightR);
        mat.SetFloat("_HeightM", heightM);
        mat.SetFloat("_SunIntensity", sunIntensity);
        mat.SetFloat("_RayleighScale", rayleighScale);
        mat.SetFloat("_MieScale", mieScale);
        mat.SetFloat("_MieG", mieG);
        mat.SetFloat("_Extinction", extinction);
        mat.SetFloat("_SampleScale", Mathf.Sqrt(2f * rp * heightR));
    }
}