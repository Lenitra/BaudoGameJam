using System.Collections;
using TMPro;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class PlaneControlerClavier : MonoBehaviour
{
    [Header("Vitesse")]
    private readonly float acceleration = 5f;       // puissance moteur
    private readonly float maxSpeed = 100f;             // vitesse max
    private readonly float minSpeed = 5f;                // vitesse min
    private readonly float gravityInfluence = 10f;     // effet de la gravite sur la vitesse (selon l'inclinaison haut/bas de l'avion)

    [Header("Controles")]
    private readonly float pitchSpeed = 80f;           // tangage, haut/bas
    private readonly float rollSpeed = 120f;           // roulis, gauche/droite
    private readonly float controlInertia = 2.5f;        // inertie des controles (plus eleve = plus reactif)
    private readonly float rollStabilizationForce = 0.5f; // force d'auto-nivelage du roll, remise a plat (plus eleve = plus rapide)
    private readonly float rollToPitchInfluence = 7.5f; // influence du roll sur le pitch (virage lors d'une inclinaison, plus eleve = virages plus serres)
    private readonly float stallSpeed = 15f;           // vitesse en dessous de laquelle l'avion pique naturellement


    [Header("UI (optionnel)")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI altitudeText;


    [Header("Visuals effects")]
    [SerializeField] private GameObject crashEffectPrefab;



    private float crashVibrationIntensity = 1f; // Intensite de la vibration au crash
    private float crashVibrationDuration = 2f; // Duree de la vibration en secondes

    private Rigidbody rb;
    private float currentSpeed;
    private Gamepad gamepad;
    private GameManager gameManager;
    private bool isCrashed = false;

    void Awake()
    {
        // get the playerprefs to know which controller to use
        if (PlayerPrefs.GetString("InputMethod") != "Keyboard") 
        {
            this.enabled = false;
            return;
        }
        gameManager = FindFirstObjectByType<GameManager>();
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;       // On gere la gravite manuellement
        rb.linearDamping = 0.5f;     // FIX: drag -> linearDamping
        rb.angularDamping = 2f;      // FIX: angularDrag -> angularDamping
        currentSpeed = minSpeed;     // L'avion demarre a la vitesse minimale
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
    // Lecture des entrees clavier
    // ------------------------------------------------------------
    private float throttleInput;
    private float pitchInput;
    private float rollInput;
    private float smoothPitch; // Inertie pour le pitch
    private float smoothRoll;  // Inertie pour le roll

    private void HandleInput()
    {
        float targetPitch = 0f;
        float targetRoll = 0f;
        float accelInput = 0f;

        // === INPUT CLAVIER (nouveau Input System) ===
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            // Z = pitch vers le haut, S = pitch vers le bas
            if (keyboard.wKey.isPressed)
                targetPitch = 1f;
            if (keyboard.sKey.isPressed)
                targetPitch = -1f;

            // Q = roll vers la gauche, D = roll vers la droite
            if (keyboard.qKey.isPressed || keyboard.aKey.isPressed)
                targetRoll = 1f;
            if (keyboard.dKey.isPressed)
                targetRoll = -1f;

            // Espace = accelerer
            if (keyboard.spaceKey.isPressed)
                accelInput = 1f;
        }

        // === TRAITEMENT FINAL ===
        throttleInput = accelInput;

        // Appliquer l'inertie aux controles
        smoothPitch = Mathf.Lerp(smoothPitch, targetPitch, Time.fixedDeltaTime * controlInertia);
        smoothRoll = Mathf.Lerp(smoothRoll, targetRoll, Time.fixedDeltaTime * controlInertia);

        pitchInput = smoothPitch;
        rollInput = smoothRoll;

        // Ajuster la vitesse
        currentSpeed += throttleInput * acceleration * Time.fixedDeltaTime;
        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);
    }

    // ------------------------------------------------------------
    // Gravite dynamique : modifie la vitesse selon l'inclinaison
    // ------------------------------------------------------------
    private void HandleGravityByIncline()
    {
        // Calcul de l'angle de montee / descente
        // Dot produit entre le vecteur avant de l'avion et le vecteur vers le haut du monde
        float incline = Vector3.Dot(transform.forward, Vector3.up);
        // incline > 0 = monte, incline < 0 = pique

        // On applique un effet de gravite sur la vitesse
        // Plus l'avion monte, plus il perd de vitesse.
        // Plus il pique, plus il en gagne.
        float gravityEffect = -incline * gravityInfluence * Time.fixedDeltaTime;
        currentSpeed += gravityEffect;

        // Clamp pour eviter les valeurs extremes
        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed);
    }

    // ------------------------------------------------------------
    // Applique le mouvement (deplacement et rotation)
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

        // Appliquer une force de redressement proportionnelle a l'angle
        // Plus l'avion est incline, plus la force de redressement est forte
        float rollCorrection = -currentRoll * rollStabilizationForce * Time.fixedDeltaTime;

        // Influence du roll sur le pitch : quand l'avion est en roll, il tire legerement vers le haut
        // Calculer le ratio d'inclinaison (0 a 90° = 0% a 100%)
        float rollRatio = Mathf.Abs(currentRoll) / 90f; // Valeur entre 0 et 1+ (peut depasser 1 si > 90°)
        rollRatio = Mathf.Clamp01(rollRatio); // Limiter entre 0 et 1

        // Appliquer une courbe pour avoir une transition naturelle (presque rien a 0°, fort a 90°)
        // On utilise une courbe exponentielle pour accentuer l'effet aux angles eleves
        float rollInfluenceFactor = rollRatio * rollRatio; // Courbe quadratique

        // Facteur de vitesse : plus l'avion va vite, plus la correction est forte
        float speedRatio = Mathf.Clamp01(currentSpeed / maxSpeed); // Ratio de vitesse entre 0 et 1
        float speedFactor = 1f + (speedRatio * 2f); // Entre 1x (vitesse 0) et 3x (vitesse max)

        float rollInfluence = rollInfluenceFactor * rollToPitchInfluence * speedFactor * Time.fixedDeltaTime;
        finalPitch -= rollInfluence; // Negatif = vers le haut (pitch up)

        // Rotation basee sur les entrees + correction d'auto-nivelage
        float finalRoll = (rollInput * rollSpeed * Time.fixedDeltaTime) + rollCorrection;

        Quaternion rotationInput = Quaternion.Euler(
            finalPitch,
            0f, // Pas de yaw
            finalRoll
        );

        rb.MoveRotation(rb.rotation * rotationInput);

        // Deplacement selon la vitesse actuelle
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    // ------------------------------------------------------------
    // Mise a jour de l'UI
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

        // Desactiver tout les meshes de l'avion pour simuler la destruction
        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>())
        {
            mr.enabled = false;
        }

        // Desactiver les rigidbodies pour arreter tout mouvement
        foreach (Rigidbody r in GetComponentsInChildren<Rigidbody>())
        {
            r.isKinematic = true;
        }

        // Desactiver les colliders pour eviter d'autres collisions
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
        // Demarrer la vibration a intensite maximale
        gamepad.SetMotorSpeeds(crashVibrationIntensity, crashVibrationIntensity);

        // Attendre la duree definie
        yield return new WaitForSeconds(crashVibrationDuration);

        // Arreter la vibration
        gamepad.SetMotorSpeeds(0f, 0f);
    }

    // ------------------------------------------------------------
    // Gestion des triggers
    // ------------------------------------------------------------
    private void OnTriggerEnter(Collider other)
    {
        // On verifie le tag de l'objet avec lequel on entre en collision
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
