using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

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
    [SerializeField] private int parryAttack = 1; // damage dealt to enemy when parried

    [Header("Animation")]
    [SerializeField] private Animator animator;

    private int currentHealth;
    private PlayerState currentState = PlayerState.Idle;

    // incoming attack info recorded when hitbox collides while guarding
    private bool incomingAttackRecorded = false;
    private IDamageable incomingAttacker = null;
    private int incomingDamage = 0;

    // optional attacker set by enemy when it starts an attack (fallback)
    private IDamageable attackingEnemyPending = null;

    private float parryTimer;

    private void Start()
    {
        currentHealth = maxHealth;

        if (animator == null)
        {
            animator = GetComponent<Animator>();
        }

        SetAnimationIdle();

        // initialize UI health display
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateHealth(currentHealth, maxHealth);

        Debug.Log($"[PlayerController] Start initialized currentHealth={currentHealth}");
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // Allow restart input when dead
        if (currentState == PlayerState.Dead)
        {
            if (Keyboard.current.spaceKey.wasPressedThisFrame)
            {
                RestartGame();
            }
            return;
        }

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

    // allow external systems (enemy) to adjust the expected parry hold duration for the next incoming attack
    public void SetPerfectParryWindow(float seconds)
    {
        perfectParryWindow = seconds;
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

        // reset any previous incoming-attack record when starting a fresh guard
        ClearIncomingAttack();

        Debug.Log("[PlayerController] GUARD - HOLD");
    }

    private void ReleaseGuard()
    {
        if (currentState != PlayerState.Guard)
            return;

        if (animator != null)
        {
            animator.SetBool("IsGuarding", false);
        }

        Debug.Log("[PlayerController] RELEASE");

        // If player held for long enough AND there is a recorded incoming attack, it's a perfect parry
        if (parryTimer >= perfectParryWindow && incomingAttackRecorded)
        {
            PerfectParry();
        }
        else
        {
            // block: simply absorb the hit (no damage to player and no damage to enemy)
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

        Debug.Log("[PlayerController] BLOCK!");

        // clear any recorded incoming attack and pending attacker so releasing later won't affect enemies
        ClearIncomingAttack();
        attackingEnemyPending = null;

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

        Debug.Log("[PlayerController] PERFECT PARRY!");

        // choose attacker to damage: prefer the recorded incomingAttacker, fall back to pending attacker set by enemy
        IDamageable attackerToDamage = incomingAttacker != null ? incomingAttacker : attackingEnemyPending;

        if (attackerToDamage != null)
        {
            // safe-check the backing Unity object before calling TakeDamage
            var enemyObject = attackerToDamage as UnityEngine.Object;
            if (enemyObject != null)
            {
                attackerToDamage.TakeDamage(parryAttack);
                Debug.Log("[PlayerController] COUNTER ATTACK!");
            }
            else
            {
                Debug.Log("[PlayerController] attacker missing/destroyed; skipping counter damage");
            }
        }

        // resolved: clear recorded incoming attack and pending attacker
        ClearIncomingAttack();
        attackingEnemyPending = null;

        // Return to Idle after animation
        Invoke(nameof(SetAnimationIdle), 0.35f);
    }

    // ==========================================
    // EXTERNAL ATTACK REGISTRATION
    // ==========================================
    // Called by enemies that want to advertise which enemy is attacking (optional).
    // Enemy2 still calls this — keep it as a harmless setter so old code compiles.
    public void SetAttackingEnemy(IDamageable enemy)
    {
        // store as a fallback only; actual hitboxes should call OnIncomingAttack when they collide
        attackingEnemyPending = enemy;
    }

    // ==========================================
    // INCOMING ATTACK RECORDING (called by hitbox)
    // ==========================================
    // Called by enemies (or hitboxes) when an attack collides with the player.
    // Behavior:
    // - If guarding: record the incoming attacker and damage and return true (attack handled now).
    //   Actual parry damage is applied only on release if timing meets parry window.
    // - If not guarding: return false (player should take damage).
    public bool OnIncomingAttack(int damage, IDamageable attacker, out bool wasParried)
    {
        wasParried = false;
        Debug.Log($"[PlayerController] OnIncomingAttack called: state={currentState}, parryTimer={parryTimer:F3}, damage={damage}");

        if (currentState == PlayerState.Guard)
        {
            // Record incoming attack for resolution on release
            incomingAttackRecorded = true;
            incomingAttacker = attacker;
            incomingDamage = damage;

            // We don't apply damage now. If player releases with sufficient parryTimer we will parry.
            // Play block animation immediately (visual feedback)
            if (animator != null)
                animator.SetTrigger("Block");

            Debug.Log("[PlayerController] Incoming attack recorded while guarding (will resolve on release)");
            return true; // attack handled (player won't take immediate damage)
        }

        // not guarding -> attack not handled, player should take damage
        Debug.Log("[PlayerController] Attack not handled, will take damage");
        return false;
    }

    private void ClearIncomingAttack()
    {
        incomingAttackRecorded = false;
        incomingAttacker = null;
        incomingDamage = 0;
    }

    // ==========================================
    // DAMAGE
    // ==========================================
    public void TakeDamage(int damage)
    {
        if (currentState == PlayerState.Dead)
            return;

        currentHealth -= damage;

        Debug.Log($"[PlayerController] TakeDamage({damage}) -> currentHealth={currentHealth}");

        // update UI
        if (UIManager.Instance != null)
            UIManager.Instance.UpdateHealth(currentHealth, maxHealth);

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

        Debug.Log("[PlayerController] PLAYER HIT!");

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

        Debug.Log("[PlayerController] PLAYER DEAD!");

        // Do not disable this component so we can detect restart input.
        // Disable collider so player stops interacting with world.
        Collider2D playerCollider = GetComponent<Collider2D>();

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }

        // update UI to zero and show lose UI
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateHealth(currentHealth, maxHealth);
            UIManager.Instance.ShowLose();
        }
    }

    // ==========================================
    // RESTART
    // ==========================================
    private void RestartGame()
    {
        // simple reload of the active scene
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
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