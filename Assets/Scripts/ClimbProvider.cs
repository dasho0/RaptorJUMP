using System;
using System.Collections.Generic;
using Unity.VisualScripting.Antlr3.Runtime.Tree;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;

public class ClimbProvider : MonoBehaviour {
	[SerializeField] private ClimbCollider leftClimbCollider; 
	[SerializeField] private ClimbCollider rightClimbCollider;
	
	[SerializeField] private Renderer leftHandRenderer;
	[SerializeField] private Renderer rightHandRenderer;
	
	[SerializeField] private Transform leftHandTransform;
	[SerializeField] private Transform rightHandTransform;
	
	[SerializeField] private CharacterController playerCharacterController;
	[SerializeField] private AccelerationMoveProvider accelerationMoveProvider;
	[SerializeField] private GravityProvider gravityProvider;
	
	[SerializeField] private float grabSpeedDecayFactor = 1.3f;
	
	private Hands _hands;
	private Dictionary<Hand, bool> _isClimbing = new Dictionary<Hand, bool> {
		{ Hand.LEFT, false },
		{ Hand.RIGHT, false },
	};
	
	private GameObject _momentumRigidbodyObject;
	private Rigidbody _momentumRigidbody;

	private Vector3 _previousPlayerPosition;

	private GrabTimer _grabTimer = new GrabTimer();

	private Vector3 _storedMoveDirection;
	private float _storedSpeed;
	// private Hand? _scheduledHandPositionStore = null;
	private Vector3 _previousHandDelta = Vector3.zero;
	
	private class GrabTimer {
		public float Value { get; private set; } = 0.5f;	
		
		public void Reset() {
			Value = 1f;
		}
		
		public void Decrement() {
			Value -= Time.deltaTime;
		}
	}
	
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
		
		// public void MoveHandToStoredPosition(Hand hand) {
		// 	if(hand == Hand.LEFT) {
		// 		_leftHandTransform.position = _left.StoredPosition;
		// 	} else {
		// 		_rightHandTransform.position = _right.StoredPosition;
		// 	}
		// }
		
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
		_grabTimer.Reset();
		
		// var storedMoveDirectionFull = Vector3.Normalize(_previousPlayerPosition - playerCharacterController.transform.position);
		var storedMoveDirectionFull = accelerationMoveProvider.LastMoveNormalized;
		_storedMoveDirection = new Vector3(storedMoveDirectionFull.x, 0, storedMoveDirectionFull.z);
		
		_storedSpeed = accelerationMoveProvider.CurrentSpeed;
	}
    
	private void HandleClimbEnded(Hand hand) {
		var handInfo = _hands.Get(hand);
		handInfo.Renderer.material.color = Color.deepSkyBlue;

		_isClimbing[hand] = false;
		
		if(_isClimbing.ContainsValue(true)) {
			return;
		}
		
		ReleasePlayer();
		CreateMomentumRigidBody();
		accelerationMoveProvider.CurrentSpeed = _storedSpeed;
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
				
				// JA PIERDOLE
				if(handDelta == _previousHandDelta * -1) {
					_hands.StorePosition(hand);
					_hands.UpdatePosition(hand);		
				} else {
					accelerationMoveProvider.ScheduleMove(handDelta);
				}
				
				_previousHandDelta = handDelta;
				// _hands.MoveHandToStoredPosition(hand);
				

				if(_grabTimer.Value <= 0f) {
					_storedSpeed = Mathf.Max(_storedSpeed - grabSpeedDecayFactor * Time.deltaTime, 0);
				}
			}
		}	
		
		_grabTimer.Decrement();
		
		if(!_momentumRigidbody) {
			return;
		}
		
		if(gravityProvider.isGrounded || _isClimbing.ContainsValue(true)) {	
			DestroyMomentumRigidBody();
		} else {
			playerCharacterController.transform.position = _momentumRigidbody.transform.position;
		}
		
		_previousPlayerPosition = playerCharacterController.transform.position;
	}

	// private void LateUpdate() {
	// 	if(_scheduledHandPositionStore != null) {
	// 		_hands.StorePosition(_scheduledHandPositionStore.Value);
	// 		_scheduledHandPositionStore = null
	// 	}
	// }

	private void StopPlayer() {
		gravityProvider.useGravity = false;
		accelerationMoveProvider.LockMovement();			
		playerCharacterController.Move(Vector3.zero);
	}
	
	private void ReleasePlayer() {
		accelerationMoveProvider.UnlockMovement();
		gravityProvider.useGravity = true;
	}

	private void CreateMomentumRigidBody() {
		_momentumRigidbodyObject = new GameObject("ClimbMomentumGuide");
    
		_momentumRigidbodyObject.transform.position = playerCharacterController.transform.position;
		_momentumRigidbody = _momentumRigidbodyObject.AddComponent<Rigidbody>();
		_momentumRigidbody.useGravity = true;
		_momentumRigidbody.linearDamping = 0.5f;
		_momentumRigidbody.linearVelocity = (_previousPlayerPosition - playerCharacterController.transform.position + _storedSpeed * _storedMoveDirection);
		// _momentumRigidbody.linearVelocity = new Vector3(0, 10, 0);
	}
	
	private void DestroyMomentumRigidBody() {
		if(!_momentumRigidbodyObject) {
			return;
		}
		Destroy(_momentumRigidbodyObject);
		_momentumRigidbodyObject = null;
		_momentumRigidbody = null;
	}

#if UNITY_EDITOR
	private void OnGUI() {
		GUILayout.Label($"Stored speed: {_storedSpeed}");
	}
#endif
}
