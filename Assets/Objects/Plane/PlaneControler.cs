using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlaneControler : MonoBehaviour
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

    private Rigidbody rb;
    private float currentSpeed;
    private Gamepad gamepad;
    private GameManager gameManager;
    private bool isCrashed = false;

    void Awake()
    {
        gameManager = FindFirstObjectByType<GameManager>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;       // On gère la gravité manuellement
        rb.linearDamping = 0.5f;     // FIX: drag → linearDamping
        rb.angularDamping = 2f;      // FIX: angularDrag → angularDamping
        currentSpeed = minSpeed;     // L'avion démarre à la vitesse minimale
    }

    void FixedUpdate()
    {
        gamepad = Gamepad.current;
        HandleInput();
        HandleGravityByIncline();
        ApplyMovement();
        UpdateUI();
    }

    // ------------------------------------------------------------
    // Lecture des entrées manette
    // ------------------------------------------------------------
    private float throttleInput;
    private float pitchInput;
    private float rollInput;
    private float smoothPitch; // Inertie pour le pitch
    private float smoothRoll;  // Inertie pour le roll

    private void HandleInput()
    {
        // Trigger droit = accélérer, gauche = ralentir
        float accelTrigger = gamepad.rightTrigger.ReadValue();
        float brakeTrigger = gamepad.leftTrigger.ReadValue();
        throttleInput = accelTrigger - brakeTrigger;

        // Stick gauche = pitch / roll
        Vector2 stick = gamepad.leftStick.ReadValue();
        float targetPitch = stick.y;
        float targetRoll = -stick.x;

        // Appliquer l'inertie aux contrôles (Lerp progressif)
        smoothPitch = Mathf.Lerp(smoothPitch, targetPitch, Time.fixedDeltaTime * controlInertia);
        smoothRoll = Mathf.Lerp(smoothRoll, targetRoll, Time.fixedDeltaTime * controlInertia);

        pitchInput = smoothPitch;
        rollInput = smoothRoll;

        // Ajuster la vitesse selon les triggers
        currentSpeed += throttleInput * acceleration * Time.fixedDeltaTime;
        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed, maxSpeed); // Vitesse peut être négative
    }

    // ------------------------------------------------------------
    // Gravité dynamique : modifie la vitesse selon l’inclinaison
    // ------------------------------------------------------------
    private void HandleGravityByIncline()
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

        // Clamp pour éviter les valeurs extrêmes (vitesse négative autorisée)
        currentSpeed = Mathf.Clamp(currentSpeed, -maxSpeed, maxSpeed * 1.5f);
    }

    // ------------------------------------------------------------
    // Applique le mouvement (déplacement et rotation)
    // ------------------------------------------------------------
    private void ApplyMovement()
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
    private void UpdateUI()
    {
        speedText.text = $"Speed: {Mathf.RoundToInt(currentSpeed)}";
        altitudeText.text = $"Altitude: {Mathf.RoundToInt(transform.position.y)}";
    }




    // ------------------------------------------------------------
    // Gestion des collisions
    // ------------------------------------------------------------
    private void OnCollisionEnter(Collision collision)
    {
        isCrashed = true;
        // Ajouter des effets de crash ici (ex: son, particules, etc.)
        Instantiate(crashEffectPrefab, transform.position, Quaternion.identity);

        // MEGA VIBRATION au crash !
        if (gamepad != null)
        {
            StartCoroutine(CrashVibration());
        }

        // Sequence de crash qui notifie le GameManager
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
    private IEnumerator CrashVibration()
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
    private void OnTriggerEnter(Collider other)
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
