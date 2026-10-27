using System.Collections;
using System.Collections.Generic;
using ThomasDev.HealthSystem;
using UnityEngine;
using UnityEngine.Events;

namespace ThomasDev.HealthDamageSystem
{
    [DisallowMultipleComponent]
    public class DamageDealer2D : MonoBehaviour
    {
        public enum DamageType { Physical, Fire, Ice, Poison, Other }

        [Header("Damage Settings")]
        [SerializeField, Tooltip("Amount of health to subtract.")]
        private float damageAmount = 10f;
        [SerializeField]
        private DamageType damageType = DamageType.Physical;

        [Header("Targeting")]
        [SerializeField, Tooltip("Which layers can be damaged. Leave empty to allow any layer.")]
        private LayerMask targetLayers = ~0; // default: everything
        [SerializeField, Tooltip("Optional tag filter. Leave empty to ignore tag filtering.")]
        private string targetTag = "";
        [SerializeField, Tooltip("If true, this will not damage objects on the same GameObject or its parents.")]
        private bool ignoreSelf = true;

        [Header("Hit Behavior")]
        [SerializeField, Tooltip("If true, this DamageDealer only deals damage once per collider (useful for projectiles).")]
        private bool singleHit = true;
        [SerializeField, Tooltip("If not singleHit, time in seconds between repeated hits while staying inside the trigger.")]
        private float repeatDamageInterval = 0.5f;

        [Header("Knockback (optional)")]
        [SerializeField, Tooltip("Apply knockback force to the hit object's Rigidbody2D if present.")]
        private bool applyKnockback = false;
        [SerializeField, Tooltip("Knockback force vector (local space relative to this object when using ForceMode2D.Impulse).")]
        private Vector2 knockback = new Vector2(0f, 0f);

        [Header("Misc")]
        [SerializeField, Tooltip("Destroy this object after dealing hit (common for projectiles).")]
        private bool destroyAfterHit = false;

        [Header("Events")]
        [SerializeField] private UnityEvent<GameObject> onHit;
        [SerializeField] private UnityEvent<GameObject> onKill;

        // Public read-only accessors
        public float DamageAmount => damageAmount;
        public DamageType Type => damageType;
        public LayerMask TargetLayers => targetLayers;
        public string TargetTag => targetTag;
        public bool IgnoreSelf => ignoreSelf;
        public bool SingleHit => singleHit;
        public float RepeatDamageInterval => repeatDamageInterval;
        public bool ApplyKnockback => applyKnockback;
        public Vector2 Knockback => knockback;
        public bool DestroyAfterHit => destroyAfterHit;

        public UnityEvent<GameObject> OnHit => onHit;
        public UnityEvent<GameObject> OnKill => onKill;

        // Tracks colliders already hit (for singleHit)
        private readonly HashSet<EntityId> hitColliders = new HashSet<EntityId>();

        // Tracks ongoing hit coroutines for repeated damage
        private readonly Dictionary<EntityId, Coroutine> repeatCoroutines = new Dictionary<EntityId, Coroutine>();


        private void Reset()
        {
            singleHit = true;
            destroyAfterHit = true;
        }

        private bool IsLayerInMask(int layer, LayerMask mask)
        {
            return (mask == (LayerMask)(-1)) || ((mask.value & (1 << layer)) != 0);
        }

        private bool PassesTagFilter(GameObject obj)
        {
            if (string.IsNullOrEmpty(targetTag)) return true;
            return obj.CompareTag(targetTag);
        }

        private bool IsSelfIgnored(GameObject other)
        {
            if (!ignoreSelf) return false;
            return other == gameObject || other.transform.IsChildOf(transform) || transform.IsChildOf(other.transform);
        }

        private void ApplyKnockbackIfPossible(Collider2D col)
        {
            if (!applyKnockback || knockback == Vector2.zero) return;

            Rigidbody2D rb = col.attachedRigidbody;
            if (rb != null)
            {
                Vector2 direction = (col.transform.position - transform.position).normalized;
                Vector2 force = new Vector2(
                    knockback.x * Mathf.Sign(direction.x == 0 ? 1f : direction.x),
                    knockback.y
                );
                rb.linearVelocity = force;
            }
        }

        private void DealDamageTo(GameObject target)
        {
            if (target == null) return;

            Health health = target.GetComponent<Health>();
            if (health == null) return;

            health.TakeDamage(damageAmount);

            onHit?.Invoke(target);

            Collider2D col = target.GetComponent<Collider2D>() ?? target.GetComponentInChildren<Collider2D>();
            if (col != null)
                ApplyKnockbackIfPossible(col);

            if (health.CurrentHealth <= 0f)
                onKill?.Invoke(target);
        }

        private void OnTriggerEnter2D(Collider2D other) => TryHandleHit(other);
        private void OnCollisionEnter2D(Collision2D collision) => TryHandleHit(collision.collider);

        private void TryHandleHit(Collider2D other)
        {
            if (other == null) return;
            GameObject otherObj = other.gameObject;

            if (IsSelfIgnored(otherObj)) return;
            if (!IsLayerInMask(otherObj.layer, targetLayers)) return;
            if (!PassesTagFilter(otherObj)) return;

            EntityId id = other.GetEntityId();

            if (singleHit)
            {
                if (hitColliders.Contains(id)) return;
                hitColliders.Add(id);

                DealDamageTo(otherObj);

                if (destroyAfterHit)
                    Destroy(gameObject);
            }
            else
            {
                if (!repeatCoroutines.ContainsKey(id))
                {
                    Coroutine c = StartCoroutine(RepeatDamageCoroutine(other, id));
                    repeatCoroutines.Add(id, c);
                }
            }
        }

        private IEnumerator RepeatDamageCoroutine(Collider2D other, EntityId id)
        {
            while (true)
            {
                if (other == null) break;
                GameObject otherObj = other.gameObject;

                if (IsSelfIgnored(otherObj)) break;
                if (!IsLayerInMask(otherObj.layer, targetLayers)) break;
                if (!PassesTagFilter(otherObj)) break;

                DealDamageTo(otherObj);

                if (singleHit) break;

                yield return new WaitForSeconds(Mathf.Max(0.01f, repeatDamageInterval));
            }

            if (repeatCoroutines.ContainsKey(id))
                repeatCoroutines.Remove(id);
        }

        private void OnTriggerExit2D(Collider2D other)
        {
            if (other == null) return;
            EntityId id = other.GetEntityId();
            if (repeatCoroutines.TryGetValue(id, out Coroutine c))
            {
                StopCoroutine(c);
                repeatCoroutines.Remove(id);
            }
        }

        private void OnDisable()
        {
            foreach (var kv in repeatCoroutines)
                if (kv.Value != null) StopCoroutine(kv.Value);

            repeatCoroutines.Clear();
        }

        public void ResetHitHistory() => hitColliders.Clear();
    }
}
