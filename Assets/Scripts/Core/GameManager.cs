using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Central game state machine. Manages score, lives, time, level progression,
/// win/lose conditions, and power-up resource. Single instance (singleton).
/// </summary>
public class GameManager : MonoBehaviour
{
    // ── Singleton ────────────────────────────────────────────────────────────
    public static GameManager Instance { get; private set; }

    // ── Events ───────────────────────────────────────────────────────────────
    public UnityEvent<int>   OnScoreChanged      = new UnityEvent<int>();
    public UnityEvent<int>   OnLivesChanged      = new UnityEvent<int>();
    public UnityEvent<float> OnTimeChanged        = new UnityEvent<float>();
    public UnityEvent<float> OnPowerUpChargeChanged = new UnityEvent<float>();
    public UnityEvent<int>   OnLevelChanged       = new UnityEvent<int>();
    public UnityEvent        OnGameOver           = new UnityEvent();
    public UnityEvent        OnGameWin            = new UnityEvent();
    public UnityEvent        OnGamePaused         = new UnityEvent();
    public UnityEvent        OnGameResumed        = new UnityEvent();

    // ── Serialized config ────────────────────────────────────────────────────
    [Header("Lives")]
    [SerializeField] private int startingLives = 3;

    [Header("Time (seconds per level)")]
    [SerializeField] private float levelDuration = 60f;

    [Header("Power-up")]
    [SerializeField] private float powerUpChargePerPotion = 0.25f; // 4 potions = full charge
    [SerializeField] private float powerUpDuration        = 5f;

    [Header("Score thresholds to advance level")]
    [SerializeField] private int scoreToLevel2 = 50;
    [SerializeField] private int scoreToLevel3 = 120;

    // ── State ────────────────────────────────────────────────────────────────
    public enum GameState { Menu, Playing, Paused, GameOver, Win }

    public GameState  CurrentState   { get; private set; } = GameState.Menu;
    public int        Score          { get; private set; }
    public int        Lives          { get; private set; }
    public float      TimeRemaining  { get; private set; }
    public int        CurrentLevel   { get; private set; } = 1;
    public float      PowerUpCharge  { get; private set; }   // 0..1
    public bool       PowerUpActive  { get; private set; }

    private float _powerUpTimer;

    // ── Unity lifecycle ──────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
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

    // ── Public API ───────────────────────────────────────────────────────────

    public void StartGame()
    {
        Score         = 0;
        Lives         = startingLives;
        TimeRemaining = levelDuration;
        CurrentLevel  = 1;
        PowerUpCharge = 0f;
        PowerUpActive = false;
        CurrentState  = GameState.Playing;

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

    /// <summary>Called when a negative object reaches the ground.</summary>
    public void LoseLife()
    {
        if (CurrentState != GameState.Playing) return;
        Lives = Mathf.Max(0, Lives - 1);
        OnLivesChanged.Invoke(Lives);
        if (Lives <= 0) TriggerGameOver();
    }

    /// <summary>Charge power-up meter from catching a power-up potion.</summary>
    public void ChargePowerUp()
    {
        if (CurrentState != GameState.Playing) return;
        PowerUpCharge = Mathf.Min(1f, PowerUpCharge + powerUpChargePerPotion);
        OnPowerUpChargeChanged.Invoke(PowerUpCharge);
    }

    /// <summary>Player taps the HUD power-up button — activates if fully charged.</summary>
    public bool TryActivatePowerUp()
    {
        if (CurrentState != GameState.Playing) return false;
        if (PowerUpActive || PowerUpCharge < 1f) return false;

        PowerUpActive = true;
        _powerUpTimer = powerUpDuration;
        PowerUpCharge = 0f;
        OnPowerUpChargeChanged.Invoke(PowerUpCharge);
        return true;
    }

    private void DeactivatePowerUp()
    {
        PowerUpActive = false;
        _powerUpTimer = 0f;
    }

    public void PauseGame()
    {
        if (CurrentState != GameState.Playing) return;
        CurrentState      = GameState.Paused;
        Time.timeScale    = 0f;
        OnGamePaused.Invoke();
    }

    public void ResumeGame()
    {
        if (CurrentState != GameState.Paused) return;
        CurrentState   = GameState.Playing;
        Time.timeScale = 1f;
        OnGameResumed.Invoke();
    }

    // ── Internal helpers ─────────────────────────────────────────────────────

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

            // Winning condition: cleared level 3 threshold
            if (CurrentLevel == 3 && Score >= scoreToLevel3 + 50)
                TriggerWin();
        }
    }
}
