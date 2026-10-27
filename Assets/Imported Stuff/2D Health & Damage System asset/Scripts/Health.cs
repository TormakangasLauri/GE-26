using UnityEngine;
using UnityEngine.Events;

namespace ThomasDev.HealthDamageSystem
{
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour
    {


        [Header("Health Settings")]
        [SerializeField] private float maxHealth = 100f;
        [SerializeField] private bool destroyOnDeath = true;

        [Header("Invincibility")]
        [Tooltip("Time in seconds after taking damage before you can be hurt again.")]
        [SerializeField] private float invincibilityDuration = 0f;

        [Header("Events")]
        public UnityEvent<float, float> OnDamaged;   // (currentHealth, maxHealth)
        public UnityEvent<float, float> OnHealed;    // (currentHealth, maxHealth)
        public UnityEvent <float> OnHealthChanged;
        public UnityEvent OnDeath;

        private float currentHealth;
        private bool isInvincible;
        private float invincibilityTimer;

        public float MaxHealth => maxHealth;
        public float CurrentHealth => currentHealth;


        private void Awake()
        {
            currentHealth = maxHealth;
        }


        private void Update()
        {
            if (isInvincible)
            {
                invincibilityTimer -= Time.deltaTime;
                if (invincibilityTimer <= 0f)
                {
                    isInvincible = false;
                }
            }
        }

        /// <summary>
        /// Reduces current health by damage amount.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (isInvincible || currentHealth <= 0f) return;

            currentHealth = Mathf.Max(currentHealth - amount, 0f);
            OnDamaged?.Invoke(currentHealth, maxHealth);

            if (invincibilityDuration > 0f)
            {
                isInvincible = true;
                invincibilityTimer = invincibilityDuration;
            }

            if (currentHealth <= 0f)
                Die();
        }

        /// <summary>
        /// Restores health by given amount.
        /// </summary>
        public void Heal(float amount)
        {
            if (currentHealth <= 0f) return;

            currentHealth = Mathf.Min(currentHealth + amount, maxHealth);
            OnHealed?.Invoke(currentHealth, maxHealth);
        }

        /// <summary>
        /// Instantly kills the object (sets health to 0 and calls Die()).
        /// </summary>
        public void Kill()
        {
            if (currentHealth <= 0f) return;
            currentHealth = 0f;
            Die();
        }

        private void Die()
        {
            OnDeath?.Invoke();

            if (destroyOnDeath)
                Destroy(gameObject);
        }

        /// <summary>
        /// Fully restores health to max.
        /// </summary>
        public void ResetHealth()
        {
            currentHealth = maxHealth;
            OnHealed?.Invoke(currentHealth, maxHealth);
        }
    }
}
