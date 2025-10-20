using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public abstract class PlaneBase : MonoBehaviour
{
    [Header("Vitesse")]
    private readonly float acceleration = 5f;       // puissance moteur
    private readonly float maxSpeed = 50f;             // vitesse max
    private readonly float spawnSpeed = 25f;                // vitesse min
    private readonly float gravityInfluence = 10f;     // effet de la gravité sur la vitesse (selon l'inclinaison haut/bas de l'avion)

    [Header("Contrôles")]
    private readonly float pitchSpeed = 120f;           // tangage, haut/bas
    private readonly float rollSpeed = 180f;           // roulis, gauche/droite
    private readonly float controlInertia = 2.5f;        // inertie des contrôles (plus élevé = plus réactif)
    private readonly float rollStabilizationForce = 0.5f; // force d'auto-nivelage du roll, remise à plat (plus élevé = plus rapide)
    private readonly float rollToYawInfluence = 30f; // influence du roll sur le yaw (virage lors d'une inclinaison, plus élevé = virages plus serrés)
    private readonly float stallSpeed = 15f;           // vitesse en dessous de laquelle l'avion pique naturellement

    [Header("UI (optionnel)")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI altitudeText;
    [SerializeField] private TextMeshProUGUI checkpointIndication;


    [Header("Visuals effects")]
    [SerializeField] private GameObject crashEffectPrefab;
    [SerializeField] private GameObject validateCheckpointEffectPrefab;

    [Header("Elements du jeu")]
    private GameObject[] checkpoints;
    private GameObject finishLine;


    private float crashVibrationIntensity = 1f; // Intensité de la vibration au crash
    private float crashVibrationDuration = 2f; // Durée de la vibration en secondes

    protected Rigidbody rb;
    protected float currentSpeed;
    protected Gamepad gamepad;
    protected GameManager gameManager;
    protected bool isCrashed = false;

    // Variables pour les inputs
    protected float throttleInput;
    protected float pitchInput;
    protected float rollInput;
    protected float smoothPitch; // Inertie pour le pitch
    protected float smoothRoll;  // Inertie pour le roll

    // Propriétés pour accéder aux constantes depuis les classes dérivées
    protected float Acceleration => acceleration;
    protected float MaxSpeed => maxSpeed;
    protected float SpawnSpeed => spawnSpeed;
    protected float GravityInfluence => gravityInfluence;
    protected float PitchSpeed => pitchSpeed;
    protected float RollSpeed => rollSpeed;
    protected float ControlInertia => controlInertia;
    protected float RollStabilizationForce => rollStabilizationForce;
    protected float RollToYawInfluence => rollToYawInfluence;
    protected float StallSpeed => stallSpeed;

    protected virtual void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;       // On gère la gravité manuellement
        rb.linearDamping = 0.5f;
        rb.angularDamping = 2f;
        currentSpeed = spawnSpeed;     // L'avion démarre à la vitesse minimale
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
        float finalPitch = pitchInput * pitchSpeed * Time.fixedDeltaTime;

        // Stall : Si vitesse trop faible, forcer le nez vers le bas
        if (currentSpeed < stallSpeed)
        {
            finalPitch += gravityInfluence * Time.fixedDeltaTime;
        }

        // Auto-nivelage du roll : calculer l'angle de roll actuel
        float currentRoll = transform.eulerAngles.z;
        // Convertir l'angle en range [-180, 180] pour avoir la direction correcte
        if (currentRoll > 180f) currentRoll -= 360f;

        // Appliquer une force de redressement proportionnelle à l'angle
        // Plus l'avion est incliné, plus la force de redressement est forte
        float rollCorrection = -currentRoll * rollStabilizationForce * Time.fixedDeltaTime;

        // Influence du roll sur le yaw GLOBAL : rotation horizontale par rapport au sol
        float rollRatio = Mathf.Abs(currentRoll) / 90f;
        rollRatio = Mathf.Clamp01(rollRatio);

        float rollInfluenceFactor = rollRatio * rollRatio; // Courbe quadratique

        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed);
        float speedFactor = 1f + (speedRatio * 2f);

        // Calculer le yaw qui sera appliqué autour de l'axe Y GLOBAL (vertical du monde)
        float rollInfluence = rollInfluenceFactor * rollToYawInfluence * speedFactor * Time.fixedDeltaTime;
        float globalYaw = -Mathf.Sign(currentRoll) * rollInfluence;

        // Rotation basée sur les entrées + correction d'auto-nivelage
        float finalRoll = (rollInput * rollSpeed * Time.fixedDeltaTime) + rollCorrection;

        // Appliquer d'abord la rotation locale (pitch et roll)
        Quaternion localRotation = Quaternion.Euler(finalPitch, 0f, finalRoll);
        rb.MoveRotation(rb.rotation * localRotation);

        // Puis appliquer la rotation globale autour de l'axe Y du monde (virage horizontal)
        Quaternion globalRotation = Quaternion.Euler(0f, globalYaw, 0f);
        rb.MoveRotation(globalRotation * rb.rotation);

        // Déplacement selon la vitesse actuelle
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    // ------------------------------------------------------------
    // Mise à jour de l'UI
    // ------------------------------------------------------------
    protected void UpdateUI()
    {
        if (speedText != null)
            speedText.text = $"{Mathf.RoundToInt(currentSpeed*5)} km/h";
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

        if (other.gameObject.CompareTag("Checkpoint"))
        {
            Instantiate(validateCheckpointEffectPrefab, transform);
        }

        // On vérifie le tag de l'objet avec lequel on entre en collision
        if (other.gameObject.CompareTag("Finish"))
        {
            if (gameManager != null)
            {
                gameManager.WinGame();
            }
        }
    }

    public void ApplyWindForce(Vector3 force)
    {
        rb.AddForce(force);
    }
}
