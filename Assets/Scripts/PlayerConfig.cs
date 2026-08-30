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

    private int currentHealth;
    private PlayerState currentState = PlayerState.Idle;

    private EnemyController attackingEnemy;
    private float parryTimer;

    private void Start()
    {
        currentHealth = maxHealth;
    }

    private void Update()
    {
        if (Keyboard.current == null)
            return;

        // Don't allow input after death
        if (currentState == PlayerState.Dead)
            return;

        // Press Space
        if (Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            StartGuard();
        }

        // Hold Space
        if (currentState == PlayerState.Guard)
        {
            parryTimer += Time.deltaTime;
        }

        // Release Space
        if (Keyboard.current.spaceKey.wasReleasedThisFrame)
        {
            ReleaseGuard();
        }
    }

    private void StartGuard()
    {
        currentState = PlayerState.Guard;
        parryTimer = 0f;

        Debug.Log("GUARD - HOLD");
    }

    private void ReleaseGuard()
    {
        if (currentState != PlayerState.Guard)
            return;

        Debug.Log("RELEASE");

        if (parryTimer >= perfectParryWindow)
        {
            PerfectParry();
        }
        else
        {
            Block();
        }
    }

    private void Block()
    {
        currentState = PlayerState.Block;

        Debug.Log("BLOCK!");

        currentState = PlayerState.Idle;
    }

    private void PerfectParry()
    {
        currentState = PlayerState.Parry;

        Debug.Log("PERFECT PARRY!");

        if (attackingEnemy != null)
        {
            attackingEnemy.TakeDamage(parryDamage);

            Debug.Log("COUNTER ATTACK!");

            attackingEnemy = null;
        }

        currentState = PlayerState.Idle;
    }

    public void SetAttackingEnemy(EnemyController enemy)
    {
        attackingEnemy = enemy;
    }

    public void TakeDamage(int damage)
    {
        // Can't take damage after death
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
            currentState = PlayerState.Hit;

            Debug.Log("PLAYER HIT!");

            currentState = PlayerState.Idle;
        }
    }

    private void Die()
    {
        currentHealth = 0;
        currentState = PlayerState.Dead;

        Debug.Log("PLAYER DEAD!");

        // Stop player input
        enabled = false;

        // Optional: disable collider
        Collider2D playerCollider = GetComponent<Collider2D>();

        if (playerCollider != null)
        {
            playerCollider.enabled = false;
        }
        Destroy(gameObject);
    }

    public int GetHealth()
    {
        return currentHealth;
    }

    public PlayerState GetState()
    {
        return currentState;
    }
}