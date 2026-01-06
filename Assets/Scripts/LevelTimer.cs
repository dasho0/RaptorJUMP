using TMPro;
using UnityEngine;

public class LevelTimer : MonoBehaviour {
	[SerializeField] private TMP_Text timerText;

	private float _timerSeconds = 0f;
	private bool _paused = true;
	// Start is called once before the first execution of Update after the MonoBehaviour is created
	void Start() {
		timerText.text = "";
	}

	// Update is called once per frame
	void Update() {
		if(_paused) return;
		
		_timerSeconds += Time.deltaTime;

		var totalSeconds = Mathf.FloorToInt(_timerSeconds);
		var minutes = totalSeconds / 60;
		var seconds = totalSeconds % 60;
		var centiseconds = Mathf.FloorToInt((_timerSeconds - totalSeconds) * 100f);

		timerText.text = $"{minutes:00}:{seconds:00}.{centiseconds:00}";
	}

	public void StartTimer() {
		_paused = false;
	}
	
	public void PauseTimer() {
		_paused = true;
	}
	
	public void ResetTimer() {
		_timerSeconds = 0f;
	}
}
