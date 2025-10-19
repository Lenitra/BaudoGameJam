using System.Collections;
using UnityEngine;

public class Environnement : MonoBehaviour
{
    [SerializeField] private Light globalLight;
    [SerializeField] private GameObject waterParrentObject;
    [SerializeField] private Material skyBoxMaterial;

    [SerializeField] private Color DefaultWaterColor = new Color(0.0f, 0.5f, 0.7f);



    void Awake()
    {
        RenderSettings.skybox = skyBoxMaterial;
        ChangeWaterColor(DefaultWaterColor);
        // ChangeSkyBoxColor(DefaultWaterColor);
        ChangeSkyColor(DefaultWaterColor);

    }

private void ChancheLightColorIntensity(Color color)
    {
        globalLight.color = color;
        float intensity = (color.r + color.g + color.b) / 3.0f;
        globalLight.intensity = intensity;
    }

    private void ChangeWaterColor(Color color)
    {
        foreach (Transform child in waterParrentObject.transform)
        {
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
            {
                foreach (Material mat in renderer.materials)
                {
                    // Debug: Lister toutes les proprietes du shader pour trouver le bon nom
                    Debug.Log($"=== Material: {mat.name} - Shader: {mat.shader.name} ===");
                    for (int i = 0; i < mat.shader.GetPropertyCount(); i++)
                    {
                        string propName = mat.shader.GetPropertyName(i);
                        var propType = mat.shader.GetPropertyType(i);
                        if (propType == UnityEngine.Rendering.ShaderPropertyType.Color)
                        {
                            Debug.Log($"  Color Property [{i}]: {propName}");
                        }
                    }

                    if (mat.HasProperty("_ShallowColor"))
                    {
                        mat.SetColor("_ShallowColor", color);
                    }
                    if (mat.HasProperty("_HorizonColor"))
                    {
                        mat.SetColor("_HorizonColor", color);
                    }
                    if (mat.HasProperty("_CaveColor"))
                    {
                        mat.SetColor("_CaveColor", color * 0.2f);
                    }
                    if (mat.HasProperty("_SecondaryFoamColor"))
                    {
                        mat.SetColor("_SecondaryFoamColor", color * 0.5f);
                    }
                    if (mat.HasProperty("_SurfaceFoamColor"))
                    {
                        float saturationMoyenne = (color.r + color.g + color.b) / 3.0f;
                        float foamIntensifier = 3.0f;
                        Color foamColor = new Color(saturationMoyenne * foamIntensifier, saturationMoyenne * foamIntensifier, saturationMoyenne * foamIntensifier);
                        mat.SetColor("_SurfaceFoamColor", foamColor);
                    }
                }
            }
        }
    }


    private void ChangeSkyColor(Color color)
    {
        if (skyBoxMaterial.HasProperty("_SkyColor"))
        {
            skyBoxMaterial.SetColor("_SkyColor", color);
        }
        // Faire un dégradé plus foncé sur EquatorColor et GroundColor
        if (skyBoxMaterial.HasProperty("_EquatorColor"))
        {
            skyBoxMaterial.SetColor("_EquatorColor", color * 0.7f);
        }
        if (skyBoxMaterial.HasProperty("_GroundColor"))
        {
            skyBoxMaterial.SetColor("_GroundColor", color * 0.3f);
        }
    }
}