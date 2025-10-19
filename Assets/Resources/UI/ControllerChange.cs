using UnityEngine;
using UnityEngine.UI;

public class ControllerChange : MonoBehaviour
{
    [SerializeField] private GameObject keyboardControllerUI;
    [SerializeField] private GameObject gamepadControllerUI;

    private Button switchInputButton;

    void Start()
    {
        switchInputButton = GetComponent<Button>();
        switchInputButton.onClick.AddListener(SwitchInputMethod);
        
        if (!PlayerPrefs.HasKey("InputMethod"))
        {
            PlayerPrefs.SetString("InputMethod", "Keyboard");
        }

        ApplyGoodObject();
    }

    private void SwitchInputMethod()
    {
        bool isKeyboardActive = keyboardControllerUI.activeSelf;

        keyboardControllerUI.SetActive(!isKeyboardActive);
        gamepadControllerUI.SetActive(isKeyboardActive);
        PlayerPrefs.SetString("InputMethod", isKeyboardActive ? "Gamepad" : "Keyboard");
        PlayerPrefs.Save();
        ApplyGoodObject();
    }


    private void ApplyGoodObject()
    {
        string savedInputMethod = PlayerPrefs.GetString("InputMethod");
        if (savedInputMethod == "Keyboard")
        {
            keyboardControllerUI.SetActive(true);
            gamepadControllerUI.SetActive(false);
        }
        else
        {
            keyboardControllerUI.SetActive(false);
            gamepadControllerUI.SetActive(true);
        }
    }




}
