using UnityEngine;

public class PlanetVisuals : MonoBehaviour
{
    [System.Serializable]
    public class Entry
    {
        public string bodyName;
        public Material material;
        public int segments = 96;
        public float axialTilt = 0f;
        public float oblateness = 0f;
        public bool ring = false;
        public float ringInner = 1.2f, ringOuter = 2.3f;
        public Material ringMaterial;

        // Текстуры: назначаются в материал в рантайме, .mat трогать не нужно
        public Texture2D mainMap;
        public Texture2D normalMap;
        [Range(0f, 3f)] public float bumpStrength = 1f;
        public Color tintColor = Color.white;
        [Range(-0.5f, 0.5f)] public float lonOffset = 0f;
        [Range(0f, 2f)] public float rimIntensity = 0f;
        public Color rimColor = new Color(0.4f, 0.6f, 1f, 1f);
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
            // Trim() чинит 'Neptune ' с пробелом в сцене без её правки
            var e = System.Array.Find(entries, x => x != null && x.bodyName != null && x.bodyName.Trim() == b.name);
            if (e == null || b.visual == null) continue;

            var mf = b.visual.GetComponent<MeshFilter>();
            int seg = e.segments > 0 ? e.segments : 96;
            if (mf != null) mf.sharedMesh = PlanetMesh.Sphere(seg);

            var r = b.visual.GetComponent<MeshRenderer>();
            if (r != null && e.material != null)
            {
                // instance, чтобы не мутировать .mat-ассет в Play Mode
                var inst = new Material(e.material);
                r.sharedMaterial = inst;

                if (e.mainMap != null) inst.SetTexture("_MainMap", e.mainMap);
                if (e.normalMap != null) inst.SetTexture("_NormalMap", e.normalMap);
                inst.SetFloat("_BumpStrength", e.bumpStrength);
                inst.SetColor("_Tint", e.tintColor);
                inst.SetFloat("_LonOffset", e.lonOffset);
                inst.SetFloat("_RimIntensity", e.rimIntensity);
                inst.SetColor("_RimColor", e.rimColor);
            }

            var sc = b.visual.localScale;
            sc.y *= (1f - e.oblateness);
            b.visual.localScale = sc;
            b.visual.localRotation = Quaternion.Euler(e.axialTilt, 0, 0);

            if (e.ring && e.ringMaterial != null)
            {
                var ring = new GameObject(b.name + "Ring");
                ring.transform.SetParent(b.visual, false);
                ring.transform.localScale = Vector3.one * 0.5f;
                ring.AddComponent<MeshFilter>().sharedMesh = PlanetMesh.Ring(e.ringInner, e.ringOuter);
                ring.AddComponent<MeshRenderer>().sharedMaterial = e.ringMaterial;
            }
        }
        built = true;
    }
}