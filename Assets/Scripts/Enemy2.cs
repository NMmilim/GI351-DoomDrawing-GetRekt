using UnityEngine;

public class Enemy2 : MonoBehaviour
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

        // clear attacking flag after attackDuration and return to idle pose
        StartCoroutine(ClearAttackAfter(attackDuration));

        // set cooldown to avoid immediate retrigger from external code
        // (external systems can ignore this if they want faster repeats)
        // note: attackCooldown default is set in inspector
        // start strike timing (enables hitbox after strikeDelay)
        //StartStrike();
        //attackCooldownTimer = attackCooldown;
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
}