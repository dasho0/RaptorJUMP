using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using UnityEngine;

public class CheckpointManager : MonoBehaviour {
	private static readonly Dictionary<int, Checkpoint> CheckpointById = new Dictionary<int, Checkpoint>();
	private static Checkpoint _nextCheckpoint;

	public static void Register(Checkpoint checkpoint) {
		Debug.Assert(!CheckpointById.ContainsKey(checkpoint.checkpointID));
		
		CheckpointById.Add(checkpoint.checkpointID, checkpoint);
		
		foreach(var c in CheckpointById.Values) {
			c.Deactivate();	
		}
		
		_nextCheckpoint = CheckpointById[CheckpointById.Keys.Min()];
		_nextCheckpoint.Activate();
	}

	public static void Pass(Checkpoint checkpoint) {
		Debug.Assert(checkpoint == _nextCheckpoint);
		

		var nextCheckpointId = checkpoint.checkpointID + 1;
		
		_nextCheckpoint.Deactivate();
		_nextCheckpoint = CheckpointById.GetValueOrDefault(nextCheckpointId); //TODO: handle end of level
		if(_nextCheckpoint != null) {
			_nextCheckpoint.Activate();	
		}
	}
}
