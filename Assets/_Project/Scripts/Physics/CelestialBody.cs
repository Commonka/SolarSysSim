using System;
using UnityEngine;

[Serializable]
public class CelestialBody
{
    public string name;
    public double mass;        // кг
    public double radius;      // км
    public Vector3d position;  // км
    public Vector3d velocity;  // км/с

    public double rotationPeriod;
    public double axialTilt;
    public double rotationPhaseDeg;
    public double temperature = 280; // K

    [NonSerialized] public bool isStar;    // светится и освещает остальные тела
    [NonSerialized] public bool isUser;    // создано пользователем
    [NonSerialized] public bool recolor;   // перекрашивать звезду по температуре
    [NonSerialized] public Transform visual;
    [NonSerialized] public Material material;
    [NonSerialized] public Color color = Color.white;

    public CelestialBody(string name, double mass, double radius, Vector3d position, Vector3d velocity)
    {
        this.name = name;
        this.mass = mass;
        this.radius = radius;
        this.position = position;
        this.velocity = velocity;
    }
}