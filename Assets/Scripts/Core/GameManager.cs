using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central game state machine. Manages score, lives, time, level progression,
/// win/lose conditions, and power-up resource. Single instance (singleton).
///
/// CAMBIO v2: El power-up se activa de forma INSTANTÁNEA al recoger el objeto
/// de power-up (FallingObjectType.PowerUp).  Ya no existe un botón de HUD que
/// lo active; se eliminó TryActivatePowerUp() y se reemplazó por
/// ActivatePowerUpInstant().  El sistema de "carga" (charge) se conserva
/// internamente para que el HUD pueda mostrarlo en etapas posteriores, pero
/// la activación es automática cuando la carga llega al 100%.
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Singleton ─────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ── Events ────────────────────────────────────────────────────────────────
    public UnityEvent<int>   OnScoreChanged         = new();
    public UnityEvent<int>   OnLivesChanged         = new();
    public UnityEvent<float> OnTimeChanged          = new();
    public UnityEvent<float> OnPowerUpChargeChanged = new();
    public UnityEvent<int>   OnLevelChanged         = new();
    public UnityEvent        OnGameOver             = new();
    public UnityEvent        OnGameWin              = new();
    public UnityEvent        OnGamePaused           = new();
    public UnityEvent        OnGameResumed          = new();
    /// <summary>Fired when a power-up becomes active (true) or expires (false).</summary>
    public UnityEvent<bool>  OnPowerUpStateChanged  = new();

    // ── Serialized config ─────────────────────────────────────────────────────
    [Header("Lives")]
    [SerializeField] private int startingLives = 3;

    [Header("Time (seconds per level)")]
    [SerializeField] private float levelDuration = 60f;

    [Header("Power-up")]
    [Tooltip("How much charge one PowerUp object adds (0–1).  4 objects = full charge.")]
    [SerializeField] private float powerUpChargePerPotion = 0.25f;
    [SerializeField] private float powerUpDuration        = 5f;

    [Header("Score thresholds to advance level")]
    [SerializeField] private int scoreToLevel2 = 50;
    [SerializeField] private int scoreToLevel3 = 120;

    // ── State ─────────────────────────────────────────────────────────────────
    public enum GameState { Menu, Playing, Paused, GameOver, Win }

    public GameState CurrentState  { get; private set; } = GameState.Menu;
    public int       Score         { get; private set; }
    public int       Lives         { get; private set; }
    public float     TimeRemaining { get; private set; }
    public int       CurrentLevel  { get; private set; } = 1;
    /// <summary>Charge level 0–1.  Reaches 1 after <c>powerUpChargePerPotion</c> accumulations.</summary>
    public float     PowerUpCharge { get; private set; }
    public bool      PowerUpActive { get; private set; }

    private float _powerUpTimer;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        // DontDestroyOnLoad only makes sense in a real build.
        // In the editor it causes stale instances between Play sessions.
#if !UNITY_EDITOR
        DontDestroyOnLoad(gameObject);
#endif
    }

    private void Update()
    {
        if (CurrentState != GameState.Playing) return;

        // Countdown timer
        TimeRemaining -= Time.deltaTime;
        OnTimeChanged.Invoke(TimeRemaining);

        if (TimeRemaining <= 0f)
        {
            TimeRemaining = 0f;
            TriggerGameOver();
            return;
        }

        // Power-up active timer
        if (PowerUpActive)
        {
            _powerUpTimer -= Time.deltaTime;
            if (_powerUpTimer <= 0f) DeactivatePowerUp();
        }
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void StartGame()
    {
        Score         = 0;
        Lives         = startingLives;
        TimeRemaining = levelDuration;
        CurrentLevel  = 1;
        PowerUpCharge = 0f;
        PowerUpActive = false;

        CurrentState = GameState.Playing;

        OnScoreChanged.Invoke(Score);
        OnLivesChanged.Invoke(Lives);
        OnTimeChanged.Invoke(TimeRemaining);
        OnPowerUpChargeChanged.Invoke(PowerUpCharge);
        OnLevelChanged.Invoke(CurrentLevel);
    }

    /// <summary>Called when the player catches a positive object.</summary>
    public void AddScore(int points)
    {
        if (CurrentState != GameState.Playing) return;
        Score += points;
        OnScoreChanged.Invoke(Score);
        CheckLevelProgression();
    }

    /// <summary>Called when a negative object reaches the ground (missed).</summary>
    public void LoseLife()
    {
        if (CurrentState != GameState.Playing) return;
        Lives = Mathf.Max(0, Lives - 1);
        OnLivesChanged.Invoke(Lives);
        if (Lives <= 0) TriggerGameOver();
    }

    /// <summary>
    /// Called by FallingObject when a PowerUp type is caught.
    /// Accumulates charge and activates the power-up INSTANTLY when full.
    /// No HUD button required.
    /// </summary>
    public void ChargePowerUp()
    {
        if (CurrentState != GameState.Playing) return;

        PowerUpCharge = Mathf.Min(1f, PowerUpCharge + powerUpChargePerPotion);
        OnPowerUpChargeChanged.Invoke(PowerUpCharge);

        // Auto-activate when fully charged
        if (PowerUpCharge >= 1f)
            ActivatePowerUpInstant();
    }

    /// <summary>
    /// Activates the power-up immediately (no button needed).
    /// Safe to call even if already active — resets the timer.
    /// </summary>
    public void ActivatePowerUpInstant()
    {
        PowerUpActive = true;
        _powerUpTimer = powerUpDuration;
        PowerUpCharge = 0f;

        OnPowerUpChargeChanged.Invoke(PowerUpCharge);
        OnPowerUpStateChanged.Invoke(true);

        Debug.Log("[GameManager] Power-up activated instantly!");
    }

    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState   = GameState.Paused;
        Time.timeScale = 0f;
        OnGamePaused.Invoke();
    }

    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;
        CurrentState   = GameState.Playing;
        Time.timeScale = 1f;
        OnGameResumed.Invoke();
    }

    // ── Internal helpers ──────────────────────────────────────────────────────

    private void DeactivatePowerUp()
    {
        PowerUpActive = false;
        _powerUpTimer = 0f;
        OnPowerUpStateChanged.Invoke(false);
        Debug.Log("[GameManager] Power-up expired.");
    }

    private void TriggerGameOver()
    {
        CurrentState = GameState.GameOver;
        OnGameOver.Invoke();
    }

    private void TriggerWin()
    {
        CurrentState = GameState.Win;
        OnGameWin.Invoke();
    }

    private void CheckLevelProgression()
    {
        int newLevel = CurrentLevel;
        if      (Score >= scoreToLevel3 && CurrentLevel < 3) newLevel = 3;
        else if (Score >= scoreToLevel2 && CurrentLevel < 2) newLevel = 2;

        if (newLevel != CurrentLevel)
        {
            CurrentLevel = newLevel;
            OnLevelChanged.Invoke(CurrentLevel);

            if (CurrentLevel == 3 && Score >= scoreToLevel3 + 50)
                TriggerWin();
        }
    }
}
