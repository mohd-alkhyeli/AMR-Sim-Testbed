using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RandomizedObstacleSpawner : MonoBehaviour
{
    [Header("Spawnable Objects")]
    [Tooltip("Prefabs that can be spawned randomly. Each prefab should usually have a Rigidbody and Collider.")]
    public List<GameObject> spawnPrefabs = new List<GameObject>();

    [Header("Spawn Timing")]
    [Tooltip("Start spawning automatically when the scene starts.")]
    public bool spawnOnStart = true;

    [Tooltip("Minimum time between spawns.")]
    public float minSpawnInterval = 0.5f;

    [Tooltip("Maximum time between spawns.")]
    public float maxSpawnInterval = 2f;

    [Tooltip("How many objects spawn each time.")]
    public Vector2Int objectsPerSpawn = new Vector2Int(1, 1);

    [Header("Spawn Volume")]
    [Tooltip("Local-space offset from this spawner object's position.")]
    public Vector3 volumeOffset = Vector3.zero;

    [Tooltip("Size of the box volume where objects can appear.")]
    public Vector3 volumeSize = new Vector3(5f, 3f, 5f);

    [Header("Random Rotation")]
    [Tooltip("If enabled, objects spawn with fully random 3D rotation.")]
    public bool randomRotation = true;

    [Tooltip("Used only if Random Rotation is disabled.")]
    public Vector3 fixedEulerRotation = Vector3.zero;

    [Header("Random Scale")]
    [Tooltip("Minimum uniform scale multiplier.")]
    public float minScale = 0.75f;

    [Tooltip("Maximum uniform scale multiplier.")]
    public float maxScale = 1.5f;

    [Header("Physics")]
    [Tooltip("Automatically add a Rigidbody if the spawned prefab does not already have one.")]
    public bool addRigidbodyIfMissing = true;

    [Tooltip("Mass assigned only when adding a new Rigidbody automatically.")]
    public float defaultMass = 1f;

    [Tooltip("Random sideways velocity range applied on spawn.")]
    public Vector2 randomHorizontalVelocityRange = new Vector2(-0.5f, 0.5f);

    [Tooltip("Random downward velocity range. Use negative values for downward movement.")]
    public Vector2 randomDownwardVelocityRange = new Vector2(-0.5f, -2f);

    [Tooltip("Random angular velocity strength. Higher = more spinning.")]
    public float randomAngularVelocity = 3f;

    [Header("Limits / Cleanup")]
    [Tooltip("Maximum spawned objects allowed at once. Set to 0 for unlimited.")]
    public int maxAliveObjects = 50;

    [Tooltip("Destroy spawned objects after this many seconds. Set to 0 to never auto-destroy.")]
    public float destroyAfterSeconds = 15f;

    [Header("Gizmos")]
    public bool drawGizmos = true;
    public Color volumeColor = new Color(0f, 1f, 1f, 0.15f);
    public Color wireColor = Color.cyan;

    private readonly List<GameObject> aliveObjects = new List<GameObject>();
    private Coroutine spawnRoutine;

    private void Start()
    {
        if (spawnOnStart)
            StartSpawning();
    }

    public void StartSpawning()
    {
        if (spawnRoutine != null)
            return;

        spawnRoutine = StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        if (spawnRoutine == null)
            return;

        StopCoroutine(spawnRoutine);
        spawnRoutine = null;
    }

    public void SpawnOnce()
    {
        CleanAliveList();

        if (spawnPrefabs == null || spawnPrefabs.Count == 0)
            return;

        int spawnCount = Random.Range(objectsPerSpawn.x, objectsPerSpawn.y + 1);

        for (int i = 0; i < spawnCount; i++)
        {
            if (maxAliveObjects > 0 && aliveObjects.Count >= maxAliveObjects)
                return;

            SpawnObject();
        }
    }

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            SpawnOnce();

            float waitTime = Random.Range(minSpawnInterval, maxSpawnInterval);
            yield return new WaitForSeconds(waitTime);
        }
    }

    private void SpawnObject()
    {
        GameObject prefab = GetRandomPrefab();

        if (prefab == null)
            return;

        Vector3 spawnPosition = GetRandomPointInVolume();
        Quaternion spawnRotation = randomRotation
            ? Random.rotation
            : Quaternion.Euler(fixedEulerRotation);

        GameObject spawnedObject = Instantiate(prefab, spawnPosition, spawnRotation);

        float scale = Random.Range(minScale, maxScale);
        spawnedObject.transform.localScale *= scale;

        Rigidbody rb = spawnedObject.GetComponent<Rigidbody>();

        if (rb == null && addRigidbodyIfMissing)
        {
            rb = spawnedObject.AddComponent<Rigidbody>();
            rb.mass = defaultMass;
        }

        if (rb != null)
        {
            rb.useGravity = true;

            float xVelocity = Random.Range(randomHorizontalVelocityRange.x, randomHorizontalVelocityRange.y);
            float zVelocity = Random.Range(randomHorizontalVelocityRange.x, randomHorizontalVelocityRange.y);
            float yVelocity = Random.Range(randomDownwardVelocityRange.x, randomDownwardVelocityRange.y);

            rb.linearVelocity = new Vector3(xVelocity, yVelocity, zVelocity);
            rb.angularVelocity = Random.insideUnitSphere * randomAngularVelocity;
        }

        aliveObjects.Add(spawnedObject);

        if (destroyAfterSeconds > 0f)
            Destroy(spawnedObject, destroyAfterSeconds);
    }

    private GameObject GetRandomPrefab()
    {
        if (spawnPrefabs.Count == 0)
            return null;

        int index = Random.Range(0, spawnPrefabs.Count);
        return spawnPrefabs[index];
    }

    private Vector3 GetRandomPointInVolume()
    {
        Vector3 localPoint = new Vector3(
            Random.Range(-volumeSize.x * 0.5f, volumeSize.x * 0.5f),
            Random.Range(-volumeSize.y * 0.5f, volumeSize.y * 0.5f),
            Random.Range(-volumeSize.z * 0.5f, volumeSize.z * 0.5f)
        );

        localPoint += volumeOffset;

        return transform.TransformPoint(localPoint);
    }

    private void CleanAliveList()
    {
        for (int i = aliveObjects.Count - 1; i >= 0; i--)
        {
            if (aliveObjects[i] == null)
                aliveObjects.RemoveAt(i);
        }
    }

    private void OnValidate()
    {
        minSpawnInterval = Mathf.Max(0.01f, minSpawnInterval);
        maxSpawnInterval = Mathf.Max(minSpawnInterval, maxSpawnInterval);

        objectsPerSpawn.x = Mathf.Max(1, objectsPerSpawn.x);
        objectsPerSpawn.y = Mathf.Max(objectsPerSpawn.x, objectsPerSpawn.y);

        volumeSize.x = Mathf.Max(0.01f, volumeSize.x);
        volumeSize.y = Mathf.Max(0.01f, volumeSize.y);
        volumeSize.z = Mathf.Max(0.01f, volumeSize.z);

        minScale = Mathf.Max(0.01f, minScale);
        maxScale = Mathf.Max(minScale, maxScale);

        defaultMass = Mathf.Max(0.01f, defaultMass);
        maxAliveObjects = Mathf.Max(0, maxAliveObjects);
        destroyAfterSeconds = Mathf.Max(0f, destroyAfterSeconds);
        randomAngularVelocity = Mathf.Max(0f, randomAngularVelocity);
    }

    private void OnDrawGizmos()
    {
        if (!drawGizmos)
            return;

        Matrix4x4 oldMatrix = Gizmos.matrix;

        Gizmos.matrix = transform.localToWorldMatrix;

        Vector3 center = volumeOffset;

        Gizmos.color = volumeColor;
        Gizmos.DrawCube(center, volumeSize);

        Gizmos.color = wireColor;
        Gizmos.DrawWireCube(center, volumeSize);

        Gizmos.matrix = oldMatrix;

        DrawDownDirectionArrow();
    }

    private void DrawDownDirectionArrow()
    {
        Vector3 center = transform.TransformPoint(volumeOffset);
        Vector3 top = center + Vector3.up * (volumeSize.y * 0.5f + 0.5f);
        Vector3 bottom = center + Vector3.down * (volumeSize.y * 0.5f + 0.5f);

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(top, bottom);

        Vector3 arrowLeft = bottom + new Vector3(-0.2f, 0.35f, 0f);
        Vector3 arrowRight = bottom + new Vector3(0.2f, 0.35f, 0f);

        Gizmos.DrawLine(bottom, arrowLeft);
        Gizmos.DrawLine(bottom, arrowRight);
    }
}