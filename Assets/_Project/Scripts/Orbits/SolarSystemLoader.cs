using System;
using System.Collections.Generic;
using UnityEngine;

public static class SolarSystemLoader
{
    [Serializable]
    class BodyData
    {
        public string name;
        public string parent;          // если задан, вектора относительно этого тела (например, Moon -> Earth)
        public double mass, radius;    // кг, км
        public double x, y, z;         // км
        public double vx, vy, vz;      // км/с
    }

    [Serializable]
    class SystemData { public BodyData[] bodies; }

    // Данные из Horizons (эклиптика, правая система) -> оси Unity (левая система): меняем местами y и z.
    // Родитель должен идти в списке раньше потомка.
    public static CelestialBody[] FromJson(string json)
    {
        var data = JsonUtility.FromJson<SystemData>(json);
        var list = new List<CelestialBody>();
        var byName = new Dictionary<string, CelestialBody>();

        foreach (var d in data.bodies)
        {
            var pos = new Vector3d(d.x, d.z, d.y);
            var vel = new Vector3d(d.vx, d.vz, d.vy);
            if (!string.IsNullOrEmpty(d.parent) && byName.TryGetValue(d.parent, out var p))
            {
                pos += p.position;
                vel += p.velocity;
            }
            var b = new CelestialBody(d.name, d.mass, d.radius, pos, vel);
            list.Add(b);
            byName[d.name] = b;
        }
        return list.ToArray();
    }

    // Запасной вариант: круговые орбиты в одной плоскости, фазы произвольные.
    // Нужен, пока нет данных Horizons. Первым телом всегда идёт Солнце.
    public static CelestialBody[] BuildCircular()
    {
        var list = new List<CelestialBody>
        {
            new CelestialBody("Sun", Constants.SunMass, 695700, Vector3d.zero, Vector3d.zero)
        };

        // имя, масса (кг), радиус (км), большая полуось (а.е.), начальный угол (градусы)
        var planets = new (string n, double m, double r, double a, double ph)[]
        {
            ("Mercury", 3.3011e23, 2439.7, 0.387098, 20),
            ("Venus",   4.8675e24, 6051.8, 0.723332, 100),
            ("Earth",   5.9722e24, 6371.0, 1.000000, 0),
            ("Mars",    6.4171e23, 3389.5, 1.523679, 200),
            ("Jupiter", 1.8982e27, 69911,  5.2044,   290),
            ("Saturn",  5.6834e26, 58232,  9.5826,   60),
            ("Uranus",  8.6810e25, 25362,  19.2184,  150),
            ("Neptune", 1.02413e26, 24622, 30.1104,  330),
        };

        foreach (var p in planets)
        {
            double a = p.a * Constants.AU;
            double v = Math.Sqrt(Constants.G * Constants.SunMass / a);   // круговая скорость
            double th = p.ph * Math.PI / 180.0;

            var dir = new Vector3d(Math.Cos(th), 0, Math.Sin(th));
            var tangent = new Vector3d(-Math.Sin(th), 0, Math.Cos(th));
            var pos = dir * a;
            var vel = tangent * v;
            list.Add(new CelestialBody(p.n, p.m, p.r, pos, vel));

            if (p.n == "Earth")
            {
                // Луна: 384 400 км от Земли, скорость около 1,022 км/с относительно Земли
                list.Add(new CelestialBody("Moon", 7.342e22, 1737.4,
                    pos + dir * 384400.0, vel + tangent * 1.022));
            }
        }
        return list.ToArray();
    }
}