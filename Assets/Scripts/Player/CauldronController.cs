using UnityEngine;

/// <summary>
/// Moves the cauldron left/right along a fixed horizontal lane.
/// Input comes from MobileInputHandler (virtual joystick or touch drag).
/// The cauldron is the "player" — no jumping since it slides on the ground.
///
/// Tag this GameObject "Cauldron" so FallingObject.OnTriggerEnter can find it.
/// Add a Collider set as Trigger to the top opening of the cauldron mesh.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class CauldronController : MonoBehaviour
{
    // ── Config ────────────────────────────────────────────────────────────────
    [Header("Movement")]
    [SerializeField] private float moveSpeed   = 6f;
    [SerializeField] private float xMin        = -4.5f;
    [SerializeField] private float xMax        =  4.5f;

    [Header("Power-up: speed boost")]
    [SerializeField] private float powerUpSpeedMultiplier = 1.8f;

    // ── Cached refs ────────────────────────────────────────────────────────────
    private Rigidbody        _rb;
    private MobileInputHandler _input;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity  = false;
        _rb.isKinematic = false;
        // Lock everything except X translation
        _rb.constraints = RigidbodyConstraints.FreezePositionY
                        | RigidbodyConstraints.FreezePositionZ
                        | RigidbodyConstraints.FreezeRotation;

        gameObject.tag = "Cauldron";
    }

    private void Start()
    {
        _input = MobileInputHandler.Instance;
        if (_input == null)
            Debug.LogWarning("[CauldronController] MobileInputHandler not found in scene.");
    }

    private void FixedUpdate()
    {
        if (GameManager.Instance?.CurrentState != GameManager.GameState.Playing)
        {
            _rb.linearVelocity = Vector3.zero;
            return;
        }

        float horizontal = _input != null ? _input.HorizontalInput : 0f;

        float speed = moveSpeed;
        if (GameManager.Instance.PowerUpActive)
            speed *= powerUpSpeedMultiplier;

        float targetX = Mathf.Clamp(transform.position.x + horizontal * speed * Time.fixedDeltaTime,
                                    xMin, xMax);

        _rb.MovePosition(new Vector3(targetX,
                                     transform.position.y,
                                     transform.position.z));
    }
}
