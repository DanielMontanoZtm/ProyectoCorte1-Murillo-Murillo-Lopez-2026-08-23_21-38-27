using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Provides non-animated, purely code-driven feedback for every catch/miss event.
/// No HUD, no UI canvas, no animations required.
///
/// Effects implemented:
///   • Scale Punch  — quick scale-up → spring back to original size.
///   • Color Flash  — renderer tints briefly then returns to original color.
///   • Miss Shake   — quick lateral shake on the cauldron when a bomb hits ground.
///
/// Usage (called by FallingObject):
///   CollisionFeedback.NotifyCatch(fallingObject);
///   CollisionFeedback.NotifyMiss(fallingObject);
///
/// The singleton is optional — if no CollisionFeedback exists in the scene
/// the static calls are no-ops, so gameplay is never blocked by missing feedback.
/// </summary>
public class CollisionFeedback : MonoBehaviour
{
    // ── Singleton (optional — feedback is a nice-to-have) ────────────────────
    public static CollisionFeedback Instance { get; private set; }

    // ── Scale punch config ────────────────────────────────────────────────────
    [Header("Scale Punch (caught objects)")]
    [SerializeField] private float punchScaleMultiplier = 1.45f;
    [SerializeField] private float punchDuration        = 0.12f;  // grow phase
    [SerializeField] private float punchReturnDuration  = 0.18f;  // spring-back phase

    // ── Color flash config ────────────────────────────────────────────────────
    [Header("Color Flash (renderer tint)")]
    [SerializeField] private Color catchPotion1Color  = new Color(0.4f, 1f,  0.4f);   // soft green
    [SerializeField] private Color catchPotion2Color  = new Color(0.4f, 0.8f, 1f);    // soft blue
    [SerializeField] private Color catchPowerUpColor  = new Color(1f,  0.85f, 0f);    // gold
    [SerializeField] private Color catchBombColor     = new Color(0.6f, 1f,  0.6f);   // green (defused)
    [SerializeField] private Color missColor          = new Color(1f,  0.2f, 0.2f);   // red (missed bomb)
    [SerializeField] private float flashDuration      = 0.25f;

    // ── Cauldron shake config (on bomb miss) ──────────────────────────────────
    [Header("Cauldron Shake (bomb reaches ground)")]
    [SerializeField] private float shakeMagnitude  = 0.12f;
    [SerializeField] private float shakeDuration   = 0.35f;
    [SerializeField] private int   shakeIterations = 6;

    // ── Active coroutines tracking (to cancel stacked coroutines per object) ──
    // Separate dictionaries for scale and color so both can run simultaneously
    // on the same GameObject without one cancelling the other.
    // Using GameObject as key avoids GetInstanceID() which is obsolete in Unity 6.
    private readonly Dictionary<GameObject, Coroutine> _scaleCoroutines = new();
    private readonly Dictionary<GameObject, Coroutine> _colorCoroutines = new();

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    // ── Static entry points (called by FallingObject) ─────────────────────────

    /// <summary>Trigger catch feedback on the given object before it is pooled.</summary>
    public static void NotifyCatch(FallingObject fo)
    {
        if (Instance == null || fo == null) return;
        Instance.PlayCatchFeedback(fo);
    }

    /// <summary>Trigger miss feedback (bomb hit ground). Shakes the cauldron.</summary>
    public static void NotifyMiss(FallingObject fo)
    {
        if (Instance == null || fo == null) return;
        Instance.PlayMissFeedback(fo);
    }

    // ── Instance methods ──────────────────────────────────────────────────────

    private void PlayCatchFeedback(FallingObject fo)
    {
        Color flashColor = fo.ObjectType switch
        {
            FallingObjectType.Potion1 => catchPotion1Color,
            FallingObjectType.Potion2 => catchPotion2Color,
            FallingObjectType.PowerUp => catchPowerUpColor,
            FallingObjectType.Bomb    => catchBombColor,
            _                         => Color.white
        };

        // Scale punch on the caught object itself
        StartTrackedScale(fo.gameObject, ScalePunchRoutine(fo.transform));

        // Color flash on caught object
        var renderer = fo.GetComponentInChildren<Renderer>();
        if (renderer != null)
            StartTrackedColor(fo.gameObject, ColorFlashRoutine(renderer, flashColor));

        // For PowerUp: also flash the cauldron gold
        if (fo.ObjectType == FallingObjectType.PowerUp)
            FlashCauldron(catchPowerUpColor);
    }

    private void PlayMissFeedback(FallingObject fo)
    {
        // Color flash red on the missed bomb (briefly, before pooling)
        var renderer = fo.GetComponentInChildren<Renderer>();
        if (renderer != null)
            StartTrackedColor(fo.gameObject, ColorFlashRoutine(renderer, missColor));

        // Shake the cauldron
        ShakeCauldron();
    }

    // ── Cauldron helpers ──────────────────────────────────────────────────────

    private void FlashCauldron(Color color)
    {
        var cauldron = GameObject.FindWithTag("Cauldron");
        if (cauldron == null) return;
        var renderer = cauldron.GetComponentInChildren<Renderer>();
        if (renderer != null)
            StartCoroutine(ColorFlashRoutine(renderer, color));
    }

    private void ShakeCauldron()
    {
        var cauldron = GameObject.FindWithTag("Cauldron");
        if (cauldron == null) return;
        StartCoroutine(ShakeRoutine(cauldron.transform));
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator ScalePunchRoutine(Transform t)
    {
        if (t == null) yield break;

        Vector3 original = t.localScale;
        Vector3 target   = original * punchScaleMultiplier;

        // Grow
        float elapsed = 0f;
        while (elapsed < punchDuration)
        {
            if (t == null) yield break;
            t.localScale = Vector3.Lerp(original, target, elapsed / punchDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        // Spring back
        elapsed = 0f;
        while (elapsed < punchReturnDuration)
        {
            if (t == null) yield break;
            t.localScale = Vector3.Lerp(target, original,
                               EaseOutBack(elapsed / punchReturnDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (t != null) t.localScale = original;
    }

    private IEnumerator ColorFlashRoutine(Renderer renderer, Color flashColor)
    {
        if (renderer == null) yield break;

        // Cache original color of every material (some prefabs have multiple)
        var mats          = renderer.materials;
        var originalColors = new Color[mats.Length];
        for (int i = 0; i < mats.Length; i++)
            originalColors[i] = mats[i].HasProperty("_Color")
                ? mats[i].color : Color.white;

        // Set flash color
        foreach (var mat in mats)
            if (mat.HasProperty("_Color")) mat.color = flashColor;

        yield return new WaitForSeconds(flashDuration);

        // Restore — guard against object being pooled/disabled mid-coroutine
        if (renderer == null) yield break;
        for (int i = 0; i < mats.Length; i++)
            if (mats[i] != null && mats[i].HasProperty("_Color"))
                mats[i].color = originalColors[i];
    }

    private IEnumerator ShakeRoutine(Transform t)
    {
        if (t == null) yield break;

        Vector3 origin = t.localPosition;
        float stepTime = shakeDuration / (shakeIterations * 2f);

        for (int i = 0; i < shakeIterations; i++)
        {
            float dir = (i % 2 == 0) ? 1f : -1f;
            float fade = 1f - (i / (float)shakeIterations);  // diminishing shake

            Vector3 offset = new Vector3(dir * shakeMagnitude * fade, 0f, 0f);

            float elapsed = 0f;
            while (elapsed < stepTime)
            {
                if (t == null) yield break;
                t.localPosition = Vector3.Lerp(origin, origin + offset,
                                      elapsed / stepTime);
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        if (t != null) t.localPosition = origin;
    }

    // ── Coroutine tracking ────────────────────────────────────────────────────

    private void StartTrackedScale(GameObject key, IEnumerator routine)
    {
        if (_scaleCoroutines.TryGetValue(key, out var existing) && existing != null)
            StopCoroutine(existing);
        _scaleCoroutines[key] = StartCoroutine(routine);
    }

    private void StartTrackedColor(GameObject key, IEnumerator routine)
    {
        if (_colorCoroutines.TryGetValue(key, out var existing) && existing != null)
            StopCoroutine(existing);
        _colorCoroutines[key] = StartCoroutine(routine);
    }

    // ── Math helpers ──────────────────────────────────────────────────────────

    /// <summary>Ease-out-back curve for a bouncy spring-back feel.</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
