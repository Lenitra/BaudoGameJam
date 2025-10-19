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
        currentSpeed = Mathf.Clamp(currentSpeed, -MaxSpeed, MaxSpeed); // Vitesse peut être négative
    }
}
