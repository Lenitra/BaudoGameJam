using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Contrôle de l'avion à la MANETTE (Gamepad)
/// Utilise les triggers et le stick gauche pour un contrôle fluide et arcade
/// </summary>
public class PlaneControler : PlaneBase
{
    // ════════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════════

    protected override void Awake()
    {
        // Vérifier que le joueur a choisi la manette
        if (PlayerPrefs.GetString("InputMethod") != "Gamepad")
        {
            this.enabled = false;
            return;
        }

        base.Awake();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GESTION DES INPUTS MANETTE
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lit les entrées de la manette et les assigne aux variables de PlaneBase
    ///
    /// CONTRÔLES:
    /// - Stick gauche: Tangage (Y) et Roulis (X)
    /// - Trigger droit (RT): Accélérer
    /// - Trigger gauche (LT): Freiner
    /// </summary>
    protected override void GererInputs()
    {
        // Si pas de manette connectée, ne rien faire
        if (manette == null) return;

        // ──────────────────────────────────────────────────────────────────
        // 1. ACCÉLÉRATION / FREINAGE (Triggers)
        // ──────────────────────────────────────────────────────────────────

        float accelerer = manette.rightTrigger.ReadValue();   // RT = Gaz
        float freiner = manette.leftTrigger.ReadValue();      // LT = Frein

        // Bloquer le frein si on est déjà trop lent (évite de reculer involontairement)
        if (vitesseActuelle < VitesseDecrochage)
        {
            freiner = 0f;
        }

        // Bloquer l'accélération en haute altitude (sauf si on pique)
        float altitudeActuelle = transform.position.y;
        if (altitudeActuelle > 175f) // Utiliser la valeur hardcodée car altitudeMax n'est pas accessible
        {
            // Vérifier si l'avion pique vers le bas
            float inclinaison = Vector3.Dot(transform.forward, Vector3.up);

            // Si on ne pique pas assez, bloquer l'accélération
            if (inclinaison >= -0.2f)
            {
                accelerer = 0f;
            }
        }

        // Calculer l'input d'accélération final (-1 à +1)
        inputAcceleration = accelerer - freiner;

        // Appliquer l'accélération à la vitesse
        vitesseActuelle += inputAcceleration * Acceleration * Time.fixedDeltaTime;

        // ──────────────────────────────────────────────────────────────────
        // 2. TANGAGE & ROULIS (Stick gauche)
        // ──────────────────────────────────────────────────────────────────

        Vector2 stick = manette.leftStick.ReadValue();

        // Stick Y = Tangage (monter/descendre)
        // Stick X = Roulis (incliner gauche/droite)
        inputTangage = stick.y;        // Haut = +1, Bas = -1
        inputRoulis = -stick.x;        // Gauche = +1, Droite = -1 (inversé pour être plus intuitif)

        // Note: Le lissage est géré dans ApplyPhysique() de PlaneBase
    }
}
