using UnityEngine;
using UnityEngine.UI;
using TMPro;

public sealed class XpToastUI : MonoBehaviour
{
    /* motion & timing -------------------------------------------------- */
    const float Rise       = 200f;
    const float Lifetime   = 3f;
    const float FadeStartT = 0.7f;     // fraction of lifetime

    [SerializeField] Image    icon;
    [SerializeField] TMP_Text label;

    Vector2      velocity;
    float        tBirth;
    ToastSystem  pool;

    /* ------------------------------------------------------------------ */
    public void Init(string message, Sprite sprite, Color colour, ToastSystem owner)
    {
        if (!label) label = GetComponentInChildren<TMP_Text>();
        if (!icon)  icon  = GetComponentInChildren<Image>();

        pool       = owner;
        tBirth     = Time.unscaledTime;
        velocity   = Vector2.up * (Rise / Lifetime);

        label.text  = message;
        label.color = colour;

        if (icon)
        {
            icon.sprite  = sprite;
            icon.enabled = sprite;
            if (sprite)
            {
                var c = icon.color; c.a = 1f; icon.color = c;   // reset α
            }
        }
    }

    /* ------------------------------------------------------------------ */
    void Update()
    {
        float age = Time.unscaledTime - tBirth;
        ((RectTransform)transform).anchoredPosition += velocity * Time.unscaledDeltaTime;

        if (age >= FadeStartT * Lifetime)
            SetAlpha(1f - Mathf.InverseLerp(FadeStartT * Lifetime, Lifetime, age));

        if (age >= Lifetime && pool)
            pool.Recycle(this);
    }

    void SetAlpha(float a)
    {
        var c = label.color; c.a = a; label.color = c;

        if (icon && icon.enabled)
        {
            c = icon.color; c.a = a; icon.color = c;
        }
    }
}
