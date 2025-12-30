using System;
using UnityEngine;
public class Checkpoint : MonoBehaviour {
	public int checkpointID = 0;

	private Renderer materialRenderer;
	private Collider _collider;

	private void OnTriggerEnter(Collider other) {
		Debug.Log("Hit checkpoint " + checkpointID);
		CheckpointManager.Pass(this);
	}

	private void Awake() {
		var visual = transform.Find("Visual").gameObject;
		materialRenderer = visual.GetComponent<Renderer>();
		_collider = GetComponent<Collider>();
		
		CheckpointManager.Register(this);	
	}

	public void Deactivate() {
		materialRenderer.enabled = false;
		_collider.enabled = false;
	}
	
	public void Activate() {
		materialRenderer.enabled = true;
		_collider.enabled = true;
	}
}
