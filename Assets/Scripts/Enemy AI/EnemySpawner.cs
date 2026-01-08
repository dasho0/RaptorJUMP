using UnityEngine;

public class EnemySpawner : MonoBehaviour
{
    [Header("Enemy Settings")]
    public GameObject enemyPrefab;
    public int numberOfEnemies = 3;

    [Header("Spawn Area")]
    public Transform[] spawnPoints;

    [Header("Trigger Settings")]
    public string playerTag = "Player";
    public bool spawnOnlyOnce = true;

    private bool hasSpawned = false;

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag) && (!hasSpawned || !spawnOnlyOnce))
        {
            SpawnEnemies();
            hasSpawned = true;
        }
    }

    void SpawnEnemies()
    {
        for (int i = 0; i < numberOfEnemies; i++)
        {
            Transform spawnPoint = GetRandomSpawnPoint();
            if (spawnPoint != null && enemyPrefab != null)
            {
                Instantiate(enemyPrefab, spawnPoint.position, spawnPoint.rotation);
            }
        }
    }

    Transform GetRandomSpawnPoint()
    {
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            return spawnPoints[Random.Range(0, spawnPoints.Length)];
        }
        return transform; // fallback to spawner's own position
    }
}
