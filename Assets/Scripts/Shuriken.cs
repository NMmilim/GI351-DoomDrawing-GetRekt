using UnityEngine;

public class Shuriken : MonoBehaviour
{
    public GameObject owner;
    public int damage = 1;
    public float speed = 6f;
    public float lifeTime = 3f;

    private Vector2 velocity;
    private bool returning = false;
    private Transform ownerTransform;

    public void Launch(Vector2 dir)
    {
        velocity = dir.normalized * speed;
        // schedule destroy
        Destroy(gameObject, lifeTime);
        if (owner != null) ownerTransform = owner.transform;
    }

    void FixedUpdate()
    {
        if (returning)
        {
            if (ownerTransform == null)
                return;

            Vector2 toOwner = (ownerTransform.position - transform.position);
            float dist = toOwner.magnitude;
            if (dist < 0.1f)
            {
                Destroy(gameObject);
                return;
            }

            Vector2 dir = toOwner.normalized;
            transform.position += (Vector3)(dir * speed * Time.fixedDeltaTime);
            return;
        }

        if (velocity.sqrMagnitude > 0f)
        {
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        if (returning)
        {
            // only collide with owner when returning
            if (owner != null && other.gameObject == owner)
            {
                Destroy(gameObject);
            }
            return;
        }

        if (other.CompareTag("Player"))
        {
            var pc = other.GetComponent<PlayerController>();
            if (pc != null)
            {
                // let player decide if parry/block handles it
                EnemyController attacker = null;
                if (owner != null) attacker = owner.GetComponent<EnemyController>();

                bool wasParried;
                bool handled = pc.OnIncomingAttack(damage, attacker, out wasParried);
                if (!handled)
                {
                    pc.TakeDamage(damage);
                }
                else
                {
                    if (wasParried)
                    {
                        // on parry, send the shuriken back to its owner
                        BeginReturnToOwner();
                        return;
                    }
                    // handled (blocked) but not parried -> destroy
                }
                // destroy shuriken in non-return cases
                Destroy(gameObject);
            }
        }
        else
        {
            // hit world or other, destroy
            // ignore owner collisions
            if (owner != null && other.gameObject == owner) return;
            Destroy(gameObject);
        }
    }

    private void BeginReturnToOwner()
    {
        returning = true;
        // cancel any scheduled destroy and reschedule for return path
        CancelInvoke();
        Destroy(gameObject, lifeTime); // ensure eventual cleanup
        // stop current velocity; will move toward owner's current position in FixedUpdate
        velocity = Vector2.zero;
    }
}
