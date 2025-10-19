using UnityEngine;
using UnityEngine.InputSystem;
using TMPro;
using System.Collections;
using Unity.VisualScripting;

[RequireComponent(typeof(Rigidbody))]
public class PlaneControler : MonoBehaviour
{
    [Header("Vitesse")]
    [SerializeField] private float acceleration = 10f;        // puissance moteur
    [SerializeField] private float maxSpeed = 80f;             // vitesse max
    [SerializeField] private float gravityInfluence = 15f;     // effet de la gravité sur la vitesse

    [Header("Contrôles")]
    [SerializeField] private float pitchSpeed = 80f;           // tangage
    [SerializeField] private float rollSpeed = 120f;           // roulis
    [SerializeField] private float controlInertia = 5f;        // inertie des contrôles (plus élevé = plus réactif)
    [SerializeField] private bool invertPitch = false;

    [Header("Stall")]
    [SerializeField] private float stallSpeed = 15f;           // vitesse en dessous de laquelle l'avion pique
    [SerializeField] private float stallPitchForce = 20f;      // force de piqué lors du stall

    [Header("UI (optionnel)")]
    [SerializeField] private TextMeshProUGUI speedText;

    [Header("Visuals effects")]
    [SerializeField] private GameObject crashEffectPrefab;

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
        currentSpeed = 0f;           // Suppression de minSpeed - vitesse démarre à 0
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
        float targetPitch = stick.y * (invertPitch ? -1f : 1f);
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
            finalPitch += stallPitchForce * Time.fixedDeltaTime;
        }

        // Rotation basée sur les entrées (plus de yaw - stick droit supprimé)
        Quaternion rotationInput = Quaternion.Euler(
            finalPitch,
            0f, // Pas de yaw
            rollInput * rollSpeed * Time.fixedDeltaTime
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
    }




    // ------------------------------------------------------------
    // Gestion des collisions
    // ------------------------------------------------------------
    private void OnCollisionEnter(Collision collision)
    {        
        isCrashed = true;
        // Ajouter des effets de crash ici (ex: son, particules, etc.)
        Instantiate(crashEffectPrefab, transform.position, Quaternion.identity);

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
