using System;
using UnityEngine;

public class QuitButton : MonoBehaviour {
	private void QuitApplication() {
		Debug.Log("Quitting application...");
		Application.Quit();
	}

	private void OnTriggerEnter(Collider other) {
		QuitApplication();
	}

}
