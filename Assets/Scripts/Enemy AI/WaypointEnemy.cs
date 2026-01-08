using UnityEngine;
using UnityEngine.AI;

public class WaypointEnemy : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Animator animator;
    public PlayerHealth playerHealth;
    public Transform waypointGroup;

    [Header("Settings")]
    public float detectionRadius = 15f;
    public float attackRange = 2f;
    public float attackCooldown = 2f;
    public float rotationSpeed = 7f;
    public float maxChaseDistance = 25f;
    public float attackDuration = 1f;

    private NavMeshAgent agent;
    private float cooldownTimer;
    private float attackTimer;
    private bool isAttacking;

    private Transform[] waypoints;
    private int currentWaypointIndex = 0;
    private Vector3 patrolStartPoint;

    private enum State { Patrol, Chase, Attack, Returning }
    private State currentState;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        if (player == null)
        {
            GameObject playerObj = GameObject.FindGameObjectWithTag("Player");
            if (playerObj) player = playerObj.transform;
        }

        if (playerHealth == null && player != null)
            playerHealth = player.GetComponent<PlayerHealth>();

        if (waypointGroup != null && waypointGroup.childCount > 1)
        {
            waypoints = new Transform[waypointGroup.childCount];
            for (int i = 0; i < waypointGroup.childCount; i++)
                waypoints[i] = waypointGroup.GetChild(i);

            patrolStartPoint = waypoints[0].position;
        }
        else
        {
            Debug.LogError("Waypoint group not assigned or has too few children.");
        }

        currentState = State.Patrol;
        GoToNextWaypoint();
    }

    void Update()
    {
        if (player == null || waypoints == null || waypoints.Length < 2) return;

        float distanceToPlayer = Vector3.Distance(transform.position, player.position);
        float distanceFromStart = Vector3.Distance(transform.position, patrolStartPoint);
        cooldownTimer -= Time.deltaTime;

        // Attack interruption logic
        if (isAttacking && distanceToPlayer > attackRange + 0.5f)
        {
            CancelAttack();
            currentState = State.Chase;
        }

        // State transitions (if not attacking)
        if (!isAttacking)
        {
            if (distanceToPlayer <= attackRange && cooldownTimer <= 0f)
                currentState = State.Attack;
            else if (distanceToPlayer <= detectionRadius && distanceFromStart <= maxChaseDistance)
                currentState = State.Chase;
            else if (distanceFromStart > maxChaseDistance)
                currentState = State.Returning;
            else
                currentState = State.Patrol;
        }

        // Behavior logic
        switch (currentState)
        {
            case State.Patrol: Patrol(); break;
            case State.Chase: ChasePlayer(); break;
            case State.Attack: Attack(); break;
            case State.Returning: ReturnToPatrol(); break;
        }

        // Animation
        animator.SetBool("isWalking", agent.velocity.magnitude > 0.1f && !isAttacking);

        if (!isAttacking)
            RotateTowardsMovementDirection();

        // Simulated attack timing
        if (isAttacking)
        {
            attackTimer -= Time.deltaTime;
            if (attackTimer <= 0f)
                EndAttack();
        }
    }

    void Patrol()
    {
        if (!agent.pathPending && agent.remainingDistance < 0.5f)
        {
            GoToNextWaypoint();
        }
    }

    void GoToNextWaypoint()
    {
        if (waypoints == null || waypoints.Length == 0) return;

        agent.SetDestination(waypoints[currentWaypointIndex].position);
        currentWaypointIndex = (currentWaypointIndex + 1) % waypoints.Length;
    }

    void ReturnToPatrol()
    {
        if (agent.remainingDistance < 0.5f && !agent.pathPending)
        {
            currentState = State.Patrol;
            GoToNextWaypoint();
        }
        else
        {
            agent.SetDestination(patrolStartPoint);
        }
    }

    void ChasePlayer()
    {
        if (agent.isOnNavMesh)
            agent.SetDestination(player.position);
    }

    void Attack()
    {
        if (isAttacking) return;

        if (Vector3.Distance(transform.position, player.position) > attackRange)
        {
            currentState = State.Chase;
            return;
        }

        isAttacking = true;
        cooldownTimer = attackCooldown;
        attackTimer = attackDuration;
        agent.ResetPath();

        Vector3 lookPos = new Vector3(player.position.x, transform.position.y, player.position.z);
        transform.rotation = Quaternion.LookRotation(lookPos - transform.position);

        animator.ResetTrigger("Attack");
        animator.SetTrigger("Attack");
    }

    public void DealDamage()
    {
        if (Vector3.Distance(transform.position, player.position) <= attackRange)
            playerHealth?.TakeDamage(10);
    }

    public void EndAttack()
    {
        isAttacking = false;
        animator.ResetTrigger("Attack");
    }

    public void CancelAttack()
    {
        if (!isAttacking) return;

        isAttacking = false;
        attackTimer = 0f;
        animator.ResetTrigger("Attack");
        animator.CrossFade("Walk", 0.1f); // Blend to Walk

        if (agent.isOnNavMesh)
            agent.SetDestination(player.position);
    }

    void RotateTowardsMovementDirection()
    {
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }
    }

#if UNITY_EDITOR
    void OnDrawGizmosSelected()
    {
        if (waypointGroup == null) return;

        Gizmos.color = Color.yellow;
        for (int i = 0; i < waypointGroup.childCount; i++)
        {
            Transform wp = waypointGroup.GetChild(i);
            Gizmos.DrawWireSphere(wp.position, 0.4f);
            if (i + 1 < waypointGroup.childCount)
                Gizmos.DrawLine(wp.position, waypointGroup.GetChild(i + 1).position);
        }
    }
#endif
}
