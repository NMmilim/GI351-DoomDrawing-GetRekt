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
    [SerializeField] private int parryAttack = 1; // damage dealt to enemy on parry
    [Header("Input & Dodge")]
    [Tooltip("Seconds after pressing an arrow within which an incoming attack can be parried")]
    [SerializeField] private float parryInputWindow = 0.25f;
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

        SetAnimationIdle();
    }

    private void Update()
    {
        if (Keyboard.current == null) return;
        if (currentState == PlayerState.Dead) return;

        // Parry input: left/right arrow for directional parry (immediate)
        if (Keyboard.current.leftArrowKey.wasPressedThisFrame)
            RegisterParry(-1);
        if (Keyboard.current.rightArrowKey.wasPressedThisFrame)
            RegisterParry(1);

        // Dodge input: spacebar
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            PerformDodge();

        // update dodge state
        if (isDodging && Time.time >= dodgeEndTime)
        {
            isDodging = false;
            SetAnimationIdle();
        }
    }

    // New input: directional parry and dodge
    private void RegisterParry(int dir)
    {
        lastParryDir = dir;
        currentState = PlayerState.Parry;
        if (animator != null)
        {
            animator.SetInteger("ParryDir", dir);
            animator.SetTrigger("Parry");
        }
        // return to idle shortly after
        Invoke(nameof(SetAnimationIdle), 0.25f);
    }

    private void PerformDodge()
    {
        if (isDodging) return;
        isDodging = true;
        dodgeEndTime = Time.time + dodgeDuration;
        currentState = PlayerState.Idle; // transient state
        if (animator != null) animator.SetTrigger("Dodge");
    }

    // ENEMY ATTACK
    public void SetAttackingEnemy(EnemyController enemy)
    {
        attackingEnemy = enemy;
    }

    // Called by hitboxes/enemies to let the player resolve incoming attack immediately.
    // Returns true if the attack was handled (dodged or parried/blocked), false if player should take damage.
    public bool OnIncomingAttack(int damage, EnemyController attacker, out bool wasParried)
    {
        wasParried = false;

        // Dodge: if currently dodging, ignore damage
        if (isDodging && Time.time <= dodgeEndTime) return true;

        // Directional parry: immediate match based on lastParryDir
        if (lastParryDir != 0 && attacker != null)
        {
            float attackerX = attacker.transform.position.x;
            float playerX = transform.position.x;
            int attackerSide = (attackerX < playerX) ? -1 : 1;
            if (attackerSide == lastParryDir)
            {
                // successful parry
                wasParried = true;
                if (animator != null) animator.SetTrigger("Parry");
                attacker.TakeDamage(parryAttack);
                // consume parry input
                lastParryDir = 0;
                return true;
            }
        }

        // No dodge or parry -> not handled
        return false;
    }

    // DAMAGE
    public void TakeDamage(int damage)
    {
        if (currentState == PlayerState.Dead)
            return;

        currentHealth -= damage;

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

        Invoke(nameof(SetAnimationIdle), 0.3f);
    }

    // DEATH
    private void Die()
    {
        currentHealth = 0;
        currentState = PlayerState.Dead;

        if (animator != null)
        {
            animator.SetBool("IsGuarding", false);
            animator.SetTrigger("Death");
        }

        enabled = false;

        Collider2D playerCollider = GetComponent<Collider2D>();
        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }
    }

    // IDLE
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

    // GETTERS
    public int GetHealth() => currentHealth;
    public PlayerState GetState() => currentState;
}
