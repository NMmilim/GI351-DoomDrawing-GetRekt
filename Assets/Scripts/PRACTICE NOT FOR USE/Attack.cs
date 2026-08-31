using UnityEngine;

public class Attack : MonoBehaviour
{
    private float timeBTWAttack;
    public float startTimeBTWAttack;
    public Transform AttackPOS;
    public float AttackRange;
    public LayerMask WhatIsEnemies;
    public int damage;
    void Update()
    {
        if (timeBTWAttack <= 0)
        {
            // Attack logic here
            if(Input.GetKey(KeyCode.Space))
            {
                Collider2D[] enemiesToDamage = Physics2D.OverlapCircleAll(AttackPOS.position, AttackRange, WhatIsEnemies);
                for (int i = 0; i < enemiesToDamage.Length; i++)
                {
                    enemiesToDamage[i].GetComponent<Enemy>().TakeDamage(damage);
                }
            }
            timeBTWAttack = startTimeBTWAttack;
        }
        else
        {
            timeBTWAttack -= Time.deltaTime;
        }
    }
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(AttackPOS.position, AttackRange);
    }
}
