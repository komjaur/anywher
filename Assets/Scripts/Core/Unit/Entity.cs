using UnityEngine;
using System.Collections.Generic;

public abstract class Entity : MonoBehaviour
{
    [Min(1)] public int maxHP = 100;

    public int  HP   { get; protected set; }
    public bool Dead => HP <= 0;

    public static readonly HashSet<Entity> All = new();

    protected virtual void Awake()     => HP = maxHP;
    protected virtual void OnEnable()  => All.Add(this);
    protected virtual void OnDisable() => All.Remove(this);

    public virtual void TakeDamage(int dmg)
    {
        if (Dead || dmg <= 0) return;
        HP = Mathf.Max(HP - dmg, 0);
        if (Dead) Destroy(gameObject);
    }
}
