// Assets/Scripts/SpeedIndicator.cs
using TMPro;
using UnityEngine;
public class SpeedIndicator : MonoBehaviour
{
    [SerializeField] private TMP_Text speedText;
    [SerializeField] private CharacterController playerController;
    [SerializeField] private bool ignoreVerticalSpeed = true;
    [SerializeField] private float zeroThreshold = 0.02f;

    private Vector3 lastPosition;

    private void Start() {
        lastPosition = playerController.transform.position;
    }

    private void LateUpdate() {
        var currentPos = playerController.transform.position;
        var dt = Time.deltaTime;
        var velocity = (currentPos - lastPosition) / dt;

        if(ignoreVerticalSpeed) velocity.y = 0f;

        var speed = velocity.magnitude;
        if(speed < zeroThreshold) speed = 0f;

        speedText.text = $"{speed:F2} m/s";
        lastPosition = currentPos;
    }
}
