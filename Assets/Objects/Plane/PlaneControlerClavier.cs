using UnityEngine;
using UnityEngine.InputSystem;

public class PlaneControlerClavier : PlaneBase
{
    protected override void Awake()
    {
        // get the playerprefs to know which controller to use
        if (PlayerPrefs.GetString("InputMethod") != "Keyboard")
        {
            this.enabled = false;
            return;
        }

        base.Awake();
    }

    // ------------------------------------------------------------
    // Lecture des entrees clavier
    // ------------------------------------------------------------
    protected override void HandleInput()
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
        smoothPitch = Mathf.Lerp(smoothPitch, targetPitch, Time.fixedDeltaTime * ControlInertia);
        smoothRoll = Mathf.Lerp(smoothRoll, targetRoll, Time.fixedDeltaTime * ControlInertia);

        pitchInput = smoothPitch;
        rollInput = smoothRoll;

        // Ajuster la vitesse
        currentSpeed += throttleInput * Acceleration * Time.fixedDeltaTime;
        currentSpeed = Mathf.Clamp(currentSpeed, MinSpeed, MaxSpeed);
    }
}
