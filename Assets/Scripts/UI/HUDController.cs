using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

/// <summary>
/// Functional HUD. Displays:
///   • Score
///   • Remaining lives (heart icons)
///   • Time countdown
///   • Power-up charge bar + activation button
///   • Current level label
///   • Feedback flash (item caught / missed)
///
/// All fields are wired in the Inspector to your Canvas UI elements.
/// Uses TextMeshPro — if not available, swap TMP_Text for Text.
/// </summary>
public class HUDController : MonoBehaviour
{
    // ── Score ─────────────────────────────────────────────────────────────────
    [Header("Score")]
    [SerializeField] private TMP_Text scoreText;

    // ── Lives ─────────────────────────────────────────────────────────────────
    [Header("Lives")]
    [SerializeField] private Image[] heartIcons;        // array of heart sprites
    [SerializeField] private Sprite  heartFull;
    [SerializeField] private Sprite  heartEmpty;

    // ── Timer ─────────────────────────────────────────────────────────────────
    [Header("Timer")]
    [SerializeField] private TMP_Text timerText;
    [SerializeField] private Color    normalTimerColor  = Color.white;
    [SerializeField] private Color    urgentTimerColor  = Color.red;
    [SerializeField] private float    urgentThreshold   = 10f;

    // ── Power-up state indicator ──────────────────────────────────────────────
    [Header("Power-up")]
    [Tooltip("Text that appears while the power-up effect is active.")]
    [SerializeField] private GameObject powerUpActiveIndicator;

    // ── Level ─────────────────────────────────────────────────────────────────
    [Header("Level")]
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private GameObject levelUpBanner;   // brief "NIVEL 2!" flash
    [SerializeField] private float     bannerDuration = 2f;

    // ── Feedback flash ────────────────────────────────────────────────────────
    [Header("Catch feedback")]
    [SerializeField] private TMP_Text  feedbackText;
    [SerializeField] private float     feedbackDuration = 0.8f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private bool _subscribed = false;

    private void Update()
    {
        // Retry subscription every frame until GameManager exists.
        // This handles cases where GameManager is created after HUDController.
        if (!_subscribed) TrySubscribe();
    }

    private void TrySubscribe()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnScoreChanged.AddListener(UpdateScore);
        gm.OnLivesChanged.AddListener(UpdateLives);
        gm.OnTimeChanged.AddListener(UpdateTimer);
        gm.OnPowerUpStateChanged.AddListener(UpdatePowerUpIndicator);
        gm.OnLevelChanged.AddListener(UpdateLevel);
        gm.OnGameOver.AddListener(OnGameOver);
        gm.OnGameWin.AddListener(OnGameWin);

        _subscribed = true;
        Debug.Log("[HUDController] Subscribed to GameManager events.");
    }

    private void OnDestroy()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        gm.OnScoreChanged.RemoveListener(UpdateScore);
        gm.OnLivesChanged.RemoveListener(UpdateLives);
        gm.OnTimeChanged.RemoveListener(UpdateTimer);
        gm.OnPowerUpStateChanged.RemoveListener(UpdatePowerUpIndicator);
        gm.OnLevelChanged.RemoveListener(UpdateLevel);
        gm.OnGameOver.RemoveListener(OnGameOver);
        gm.OnGameWin.RemoveListener(OnGameWin);
    }

    private void OnGameOver() { }   // UIManager handles the panel — no-op here
    private void OnGameWin()  { }   // UIManager handles the panel — no-op here

    // ── Update callbacks ──────────────────────────────────────────────────────

    private void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"Puntaje: {score}";
    }

    private void UpdateLives(int lives)
    {
        for (int i = 0; i < heartIcons.Length; i++)
        {
            if (heartIcons[i] == null) continue;
            bool alive = i < lives;

            if (heartFull != null && heartEmpty != null)
            {
                // Use sprites if assigned
                heartIcons[i].sprite = alive ? heartFull : heartEmpty;
                heartIcons[i].color  = Color.white;
            }
            else
            {
                // No sprites — use color only: red = alive, dark grey = lost
                heartIcons[i].color = alive
                    ? new Color(0.9f, 0.2f, 0.2f, 1f)     // red
                    : new Color(0.3f, 0.3f, 0.3f, 0.5f);  // dark grey transparent
            }
        }
    }

    private void UpdateTimer(float seconds)
    {
        if (timerText == null) return;

        int mins = Mathf.FloorToInt(seconds / 60f);
        int secs = Mathf.FloorToInt(seconds % 60f);
        timerText.text  = $"{mins:0}:{secs:00}";
        timerText.color = seconds <= urgentThreshold ? urgentTimerColor : normalTimerColor;
    }

    private void UpdatePowerUpIndicator(bool isActive)
    {
        if (powerUpActiveIndicator != null)
            powerUpActiveIndicator.SetActive(isActive);
    }

    private void UpdateLevel(int level)
    {
        if (levelText != null)
            levelText.text = $"Nivel {level}";

        if (levelUpBanner != null)
            StartCoroutine(ShowBanner(level));
    }

    // ── Public: feedback flash (called from FallingObject or CauldronController) ──

    public void ShowFeedback(string message, Color color)
    {
        if (feedbackText == null) return;
        StopCoroutine(nameof(FeedbackRoutine));
        StartCoroutine(FeedbackRoutine(message, color));
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator FeedbackRoutine(string message, Color color)
    {
        feedbackText.text  = message;
        feedbackText.color = color;
        feedbackText.gameObject.SetActive(true);
        yield return new WaitForSeconds(feedbackDuration);
        feedbackText.gameObject.SetActive(false);
    }

    private IEnumerator ShowBanner(int level)
    {
        levelUpBanner.SetActive(true);
        var t = levelUpBanner.GetComponentInChildren<TMP_Text>();
        if (t != null) t.text = $"¡NIVEL {level}!";
        yield return new WaitForSeconds(bannerDuration);
        levelUpBanner.SetActive(false);
    }
}
