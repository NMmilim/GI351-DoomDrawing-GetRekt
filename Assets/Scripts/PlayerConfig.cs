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

        // Parry input: spacebar (single button parry)
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            ActivateParry();
        }
    }

    private void ActivateParry()
    {
        parryActive = true;
        currentState = PlayerState.Parry;
        if (animator != null)
            animator.SetTrigger("Parry");

        if (parryActiveDuration > 0f)
            Invoke(nameof(ClearParryActive), parryActiveDuration);
    }

    private void ClearParryActive()
    {
        parryActive = false;
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

        // Parry: if player activated parry, handle it immediately
        if (parryActive)
        {
            wasParried = true;
            if (animator != null) animator.SetTrigger("Parry");
            if (attacker != null) attacker.TakeDamage(parryAttack);
            parryActive = false;
            return true;
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
