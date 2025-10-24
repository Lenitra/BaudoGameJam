using System;
using System.Collections;
using UnityEngine;

public class Environnement : MonoBehaviour
{
    [SerializeField] private Light globalLight;
    [SerializeField] private GameObject waterParentObject;
    [SerializeField] private Material skyBoxMaterial;

    [SerializeField] private Color defaultColor = new Color(0.0f, 0.5f, 0.7f);



    void Awake()
    {
        RenderSettings.skybox = skyBoxMaterial;
        ChangeWaterColor(defaultColor);
        ChangeSkyColor(defaultColor);
        ChangeLightColorIntensity(defaultColor);
    }

    private void ChangeLightColorIntensity(Color color)
    {
        globalLight.color = color;
        float intensity = (color.r + color.g + color.b) / 10.0f;
        globalLight.intensity = intensity;
    }

    private void ChangeWaterColor(Color color)
    {
        color /= 10;
        color.a = 1;
        foreach (Transform child in waterParentObject.transform)
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
                        mat.SetColor("_HorizonColor", new Color(color.r * 0.3f, color.g * 0.3f, color.b * 0.3f, 1.0f));
                    }
                    if (mat.HasProperty("_DeepColor"))
                    {
                        mat.SetColor("_DeepColor", color * 0.7f);
                    }
                    if (mat.HasProperty("_SpectacularColor"))
                    {
                        Color specColor = new Color(color.r * 1.1f, color.g * 1.1f, color.b * 1.1f, 0.1f);
                        mat.SetColor("_SpectacularColor", specColor);

                    }
                    if (mat.HasProperty("_SecondaryFoamColor"))
                    {
                        Color secondaryFoamColor = new Color(color.r, color.g, color.b, 0.3f);
                        mat.SetColor("_SecondaryFoamColor", secondaryFoamColor);
                    }
                    if (mat.HasProperty("_SurfaceFoamColor"))
                    {
                        float saturationMax = Mathf.Max(color.r, Mathf.Max(color.g, color.b)) * 1.1f;
                        Color foamColor = new Color(saturationMax, saturationMax, saturationMax, 0.4f);
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