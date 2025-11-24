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
        if(!cc.isGrounded) {
            Debug.Log("hit wall while in mid air");
        }
        accelerationMoveProvider.ProcessWallCollision(hit);
    }
}
