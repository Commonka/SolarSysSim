using System.Collections.Generic;
using UnityEngine;

public static class PlanetMesh
{
    static readonly Dictionary<int, Mesh> sphereCache = new();

    // Сфера радиуса 0.5 (как Unity primitive), но с настраиваемой детализацией
    public static Mesh Sphere(int seg)
    {
        if (sphereCache.TryGetValue(seg, out var cached)) return cached;
        int rings = seg / 2;
        var verts = new List<Vector3>(); var norms = new List<Vector3>();
        var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int y = 0; y <= rings; y++)
        {
            float phi = y / (float)rings * Mathf.PI;
            for (int x = 0; x <= seg; x++)
            {
                float theta = x / (float)seg * 2 * Mathf.PI;
                Vector3 p = new Vector3(
                    Mathf.Sin(phi) * Mathf.Cos(theta),
                    Mathf.Cos(phi),
                    Mathf.Sin(phi) * Mathf.Sin(theta)) * 0.5f;
                verts.Add(p); norms.Add(p.normalized);
                uvs.Add(new Vector2(x / (float)seg, 1 - y / (float)rings));
            }
        }
        for (int y = 0; y < rings; y++)
            for (int x = 0; x < seg; x++)
            {
                int i = y * (seg + 1) + x;
                tris.Add(i); tris.Add(i + 1); tris.Add(i + seg + 1);
                tris.Add(i + 1); tris.Add(i + seg + 2); tris.Add(i + seg + 1);
            }
        var m = new Mesh { name = $"Sphere_{seg}" };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        sphereCache[seg] = m;
        return m;
    }

    // Кольцо в плоскости XZ, радиусы в «радиусах планеты», uv.y = радиальная координата
    public static Mesh Ring(float inner, float outer, int seg = 128)
    {
        var verts = new List<Vector3>(); var norms = new List<Vector3>();
        var uvs = new List<Vector2>(); var tris = new List<int>();
        for (int x = 0; x <= seg; x++)
        {
            float t = x / (float)seg * 2 * Mathf.PI;
            float c = Mathf.Cos(t), s = Mathf.Sin(t);
            verts.Add(new Vector3(c * inner, 0, s * inner));
            verts.Add(new Vector3(c * outer, 0, s * outer));
            norms.Add(Vector3.up); norms.Add(Vector3.up);
            uvs.Add(new Vector2(x / (float)seg, 0));
            uvs.Add(new Vector2(x / (float)seg, 1));
        }
        for (int x = 0; x < seg; x++)
        {
            int b = x * 2;
            tris.Add(b); tris.Add(b + 2); tris.Add(b + 1);
            tris.Add(b + 1); tris.Add(b + 2); tris.Add(b + 3);
        }
        var m = new Mesh { name = "Ring" };
        m.SetVertices(verts); m.SetNormals(norms); m.SetUVs(0, uvs); m.SetTriangles(tris, 0);
        m.RecalculateBounds();
        return m;
    }
}