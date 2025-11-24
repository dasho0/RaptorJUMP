using System;
using UnityEngine;

public class ClimbProvider : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField] private ClimbCollider leftClimbCollider; 
    [SerializeField] private ClimbCollider rightClimbCollider; 
    
    void Start()
    {
        
    }

    private void OnEnable() {
        leftClimbCollider.onClimbStarted += HandleClimbStarted;
        rightClimbCollider.onClimbStarted += HandleClimbStarted;
        
        leftClimbCollider.onClimbEnded += HandleClimbEnded;
        rightClimbCollider.onClimbEnded += HandleClimbEnded;
    }

    private void OnDisable() {
        leftClimbCollider.onClimbStarted -= HandleClimbStarted;
        rightClimbCollider.onClimbStarted -= HandleClimbStarted;
        
        leftClimbCollider.onClimbEnded -= HandleClimbEnded;
        rightClimbCollider.onClimbEnded -= HandleClimbEnded;
    }

    private void HandleClimbStarted(Hand hand) {
        
    }
    
    private void HandleClimbEnded(Hand hand) {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
