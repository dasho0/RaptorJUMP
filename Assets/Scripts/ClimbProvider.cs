using System;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;

public class ClimbProvider : MonoBehaviour {
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	[SerializeField] private ClimbCollider leftClimbCollider; 
	[SerializeField] private ClimbCollider rightClimbCollider;
	
	[SerializeField] private Renderer leftHandRenderer;
	[SerializeField] private Renderer rightHandRenderer;
	
	[SerializeField] private Transform leftHandTransform;
	[SerializeField] private Transform rightHandTransform;
	
	[SerializeField] private CharacterController playerCharacterController;
	
	[SerializeField] private float handLength = 1f;

	private record HandInfo(Renderer Renderer, ClimbCollider ClimbCollider);
	private class Hands {
		private readonly HandInfo _left;
		private readonly HandInfo _right;

		private Transform _leftTransform;
		private Transform _rightTransform;
		
		private Vector3 _storedLeftPosition;
		private Vector3 _storedRightPosition;

		public Hands(HandInfo left, HandInfo right, Transform leftTransform, Transform rightTransform) {
			_left = left;
			_right = right;
			
			_leftTransform = leftTransform;
			_rightTransform = rightTransform;

			_storedLeftPosition = _leftTransform.position;
			_storedRightPosition = _rightTransform.position;
		}
		public HandInfo Get(Hand hand) {
			return hand == Hand.LEFT ? _left : _right;
		}
		
		public Transform GetTransform(Hand hand) {
			return hand == Hand.LEFT ? _leftTransform : _rightTransform;
		}
		
		public Vector3 GetStoredPosition(Hand hand) {
			return hand == Hand.LEFT ? _storedLeftPosition : _storedRightPosition;
		}

		public void StorePosition(Hand hand) {
			if(hand == Hand.LEFT) {
				_storedLeftPosition = _leftTransform.position;
			} else {
				_storedRightPosition = _rightTransform.position;
			}
		}
		
	}

	private Hands _hands;
    private Dictionary<Hand, bool> _isClimbing = new Dictionary<Hand, bool> {
		{ Hand.LEFT, false },
		{ Hand.RIGHT, false },
	};
	void Start() {
		_hands = new Hands(
			new HandInfo(leftHandRenderer, leftClimbCollider),
			new HandInfo(rightHandRenderer, rightClimbCollider),
			leftHandTransform,
			rightHandTransform
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
		var handInfo = _hands.Get(hand);
		handInfo.Renderer.material.color = Color.darkRed;

		_isClimbing[hand] = true;
		_hands.StorePosition(hand);
	}
    
	private void HandleClimbEnded(Hand hand) {
		var handInfo = _hands.Get(hand);
		handInfo.Renderer.material.color = Color.deepSkyBlue;

		_isClimbing[hand] = false;
	}

	// Update is called once per frame
	private void Update() {
		foreach(var (hand, climbing) in _isClimbing) {
			if(climbing) {
				// Debug.Log($"Hand: {hand} is attached at position {_hands.GetStoredPosition(hand)}");
				// _hands.GetTransform(hand).position = _hands.GetStoredPosition(hand);
			}
		}	
	}
}
