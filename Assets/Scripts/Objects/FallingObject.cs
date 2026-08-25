using UnityEngine;

/// <summary>
/// Object types that can fall from the sky.
/// Potion1 / Potion2 = positive (score).
/// Bomb              = negative (lose life).
/// PowerUp           = positive (charges power-up meter).
/// </summary>
public enum FallingObjectType
{
    Potion1,
    Potion2,
    Bomb,
    PowerUp
}

/// <summary>
/// Attached to every falling object prefab.  
/// Moves downward at a configurable speed, triggers effects on collision with
/// the cauldron (via CauldronController), and penalises the player if it
/// reaches the ground destroy-plane without being caught.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class FallingObject : MonoBehaviour
{
    // ── Config set by spawner ─────────────────────────────────────────────────
    public FallingObjectType ObjectType { get; private set; }
    public float             FallSpeed  { get; private set; }

    // ── References ────────────────────────────────────────────────────────────
    private Rigidbody _rb;

    // ── Y position that counts as "hit ground without being caught" ───────────
    [SerializeField] private float destroyYThreshold = -5f;

    // ── Unity lifecycle ───────────────────────────────────────────────────────
    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        // We move it ourselves; disable gravity so physics doesn't fight us.
        _rb.useGravity  = false;
        _rb.isKinematic = false;
        _rb.constraints = RigidbodyConstraints.FreezeRotation
                        | RigidbodyConstraints.FreezePositionX
                        | RigidbodyConstraints.FreezePositionZ;
    }

    private void FixedUpdate()
    {
        _rb.linearVelocity = Vector3.down * FallSpeed;

        if (transform.position.y <= destroyYThreshold)
            HitGround();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by ObjectSpawner right after instantiation.</summary>
    public void Initialise(FallingObjectType type, float speed)
    {
        ObjectType = type;
        FallSpeed  = speed;
    }

    /// <summary>Called by CauldronController when this object enters the catch zone.</summary>
    public void Catch()
    {
        ApplyEffect();
        ObjectSpawner.Instance?.NotifyObjectRemoved();
        Destroy(gameObject);
    }

    // ── Internal ──────────────────────────────────────────────────────────────

    private void HitGround()
    {
        // Negative objects reaching the ground cost a life
        if (ObjectType == FallingObjectType.Bomb)
            GameManager.Instance?.LoseLife();

        ObjectSpawner.Instance?.NotifyObjectRemoved();
        Destroy(gameObject);
    }

    private void ApplyEffect()
    {
        var gm = GameManager.Instance;
        if (gm == null) return;

        var lm = LevelManager.Instance;

        switch (ObjectType)
        {
            case FallingObjectType.Potion1:
                int p1 = lm != null ? lm.ActiveConfig.potion1Points : 10;
                gm.AddScore(p1);
                break;

            case FallingObjectType.Potion2:
                int p2 = lm != null ? lm.ActiveConfig.potion2Points : 25;
                gm.AddScore(p2);
                break;

            case FallingObjectType.PowerUp:
                gm.ChargePowerUp();
                break;

            case FallingObjectType.Bomb:
                // A bomb that was *caught* counts as a life loss
                gm.LoseLife();
                break;
        }
    }

    // ── Trigger-based catch detection ─────────────────────────────────────────
    // (Alternative approach: the cauldron's trigger calls Catch() on us)
    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Cauldron"))
            Catch();
    }
}
