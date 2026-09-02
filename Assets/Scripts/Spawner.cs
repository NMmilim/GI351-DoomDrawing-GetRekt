using System.Collections;
using UnityEngine;

public class Spawner : MonoBehaviour
{
    [Header("Spawn")]
    public GameObject enemyPrefab; // assign Enemy2 prefab
    public Transform[] spawnPoints; // optional spawn locations
    public float spawnRate = 3f; // seconds between spawns
    public bool spawnOnStart = true;
    public int maxConcurrent = 10; // 0 = unlimited
    public bool waitForClear = true; // if true, wait until spawned enemies are destroyed before spawning next

    void Start()
    {
        if (spawnOnStart)
            StartCoroutine(SpawnLoop());
    }

    IEnumerator SpawnLoop()
    {
        while (true)
        {
            if (enemyPrefab != null)
            {
                if (maxConcurrent <= 0 || CountEnemies() < maxConcurrent)
                {
                    SpawnOne();

                    // if configured, wait until all spawned enemies are gone before continuing
                    if (waitForClear)
                    {
                        // wait until there are no Enemy2 instances (spawned enemy destroyed)
                        yield return new WaitUntil(() => CountEnemies() == 0);
                    }
                }
            }
            // always wait spawnRate between spawn attempts (after optional waitForClear)
            yield return new WaitForSeconds(spawnRate);
        }
    }

    void SpawnOne()
    {
        Vector3 pos = transform.position;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            var pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
            pos = pt.position;
        }

        var go = Instantiate(enemyPrefab, pos, Quaternion.identity);
        // optional: set tag
        // go.tag = "Enemy";
    }

    int CountEnemies()
    {
        if (maxConcurrent <= 0) return 0;
        var arr = GameObject.FindObjectsOfType<Enemy2>();
        return arr.Length;
    }
}
