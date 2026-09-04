using UnityEngine;

public class Shuriken : MonoBehaviour
{
    public GameObject owner;
    public int damage = 1;
    public float speed = 6f;
    public float lifeTime = 3f;

    private Vector2 velocity;
    // no return behavior; destroyed on parry

    public void Launch(Vector2 dir)
    {
        velocity = dir.normalized * speed;
        // schedule destroy
        Destroy(gameObject, lifeTime);
      
    }

    void FixedUpdate()
    {
  

        if (velocity.sqrMagnitude > 0f)
        {
            transform.position += (Vector3)(velocity * Time.fixedDeltaTime);
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (other == null) return;
        // no return behavior: handle collision normally

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
                        // on parry, destroy the shuriken
                        Destroy(gameObject);
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

    // no return behavior
}
