using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Références")]
    [SerializeField] private PlaneControler planeControler;

    void Start()
    {
        planeControler = FindFirstObjectByType<PlaneControler>();
    }

    public void WinGame()
    {
        Debug.Log("Bien joué ! Vous avez terminé le jeu !");
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }

    public void LooseGame()
    {
        Debug.Log("Game Over ! Vous vous êtes écrasé !");
        // Revenir sur une autre scene
        UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenu");
    }








}
