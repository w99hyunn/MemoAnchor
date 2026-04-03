using UnityEngine;
using UnityEngine.SceneManagement;

public class HomeButton : MonoBehaviour
{
    public void GoToHome()
    {
        SceneManager.LoadScene("Home");
    }
}