using System;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

public class ClimbProvider : MonoBehaviour {
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	[SerializeField] private ClimbCollider leftClimbCollider; 
	[SerializeField] private ClimbCollider rightClimbCollider;
	
	[SerializeField] private Renderer leftHandRenderer;
	[SerializeField] private Renderer rightHandRenderer;
	
	[SerializeField] private Transform leftHandTransform;
	[SerializeField] private Transform rightHandTransform;
	
	[SerializeField] private CharacterController playerCharacterController;
	[SerializeField] private AccelerationMoveProvider accelerationMoveProvider;
	[SerializeField] private GravityProvider gravityProvider;
	
	[SerializeField] private float handLength = 1f;

	private class HandInfo {
		public readonly Renderer Renderer;
		public readonly ClimbCollider ClimbCollider;
		public Vector3 PreviousPosition;
		public Vector3 CurrentPosition;
		public Vector3 StoredPosition;

		public HandInfo(Renderer renderer, ClimbCollider climbCollider, Vector3 position) {
			Renderer = renderer;
			ClimbCollider = climbCollider;
			PreviousPosition = position;
			CurrentPosition = position;
			StoredPosition = Vector3.zero;
		}
	};
	
	private class Hands {
		private readonly HandInfo _left;
		private readonly HandInfo _right;
		private readonly Transform _leftHandTransform;
		private readonly Transform _rightHandTransform;

		public Hands(HandInfo left, HandInfo right, Transform leftHandTransform, Transform rightHandTransform) {
			_left = left;
			_right = right;
			
			_leftHandTransform = leftHandTransform;
			_rightHandTransform = rightHandTransform;
		}
		public HandInfo Get(Hand hand) {
			return hand == Hand.LEFT ? _left : _right;
		}
		
		public void StorePosition(Hand hand) {
			if(hand == Hand.LEFT) {
				_left.StoredPosition = _leftHandTransform.position;
				_left.PreviousPosition = _leftHandTransform.position;
				_left.CurrentPosition = _leftHandTransform.position;
			} else {
				_right.StoredPosition = _rightHandTransform.position;
				_right.PreviousPosition = _rightHandTransform.position;
				_right.CurrentPosition = _rightHandTransform.position;
			}
		}
		
		public void UpdatePosition(Hand hand) {
			if(hand == Hand.LEFT) {
				_left.PreviousPosition = _left.CurrentPosition;
				_left.CurrentPosition = _leftHandTransform.position;
			} else {
				_right.PreviousPosition = _right.CurrentPosition;
				_right.CurrentPosition = _rightHandTransform.position;
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
			new HandInfo(renderer: leftHandRenderer, climbCollider: leftClimbCollider, position: leftHandTransform.position),
			new HandInfo(renderer: rightHandRenderer, climbCollider: rightClimbCollider, position: rightHandTransform.position),
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
		StopPlayer();

		_isClimbing[hand] = true;
		_hands.StorePosition(hand);
	}
    
	private void HandleClimbEnded(Hand hand) {
		var handInfo = _hands.Get(hand);
		handInfo.Renderer.material.color = Color.deepSkyBlue;

		_isClimbing[hand] = false;
		
		if(_isClimbing.ContainsValue(true)) {
			return;
		}
		
		ReleasePlayer();
	}

	// Update is called once per frame
	private void Update() {
		foreach(var (hand, climbing) in _isClimbing) {
			if(climbing) {
				// Debug.Log($"Hand: {hand} is attached at position {_hands.GetStoredPosition(hand)}");
				// _hands.GetTransform(hand).position = _hands.GetStoredPosition(hand);
				_hands.UpdatePosition(hand);
				var handInfo = _hands.Get(hand);
				var handDelta = handInfo.StoredPosition - handInfo.CurrentPosition;
				
				accelerationMoveProvider.ScheduleMove(handDelta);
			}
		}	
	}

	private void StopPlayer() {
		gravityProvider.useGravity = false;
		accelerationMoveProvider.LockMovement();			
		playerCharacterController.Move(Vector3.zero);
	}
	
	private void ReleasePlayer() {
		accelerationMoveProvider.UnlockMovement();
		gravityProvider.useGravity = true;
	}
}
