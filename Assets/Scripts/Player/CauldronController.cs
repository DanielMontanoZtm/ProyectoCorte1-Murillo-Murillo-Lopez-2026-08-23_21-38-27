using UnityEngine;

/// <summary>
/// Moves the cauldron left/right along the X axis.
///
/// LÍMITES RELATIVOS:
///   xRange define cuántas unidades puede moverse el caldero hacia cada lado
///   desde su posición inicial en la escena. Así no importa en qué coordenada
///   absoluta esté el caldero — siempre se mueve el mismo rango.
///
///   Ejemplo: si el caldero está en X=47.25 y xRange=4, puede ir de X=43.25
///   a X=51.25. Ajusta xRange para que coincida con el ancho de la boca del
///   cuarto visible en pantalla.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CauldronController : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Movement — X axis (horizontal on screen)")]
    [SerializeField] private float moveSpeed = 6f;

    [Tooltip("How many units the cauldron can move LEFT from its starting position.")]
    [SerializeField] private float xRangeLeft  = 4f;
    [Tooltip("How many units the cauldron can move RIGHT from its starting position.")]
    [SerializeField] private float xRangeRight = 4f;

    [Header("Smooth movement")]
    [SerializeField] private float accelerationTime = 0.08f;
    [SerializeField] private float decelerationTime = 0.12f;

    [Header("Power-up: speed boost")]
    [SerializeField] private float powerUpSpeedMultiplier = 1.8f;

    // ── Cached refs ───────────────────────────────────────────────────────────
    private Rigidbody          _rb;
    private MobileInputHandler _input;

    // ── Computed limits (set once in Start from initial position) ────────────
    private float _xMin;
    private float _xMax;

    // ── SmoothDamp state ──────────────────────────────────────────────────────
    private float _currentVelocityX;
    private float _smoothedInput;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity  = false;
        _rb.isKinematic = false;

        // Camera faces -Z (Y=180°) → visible horizontal = X → freeze Y and Z.
        _rb.constraints = RigidbodyConstraints.FreezePositionY
                        | RigidbodyConstraints.FreezePositionZ
                        | RigidbodyConstraints.FreezeRotation;

        gameObject.tag = "Cauldron";
        // Do NOT touch transform.position here.
    }

    private void Start()
    {
        _input = MobileInputHandler.Instance;
        if (_input == null)
            Debug.LogWarning("[CauldronController] MobileInputHandler not found.");

        // Compute absolute limits from the cauldron's actual scene position.
        // This way xMin/xMax are always correct regardless of world position.
        float startX = transform.position.x;
        _xMin = startX - xRangeLeft;
        _xMax = startX + xRangeRight;

        Debug.Log($"[CauldronController] Start X={startX:F2}  limits [{_xMin:F2}, {_xMax:F2}]");
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing)
        {
            _smoothedInput     = Mathf.SmoothDamp(_smoothedInput, 0f,
                                     ref _currentVelocityX, decelerationTime);
            _rb.linearVelocity = Vector3.zero;
            return;
        }

        // Camera Y=180° flips the perceived X direction, so we negate the input
        // to match player expectation: left arrow = move left on screen.
        float rawInput   = _input != null ? -_input.HorizontalInput : 0f;
        float smoothTime = Mathf.Abs(rawInput) > 0.01f ? accelerationTime : decelerationTime;

        _smoothedInput = Mathf.SmoothDamp(_smoothedInput, rawInput,
                             ref _currentVelocityX, smoothTime);

        float speed = moveSpeed;
        if (GameManager.Instance.PowerUpActive)
            speed *= powerUpSpeedMultiplier;

        float targetX = Mathf.Clamp(
            transform.position.x + _smoothedInput * speed * Time.fixedDeltaTime,
            _xMin, _xMax);

        _rb.MovePosition(new Vector3(targetX,
                                     transform.position.y,
                                     transform.position.z));
    }
}
