using UnityEngine;

// Повесить на Main Camera. Включает skybox и переключает фон камеры.
public class SkySetup : MonoBehaviour
{
    public Material skyMaterial;

    void Awake()
    {
        if (skyMaterial == null) return;
        RenderSettings.skybox = skyMaterial;
        GetComponent<Camera>().clearFlags = CameraClearFlags.Skybox;
    }
}