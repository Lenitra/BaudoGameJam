using UnityEngine;

public class FollowCam : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private Transform target;

    [Header("Position Settings")]
    [SerializeField] private float distance = 10f;
    [SerializeField] private float height = 5f;
    [SerializeField] private float positionLerpSpeed = 0.05f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationLerpSpeed = 0.02f;




    void FixedUpdate()
    {
        if (target == null) return;

        // Calculer la position désirée derrière l'avion (en utilisant seulement la direction horizontale)
        Vector3 horizontalForward = Vector3.ProjectOnPlane(target.forward, Vector3.up).normalized;
        Vector3 targetPosition = target.position - horizontalForward * distance + Vector3.up * height;

        // Suivre la position avec un lerp très léger
        transform.position = Vector3.Lerp(transform.position, targetPosition, positionLerpSpeed);

        // Calculer la direction de regard (toujours parallèle au sol)
        Vector3 directionToTarget = target.position - transform.position;
        directionToTarget.y = 0; // Forcer la direction à être parallèle au sol

        if (directionToTarget != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, rotationLerpSpeed);
        }
    }
}
