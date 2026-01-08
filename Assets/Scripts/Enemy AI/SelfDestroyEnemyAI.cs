using UnityEngine;
using UnityEngine.AI;

public class SelfDestroyEnemyAI : MonoBehaviour
{
    [Header("Settings")]
    public float detectionRadius = 15f;
    public float attackRange = 2f;
    public float patrolRadius = 20f;
    public float attackCooldown = 2f;
    public float patrolIdleTime = 3f;
    public float rotationSpeed = 7f;
    public float destroyAfterSeconds = 30f;

    [Header("Attack Settings")]
    public float attackDuration = 1.0f; // Adjust this to match your attack animation length

    private Transform player;
    private NavMeshAgent agent;
    private Animator animator;

    private float cooldownTimer;
    private float idleTimer;
    private float destroyTimer;
    private float attackTimer;

    private Vector3 patrolPoint;

    private bool isPatrolling;
    private bool isIdle;
    private bool isAttacking;
    private bool playerFound;

    private enum State { Patrol, Chase, Attack }
    private State currentState = State.Patrol;

    void Start()
    {
        agent = GetComponent<NavMeshAgent>();
        animator = GetComponent<Animator>();

        destroyTimer = destroyAfterSeconds;
        SetNewPatrolPoint();
    }

    void Update()
{
    FindPlayerIfNeeded();

    float distanceToPlayer = player ? Vector3.Distance(transform.position, player.position) : Mathf.Infinity;

    cooldownTimer -= Time.deltaTime;

    // Handle destroy timer when player is missing or out of detection range
    if (player == null || distanceToPlayer > detectionRadius)
    {
        destroyTimer -= Time.deltaTime;
        if (destroyTimer <= 0f)
        {
            Destroy(gameObject);
            return;
        }
    }
    else
    {
        destroyTimer = destroyAfterSeconds; // Reset the timer when player is nearby
    }

    // Cancel attack immediately if player leaves attack range
    if (isAttacking && distanceToPlayer > attackRange)
    {
        CancelAttack();
        currentState = State.Chase;
    }

    // Decide next state if not in attack
    if (!isAttacking)
    {
        if (distanceToPlayer <= attackRange && cooldownTimer <= 0f)
            currentState = State.Attack;
        else if (distanceToPlayer <= detectionRadius)
            currentState = State.Chase;
        else
            currentState = State.Patrol;
    }

    // Perform current state behavior
    switch (currentState)
    {
        case State.Patrol: Patrol(); break;
        case State.Chase: Chase(); break;
        case State.Attack: TryAttack(); break;
    }

    animator.SetBool("isWalking", agent.velocity.magnitude > 0.1f && !isAttacking);

    if (!isAttacking)
        RotateToVelocity();

    // Handle attack timer (no animation event)
    if (isAttacking)
    {
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f)
        {
            EndAttack(); // Simulate event
        }
    }
}


    void FindPlayerIfNeeded()
{
    if (player == null)
    {
        GameObject found = GameObject.FindGameObjectWithTag("Player");
        if (found)
        {
            player = found.transform;
        }
    }
}


    void Patrol()
    {
        if (isIdle)
        {
            idleTimer += Time.deltaTime;
            if (idleTimer >= patrolIdleTime)
            {
                SetNewPatrolPoint();
                idleTimer = 0f;
            }
            return;
        }

        if (!isPatrolling || Vector3.Distance(transform.position, patrolPoint) < 1.5f)
        {
            isIdle = true;
            isPatrolling = false;
            agent.ResetPath();
        }
    }

    void SetNewPatrolPoint()
    {
        Vector3 random = Random.insideUnitSphere * patrolRadius + transform.position;

        if (NavMesh.SamplePosition(random, out NavMeshHit hit, patrolRadius, NavMesh.AllAreas))
        {
            patrolPoint = hit.position;
            agent.SetDestination(patrolPoint);
            isPatrolling = true;
            isIdle = false;
        }
    }

    void Chase()
    {
        isIdle = false;
        isPatrolling = false;

        if (agent.isOnNavMesh && player != null)
            agent.SetDestination(player.position);
    }

    void TryAttack()
    {
        float distance = Vector3.Distance(transform.position, player.position);
        if (distance > attackRange)
        {
            currentState = State.Chase;
            return;
        }

        if (cooldownTimer <= 0f && !isAttacking)
        {
            isAttacking = true;
            cooldownTimer = attackCooldown;
            attackTimer = attackDuration;

            agent.ResetPath();

            // Face player
            Vector3 look = new Vector3(player.position.x, transform.position.y, player.position.z);
            transform.rotation = Quaternion.LookRotation(look - transform.position);

            animator.ResetTrigger("Attack");
            animator.SetTrigger("Attack");
        }
    }

    void EndAttack()
    {
        isAttacking = false;

        float distance = Vector3.Distance(transform.position, player.position);

        if (distance <= attackRange)
        {
            if (cooldownTimer <= 0f)
                currentState = State.Attack;
            else
                currentState = State.Attack; // wait in attack until ready again
        }
        else if (distance <= detectionRadius)
        {
            currentState = State.Chase;
        }
        else
        {
            currentState = State.Patrol;
        }
    }

  void CancelAttack()
{
    isAttacking = false;
    attackTimer = 0f;
    cooldownTimer = attackCooldown;

    animator.ResetTrigger("Attack");

    // ⛔ Force cancel attack anim using CrossFade to Idle or Blend Tree
    if (animator.HasState(0, Animator.StringToHash("Walk")))
    {
        animator.CrossFade("Walk", 0.1f);
    }
    else
    {
        animator.CrossFade("Walk", 0.1f); // fallback to walk if no idle
    }

    if (agent.isOnNavMesh && player != null)
    {
        agent.SetDestination(player.position);
    }
}



    void RotateToVelocity()
    {
        if (agent.velocity.sqrMagnitude > 0.1f)
        {
            Quaternion targetRot = Quaternion.LookRotation(agent.velocity.normalized);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSpeed);
        }
    }
}
