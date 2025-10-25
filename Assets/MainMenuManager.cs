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

    [SerializeField] private Button nextLvl;
    [SerializeField] private GameObject lvlInfosParent;
    [SerializeField] private Rotate planeRotation;

    private int currentLvlIndex = 0;

    void Start()
    {
        switchInputButton.onClick.AddListener(SwitchInputMethod);
        startButton.onClick.AddListener(StartGame);

        restartButton.onClick.AddListener(StartGame);

        changelvlButton.onClick.AddListener(DesactivateModale);

        nextLvl.onClick.AddListener(NextLevel);

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

        // Réactiver la rotation de l'avion quand la modale est fermée
        if (planeRotation != null)
        {
            planeRotation.gameObject.SetActive(true);
        }
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

                // Désactiver la rotation de l'avion quand la modale est active
                if (planeRotation != null)
                {
                    planeRotation.gameObject.SetActive(false);
                }
            }
            else if (gameState == "lose")
            {
                modalMessage.text = "Perdu !";
                changelvlButton.transform.parent.gameObject.SetActive(true);

                // Désactiver la rotation de l'avion quand la modale est active
                if (planeRotation != null)
                {
                    planeRotation.gameObject.SetActive(false);
                }
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
        // Détecter automatiquement si une manette est connectée et basculer par défaut
        if (Gamepad.current != null && PlayerPrefs.GetString("InputMethod") != "Gamepad")
        {
            PlayerPrefs.SetString("InputMethod", "Gamepad");
            PlayerPrefs.Save();
            ApplyGoodObject();
        }

        // Détecter automatiquement l'input de la manette et basculer vers le mode manette
        if (Gamepad.current != null)
        {
            // Vérifier si un bouton quelconque est pressé
            if (Gamepad.current.buttonSouth.wasPressedThisFrame ||
                Gamepad.current.buttonNorth.wasPressedThisFrame ||
                Gamepad.current.buttonEast.wasPressedThisFrame ||
                Gamepad.current.buttonWest.wasPressedThisFrame ||
                Gamepad.current.startButton.wasPressedThisFrame ||
                Gamepad.current.leftShoulder.wasPressedThisFrame ||
                Gamepad.current.rightShoulder.wasPressedThisFrame ||
                Gamepad.current.leftTrigger.wasPressedThisFrame ||
                Gamepad.current.rightTrigger.wasPressedThisFrame ||
                Gamepad.current.dpad.up.wasPressedThisFrame ||
                Gamepad.current.dpad.down.wasPressedThisFrame ||
                Gamepad.current.dpad.left.wasPressedThisFrame ||
                Gamepad.current.dpad.right.wasPressedThisFrame)
            {
                StartGame();
            }
        }

        // Vérifier si n'importe quelle touche du clavier est pressée
        if (Keyboard.current != null && Keyboard.current.anyKey.wasPressedThisFrame)
        {
            // Basculer automatiquement vers les contrôles clavier
            if (PlayerPrefs.GetString("InputMethod") != "Keyboard")
            {
                PlayerPrefs.SetString("InputMethod", "Keyboard");
                PlayerPrefs.Save();
                ApplyGoodObject();
            }
            StartGame();
        }
    }

    public void StartGame()
    {
        string sceneName = "game" + currentLvlIndex;
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }

    private void NextLevel()
    {
        if (lvlInfosParent == null || lvlInfosParent.transform.childCount == 0)
            return;

        // Désactiver l'enfant actuel
        lvlInfosParent.transform.GetChild(currentLvlIndex).gameObject.SetActive(false);

        // Passer à l'enfant suivant (retour au début si on est sur le dernier)
        currentLvlIndex = (currentLvlIndex + 1) % lvlInfosParent.transform.childCount;

        // Activer le nouvel enfant
        lvlInfosParent.transform.GetChild(currentLvlIndex).gameObject.SetActive(true);

        planeRotation.OneFullRotate();
    }
}
