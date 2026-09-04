using UnityEngine;

[RequireComponent(typeof(Collider2D), typeof(Rigidbody2D))]
public class Shuriken : MonoBehaviour
{
    public GameObject owner;
    public int damage = 1;
    public float speed = 6f;
    public float hitRange = 0.3f; // how close to player before it counts as a hit

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
        rb.linearVelocity = dir.normalized * speed;
    }

    private void Update()
    {
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
