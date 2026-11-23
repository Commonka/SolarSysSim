using System;
using System.Collections.Generic;
using UnityEngine;

public static class SolarSystemLoader
{
    struct Row
    {
        public string name;
        public double mass;      // кг
        public double radius;    // км (средний)
        public double orbit;     // км (большая полуось)
        public double phaseDeg;  // начальный угол на орбите
        public Color color;
        public double rotHours;  // сидерический период вращения, часы
        public double tiltDeg;   // наклон оси к орбите, градусы

        public Row(string name, double mass, double radius, double orbit, double phaseDeg,
                   Color color, double rotHours, double tiltDeg)
        {
            this.name = name; this.mass = mass; this.radius = radius;
            this.orbit = orbit; this.phaseDeg = phaseDeg; this.color = color;
            this.rotHours = rotHours; this.tiltDeg = tiltDeg;
        }
    }

    // ВАЖНО: значения нужно сверить с NASA Planetary Fact Sheet (для записки укажи источник).
    // Венера (177.36°) и Уран (97.77°): наклон больше 90° даёт обратное вращение сам по себе.
    static readonly Row[] planets =
    {
        new Row("Mercury", 3.3011e23, 2439.7,  57.909e6,   20, new Color(0.60f, 0.57f, 0.53f), 1407.6,  0.034),
        new Row("Venus",   4.8675e24, 6051.8, 108.209e6,  200, new Color(0.90f, 0.75f, 0.50f), 5832.5,  177.36),
        new Row("Earth",   5.9722e24, 6371.0, 149.598e6,    0, new Color(0.25f, 0.45f, 0.80f), 23.9345, 23.44),
        new Row("Mars",    6.4171e23, 3389.5, 227.956e6,  120, new Color(0.80f, 0.40f, 0.25f), 24.6229, 25.19),
        new Row("Jupiter", 1.8982e27, 69911,  778.479e6,  250, new Color(0.80f, 0.65f, 0.50f), 9.925,   3.13),
        new Row("Saturn",  5.6834e26, 58232, 1432.041e6,  310, new Color(0.85f, 0.78f, 0.55f), 10.656,  26.73),
        new Row("Uranus",  8.6810e25, 25362, 2867.043e6,   70, new Color(0.60f, 0.85f, 0.90f), 17.24,   97.77),
        new Row("Neptune", 1.02413e26, 24622, 4514.953e6, 190, new Color(0.30f, 0.45f, 0.90f), 16.11,   28.32),
    };

    const double MoonMass = 7.342e22;
    const double MoonRadius = 1737.4;
    const double MoonOrbit = 384400.0;
    const double MoonRotHours = 27.321661 * 24.0; // синхронное вращение: период = орбитальный
    const double MoonTiltDeg = 1.54;              // к эклиптике

    // Порядок в массиве: Sun, Mercury, Venus, Earth, Mars, Jupiter, Saturn, Uranus, Neptune, Moon
    public static CelestialBody[] BuildCircular()
    {
        var list = new List<CelestialBody>();
        CelestialBody moon = null;

        list.Add(new CelestialBody("Sun", Constants.SunMass, 695700, Vector3d.zero, Vector3d.zero)
        {
            color = new Color(1f, 0.85f, 0.5f),
            rotationPeriod = 25.05 * 24.0 * 3600.0,
            axialTilt = 7.25
        });

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
                list.Add(new CelestialBody(p.name, p.mass, p.radius, pos, vel)
                {
                    color = p.color,
                    rotationPeriod = p.rotHours * 3600.0,
                    axialTilt = p.tiltDeg
                });
                continue;
            }

            // Пара Земля-Луна вокруг общего центра масс
            double fE = p.mass / mSys;
            double fM = MoonMass / mSys;
            double vRel = Math.Sqrt(Constants.G * mSys / MoonOrbit);

            list.Add(new CelestialBody(p.name, p.mass, p.radius,
                pos - dir * (MoonOrbit * fM), vel - tang * (vRel * fM))
            {
                color = p.color,
                rotationPeriod = p.rotHours * 3600.0,
                axialTilt = p.tiltDeg
            });

            moon = new CelestialBody("Moon", MoonMass, MoonRadius,
                pos + dir * (MoonOrbit * fE), vel + tang * (vRel * fE))
            {
                color = new Color(0.7f, 0.7f, 0.7f),
                rotationPeriod = MoonRotHours * 3600.0,
                axialTilt = MoonTiltDeg
            };
        }

        list.Add(moon);
        return list.ToArray();
    }
}