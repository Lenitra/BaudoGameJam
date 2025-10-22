using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Compass : MonoBehaviour
{
    private GameObject target;

    // Setter pour le target
    public void SetTarget(GameObject newTarget)
    {
        target = newTarget;
    }

    // Update est appelé à chaque frame
    void Update()
    {
        // Si on a un target, faire pointer la boussole vers lui
        if (target != null)
        {
            // Calculer la direction vers le target (seulement sur le plan horizontal)
            Vector3 direction = target.transform.position - transform.position;
            // Bloquer l'axe Y pour n'avoir que la direction horizontale
            direction.y = 0;

            // Si la direction n'est pas nulle, orienter la boussole
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                // Ajouter une rotation de 180° sur l'axe Y pour inverser la flèche
                transform.rotation = targetRotation * Quaternion.Euler(0, 180, 0);
            }
        }
    }
}