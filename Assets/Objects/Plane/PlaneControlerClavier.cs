using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Contrôle de l'avion au CLAVIER
/// Utilise WASD/ZQSD pour le vol et Espace pour accélérer
/// </summary>
public class PlaneControlerClavier : PlaneBase
{
    // ════════════════════════════════════════════════════════════════════════
    // INITIALISATION
    // ════════════════════════════════════════════════════════════════════════

    protected override void Awake()
    {
        // Vérifier que le joueur a choisi le clavier
        if (PlayerPrefs.GetString("InputMethod") != "Keyboard")
        {
            this.enabled = false;
            return;
        }

        base.Awake();
    }

    // ════════════════════════════════════════════════════════════════════════
    // GESTION DES INPUTS CLAVIER
    // ════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Lit les entrées du clavier et les assigne aux variables de PlaneBase
    ///
    /// CONTRÔLES:
    /// - W/Z : Tangage vers le haut (monter)
    /// - S   : Tangage vers le bas (descendre)
    /// - A/Q : Roulis vers la gauche
    /// - D   : Roulis vers la droite
    /// - Espace : Accélérer
    /// </summary>
    protected override void GererInputs()
    {
        var clavier = Keyboard.current;

        // Si pas de clavier détecté, ne rien faire
        if (clavier == null) return;

        // ──────────────────────────────────────────────────────────────────
        // 1. TANGAGE (Monter/Descendre)
        // ──────────────────────────────────────────────────────────────────

        float tangageCible = 0f;

        // W ou Z = Monter
        if (clavier.wKey.isPressed || clavier.zKey.isPressed)
        {
            tangageCible = 1f;
        }

        // S = Descendre
        if (clavier.sKey.isPressed)
        {
            tangageCible = -1f;
        }

        inputTangage = tangageCible;

        // ──────────────────────────────────────────────────────────────────
        // 2. ROULIS (Incliner gauche/droite)
        // ──────────────────────────────────────────────────────────────────

        float roulisCible = 0f;

        // A ou Q = Incliner à gauche
        if (clavier.aKey.isPressed || clavier.qKey.isPressed)
        {
            roulisCible = 1f;
        }

        // D = Incliner à droite
        if (clavier.dKey.isPressed)
        {
            roulisCible = -1f;
        }

        inputRoulis = roulisCible;

        // ──────────────────────────────────────────────────────────────────
        // 3. ACCÉLÉRATION
        // ──────────────────────────────────────────────────────────────────

        float accelerationCible = 0f;

        // Espace = Accélérer
        if (clavier.spaceKey.isPressed)
        {
            accelerationCible = 1f;
        }

        inputAcceleration = accelerationCible;

        // Appliquer l'accélération à la vitesse
        vitesseActuelle += inputAcceleration * Acceleration * Time.fixedDeltaTime;

        // Limiter la vitesse pour le clavier (pas de marche arrière)
        vitesseActuelle = Mathf.Clamp(vitesseActuelle, VitesseDepart, VitesseMax);

        // Note: Le lissage des inputs est géré dans ApplyPhysique() de PlaneBase
    }
}
