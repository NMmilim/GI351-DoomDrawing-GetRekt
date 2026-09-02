using UnityEngine;

// Simple strike hitbox component. Attach to a GameObject with a CircleCollider2D (isTrigger=true).
public class StrikeHitbox : MonoBehaviour
{
    public int damage = 1;
    public GameObject owner; // enemy that created this hitbox
    private bool used = false;

    void Awake()
    {
        // ensure collider is trigger
        var col = GetComponent<Collider2D>();
        if (col != null) col.isTrigger = true;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (used) return;
        if (other == null) return;

        Debug.Log($"[StrikeHitbox] OnTriggerEnter2D: owner={(owner!=null?owner.name:"null")}, other={(other.gameObject!=null?other.gameObject.name:"null")}");

        // only affect player
        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                // ask player if incoming attack is handled (parry/block)
                IDamageable attacker = null;
                if (owner != null) attacker = owner.GetComponent<IDamageable>();
                Debug.Log($"[StrikeHitbox] Passing attacker={(attacker as UnityEngine.Object)?.name} to OnIncomingAttack");
                bool wasParried;
                bool handled = pc.OnIncomingAttack(damage, attacker, out wasParried);
                if (!handled)
                {
                    // player didn't block/parry -> apply damage
                    pc.TakeDamage(damage);
                }

                used = true;
            }
        }
    }

    void OnDrawGizmos()
    {
        // draw the hitbox in scene/game view for debugging
        var col = GetComponent<CircleCollider2D>();
        if (col == null) return;

        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawSphere(transform.position + (Vector3)col.offset, col.radius);
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position + (Vector3)col.offset, col.radius);
    }
}
