/* =========================================================================
 *  AudioManager.cs — global SFX entry-point, no abbreviated names
 * ========================================================================= */
using UnityEngine;

public sealed class AudioManager : MonoBehaviour
{
    /* ───────── design-time data ───────── */
    [Header("Audio sources")]
    [SerializeField] private AudioSource sfxSource;              // auto-created if null

    [Header("Fallback Clips")]
    [SerializeField] private AudioClip fallbackLevelUpClip;      // used only if no clip supplied

    /* ───────── runtime data ───────── */
    private AudioClip levelUpClip;

    /* ───────── public API ───────── */
    /// Called once by GameManager after the component is created.
    public void Initialize(AudioClip runtimeLevelUpClip)
    {
        levelUpClip = runtimeLevelUpClip ? runtimeLevelUpClip : fallbackLevelUpClip;
         if (GameManager.Instance?.SkillManager)
            GameManager.Instance.SkillManager.OnLevelUp += HandleLevelUp;
    }

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (clip) sfxSource.PlayOneShot(clip, volume);
    }

    /* ───────── lifecycle ───────── */
    void Awake()
    {
        if (!sfxSource)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }
    }



    /* ───────── event handlers ───────── */
    void HandleLevelUp(SkillId id, int newLevel)
    {
        Debug.Log("level up");
        if (levelUpClip)
            sfxSource.PlayOneShot(levelUpClip);
    }
}
