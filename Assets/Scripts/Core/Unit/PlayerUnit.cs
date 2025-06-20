/*********************************************************
 *  PlayerUnit.cs – crisp Terraria-style controller  (v5)
 *                 • left-click = use / place
 *                 • automatic sprite-flip
 *                 • “step-up” over a 1-tile ledge
 *  
 *  NOTE: All wall-related checks (slide, grab, jump, gizmos)
 *        have been completely removed.
 *********************************************************/
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerUnit : Entity
{
    /* ───────── movement tunables ───────── */
    [Header("Horizontal")]
    [SerializeField] float topSpeed       = 8f;
    [SerializeField] float accelGround    = 90f;
    [SerializeField] float decelGround    = 110f;
    [SerializeField] float accelAir       = 55f;
    [SerializeField] float decelAir       = 55f;

    [Header("Jump & fall")]
    [SerializeField] float jumpPower      = 14f;
    [SerializeField] float coyoteTime     = 0.12f;
    [SerializeField] float jumpBufferTime = 0.15f;
    [SerializeField] float jumpCutFactor  = 0.5f;
    [SerializeField] float gravityRise    = 2.5f;
    [SerializeField] float gravityFall    = 4.5f;
    [SerializeField] float fastFallMult   = 1.5f;
    [SerializeField] float apexAccelBoost = 2f;   // extra x accel near apex
    [SerializeField] float apexThreshold  = 1.5f; // y-velocity mag below which boost starts

    [Header("Ground check")]
    [SerializeField] Transform groundProbe;
    [SerializeField] float probeRadius = 0.05f;
    [SerializeField] LayerMask groundMask = ~0;

    [Header("Step-up (auto-climb)")]
    [SerializeField] float stepHeight        = 1.0f;   // full block
    [SerializeField] float stepCheckDistance = 0.05f;  // ray length in front of feet

    [Header("Audio")]
    [SerializeField] AudioClip footstepClip;

    [Header("Template / meta-data")]
    [SerializeField] public UnitTemplate template;   // assign in prefab or at runtime



    /* ───────── components ───────── */
    Rigidbody2D     rb;
    AudioSource     sfx;
    SpriteRenderer  sr;
    Collider2D      col;

    /* ───────── inventory ───────── */
    public Inventory Inventory { get; private set; }

    /* ───────── state ───────── */
    float targetX;
    float coyoteTimer;
    float jumpBufferTimer;
    bool  grounded;

    bool  facingRight = true;

    /* ───────── setup ───────── */
    protected override void Awake()
    {
        base.Awake();

        rb  = GetComponent<Rigidbody2D>();
        rb.gravityScale   = gravityRise;                      // start with rise gravity
        rb.interpolation  = RigidbodyInterpolation2D.Interpolate;

        sr  = GetComponent<SpriteRenderer>() ?? GetComponentInChildren<SpriteRenderer>();
        col = GetComponent<Collider2D>();                     // for step-up extents

        sfx = GetComponent<AudioSource>() ? GetComponent<AudioSource>()
                                          : gameObject.AddComponent<AudioSource>();
        sfx.playOnAwake = false;

        if (!groundProbe)
        {
            var p = new GameObject("GroundProbe");
            p.transform.SetParent(transform);
            p.transform.localPosition = new Vector3(0f, -0.9f, 0f);
            groundProbe = p.transform;
        }

        Inventory = gameObject.AddComponent<Inventory>();
    }

    /* ───────── update loop ───────── */
    void Update()
    {
        ReadInput();
        HandleJump();         // buffer + coyote
        HandleFootsteps();
        HandleUseItem();
        ApplyGravityScale();  // variable gravity every frame
        HandleFlip();         // sprite-direction
    }

    void FixedUpdate()
    {
        ApplyHorizontal();
        TryStepUp();          // auto-climb one tile
        CapFallingSpeed();
    }

    /* ───────── sprite flip ───────── */
    void HandleFlip()
    {
        if (Mathf.Abs(targetX) < 0.01f) return;              // no intent ◂ no change

        bool movingRight = targetX > 0f;
        if (movingRight != facingRight)
        {
            facingRight  = movingRight;
            // either flip the transform scale…
            Vector3 sc = transform.localScale;
            sc.x = Mathf.Abs(sc.x) * (facingRight ? 1f : -1f);
            transform.localScale = sc;
            // …or, if preferred:
            // sr.flipX = !facingRight;
        }
    }

    /* ───────── one-tile step-up ───────── */
    void TryStepUp()
    {
        // Grounded and with horizontal intent only
        if (!grounded || Mathf.Abs(targetX) < 0.01f) return;

        float dir  = Mathf.Sign(targetX);
        float cast = col.bounds.extents.x + stepCheckDistance;

        // 1. LOW raycast – is there a wall at foot level?
        Vector2 feet = (Vector2)transform.position +
                       Vector2.down * (col.bounds.extents.y - 0.02f);
        RaycastHit2D hitLow = Physics2D.Raycast(feet, Vector2.right * dir,
                                                cast, groundMask);
        if (!hitLow) return;                                  // nothing to climb

        // 2. HIGH raycast – is the space above free?
        Vector2 knee = feet + Vector2.up * stepHeight;
        RaycastHit2D hitHigh = Physics2D.Raycast(knee, Vector2.right * dir,
                                                 cast, groundMask);
        if (hitHigh) return;                                  // blocked

        // 3. Clear – shift body up by exactly one tile
        rb.position += new Vector2(0f, stepHeight);
    }

    /* ───────── gameplay helpers ───────── */
    void ReadInput()
    {
        float inputX = Input.GetAxisRaw("Horizontal");
        targetX      = inputX * topSpeed;

        /* ground */
        grounded     = Physics2D.OverlapCircle(groundProbe.position,
                                               probeRadius,
                                               groundMask);
        coyoteTimer  = grounded
                     ? coyoteTime
                     : Mathf.Max(coyoteTimer - Time.deltaTime, 0f);

        /* jump buffer */
        if (Input.GetButtonDown("Jump"))
            jumpBufferTimer = jumpBufferTime;
        else
            jumpBufferTimer = Mathf.Max(jumpBufferTimer - Time.deltaTime, 0f);
    }

    void HandleJump()
    {
        bool held = Input.GetButton("Jump");

        if (jumpBufferTimer > 0f && coyoteTimer > 0f)
        {
            rb.linearVelocity = new Vector2(rb.linearVelocity.x, jumpPower);
            coyoteTimer       = 0f;
            jumpBufferTimer   = 0f;
            PlayAmbient();
        }

        /* jump-cut */
        if (!held && rb.linearVelocity.y > 0f)
            rb.linearVelocity =
                new Vector2(rb.linearVelocity.x,
                            rb.linearVelocity.y * jumpCutFactor);
    }

    void ApplyHorizontal()
    {
        float accel = grounded ? accelGround : accelAir;
        float decel = grounded ? decelGround : decelAir;

        /* extra steering near apex */
        if (Mathf.Abs(rb.linearVelocity.y) < apexThreshold)
            accel += apexAccelBoost;

        if (Mathf.Abs(targetX) > 0.01f)
        {
            rb.linearVelocity = new Vector2(
                Mathf.MoveTowards(rb.linearVelocity.x,
                                  targetX,
                                  accel * Time.fixedDeltaTime),
                rb.linearVelocity.y);
        }
        else
        {
            rb.linearVelocity = new Vector2(
                Mathf.MoveTowards(rb.linearVelocity.x,
                                  0f,
                                  decel * Time.fixedDeltaTime),
                rb.linearVelocity.y);
        }
    }

    void CapFallingSpeed()
    {
        // only care about downward motion
        if (rb.linearVelocity.y >= 0f) return;

        // optional fast-fall when player holds ↓
        float mult = (Input.GetAxisRaw("Vertical") < -0.1f) ? fastFallMult : 1f;
        float cap  = -topSpeed * 2f * mult;  // cheap cap ≈ twice top-speed

        rb.linearVelocity = new Vector2(rb.linearVelocity.x,
                                        Mathf.Max(rb.linearVelocity.y, cap));
    }

    void ApplyGravityScale()
    {
        bool fastFall = Input.GetAxisRaw("Vertical") < -0.1f
                     && rb.linearVelocity.y < 0f;

        rb.gravityScale = rb.linearVelocity.y > 0f ? gravityRise
                         : fastFall               ? gravityFall * fastFallMult
                         : gravityFall;
    }

    void HandleFootsteps()
    {
        if (grounded &&
            Mathf.Abs(rb.linearVelocity.x) > 0.2f &&
            !sfx.isPlaying &&
            footstepClip)
        {
            PlayAmbient();
        }
    }

    /* ───────── left-click use / place ───────── */
    void HandleUseItem()
    {
        if (Inventory == null || !Input.GetMouseButtonDown(0)) return;

        InvSlot slot = Inventory.ActiveSlot;
        if (slot.IsEmpty) return;

        /* placeable block */
        if (slot.item.placeableTile)
        {
            Vector3 wp = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            int wx = Mathf.FloorToInt(wp.x);
            int wy = Mathf.FloorToInt(wp.y);

            World world  = GameManager.Instance.WorldManager.GetCurrentWorld();
            var   viewer = GameManager.Instance.WorldTilemapViewer;

            var edit = new TerrainEditService(world, viewer);
            if (edit.TryPlaceTile(wx, wy, slot.item.placeableTile.tileID).success)
                Inventory.TryConsumeFromSlot(Inventory.ActiveHotbarIndex, 1);
        }
        /* consumable */
        else if (slot.item.IsConsumable)
        {
            Debug.Log($"Consumed {slot.item.itemName}");
            Inventory.TryConsumeFromSlot(Inventory.ActiveHotbarIndex, 1);
        }
        /* weapon / tool */
        else
        {
            Debug.Log($"Used {slot.item.itemName}");
            // hook up combat / dig behaviour here
        }
    }

    /* ───────── tiny audio helper ───────── */
    void PlayAmbient()
    {
        if (footstepClip && sfx)
            sfx.PlayOneShot(footstepClip);
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (groundProbe)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(groundProbe.position, probeRadius);
        }
    }
#endif
}
