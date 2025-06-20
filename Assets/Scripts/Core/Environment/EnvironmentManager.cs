/* =======================================================================
 *  EnvironmentManager.cs – world clock (day / season) with explicit Init
 * ===================================================================== */
using UnityEngine;
using System;

[DisallowMultipleComponent]
public sealed class EnvironmentManager : MonoBehaviour
{
    /* ───── Inspector ───── */
    [Header("Clock Settings")]
    [Tooltip("Real-minutes that equal one in-game day (00:00 → 24:00).")]
    [Range(1f, 240f)]
    [SerializeField] private float minutesPerDay = 24f;

    [Tooltip("In-game days that make up one season.")]
    [Min(1)]
    [SerializeField] private int daysPerSeason = 7;

    [Tooltip("Start time-of-day in range 0‥1 (0 = midnight, 0.5 = midday).")]
    [Range(0f, 1f)]
    [SerializeField] private float startTimeOfDay = 0.25f;   // 06:00

    /* ───── Public read-only state ───── */
    public float TimeOfDay01 { get; private set; }   // 0‥1
    public int   DayCount    { get; private set; }   // 0-based
    public int   SeasonIndex { get; private set; }   // 0-based

    /* ───── Events ───── */
    public event Action<int> OnDayBegin;      // new day #
    public event Action<int> OnSeasonBegin;   // new season #

    /* ───── Internals ───── */
    private float secondsPerDay;
    private bool  isReady;

    /* ───────── Initialization ───────── */
    /// <summary>Must be called once by GameManager after all managers exist.</summary>
    public void Initialize()
    {
        if (isReady) return;

        secondsPerDay = minutesPerDay * 60f;
        TimeOfDay01   = Mathf.Repeat(startTimeOfDay, 1f);

        isReady = true;
    }

    /* ───────── Update the clock ───────── */
    private void Update()
    {
        if (!isReady) return;
        AdvanceClock(Time.unscaledDeltaTime);
    }

    /* ───────── Helpers ───────── */
    private void AdvanceClock(float deltaRealSeconds)
    {
        float t       = TimeOfDay01 * secondsPerDay + deltaRealSeconds;
        int   addDays = 0;

        if (t >= secondsPerDay)
        {
            addDays = Mathf.FloorToInt(t / secondsPerDay);
            t      -= addDays * secondsPerDay;
        }

        if (addDays > 0)
        {
            DayCount += addDays;
            OnDayBegin?.Invoke(DayCount);

            int newSeason = DayCount / daysPerSeason;
            if (newSeason != SeasonIndex)
            {
                SeasonIndex = newSeason;
                OnSeasonBegin?.Invoke(SeasonIndex);
            }
        }

        TimeOfDay01 = t / secondsPerDay;
    }

    /* ───────── Public convenience ───────── */
    public float TimeOfDayHours => TimeOfDay01 * 24f;
    public bool  IsNight        => TimeOfDay01 >= 0.75f || TimeOfDay01 < 0.25f;

    public void SkipHours(float hours)
    {
        if (!isReady) return;
        AdvanceClock(hours / 24f * secondsPerDay);
    }
}
