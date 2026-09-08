using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class Shuriken : MonoBehaviour
{
    public GameObject owner;
    public int damage = 1;
    public float speed = 6f;
    public float hitRange = 0.3f; // how close to player before it counts as a hit

    // When true the shuriken will scale its speed by the HeartRate multiplier at launch.
    // If you prefer controlling speed centrally in EnemyController, set this false and adjust there instead.
    public bool followHeartRate = true;

    // Rotation animation: degrees per second
    [Tooltip("Rotation speed in degrees per second (positive = clockwise)")]
    public float rotationSpeed = 720f;

    private Rigidbody2D rb;
    private Transform player;
    private bool used = false; // prevent double-hit processing

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();               
        rb.gravityScale = 0f;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;

        // find player once
        GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
        if (playerObj != null) player = playerObj.transform;
    }

    public void Launch(Vector2 dir)
    {
        // Apply heart-rate multiplier at launch if enabled.
        float finalSpeed = speed;
        if (followHeartRate && HeartRate.Instance != null)
        {
            finalSpeed = speed * HeartRate.Instance.GetAttackSpeedMultiplier();
        }

        // use Rigidbody2D.velocity (correct API) to set linear velocity
        if (rb != null)
            rb.linearVelocity = dir.normalized * finalSpeed;

        // optional: align initial rotation to travel direction
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        transform.rotation = Quaternion.Euler(0f, 0f, angle);
    }

    // Stop movement and disable further processing. Called on game over.
    public void StopMotion()
    {
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }

        // prevent Update/OnTrigger from processing further
        enabled = false;
    }

    private void Update()
    {
        // simple visual rotation every frame
        if (Mathf.Abs(rotationSpeed) > 0f)
        {
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }

        if (used) return;
        if (player == null) return;

        // check distance to player
        float dist = Vector2.Distance(transform.position, player.position);
        if (dist <= hitRange)
        {
            used = true;

            var pc = player.GetComponent<PlayerController>();
            if (pc != null)
            {
                EnemyController attacker = owner != null ? owner.GetComponent<EnemyController>() : null;

                bool wasParried;
                bool handled = pc.OnIncomingAttack(damage, attacker, out wasParried);

                if (!handled)
                {
                    pc.TakeDamage(damage);
                }
                else if (wasParried)
                {
                    Debug.Log("Shuriken parried!");

                    // Award score for successful parry
                    UIManager.Instance?.AddScore(1);
                    Debug.Log("[Shuriken] Called UIManager.AddScore(1)");

                    Destroy(gameObject);
                    return;
                }
            }

            // always destroy once it reaches player
            Destroy(gameObject);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;

        // still destroy if it hits environment
        if (other != null && !other.CompareTag("Player"))
        {
            if (owner != null && other.gameObject == owner) return;
            Destroy(gameObject);
        }
    }
}
