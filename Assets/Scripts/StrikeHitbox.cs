using UnityEngine;

// Simple strike hitbox component. Attach to a GameObject with a CircleCollider2D (isTrigger=true).
public class StrikeHitbox : MonoBehaviour
{
    public int damage = 1;
    public GameObject owner; // enemy that created this hitbox
    private bool used = false;

    void Awake()
    {
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (other == null) return;

        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                // let player resolve parry/block first
                EnemyController attacker = null;
                if (owner != null) attacker = owner.GetComponent<EnemyController>();

                bool wasParried;
                bool handled = pc.OnIncomingAttack(damage, attacker, out wasParried);
                if (!handled)
                {
                    pc.TakeDamage(damage);
                }

                used = true;
            }
        }
    }

    void OnDrawGizmos()
    {
        var col = GetComponent<CircleCollider2D>();
        if (col == null) return;
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position + (Vector3)col.offset, col.radius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + (Vector3)col.offset, col.radius);
    }
}
