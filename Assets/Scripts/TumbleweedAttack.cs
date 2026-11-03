using UnityEngine;
using ThomasDev.HealthDamageSystem;

public class TumbleweedAttack : MonoBehaviour
{
    public float damage = 10f;

    void OnCollisionEnter(Collision collision)
    {
        if (!collision.gameObject.CompareTag("Player")) return;

        var health = collision.gameObject.GetComponentInParent<Health>();
        if (health != null) health.TakeDamage(damage);

        Destroy(gameObject);
    }
}