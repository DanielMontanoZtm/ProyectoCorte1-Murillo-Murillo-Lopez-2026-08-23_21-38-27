using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// Controls the visibility of all UI panels:
///   • Main menu
///   • HUD (in-game)
///   • Pause menu
///   • Game-over screen  (with final score + retry)
///   • Win screen        (with final score + next-level / menu)
///
/// Panels are plain GameObjects with CanvasGroups; set them in the Inspector.
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    // ── Panel references ──────────────────────────────────────────────────────
    [Header("Panels")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject hudPanel;
    [SerializeField] private GameObject pausePanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private GameObject winPanel;

    // ── Text fields ───────────────────────────────────────────────────────────
    [Header("Game-over labels")]
    [SerializeField] private TMP_Text gameOverScoreText;
    [SerializeField] private TMP_Text gameOverMessageText;

    [Header("Win labels")]
    [SerializeField] private TMP_Text winScoreText;
    [SerializeField] private TMP_Text winMessageText;

    // ── Scene names ───────────────────────────────────────────────────────────
    [Header("Scene management")]
    [Tooltip("Scene to reload when the player presses Retry.")]
    [SerializeField] private string gameSceneName = "Main";
    [Tooltip("Scene to load when the player presses Main Menu.")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        ShowHUD();
    }

    private bool _subscribed = false;

    private void Update()
    {
        if (!_subscribed) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnGameOver.AddListener(ShowGameOver);
        gm.OnGameWin.AddListener(ShowWin);
        gm.OnGamePaused.AddListener(ShowPause);
        gm.OnGameResumed.AddListener(HidePause);

        _subscribed = true;
        Debug.Log("[UIManager] Subscribed to GameManager events.");
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        gm.OnGameOver.RemoveListener(ShowGameOver);
        gm.OnGameWin.RemoveListener(ShowWin);
        gm.OnGamePaused.RemoveListener(ShowPause);
        gm.OnGameResumed.RemoveListener(HidePause);
    }

    // ── Panel control ─────────────────────────────────────────────────────────

    public void ShowMainMenu()
    {
        SetAll(false);
        SetActive(mainMenuPanel, true);
    }

    public void ShowHUD()
    {
        SetAll(false);
        SetActive(hudPanel, true);
    }

    private void ShowPause()
    {
        SetActive(pausePanel, true);
    }

    private void HidePause()
    {
        SetActive(pausePanel, false);
    }

    private void ShowGameOver()
    {
        SetActive(hudPanel, false);
        SetActive(gameOverPanel, true);

        int score = GameManager.Instance?.Score ?? 0;
        if (gameOverScoreText  != null) gameOverScoreText.text  = $"Puntaje: {score}";
        if (gameOverMessageText != null) gameOverMessageText.text = "¡Se acabó el tiempo!";
    }

    private void ShowWin()
    {
        SetActive(hudPanel, false);
        SetActive(winPanel, true);

        int score = GameManager.Instance?.Score ?? 0;
        if (winScoreText    != null) winScoreText.text  = $"Puntaje final: {score}";
        if (winMessageText  != null) winMessageText.text = "¡Ganaste, archimago!";
    }

    // ── Button callbacks (wire these to UI Buttons) ───────────────────────────

    /// <summary>Play button on main menu.</summary>
    public void OnPlayButtonPressed()
    {
        GameManager.Instance?.StartGame();
        ObjectSpawner.Instance?.StartSpawning();
        ShowHUD();
    }

    /// <summary>Pause button on HUD.</summary>
    public void OnPauseButtonPressed()
    {
        GameManager.Instance?.PauseGame();
    }

    /// <summary>Resume button on pause panel.</summary>
    public void OnResumeButtonPressed()
    {
        GameManager.Instance?.ResumeGame();
    }

    /// <summary>Retry button — reloads the game scene.</summary>
    public void OnRetryButtonPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>Main menu button — goes to the menu scene.</summary>
    public void OnMainMenuButtonPressed()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetActive(GameObject panel, bool active)
    {
        if (panel != null) panel.SetActive(active);
    }

    private void SetAll(bool active)
    {
        SetActive(mainMenuPanel, active);
        SetActive(hudPanel,      active);
        SetActive(pausePanel,    active);
        SetActive(gameOverPanel, active);
        SetActive(winPanel,      active);
    }
}
