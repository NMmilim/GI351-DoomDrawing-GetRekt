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

        // Update UI health immediately
        UIManager.Instance?.UpdateHealth(currentHealth, maxHealth);

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

        // Check parry timing
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
            return true; // attack was handled
        }

        // --- DODGE SYSTEM (planned, commented out) ---
        /*
        if (isDodging)
        {
            return true; // attack avoided
        }
        */

        Debug.Log($"OnIncomingAttack called. damage={damage}. lastParryDelta={Time.time - lastParryTime}");

        // Not handled: caller (StrikeHitbox / Shuriken) should call TakeDamage(damage)
        return false;
    }


}
