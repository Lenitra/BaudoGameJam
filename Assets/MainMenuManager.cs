using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{

    [SerializeField] private Button startButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button changelvlButton;
    [SerializeField] private TMPro.TextMeshProUGUI modalMessage;


    [SerializeField] private GameObject keyboardControllerUI;
    [SerializeField] private GameObject gamepadControllerUI;

    [SerializeField] private Button switchInputButton;

    void Start()
    {
        switchInputButton.onClick.AddListener(SwitchInputMethod);
        startButton.onClick.AddListener(StartGame);

        restartButton.onClick.AddListener(StartGame);

        changelvlButton.onClick.AddListener(DesactivateModale);

        ShowModale();

        if (!PlayerPrefs.HasKey("InputMethod"))
        {
            PlayerPrefs.SetString("InputMethod", "Keyboard");
        }

        ApplyGoodObject();
    }


    private void DesactivateModale()
    {
        changelvlButton.transform.parent.gameObject.SetActive(false);
    }

    private void ShowModale()
    {
        if (PlayerPrefs.HasKey("gamestate"))
        {
            string gameState = PlayerPrefs.GetString("gamestate");

            if (gameState == "win")
            {
                modalMessage.text = "Gagné !";
                changelvlButton.transform.parent.gameObject.SetActive(true);
            }
            else if (gameState == "lose")
            {
                modalMessage.text = "Perdu !";
                changelvlButton.transform.parent.gameObject.SetActive(true);
            }

            PlayerPrefs.DeleteKey("gamestate");
            PlayerPrefs.Save();
        }
        else
        {
            DesactivateModale();
        }
    }

    private void SwitchInputMethod()
    {
        // Determine new input method (toggle)
        string newInputMethod = keyboardControllerUI.activeSelf ? "Gamepad" : "Keyboard";

        // Keep keyboard if trying to switch to gamepad but no gamepad connected
        if (newInputMethod == "Gamepad" && Gamepad.current == null)
        {
            newInputMethod = "Keyboard";
        }

        // Save and apply the new input method
        PlayerPrefs.SetString("InputMethod", newInputMethod);
        PlayerPrefs.Save();

        ApplyGoodObject();

    }


    private void ApplyGoodObject()
    {

        bool showGamepad = PlayerPrefs.GetString("InputMethod") == "Gamepad";

        keyboardControllerUI.SetActive(!showGamepad);
        gamepadControllerUI.SetActive(showGamepad);
    }


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
