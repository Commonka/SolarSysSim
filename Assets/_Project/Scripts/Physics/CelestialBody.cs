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

    [NonSerialized] public Transform visual; // сфера на сцене
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