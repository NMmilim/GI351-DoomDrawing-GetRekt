using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    public enum PlayerState
    {
        Idle,
        Guard,
        Block,
        Parry,
        Hit,
        Dead
    }

    [Header("Health")]
    [SerializeField] private int maxHealth = 3;

    [Header("Parry")]
    [SerializeField] private float perfectParryWindow = 0.2f;
    [SerializeField] private int parryDamage = 1;

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private int currentHealth;
    private PlayerState currentState = PlayerState.Idle;

    private EnemyController attackingEnemy;
    private float parryTimer;

    private void Start()
    {
        currentHealth = maxHealth;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        SetAnimationIdle();
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        if (currentState == PlayerState.Dead)
            return;

        // SPACE DOWN
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartGuard();
        }

        // HOLD SPACE
        if (currentState == PlayerState.Guard)
        {
            parryTimer += Time.deltaTime;
        }

        // SPACE UP
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            ReleaseGuard();
        }
    }

    // ==========================================
    // GUARD
    // ==========================================

    private void StartGuard()
    {
        currentState = PlayerState.Guard;
        parryTimer = 0f;

        if (animator != null)
        {
            animator.SetBool("IsGuarding", true);
        }

        Debug.Log("GUARD - HOLD");
    }

    private void ReleaseGuard()
    {
        if (currentState != PlayerState.Guard)
            return;

        if (animator != null)
        {
            animator.SetBool("IsGuarding", false);
        }

        Debug.Log("RELEASE");

        // Temporary timing system
        if (parryTimer >= perfectParryWindow)
        {
            PerfectParry();
        }
        else
        {
            Block();
        }
    }

    // ==========================================
    // BLOCK
    // ==========================================

    private void Block()
    {
        currentState = PlayerState.Block;

        if (animator != null)
        {
            animator.SetTrigger("Block");
        }

        Debug.Log("BLOCK!");

        // Return to Idle after animation
        Invoke(nameof(SetAnimationIdle), 0.25f);
    }

    // ==========================================
    // PARRY
    // ==========================================

    private void PerfectParry()
    {
        currentState = PlayerState.Parry;

        if (animator != null)
        {
            animator.SetTrigger("Parry");
        }

        Debug.Log("PERFECT PARRY!");

        if (attackingEnemy != null)
        {
            attackingEnemy.TakeDamage(parryDamage);

            Debug.Log("COUNTER ATTACK!");

            attackingEnemy = null;
        }

        // Return to Idle after animation
        Invoke(nameof(SetAnimationIdle), 0.35f);
    }

    // ==========================================
    // ENEMY ATTACK
    // ==========================================

    public void SetAttackingEnemy(EnemyController enemy)
    {
        attackingEnemy = enemy;
    }

    // ==========================================
    // DAMAGE
    // ==========================================

    public void TakeDamage(int damage)
    {
        if (currentState == PlayerState.Dead)
            return;

        currentHealth -= damage;

        Debug.Log("PLAYER HP: " + currentHealth);

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
            animator.SetBool("IsGuarding", false);
            animator.SetTrigger("Hit");
        }

        Debug.Log("PLAYER HIT!");

        Invoke(nameof(SetAnimationIdle), 0.3f);
    }

    // ==========================================
    // DEATH
    // ==========================================

    private void Die()
    {
        currentHealth = 0;
        currentState = PlayerState.Dead;

        if (animator != null)
        {
            animator.SetBool("IsGuarding", false);
            animator.SetTrigger("Death");
        }

        Debug.Log("PLAYER DEAD!");

        // Stop input
        enabled = false;

        // Disable collider
        Collider2D playerCollider = GetComponent<Collider2D>();

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }
    }

    // ==========================================
    // IDLE
    // ==========================================

    private void SetAnimationIdle()
    {
        if (currentState == PlayerState.Dead)
            return;

        currentState = PlayerState.Idle;

        if (animator != null)
        {
            animator.SetBool("IsGuarding", false);
        }
    }

    // ==========================================
    // GETTERS
    // ==========================================

    public int GetHealth()
    {
        return currentHealth;
    }

    public PlayerState GetState()
    {
        return currentState;
    }
}