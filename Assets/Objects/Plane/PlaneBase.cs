using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public abstract class PlaneBase : MonoBehaviour
{
    [Header("Vitesse")]
    private readonly float acceleration = 3f;       // puissance moteur
    private readonly float deceleration = 7.5f;
    private readonly float maxSpeed = 30f;             // vitesse max
    private readonly float minSpeed = 5f;                // vitesse min
    private readonly float gravityInfluence = 10f;     // effet de la gravité sur la vitesse (selon l'inclinaison haut/bas de l'avion)
    private readonly float maxAltitude = 175f;          // altitude maximale
    private readonly float altitudeSlowdownRate = 1f;  // vitesse de ralentissement au-dessus de l'altitude max
    private readonly float altitudeDecelerationFactor = 0.1f; // facteur multiplicateur de la décélération (ajustable)
    private readonly float altitudePitchDownFactor = 5f; // force de piqué vers le bas au-dessus de l'altitude max
    private readonly float altitudeRollStabilizationFactor = 2f; // multiplicateur de la stabilisation du roll en altitude

    [Header("Contrôles")]
    private readonly float pitchSpeed = 80f;           // tangage, haut/bas
    private readonly float rollSpeed = 120f;           // roulis, gauche/droite
    private readonly float controlInertia = 5f;        // inertie des contrôles (plus élevé = plus réactif)
    private readonly float rollStabilizationForce = 0.5f; // force d'auto-nivelage du roll, remise à plat (plus élevé = plus rapide)
    private readonly float rollToPitchInfluence = 7.5f; // influence du roll sur le pitch (virage lors d'une inclinaison, plus élevé = virages plus serrés)
    private readonly float rollToYawInfluence = 10f;   // influence du roll sur le yaw (rotation automatique pendant le roll, plus élevé = virages plus serrés)
    private readonly float stallSpeed = 15f;           // vitesse en dessous de laquelle l'avion pique naturellement


    [Header("UI (optionnel)")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI altitudeText;


    [Header("Visuals effects")]
    [SerializeField] private GameObject crashEffectPrefab;
    [SerializeField] private GameObject checkpointEffectPrefab;


    private float crashVibrationIntensity = 1f; // Intensité de la vibration au crash
    private float crashVibrationDuration = 2f; // Durée de la vibration en secondes

    protected Rigidbody rb;
    protected float currentSpeed;
    protected Gamepad gamepad;
    protected GameManager gameManager;
    protected bool isCrashed = false;
    protected bool canAccelerate = true; // Indique si le joueur peut accélérer

    // Variables pour les inputs
    protected float throttleInput;
    protected float pitchInput;
    protected float rollInput;
    protected float smoothPitch; // Inertie pour le pitch
    protected float smoothRoll;  // Inertie pour le roll

    // Propriétés pour accéder aux constantes depuis les classes dérivées
    protected float Acceleration => acceleration;
    protected float MaxSpeed => maxSpeed;
    protected float MinSpeed => minSpeed;
    protected float GravityInfluence => gravityInfluence;
    protected float PitchSpeed => pitchSpeed;
    protected float RollSpeed => rollSpeed;
    protected float ControlInertia => controlInertia;
    protected float RollStabilizationForce => rollStabilizationForce;
    protected float RollToPitchInfluence => rollToPitchInfluence;
    protected float RollToYawInfluence => rollToYawInfluence;
    protected float StallSpeed => stallSpeed;
    [Header("Game Elements")]
    private List<GameObject> checkpoints = new List<GameObject>();
    private int currentCheckpointIndex = 0; // Index du checkpoint actuel



    protected virtual void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;       // On gère la gravité manuellement
        rb.linearDamping = 0.5f;
        rb.angularDamping = 2f;
        currentSpeed = minSpeed;     // L'avion démarre à la vitesse minimale

        // Récupérer tout les objets avec un tag "Checkpoint"
        GameObject[] foundCheckpoints = GameObject.FindGameObjectsWithTag("Checkpoint");

        // Trier les checkpoints par leur nom (ordre numérique)
        checkpoints = foundCheckpoints.OrderBy(cp => {
            // Extraire le nombre du nom (ex: "1", "2", "10", etc.)
            if (int.TryParse(cp.name, out int number))
                return number;
            return 0;
        }).ToList();

        Debug.Log($"Found {checkpoints.Count} checkpoints in the scene");

        // Désactiver tous les checkpoints
        foreach (GameObject checkpoint in checkpoints)
        {
            checkpoint.SetActive(false);
        }

        // Activer le premier checkpoint s'il existe
        if (checkpoints.Count > 0)
        {
            checkpoints[0].SetActive(true);
            Debug.Log($"Activated first checkpoint: {checkpoints[0].name}");
        }
    }

    protected virtual void FixedUpdate()
    {
        gamepad = Gamepad.current;
        HandleInput();
        HandleGravityByIncline();
        ApplyMovement();
        UpdateUI();
    }

    // Méthode abstraite à implémenter dans les classes dérivées
    protected abstract void HandleInput();

    // ------------------------------------------------------------
    // Gravité dynamique : modifie la vitesse selon l'inclinaison
    // ------------------------------------------------------------
    protected void HandleGravityByIncline()
    {
        // Calcul de l'angle de montée / descente
        // Dot produit entre le vecteur avant de l'avion et le vecteur vers le haut du monde
        float incline = Vector3.Dot(transform.forward, Vector3.up);
        // incline > 0 = monte, incline < 0 = pique

        // On applique un effet de gravité sur la vitesse
        // Plus l'avion monte, plus il perd de vitesse (peut devenir négatif = marche arrière).
        // Plus il pique, plus il en gagne.
        float gravityEffect = -incline * gravityInfluence * Time.fixedDeltaTime;
        currentSpeed += gravityEffect;

        // Clamp pour éviter les valeurs extrêmes (vitesse négative autorisée pour gamepad)
        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed, maxSpeed * 1.5f);
    }

    // ------------------------------------------------------------
    // Applique le mouvement (déplacement et rotation)
    // ------------------------------------------------------------
    protected void ApplyMovement()
    {
        // Vérifier si on dépasse l'altitude maximale
        bool aboveMaxAltitude = transform.position.y > maxAltitude;

        // Si au-dessus de l'altitude max, empêcher l'accélération et ralentir progressivement
        if (aboveMaxAltitude)
        {
            canAccelerate = false;

            // Vérifier si l'avion est en montée (incline > 0) ou en descente (incline < 0)
            float incline = Vector3.Dot(transform.forward, Vector3.up);

            // Appliquer le ralentissement SEULEMENT si l'avion pique vers le haut (en montée)
            if (incline > 0f)
            {
                // Calculer la différence d'altitude (plus on est haut, plus on ralentit)
                float altitudeDifference = transform.position.y - maxAltitude;
                float slowdownForce = altitudeDifference * altitudeSlowdownRate * altitudeDecelerationFactor * Time.fixedDeltaTime;

                currentSpeed -= slowdownForce;
                currentSpeed = Mathf.Max(currentSpeed, minSpeed);
            }
        }
        else
        {
            canAccelerate = true;
        }

        float finalPitch = pitchInput * pitchSpeed * Time.fixedDeltaTime;

        // Stall : Si vitesse trop faible, forcer le nez vers le bas
        if (currentSpeed < stallSpeed)
        {
            finalPitch += gravityInfluence * Time.fixedDeltaTime;
        }

        // Force de piqué au-dessus de l'altitude max : plus on est haut, plus l'avion pique vers le bas
        if (aboveMaxAltitude)
        {
            float altitudeDifference = transform.position.y - maxAltitude;
            float pitchDownForce = altitudeDifference * altitudePitchDownFactor * Time.fixedDeltaTime;
            finalPitch += pitchDownForce; // Positif = vers le bas (pitch down)
        }

        // Auto-nivelage du roll : calculer l'angle de roll actuel
        float currentRoll = transform.eulerAngles.z;
        // Convertir l'angle en range [-180, 180] pour avoir la direction correcte
        if (currentRoll > 180f) currentRoll -= 360f;

        // Appliquer une force de redressement proportionnelle à l'angle
        // Plus l'avion est incliné, plus la force de redressement est forte
        // Ne s'applique que quand il n'y a pas d'input de roll (proche de 0)
        float rollCorrection = 0f;
        if (Mathf.Abs(rollInput) < 0.1f) // Seuil de détection d'absence d'input
        {
            // Augmenter la force de stabilisation en fonction de l'altitude
            float rollStabilization = rollStabilizationForce;

            if (aboveMaxAltitude)
            {
                float altitudeDifference = transform.position.y - maxAltitude;
                rollStabilization += altitudeDifference * altitudeRollStabilizationFactor * Time.fixedDeltaTime;
            }

            rollCorrection = -currentRoll * rollStabilization * Time.fixedDeltaTime;
        }

        // Influence du roll sur le pitch : quand l'avion est en roll, il tire légèrement vers le haut
        // Calculer le ratio d'inclinaison (0 à 90° = 0% à 100%)
        float rollRatio = Mathf.Abs(currentRoll) / 90f; // Valeur entre 0 et 1+ (peut dépasser 1 si > 90°)
        rollRatio = Mathf.Clamp01(rollRatio); // Limiter entre 0 et 1

        // Appliquer une courbe pour avoir une transition naturelle (presque rien à 0°, fort à 90°)
        // On utilise une courbe exponentielle pour accentuer l'effet aux angles élevés
        float rollInfluenceFactor = rollRatio * rollRatio; // Courbe quadratique

        // Facteur de vitesse : plus l'avion va vite, plus la correction est forte
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed); // Ratio de vitesse entre 0 et 1
        float speedFactor = 1f + (speedRatio * 2f); // Entre 1x (vitesse 0) et 3x (vitesse max)

        float rollInfluence = rollInfluenceFactor * rollToPitchInfluence * speedFactor * Time.fixedDeltaTime;
        finalPitch -= rollInfluence; // Négatif = vers le haut (pitch up)

        // Rotation basée sur les entrées + correction d'auto-nivelage
        float finalRoll = (rollInput * rollSpeed * Time.fixedDeltaTime) + rollCorrection;

        // Appliquer d'abord les rotations pitch et roll dans l'espace local
        Quaternion localRotation = Quaternion.Euler(
            finalPitch,
            0f,
            finalRoll
        );

        rb.MoveRotation(rb.rotation * localRotation);

        // Calcul du yaw automatique basé sur le roll dans l'espace MONDIAL
        // Quand l'avion est incliné, il tourne automatiquement autour de l'axe Y mondial (vertical)
        float yawFromRoll = -(currentRoll / 90f) * rollToYawInfluence * speedFactor * Time.fixedDeltaTime;

        // Appliquer la rotation yaw autour de l'axe Y mondial (Vector3.up)
        Quaternion worldYawRotation = Quaternion.AngleAxis(yawFromRoll, Vector3.up);
        rb.MoveRotation(worldYawRotation * rb.rotation);

        // Déplacement selon la vitesse actuelle
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    // ------------------------------------------------------------
    // Mise à jour de l'UI
    // ------------------------------------------------------------
    protected void UpdateUI()
    {
        if (speedText != null)
            speedText.text = $"Speed: {Mathf.RoundToInt(currentSpeed)}";
        if (altitudeText != null)
            altitudeText.text = $"Altitude: {Mathf.RoundToInt(transform.position.y)}";
    }

    // ------------------------------------------------------------
    // Gestion des collisions
    // ------------------------------------------------------------
    protected virtual void OnCollisionEnter(Collision collision)
    {
        isCrashed = true;
        // Ajouter des effets de crash ici (ex: son, particules, etc.)
        if (crashEffectPrefab != null)
            Instantiate(crashEffectPrefab, transform.position, Quaternion.identity);

        // MEGA VIBRATION au crash !
        if (gamepad != null)
        {
            StartCoroutine(CrashVibration());
        }

        // Sequence de crash qui notifie le GameManager
        if (gameManager != null)
            StartCoroutine(gameManager.CrashSequence());

        // Désactiver tout les meshes de l'avion pour simuler la destruction
        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>())
        {
            mr.enabled = false;
        }

        // Désactiver les rigidbodies pour arrêter tout mouvement
        foreach (Rigidbody r in GetComponentsInChildren<Rigidbody>())
        {
            r.isKinematic = true;
        }

        // Désactiver les colliders pour éviter d'autres collisions
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }
    }

    // ------------------------------------------------------------
    // Coroutine pour la vibration de crash
    // ------------------------------------------------------------
    protected IEnumerator CrashVibration()
    {
        // Démarrer la vibration à intensité maximale
        gamepad.SetMotorSpeeds(crashVibrationIntensity, crashVibrationIntensity);

        // Attendre la durée définie
        yield return new WaitForSeconds(crashVibrationDuration);

        // Arrêter la vibration
        gamepad.SetMotorSpeeds(0f, 0f);
    }

    // ------------------------------------------------------------
    // Gestion des triggers
    // ------------------------------------------------------------
    protected virtual void OnTriggerEnter(Collider other)
    {
        // On vérifie le tag de l'objet avec lequel on entre en collision
        if (other.gameObject.CompareTag("Finish"))
        {
            if (gameManager != null)
            {
                gameManager.WinGame();
            }
        }
        if (other.gameObject.CompareTag("Checkpoint"))
        {
            // Vérifier que c'est bien le checkpoint actuel
            if (currentCheckpointIndex < checkpoints.Count && other.gameObject == checkpoints[currentCheckpointIndex])
            {
                // Effet visuel lors du passage du checkpoint (déjà instancié dans Awake)
                if (checkpointEffectPrefab != null)
                {
                    Instantiate(checkpointEffectPrefab, other.transform.position, Quaternion.identity);
                }

                Debug.Log($"Checkpoint passed: {checkpoints[currentCheckpointIndex].name}");

                // Désactiver le checkpoint actuel
                checkpoints[currentCheckpointIndex].SetActive(false);

                // Passer au checkpoint suivant
                currentCheckpointIndex++;

                // Activer le prochain checkpoint s'il existe
                if (currentCheckpointIndex < checkpoints.Count)
                {
                    checkpoints[currentCheckpointIndex].SetActive(true);
                    Debug.Log($"Activated next checkpoint: {checkpoints[currentCheckpointIndex].name}");
                }
                else
                {
                    Debug.Log("All checkpoints completed!");
                }
            }
        }
    }


}