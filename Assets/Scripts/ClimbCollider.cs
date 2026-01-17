using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;
using Color=UnityEngine.Color;

public enum Hand {
	LEFT,
	RIGHT,
}
public class ClimbCollider : MonoBehaviour {
	[FormerlySerializedAs("collider")]
	[SerializeField] private Collider handCollider;
	[SerializeField] private InputAction climbButton;
	[SerializeField] private Hand hand;
	[SerializeField] private float colliderRadius;
	[SerializeField] private int grabRaycasts;
	[SerializeField] private LayerMask ignoreLayers;
	[SerializeField] private int grabAngleThreshold = 80;

	private bool _gripping = false;
	private readonly List<Collider> _currentlyOverlapping = new List<Collider>();
	public event Action<Hand> onClimbStarted;
	public event Action<Hand> onClimbEnded;
	
#if UNITY_EDITOR
	private List<Vector3> dbg_pointsAroundCollider = new List<Vector3>();
	private void dbg_DrawPoints() {
		foreach(var p in dbg_pointsAroundCollider) {
			Debug.DrawRay(p, Vector3.up * 0.02f, Color.green);
		}
	}		
#endif
    
	void Start()
	{
		Debug.Assert(handCollider.isTrigger);
	}

	private void OnEnable() {
		climbButton.Enable();
	}

	private void OnDisable() {
		climbButton.Disable();	
	}

	// private void isGrabbingCorner(Collider handCollider, Collider other) {
	// 	handCollider.	
	// }
	
	private void OnTriggerEnter(Collider other) {
		if(((1 << other.gameObject.layer) & ignoreLayers) != 0) return;
		_currentlyOverlapping.Add(other);
		Debug.Assert(other.gameObject.layer != 3);
	}

	private void OnTriggerExit(Collider other) {
		if(((1 << other.gameObject.layer) & ignoreLayers) != 0) return;
		_currentlyOverlapping.Remove(other);
	}

	private bool CheckIfCanGrip() {
		var center = handCollider.bounds.center;
		var pointsAroundCollider = FibonacciSphere(grabRaycasts)
			.Select(p => p * colliderRadius + center)
			.ToList();
		
#if UNITY_EDITOR
		dbg_pointsAroundCollider = pointsAroundCollider;
#endif

		const float castRadius = 0.01f; 
		const float backoff = 0.002f;   
		const float extra = 0.01f;      

		var distinctNormals = new List<Vector3>();

		foreach (var point in pointsAroundCollider) {
			var toCenter = center - point;
			var dist = toCenter.magnitude;
			if (dist <= Mathf.Epsilon) {
				continue;
			}

			var direction = toCenter / dist;
			var origin = point - direction * backoff; 
			var maxDistance = dist + backoff + extra;

			if (!Physics.SphereCast(origin, castRadius, direction, out var hitInfo, maxDistance, ~ignoreLayers, QueryTriggerInteraction.Ignore)) {
				continue;
			}

			distinctNormals.Add(hitInfo.normal);

			Debug.DrawRay(origin, direction * Mathf.Min(maxDistance, 0.25f), Color.red, 0.016f);
			Debug.DrawRay(hitInfo.point, hitInfo.normal * 0.1f, Color.cyan, 0.016f);
		}

		const float normalDotSameThreshold = 0.999f;
		var filteredNormals = new List<Vector3>();
		foreach (var n in distinctNormals) {
			var nn = n.normalized;
			if (!filteredNormals.Any(existing => Vector3.Dot(existing, nn) >= normalDotSameThreshold)) {
				filteredNormals.Add(nn);
			}
		}

		var dotThreshold = Mathf.Abs(Mathf.Cos(grabAngleThreshold * Mathf.Deg2Rad));
		for (var i = 0; i < filteredNormals.Count; i++) {
			for (var j = i + 1; j < filteredNormals.Count; j++) {
				if (Vector3.Dot(filteredNormals[i], filteredNormals[j]) <= dotThreshold) {
					return true;
				}
			}
		}

		return false;
	}
	
	private List<Vector3> FibonacciSphere(int n)
	{
		var points = new List<Vector3>(n);

		float goldenAngle = Mathf.PI * (3f - Mathf.Sqrt(5f));

		for (int i = 0; i < n; i++)
		{
			float y = 1f - 2f * (i + 0.5f) / n;
			float radiusAtY = Mathf.Sqrt(1f - y * y);

			float theta = goldenAngle * i;

			float x = Mathf.Cos(theta) * radiusAtY;
			float z = Mathf.Sin(theta) * radiusAtY;

			points.Add(new Vector3(x, y, z));
		}
		
		return points;
	}

	void Update() {
		var isPressed = climbButton.IsPressed();
		handCollider.enabled = isPressed;

		if(_currentlyOverlapping.Count == 0 || !isPressed) {
			_currentlyOverlapping.Clear();
			if(!_gripping || isPressed) {
				return;
			}
				
			onClimbEnded?.Invoke(hand);
			_gripping = false;
			return;
		}

		var canGrip = CheckIfCanGrip();
		if(canGrip && !_gripping) {
			onClimbStarted?.Invoke(hand);
			
			_gripping = true;
		} 

#if UNITY_EDITOR
		dbg_DrawPoints();
#endif
	}

	// public void IncreaseColliderRadius(float delta) {
	// 	Debug.Assert(_gripping);
	// 	colliderRadius += delta;
	// }
}
