using UnityEngine;

public class PlanetVisuals : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string bodyName;
        public Material material;
        public int segments = 96;
        public float axialTilt = 0f;      // градусы
        public float oblateness = 0f;     // сплюснутость (0..0.1)
        public bool ring = false;
        public float ringInner = 1.2f, ringOuter = 2.3f;
        public Material ringMaterial;
    }

    public SimulationManager sim;
    public Entry[] entries;
    bool built;

    void Update()
    {
        if (built || sim == null || sim.Bodies == null) return;
        if (sim.Bodies[0].visual == null) return;

        foreach (var b in sim.Bodies)
        {
            var e = System.Array.Find(entries, x => x != null && x.bodyName == b.name);
            if (e == null || b.visual == null) continue;

            var mf = b.visual.GetComponent<MeshFilter>();
            int seg = e.segments > 0 ? e.segments : 96;
            if (mf != null) mf.sharedMesh = PlanetMesh.Sphere(seg);

            var r = b.visual.GetComponent<MeshRenderer>();
            if (r != null && e.material != null) r.sharedMaterial = e.material;

            var sc = b.visual.localScale;
            sc.y *= (1f - e.oblateness);
            b.visual.localScale = sc;
            b.visual.localRotation = Quaternion.Euler(e.axialTilt, 0, 0);

            if (e.ring && e.ringMaterial != null)
            {
                var ring = new GameObject(b.name + "Ring");
                ring.transform.SetParent(b.visual, false);
                ring.transform.localScale = Vector3.one * 0.5f; // радиусы в радиусах планеты
                ring.AddComponent<MeshFilter>().sharedMesh = PlanetMesh.Ring(e.ringInner, e.ringOuter);
                ring.AddComponent<MeshRenderer>().sharedMaterial = e.ringMaterial;
            }
        }
        built = true;
    }
}