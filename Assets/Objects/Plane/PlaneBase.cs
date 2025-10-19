using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public abstract class PlaneBase : MonoBehaviour
{
    [Header("Vitesse")]
    private readonly float acceleration = 5f;       // puissance moteur
    private readonly float maxSpeed = 100f;             // vitesse max
    private readonly float minSpeed = 5f;                // vitesse min
    private readonly float gravityInfluence = 10f;     // effet de la gravité sur la vitesse (selon l'inclinaison haut/bas de l'avion)

    [Header("Contrôles")]
    private readonly float pitchSpeed = 80f;           // tangage, haut/bas
    private readonly float rollSpeed = 120f;           // roulis, gauche/droite
    private readonly float controlInertia = 2.5f;        // inertie des contrôles (plus élevé = plus réactif)
    private readonly float rollStabilizationForce = 0.5f; // force d'auto-nivelage du roll, remise à plat (plus élevé = plus rapide)
    private readonly float rollToPitchInfluence = 7.5f; // influence du roll sur le pitch (virage lors d'une inclinaison, plus élevé = virages plus serrés)
    private readonly float stallSpeed = 15f;           // vitesse en dessous de laquelle l'avion pique naturellement


    [Header("UI (optionnel)")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI altitudeText;


    [Header("Visuals effects")]
    [SerializeField] private GameObject crashEffectPrefab;


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
    protected float MinSpeed => minSpeed;
    protected float GravityInfluence => gravityInfluence;
    protected float PitchSpeed => pitchSpeed;
    protected float RollSpeed => rollSpeed;
    protected float ControlInertia => controlInertia;
    protected float RollStabilizationForce => rollStabilizationForce;
    protected float RollToPitchInfluence => rollToPitchInfluence;
    protected float StallSpeed => stallSpeed;

    protected virtual void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;       // On gère la gravité manuellement
        rb.linearDamping = 0.5f;
        rb.angularDamping = 2f;
        currentSpeed = minSpeed;     // L'avion démarre à la vitesse minimale
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

        Quaternion rotationInput = Quaternion.Euler(
            finalPitch,
            0f, // Pas de yaw
            finalRoll
        );

        rb.MoveRotation(rb.rotation * rotationInput);

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
    }

    public void ApplyWindForce(Vector3 force)
    {
        rb.AddForce(force);
    }
}
