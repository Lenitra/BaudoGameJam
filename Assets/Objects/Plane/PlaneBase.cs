using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public abstract class PlaneBase : MonoBehaviour
{
    [Header("Vitesse")]
    private readonly float acceleration = 2.5f;       // puissance moteur
    private readonly float maxSpeed = 25f;             // vitesse max
    private readonly float spawnSpeed = 15f;                // vitesse min
    private readonly float gravityInfluence = 5f;     // effet de la gravité sur la vitesse (selon l'inclinaison haut/bas de l'avion)

    [Header("Contrôles")]
    private readonly float pitchSpeed = 60f;           // tangage, haut/bas
    private readonly float rollSpeed = 100f;           // roulis, gauche/droite
    private readonly float controlInertia = 5f;        // inertie des contrôles (plus élevé = plus réactif)
    private readonly float rollStabilizationForce = 0.7f; // force d'auto-nivelage du roll, remise à plat (plus élevé = plus rapide)
    private readonly float rollToYawInfluence = 15f; // influence du roll sur le yaw (virage lors d'une inclinaison, plus élevé = virages plus serrés)
    private readonly float stallSpeed = 10f;            // vitesse en dessous de laquelle l'avion pique naturellement
    private readonly float stallPitchFactor = 50f;    // facteur d'inclinaison en piqué lors du décrochage (plus élevé = pique plus fort)
 
    [Header("Altitude")]
    private readonly float altitudeSoftLimit = 175f;    // altitude à partir de laquelle la résistance commence
    private readonly float altitudeEffectScale = 0.05f; // échelle d'augmentation des effets par mètre au-dessus de la limite
    private readonly float altitudeSlowdownBase = 2f;   // ralentissement de base au-dessus de la limite
    private readonly float altitudePitchForceBase = 30f; // force de piqué de base au-dessus de la limite
    private readonly float altitudeMinSpeed = 5f;       // vitesse minimale en dessous de laquelle le ralentissement ne s'applique plus

    [Header("UI (optionnel)")]
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private TextMeshProUGUI altitudeText;
    [SerializeField] private TextMeshProUGUI checkpointIndication;


    [Header("Visuals effects")]
    [SerializeField] private GameObject crashEffectPrefab;
    [SerializeField] private GameObject validateCheckpointEffectPrefab;

    [Header("Elements du jeu")]
    private List<GameObject> checkpoints; // Liste pour manipulation
    private GameObject finishLine;
    [SerializeField] private Compass compass;


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
    protected float smoothStallPitch; // Inertie pour le pitch de décrochage
    protected float smoothAltitudePitch; // Inertie pour le pitch dû à l'altitude
    protected bool isAboveAltitudeLimit; // Indique si l'avion est au-dessus de la limite d'altitude
    protected float altitudeRatio; // Ratio de dépassement d'altitude (0 = soft limit, 1 = hard limit)

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

        // Trouver tous les objets avec le tag "Checkpoint"
        GameObject[] checkpointsArray = GameObject.FindGameObjectsWithTag("Checkpoint");
        checkpoints = new List<GameObject>(checkpointsArray);

        // Trier la liste par ordre alphabétique des noms d'objets
        checkpoints.Sort((a, b) => string.Compare(a.name, b.name, System.StringComparison.Ordinal));

        // Trouver le premier objet avec le tag "Finish"
        finishLine = GameObject.FindGameObjectWithTag("Finish");

        // Configurer le compass au démarrage
        SetUpCompassTarget();
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

        // On applique un effet de gravité sur la vitesse (TOUJOURS, même au-dessus de l'altitude max)
        // Plus l'avion monte, plus il perd de vitesse (peut devenir négatif = marche arrière).
        // Plus il pique, plus il en gagne.
        float gravityEffect = -incline * gravityInfluence * Time.fixedDeltaTime;
        currentSpeed += gravityEffect;

        // Limiter l'altitude : ralentir progressivement l'avion
        float currentAltitude = transform.position.y;
        isAboveAltitudeLimit = currentAltitude > altitudeSoftLimit;

        if (isAboveAltitudeLimit)
        {
            // Calculer l'excès d'altitude (en mètres au-dessus de la limite)
            float altitudeExcess = currentAltitude - altitudeSoftLimit;

            // Le ratio augmente indéfiniment avec l'altitude (pas de limite dure)
            // altitudeRatio = distance × échelle (ex: 50m × 0.02 = 1.0)
            altitudeRatio = Mathf.Min(altitudeExcess * altitudeEffectScale, 3f); // Plafonné à 3 pour éviter des valeurs extrêmes

            // Ralentissement progressif : plus on monte, plus on ralentit
            // Ne s'applique que si l'avion n'est PAS incliné vers le bas (incline >= 0)
            // et si la vitesse est supérieure à la vitesse minimale
            if (currentSpeed > altitudeMinSpeed && incline >= 0)
            {
                float slowdownForce = altitudeSlowdownBase * (1f + altitudeRatio * 2f);
                currentSpeed -= slowdownForce * Time.fixedDeltaTime;
                // S'assurer de ne pas descendre en dessous de la vitesse minimale
                currentSpeed = Mathf.Max(currentSpeed, altitudeMinSpeed);
            }
        }
        else
        {
            altitudeRatio = 0f;
        }
    }

    // ------------------------------------------------------------
    // Applique le mouvement (déplacement et rotation)
    // ------------------------------------------------------------
    protected void ApplyMovement()
    {
        // Auto-nivelage du roll : calculer l'angle de roll actuel
        float currentRoll = transform.eulerAngles.z;
        // Convertir l'angle en range [-180, 180] pour avoir la direction correcte
        if (currentRoll > 180f) currentRoll -= 360f;

        // Réduire la force de pitch en fonction du roll
        // Plus l'avion est incliné, moins le pitch est efficace
        float rollRatio = Mathf.Abs(currentRoll) / 90f; // Ratio de 0 (à plat) à 1 (90° de roll)
        rollRatio = Mathf.Clamp01(rollRatio);
        float pitchReduction = 1f - (rollRatio * 0.7f); // Réduction jusqu'à 70% quand complètement incliné

        // Bloquer le pitch vers le haut si au-dessus de la limite d'altitude
        float adjustedPitchInput = pitchInput;
        if (isAboveAltitudeLimit && pitchInput > 0)
        {
            // Réduire progressivement le pitch vers le haut selon l'altitude
            adjustedPitchInput *= (1f - altitudeRatio);
        }

        float finalPitch = adjustedPitchInput * pitchSpeed * pitchReduction * Time.fixedDeltaTime;

        // Stall : Si vitesse trop faible, forcer le nez vers le bas
        // Calculer l'intensité du décrochage (plus la vitesse est faible, plus le piqué est fort)
        float targetStallRatio = 0f;
        if (currentSpeed < stallSpeed)
        {
            float stallRatio = 1f - (currentSpeed / stallSpeed);
            targetStallRatio = Mathf.Clamp01(stallRatio);
        }

        // Appliquer l'inertie au ratio de décrochage pour une transition douce
        smoothStallPitch = Mathf.Lerp(smoothStallPitch, targetStallRatio, Time.fixedDeltaTime * controlInertia);

        // Appliquer la force de piqué proportionnelle au ratio lissé
        finalPitch += smoothStallPitch * stallPitchFactor * Time.fixedDeltaTime;

        // Altitude : calculer la force de piqué cible (progressive selon l'altitude)
        float targetAltitudeRatio = isAboveAltitudeLimit ? altitudeRatio : 0f;

        // Appliquer l'inertie au ratio d'altitude pour une transition douce
        smoothAltitudePitch = Mathf.Lerp(smoothAltitudePitch, targetAltitudeRatio, Time.fixedDeltaTime * controlInertia);

        // Force de piqué progressive : augmente avec l'altitude
        float altitudePitchForce = altitudePitchForceBase * (1f + smoothAltitudePitch * 2f);
        finalPitch += smoothAltitudePitch * altitudePitchForce * Time.fixedDeltaTime;

        // Appliquer une force de redressement proportionnelle à l'angle
        // Plus l'avion est incliné, plus la force de redressement est forte
        // Au-dessus de l'altitude max, augmenter la force de stabilisation pour rendre l'avion plus difficile à contrôler
        float adjustedRollStabilization = rollStabilizationForce;
        if (isAboveAltitudeLimit)
        {
            // Augmenter la stabilisation progressivement avec l'altitude
            adjustedRollStabilization *= (1f + altitudeRatio * 2f);
        }
        float rollCorrection = -currentRoll * adjustedRollStabilization * Time.fixedDeltaTime;

        // Influence du roll sur le yaw GLOBAL : rotation horizontale par rapport au sol
        // On réutilise rollRatio déjà calculé plus haut pour le pitch
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

        // Clamp final de vitesse
        // Si au-dessus de l'altitude limite, vitesse minimum = 0 (pas de marche arrière)
        // Sinon, vitesse négative autorisée pour gamepad
        float minSpeed = isAboveAltitudeLimit ? 0f : -maxSpeed;
        currentSpeed = Mathf.Clamp(currentSpeed, minSpeed, maxSpeed * 1.5f);

        // Déplacement selon la vitesse actuelle
        rb.linearVelocity = transform.forward * currentSpeed;
    }

    // ------------------------------------------------------------
    // Mise à jour de l'UI
    // ------------------------------------------------------------
    protected void UpdateUI()
    {
        if (speedText != null)
            speedText.text = $"{Mathf.RoundToInt(currentSpeed*10)} km/h";
        if (altitudeText != null)
            altitudeText.text = $"Altitude: {(Mathf.RoundToInt(transform.position.y)*4)-550}m\n{(altitudeSoftLimit*4)-550}m max";
    }

    // ------------------------------------------------------------
    // Gestion des collisions
    // ------------------------------------------------------------
    protected virtual void OnCollisionEnter(Collision collision)
    {
        isCrashed = true;

        // Sauvegarder l'état de défaite
        PlayerPrefs.SetString("gamestate", "lose");
        PlayerPrefs.Save();

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
            // Effet visuel de validation
            if (validateCheckpointEffectPrefab != null)
            {
                Instantiate(validateCheckpointEffectPrefab, transform);
            }

            // Trouver et supprimer le checkpoint de la liste
            GameObject checkpointObject = other.gameObject;
            if (checkpoints.Contains(checkpointObject))
            {
                checkpoints.Remove(checkpointObject);
            }

            // Détruire le GameObject du checkpoint
            Destroy(checkpointObject);

            // Si c'était le dernier checkpoint, victoire !
            if (checkpoints.Count == 0)
            {
                PlayerPrefs.SetString("gamestate", "win");
                PlayerPrefs.Save();

                if (gameManager != null)
                {
                    gameManager.WinGame();
                }
            }

            // Mettre à jour le compass pour pointer vers le prochain checkpoint
            SetUpCompassTarget();
        }

        // On vérifie le tag de l'objet avec lequel on entre en collision
        if (other.gameObject.CompareTag("Finish") && checkpoints.Count == 0)
        {
            // Sauvegarder l'état de victoire
            PlayerPrefs.SetString("gamestate", "win");
            PlayerPrefs.Save();

            if (gameManager != null)
            {
                gameManager.WinGame();
            }
        }
    }
    
    void SetUpCompassTarget()
    {
        // Si pas de compass, ne rien faire
        if (compass == null)
            return;

        // Priorité 1 : Premier checkpoint de la liste
        if (checkpoints != null && checkpoints.Count > 0)
        {
            compass.SetTarget(checkpoints[0]);
            compass.gameObject.SetActive(true);

            // Activer seulement le premier checkpoint, désactiver les autres
            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (checkpoints[i] != null)
                {
                    checkpoints[i].SetActive(i == 0);
                }
            }
        }
        // Priorité 2 : Finish line si plus de checkpoints
        else if (finishLine != null)
        {
            compass.SetTarget(finishLine);
            compass.gameObject.SetActive(true);
        }
        // Priorité 3 : Désactiver le compass si rien à viser
        else
        {
            compass.gameObject.SetActive(false);
        }
    }


}
