using UnityEngine;

public class Enemy2 : MonoBehaviour
{
    public float speed = 1.5f;
    public float stopDistance = 1.2f;
    public Transform target;

    private bool Prep = false;
    private Rigidbody2D rb2d;
    [SerializeField] private Animator animator;

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
    }

    // Physics-friendly movement when Rigidbody2D is present
    void FixedUpdate()
    {
        if (target == null) return;

        if (rb2d != null)
        {
            Vector2 current = rb2d.position;
            Vector2 playerPos = target.position;
            float dist = Vector2.Distance(current, playerPos);
            bool moving = dist > stopDistance;

            if (animator != null)
                animator.SetBool("IsMoving", moving);

            if (moving)
            {
                Vector2 next = Vector2.MoveTowards(current, playerPos, speed * Time.fixedDeltaTime);
                rb2d.MovePosition(next);
                Prep = false;
            }
            else
            {
                if (!Prep)
                {
                    if (animator != null)
                    {
                        animator.SetBool("IsMoving", false);
                        animator.SetTrigger("Prepare");
                    }
                    Prep = true;
                }
            }
        }
    }

    // Fallback movement if no Rigidbody2D (simple transform-based)
    void Update()
    {
        if (rb2d != null) return; // handled in FixedUpdate
        if (target == null) return;
        if (Vector3.Distance(transform.position, target.position) > stopDistance)
            transform.position = Vector3.MoveTowards(transform.position, target.position, speed * Time.deltaTime);
    }
}