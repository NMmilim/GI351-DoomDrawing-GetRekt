using System;
using UnityEngine;

/// <summary>
/// Simple heart-rate system:
/// - heart rate increases on events (perfect parry, failed parry, being hit)
/// - decays back to baseline over time; decay is reduced when player HP is low 
/// - exposes an attack-speed multiplier derived from current heart rate
/// 
/// Usage:
/// - Call HeartRate.Instance.RegisterPerfectParry() when player scores a perfect parry
/// - Call HeartRate.Instance.RegisterFailedParry() when a parry attempt fails
/// - Call HeartRate.Instance.RegisterHitTaken(int damage) when player takes damage
/// - Read HeartRate.Instance.GetAttackSpeedMultiplier() in EnemyController (apply to prepare times, attack durations, projectile speed)
/// </summary>
public class HeartRate : MonoBehaviour
{
    public static HeartRate Instance { get; private set; }

    [Header("Base / Range")]
    [Tooltip("Baseline (resting) heart rate")]
    public float baseline = 70f;
    [Tooltip("Minimum allowed heart rate")]
    public float minRate = 40f;
    [Tooltip("Maximum allowed heart rate")]
    public float maxRate = 200f;

    [Header("Recovery")]
    [Tooltip("BPM per second recovered toward baseline at full HP")]
    public float recoveryRate = 8f;
    [Tooltip("Multiplier applied to recovery when player HP is low (0..1). Lower means slower recovery)")]
    [Range(0.1f, 1f)]
    public float lowHpRecoveryMultiplier = 0.5f;
    [Tooltip("HP fraction below which low-HP recovery applies (0..1)")]
    [Range(0f, 1f)]
    public float lowHpThreshold = 0.35f;

    [Header("Event gains")]
    [Tooltip("BPM added per perfect parry (adrenaline)")]
    public float gainPerPerfectParry = 3f;
    [Tooltip("BPM added per failed parry")]
    public float gainPerFailedParry = 10f;
    [Tooltip("BPM added when player takes damage (per hit, scaled by damage)")]
    public float gainPerDamage = 6f;

    [Header("Attack speed mapping")]
    [Tooltip("Multiplier range applied to enemy attack speed. When HR==baseline -> 1. When HR==maxRate -> attackSpeedMax.")]
    public float attackSpeedMax = 1.6f;

    [Header("References")]
    [Tooltip("Optional: assign Player GameObject. If null it's found by tag 'Player'")]
    public GameObject playerObject;

    // current heart rate (bpm)
    [SerializeField] private float currentRate = 70f;

    // internal
    private PlayerController playerController;

    // Event fired whenever heart rate changes (newRate)
    public event Action<float> OnHeartRateChanged;

    // When true we freeze the heart-rate logic (used for player death to show 0 BPM)
    private bool freezeOnDeath = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else if (Instance != this) Destroy(gameObject);

        currentRate = Mathf.Clamp(currentRate, minRate, maxRate);
    }

    void Start()
    {
        if (playerObject == null)
        {
            playerObject = GameObject.FindGameObjectWithTag("Player");
        }

        if (playerObject != null)
            playerController = playerObject.GetComponent<PlayerController>();
    }

    void Update()
    {
        // If frozen (player dead) don't recover / change rate automatically.
        if (freezeOnDeath) return;

        // Recover toward baseline each frame. Recovery slows when player HP is low.
        float effectiveRecovery = recoveryRate;
        if (playerController != null)
        {
            int hp = playerController.GetHealth();
            // Conservative low-HP check: if hp fraction is low, slow recovery.
            // If exact max health is needed, expose GetMaxHealth on PlayerController and compute fraction.
            if (hp <= 1)
                effectiveRecovery *= lowHpRecoveryMultiplier;
        }

        // Decay toward baseline
        if (currentRate > baseline)
        {
            currentRate -= effectiveRecovery * Time.deltaTime;
            if (currentRate < baseline) currentRate = baseline;
            OnHeartRateChanged?.Invoke(currentRate);
        }
        else if (currentRate < baseline)
        {
            // small drift up/down if below baseline
            currentRate += (recoveryRate * 0.5f) * Time.deltaTime;
            if (currentRate > baseline) currentRate = baseline;
            OnHeartRateChanged?.Invoke(currentRate);
        }

        currentRate = Mathf.Clamp(currentRate, minRate, maxRate);
    }

    // --- Public API ---

    public float GetCurrentRate() => currentRate;

    // Attack speed multiplier: maps baseline->1.0 and maxRate->attackSpeedMax
    public float GetAttackSpeedMultiplier() 
    {
        if (Mathf.Approximately(maxRate, baseline)) return 1f;
        float t = Mathf.InverseLerp(baseline, maxRate, currentRate);
        return Mathf.Lerp(1f, attackSpeedMax, t);
    }

    // Call when player does a perfect parry
    public void RegisterPerfectParry()
    {
        ModifyRate(gainPerPerfectParry);
    }

    // Call when player fails a parry
    public void RegisterFailedParry()
    {
        ModifyRate(gainPerFailedParry);
    }

    // Call when player takes damage
    public void RegisterHitTaken(int damage)
    {
        ModifyRate(gainPerDamage * damage);
    }

    // Directly change heart rate (positive to increase, negative to reduce)
    // This respects min/max clamps.
    public void ModifyRate(float delta)
    {
        if (freezeOnDeath) return;
        float prev = currentRate;
        currentRate = Mathf.Clamp(currentRate + delta, minRate, maxRate);
        if (!Mathf.Approximately(prev, currentRate))
            OnHeartRateChanged?.Invoke(currentRate);
    }

    // Force-set the current heart rate immediately.
    // If 'freeze' is true the heart-rate will be frozen (no recovery) until unfrozen.
    public void ForceSetRate(float rate, bool freeze = false)
    {
        currentRate = rate;
        freezeOnDeath = freeze;
        OnHeartRateChanged?.Invoke(currentRate);
    }

    // Convenience call when player dies: set BPM to zero and freeze updates.
    public void OnPlayerDeath()
    {
        ForceSetRate(0f, true);
    }

    // Optional: reduce heart rate when player performs a successful recovery action
    // Use when "perfect parry after failing" should reduce the heart rate
    public void RegisterRecoveryAfterFailure(float reduceAmount)
    {
        ModifyRate(-Mathf.Abs(reduceAmount));
    }

    // Optional: unfreeze after death / respawn
    public void UnfreezeAndResetToBaseline()
    {
        freezeOnDeath = false;
        currentRate = Mathf.Clamp(baseline, minRate, maxRate);
        OnHeartRateChanged?.Invoke(currentRate);
    }
}
