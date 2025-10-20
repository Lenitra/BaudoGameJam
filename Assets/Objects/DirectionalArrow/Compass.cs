using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Compass : MonoBehaviour
{
    private List<GameObject> checkpointList; // Liste dynamique des checkpoints
    private GameObject finishObject; // L'objet finish

    void Awake()
    {
        // Trouver tous les checkpoints et les ajouter à la liste
        checkpointList = GameObject.FindGameObjectsWithTag("Checkpoint").ToList();

        // Trouver le premier objet Finish
        GameObject[] finishObjects = GameObject.FindGameObjectsWithTag("Finish");
        finishObject = finishObjects.Length > 0 ? finishObjects[0] : null;

        if (finishObject == null && checkpointList.Count == 0)
        {
            this.gameObject.SetActive(false); // Désactiver la boussole si aucun objectif n'est trouvé
        }
    }

    void Update()
    {
        GameObject target = null;

        // Nettoyer la liste des checkpoints détruits (null)
        checkpointList.RemoveAll(checkpoint => checkpoint == null);

        // Priorité aux checkpoints : pointer vers le premier checkpoint actif
        if (checkpointList.Count > 0)
        {
            target = checkpointList[0];
        }
        // Si plus de checkpoints, pointer vers le finish
        else if (finishObject != null)
        {
            target = finishObject;
        }

        // Orienter la boussole vers la cible
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
