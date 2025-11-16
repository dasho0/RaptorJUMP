using UnityEngine;

public class FollowCcontroller : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        this.transform.position = controller.transform.position;
    }
}
