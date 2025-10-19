using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;

public class MainMenuManager : MonoBehaviour
{
    void Update()
    {
        // Vérifier si n'importe quelle touche du clavier est pressée
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            StartGame();
        }

        // Vérifier si n'importe quel bouton de la manette est pressé
        if (Gamepad.current != null && Gamepad.current.wasUpdatedThisFrame)
        {
            // Vérifier si un bouton quelconque est pressé
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonNorth.wasPressedThisFrame ||
                Gamepad.current.buttonEast.wasPressedThisFrame ||
                Gamepad.current.buttonWest.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame)
            {
                StartGame();
            }
        }
    }

    public void StartGame()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Game");
    }
}
