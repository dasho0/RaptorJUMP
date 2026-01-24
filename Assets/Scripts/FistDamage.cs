using UnityEngine;
using UnityEngine.InputSystem;

public class FistDamage : MonoBehaviour
{
    [Header("Damage Settings")]
    public int baseDamage = 10;
    public float hitCooldown = 0.5f;

    [Header("Fist Input")]
    public InputActionReference gripAction;
    public float gripThreshold = 0.5f;

    [Header("Velocity Damage")]
    public bool useVelocityDamage = false;
    public float velocityMultiplier = 5f;
    public float minVelocityToHit = 1f;

    private float lastHitTime;
    private Vector3 lastPosition;
    private Vector3 velocity;

    void Update()
    {
        velocity = (transform.position - lastPosition) / Time.deltaTime;
        lastPosition = transform.position;
    }

    bool IsFistClenched()
    {
        if (gripAction == null || gripAction.action == null)
            return false;

        return gripAction.action.ReadValue<float>() >= gripThreshold;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsFistClenched()) return;

        if (Time.time - lastHitTime < hitCooldown) return;

        EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
        if (enemyHealth == null)
        {
            enemyHealth = other.GetComponentInParent<EnemyHealth>();
        }

        if (enemyHealth != null)
        {
            int damage = CalculateDamage();

            if (damage > 0)
            {
                enemyHealth.TakeDamage(damage);
                lastHitTime = Time.time;
                Debug.Log("Punch! Damage: " + damage);
            }
        }
    }

    void OnTriggerStay(Collider other)
    {
        OnTriggerEnter(other);
    }

    int CalculateDamage()
    {
        if (useVelocityDamage)
        {
            if (velocity.magnitude < minVelocityToHit)
                return 0;

            return Mathf.RoundToInt(velocity.magnitude * velocityMultiplier);
        }
        else
        {
            return baseDamage;
        }
    }
}