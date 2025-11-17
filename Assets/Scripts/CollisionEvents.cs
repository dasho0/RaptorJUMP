using UnityEngine;
public class PlayerCollisionEvents : MonoBehaviour
{
    private CharacterController cc;

    [SerializeField]
    private AccelerationMoveProvider accelerationMoveProvider;

    void Awake()
    {
        cc = GetComponent<CharacterController>();
    }

    void OnControllerColliderHit(ControllerColliderHit hit)
    {
        accelerationMoveProvider.ProcessWallCollision(hit);
    }
}
