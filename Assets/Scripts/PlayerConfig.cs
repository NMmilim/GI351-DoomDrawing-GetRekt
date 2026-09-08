using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Parry,
        Hit,
        Dead
    }

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [Header("Parry")]
    [SerializeField] private int parryAttack = 1; // damage dealt to enemy on parry
    [Tooltip("Seconds after pressing parry input within which an incoming attack can be parried")]
    [SerializeField] private float parryInputWindow = 0.25f;

    [Header("Input & Dodge")]
    [SerializeField] private float dodgeDuration = 0.35f;

    [Header("HeartRate Recovery")]
    [Tooltip("Time window (seconds) after taking damage during which a subsequent perfect parry reduces BPM")]
    [SerializeField] private float hitRecoveryWindow = 3f;
    [Tooltip("Fraction of the BPM gained from the hit that will be removed by a recovery parry (0..1)")]
    [Range(0f, 1f)]
    [SerializeField] private float recoveryAfterHitMultiplier = 0.75f;

    [Header("Adrenaline Surge (after recovery)")]
    [Tooltip("Delay after recovery parry before adrenaline surge begins (seconds)")]
    [SerializeField] private float adrenalineSurgeDelay = 1f;
    [Tooltip("Duration of the adrenaline surge (seconds)")]
    [SerializeField] private float adrenalineSurgeDuration = 5f;
    [Tooltip("BPM added per second during the adrenaline surge")]
    [SerializeField] private float adrenalineSurgePerSecond = 1f;
    [Tooltip("If true start surge automatically after a recovery parry")]
    [SerializeField] private bool startSurgeOnRecoveryParry = true;

    // runtime input state
    private float lastParryTime = -10f;
    private int lastParryDir = 0; // -1 left, +1 right
    private bool isDodging = false;
    private float dodgeEndTime = 0f;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private int currentHealth;
    private PlayerState currentState = PlayerState.Idle;

    private EnemyController attackingEnemy;

    // Heart-rate / parry tracking
    // If a parry was attempted recently but did not succeed, this becomes true so we can apply a "failed parry" HR penalty.
    private bool lastParryFailed = false;
    // Multiplier for the window after lastParryTime that counts as a "failed parry" when an attack lands.
    private const float failedParryWindowMultiplier = 2.0f;

    // Track recent hit info so a later perfect parry can reduce BPM
    private float lastHitTime = -10f;
    private int lastHitDamage = 0;

    // Adrenaline coroutine handle
    private Coroutine adrenalineCoroutine;

    private void Start()
    {
        currentHealth = maxHealth;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        // Update UI with initial health
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);

        SetAnimationIdle();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (currentState == PlayerState.Dead) return;

        // --- PARRY INPUT ---
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            lastParryTime = Time.time;
            currentState = PlayerState.Parry;

            // reset failed flag until we know outcome
            lastParryFailed = false;

            if (animator != null)
            {
                animator.SetTrigger("Parry");
            }

            // Reset back to idle after short delay
            Invoke(nameof(SetAnimationIdle), parryInputWindow);
        }

        // --- DODGE SYSTEM (planned, commented out) ---
        /*
        if (Keyboard.current.spaceKey.wasPressedThisFrame && SomeConditionForDodge())
        {
            isDodging = true;
            dodgeEndTime = Time.time + dodgeDuration;
            currentState = PlayerState.Parry; // placeholder, later replace with Dodge state

            if (animator != null)
            {
                animator.SetTrigger("Dodge");
            }
        }

        if (isDodging && Time.time >= dodgeEndTime)
        {
            isDodging = false;
            SetAnimationIdle();
        }
        */
    }

    // ENEMY ATTACK
    public void SetAttackingEnemy(EnemyController enemy)
    {
        attackingEnemy = enemy;
    }

    // DAMAGE
    public void TakeDamage(int damage)
    {
        if (currentState == PlayerState.Dead)
            return;

        Debug.Log($"TakeDamage called. dmg={damage} currentHealth(before)={currentHealth}\n{System.Environment.StackTrace}");

        currentHealth -= damage;

        // store recent hit data for potential recovery-on-parry
        lastHitTime = Time.time;
        lastHitDamage = damage;

        // Update UI health immediately
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);

        // Heart-rate: register hit taken
        HeartRate.Instance?.RegisterHitTaken(damage);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            PlayerHit();
        }
    }

    private void PlayerHit()
    {
        currentState = PlayerState.Hit;

        if (animator != null)
        {
            animator.SetTrigger("Hit");
        }

        Invoke(nameof(SetAnimationIdle), 0.3f);
    }

    // DEATH
    private void Die()
    {
        currentHealth = 0;
        currentState = PlayerState.Dead;

        if (animator != null)
        {
            animator.SetTrigger("Death");
        }

        enabled = false;

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        // Heart-rate: set BPM to zero and freeze updates
        HeartRate.Instance?.OnPlayerDeath();

        // Update UI and show game over
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);
        UIManager.Instance?.ShowLose();
    }

    // IDLE
    private void SetAnimationIdle()
    {
        if (currentState == PlayerState.Dead)
            return;

        currentState = PlayerState.Idle;
    }

    // GETTERS
    public int GetHealth() => currentHealth;
    public PlayerState GetState() => currentState;

    // Called by enemies when they attempt to hit the player.
    // Returns true if the attack was handled (parried/dodged), false if caller should apply damage.
    public bool OnIncomingAttack(int damage, EnemyController attacker, out bool wasParried)
    {
        wasParried = false;
        
        


        // --- SUCCESSFUL PARRY ---
        if (Time.time - lastParryTime <= parryInputWindow)
        {
            currentState = PlayerState.Parry;
            if (animator != null)
            {
                animator.SetTrigger("Parry");
            }

            // Damage enemy back if reference exists
            if (attacker != null)
            {
                attacker.TakeDamage(parryAttack);
            }

            wasParried = true;
            


            // Heart-rate: successful perfect parry
            HeartRate.Instance?.RegisterPerfectParry();

            // UI: award parry points / temporary multiplier
            UIManager.Instance?.OnPerfectParry();

            // NEW: increment combo on successful parry
            UIManager.Instance?.AddCombo();

            // Recovery logic if last parry failed
            if (lastParryFailed)
            {
                float reduceAmount = HeartRate.Instance != null
                    ? HeartRate.Instance.gainPerFailedParry * 0.5f
                    : 5f;
                HeartRate.Instance?.RegisterRecoveryAfterFailure(reduceAmount);
                lastParryFailed = false;
            }

            // Recovery logic if player was recently hit
            if (Time.time - lastHitTime <= hitRecoveryWindow && lastHitDamage > 0)
            {
                if (HeartRate.Instance != null)
                {
                    float reduceAmount = HeartRate.Instance.gainPerDamage * lastHitDamage * recoveryAfterHitMultiplier;
                    HeartRate.Instance.RegisterRecoveryAfterFailure(reduceAmount);
                }
                lastHitDamage = 0;
                lastHitTime = -10f;

                if (startSurgeOnRecoveryParry)
                {
                    if (adrenalineCoroutine != null) StopCoroutine(adrenalineCoroutine);
                    adrenalineCoroutine = StartCoroutine(AdrenalineSurgeRoutine());
                }
            }

            return true; // attack was handled
        }

        // --- FAILED PARRY ---
        if (Time.time - lastParryTime <= parryInputWindow * failedParryWindowMultiplier)
        {
            lastParryFailed = true;
            HeartRate.Instance?.RegisterFailedParry();

            // NEW: reset combo on failed parry
            UIManager.Instance?.ResetCombo();
        }
        else
        {
            lastParryFailed = false;

            // NEW: reset combo when taking damage
            UIManager.Instance?.ResetCombo();
        }

        Debug.Log($"OnIncomingAttack called. damage={damage}. lastParryDelta={Time.time - lastParryTime}");

        return false; // not handled, caller should apply damage
    }


    // Adrenaline surge: after a recovery parry we optionally add BPM over time to simulate adrenaline.
    private IEnumerator AdrenalineSurgeRoutine()
    {
        // delay before surge starts
        if (adrenalineSurgeDelay > 0f)
            yield return new WaitForSeconds(adrenalineSurgeDelay);

        float t = 0f;
        while (t < adrenalineSurgeDuration)
        {
            float dt = Time.deltaTime;
            t += dt;
            // small per-frame BPM increase
            HeartRate.Instance?.ModifyRate(adrenalineSurgePerSecond * dt);
            yield return null;
        }

        adrenalineCoroutine = null;
    }
}
