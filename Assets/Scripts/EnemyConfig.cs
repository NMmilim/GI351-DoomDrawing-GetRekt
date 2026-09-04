using UnityEngine;

public class EnemyController : MonoBehaviour
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
    [Header("Visual/Timing")]
    [SerializeField] private Animator animator;
    [SerializeField] private float attackDuration = 0.5f;
    [SerializeField] private float strikeOffset = 0.6f;
    // Shuriken (ranged) settings
    [Header("Shuriken")]
    public GameObject shurikenPrefab;
    public float shurikenSpeed = 6f;
    public float shurikenLifeTime = 3f;
    public int shurikenDamage = 1;

    [Header("Health")]
    [SerializeField] private int maxHealth = 1;

    private int currentHealth;
    private float prepareTimer;

    private EnemyState currentState = EnemyState.Approach;

    private bool prepared = false;
    private bool isAttacking = false;
    private bool beatSubscribed = false;

    private Transform player;

    private void Start()
    {
        currentHealth = maxHealth;

        GameObject playerObject = GameObject.FindGameObjectWithTag("Player");

        if (playerObject != null)
        {
            player = playerObject.transform;
        }

        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // subscribe to BeatHit if present
        if (BeatHit.Instance != null)
        {
            BeatHit.Instance.OnBeat += OnBeat;
            beatSubscribed = true;
        }
    }

    void OnDestroy()
    {
        if (beatSubscribed && BeatHit.Instance != null)
            BeatHit.Instance.OnBeat -= OnBeat;
    }

    private void OnBeat(double dspTime, int beatIndex)
    {
        if (prepared && !isAttacking)
        {
            prepared = false;
            AttackPlayer();
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
        prepared = false;

        if (animator != null)
            animator.SetTrigger("Prepare");
        Debug.Log("ENEMY PREPARE ATTACK");
    }

    private void PrepareAttack()
    {
        prepareTimer -= Time.deltaTime;

        if (prepareTimer <= 0f)
        {
            // finished preparing; wait for beat (or attack immediately if no BeatHit)
            prepared = true;
            if (BeatHit.Instance == null)
            {
                AttackPlayer();
            }
        }
    }

    private void AttackPlayer()
    {
        if (isAttacking) return;
        isAttacking = true;

        PlayerController playerController = player.GetComponent<PlayerController>();
        if (playerController != null)
        {
            playerController.SetAttackingEnemy(this);
        }

        if (animator != null)
            animator.SetTrigger("Attack");

        // spawn shuriken projectile toward player
        SpawnShuriken();

        // finish attack after attackDuration
        Invoke(nameof(FinishAttack), attackDuration);
    }

    private void FinishAttack()
    {
        isAttacking = false;
        currentState = EnemyState.Approach;
        if (animator != null)
            animator.ResetTrigger("Attack");
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
    private void SpawnShuriken()
    {
        if (shurikenPrefab == null || player == null) return;

        Vector2 dir = ((Vector2)player.position - (Vector2)transform.position).normalized;
        Vector3 spawnPos = transform.position + (Vector3)(dir * strikeOffset);

        var go = Instantiate(shurikenPrefab, spawnPos, Quaternion.identity);
        // try to find a Shuriken component to set parameters, otherwise set Rigidbody2D velocity
        var s = go.GetComponent<Shuriken>();
        if (s != null)
        {
            s.owner = this.gameObject;
            s.damage = shurikenDamage;
            s.speed = shurikenSpeed;
            s.lifeTime = shurikenLifeTime;
            s.Launch(dir);
        }
        else
        {
            var rb = go.GetComponent<Rigidbody2D>();
            if (rb == null) rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 0f;
            rb.velocity = dir * shurikenSpeed;
            Destroy(go, shurikenLifeTime);
        }
    }
}