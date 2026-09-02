using UnityEngine;

public class Enemy2 : MonoBehaviour, IDamageable
{
    public float speed = 1.5f;
    public float stopDistance = 1.2f;
    public Transform target;

    private bool Prep = false;
    private Rigidbody2D rb2d;
    [SerializeField] private Animator animator;
    public float prepareTime = 0.8f;
    private float prepareTimer = 0f;
    private bool isPreparing = false;
    private bool prepared = false; // ready to attack on next beat
    private bool isAttacking = false;
    public float attackDuration = 0.4f; // how long the attack state lasts before returning to idle
    private bool beatSubscribed = false;
    [Header("Health")]
    public int maxHealth = 1;
    private int currentHealth;

    [Header("Strike Settings")]
    public float strikeOffset = 0.6f;
    public float strikeRadius = 0.25f;
    public float strikeActiveTime = 0.15f;
    public float strikeDelay = 0.08f; // delay between animation trigger and hitbox active
    public int strikeDamage = 1;

    void Start()
    {
        rb2d = GetComponent<Rigidbody2D>();

        if (rb2d != null)
        {
            // Disable gravity and use kinematic body for scripted movement
            rb2d.gravityScale = 0f;
            rb2d.interpolation = RigidbodyInterpolation2D.Interpolate;
            rb2d.bodyType = RigidbodyType2D.Kinematic;
        }
        else
        {
            Debug.LogWarning("Enemy2: Rigidbody2D not found - movement will use transform instead.");
        }

        // If target not assigned in inspector, try to find the Player by tag
        if (target == null)
            target = GameObject.FindGameObjectWithTag("Player")?.transform;

        if (animator == null)
            animator = GetComponent<Animator>();

        // if Animator is on a child sprite object, try to find it there too
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // init health
        currentHealth = maxHealth;

        // subscribe to beat events if BeatHit exists (or set up to subscribe later)
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

    // If BeatHit wasn't available at Start, subscribe as soon as it becomes available
    void LateUpdate()
    {
        if (!beatSubscribed && BeatHit.Instance != null)
        {
            BeatHit.Instance.OnBeat += OnBeat;
            beatSubscribed = true;
        }
    }

    private void OnBeat(double dspTime, int beatIndex)
    {
        // if prepared and not currently attacking, trigger attack on beat
        if (prepared && !isAttacking)
        {
            prepared = false;
            TriggerAttack();
        }
    }

    // Physics-friendly movement when Rigidbody2D is present
    void FixedUpdate()
    {
        if (target == null) return;

        if (rb2d != null)
        {
            Vector2 current = rb2d.position;
            Vector2 playerPos = target.position;

            // compute the next position first and decide moving based on actual change
            Vector2 next = Vector2.MoveTowards(current, playerPos, speed * Time.fixedDeltaTime);
            bool moving = (next - current).sqrMagnitude > 0.000001f && Vector2.Distance(current, playerPos) > stopDistance;

            if (animator != null)
                animator.SetBool("IsMoving", moving);

            if (moving)
            {
                rb2d.MovePosition(next);
                Prep = false;
                // ensure swing pose is cleared while moving
                if (animator != null)
                    animator.SetBool("IsSwinging", false);
            }
            else
            {
                // start preparing (swing up) when stopped
                if (!Prep)
                {
                    Prep = true;
                    isPreparing = true;
                    prepareTimer = prepareTime;
                    if (animator != null)
                    {
                        animator.SetBool("IsMoving", false);
                        animator.SetBool("IsSwinging", true); // lift weapon
                    }
                }

                // count down prepare time; when finished, remain in swing pose until next logic
                if (isPreparing)
                {
                    prepareTimer -= Time.fixedDeltaTime;
                    if (prepareTimer <= 0f)
                    {
                        isPreparing = false;
                        // now fully prepared: wait for beat to trigger attack
                        prepared = true;
                        // keep IsSwinging = true so enemy stays in lifted pose
                    }
                }
            }
        }
    }

    // Fallback movement if no Rigidbody2D (simple transform-based)
    void Update()
    {
        if (rb2d != null) return; // handled in FixedUpdate
        if (target == null) return;
        float dist = Vector3.Distance(transform.position, target.position);
        bool moving = dist > stopDistance;

        if (animator != null)
        {
            animator.SetBool("IsMoving", moving);
            if (moving)
                animator.SetBool("IsSwinging", false);
        }

        if (moving)
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
        else
        {
            // start preparing (swing up) when stopped (transform fallback)
            if (!Prep)
            {
                Prep = true;
                isPreparing = true;
                prepareTimer = prepareTime;
                if (animator != null)
                {
                    animator.SetBool("IsMoving", false);
                    animator.SetBool("IsSwinging", true);
                }
            }

            // count down prepare time for transform path as well
            if (isPreparing)
            {
                prepareTimer -= Time.deltaTime;
                if (prepareTimer <= 0f)
                {
                    isPreparing = false;
                    // now fully prepared: wait for beat to trigger attack
                    prepared = true;
                }
            }
        }
    }
    void EnemAttack()
    {
        // Attack logic removed; enemy remains in swing (prep) state when in range.
    }

    // Public entry for external systems (BeatManager, player code, etc.) to trigger the attack.
    // Keeps enemy-side only: plays Attack animation and runs the strike timing (hitbox).
    public void TriggerAttack()
    {
        if (isAttacking) return; // ignore if already attacking
        isAttacking = true;

        if (animator != null)
        {
            // ensure swing pose (if not already) and trigger attack animation
            animator.SetBool("IsSwinging", true);
            animator.SetTrigger("Attack");
        }

        // stop preparing state and start strike coroutine
        isPreparing = false;
        Prep = false;

        // inform player which enemy is attacking so PerfectParry can damage this enemy
        if (target != null)
        {
            var pc = target.GetComponent<PlayerController>();
            if (pc != null)
                pc.SetAttackingEnemy(this);
        }

        // clear attacking flag after attackDuration and return to idle pose
        StartCoroutine(ClearAttackAfter(attackDuration));

        // set cooldown to avoid immediate retrigger from external code
        // (external systems can ignore this if they want faster repeats)
        // note: attackCooldown default is set in inspector
        // start strike timing (enables hitbox after strikeDelay)
        //StartStrike();
        //attackCooldownTimer = attackCooldown;
        // create a transient strike hitbox in front of enemy for player to parry
        CreateStrikeHitbox();
    }

    private void CreateStrikeHitbox()
    {
        // spawn a GameObject with CircleCollider2D and StrikeHitbox script
        GameObject hb = new GameObject("StrikeHitbox");
        // position in front of enemy toward the player if available
        Vector2 dir;
        if (target != null)
            dir = ((Vector2)target.position - (Vector2)transform.position).normalized;
        else
            dir = transform.right;

        Vector3 pos = transform.position + (Vector3)(dir * strikeOffset);
        hb.transform.position = pos;
        hb.transform.parent = transform; // parent so it follows enemy movement briefly

        // add collider
        var col = hb.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = strikeRadius;

        // disable collider if there's a delay so it doesn't hit early
        if (strikeDelay > 0f)
            col.enabled = false;

        // add script
        var sh = hb.AddComponent<StrikeHitbox>();
        sh.damage = strikeDamage;
        sh.owner = gameObject;

        // schedule activation and destruction
        StartCoroutine(StrikeLifecycle(hb, col));
    }

    private System.Collections.IEnumerator StrikeLifecycle(GameObject hb, Collider2D col)
    {
        if (strikeDelay > 0f)
            yield return new WaitForSeconds(strikeDelay);

        if (col != null)
            col.enabled = true;

        // keep active for strikeActiveTime
        yield return new WaitForSeconds(strikeActiveTime);

        if (hb != null)
            Destroy(hb);
    }

    private System.Collections.IEnumerator ClearAttackAfter(float duration)
    {
        if (duration > 0f)
            yield return new WaitForSeconds(duration);
        // return to idle pose
        if (animator != null)
            animator.SetBool("IsSwinging", false);
        isAttacking = false;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        // simple death: play animation and destroy
        if (animator != null)
            animator.SetTrigger("Death");
        // disable collider and script
        var col = GetComponent<Collider2D>();
        if (col != null) col.enabled = false;
        enabled = false;
        // notify UI
        if (UIManager.Instance != null)
            UIManager.Instance.AddKill(1);

        Destroy(gameObject, 0.5f);
    }
}