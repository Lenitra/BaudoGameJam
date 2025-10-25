using System.Collections;
using UnityEngine;

public class Rotate : MonoBehaviour
{
    [Header("Rotation Settings")]
    [Tooltip("Durée en secondes pour effectuer une rotation complète de 360 degrés sur l'axe Y")]
    [SerializeField] private float rotationDuration = 5f;

    [Tooltip("Si activé, rotation en world space (axe Y mondial). Sinon, rotation locale")]
    [SerializeField] private bool isWorldRotation = false;

    [Header("One Full Rotation Settings")]
    [Tooltip("Durée en secondes pour effectuer une rotation complète unique")]
    [SerializeField] private float oneFullRotateDuration = 2f;

    private float rotationSpeed; // Vitesse de rotation en degrés par seconde
    private bool isRotating = false; // Indique si une rotation unique est en cours

    void Start()
    {
        // Calculer la vitesse de rotation : 360 degrés divisés par la durée
        rotationSpeed = 360f / rotationDuration;
    }

    void Update()
    {
        // Appliquer la rotation continue seulement si on n'est pas en train de faire une rotation unique
        if (!isRotating && rotationDuration>0)
        {
            // Appliquer la rotation autour de l'axe Y selon le mode choisi
            Space rotationSpace = isWorldRotation ? Space.World : Space.Self;
            transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, rotationSpace);
        }
    }

    /// <summary>
    /// Effectue une rotation complète de 360 degrés sur l'axe Y
    /// </summary>
    public void OneFullRotate()
    {
        if (!isRotating)
        {
            StartCoroutine(OneFullRotateCoroutine());
        }
    }

    private IEnumerator OneFullRotateCoroutine()
    {
        isRotating = true;

        float totalRotation = 0f;
        float targetRotation = 360f;
        float rotationPerSecond = 360f / oneFullRotateDuration;

        while (totalRotation < targetRotation)
        {
            float rotationThisFrame = rotationPerSecond * Time.deltaTime;

            // S'assurer qu'on ne dépasse pas 360° exactement
            if (totalRotation + rotationThisFrame > targetRotation)
            {
                rotationThisFrame = targetRotation - totalRotation;
            }

            // Rotation sur l'axe Z (axe avant de l'avion) pour faire un looping
            // En Space.Self pour que ce soit toujours selon l'axe de l'avion
            transform.Rotate(Vector3.forward, rotationThisFrame, Space.Self);

            totalRotation += rotationThisFrame;
            yield return null;
        }

        isRotating = false;
    }
}
