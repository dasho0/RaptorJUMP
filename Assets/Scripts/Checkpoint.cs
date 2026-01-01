using System;
using UnityEngine;
public class Checkpoint : MonoBehaviour {
	public int checkpointID = 0;
	
	private CheckpointManager _checkpointManager;

	private Renderer _materialRenderer;
	private Collider _collider;

	private void OnTriggerEnter(Collider other) {
		Debug.Log("Hit checkpoint " + checkpointID);
		_checkpointManager.Pass(this);
	}

	private void Awake() {
		var visual = transform.Find("Visual").gameObject;
		_materialRenderer = visual.GetComponent<Renderer>();
		_collider = GetComponent<Collider>();
	}

	private void Start() {
		_checkpointManager = FindFirstObjectByType<CheckpointManager>();
		Debug.Assert(_checkpointManager != null);
		
		_checkpointManager.Register(this);	
	}

	public void Deactivate() {
		_materialRenderer.enabled = false;
		_collider.enabled = false;
	}
	
	public void Activate() {
		_materialRenderer.enabled = true;
		_collider.enabled = true;
	}
}
