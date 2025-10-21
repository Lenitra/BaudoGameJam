using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneControler : PlaneBase
{
    protected override void Awake()
    {
        // get the playerprefs to know which controller to use
        if (PlayerPrefs.GetString("InputMethod") != "Gamepad")
        {
            this.enabled = false;
            return;
        }

        base.Awake();
    }

    // ------------------------------------------------------------
    // Lecture des entrées manette
    // ------------------------------------------------------------
    protected override void HandleInput()
    {
        if (gamepad == null) return;

        // Trigger droit = accélérer, gauche = ralentir
        float accelTrigger = gamepad.rightTrigger.ReadValue();
        float brakeTrigger = gamepad.leftTrigger.ReadValue();

        // Désactiver le frein si la vitesse est inférieure à stallSpeed
        if (currentSpeed < StallSpeed)
        {
            brakeTrigger = 0f;
        }

        // Bloquer l'accélération si au-dessus de la limite d'altitude
        // SAUF si l'avion pointe vers le bas (en piqué)
        if (isAboveAltitudeLimit)
        {
            // Vérifier si l'avion pointe vers le bas
            float incline = Vector3.Dot(transform.forward, Vector3.up);
            // incline < 0 = pique vers le bas, incline > 0 = monte

            // Bloquer l'accélération seulement si on ne pique pas
            if (incline >= -0.2f) // Seuil de -0.2 pour permettre un peu de tolérance
            {
                accelTrigger = 0f;
            }
        }

        throttleInput = accelTrigger - brakeTrigger;

        // Stick gauche = pitch / roll
        Vector2 stick = gamepad.leftStick.ReadValue();
        float targetPitch = stick.y;
        float targetRoll = -stick.x;

        // Appliquer l'inertie aux contrôles (Lerp progressif)
        smoothPitch = Mathf.Lerp(smoothPitch, targetPitch, Time.fixedDeltaTime * ControlInertia);
        smoothRoll = Mathf.Lerp(smoothRoll, targetRoll, Time.fixedDeltaTime * ControlInertia);

        pitchInput = smoothPitch;
        rollInput = smoothRoll;

        // Ajuster la vitesse selon les triggers
        currentSpeed += throttleInput * Acceleration * Time.fixedDeltaTime;
        // Note: Le clamp de vitesse est fait dans ApplyMovement() de la classe parente
    }
}
