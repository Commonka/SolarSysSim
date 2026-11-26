using UnityEngine;
using UnityEngine.InputSystem;

public class BodyInspector : MonoBehaviour
{
    public SimulationManager sim;
    public CelestialBody Selected { get; private set; }

    struct Preset { public string label; public double mass, radius, temp; public Color color; public bool star; }
    static readonly Preset[] presets =
    {
        new Preset { label = "Каменная планета", mass = Constants.EarthMass, radius = 6371,   temp = 288,  color = new Color(0.45f, 0.75f, 0.55f) },
        new Preset { label = "Газовый гигант",   mass = 1.898e27,            radius = 69911,  temp = 165,  color = new Color(0.85f, 0.65f, 0.45f) },
        new Preset { label = "Астероид",         mass = 1.5e20,              radius = 250,    temp = 170,  color = new Color(0.60f, 0.58f, 0.55f) },
        new Preset { label = "Звезда",           mass = Constants.SunMass * 0.8, radius = 600000, temp = 4500, color = Color.white, star = true },
    };
    static readonly string[] presetNames = { "Каменная планета", "Газовый гигант", "Астероид", "Звезда" };

    int presetIndex, counter = 1;
    bool throwMode;
    float spawnLog = 5f, orbitFactor = 1f, throwSpeed = 15f;

    Vector2 scroll;
    Rect panel = new Rect(10, 205, 320, 600);
    Texture2D ring;
    GUIStyle centered;
    Camera cam;
    CelestialBody primary;       // кэшируется в Layout, чтобы набор контролов не менялся между Layout и Repaint
    System.Action pending;       // действия из кнопок выполняем в Update (иначе IMGUI ругается на смену layout)

    void Start()
    {
        if (sim == null) sim = GetComponent<SimulationManager>();
        ring = MakeRing();
    }

    static float Scale() => Mathf.Max(0.5f, Screen.height / 1080f);

    // ===================== Ввод =====================
    void Update()
    {
        if (sim == null || sim.Bodies == null) return;
        if (cam == null) cam = sim.spaceCamera.GetComponent<Camera>();

        if (pending != null) { var a = pending; pending = null; a(); }
        if (Selected != null && System.Array.IndexOf(sim.Bodies, Selected) < 0) Selected = null;
        OrbitRenderer.Highlight = Selected;

        var mouse = Mouse.current; var kb = Keyboard.current;
        if (mouse == null || kb == null) return;

        Vector2 mp = mouse.position.ReadValue();
        if (mouse.leftButton.wasPressedThisFrame && !OverUI(mp) && !HudController.PointerOverUI) Pick(mp);
        if (kb.nKey.wasPressedThisFrame) Spawn();
        if (kb.deleteKey.wasPressedThisFrame) DeleteSelected();
    }

    bool OverUI(Vector2 m)
    {
        float s = Scale();
        var g = new Vector2(m.x / s, (Screen.height - m.y) / s);
        float vw = Screen.width / s;
        return panel.Contains(g)
            || (g.x > vw - 290f && g.y < 820f)
            || (g.x < 235f && g.y > 125f && g.y < 200f);
    }

    float ScreenRadiusPx(CelestialBody b)
    {
        float dist = Mathf.Max(b.visual.position.magnitude, 1e-4f);
        float rUnits = (float)(b.radius * 0.001);
        float k = Screen.height * 0.5f / Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        return rUnits / dist * k;
    }

    void Pick(Vector2 mouse)
    {
        CelestialBody best = null; float bestZ = float.MaxValue;
        foreach (var b in sim.Bodies)
        {
            if (b.visual == null) continue;
            Vector3 sp = cam.WorldToScreenPoint(b.visual.position);
            if (sp.z <= 0f) continue;
            float tol = Mathf.Max(ScreenRadiusPx(b), 16f);
            if (Vector2.Distance(mouse, sp) <= tol && sp.z < bestZ) { bestZ = sp.z; best = b; }
        }
        Selected = best;   // клик в пустоту снимает выделение
    }

    // ===================== Создание / удаление =====================
    void Spawn()
    {
        var p = presets[presetIndex];
        Vector3 f = sim.spaceCamera.transform.forward;
        Vector3d fwd = new Vector3d(f.x, f.y, f.z);
        double dist = System.Math.Pow(10, spawnLog);
        Vector3d pos = sim.spaceCamera.position + fwd * dist;

        var b = new CelestialBody($"{p.label} {counter++}", p.mass, p.radius, pos, Vector3d.zero)
        {
            color = Color.Lerp(p.color, Color.HSVToRGB(Random.value, 0.5f, 0.9f), p.star ? 0f : 0.35f),
            temperature = p.temp,
            isStar = p.star,
            isUser = true,
            recolor = p.star,
            rotationPeriod = 10 * 3600.0,
            axialTilt = Random.Range(0f, 30f)
        };

        if (throwMode)
        {
            Vector3d baseV = sim.FollowIndex >= 0 ? sim.Bodies[sim.FollowIndex].velocity : Vector3d.zero;
            b.velocity = baseV + fwd * throwSpeed;
        }
        else
        {
            var pr = sim.FindPrimary(pos, p.mass, null);
            Vector3d rel = pos - pr.position;
            Vector3d tang = Vector3d.Cross(rel, new Vector3d(0, 1, 0)).Normalized;   // против часовой, как у планет
            if (tang.SqrMagnitude < 0.5) tang = new Vector3d(1, 0, 0);
            double vc = System.Math.Sqrt(Constants.G * (pr.mass + p.mass) / rel.Magnitude);
            b.velocity = pr.velocity + tang * (vc * orbitFactor);
        }

        sim.AddBody(b);
        Selected = b;
    }

    void DeleteSelected()
    {
        if (Selected == null || Selected == sim.Bodies[0]) return;
        sim.RemoveBody(Selected);
        Selected = null;
    }

    // ===================== GUI =====================
    void OnGUI()
    {
        if (sim == null || sim.Bodies == null || cam == null) return;
        if (Event.current.type == EventType.Layout && Selected != null) primary = sim.FindPrimary(Selected);

        float s = Scale();
        GUI.matrix = Matrix4x4.Scale(new Vector3(s, s, 1f));
        panel = new Rect(10, 205, 320, Screen.height / s - 215);

        DrawSelectionRing(s);

        GUILayout.BeginArea(panel, GUI.skin.box);
        scroll = GUILayout.BeginScrollView(scroll);
        DrawSpawn();
        GUILayout.Space(10);
        if (Selected != null) DrawInspector();
        else GUILayout.Label("Кликни по телу, чтобы редактировать его параметры.");
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void DrawSpawn()
    {
        GUILayout.Label("— Новое тело (N) —");
        presetIndex = GUILayout.SelectionGrid(presetIndex, presetNames, 2);
        GUILayout.Label($"Дистанция перед камерой: {System.Math.Pow(10, spawnLog):N0} км");
        spawnLog = GUILayout.HorizontalSlider(spawnLog, 3f, 9.5f);

        throwMode = GUILayout.Toggle(throwMode, throwMode ? " Режим: бросок по взгляду" : " Режим: орбита вокруг главного тела", GUI.skin.button);
        if (throwMode)
        {
            GUILayout.Label($"Скорость броска: {throwSpeed:F1} км/с");
            throwSpeed = GUILayout.HorizontalSlider(throwSpeed, 1f, 100f);
        }
        else
        {
            GUILayout.Label($"Скорость: {orbitFactor:F2} × круговой");
            orbitFactor = GUILayout.HorizontalSlider(orbitFactor, 0f, 1.6f);
        }
        if (GUILayout.Button("Создать")) pending = Spawn;
    }

    bool Slider(string label, string value, ref float v, float min, float max)
    {
        GUILayout.Label($"{label}: {value}");
        float nv = GUILayout.HorizontalSlider(v, min, max);
        if (nv != v) { v = nv; return true; }
        return false;
    }

    static string FormatMass(double m)
    {
        if (m >= 0.1 * Constants.SunMass) return $"{m / Constants.SunMass:G3} масс Солнца";
        if (m >= 0.01 * Constants.EarthMass) return $"{m / Constants.EarthMass:G3} масс Земли";
        return $"{m:E2} кг";
    }

    void DrawInspector()
    {
        var b = Selected;
        bool isRoot = b == sim.Bodies[0];
        GUILayout.Label($"=== {b.name} ===");

        // Масса
        float lm = (float)System.Math.Log10(b.mass);
        if (Slider("Масса", FormatMass(b.mass), ref lm, 15f, 33f)) { b.mass = System.Math.Pow(10, lm); sim.NotifyChanged(); }
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("×0.1")) { b.mass *= 0.1; sim.NotifyChanged(); }
        if (GUILayout.Button("×0.5")) { b.mass *= 0.5; sim.NotifyChanged(); }
        if (GUILayout.Button("×2"))   { b.mass *= 2;   sim.NotifyChanged(); }
        if (GUILayout.Button("×10"))  { b.mass *= 10;  sim.NotifyChanged(); }
        GUILayout.EndHorizontal();

        // Радиус / объём / плотность
        float lr = (float)System.Math.Log10(b.radius);
        if (Slider("Радиус", $"{b.radius:N0} км", ref lr, 0f, 6.5f))
        { b.radius = System.Math.Pow(10, lr); sim.ApplyScale(b); sim.NotifyChanged(); }
        double vol = 4.0 / 3.0 * System.Math.PI * b.radius * b.radius * b.radius;   // км³
        double rho = b.mass / (vol * 1e9);                                          // кг/м³
        GUILayout.Label($"Объём: {vol:E2} км³ ({vol / 1.0832e12:G3} объёмов Земли)\nПлотность: {rho:N0} кг/м³");

        // Температура
        float lt = (float)System.Math.Log10(System.Math.Max(b.temperature, 3.0));
        if (Slider("Температура", $"{b.temperature:N0} K", ref lt, 0.5f, 4.6f))
        {
            b.temperature = System.Math.Pow(10, lt);
            if (b.isStar) b.recolor = true;
            sim.ApplyThermal(b);
        }
        var old = GUI.color;
        GUI.color = Blackbody.FromKelvin((float)b.temperature);
        GUILayout.Box(GUIContent.none, GUILayout.Height(8));
        GUI.color = old;
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Раскалить 2500 K")) { b.temperature = 2500; if (b.isStar) b.recolor = true; sim.ApplyThermal(b); }
        if (GUILayout.Button("Охладить 100 K"))   { b.temperature = 100;  if (b.isStar) b.recolor = true; sim.ApplyThermal(b); }
        GUILayout.EndHorizontal();

        // Скорость относительно главного тела
        if (primary != null)
        {
            Vector3d rel = b.position - primary.position;
            Vector3d vr = b.velocity - primary.velocity;
            double vc = System.Math.Sqrt(Constants.G * (primary.mass + b.mass) / rel.Magnitude);
            float f = (float)(vr.Magnitude / vc);
            GUILayout.Label($"Скорость отн. {primary.name}: {vr.Magnitude:F2} км/с\n(круговая {vc:F2}, убегания {vc * 1.4142:F2})");
            if (Slider("× круговой скорости", $"{f:F2}", ref f, 0f, 2.5f) && vr.SqrMagnitude > 0)
            { b.velocity = primary.velocity + vr.Normalized * (f * vc); sim.NotifyChanged(); }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Круговая орбита"))
            {
                Vector3d h = Vector3d.Cross(rel, vr);
                if (h.SqrMagnitude < 1e-12) h = new Vector3d(0, 1, 0);
                Vector3d t = Vector3d.Cross(h.Normalized, rel).Normalized;   // направление движения
                b.velocity = primary.velocity + t * vc;
                sim.NotifyChanged();
            }
            if (GUILayout.Button("Стоп"))
            { b.velocity = primary.velocity; sim.NotifyChanged(); }
            GUILayout.EndHorizontal();
        }
        else GUILayout.Label("Центральное тело системы.");

        // Вращение
        float tilt = (float)b.axialTilt;
        if (Slider("Наклон оси", $"{tilt:F1}°", ref tilt, 0f, 180f)) b.axialTilt = tilt;
        float lp = (float)System.Math.Log10(System.Math.Max(System.Math.Abs(b.rotationPeriod) / 3600.0, 1.0));
        if (Slider("Период вращения", $"{b.rotationPeriod / 3600.0:F1} ч", ref lp, 0f, 3.5f))
            b.rotationPeriod = System.Math.Pow(10, lp) * 3600.0;

        GUILayout.Space(6);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("К телу"))    pending = () => sim.GoToBody(b);
        if (GUILayout.Button("Следовать")) pending = () => sim.Follow(b);
        GUI.enabled = !isRoot;
        if (GUILayout.Button("Удалить (Del)")) pending = DeleteSelected;
        GUI.enabled = true;
        GUILayout.EndHorizontal();
    }

    void DrawSelectionRing(float s)
    {
        if (Selected == null || Selected.visual == null || Event.current.type != EventType.Repaint) return;
        Vector3 sp = cam.WorldToScreenPoint(Selected.visual.position);
        if (sp.z <= 0f) return;

        if (centered == null) centered = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperCenter };
        float R = Mathf.Min(Mathf.Max(ScreenRadiusPx(Selected), 12f) + 10f, Screen.height);
        float gx = sp.x / s, gy = (Screen.height - sp.y) / s, rv = R / s;

        var old = GUI.color;
        GUI.color = new Color(0.3f, 0.9f, 1f, 0.9f);
        GUI.DrawTexture(new Rect(gx - rv, gy - rv, rv * 2, rv * 2), ring);
        GUI.color = old;
        GUI.Label(new Rect(gx - 80, gy + rv + 2, 160, 22), Selected.name, centered);
    }

    static Texture2D MakeRing()
    {
        const int N = 128;
        var t = new Texture2D(N, N, TextureFormat.RGBA32, false);
        var px = new Color[N * N];
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float dx = (x + 0.5f) / N * 2f - 1f, dy = (y + 0.5f) / N * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                px[y * N + x] = new Color(1, 1, 1, Mathf.Clamp01(1f - Mathf.Abs(r - 0.94f) / 0.03f));
            }
        t.SetPixels(px);
        t.filterMode = FilterMode.Bilinear;
        t.Apply();
        return t;
    }
}