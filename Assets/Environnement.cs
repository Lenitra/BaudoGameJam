using System.Collections;
using UnityEngine;

public class Environnement : MonoBehaviour
{
    [SerializeField] private Light globalLight;
    [SerializeField] private GameObject waterParrentObject;



    void Awake()
    {
        ChangeWaterColor(Color.red);
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
                        mat.SetColor("_CaveColor", color * 0.6f);
                    }
                    if (mat.HasProperty("_SecondaryFoamColor"))
                    {
                        mat.SetColor("_SecondaryFoamColor", color * 0.5f);
                    }
                }
            }
        }
    }

}
