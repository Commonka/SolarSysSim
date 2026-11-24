using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

// Ползунок без зависимости от темы UI Toolkit
public class HSlider : VisualElement
{
    public float Value;
    public bool Dragging;
    public int Steps;                    // >0: дискретные положения
    public Action<float> Changed;
    readonly VisualElement fill, knob;

    public HSlider()
    {
        style.height = 22; style.flexGrow = 1; style.justifyContent = Justify.Center;
        var track = new VisualElement { pickingMode = PickingMode.Ignore };
        track.style.height = 3;
        track.style.backgroundColor = new Color(1, 1, 1, 0.18f);
        fill = new VisualElement { pickingMode = PickingMode.Ignore };
        fill.style.position = Position.Absolute;
        fill.style.left = 0; fill.style.top = 0; fill.style.bottom = 0;
        fill.style.backgroundColor = new Color(0.55f, 0.8f, 1f);
        knob = new VisualElement { pickingMode = PickingMode.Ignore };
        knob.style.position = Position.Absolute;
        knob.style.top = -5; knob.style.width = 13; knob.style.height = 13;
        knob.style.borderTopLeftRadius = knob.style.borderTopRightRadius =
            knob.style.borderBottomLeftRadius = knob.style.borderBottomRightRadius = 7;
        knob.style.backgroundColor = Color.white;
        knob.style.translate = new Translate(Length.Percent(-50), 0);
        track.Add(fill); track.Add(knob); Add(track);

        RegisterCallback<PointerDownEvent>(e => { Dragging = true; this.CapturePointer(e.pointerId); Apply(e.localPosition.x); e.StopPropagation(); });
        RegisterCallback<PointerMoveEvent>(e => { if (Dragging) Apply(e.localPosition.x); });
        RegisterCallback<PointerUpEvent>(e => { if (Dragging) { Dragging = false; this.ReleasePointer(e.pointerId); } });
        RegisterCallback<PointerCaptureOutEvent>(e => Dragging = false);
    }

    
    void Apply(float x) => Set(Mathf.Clamp01(x / Mathf.Max(resolvedStyle.width, 1f)), true);

    public void Set(float v, bool notify)
    {
        if (Steps > 0) v = Mathf.Round(v * Steps) / Steps;
        Value = v;
        fill.style.width = Length.Percent(v * 100f);
        knob.style.left = Length.Percent(v * 100f);
        if (notify) Changed?.Invoke(v);
    }
}

public class HudController : MonoBehaviour
{
    public static bool PointerOverUI;

    public SimulationManager sim;
    public OrbitRenderer orbits;
    public AsteroidBelt belt;
    public AtmosphereShell atmosphere;

    static readonly Color Bg = new Color(0.03f, 0.04f, 0.06f, 0.88f);
    static readonly Color Line = new Color(1, 1, 1, 0.12f);
    static readonly Color Txt = new Color(1, 1, 1, 0.88f);
    static readonly Color Dim = new Color(1, 1, 1, 0.5f);
    static readonly Color Accent = new Color(0.55f, 0.8f, 1f);
    static readonly DateTime Epoch = new DateTime(2000, 1, 1, 12, 0, 0);

    UIDocument doc;
    VisualElement root, bar, panel;
    Label timeLabel, subLabel, speedLabel, pauseLabel, infoLabel, driftLabel;
    HSlider speedSlider;
    readonly Dictionary<string, VisualElement> pages = new();
    readonly List<Action> syncs = new();
    VisualElement earthGroup, bodyList;
    string openTab;
    bool barOver, panelOver, paused, objectsBuilt;
    double speed = 60;
    int sel;
    float infoTimer;

    void Awake()
    {
        if (!sim) sim = FindFirstObjectByType<SimulationManager>();
        if (!orbits) orbits = FindFirstObjectByType<OrbitRenderer>();
        if (!belt) belt = FindFirstObjectByType<AsteroidBelt>();
        if (!atmosphere) atmosphere = FindFirstObjectByType<AtmosphereShell>();

        var ps = ScriptableObject.CreateInstance<PanelSettings>();
        ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        ps.referenceResolution = new Vector2Int(1920, 1080);
        ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        ps.match = 1f;
        doc = gameObject.AddComponent<UIDocument>();
        doc.panelSettings = ps;
    }

    void Start() => Build();

    // ---------- helpers ----------
    Label L(string text, float size, Color c)
    {
        var l = new Label(text) { pickingMode = PickingMode.Ignore };
        l.style.fontSize = size; l.style.color = c;
        return l;
    }

    Label Chip(VisualElement parent, string text, Action<int> click, Func<bool> active = null)
    {
        var l = new Label(text);
        l.style.paddingLeft = l.style.paddingRight = 14;
        l.style.paddingTop = l.style.paddingBottom = 7;
        l.style.marginRight = 6; l.style.marginBottom = 4;
        l.style.borderBottomWidth = 2; l.style.borderBottomColor = Color.clear;
        l.style.unityTextAlign = TextAnchor.MiddleCenter;
        l.RegisterCallback<ClickEvent>(e => click(e.clickCount));
        if (active != null)
            syncs.Add(() => { bool a = active(); l.style.color = a ? Accent : Txt; l.style.borderBottomColor = a ? Accent : Color.clear; });
        parent.Add(l);
        return l;
    }

    HSlider AddSlider(VisualElement parent, string title, float lo, float hi, Func<float> get,
                      Action<float> set, Func<float, string> fmt, bool log = false, int steps = 0)
    {
        float Map(float t) => log ? lo * Mathf.Pow(hi / lo, t) : Mathf.Lerp(lo, hi, t);
        float Inv(float v) => log ? Mathf.Log(Mathf.Clamp(v, lo, hi) / lo) / Mathf.Log(hi / lo)
                                  : Mathf.InverseLerp(lo, hi, v);

        var cell = new VisualElement();
        cell.style.width = 300; cell.style.marginRight = 28; cell.style.marginBottom = 10;
        var head = new VisualElement(); head.style.flexDirection = FlexDirection.Row;
        head.style.justifyContent = Justify.SpaceBetween;
        var name = L(title, 13, Dim);
        var val = L("", 13, Txt);
        head.Add(name); head.Add(val);
        var s = new HSlider { Steps = steps };
        s.Changed = t => { float v = Map(t); set(v); val.text = fmt(v); };
        cell.Add(head); cell.Add(s); parent.Add(cell);

        syncs.Add(() =>
        {
            if (s.Dragging) return;
            float v = get();
            s.Set(Inv(v), false);
            val.text = fmt(v);
        });
        return s;
    }

    static string FmtSpeed(double s)
    {
        if (s < 60) return $"{s:F0} с/с";
        if (s < 3600) return $"{s / 60:F1} мин/с";
        if (s < 86400) return $"{s / 3600:F1} ч/с";
        if (s < 31557600) return $"{s / 86400:F1} сут/с";
        return $"{s / 31557600:F2} лет/с";
    }

    void ApplySpeed() { if (sim) sim.timeScale = paused ? 0 : speed; }

    // ---------- построение ----------
    void Build()
    {
        root = doc.rootVisualElement;
        root.pickingMode = PickingMode.Ignore;
        root.style.color = Txt; root.style.fontSize = 15;

        // нижняя панель
        bar = new VisualElement();
        bar.style.position = Position.Absolute;
        bar.style.left = 0; bar.style.right = 0; bar.style.bottom = 0; bar.style.height = 58;
        bar.style.flexDirection = FlexDirection.Row; bar.style.alignItems = Align.Center;
        bar.style.backgroundColor = Bg;
        bar.style.borderTopWidth = 1; bar.style.borderTopColor = Line;
        bar.style.paddingLeft = bar.style.paddingRight = 18;
        bar.RegisterCallback<PointerEnterEvent>(e => barOver = true);
        bar.RegisterCallback<PointerLeaveEvent>(e => barOver = false);

        pauseLabel = Chip(bar, "Пауза", _ => { paused = !paused; ApplySpeed(); }, () => paused);
        pauseLabel.style.width = 76;

        var tcol = new VisualElement(); tcol.style.width = 190; tcol.style.marginLeft = 10;
        timeLabel = L("", 17, Txt); subLabel = L("", 12, Dim);
        tcol.Add(timeLabel); tcol.Add(subLabel); bar.Add(tcol);

        speedLabel = L("", 14, Txt); speedLabel.style.width = 110;
        bar.Add(speedLabel);
        speedSlider = new HSlider(); speedSlider.style.flexGrow = 0; speedSlider.style.width = 240;
        speedSlider.Changed = t =>
        {
            speed = Math.Pow(10, t * 8.0);           // 1 с/с .. ~3 года/с
            speedLabel.text = FmtSpeed(speed);
            ApplySpeed();
        };
        bar.Add(speedSlider);
        speed = sim.timeScale > 0 ? sim.timeScale : 60;
        speedSlider.Set(Mathf.Clamp01(Mathf.Log10((float)speed) / 8f), false);
        speedLabel.text = FmtSpeed(speed);

        var sp1 = new VisualElement(); sp1.style.flexGrow = 1; bar.Add(sp1);
        foreach (var t in new[] { ("view", "вид"), ("objects", "тела"), ("sim", "расчёт") })
        {
            string key = t.Item1;
            var chip = Chip(bar, t.Item2, _ => { openTab = openTab == key ? null : key; ShowPage(); }, () => openTab == key);
            chip.style.marginBottom = 0; chip.style.paddingLeft = chip.style.paddingRight = 22;
        }
        var sp2 = new VisualElement(); sp2.style.flexGrow = 1; bar.Add(sp2);
        bar.Add(L("H — скрыть · P — пауза", 12, Dim));

        // выезжающая панель
        panel = new VisualElement();
        panel.style.position = Position.Absolute;
        panel.style.left = 0; panel.style.right = 0; panel.style.bottom = 58;
        panel.style.backgroundColor = Bg;
        panel.style.borderTopWidth = 1; panel.style.borderTopColor = Line;
        panel.style.paddingLeft = panel.style.paddingRight = 24;
        panel.style.paddingTop = 16; panel.style.paddingBottom = 8;
        panel.style.display = DisplayStyle.None;
        panel.RegisterCallback<PointerEnterEvent>(e => panelOver = true);
        panel.RegisterCallback<PointerLeaveEvent>(e => panelOver = false);

        BuildViewPage(); BuildObjectsPage(); BuildSimPage();
        root.Add(panel); root.Add(bar);
        ShowPage();
    }

    VisualElement NewPage(string key)
    {
        var p = new VisualElement();
        p.style.flexDirection = FlexDirection.Row; p.style.flexWrap = Wrap.Wrap;
        p.style.display = DisplayStyle.None;
        panel.Add(p); pages[key] = p;
        return p;
    }

    void ShowPage()
    {
        foreach (var kv in pages) kv.Value.style.display = kv.Key == openTab ? DisplayStyle.Flex : DisplayStyle.None;
        panel.style.display = openTab == null ? DisplayStyle.None : DisplayStyle.Flex;
        if (openTab == null) panelOver = false;
    }

    void BuildViewPage()
    {
        var p = NewPage("view");
        var chips = new VisualElement(); chips.style.flexDirection = FlexDirection.Row;
        chips.style.width = Length.Percent(100); chips.style.marginBottom = 8;
        Chip(chips, "Орбиты", _ => orbits.showOrbits = !orbits.showOrbits, () => orbits.showOrbits);
        Chip(chips, "Пояс астероидов", _ => belt.show = !belt.show, () => belt.show);
        Chip(chips, "Обзор системы", _ => orbits.Overview());
        Chip(chips, "Скриншот", _ => StartCoroutine(Photo()));
        p.Add(chips);

        AddSlider(p, "Прозрачность орбит", 0.1f, 3f, () => orbits.opacityMul, v => orbits.opacityMul = v, v => $"×{v:F1}");
        AddSlider(p, "Яркость пояса", 0.2f, 6f, () => belt.brightness, v => belt.brightness = v, v => $"{v:F1}");
        AddSlider(p, "Размер астероидов", 1f, 500f, () => belt.sizeExaggeration, v => belt.sizeExaggeration = v, v => $"×{v:F0}", true);
        AddSlider(p, "Мин. размер точки", 1f, 6f, () => belt.minPixels, v => belt.minPixels = v, v => $"{v:F1} px");
        AddSlider(p, "Поле зрения", 20f, 100f, () => sim.spaceCamera.GetComponent<Camera>().fieldOfView,
                  v => sim.spaceCamera.GetComponent<Camera>().fieldOfView = v, v => $"{v:F0}°");
        AddSlider(p, "Скорость камеры", 0.001f, 1000f, () => (float)sim.spaceCamera.speedMultiplier,
                  v => sim.spaceCamera.speedMultiplier = v, v => $"×{v:G3}", true);
    }

    void BuildObjectsPage()
    {
        var p = NewPage("objects");
        bodyList = new VisualElement();
        bodyList.style.flexDirection = FlexDirection.Row; bodyList.style.flexWrap = Wrap.Wrap;
        bodyList.style.width = Length.Percent(100); bodyList.style.marginBottom = 8;
        p.Add(bodyList);

        var card = new VisualElement(); card.style.width = 340; card.style.marginRight = 28;
        infoLabel = L("", 13, Txt); infoLabel.style.whiteSpace = WhiteSpace.Normal;
        card.Add(infoLabel); p.Add(card);

        CelestialBody B() => sim.Bodies[sel];
        AddSlider(p, "Наклон оси", 0f, 180f, () => (float)B().axialTilt, v => B().axialTilt = v, v => $"{v:F1}°");
        AddSlider(p, "Период вращения", 0.5f, 8000f, () => (float)(B().rotationPeriod / 3600.0),
                  v => B().rotationPeriod = v * 3600.0, v => $"{v:F1} ч", true);

        earthGroup = new VisualElement();
        earthGroup.style.flexDirection = FlexDirection.Row; earthGroup.style.flexWrap = Wrap.Wrap;
        earthGroup.style.width = Length.Percent(100);
        p.Add(earthGroup);
        Material EM() => sim.Bodies[sel].visual.GetComponent<MeshRenderer>().material;
        AddSlider(earthGroup, "Рельеф", 0f, 3f, () => EM().GetFloat("_BumpStrength"), v => EM().SetFloat("_BumpStrength", v), v => $"{v:F2}");
        AddSlider(earthGroup, "Облака", 0f, 2f, () => EM().GetFloat("_CloudBrightness"), v => EM().SetFloat("_CloudBrightness", v), v => $"{v:F2}");
        if (atmosphere)
            AddSlider(earthGroup, "Атмосфера", 0f, 20f, () => atmosphere.sunIntensity, v => atmosphere.sunIntensity = v, v => $"{v:F1}");
    }

    void BuildSimPage()
    {
        var p = NewPage("sim");
        AddSlider(p, "Интегратор", 0f, 2f, () => (int)sim.integratorType,
                  v => sim.integratorType = (SimulationManager.IntegratorType)Mathf.RoundToInt(v),
                  v => ((SimulationManager.IntegratorType)Mathf.RoundToInt(v)).ToString(), false, 2);
        AddSlider(p, "Макс. шаг", 60f, 7200f, () => (float)sim.maxStepSeconds, v => sim.maxStepSeconds = v,
                  v => $"{v:F0} с", true);
        driftLabel = L("", 13, Txt);
        driftLabel.style.marginTop = 6; driftLabel.style.width = Length.Percent(100);
        p.Add(driftLabel);
    }

    void BuildObjects()
    {
        for (int i = 0; i < sim.Bodies.Length; i++)
        {
            int idx = i;
            Chip(bodyList, sim.Bodies[i].name, clicks =>
            {
                sel = idx; sim.SetFollow(idx);
                if (clicks >= 2) sim.GoTo(idx);
            }, () => sel == idx);
        }
        sel = Mathf.Max(sim.FollowIndex, 0);
        objectsBuilt = true;
    }

    IEnumerator Photo()
    {
        root.style.display = DisplayStyle.None;
        yield return null; yield return null;
        string path = System.IO.Path.Combine(Application.persistentDataPath, $"SolarSys_{DateTime.Now:yyyyMMdd_HHmmss}.png");
        ScreenCapture.CaptureScreenshot(path);
        yield return null; yield return null;
        root.style.display = DisplayStyle.Flex;
        Debug.Log("Скриншот: " + path);
    }

    // ---------- обновление ----------
    void Update()
    {
        PointerOverUI = barOver || panelOver;
        if (root == null || sim == null || sim.Bodies == null) return;
        if (!objectsBuilt) BuildObjects();

        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.hKey.wasPressedThisFrame)
                root.style.display = root.style.display == DisplayStyle.None ? DisplayStyle.Flex : DisplayStyle.None;
            if (kb.pKey.wasPressedThisFrame) { paused = !paused; ApplySpeed(); }
        }

        double days = sim.SimTime / Constants.Day;
        timeLabel.text = Epoch.AddDays(Math.Min(days, 2.9e6)).ToString("yyyy-MM-dd HH:mm");
        subLabel.text = $"T+ {days:F1} сут";
        pauseLabel.text = paused ? "Пуск" : "Пауза";

        foreach (var s in syncs)
        {
            if (openTab == null && s.Target == this && false) continue;
            s();
        }

        infoTimer -= Time.unscaledDeltaTime;
        if (infoTimer <= 0f)
        {
            infoTimer = 0.25f;
            var b = sim.Bodies[sel]; var sun = sim.Bodies[0];
            double dist = (b.position - sun.position).Magnitude;
            double v = (b.velocity - sun.velocity).Magnitude;
            infoLabel.text = $"{b.name}\nРадиус: {b.radius:N0} км\nМасса: {b.mass:E3} кг\n" +
                             $"До Солнца: {dist / Constants.AU:F3} а.е.\nСкорость: {v:F2} км/с";
            earthGroup.style.display = b.name == "Earth" ? DisplayStyle.Flex : DisplayStyle.None;
            driftLabel.text = $"Дрейф энергии: {sim.EnergyDrift:E2}";
        }
    }
}