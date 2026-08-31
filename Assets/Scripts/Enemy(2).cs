using UnityEngine;

public class Enemy(2) : MonoBehaviour
{
    public float Speed = 1.5f;
    public float stopDistance = 1.2f;
    Rigidbody2D rb;
    Transform target;
    void start()
    {
        rb = GetComponent<Rigidbody2D>();
        GameObject p = GameObject.FindGameObjectWithTag("Player");
        if (p != null)
        {
            target = p.transform;
        }
    }

    
    void FixedUpdate()
    {
        if (target == null || rb == null) return;
        Vector2 current = rb.position;
        Vector2 playerPOS = target.position;
        if (Vector2.Distance(current, playerPOS) > stopDistance)
        {
            Vector2 next = Vector2.MoveTowards(current, playerPOS, Speed * Time.fixedDeltaTime);
            rb.MovePosition(next);
        }
    }
}
