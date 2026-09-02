using UnityEngine;

public class EnemyController : MonoBehaviour, IDamageable
{
    public enum EnemyState
    {
        Approach,
        Prepare,
        Attack,
        Stunned,
        Dead
    }

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2f;
    [SerializeField] private float attackDistance = 2f;

    [Header("Attack")]
    [SerializeField] private float prepareTime = 0.8f;
    [SerializeField] private int attackDamage = 1;

    [Header("Health")]
    [SerializeField] private int maxHealth = 1;

    private int currentHealth;
    private float prepareTimer;

    private EnemyState currentState = EnemyState.Approach;

    private Transform player;

    private void Start()
    {
        currentHealth = maxHealth;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }
    }

    private void Update()
    {
        if (player == null)
            return;

        switch (currentState)
        {
            case EnemyState.Approach:
                ApproachPlayer();
                break;

            case EnemyState.Prepare:
                PrepareAttack();
                break;

            case EnemyState.Attack:
                AttackPlayer();
                break;

            case EnemyState.Stunned:
                // Wait for counter / recovery
                break;

            case EnemyState.Dead:
                break;
        }
    }

    private void ApproachPlayer()
    {
        float distance = Vector2.Distance(transform.position, player.position);

        if (distance > attackDistance)
        {
            transform.position = Vector2.MoveTowards(
                transform.position,
                player.position,
                moveSpeed * Time.deltaTime
            );
        }
        else
        {
            StartPrepare();
        }
    }

    private void StartPrepare()
    {
        currentState = EnemyState.Prepare;
        prepareTimer = prepareTime;

        Debug.Log("ENEMY PREPARE ATTACK");
    }

    private void PrepareAttack()
    {
        prepareTimer -= Time.deltaTime;

        if (prepareTimer <= 0f)
        {
            currentState = EnemyState.Attack;
        }
    }

    private void AttackPlayer()
    {
        PlayerController playerController =
            player.GetComponent<PlayerController>();

        if (playerController != null)
        {
            // Tell Player which enemy is attacking
            playerController.SetAttackingEnemy(this);

            // Ask player to handle incoming attack (parry/block). If not handled, apply damage.
            bool wasParried;
            bool handled = playerController.OnIncomingAttack(attackDamage, this, out wasParried);
            if (!handled)
            {
                playerController.TakeDamage(attackDamage);
            }
        }

        Debug.Log("ENEMY ATTACK!");

        currentState = EnemyState.Approach;
    }
    public void TakeDamage(int damage)
    {
        if (currentState == EnemyState.Dead)
            return;

        currentHealth -= damage;

        Debug.Log("Enemy HP: " + currentHealth);

        if (currentHealth <= 0)
        {
            Die();
        }
        else
        {
            currentState = EnemyState.Stunned;
        }
    }

    public void Stun()
    {
        if (currentState == EnemyState.Dead)
            return;

        currentState = EnemyState.Stunned;

        Debug.Log("ENEMY PARRIED!");
    }

    public void Die()
    {
        currentState = EnemyState.Dead;

        Debug.Log("ENEMY DEAD");

        Destroy(gameObject);
    }

    public EnemyState GetState()
    {
        return currentState;
    }
}