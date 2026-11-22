using System;
using System.Collections.Generic;
using UnityEngine;

public static class SolarSystemLoader
{
    struct Row
    {
        public string name;
        public double mass;     // кг
        public double radius;   // км (средний)
        public double orbit;    // км (большая полуось)
        public double phaseDeg; // начальный угол на орбите
        public Color color;

        public Row(string name, double mass, double radius, double orbit, double phaseDeg, Color color)
        {
            this.name = name; this.mass = mass; this.radius = radius;
            this.orbit = orbit; this.phaseDeg = phaseDeg; this.color = color;
        }
    }

    // ВАЖНО: значения нужно сверить с NASA Planetary Fact Sheet (для записки укажи источник).
    static readonly Row[] planets =
    {
        new Row("Mercury", 3.3011e23, 2439.7,  57.909e6,   20, new Color(0.60f, 0.57f, 0.53f)),
        new Row("Venus",   4.8675e24, 6051.8, 108.209e6,  200, new Color(0.90f, 0.75f, 0.50f)),
        new Row("Earth",   5.9722e24, 6371.0, 149.598e6,    0, new Color(0.25f, 0.45f, 0.80f)),
        new Row("Mars",    6.4171e23, 3389.5, 227.956e6,  120, new Color(0.80f, 0.40f, 0.25f)),
        new Row("Jupiter", 1.8982e27, 69911,  778.479e6,  250, new Color(0.80f, 0.65f, 0.50f)),
        new Row("Saturn",  5.6834e26, 58232, 1432.041e6,  310, new Color(0.85f, 0.78f, 0.55f)),
        new Row("Uranus",  8.6810e25, 25362, 2867.043e6,   70, new Color(0.60f, 0.85f, 0.90f)),
        new Row("Neptune", 1.02413e26, 24622, 4514.953e6, 190, new Color(0.30f, 0.45f, 0.90f)),
    };

    const double MoonMass = 7.342e22;
    const double MoonRadius = 1737.4;
    const double MoonOrbit = 384400.0;

    // Порядок в массиве: Sun, Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune, Moon
    public static CelestialBody[] BuildCircular()
    {
        var list = new List<CelestialBody>();
        CelestialBody moon = null;

        list.Add(new CelestialBody("Sun", Constants.SunMass, 695700, Vector3d.zero, Vector3d.zero)
        { color = new Color(1f, 0.85f, 0.5f) });

        foreach (var p in planets)
        {
            double ang = p.phaseDeg * Math.PI / 180.0;
            Vector3d dir = new Vector3d(Math.Cos(ang), 0, Math.Sin(ang));
            Vector3d tang = new Vector3d(-Math.Sin(ang), 0, Math.Cos(ang)); // против часовой

            bool hasMoon = p.name == "Earth";
            double mSys = p.mass + (hasMoon ? MoonMass : 0);
            double v = Math.Sqrt(Constants.G * (Constants.SunMass + mSys) / p.orbit);

            Vector3d pos = dir * p.orbit;
            Vector3d vel = tang * v;

            if (!hasMoon)
            {
                list.Add(new CelestialBody(p.name, p.mass, p.radius, pos, vel) { color = p.color });
                continue;
            }

            // Пара Земля-Луна вокруг общего центра масс
            double fE = p.mass / mSys;
            double fM = MoonMass / mSys;
            double vRel = Math.Sqrt(Constants.G * mSys / MoonOrbit);

            list.Add(new CelestialBody(p.name, p.mass, p.radius,
                pos - dir * (MoonOrbit * fM), vel - tang * (vRel * fM)) { color = p.color });

            moon = new CelestialBody("Moon", MoonMass, MoonRadius,
                pos + dir * (MoonOrbit * fE), vel + tang * (vRel * fE))
            { color = new Color(0.7f, 0.7f, 0.7f) };
        }

        list.Add(moon);
        return list.ToArray();
    }
}