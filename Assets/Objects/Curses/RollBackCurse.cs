using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RollBackCurse : MonoBehaviour
{
    [Header("Recording Settings")]
    [SerializeField] private float recordInterval = 0.5f; // Intervalle d'enregistrement en secondes
    [SerializeField] private int maxRecordedStates = 100; // Nombre maximum d'états enregistrés

    [Header("Rollback Settings")]
    [SerializeField] private float rollbackDuration = 2f; // Durée du retour en arrière en secondes
    [SerializeField] private AudioSource rollbackSound; // Son joué lors du rollback

    private bool isRollingBack = false;

    // Structure pour stocker l'état complet
    [System.Serializable]
    public class ObjectState
    {
        // Transform
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;

        // Rigidbody
        public Vector3 velocity;
        public Vector3 angularVelocity;

        public ObjectState(Transform transform, Rigidbody rb)
        {
            // Enregistrer le Transform
            position = transform.position;
            rotation = transform.rotation;
            scale = transform.localScale;

            // Enregistrer le Rigidbody
            if (rb != null)
            {
                velocity = rb.linearVelocity;
                angularVelocity = rb.angularVelocity;
            }
            else
            {
                velocity = Vector3.zero;
                angularVelocity = Vector3.zero;
            }
        }
    }

    private List<ObjectState> recordedStates = new List<ObjectState>();
    private Rigidbody rb;
    private float nextRecordTime = 0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (rb == null)
        {
            Debug.LogWarning($"RollBackCurse sur {gameObject.name}: Aucun Rigidbody détecté!");
        }

        // Enregistrer l'état initial
        RecordState();
    }

    void Update()
    {
        // Ne pas enregistrer pendant le rollback
        if (isRollingBack)
            return;

        // Enregistrer l'état toutes les 0.5 secondes
        if (Time.time >= nextRecordTime)
        {
            RecordState();
            nextRecordTime = Time.time + recordInterval;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        // Détecter les collisions avec les objets "Rollback"
        if (other.CompareTag("Rollback") && !isRollingBack)
        {
            Debug.Log($"Rollback déclenché par {other.gameObject.name}");

            // Jouer le son de rollback
            if (rollbackSound != null)
            {
                rollbackSound.Play();
            }

            StartCoroutine(RollBackToFirstPosition());
        }

        // Détecter les collisions avec les checkpoints pour purger la liste
        if (other.CompareTag("Checkpoint"))
        {
            Debug.Log($"Checkpoint atteint: {other.gameObject.name} - Purge des états enregistrés");
            recordedStates.Clear();
            // Redéfinir le point 0 à la position actuelle
            RecordState();
        }
    }

    private void RecordState()
    {
        // Créer un nouvel état
        ObjectState newState = new ObjectState(transform, rb);
        recordedStates.Add(newState);

        // Limiter la taille de la liste
        if (recordedStates.Count > maxRecordedStates)
        {
            recordedStates.RemoveAt(0); // Supprimer l'état le plus ancien
        }

        Debug.Log($"État enregistré #{recordedStates.Count} - Pos: {newState.position}, Vel: {newState.velocity}");
    }

    /// <summary>
    /// Récupère tous les états enregistrés
    /// </summary>
    public List<ObjectState> GetRecordedStates()
    {
        return new List<ObjectState>(recordedStates); // Retourner une copie
    }

    /// <summary>
    /// Récupère l'état le plus récent
    /// </summary>
    public ObjectState GetLatestState()
    {
        if (recordedStates.Count > 0)
            return recordedStates[recordedStates.Count - 1];
        return null;
    }

    /// <summary>
    /// Récupère un état à un index spécifique
    /// </summary>
    public ObjectState GetStateAtIndex(int index)
    {
        if (index >= 0 && index < recordedStates.Count)
            return recordedStates[index];
        return null;
    }

    /// <summary>
    /// Applique un état enregistré à l'objet
    /// </summary>
    public void ApplyState(ObjectState state)
    {
        if (state == null) return;

        // Appliquer le Transform
        transform.position = state.position;
        transform.rotation = state.rotation;
        transform.localScale = state.scale;

        // Appliquer le Rigidbody
        if (rb != null)
        {
            rb.linearVelocity = state.velocity;
            rb.angularVelocity = state.angularVelocity;
        }

        Debug.Log($"État appliqué - Pos: {state.position}, Vel: {state.velocity}");
    }

    /// <summary>
    /// Revenir à l'état précédent (rollback)
    /// </summary>
    public void RollBackToPreviousState()
    {
        if (recordedStates.Count > 1)
        {
            // Supprimer l'état actuel et revenir au précédent
            recordedStates.RemoveAt(recordedStates.Count - 1);
            ApplyState(recordedStates[recordedStates.Count - 1]);
        }
    }

    /// <summary>
    /// Revenir à un état X secondes en arrière
    /// </summary>
    public void RollBackByTime(float seconds)
    {
        int statesBack = Mathf.RoundToInt(seconds / recordInterval);
        int targetIndex = Mathf.Max(0, recordedStates.Count - 1 - statesBack);

        if (targetIndex >= 0 && targetIndex < recordedStates.Count)
        {
            ApplyState(recordedStates[targetIndex]);
        }
    }

    /// <summary>
    /// Efface tous les états enregistrés
    /// </summary>
    public void ClearRecordedStates()
    {
        recordedStates.Clear();
    }

    /// <summary>
    /// Coroutine pour revenir progressivement à la première position
    /// </summary>
    private IEnumerator RollBackToFirstPosition()
    {
        if (recordedStates.Count == 0)
        {
            Debug.LogWarning("Aucun état enregistré pour faire un rollback!");
            yield break;
        }

        isRollingBack = true;

        // Sauvegarder la première position
        ObjectState firstState = recordedStates[0];

        // État de départ (position actuelle)
        Vector3 startPosition = transform.position;
        Quaternion startRotation = transform.rotation;
        Vector3 startVelocity = rb != null ? rb.linearVelocity : Vector3.zero;
        Vector3 startAngularVelocity = rb != null ? rb.angularVelocity : Vector3.zero;

        float elapsedTime = 0f;

        // Interpolation progressive vers la première position
        while (elapsedTime < rollbackDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = elapsedTime / rollbackDuration;

            // Interpolation avec une courbe smooth (ease-in-out)
            float smoothT = Mathf.SmoothStep(0f, 1f, t);

            // Appliquer l'interpolation au Transform
            transform.position = Vector3.Lerp(startPosition, firstState.position, smoothT);
            transform.rotation = Quaternion.Slerp(startRotation, firstState.rotation, smoothT);

            // Appliquer l'interpolation au Rigidbody
            if (rb != null)
            {
                rb.linearVelocity = Vector3.Lerp(startVelocity, firstState.velocity, smoothT);
                rb.angularVelocity = Vector3.Lerp(startAngularVelocity, firstState.angularVelocity, smoothT);
            }

            yield return null;
        }

        // S'assurer qu'on arrive exactement à la première position
        ApplyState(firstState);

        // Effacer toutes les anciennes positions
        recordedStates.Clear();

        // Redéfinir le point 0 (enregistrer la position actuelle comme nouvelle origine)
        RecordState();

        isRollingBack = false;

        Debug.Log("Rollback terminé - Point 0 redéfini");
    }

    void OnDrawGizmos()
    {
        // Visualiser les états enregistrés dans l'éditeur
        if (recordedStates != null && recordedStates.Count > 1)
        {
            Gizmos.color = Color.yellow;
            for (int i = 0; i < recordedStates.Count - 1; i++)
            {
                Gizmos.DrawLine(recordedStates[i].position, recordedStates[i + 1].position);
                Gizmos.DrawWireSphere(recordedStates[i].position, 0.1f);
            }
        }
    }
}
