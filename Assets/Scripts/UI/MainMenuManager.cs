using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    public void IniciarJuego()
    {
        SceneManager.LoadScene("Main"); 
    }

    public void SalirJuego()
    {
        Debug.Log("Saliendo del juego");
        Application.Quit();
    }
}