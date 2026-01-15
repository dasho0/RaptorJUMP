using UnityEngine;

public class FinalMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    private void Start()
    {
        gameObject.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
    }
}
