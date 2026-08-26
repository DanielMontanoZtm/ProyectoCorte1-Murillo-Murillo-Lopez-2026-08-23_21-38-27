using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Reads device accelerometer input and exposes a HorizontalInput value (-1 to 1).
///
/// CONTROL SCHEME — Accelerometer (tilt):
///   Tilt phone right → caldero moves right.
///   Tilt phone left  → caldero moves left.
///   Hold phone upright (portrait) = neutral position.
///
/// The raw accelerometer X axis is mapped through a dead zone and then
/// normalized to -1..1 so CauldronController gets clean input.
///
/// EDITOR FALLBACK:
///   ← → arrow keys or A/D work in the editor since the accelerometer
///   is not available on PC.
///
/// CALIBRATION:
///   neutralX      — raw accelerometer value at rest (usually near 0).
///                   Tap "Calibrate" button in Inspector at runtime to set it.
///   tiltRange     — how many units of tilt = full (-1 or 1) input.
///                   Smaller = more sensitive. Default 0.4 works well.
///   deadZone      — tilt smaller than this is ignored (prevents drift).
/// </summary>
public class MobileInputHandler : MonoBehaviour
{
    public static MobileInputHandler Instance { get; private set; }

    [Header("Accelerometer settings")]
    [Tooltip("Dead zone — tilts smaller than this are ignored (prevents drift).")]
    [SerializeField] private float deadZone   = 0.05f;

    [Tooltip("How much tilt (in g units) maps to full (-1 or 1) input. " +
             "Lower = more sensitive. Typical range: 0.25 – 0.5.")]
    [SerializeField] private float tiltRange  = 0.35f;

    [Tooltip("Neutral X offset. Tap Calibrate at runtime to set automatically.")]
    [SerializeField] private float neutralX   = 0f;

    [Header("Smoothing")]
    [Tooltip("Low-pass filter strength (0 = no smoothing, 1 = no movement). " +
             "0.1 – 0.2 removes jitter without adding lag.")]
    [SerializeField] private float smoothing  = 0.15f;

    // ── Exposed value consumed by CauldronController ─────────────────────────
    public float HorizontalInput { get; private set; }

    // ── Internal ──────────────────────────────────────────────────────────────
    private float      _rawInput;
    private Accelerometer _accel;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnEnable()
    {
        // Enable the accelerometer sensor through the new Input System
        _accel = Accelerometer.current;
        if (_accel != null)
        {
            InputSystem.EnableDevice(_accel);
            Debug.Log("[MobileInputHandler] Accelerometer enabled.");
        }
        else
        {
            Debug.LogWarning("[MobileInputHandler] No accelerometer found on this device.");
        }
    }

    private void OnDisable()
    {
        if (_accel != null)
            InputSystem.DisableDevice(_accel);
    }

    private void Update()
    {
        float raw = 0f;

#if UNITY_EDITOR
        // Keyboard fallback in the editor
        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.isPressed  || keyboard.aKey.isPressed) raw = -1f;
            if (keyboard.rightArrowKey.isPressed || keyboard.dKey.isPressed) raw =  1f;
        }
        // Suppress unused-field warnings — these are used in the device build
        _ = deadZone;
        _ = tiltRange;
#else
        // Real device — read accelerometer
        if (_accel != null && _accel.enabled)
        {
            // acceleration.x:
            //   Portrait, phone upright → near 0
            //   Tilt right              → positive
            //   Tilt left               → negative
            float tilt = _accel.acceleration.ReadValue().x - neutralX;

            // Apply dead zone
            if (Mathf.Abs(tilt) < deadZone)
                tilt = 0f;
            else
                tilt -= Mathf.Sign(tilt) * deadZone; // remove dead zone offset

            // Normalize to -1..1 based on tiltRange
            raw = Mathf.Clamp(tilt / tiltRange, -1f, 1f);
        }
#endif

        // Low-pass filter to remove jitter
        _rawInput     = Mathf.Lerp(_rawInput, raw, 1f - smoothing);
        HorizontalInput = _rawInput;
    }

    // ── Runtime calibration ───────────────────────────────────────────────────

    /// <summary>
    /// Call this (e.g. from a UI button) while the player holds the phone
    /// in their natural playing position. Sets the current X reading as neutral.
    /// </summary>
    public void Calibrate()
    {
        if (_accel != null && _accel.enabled)
        {
            neutralX = _accel.acceleration.ReadValue().x;
            Debug.Log($"[MobileInputHandler] Calibrated. neutralX = {neutralX:F3}");
        }
    }
}
