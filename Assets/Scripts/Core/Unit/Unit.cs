/*********************************************************
 *  Unit.cs – mobile entity with 3-D ambient SFX on demand
 *********************************************************/
using UnityEngine;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(SpriteRenderer))]
public class Unit : Entity
{
    /* identity */
    public new int HP
    {
        get => base.HP;
        set => base.HP = Mathf.Clamp(value, 0, maxHP);
    }

    public UnitTemplate template;
    public int          team = 0;

    /* gameplay stats */
    public float moveSpeed = 3f;
    public int   damage    = 10;

    /* ambient clips */
    [Header("Ambient Sound")]
    [Tooltip("Clips the manager may ask this unit to play")]
    public AudioClip[] ambientClips;

    /* runtime */
    public new static readonly HashSet<Unit> All = new();

    protected Rigidbody2D    rb;
    protected AudioSource    sfx;
    protected SpriteRenderer sr;

    /* -------------------------------------------------- */
    protected override void Awake()
    {
        base.Awake();

        rb = GetComponent<Rigidbody2D>();
        sr = GetComponent<SpriteRenderer>();

        if (!TryGetComponent(out sfx))
            sfx = gameObject.AddComponent<AudioSource>();

        sfx.playOnAwake  = false;
        sfx.spatialBlend = 1f;
        sfx.rolloffMode  = AudioRolloffMode.Logarithmic;
        sfx.maxDistance  = 15f;
        sfx.minDistance  = 1f;
    }

    protected override void OnEnable()  { base.OnEnable();  All.Add(this); }
    protected override void OnDisable() { base.OnDisable(); All.Remove(this); }

    /* movement helper: horizontal only, plus sprite flip */
    public void Move(Vector2 dir)
    {
        float xSpeed =
            dir.sqrMagnitude > 0.01f
                ? dir.normalized.x * moveSpeed
                : 0f;

        rb.linearVelocity = new Vector2(xSpeed, rb.linearVelocity.y);

        if (xSpeed > 0.01f)
            sr.flipX = false;
        else if (xSpeed < -0.01f)
            sr.flipX = true;
    }

    /* ambient executor (called by UnitManager) */
    public void PlayAmbient()
    {
        if (ambientClips == null || ambientClips.Length == 0) return;

        AudioClip clip = ambientClips[Random.Range(0, ambientClips.Length)];
        if (clip) sfx.PlayOneShot(clip);
    }
}
