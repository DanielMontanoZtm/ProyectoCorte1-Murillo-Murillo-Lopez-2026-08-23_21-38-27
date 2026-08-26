using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spawns falling objects using per-type object pools.
///
/// COORDINATE SYSTEM (this scene):
///   - Camera at (46.9, 13.9, 16.6), rotation Y=180°, X=16.8° → faces -Z.
///   - Horizontal axis visible on screen = X.
///   - Vertical axis on screen = Y.
///   - Cauldron Z is fixed (~-9.24). Objects spawn at that same Z so they
///     fall straight toward the cauldron in the camera's view.
///   - Objects fall downward along -Y until caught or they pass destroyYThreshold.
///
/// BALANCE:
///   - Anti-racha: max 2 consecutive bombs before forcing a positive.
///   - Power-up gap: forced every maxTurnsBetweenPowerUps spawns.
///   - Anti-overlap: min horizontal (X) gap between active objects.
/// </summary>
public class ObjectSpawner : MonoBehaviour
{
    public static ObjectSpawner Instance { get; private set; }

    // ── Prefabs ───────────────────────────────────────────────────────────────
    [Header("Prefabs (drag from Assets/Prefabs/)")]
    [SerializeField] private GameObject potion1Prefab;
    [SerializeField] private GameObject potion2Prefab;
    [SerializeField] private GameObject bombPrefab;
    [SerializeField] private GameObject powerUpPrefab;

    // ── Spawn zone ────────────────────────────────────────────────────────────
    [Header("Spawn zone (world coordinates)")]
    [Tooltip("How many units ABOVE the cauldron's Y to spawn objects. " +
             "Increase this if objects appear inside the room instead of falling from outside.")]
    [SerializeField] private float spawnHeightAboveCauldron = 14f;

    [Tooltip("How many units to the LEFT of the cauldron's X the spawn zone extends.")]
    [SerializeField] private float spawnRangeLeft  = 4f;
    [Tooltip("How many units to the RIGHT of the cauldron's X the spawn zone extends.")]
    [SerializeField] private float spawnRangeRight = 4f;

    [Tooltip("Fixed Z of the play lane — auto-read from Cauldron tag at Start if enabled.")]
    [SerializeField] private float spawnZ = 0f;
    [SerializeField] private bool  autoReadCauldronTransform = true;

    // Computed at Start from the Cauldron's actual scene position
    private float _spawnXMin;
    private float _spawnXMax;
    private float _spawnY;

    // ── Pool sizes ────────────────────────────────────────────────────────────
    [Header("Pool sizes")]
    [SerializeField] private int poolSizePotion1 = 8;
    [SerializeField] private int poolSizePotion2 = 8;
    [SerializeField] private int poolSizeBomb    = 6;
    [SerializeField] private int poolSizePowerUp = 4;

    // ── Uniform scale ─────────────────────────────────────────────────────────
    [Header("Uniform scale for all spawned objects")]
    [Tooltip("Overrides each prefab's original scale so all objects look the same size. " +
             "Adjust until objects look right relative to the cauldron opening.")]
    [SerializeField] private float uniformObjectScale = 0.35f;

    // ── Balance tuning ────────────────────────────────────────────────────────
    [Header("Balance tuning")]
    [SerializeField] private int   maxConsecutiveBombs     = 2;
    [SerializeField] private int   maxTurnsBetweenPowerUps = 12;
    [Tooltip("Minimum X distance between two simultaneously falling objects.")]
    [SerializeField] private float minXGap = 1.5f;

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly Dictionary<FallingObjectType, Queue<FallingObject>> _pools = new();
    private readonly List<FallingObject> _activeObjects = new();

    private bool _spawning;
    private int  _consecutiveBombs;
    private int  _turnsSinceLastPowerUp;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildPools();
    }

    private void Start()
    {
        var cauldronGO = GameObject.FindWithTag("Cauldron");
        if (cauldronGO != null && autoReadCauldronTransform)
        {
            float cx = cauldronGO.transform.position.x;
            float cy = cauldronGO.transform.position.y;
            float cz = cauldronGO.transform.position.z;

            _spawnXMin = cx - spawnRangeLeft;
            _spawnXMax = cx + spawnRangeRight;
            _spawnY    = cy + spawnHeightAboveCauldron;
            spawnZ     = cz;

            Debug.Log($"[ObjectSpawner] Calibrated → " +
                      $"X=[{_spawnXMin:F2}, {_spawnXMax:F2}]  " +
                      $"Y={_spawnY:F2}  Z={spawnZ:F2}");
        }
        else
        {
            _spawnXMin = -4f;
            _spawnXMax =  4f;
            _spawnY    = 12f;
            Debug.LogWarning("[ObjectSpawner] Cauldron tag not found — using fallback values. " +
                             "Make sure the Cauldron GameObject has the 'Cauldron' tag.");
        }
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelChanged.AddListener(OnLevelChanged);
            GameManager.Instance.OnGameOver.AddListener(StopSpawning);
            GameManager.Instance.OnGameWin.AddListener(StopSpawning);
        }
    }

    private void OnDisable()
    {
        StopSpawning();
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnLevelChanged.RemoveListener(OnLevelChanged);
            GameManager.Instance.OnGameOver.RemoveListener(StopSpawning);
            GameManager.Instance.OnGameWin.RemoveListener(StopSpawning);
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartSpawning()
    {
        StopSpawning();
        _spawning              = true;
        _consecutiveBombs      = 0;
        _turnsSinceLastPowerUp = 0;
        StartCoroutine(SpawnLoop());
    }

    public void StopSpawning()
    {
        _spawning = false;
        StopAllCoroutines();
        RecallAllActive();
    }

    public void ReturnToPool(FallingObject fo)
    {
        if (fo == null) return;
        _activeObjects.Remove(fo);
        fo.gameObject.SetActive(false);
        if (_pools.TryGetValue(fo.ObjectType, out var queue))
            queue.Enqueue(fo);
    }

    /// <summary>Legacy compatibility shim — no-op with pooling.</summary>
    public void NotifyObjectRemoved() { }

    // ── Pool management ───────────────────────────────────────────────────────

    private void BuildPools()
    {
        _pools[FallingObjectType.Potion1] = BuildPool(potion1Prefab,  FallingObjectType.Potion1,  poolSizePotion1);
        _pools[FallingObjectType.Potion2] = BuildPool(potion2Prefab,  FallingObjectType.Potion2,  poolSizePotion2);
        _pools[FallingObjectType.Bomb]    = BuildPool(bombPrefab,      FallingObjectType.Bomb,     poolSizeBomb);
        _pools[FallingObjectType.PowerUp] = BuildPool(
            powerUpPrefab != null ? powerUpPrefab : potion1Prefab,
            FallingObjectType.PowerUp, poolSizePowerUp);
    }

    private Queue<FallingObject> BuildPool(GameObject prefab, FallingObjectType type, int size)
    {
        var queue = new Queue<FallingObject>(size);
        if (prefab == null)
        {
            Debug.LogWarning($"[ObjectSpawner] Prefab for {type} not assigned.");
            return queue;
        }
        for (int i = 0; i < size; i++)
        {
            var go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            go.name = $"[Pool] {type} {i}";
            go.SetActive(false);
            var fo = go.GetComponent<FallingObject>();
            if (fo == null)
            {
                Debug.LogWarning($"[ObjectSpawner] '{prefab.name}' missing FallingObject.");
                Destroy(go);
                continue;
            }
            queue.Enqueue(fo);
        }
        return queue;
    }

    private FallingObject PullFromPool(FallingObjectType type)
    {
        if (!_pools.TryGetValue(type, out var queue) || queue.Count == 0)
        {
            var prefab = GetPrefab(type);
            if (prefab == null) return null;
            var go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
            var fo2 = go.GetComponent<FallingObject>();
            if (fo2 == null) { Destroy(go); return null; }
            go.SetActive(false);
            return fo2;
        }
        return queue.Dequeue();
    }

    private void RecallAllActive()
    {
        var snapshot = new List<FallingObject>(_activeObjects);
        foreach (var fo in snapshot) ReturnToPool(fo);
    }

    // ── Spawn loop ────────────────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        while (_spawning)
        {
            LevelConfig cfg      = LevelManager.Instance?.ActiveConfig;
            int   maxSim         = cfg?.maxSimultaneous ?? 4;
            float interval       = cfg?.spawnInterval   ?? 1.5f;

            bool playing = GameManager.Instance?.CurrentState == GameManager.GameState.Playing;
            if (playing && _activeObjects.Count < maxSim)
                TrySpawnOne(cfg);

            yield return new WaitForSeconds(interval);
        }
    }

    private void TrySpawnOne(LevelConfig cfg)
    {
        FallingObjectType type = ChooseType();

        if (!TryPickSafeX(out float x))
            return;

        var fo = PullFromPool(type);
        if (fo == null) return;

        float speed = Random.Range(cfg?.minFallSpeed ?? 2f, cfg?.maxFallSpeed ?? 4f);

        // Objects spawn at variable X, fixed Y (above screen), fixed Z (play lane)
        Vector3 pos = new Vector3(x, _spawnY, spawnZ);
        fo.Initialise(type, speed, pos);
        fo.transform.localScale = Vector3.one * uniformObjectScale;
        fo.gameObject.SetActive(true);

        _activeObjects.Add(fo);

        // Anti-racha counters
        if (type == FallingObjectType.Bomb)
        {
            _consecutiveBombs++;
            _turnsSinceLastPowerUp++;
        }
        else if (type == FallingObjectType.PowerUp)
        {
            _consecutiveBombs = _turnsSinceLastPowerUp = 0;
        }
        else
        {
            _consecutiveBombs = 0;
            _turnsSinceLastPowerUp++;
        }
    }

    // ── Type selection ────────────────────────────────────────────────────────

    private FallingObjectType ChooseType()
    {
        if (_turnsSinceLastPowerUp >= maxTurnsBetweenPowerUps)
            return FallingObjectType.PowerUp;
        if (_consecutiveBombs >= maxConsecutiveBombs)
            return ChoosePositiveOnly();
        return LevelManager.Instance != null
            ? LevelManager.Instance.GetRandomObjectType()
            : FallingObjectType.Potion1;
    }

    private FallingObjectType ChoosePositiveOnly()
    {
        LevelConfig cfg = LevelManager.Instance?.ActiveConfig;
        int p1w  = cfg?.potion1Weight  ?? 45;
        int p2w  = cfg?.potion2Weight  ?? 30;
        int puw  = cfg?.powerUpWeight  ?? 5;
        int roll = Random.Range(0, p1w + p2w + puw);
        if (roll < p1w)       return FallingObjectType.Potion1;
        if (roll < p1w + p2w) return FallingObjectType.Potion2;
        return FallingObjectType.PowerUp;
    }

    // ── X anti-overlap ────────────────────────────────────────────────────────

    private bool TryPickSafeX(out float x)
    {
        for (int i = 0; i < 10; i++)
        {
            float c = Random.Range(_spawnXMin, _spawnXMax);
            if (IsXSafe(c)) { x = c; return true; }
        }
        x = (_spawnXMin + _spawnXMax) * 0.5f;
        return false;
    }

    private bool IsXSafe(float candidate)
    {
        foreach (var fo in _activeObjects)
        {
            if (fo == null || !fo.gameObject.activeInHierarchy) continue;
            if (Mathf.Abs(fo.transform.position.x - candidate) < minXGap) return false;
        }
        return true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void OnLevelChanged(int _)
    {
        if (!_spawning) return;

        // Update speed of ALL currently falling objects to match the new level.
        // Do NOT recall them — they keep falling, just faster.
        LevelConfig cfg = LevelManager.Instance?.ActiveConfig;
        if (cfg == null) return;

        foreach (var fo in _activeObjects)
        {
            if (fo == null || !fo.gameObject.activeInHierarchy) continue;
            float newSpeed = Random.Range(cfg.minFallSpeed, cfg.maxFallSpeed);
            fo.UpdateSpeed(newSpeed);
        }

        // Restart only the spawn loop coroutine so it picks up the new
        // spawnInterval and maxSimultaneous — without touching active objects.
        StopAllCoroutines();
        StartCoroutine(SpawnLoop());

        Debug.Log($"[ObjectSpawner] Level changed — updated {_activeObjects.Count} " +
                  $"active objects to new speed range [{cfg.minFallSpeed},{cfg.maxFallSpeed}]");
    }

    private GameObject GetPrefab(FallingObjectType type) => type switch
    {
        FallingObjectType.Potion1 => potion1Prefab,
        FallingObjectType.Potion2 => potion2Prefab,
        FallingObjectType.Bomb    => bombPrefab,
        FallingObjectType.PowerUp => powerUpPrefab != null ? powerUpPrefab : potion1Prefab,
        _                         => null
    };
}
