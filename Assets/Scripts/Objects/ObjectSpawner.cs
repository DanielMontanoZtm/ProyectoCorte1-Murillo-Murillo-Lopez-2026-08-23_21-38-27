using System.Collections;
using UnityEngine;

/// <summary>
/// Spawns falling objects (potions / bombs) from a horizontal spawn zone
/// above the play field.  Respects per-level configuration (speed, rate,
/// density) from LevelManager.  Exposes a singleton so FallingObject can
/// notify removal without a direct reference.
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    public static ObjectSpawner Instance { get; private set; }

    // ── Prefab references — assign in Inspector ──────────────────────────────
    [Header("Prefabs (drag from Assets/Prefabs/)")]
    [SerializeField] private GameObject potion1Prefab;
    [SerializeField] private GameObject potion2Prefab;
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private GameObject powerUpPrefab;   // re-use potion with tint if needed

    // ── Spawn zone ───────────────────────────────────────────────────────────
    [Header("Spawn zone")]
    [SerializeField] private float spawnY      = 12f;   // height above the play area
    [SerializeField] private float spawnXMin   = -4f;
    [SerializeField] private float spawnXMax   =  4f;
    [SerializeField] private float spawnZ      =  0f;

    // ── State ────────────────────────────────────────────────────────────────
    private int   _activeObjects;
    private bool  _spawning;

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelChanged.AddListener(_ => RestartSpawnLoop());
            GameManager.Instance.OnGameOver.AddListener(StopSpawning);
            GameManager.Instance.OnGameWin.AddListener(StopSpawning);
        }
    }

    private void OnDisable()
    {
        StopSpawning();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelChanged.RemoveListener(_ => RestartSpawnLoop());
            GameManager.Instance.OnGameOver.RemoveListener(StopSpawning);
            GameManager.Instance.OnGameWin.RemoveListener(StopSpawning);
        }
    }

    // ── Public API ───────────────────────────────────────────────────────────

    public void StartSpawning()
    {
        StopSpawning();
        _spawning = true;
        StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        _spawning = false;
        StopAllCoroutines();
    }

    /// <summary>Called by FallingObject when it is destroyed (caught or missed).</summary>
    public void NotifyObjectRemoved() => _activeObjects = Mathf.Max(0, _activeObjects - 1);

    // ── Internal ─────────────────────────────────────────────────────────────

    private void RestartSpawnLoop()
    {
        if (_spawning) StartSpawning();
    }

    private IEnumerator SpawnLoop()
    {
        while (_spawning)
        {
            LevelConfig cfg = LevelManager.Instance != null
                ? LevelManager.Instance.ActiveConfig
                : null;

            int   maxSim  = cfg?.maxSimultaneous ?? 4;
            float interval = cfg?.spawnInterval  ?? 1.5f;

            if (_activeObjects < maxSim &&
                GameManager.Instance?.CurrentState == GameManager.GameState.Playing)
            {
                SpawnOne();
            }

            yield return new WaitForSeconds(interval);
        }
    }

    private void SpawnOne()
    {
        FallingObjectType type = LevelManager.Instance != null
            ? LevelManager.Instance.GetRandomObjectType()
            : FallingObjectType.Potion1;

        GameObject prefab = GetPrefab(type);
        if (prefab == null)
        {
            Debug.LogWarning($"[ObjectSpawner] Prefab for {type} is not assigned.");
            return;
        }

        float   x   = Random.Range(spawnXMin, spawnXMax);
        Vector3 pos = new Vector3(x, spawnY, spawnZ);

        GameObject obj = Instantiate(prefab, pos, Quaternion.identity);

        LevelConfig cfg   = LevelManager.Instance?.ActiveConfig;
        float minSpeed    = cfg?.minFallSpeed ?? 2f;
        float maxSpeed    = cfg?.maxFallSpeed ?? 4f;
        float speed       = Random.Range(minSpeed, maxSpeed);

        var fo = obj.GetComponent<FallingObject>();
        if (fo != null)
            fo.Initialise(type, speed);
        else
            Debug.LogWarning($"[ObjectSpawner] Prefab '{prefab.name}' is missing a FallingObject component.");

        _activeObjects++;
    }

    private GameObject GetPrefab(FallingObjectType type) => type switch
    {
        FallingObjectType.Potion1  => potion1Prefab,
        FallingObjectType.Potion2  => potion2Prefab,
        FallingObjectType.Bomb     => bombPrefab,
        FallingObjectType.PowerUp  => powerUpPrefab ?? potion1Prefab,
        _                          => null
    };
}
