using UnityEngine;

public class FinalMenu : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created

    [SerializeField] private GameObject leftHandLaser;
    [SerializeField] private GameObject rightHandLaser;

    private void Start()
    {
        gameObject.SetActive(false);
        leftHandLaser.SetActive(false);
        rightHandLaser.SetActive(false);
    }

    public void Show()
    {
        gameObject.SetActive(true);
        leftHandLaser.SetActive(true);
        rightHandLaser.SetActive(true);
    }
}
