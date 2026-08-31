using UnityEngine;

public class Enemy : MonoBehaviour
{
    public int Health;
    public float speed;
    private Animator anim;
    void Start()
    {
        anim= GetComponent<Animator>();
        anim.SetBool("IsRunning", true);    
    }

    
    void Update()
    {
      transform.Translate(Vector2.left * speed * Time.deltaTime);
    }
    public void TakeDamage(int damage)
    {
        Health -= damage;
        Debug.Log("Enemy took damage" + damage + "Current health: " + Health);

    }
}
