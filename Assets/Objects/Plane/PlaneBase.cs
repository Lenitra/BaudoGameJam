using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Contrôleur d'avion arcade - Optimisé pour le game feel et la jouabilité
/// Architecture simplifiée avec séparation claire des responsabilités
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public abstract class PlaneBase : MonoBehaviour
{
    // ════════════════════════════════════════════════════════════════════════
    // PARAMÈTRES DE VOL - Ajustez ces valeurs pour modifier le game feel
    // ════════════════════════════════════════════════════════════════════════

    [Header("💨 Vitesse")]
    [SerializeField] private float vitesseDepart = 15f;           // Vitesse au spawn
    [SerializeField] private float vitesseMax = 25f;              // Vitesse maximale
    [SerializeField] private float acceleration = 2.5f;           // Puissance du moteur
    [SerializeField] private float effetGravite = 5f;             // Impact de l'inclinaison sur la vitesse

    [Header("🎮 Contrôles")]
    [SerializeField] private float vitesseTangage = 100f;         // Sensibilité haut/bas (pitch)
    [SerializeField] private float vitesseRoulis = 120f;          // Sensibilité gauche/droite (roll)
    [SerializeField] private float roulisMax = -1f;               // Angle max du roll (-1 = illimité)
    [SerializeField] private float reactivite = 5f;               // Rapidité de réaction aux inputs (plus élevé = plus nerveux)
    [SerializeField] private float autoStabilisation = 1f;        // Force de remise à plat automatique
    [SerializeField] private float influenceVirage = 20f;         // Influence du roll sur le virage horizontal

    [Header("⚠️ Limites & Décrochage")]
    [SerializeField] private float vitesseDecrochage = 10f;       // Vitesse minimum avant perte de contrôle
    [SerializeField] private float forceDecrochage = 80f;         // Force du piqué en décrochage
    [SerializeField] private float altitudeMax = 175f;            // Altitude limite avant effets
    [SerializeField] private float forceAltitude = 80f;           // Force de piqué à haute altitude

    [Header("🎨 Interface")]
    [SerializeField] private TextMeshProUGUI texteVitesse;
    [SerializeField] private TextMeshProUGUI texteAltitude;

    [Header("✨ Effets Visuels")]
    [SerializeField] private GameObject effetCrash;
    [SerializeField] private GameObject effetCheckpoint;

    [Header("🎯 Éléments de Jeu")]
    [SerializeField] private Compass boussole;

    // ════════════════════════════════════════════════════════════════════════
    // VARIABLES INTERNES - Ne pas modifier directement
    // ════════════════════════════════════════════════════════════════════════

    // Composants
    protected Rigidbody rb;
    protected Gamepad manette;
    protected GameManager gameManager;

    // État du vol
    protected float vitesseActuelle;
    protected bool estCrash = false;

    // Inputs lissés (pour des mouvements fluides)
    protected float inputAcceleration;
    protected float inputTangage;
    protected float inputRoulis;
    protected float tangageLisse;
    protected float roulisLisse;

    // Système de checkpoints
    private List<GameObject> checkpoints;
    private GameObject ligneArrivee;

    // ════════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════════

    protected virtual void Awake()
    {
        // Récupération des composants
        gameManager = FindFirstObjectByType<GameManager>();
        rb = GetComponent<Rigidbody>();

        // Configuration du Rigidbody pour un contrôle physique arcade
        rb.useGravity = false;        // On gère la gravité manuellement pour plus de contrôle
        rb.linearDamping = 0.5f;      // Légère résistance pour un mouvement plus naturel
        rb.angularDamping = 2f;       // Frein de rotation pour éviter les tonneaux infinis

        // Initialisation de la vitesse
        vitesseActuelle = vitesseDepart;

        // Configuration du système de checkpoints
        InitialiserCheckpoints();
    }

    /// <summary>
    /// Récupère et trie tous les checkpoints de la scène
    /// </summary>
    private void InitialiserCheckpoints()
    {
        // Récupérer tous les objets tagués "Checkpoint"
        GameObject[] tousLesCheckpoints = GameObject.FindGameObjectsWithTag("Checkpoint");
        checkpoints = new List<GameObject>();

        // Ne garder que ceux avec un nom numérique (pour le tri)
        foreach (GameObject cp in tousLesCheckpoints)
        {
            if (int.TryParse(cp.name, out _))
            {
                checkpoints.Add(cp);
            }
        }

        // Trier par ordre numérique
        checkpoints.Sort((a, b) => int.Parse(a.name).CompareTo(int.Parse(b.name)));

        // Trouver la ligne d'arrivée
        ligneArrivee = GameObject.FindGameObjectWithTag("Finish");

        // Configurer la boussole pour pointer vers le premier objectif
        MettreAJourBoussole();
    }

    // ════════════════════════════════════════════════════════════════════════
    // BOUCLE PRINCIPALE
    // ════════════════════════════════════════════════════════════════════════

    protected virtual void FixedUpdate()
    {
        if (estCrash) return; // Ne rien faire si l'avion est crashé

        manette = Gamepad.current;

        GererInputs();           // Récupérer les inputs du joueur
        AppliquerPhysique();     // Calculer et appliquer les forces/rotations
        MettreAJourInterface();  // Mettre à jour l'UI
    }

    /// <summary>
    /// Méthode abstraite - Chaque type d'avion gère ses inputs différemment
    /// (Clavier, manette, IA, etc.)
    /// </summary>
    protected abstract void GererInputs();

    // ════════════════════════════════════════════════════════════════════════
    // PHYSIQUE DU VOL
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Applique toute la physique du vol : gravité, rotation, mouvement
    /// C'est ici que le game feel se joue !
    /// </summary>
    protected void AppliquerPhysique()
    {
        // ──────────────────────────────────────────────────────────────────
        // 1. GRAVITÉ DYNAMIQUE - La vitesse change selon l'inclinaison
        // ──────────────────────────────────────────────────────────────────

        // Calculer l'angle de montée/descente (-1 = piqué, +1 = montée)
        float inclinaison = Vector3.Dot(transform.forward, Vector3.up);

        // Plus on monte, plus on perd de vitesse (et vice-versa)
        vitesseActuelle -= inclinaison * effetGravite * Time.fixedDeltaTime;

        // ──────────────────────────────────────────────────────────────────
        // 2. LIMITATION D'ALTITUDE - Effets progressifs en haute altitude
        // ──────────────────────────────────────────────────────────────────

        float altitudeActuelle = transform.position.y;
        float depassementAltitude = Mathf.Max(0, altitudeActuelle - altitudeMax);
        bool tropHaut = depassementAltitude > 0;

        if (tropHaut)
        {
            // Ralentir progressivement si on monte trop haut
            if (vitesseActuelle > 10f && inclinaison >= 0)
            {
                float ralentissement = 2f * (1f + depassementAltitude * 0.1f);
                vitesseActuelle -= ralentissement * Time.fixedDeltaTime;
                vitesseActuelle = Mathf.Max(vitesseActuelle, 10f);
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 3. ROTATION - Tangage (pitch) et Roulis (roll)
        // ──────────────────────────────────────────────────────────────────

        // Lisser les inputs pour des mouvements fluides
        tangageLisse = Mathf.Lerp(tangageLisse, inputTangage, Time.fixedDeltaTime * reactivite);
        roulisLisse = Mathf.Lerp(roulisLisse, inputRoulis, Time.fixedDeltaTime * reactivite);

        // Calculer le roll actuel (converti en range -180° à +180°)
        float roulisActuel = transform.eulerAngles.z;
        if (roulisActuel > 180f) roulisActuel -= 360f;

        // TANGAGE (Pitch) : Monter/Descendre
        // ─────────────────────────────────
        float rotationTangage = tangageLisse * vitesseTangage * Time.fixedDeltaTime;

        // Réduire l'efficacité du tangage si l'avion est très incliné
        float ratioRoulis = Mathf.Abs(roulisActuel) / 90f;
        rotationTangage *= (1f - ratioRoulis * 0.7f);

        // Bloquer la montée si trop haut
        if (tropHaut && tangageLisse > 0)
        {
            rotationTangage *= Mathf.Max(0, 1f - depassementAltitude * 0.05f);
        }

        // DÉCROCHAGE : Forcer le nez vers le bas si trop lent
        if (vitesseActuelle < vitesseDecrochage)
        {
            float intensiteDecrochage = 1f - (vitesseActuelle / vitesseDecrochage);
            rotationTangage += intensiteDecrochage * forceDecrochage * Time.fixedDeltaTime;
        }

        // HAUTE ALTITUDE : Forcer le nez vers le bas si trop haut
        if (tropHaut)
        {
            float intensiteAltitude = Mathf.Min(depassementAltitude * 0.05f, 3f);
            rotationTangage += intensiteAltitude * forceAltitude * Time.fixedDeltaTime;
        }

        // ROULIS (Roll) : Incliner gauche/droite
        // ───────────────────────────────────────
        float rotationRoulis = roulisLisse * vitesseRoulis * Time.fixedDeltaTime;

        // Auto-stabilisation : Retour automatique à plat quand pas d'input
        if (Mathf.Abs(inputRoulis) <= 0.2f)
        {
            float forceStabilisation = autoStabilisation;

            // Augmenter la stabilisation en altitude pour rendre le vol plus difficile
            if (tropHaut)
            {
                forceStabilisation *= (1f + depassementAltitude * 0.1f);
            }

            rotationRoulis -= roulisActuel * forceStabilisation * Time.fixedDeltaTime;
        }

        // Limiter le roulis maximum si activé (roulisMax >= 0)
        if (roulisMax >= 0f)
        {
            // Calculer le roulis potentiel après rotation
            Quaternion rotationPotentielle = rb.rotation * Quaternion.Euler(0f, 0f, rotationRoulis);
            float roulisPotentiel = rotationPotentielle.eulerAngles.z;
            if (roulisPotentiel > 180f) roulisPotentiel -= 360f;

            // Bloquer si on dépasse la limite
            if (Mathf.Abs(roulisPotentiel) > roulisMax)
            {
                float roulisCible = Mathf.Sign(roulisPotentiel) * roulisMax;
                rotationRoulis = roulisCible - roulisActuel;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // 4. VIRAGE HORIZONTAL - Le roll influence le yaw (réalisme arcade)
        // ──────────────────────────────────────────────────────────────────

        // Plus l'avion est incliné, plus il tourne
        float facteurVirage = (ratioRoulis * ratioRoulis); // Courbe exponentielle pour plus de contrôle

        // Plus l'avion va vite, plus il tourne facilement
        float facteurVitesse = 1f + (vitesseActuelle / vitesseMax) * 2f;

        // Calculer le virage horizontal (yaw global)
        float virageHorizontal = -Mathf.Sign(roulisActuel) * facteurVirage * influenceVirage * facteurVitesse * Time.fixedDeltaTime;

        // ──────────────────────────────────────────────────────────────────
        // 5. APPLIQUER LES ROTATIONS
        // ──────────────────────────────────────────────────────────────────

        // D'abord : Rotations locales (pitch et roll)
        Quaternion rotationLocale = Quaternion.Euler(rotationTangage, 0f, rotationRoulis);
        rb.MoveRotation(rb.rotation * rotationLocale);

        // Ensuite : Rotation globale (yaw autour de l'axe Y du monde)
        Quaternion rotationGlobale = Quaternion.Euler(0f, virageHorizontal, 0f);
        rb.MoveRotation(rotationGlobale * rb.rotation);

        // ──────────────────────────────────────────────────────────────────
        // 6. APPLIQUER LE MOUVEMENT
        // ──────────────────────────────────────────────────────────────────

        // Limiter la vitesse
        float vitesseMin = tropHaut ? 0f : -vitesseMax; // Pas de marche arrière en altitude
        vitesseActuelle = Mathf.Clamp(vitesseActuelle, vitesseMin, vitesseMax * 1.5f);

        // Déplacer l'avion dans la direction où il pointe
        rb.linearVelocity = transform.forward * vitesseActuelle;
    }

    // ════════════════════════════════════════════════════════════════════════
    // INTERFACE UTILISATEUR
    // ════════════════════════════════════════════════════════════════════════

    protected void MettreAJourInterface()
    {
        if (texteVitesse != null)
        {
            texteVitesse.text = $"{Mathf.RoundToInt(vitesseActuelle * 10)} km/h";
        }

        if (texteAltitude != null)
        {
            int altitudeAffichage = (Mathf.RoundToInt(transform.position.y) * 4) - 550;
            int altitudeMaxAffichage = (Mathf.RoundToInt(altitudeMax) * 4) - 550;
            texteAltitude.text = $"Altitude: {altitudeAffichage}m\n{altitudeMaxAffichage}m max";
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // GESTION DES COLLISIONS & TRIGGERS
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Collision = CRASH !
    /// </summary>
    protected virtual void OnCollisionEnter(Collision collision)
    {
        if (estCrash) return;
        estCrash = true;

        // Sauvegarder la défaite
        PlayerPrefs.SetString("gamestate", "lose");
        PlayerPrefs.Save();

        // Effet visuel de crash
        if (effetCrash != null)
        {
            Instantiate(effetCrash, transform.position, Quaternion.identity);
        }

        // Vibration de la manette
        if (manette != null)
        {
            StartCoroutine(VibrationCrash());
        }

        // Notifier le GameManager
        if (gameManager != null)
        {
            StartCoroutine(gameManager.CrashSequence());
        }

        // Désactiver visuellement l'avion
        foreach (MeshRenderer mr in GetComponentsInChildren<MeshRenderer>())
        {
            mr.enabled = false;
        }

        // Stopper toute physique
        foreach (Rigidbody r in GetComponentsInChildren<Rigidbody>())
        {
            r.isKinematic = true;
        }

        // Désactiver les collisions
        foreach (Collider c in GetComponentsInChildren<Collider>())
        {
            c.enabled = false;
        }
    }

    /// <summary>
    /// Vibration intense lors du crash
    /// </summary>
    protected IEnumerator VibrationCrash()
    {
        manette.SetMotorSpeeds(1f, 1f);
        yield return new WaitForSeconds(2f);
        manette.SetMotorSpeeds(0f, 0f);
    }

    /// <summary>
    /// Passage dans un checkpoint ou la ligne d'arrivée
    /// </summary>
    protected virtual void OnTriggerEnter(Collider other)
    {
        // ──────────────────────────────────────────────────────────────────
        // CHECKPOINT VALIDÉ
        // ──────────────────────────────────────────────────────────────────
        if (other.gameObject.CompareTag("Checkpoint"))
        {
            // Effet visuel
            if (effetCheckpoint != null)
            {
                Instantiate(effetCheckpoint, transform);
            }

            // Retirer le checkpoint de la liste
            GameObject checkpointValide = other.gameObject;
            if (checkpoints.Contains(checkpointValide))
            {
                checkpoints.Remove(checkpointValide);
            }

            // Détruire le checkpoint
            Destroy(checkpointValide);

            // Si c'était le dernier, victoire !
            if (checkpoints.Count == 0)
            {
                PlayerPrefs.SetString("gamestate", "win");
                PlayerPrefs.Save();

                if (gameManager != null)
                {
                    gameManager.WinGame();
                }
            }

            // Mettre à jour la boussole
            MettreAJourBoussole();
        }

        // ──────────────────────────────────────────────────────────────────
        // LIGNE D'ARRIVÉE (seulement si tous les checkpoints sont validés)
        // ──────────────────────────────────────────────────────────────────
        if (other.gameObject.CompareTag("Finish") && checkpoints.Count == 0)
        {
            PlayerPrefs.SetString("gamestate", "win");
            PlayerPrefs.Save();

            if (gameManager != null)
            {
                gameManager.WinGame();
            }
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // SYSTÈME DE BOUSSOLE
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Met à jour la cible de la boussole (prochain checkpoint ou arrivée)
    /// </summary>
    private void MettreAJourBoussole()
    {
        if (boussole == null) return;

        // Priorité 1 : Premier checkpoint
        if (checkpoints != null && checkpoints.Count > 0)
        {
            boussole.SetTarget(checkpoints[0]);
            boussole.gameObject.SetActive(true);

            // Activer uniquement le premier checkpoint, désactiver les autres
            for (int i = 0; i < checkpoints.Count; i++)
            {
                if (checkpoints[i] != null)
                {
                    checkpoints[i].SetActive(i == 0);
                }
            }
        }
        // Priorité 2 : Ligne d'arrivée
        else if (ligneArrivee != null)
        {
            boussole.SetTarget(ligneArrivee);
            boussole.gameObject.SetActive(true);
        }
        // Pas d'objectif : désactiver la boussole
        else
        {
            boussole.gameObject.SetActive(false);
        }
    }

    // ════════════════════════════════════════════════════════════════════════
    // PROPRIÉTÉS ACCESSIBLES (pour les classes dérivées)
    // ════════════════════════════════════════════════════════════════════════

    protected float Acceleration => acceleration;
    protected float VitesseMax => vitesseMax;
    protected float VitesseDepart => vitesseDepart;
    protected float EffetGravite => effetGravite;
    protected float VitesseTangage => vitesseTangage;
    protected float VitesseRoulis => vitesseRoulis;
    protected float Reactivite => reactivite;
    protected float AutoStabilisation => autoStabilisation;
    protected float InfluenceVirage => influenceVirage;
    protected float VitesseDecrochage => vitesseDecrochage;
}
