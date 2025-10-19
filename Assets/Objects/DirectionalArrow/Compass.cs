using UnityEngine;

public class Compass : MonoBehaviour
{
    [SerializeField] private GameObject[] finishObjects; // Tableau pour stocker les objets Finish

    [SerializeField] private GameObject[] checkpointObjects; // Cible que la boussole doit pointer



    void Awake()
    {

        // Trouver le target : un objet qui à pour tag Finish
        finishObjects = GameObject.FindGameObjectsWithTag("Finish");

        // Trouver le target : un objet qui à pour tag Checkpoint
        checkpointObjects = GameObject.FindGameObjectsWithTag("Checkpoint");


        if (finishObjects.Length == 0 && checkpointObjects.Length == 0)
        {
            this.gameObject.SetActive(false); // Désactiver la boussole si aucun objet Finish ou Checkpoint n'est trouvé
        }

    }

    void Update()
    {
        GameObject target = null;

        // Priorité aux checkpoints
        if (checkpointObjects.Length > 0)
        {
            target = checkpointObjects[0]; // Pointer vers le premier checkpoint trouvé
        }
        else if (finishObjects.Length > 0)
        {
            target = finishObjects[0]; // Pointer vers le premier finish trouvé
        }

        if (target != null)
        {
            Vector3 directionToTarget = target.transform.position - transform.position;
            directionToTarget.y = 0; // Ignorer la différence de hauteur pour une rotation horizontale

            if (directionToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 5f);
            }
        }
    }


}
