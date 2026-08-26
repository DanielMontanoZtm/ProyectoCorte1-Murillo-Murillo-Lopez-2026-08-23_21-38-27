using UnityEngine;

/// <summary>
/// Holds per-level configuration and exposes the active config to other systems.
/// Three levels with real differences in fall speed, spawn rate, and obstacle density,
/// satisfying the minimum requirements of the guidelines.
/// </summary>
[System.Serializable]
public class LevelConfig
{
    [Tooltip("Human-readable level name")]
    public string levelName = "Nivel 1";

    [Header("Falling speed (units/sec)")]
    public float minFallSpeed = 2f;
    public float maxFallSpeed = 4f;

    [Header("Spawn interval (seconds between spawns)")]
    public float spawnInterval = 1.5f;

    [Header("Object weights (relative probability)")]
    [Range(0, 100)] public int potion1Weight  = 45;  // Positive — low points
    [Range(0, 100)] public int potion2Weight  = 30;  // Positive — high points
    [Range(0, 100)] public int bombWeight     = 20;  // Negative — lose life
    [Range(0, 100)] public int powerUpWeight  = 5;   // Positive — charges power-up

    [Header("Number of simultaneous falling objects allowed")]
    public int maxSimultaneous = 4;

    [Header("Score reward")]
    public int potion1Points  = 10;
    public int potion2Points  = 25;
}

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    [Header("Level Configurations")]
    [SerializeField] private LevelConfig level1 = new LevelConfig
    {
        levelName       = "Nivel 1 – Aprendiz",
        minFallSpeed    = 4f,      // was 2 — noticeably faster from the start
        maxFallSpeed    = 6f,      // was 3.5
        spawnInterval   = 1.0f,   // was 1.8 — more frequent spawns
        potion1Weight   = 50,
        potion2Weight   = 25,
        bombWeight      = 20,
        powerUpWeight   = 5,
        maxSimultaneous = 4,      // was 3
        potion1Points   = 10,
        potion2Points   = 25
    };

    [SerializeField] private LevelConfig level2 = new LevelConfig
    {
        levelName       = "Nivel 2 – Hechicero",
        minFallSpeed    = 6f,      // was 3.5
        maxFallSpeed    = 9f,      // was 5.5
        spawnInterval   = 0.7f,   // was 1.2
        potion1Weight   = 40,
        potion2Weight   = 25,
        bombWeight      = 30,
        powerUpWeight   = 5,
        maxSimultaneous = 6,      // was 5
        potion1Points   = 10,
        potion2Points   = 25
    };

    [SerializeField] private LevelConfig level3 = new LevelConfig
    {
        levelName       = "Nivel 3 – Archimago",
        minFallSpeed    = 9f,      // was 5
        maxFallSpeed    = 13f,     // was 8
        spawnInterval   = 0.45f,  // was 0.7
        potion1Weight   = 35,
        potion2Weight   = 20,
        bombWeight      = 38,
        powerUpWeight   = 7,
        maxSimultaneous = 8,      // was 7
        potion1Points   = 10,
        potion2Points   = 25
    };

    public LevelConfig ActiveConfig { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        ActiveConfig = level1;
    }

    private void OnEnable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelChanged.AddListener(SetLevel);
    }

    private void OnDisable()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.OnLevelChanged.RemoveListener(SetLevel);
    }

    /// <summary>Switch to the config for the given level number (1, 2, or 3).</summary>
    public void SetLevel(int level)
    {
        ActiveConfig = level switch
        {
            2 => level2,
            3 => level3,
            _ => level1
        };

        Debug.Log($"[LevelManager] Switching to {ActiveConfig.levelName}");
    }

    /// <summary>
    /// Returns a random FallingObjectType based on the current level weights.
    /// </summary>
    public FallingObjectType GetRandomObjectType()
    {
        int total = ActiveConfig.potion1Weight
                  + ActiveConfig.potion2Weight
                  + ActiveConfig.bombWeight
                  + ActiveConfig.powerUpWeight;

        int roll = Random.Range(0, total);
        int acc  = 0;

        acc += ActiveConfig.potion1Weight;
        if (roll < acc) return FallingObjectType.Potion1;

        acc += ActiveConfig.potion2Weight;
        if (roll < acc) return FallingObjectType.Potion2;

        acc += ActiveConfig.bombWeight;
        if (roll < acc) return FallingObjectType.Bomb;

        return FallingObjectType.PowerUp;
    }
}
