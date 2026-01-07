using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

public class CheckpointManager : MonoBehaviour {
	// [SerializeField] private Transform playerTransform;
	[SerializeField] private TeleportationProvider teleportationProvider;
	[SerializeField] private AccelerationMoveProvider accelerationMoveProvider;
	[SerializeField] private LevelTimer levelTimer;
	
	private readonly Dictionary<int, Checkpoint> _checkpointById = new Dictionary<int, Checkpoint>();
	private Checkpoint _nextCheckpoint;
	private Checkpoint _currentCheckpoint;

	public void Register(Checkpoint checkpoint) {
		Debug.Assert(!_checkpointById.ContainsKey(checkpoint.checkpointID));

		_checkpointById.Add(checkpoint.checkpointID, checkpoint);

		foreach (var c in _checkpointById.Values) {
			c.Deactivate();
		}

		_nextCheckpoint = _checkpointById[_checkpointById.Keys.Min()];
		_nextCheckpoint.Activate();

		// _currentCheckpoint ??= _nextCheckpoint;
		if(checkpoint.checkpointID == 0) {
			_currentCheckpoint = checkpoint;
		}
	}

	public void Pass(Checkpoint checkpoint) {
		Debug.Assert(checkpoint == _nextCheckpoint);

		if(_currentCheckpoint == _checkpointById[0]) {
			levelTimer.StartTimer();	
		}

		_currentCheckpoint = checkpoint;

		var nextCheckpointId = checkpoint.checkpointID + 1;

		_nextCheckpoint.Deactivate();
		_nextCheckpoint = _checkpointById.GetValueOrDefault(nextCheckpointId); //TODO: handle end of level
		if (_nextCheckpoint == null) {
			levelTimer.PauseTimer();			
			return;
		}
		
		_nextCheckpoint.Activate();
	}

	public void Respawn() {
		var targetPosition = _currentCheckpoint.transform.position;
		var teleportationRequest = new TeleportRequest {	
			destinationPosition = targetPosition,
			destinationRotation = Quaternion.identity,
		};

		accelerationMoveProvider.CurrentSpeed = 0;
		teleportationProvider.QueueTeleportRequest(teleportationRequest);
	}
}
