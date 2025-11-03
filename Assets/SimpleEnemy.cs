using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class SimpleEnemy : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 2f;
    public float rotationSpeed = 8f;
    public float changeDirectionTime = 3f;

    [Header("Ground (drag a Plane or other floor here)")]
    [Tooltip("Drag the Plane (or any GameObject with a Collider) from the Hierarchy here.")]
    public Collider groundCollider;
    public float groundCheckDistance = 1.2f;
    public float maxSlopeAngle = 60f;

    [Header("Physics / Gravity")]
    public float gravity = -9.81f;

    [Header("Health Settings")]
    public int maxHealth = 3;
    private int currentHealth;

    [Header("Punch Detection (trigger or collision)")]
    public float punchForceThreshold = 1.0f;        // min velocity magnitude to register a hit
    public float hitForceMultiplier = 1.5f;         // scales applied impulse
    [Tooltip("Minimum seconds between hits from the same hand (prevents continuous damage).")]
    public float hitCooldown = 0.25f;

    [Header("Stun / ignore collisions")]
    [Tooltip("Time to ignore collisions between this enemy and the hitting hand after a successful hit")]
    public float stunDuration = 0.25f;

    [Header("Disappear / Fade")]
    [Tooltip("Duration of fade to transparent in seconds.")]
    public float fadeDuration = 1.0f;
    [Tooltip("Extra delay after fade before object is destroyed.")]
    public float disappearDelayAfterFade = 0.1f;

    [Header("Hand sampling")]
    [Tooltip("How often (seconds) to refresh the list of Hand objects in the scene.")]
    public float handRefreshInterval = 2f;

    [Header("Debug")]
    public bool debugPunch = true;

    // internals
    private Vector3 moveDirection;
    private float directionTimer;
    private Rigidbody rb;
    private Collider col;
    private float verticalVelocity = 0f;
    private bool grounded = false;
    private Vector3 groundNormal = Vector3.up;
    private Renderer cachedRenderer;

    // prevent repeated hits per hand
    private Dictionary<GameObject, float> lastHitTime = new Dictionary<GameObject, float>();

    // hand sampling storage (used as fallback if no HandVelocity component)
    private List<Transform> handTransforms = new List<Transform>();
    private Dictionary<Transform, Vector3> handLastPositions = new Dictionary<Transform, Vector3>();
    private float handRefreshTimer = 0f;

    // store temporarily ignored collisions (so we can restore)
    private List<(Collider handCollider, float restoreTime)> ignoredCollisions = new List<(Collider, float)>();

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        col = GetComponent<Collider>();

        cachedRenderer = GetComponent<Renderer>();
        if (cachedRenderer == null)
            cachedRenderer = GetComponentInChildren<Renderer>();
    }

    void Start()
    {
        // ensure dynamic body for impulses
        rb.freezeRotation = true;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        currentHealth = maxHealth;
        ChangeDirection();
        RefreshHands();
    }

    void Update()
    {
        directionTimer -= Time.deltaTime;
        if (directionTimer <= 0f)
            ChangeDirection();

        UpdateHandPositions();

        handRefreshTimer += Time.deltaTime;
        if (handRefreshTimer >= handRefreshInterval)
        {
            handRefreshTimer = 0f;
            RefreshHands();
        }

        // restore ignored collisions when time elapses
        if (ignoredCollisions.Count > 0)
        {
            float now = Time.time;
            for (int i = ignoredCollisions.Count - 1; i >= 0; i--)
            {
                var entry = ignoredCollisions[i];
                if (now >= entry.restoreTime)
                {
                    if (entry.handCollider != null && col != null)
                    {
                        Physics.IgnoreCollision(col, entry.handCollider, false);
                        if (debugPunch) Debug.Log($"[SimpleEnemy] Restored collision with {entry.handCollider.name}");
                    }
                    ignoredCollisions.RemoveAt(i);
                }
            }
        }
    }

    void FixedUpdate()
    {
        GroundCheck();

        Vector3 horizontalMove = Vector3.zero;
        if (grounded)
        {
            Vector3 projected = Vector3.ProjectOnPlane(moveDirection, groundNormal);
            if (projected.sqrMagnitude > 0.0001f)
                horizontalMove = projected.normalized * moveSpeed;
            else
                horizontalMove = Vector3.zero;

            verticalVelocity = 0f;
        }
        else
        {
            verticalVelocity += gravity * Time.fixedDeltaTime;
        }

        Vector3 move = horizontalMove + Vector3.up * verticalVelocity;
        Vector3 targetPos = rb.position + move * Time.fixedDeltaTime;

        // Snap to ground when grounded using the configured groundCollider
        if (grounded && groundCollider != null)
        {
            Ray downRay = new Ray(targetPos + Vector3.up * 0.2f, Vector3.down);
            if (groundCollider.Raycast(downRay, out RaycastHit hit, groundCheckDistance))
            {
                float halfHeight = col.bounds.extents.y;
                targetPos.y = hit.point.y + halfHeight;
            }
        }

        rb.MovePosition(targetPos);

        // rotate to face movement direction (horizontal only)
        Vector3 lookDir = horizontalMove;
        lookDir.y = 0f;
        if (lookDir.sqrMagnitude > 0.0001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(lookDir);
            rb.rotation = Quaternion.Slerp(rb.rotation, targetRotation, rotationSpeed * Time.fixedDeltaTime);
        }
    }

    void GroundCheck()
    {
        Vector3 origin = rb.position + Vector3.up * 0.2f;
        Ray downRay = new Ray(origin, Vector3.down);
        RaycastHit hit;

        if (groundCollider != null)
        {
            if (groundCollider.Raycast(downRay, out hit, groundCheckDistance))
            {
                float slope = Vector3.Angle(hit.normal, Vector3.up);
                if (slope <= maxSlopeAngle)
                {
                    grounded = true;
                    groundNormal = hit.normal;
                    return;
                }
            }
        }

        grounded = false;
        groundNormal = Vector3.up;
    }

    void ChangeDirection()
    {
        moveDirection = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
        directionTimer = changeDirectionTime;
    }

    // ---------- Hand discovery & sampling ----------
    void RefreshHands()
    {
        handTransforms.Clear();
        handLastPositions.Clear();

        GameObject[] hands = GameObject.FindGameObjectsWithTag("Hand");
        for (int i = 0; i < hands.Length; i++)
        {
            var t = hands[i].transform;
            handTransforms.Add(t);
            handLastPositions[t] = t.position;
        }

        if (debugPunch) Debug.Log($"[SimpleEnemy] Found {handTransforms.Count} hands");
    }

    void UpdateHandPositions()
    {
        foreach (var t in handTransforms)
        {
            if (t == null) continue;
            handLastPositions[t] = t.position;
        }
    }

    // ---------- Unified hit handler ----------
    // Called by trigger or collision entry to process a hit once
    void HandlePotentialHit(Collider handCollider, Vector3? explicitVelocity = null)
    {
        if (handCollider == null) return;
        GameObject handObj = handCollider.gameObject;

        // cooldown
        float last;
        if (lastHitTime.TryGetValue(handObj, out last))
        {
            if (Time.time - last < hitCooldown)
            {
                if (debugPunch) Debug.Log("[SimpleEnemy] Hit ignored due to cooldown.");
                return;
            }
        }

        // Determine impact magnitude and velocity
        Vector3 handVel = Vector3.zero;
        float impactMag = 0f;

        if (explicitVelocity.HasValue)
        {
            handVel = explicitVelocity.Value;
            impactMag = handVel.magnitude;
            if (debugPunch) Debug.Log($"[SimpleEnemy] Using explicitVelocity mag={impactMag:F3}");
        }

        // Try to read a 'HandVelocity' component if it exists on hand
        if (impactMag <= 0f)
        {
            var hvComp = handCollider.GetComponent("HandVelocity") as Component;
            if (hvComp != null)
            {
                var prop = hvComp.GetType().GetProperty("CurrentVelocity");
                if (prop != null)
                {
                    object val = prop.GetValue(hvComp, null);
                    if (val is Vector3 v)
                    {
                        handVel = v;
                        impactMag = handVel.magnitude;
                        if (debugPunch) Debug.Log($"[SimpleEnemy] Read HandVelocity component mag={impactMag:F3}");
                    }
                }
            }
        }

        // Fallback to sampled transform velocity (cached last positions)
        if (impactMag <= 0f)
        {
            Transform ht = handCollider.transform;
            if (handLastPositions.TryGetValue(ht, out Vector3 lastPos))
            {
                float dt = Mathf.Max(Time.deltaTime, 1e-6f);
                handVel = (ht.position - lastPos) / dt;
                impactMag = handVel.magnitude;
                if (debugPunch) Debug.Log($"[SimpleEnemy] Sampled hand vel mag={impactMag:F3}");
            }
        }

        // Final fallback to attached Rigidbody
        if (impactMag <= 0f)
        {
            Rigidbody otherRb = handCollider.attachedRigidbody;
            if (otherRb != null)
            {
                handVel = otherRb.linearVelocity;
                impactMag = otherRb.linearVelocity.magnitude * otherRb.mass;
                if (debugPunch) Debug.Log($"[SimpleEnemy] Using attachedRigidbody vel mag={otherRb.linearVelocity.magnitude:F3} mass={otherRb.mass:F3}");
            }
        }

        // last tiny fallback so touches still do something
        if (impactMag <= 0f)
        {
            impactMag = 0.5f;
            handVel = (transform.position - handCollider.transform.position).normalized * impactMag;
            if (debugPunch) Debug.Log("[SimpleEnemy] Falling back to tiny impact");
        }

        // compute direction and impulse
        Vector3 hitDir = (transform.position - handCollider.transform.position).normalized;
        if (hitDir.sqrMagnitude < 0.0001f) hitDir = Vector3.up;
        Vector3 appliedImpulse = hitDir * impactMag * hitForceMultiplier;

        if (debugPunch) Debug.Log($"[SimpleEnemy] impactMag={impactMag:F3} appliedImpulse={appliedImpulse}");

        if (impactMag > punchForceThreshold)
        {
            lastHitTime[handObj] = Time.time;

            // Apply the impulse
            if (rb != null)
            {
                rb.AddForce(appliedImpulse, ForceMode.Impulse);
            }

            // Prevent continuous push by temporarily disabling collisions with this hand
            TryTemporarilyIgnoreCollisionWithHand(handCollider, stunDuration);

            // Damage / visuals
            currentHealth -= 1;
            StartCoroutine(FlashRed());

            if (currentHealth <= 0)
                Die();
        }
        else
        {
            if (debugPunch) Debug.Log($"[SimpleEnemy] Impact below threshold ({impactMag:F3} <= {punchForceThreshold})");
        }
    }

    // Temporarily ignore collisions between the enemy collider and the specific hand collider
    void TryTemporarilyIgnoreCollisionWithHand(Collider handCollider, float duration)
    {
        if (handCollider == null || col == null) return;

        // If already ignoring collisions with this collider, skip
        for (int i = 0; i < ignoredCollisions.Count; i++)
            if (ignoredCollisions[i].handCollider == handCollider)
                return;

        Physics.IgnoreCollision(col, handCollider, true);
        ignoredCollisions.Add((handCollider, Time.time + duration));
        if (debugPunch) Debug.Log($"[SimpleEnemy] Ignoring collisions with {handCollider.name} for {duration}s");
    }

    // ---------- Trigger and Collision entry handlers ----------
    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Hand")) return;
        HandlePotentialHit(other, null);
    }

    void OnCollisionEnter(Collision collision)
    {
        // If the hand is physics-based (non-trigger collision), we still handle a single hit.
        if (!collision.gameObject.CompareTag("Hand")) return;

        // Compute fallback velocity from collision relative velocity if possible
        Vector3 fallbackVel = collision.relativeVelocity;
        // Use the collision's first contact point direction for better impulse direction
        if (collision.contacts != null && collision.contacts.Length > 0)
        {
            Vector3 contactNormal = collision.contacts[0].normal;
            // relativeVelocity should roughly represent the hitting speed
        }

        HandlePotentialHit(collision.collider, fallbackVel);
    }

    // ---------- Visuals & death ----------
    System.Collections.IEnumerator FlashRed()
    {
        if (cachedRenderer == null) yield break;
        Material mat = cachedRenderer.material; // instantiates if needed
        string prop = mat.HasProperty("_Color") ? "_Color" : (mat.HasProperty("_BaseColor") ? "_BaseColor" : null);
        if (prop == null) yield break;

        Color originalColor = mat.GetColor(prop);
        mat.SetColor(prop, Color.red);
        yield return new WaitForSeconds(0.15f);
        if (cachedRenderer != null)
            mat.SetColor(prop, originalColor);
    }

    void Die()
    {
        if (col != null) col.enabled = false;
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.detectCollisions = false;
        }

        this.enabled = false;
        gameObject.tag = "Untagged";
        StartCoroutine(FadeAndDestroy());
    }

    private struct MatInfo
    {
        public Material mat;
        public string colorProp;
        public Color originalColor;
        public int originalRenderQueue;
        public string[] originalKeywords;
    }

    System.Collections.IEnumerator FadeAndDestroy()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        List<MatInfo> mats = new List<MatInfo>();

        foreach (var r in renderers)
        {
            Material[] rMats = r.materials;
            for (int i = 0; i < rMats.Length; i++)
            {
                var m = rMats[i];
                if (m == null) continue;

                string colorProp = m.HasProperty("_Color") ? "_Color"
                                 : m.HasProperty("_BaseColor") ? "_BaseColor"
                                 : null;

                if (colorProp == null)
                {
                    mats.Add(new MatInfo
                    {
                        mat = m,
                        colorProp = null,
                        originalColor = Color.white,
                        originalRenderQueue = m.renderQueue,
                        originalKeywords = m.shaderKeywords
                    });
                }
                else
                {
                    mats.Add(new MatInfo
                    {
                        mat = m,
                        colorProp = colorProp,
                        originalColor = m.GetColor(colorProp),
                        originalRenderQueue = m.renderQueue,
                        originalKeywords = m.shaderKeywords
                    });
                }

                TryMakeMaterialTransparent(m);
            }
        }

        float t = 0f;
        while (t < fadeDuration)
        {
            float alpha = 1f - (t / Mathf.Max(0.0001f, fadeDuration));
            for (int i = 0; i < mats.Count; i++)
            {
                var info = mats[i];
                if (info.colorProp == null) continue;
                Color c = info.originalColor;
                c.a = alpha;
                info.mat.SetColor(info.colorProp, c);
            }
            t += Time.deltaTime;
            yield return null;
        }

        for (int i = 0; i < mats.Count; i++)
        {
            var info = mats[i];
            if (info.colorProp == null) continue;
            Color c = info.originalColor;
            c.a = 0f;
            info.mat.SetColor(info.colorProp, c);
        }

        yield return new WaitForSeconds(disappearDelayAfterFade);
        Destroy(gameObject);
    }

    void TryMakeMaterialTransparent(Material m)
    {
        if (m == null) return;

        if (m.HasProperty("_Mode"))
        {
            m.SetFloat("_Mode", 2f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = 3000;
            try
            {
                m.DisableKeyword("_ALPHATEST_ON");
                m.EnableKeyword("_ALPHABLEND_ON");
                m.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            }
            catch { }
        }

        if (m.HasProperty("_Surface"))
        {
            m.SetFloat("_Surface", 1f);
            m.renderQueue = 3000;
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 start;
        if (rb != null)
            start = rb.position + Vector3.up * 0.2f;
        else
            start = transform.position + Vector3.up * 0.2f;
        Gizmos.DrawLine(start, start + Vector3.down * groundCheckDistance);
    }
#endif
}
