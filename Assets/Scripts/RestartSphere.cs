using UnityEngine;

public class RestartButton : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    private void Restart() {
        UnityEngine.SceneManagement.SceneManager.LoadScene(
            UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
    }
    
    private void OnTriggerEnter(Collider other) {
        Restart();
    }
}
