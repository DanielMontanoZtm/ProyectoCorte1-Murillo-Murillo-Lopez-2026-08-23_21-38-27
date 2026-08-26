using UnityEngine;

public enum FallingObjectType { Potion1, Potion2, Bomb, PowerUp }

/// <summary>
/// Falling object — moves straight down in Y, detects catch via trigger
/// overlap with the Cauldron collider.
///
/// COLISIÓN ROBUSTA:
///   Unity requiere que AL MENOS UNO de los dos objetos en un trigger tenga
///   un Rigidbody non-kinematic para que OnTriggerEnter se dispare.
///   Este objeto tiene su propio Rigidbody (non-kinematic, sin gravedad),
///   así que OnTriggerEnter funciona aunque el caldero sea estático o
///   kinematic.
///
///   Adicionalmente, usamos detección por distancia en FixedUpdate como
///   fallback: si el objeto pasa por la misma Y que el caldero y la
///   diferencia horizontal es menor que catchRadius, se considera atrapado.
///
/// BOMBAS:
///   - Si el jugador las ATRAPA: pierden una vida (son dañinas).
///   - Si llegan al suelo sin ser atrapadas: también pierden una vida.
///   El jugador debe esquivarlas, no atraparlas.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class FallingObject : MonoBehaviour
{
    [Header("Ground threshold")]
    [Tooltip("Y below which the object counts as having hit the ground. " +
             "Should be just below the cauldron's Y position (cauldron Y ≈ 0).")]
    [SerializeField] private float destroyYThreshold = -1.5f;

    [Header("Fallback catch detection")]
    [Tooltip("Disable this if trigger-based detection is working correctly. " +
             "The proximity fallback can cause false catches if the catchRadius is too large.")]
    [SerializeField] private bool useProximityFallback = false;
    [SerializeField] private float catchRadius    = 0.8f;
    [SerializeField] private float catchYTolerance = 0.5f;

    // ── Runtime state ─────────────────────────────────────────────────────────
    public FallingObjectType ObjectType { get; private set; }
    public float             FallSpeed  { get; private set; }

    private Rigidbody  _rb;
    private bool       _alive;
    private Transform  _cauldronTransform;   // cached at Start for proximity check

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity  = false;
        _rb.isKinematic = false;

        // Freeze X and Z — object only moves in Y (straight down).
        _rb.constraints = RigidbodyConstraints.FreezeRotation
                        | RigidbodyConstraints.FreezePositionX
                        | RigidbodyConstraints.FreezePositionZ;

        // Must be a trigger so OnTriggerEnter fires with the cauldron
        var col = GetComponent<Collider>();
        if (col != null) col.isTrigger = true;
    }

    private void Start()
    {
        CacheCauldron();
    }

    private void OnEnable()
    {
        _alive = true;
        CacheCauldron();
    }

    private void OnDisable()
    {
        _alive = false;
        _rb.linearVelocity = Vector3.zero;
    }

    private void CacheCauldron()
    {
        if (_cauldronTransform != null) return;
        var go = GameObject.FindWithTag("Cauldron");
        if (go != null) _cauldronTransform = go.transform;
    }

    // ── FixedUpdate: fall + ground check + proximity catch ───────────────────
    private void FixedUpdate()
    {
        if (!_alive) return;

        _rb.linearVelocity = Vector3.down * FallSpeed;

        // ── Proximity-based catch fallback ─────────────────────────────────
        if (useProximityFallback && _cauldronTransform != null)
        {
            float dy = Mathf.Abs(transform.position.y - _cauldronTransform.position.y);
            float dx = Mathf.Abs(transform.position.x - _cauldronTransform.position.x);
            if (dy < catchYTolerance && dx < catchRadius)
            {
                Catch();
                return;
            }
        }

        // ── Ground check ───────────────────────────────────────────────────
        if (transform.position.y <= destroyYThreshold)
            HitGround();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    public void Initialise(FallingObjectType type, float speed, Vector3 spawnPosition)
    {
        ObjectType           = type;
        FallSpeed            = speed;
        transform.position   = spawnPosition;
        transform.localScale = Vector3.one;
        _alive               = true;
        _rb.linearVelocity   = Vector3.zero;
        CacheCauldron();
    }

    /// <summary>
    /// Updates the fall speed of an already-active object (called on level change).
    /// The object keeps falling — only its speed changes.
    /// </summary>
    public void UpdateSpeed(float newSpeed)
    {
        FallSpeed = newSpeed;
    }

    public void Catch()
    {
        if (!_alive) return;
        _alive = false;

        ApplyCatchEffect();
        CollisionFeedback.NotifyCatch(this);
        ReturnToPool();
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void HitGround()
    {
        if (!_alive) return;
        _alive = false;

        // Move object out of play area immediately to prevent late triggers.
        transform.position = new Vector3(transform.position.x, -999f, transform.position.z);
        _rb.linearVelocity = Vector3.zero;

        // A bomb that reaches the ground WITHOUT being caught = player dodged it.
        // No penalty. Damage only happens if the cauldron catches the bomb (ApplyCatchEffect).
        // If you want bombs to punish the player for NOT catching them,
        // uncomment the block below.
        //
        // if (ObjectType == FallingObjectType.Bomb)
        // {
        //     GameManager.Instance?.LoseLife();
        //     CollisionFeedback.NotifyMiss(this);
        // }

        ReturnToPool();
    }

    private void ApplyCatchEffect()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;
        var lm = LevelManager.Instance;

        switch (ObjectType)
        {
            case FallingObjectType.Potion1:
                gm.AddScore(lm != null ? lm.ActiveConfig.potion1Points : 10);
                Debug.Log("[FallingObject] Potion1 caught → +score");
                break;

            case FallingObjectType.Potion2:
                gm.AddScore(lm != null ? lm.ActiveConfig.potion2Points : 25);
                Debug.Log("[FallingObject] Potion2 caught → +score");
                break;

            case FallingObjectType.PowerUp:
                gm.ChargePowerUp();
                Debug.Log("[FallingObject] PowerUp caught → ChargePowerUp");
                break;

            case FallingObjectType.Bomb:
                // Catching a bomb hurts — the player should dodge, not catch
                gm.LoseLife();
                Debug.Log("[FallingObject] Bomb CAUGHT → LoseLife");
                break;
        }
    }

    private void ReturnToPool()
    {
        ObjectSpawner.Instance?.ReturnToPool(this);
    }

    // ── Trigger detection (primary method) ───────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        // Double-check _alive here — HitGround may have already set it false
        // in the same frame that the cauldron slides over the landed object.
        if (!_alive) return;
        if (other.CompareTag("Cauldron"))
        {
            Debug.Log($"[FallingObject] OnTriggerEnter with Cauldron — type={ObjectType}");
            Catch();
        }
    }
}
