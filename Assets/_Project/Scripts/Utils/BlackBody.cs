using UnityEngine;

public static class Blackbody
{
    // Цвет чёрного тела по температуре (аппроксимация Tanner Helland)
    public static Color FromKelvin(float k)
    {
        float t = Mathf.Clamp(k, 1000f, 40000f) / 100f;
        float r, g, b;
        if (t <= 66f)
        {
            r = 255f;
            g = 99.4708025861f * Mathf.Log(t) - 161.1195681661f;
            b = t <= 19f ? 0f : 138.5177312231f * Mathf.Log(t - 10f) - 305.0447927307f;
        }
        else
        {
            r = 329.698727446f * Mathf.Pow(t - 60f, -0.1332047592f);
            g = 288.1221695283f * Mathf.Pow(t - 60f, -0.0755148492f);
            b = 255f;
        }
        return new Color(Mathf.Clamp01(r / 255f), Mathf.Clamp01(g / 255f), Mathf.Clamp01(b / 255f), 1f);
    }

    static readonly Color sunRef = FromKelvin(5772f);

    // Оттенок относительно Солнца (для тонировки бликов: на 5772 K = белый)
    public static Color Tint(float k)
    {
        var c = FromKelvin(k);
        return new Color(c.r / sunRef.r, c.g / sunRef.g, c.b / sunRef.b, 1f);
    }
}