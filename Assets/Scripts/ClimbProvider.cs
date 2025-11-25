using System;
using UnityEngine;

public class ClimbProvider : MonoBehaviour
{
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	[SerializeField] private ClimbCollider leftClimbCollider; 
	[SerializeField] private ClimbCollider rightClimbCollider;
	[SerializeField] private Renderer leftHandRenderer;
	[SerializeField] private Renderer rightHandRenderer;
    
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
		leftHandRenderer.material.color = Color.blue;	
		rightHandRenderer.material.color = Color.blue;	
	}
    
	private void HandleClimbEnded(Hand hand) {
		leftHandRenderer.material.color = Color.darkRed;	
		rightHandRenderer.material.color = Color.darkRed;	
	}

	// Update is called once per frame
	void Update()
	{
        
	}
}
