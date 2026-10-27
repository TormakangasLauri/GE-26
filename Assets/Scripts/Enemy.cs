using UnityEngine;

public class Enemy : MonoBehaviour
{
    public float health = 3f;
    public float force = 15f;
    public float hopForce = 4f;
    public float hopInterval = 1.5f;
    Rigidbody rb; Transform target; float nextHop;

    void Start() { rb = GetComponent<Rigidbody>(); target = GameObject.FindWithTag("Player").transform; }

    void FixedUpdate()
    {
        Vector3 dir = target.position - transform.position;
        dir.z = 0; // ignore z-axis because the game is 2D
        dir.Normalize();
        rb.AddForce(dir * force);
        rb.AddForce(dir * force);
        if (Time.time >= nextHop)
        {
            rb.AddForce(Vector3.up * hopForce, ForceMode.Impulse);
            nextHop = Time.time + hopInterval * Random.Range(0.7f, 1.3f);
        }
    }

    public void TakeDamage(float d) { health -= d; if (health <= 0) Destroy(gameObject); }
}