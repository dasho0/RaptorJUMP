using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;

namespace UnityEngine.XR.Interaction.Toolkit.Locomotion.WallRun
{
    [AddComponentMenu("XR/Locomotion/Wall Run Provider")]
    public class WallRunProvider : LocomotionProvider, IGravityController
    {
        [Header("Detection Settings")]
        [SerializeField] float m_WallCheckDistance = 0.6f;
        [Tooltip("How far the ray reaches AFTER you are already attached (prevents flickering).")]
        [SerializeField] float m_WallStickinessDistance = 1.2f;
        [SerializeField] LayerMask m_WallLayers = -1;
        [SerializeField] float m_AttachCooldown = 0.4f; // cooldown before re-attach allowed

        [Header("Movement Settings")]
        [SerializeField] float m_WallSlideSpeed = 0.5f; // base slow slide (used when speed fraction == 1)
        [SerializeField] float m_WallJumpUpForce = 8f;
        [SerializeField] float m_WallJumpOutForce = 7f;

        [Header("Wall-Run Speed / Deceleration")]
        [SerializeField] float m_MaxWallRunSpeed = 8f;
        [SerializeField] float m_WallRunDeceleration = 2f; // m/s^2
        [SerializeField] float m_MinSpeedToStayAttached = 0.5f; // below this we detach

        [Header("Jump / Input")]
        [SerializeField] XRInputButtonReader m_JumpInput = new XRInputButtonReader("Jump");
        [SerializeField] float m_PostWallJumpJumpProviderDelay = 0.18f; // delay before re-enabling JumpProvider

        // internal refs
        GravityProvider m_GravityProvider;
        JumpProvider m_JumpProvider;
        CharacterController m_CharacterController;

        // state
        bool m_IsWallRunning;
        Vector3 m_WallNormal;
        Vector3 m_WallForward;
        float m_NextAttachAllowedTime;
        bool m_IsWallJumpingInProgress;
        bool m_HasWallJumped; // prevents double wall-jump spam
        Coroutine m_ReenableJumpProviderCoroutine;

        // speed tracking for wall-run
        float m_InitialWallRunSpeed;
        float m_CurrentWallRunSpeed;

        // fallback speed estimation
        Vector3 m_LastPosition;

        public bool gravityPaused { get; private set; }
        public bool canProcess => isActiveAndEnabled;

        protected override void Awake()
        {
            base.Awake();
            m_CharacterController = GetComponentInParent<CharacterController>();
            m_GravityProvider = GetComponentInParent<GravityProvider>() ?? Object.FindAnyObjectByType<GravityProvider>();
            m_JumpProvider = GetComponentInParent<JumpProvider>() ?? Object.FindAnyObjectByType<JumpProvider>();
            m_LastPosition = transform.position;
        }

        protected virtual void OnEnable() => m_JumpInput.EnableDirectActionIfModeUsed();
        protected virtual void OnDisable() => m_JumpInput.DisableDirectActionIfModeUsed();

        void LateUpdate()
        {
            // store last position for fallback speed estimate
            m_LastPosition = transform.position;
        }

        void Update()
        {
            // Use a longer ray if we are already wall running to stay "glued"
            float currentCheckDist = m_IsWallRunning ? m_WallStickinessDistance : m_WallCheckDistance;
            bool hitWall = CheckForWall(currentCheckDist);
            bool isGrounded = m_GravityProvider.isGrounded;

            // ATTACH: Airborne + Wall Found + Cooldown finished
            if (!m_IsWallRunning && !isGrounded && hitWall && Time.time >= m_NextAttachAllowedTime)
            {
                StartWallRun();
            }

            // WHILE RUNNING
            if (m_IsWallRunning)
            {
                // Move along wall using current speed and apply vertical slide depending on speed fraction
                HandleWallRunMovement();

                // DETACH CONDITIONS
                if (isGrounded)
                {
                    StopWallRun();
                }
                else if (m_JumpInput.ReadWasPerformedThisFrame())
                {
                    if (!m_HasWallJumped)
                    {
                        PerformWallJump();
                        m_HasWallJumped = true;
                    }
                }
                // Optional: Detach if we move COMPLETELY away from the wall
                else if (!hitWall)
                {
                    StopWallRun();
                }
                // Detach if speed decayed too much
                else if (m_CurrentWallRunSpeed <= m_MinSpeedToStayAttached)
                {
                    StopWallRun();
                }
            }
        }

        bool CheckForWall(float distance)
        {
            RaycastHit hit;
            // Check in a wider arc (left, right, and slightly forward)
            bool hitRight = Physics.Raycast(transform.position, transform.right, out hit, distance, m_WallLayers);
            bool hitLeft = Physics.Raycast(transform.position, -transform.right, out hit, distance, m_WallLayers);
            bool hitForwardRight = Physics.Raycast(transform.position, (transform.right + transform.forward).normalized, out hit, distance, m_WallLayers);
            bool hitForwardLeft = Physics.Raycast(transform.position, (-transform.right + transform.forward).normalized, out hit, distance, m_WallLayers);

            if (hitRight || hitLeft || hitForwardRight || hitForwardLeft)
            {
                // Ensure it's a wall (vertical-ish) and not a ceiling/floor
                if (Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) < 0.3f)
                {
                    m_WallNormal = hit.normal;
                    return true;
                }
            }
            return false;
        }

        void StartWallRun()
        {
            m_IsWallRunning = true;
            if (m_JumpProvider != null) m_JumpProvider.enabled = false;
            m_GravityProvider.ResetFallForce();
            TryLockGravity(GravityOverride.ForcedOff);

            // Determine incoming horizontal speed
            Vector3 horizVel = Vector3.zero;
            if (m_CharacterController != null)
            {
                // Use CharacterController.velocity if available
                horizVel = m_CharacterController.velocity;
                horizVel.y = 0f;
            }

            // Fallback: estimate from last position if velocity is near-zero
            if (horizVel.sqrMagnitude < 0.001f)
            {
                Vector3 est = (transform.position - m_LastPosition) / Mathf.Max(Time.deltaTime, 1e-6f);
                est.y = 0f;
                horizVel = est;
            }

            m_InitialWallRunSpeed = horizVel.magnitude;
            m_CurrentWallRunSpeed = Mathf.Clamp(m_InitialWallRunSpeed, 0f, m_MaxWallRunSpeed);

            // Determine wall-forward direction (along wall, aligned with player's forward where possible)
            m_WallForward = Vector3.Cross(m_WallNormal, Vector3.up).normalized;
            if (Vector3.Dot(m_WallForward, transform.forward) < 0f)
                m_WallForward = -m_WallForward;

            // reset jump flags
            m_HasWallJumped = false;
        }

        void HandleWallRunMovement()
        {
            // Move along wall
            Vector3 moveAlong = m_WallForward * m_CurrentWallRunSpeed * Time.deltaTime;

            // Compute speed fraction relative to initial (protect division by zero)
            float speedFraction = (m_InitialWallRunSpeed > 0f) ? (m_CurrentWallRunSpeed / m_InitialWallRunSpeed) : 0f;
            // when speedFraction==1 => gentle slide, when ->0 => faster fall
            float slideDown = Mathf.Lerp(m_WallSlideSpeed, m_WallSlideSpeed * 4f, 1f - speedFraction);

            Vector3 totalMove = moveAlong + Vector3.down * slideDown * Time.deltaTime;

            // Apply move through CharacterController
            m_CharacterController.Move(totalMove);

            // Decelerate
            m_CurrentWallRunSpeed = Mathf.MoveTowards(m_CurrentWallRunSpeed, 0f, m_WallRunDeceleration * Time.deltaTime);
        }

        void StopWallRun()
        {
            if (!m_IsWallRunning) return;
            m_IsWallRunning = false;

            // If we're currently in a wall-jump re-enable coroutine, don't enable jumpprovider here.
            if (!m_IsWallJumpingInProgress)
            {
                if (m_JumpProvider != null) m_JumpProvider.enabled = true;
            }

            RemoveGravityLock();
        }

        void PerformWallJump()
        {
            Vector3 jumpDirection = (Vector3.up * m_WallJumpUpForce) + (m_WallNormal * m_WallJumpOutForce);
            m_NextAttachAllowedTime = Time.time + m_AttachCooldown;
            StopWallRun();
            m_GravityProvider.ResetFallForce();

            if (m_ReenableJumpProviderCoroutine != null) StopCoroutine(m_ReenableJumpProviderCoroutine);
            m_ReenableJumpProviderCoroutine = StartCoroutine(DoWallJumpAndReenableJumpProvider(jumpDirection));
        }

        System.Collections.IEnumerator DoWallJumpAndReenableJumpProvider(Vector3 force)
        {
            // Mark that we're in jump-in-progress (prevents StopWallRun from re-enabling JumpProvider immediately)
            m_IsWallJumpingInProgress = true;

            // Apply a short impulse period for cleaner detachment
            float timer = 0f;
            float impulseDuration = 0.25f;
            while (timer < impulseDuration)
            {
                m_CharacterController.Move(force * Time.deltaTime);
                timer += Time.deltaTime;
                yield return null;
            }

            // small delay to prevent immediate re-jump spam
            yield return new WaitForSeconds(m_PostWallJumpJumpProviderDelay);

            if (m_JumpProvider != null) m_JumpProvider.enabled = true;

            // Safety: set flags to allow further jumps in future (player must press jump again)
            m_HasWallJumped = false;
            m_IsWallJumpingInProgress = false;
            m_ReenableJumpProviderCoroutine = null;
        }

        public bool TryLockGravity(GravityOverride gravityOverride) => m_GravityProvider.TryLockGravity(this, gravityOverride);
        public void RemoveGravityLock() => m_GravityProvider.UnlockGravity(this);
        void IGravityController.OnGroundedChanged(bool isGrounded) { if (isGrounded) StopWallRun(); }
        void IGravityController.OnGravityLockChanged(GravityOverride gravityOverride) { /* no-op */ }
    }
}
