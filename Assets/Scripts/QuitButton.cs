using UnityEngine;

public class QuitButton : MonoBehaviour {
	public void QuitApplication() {
		Debug.Log("Quitting application...");
		Application.Quit();
	}
	
}
