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

    [Header("Player")]
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

        // For now, holding for the correct amount of time = Perfect Parry
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
        }

        currentState = PlayerState.Idle;
        attackingEnemy = null;
    }

    public void SetAttackingEnemy(EnemyController enemy)
    {
        attackingEnemy = enemy;

        Debug.Log("Enemy is attacking!");
    }

    public void TakeDamage(int damage)
    {
        if (currentState == PlayerState.Dead)
            return;

        currentHealth -= damage;

        Debug.Log("Player HP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            currentState = PlayerState.Hit;
            currentState = PlayerState.Idle;
        }
    }

    private void Die()
    {
        currentState = PlayerState.Dead;

        Debug.Log("PLAYER DEAD");
    }

    public PlayerState GetState()
    {
        return currentState;
    }
}