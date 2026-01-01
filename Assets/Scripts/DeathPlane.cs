using UnityEngine;

public class DeathPlane : MonoBehaviour
{
    [SerializeField] private CheckpointManager checkpointManager;
    
    private void OnTriggerEnter(Collider other)
    {
        checkpointManager.Respawn();
    }
}
