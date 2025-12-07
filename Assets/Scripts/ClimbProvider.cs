using System;
using UnityEngine;

public class ClimbProvider : MonoBehaviour {
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	[SerializeField] private ClimbCollider leftClimbCollider; 
	[SerializeField] private ClimbCollider rightClimbCollider;
	[SerializeField] private Renderer leftHandRenderer;
	[SerializeField] private Renderer rightHandRenderer;
	
	[SerializeField] private float handLength = 1f;

	private record HandInfo(Renderer Renderer, ClimbCollider ClimbCollider);
	private readonly struct Hands {
		private readonly HandInfo _left;
		private readonly HandInfo _right;

		public Hands(HandInfo left, HandInfo right) {
			_left = left;
			_right = right;
		}
		public HandInfo Get(Hand hand) {
			return hand == Hand.LEFT ? _left : _right;
		}
		
	}

	private Hands _hands;
    
	void Start() {
		_hands = new Hands(
		new HandInfo(leftHandRenderer, leftClimbCollider),
		new HandInfo(rightHandRenderer, rightClimbCollider)
		);
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
		_hands.Get(hand).Renderer.material.color = Color.darkRed;
	}
    
	private void HandleClimbEnded(Hand hand) {
		_hands.Get(hand).Renderer.material.color = Color.deepSkyBlue;
	}

	// Update is called once per frame
	void Update()
	{
        
	}
}
