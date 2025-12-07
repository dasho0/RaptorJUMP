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
		var pointsAroundCollider = FibonacciSphere(grabRaycasts)
			.Select(p => p * colliderRadius + handCollider.bounds.center)
			.ToList();
		
#if UNITY_EDITOR
		dbg_pointsAroundCollider = pointsAroundCollider;
#endif

		var distinctNormals = new List<Vector3>();
		foreach(var point in pointsAroundCollider) {
			var closestPointsOnColliders = _currentlyOverlapping
				.Select(c => c.ClosestPoint(point))
				.Where(p => Vector3.Distance(handCollider.bounds.center, p) <= colliderRadius)
				.Distinct()
				.ToList();

			var normals = new List<Vector3>(closestPointsOnColliders.Count());
			foreach (var closest in closestPointsOnColliders) {
				var direction = (closest - point).normalized;
				var distance = Vector3.Distance(point, closest);
				var ray = new Ray(point, direction);
				Debug.DrawRay(point, direction * distance, Color.red, 0.016f);
				
				if(Physics.Raycast(ray, out var hitInfo, distance, ~ignoreLayers)) {
					normals.Add(hitInfo.normal);	
					Debug.DrawRay(hitInfo.point, hitInfo.normal * 0.1f, Color.cyan, 0.016f);
				}
			}

			distinctNormals.AddRange(normals.Distinct().ToList());
		}
		
		// foreach(var normal in distinctNormals) {
		// 	Debug.DrawRay(handCollider.bounds.center, normal * 0.1f, Color.yellow, 0.016f);	
		// }
		
		foreach(var n1 in distinctNormals) {
			foreach(var n2 in distinctNormals) {
				if(Vector3.Dot(n1, n2) <= Mathf.Abs(Mathf.Cos(grabAngleThreshold * 0.017453292519943f))) {
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

	// Update is called once per frame
	void Update() {
		var isPressed = climbButton.IsPressed();
		handCollider.enabled = isPressed;

		if(_currentlyOverlapping.Count == 0 || !isPressed) {
			_currentlyOverlapping.Clear();
			if(_gripping) {
				onClimbEnded?.Invoke(hand);
				_gripping = false;
			}
			return;
		}

		var canGrip = CheckIfCanGrip();
		if(canGrip && !_gripping) {
			onClimbStarted?.Invoke(hand);
			
			_gripping = true;
		} else if(!canGrip && _gripping) {
			onClimbEnded?.Invoke(hand);
			_gripping = false;
		}

#if UNITY_EDITOR
		dbg_DrawPoints();
#endif
	}
}
