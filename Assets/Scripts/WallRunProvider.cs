using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Readers;
using UnityEngine.XR.Interaction.Toolkit.Locomotion;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Jump;

namespace UnityEngine.XR.Interaction.Toolkit.Locomotion.WallRun
{
    /// <summary>
    /// Wall run provider with energy-based momentum system.
    /// Energy is gained from running speed (via AccelerationMoveProvider),
    /// depletes during wall run, and transfers (with loss) between wall jumps.
    /// </summary>
    [AddComponentMenu("XR/Locomotion/Wall Run Provider")]
    public class WallRunProvider : LocomotionProvider, IGravityController
    {
        [Header("Wall Detection")]
        [SerializeField] float m_WallCheckDistance = 0.6f;
        [SerializeField] float m_WallStickinessDistance = 1.2f;
        [SerializeField] LayerMask m_WallLayers = -1;

        [Header("Energy System")]
        [Tooltip("Maximum energy (corresponds to max speed).")]
        [SerializeField] float m_MaxEnergy = 8f;

        [Tooltip("How fast energy depletes while wall running (per second)")]
        [SerializeField] float m_EnergyDepletionRate = 1.5f;

        [Tooltip("Minimum energy required to stay attached to wall")]
        [SerializeField] float m_MinEnergyToStayAttached = 0.3f;

        [Tooltip("Minimum energy required to perform wall jump")]
        [SerializeField] float m_MinEnergyToJump = 0.5f;

        [Tooltip("Energy multiplier when transitioning to another wall (0.7 = lose 30%)")]
        [SerializeField, Range(0.3f, 0.9f)] float m_WallTransitionEnergyRetention = 0.7f;

        [Tooltip("Minimum speed required to attach to wall for the first time")]
        [SerializeField] float m_MinSpeedToAttach = 0.5f;

        [Header("Wall Run Movement")]
        [Tooltip("Base slide down speed when at full energy")]
        [SerializeField] float m_MinSlideSpeed = 0.3f;

        [Tooltip("Max slide down speed when energy is depleted")]
        [SerializeField] float m_MaxSlideSpeed = 2.5f;

        [Header("Wall Jump")]
        [Tooltip("Horizontal force pushing away from wall (scaled by energy)")]
        [SerializeField] float m_JumpOutForce = 5f;

        [Tooltip("Vertical force of wall jump (scaled by energy)")]
        [SerializeField] float m_JumpUpForce = 4f;

        [Tooltip("Minimum jump force multiplier even at low energy")]
        [SerializeField, Range(0.1f, 0.5f)] float m_MinJumpForceMultiplier = 0.25f;

        [Tooltip("Time before player can attach to another wall after jumping")]
        [SerializeField] float m_AttachCooldown = 0.3f;

        [Header("Input")]
        [SerializeField] XRInputButtonReader m_JumpInput = new XRInputButtonReader("Jump");

        [Header("References")]
        [Tooltip("Reference to AccelerationMoveProvider to get player speed. Will auto-find if not set.")]
        [SerializeField] AccelerationMoveProvider m_MoveProvider;

        // Auto-found references
        GravityProvider m_GravityProvider;
        JumpProvider m_JumpProvider;
        CharacterController m_CharacterController;

        // Wall run state
        bool m_IsWallRunning;
        Vector3 m_WallNormal;
        Vector3 m_WallForward;
        float m_NextAttachAllowedTime;

        // Energy system
        float m_CurrentEnergy;
        float m_EnergyAtChainStart;
        bool m_IsInAirChain;

        // Track last known speed before jumping (since input stops during jump)
        float m_LastKnownSpeed;

        // Jump state
        bool m_IsPerformingWallJump;
        Coroutine m_WallJumpCoroutine;

        public bool gravityPaused { get; private set; }
        public bool canProcess => isActiveAndEnabled;

        // Public getters for debugging
        public bool IsWallRunning => m_IsWallRunning;
        public float CurrentEnergy => m_CurrentEnergy;
        public float EnergyFraction => m_EnergyAtChainStart > 0 ? m_CurrentEnergy / m_EnergyAtChainStart : 0f;

#if UNITY_EDITOR
        [Header("Debug")]
        [SerializeField] bool m_ShowDebugGUI = true;

        void OnGUI()
        {
            if (!m_ShowDebugGUI) return;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.fontSize = 14;

            float moveProviderSpeed = m_MoveProvider != null ? m_MoveProvider.CurrentSpeed : 0f;

            GUILayout.BeginArea(new Rect(10, 100, 350, 250));
            GUILayout.Label($"=== Wall Run Debug ===", style);
            GUILayout.Label($"MoveProvider Speed: {moveProviderSpeed:F2} m/s", style);
            GUILayout.Label($"Last Known Speed: {m_LastKnownSpeed:F2} m/s", style);
            GUILayout.Label($"Wall Running: {m_IsWallRunning}", style);
            GUILayout.Label($"Energy: {m_CurrentEnergy:F2} / {m_EnergyAtChainStart:F2}", style);
            GUILayout.Label($"Energy %: {EnergyFraction * 100:F0}%", style);
            GUILayout.Label($"In Air Chain: {m_IsInAirChain}", style);
            GUILayout.Label($"Can Jump: {CanPerformWallJump()}", style);
            GUILayout.Label($"Cooldown: {Mathf.Max(0, m_NextAttachAllowedTime - Time.time):F2}s", style);
            GUILayout.Label($"MoveProvider found: {m_MoveProvider != null}", style);
            GUILayout.EndArea();
        }
#endif

        protected override void Awake()
        {
            base.Awake();
            m_CharacterController = GetComponentInParent<CharacterController>();
            m_GravityProvider = GetComponentInParent<GravityProvider>() ?? FindAnyObjectByType<GravityProvider>();
            m_JumpProvider = GetComponentInParent<JumpProvider>() ?? FindAnyObjectByType<JumpProvider>();

            // Find AccelerationMoveProvider if not assigned
            if (m_MoveProvider == null)
            {
                m_MoveProvider = GetComponentInParent<AccelerationMoveProvider>() ?? FindAnyObjectByType<AccelerationMoveProvider>();
            }

            if (m_MoveProvider == null)
            {
                Debug.LogError("[WallRun] AccelerationMoveProvider not found! Wall run will not work correctly.");
            }
        }

        protected virtual void OnEnable() => m_JumpInput.EnableDirectActionIfModeUsed();
        protected virtual void OnDisable() => m_JumpInput.DisableDirectActionIfModeUsed();

        void Update()
        {
            // Track speed while grounded (before jumping)
            if (m_MoveProvider != null && m_GravityProvider != null && m_GravityProvider.isGrounded)
            {
                float currentMoveSpeed = m_MoveProvider.CurrentSpeed;
                if (currentMoveSpeed > 0.1f)
                {
                    m_LastKnownSpeed = currentMoveSpeed;
                }
            }

            bool isGrounded = m_GravityProvider != null && m_GravityProvider.isGrounded;

            // Reset energy chain when touching ground
            if (isGrounded)
            {
                ResetEnergyChain();
            }

            float checkDistance = m_IsWallRunning ? m_WallStickinessDistance : m_WallCheckDistance;
            bool hitWall = CheckForWall(checkDistance);

            // Try to attach to wall
            if (!m_IsWallRunning && !isGrounded && hitWall && Time.time >= m_NextAttachAllowedTime)
            {
                TryStartWallRun();
            }

            // While wall running
            if (m_IsWallRunning)
            {
                UpdateWallRun();

                if (isGrounded)
                {
                    StopWallRun();
                }
                else if (m_JumpInput.ReadWasPerformedThisFrame() && CanPerformWallJump())
                {
                    PerformWallJump();
                }
                else if (!hitWall)
                {
                    StopWallRun();
                }
                else if (m_CurrentEnergy <= m_MinEnergyToStayAttached)
                {
                    StopWallRun();
                }
            }
        }

        void ResetEnergyChain()
        {
            m_IsInAirChain = false;
            m_EnergyAtChainStart = 0f;
        }

        bool CheckForWall(float distance)
        {
            Vector3[] directions = {
                transform.right,
                -transform.right,
                (transform.right + transform.forward).normalized,
                (-transform.right + transform.forward).normalized
            };

            RaycastHit bestHit = default;
            float bestDistance = float.MaxValue;
            bool found = false;

            foreach (var dir in directions)
            {
                if (Physics.Raycast(transform.position, dir, out RaycastHit hit, distance, m_WallLayers))
                {
                    // Must be vertical-ish (wall, not floor/ceiling)
                    if (Mathf.Abs(Vector3.Dot(hit.normal, Vector3.up)) < 0.3f)
                    {
                        if (hit.distance < bestDistance)
                        {
                            bestHit = hit;
                            bestDistance = hit.distance;
                            found = true;
                        }
                    }
                }
            }

            if (found)
            {
                m_WallNormal = bestHit.normal;
                return true;
            }
            return false;
        }

        void TryStartWallRun()
        {
            // Get speed from AccelerationMoveProvider or use last known speed
            float incomingSpeed = 0f;

            if (m_MoveProvider != null)
            {
                incomingSpeed = m_MoveProvider.CurrentSpeed;
            }

            // If current speed is 0 (player released stick mid-air), use last known speed
            if (incomingSpeed < 0.1f)
            {
                incomingSpeed = m_LastKnownSpeed;
            }

            Debug.Log($"[WallRun] TryStartWallRun - IncomingSpeed: {incomingSpeed:F2}, LastKnown: {m_LastKnownSpeed:F2}, IsInAirChain: {m_IsInAirChain}, CurrentEnergy: {m_CurrentEnergy:F2}");

            // Wall-to-wall transition
            if (m_IsInAirChain)
            {
                float chainEnergy = m_CurrentEnergy * m_WallTransitionEnergyRetention;
                m_CurrentEnergy = chainEnergy;

                Debug.Log($"[WallRun] Wall transition - ChainEnergy: {chainEnergy:F2}");

                if (m_CurrentEnergy < m_MinEnergyToStayAttached)
                {
                    Debug.Log($"[WallRun] Not enough energy for wall transition: {m_CurrentEnergy:F2} < {m_MinEnergyToStayAttached}");
                    return;
                }
            }
            else
            {
                // First wall - need minimum speed
                if (incomingSpeed < m_MinSpeedToAttach)
                {
                    Debug.Log($"[WallRun] Not enough speed to attach: {incomingSpeed:F2} < {m_MinSpeedToAttach}");
                    return;
                }

                m_CurrentEnergy = Mathf.Clamp(incomingSpeed, 0f, m_MaxEnergy);
                m_EnergyAtChainStart = m_CurrentEnergy;
                m_IsInAirChain = true;

                Debug.Log($"[WallRun] First wall attach - Energy: {m_CurrentEnergy:F2}");
            }

            StartWallRun();
        }

        void StartWallRun()
        {
            m_IsWallRunning = true;

            Debug.Log($"[WallRun] StartWallRun - Energy: {m_CurrentEnergy:F2}");

            if (m_JumpProvider != null)
                m_JumpProvider.enabled = false;

            if (m_GravityProvider != null)
            {
                m_GravityProvider.ResetFallForce();
                TryLockGravity(GravityOverride.ForcedOff);
            }

            // Calculate wall-forward direction
            m_WallForward = Vector3.Cross(m_WallNormal, Vector3.up).normalized;
            if (Vector3.Dot(m_WallForward, transform.forward) < 0f)
                m_WallForward = -m_WallForward;
        }

        void UpdateWallRun()
        {
            float dt = Time.deltaTime;

            // Deplete energy
            m_CurrentEnergy = Mathf.MoveTowards(m_CurrentEnergy, 0f, m_EnergyDepletionRate * dt);

            float energyFraction = m_EnergyAtChainStart > 0 ? m_CurrentEnergy / m_EnergyAtChainStart : 0f;

            // Horizontal movement
            float horizontalSpeed = m_CurrentEnergy;
            Vector3 moveHorizontal = m_WallForward * horizontalSpeed * dt;

            // Vertical slide (faster as energy depletes)
            float slideSpeed = Mathf.Lerp(m_MaxSlideSpeed, m_MinSlideSpeed, energyFraction);
            Vector3 moveVertical = Vector3.down * slideSpeed * dt;

            m_CharacterController.Move(moveHorizontal + moveVertical);
        }

        bool CanPerformWallJump()
        {
            return m_CurrentEnergy >= m_MinEnergyToJump && !m_IsPerformingWallJump;
        }

        void PerformWallJump()
        {
            float energyFraction = m_EnergyAtChainStart > 0 ? m_CurrentEnergy / m_EnergyAtChainStart : 0f;
            float forceMultiplier = Mathf.Lerp(m_MinJumpForceMultiplier, 1f, energyFraction);

            Vector3 jumpForce = (m_WallNormal * m_JumpOutForce + Vector3.up * m_JumpUpForce) * forceMultiplier;

            Debug.Log($"[WallRun] PerformWallJump - EnergyFraction: {energyFraction:F2}, ForceMultiplier: {forceMultiplier:F2}, RemainingEnergy: {m_CurrentEnergy:F2}");

            m_NextAttachAllowedTime = Time.time + m_AttachCooldown;

            StopWallRun();

            if (m_GravityProvider != null)
                m_GravityProvider.ResetFallForce();

            if (m_WallJumpCoroutine != null)
                StopCoroutine(m_WallJumpCoroutine);
            m_WallJumpCoroutine = StartCoroutine(ApplyWallJumpForce(jumpForce));
        }

        IEnumerator ApplyWallJumpForce(Vector3 force)
        {
            m_IsPerformingWallJump = true;

            float duration = 0.2f;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                float t = elapsed / duration;
                float forceScale = 1f - t;
                m_CharacterController.Move(force * forceScale * Time.deltaTime);
                elapsed += Time.deltaTime;
                yield return null;
            }

            yield return new WaitForSeconds(0.15f);

            if (m_JumpProvider != null)
                m_JumpProvider.enabled = true;

            m_IsPerformingWallJump = false;
            m_WallJumpCoroutine = null;
        }

        void StopWallRun()
        {
            if (!m_IsWallRunning) return;

            Debug.Log($"[WallRun] StopWallRun - RemainingEnergy: {m_CurrentEnergy:F2}");

            m_IsWallRunning = false;
            RemoveGravityLock();

            if (!m_IsPerformingWallJump && m_JumpProvider != null)
                m_JumpProvider.enabled = true;
        }

        public bool TryLockGravity(GravityOverride gravityOverride)
        {
            return m_GravityProvider != null && m_GravityProvider.TryLockGravity(this, gravityOverride);
        }

        public void RemoveGravityLock()
        {
            m_GravityProvider?.UnlockGravity(this);
        }

        void IGravityController.OnGroundedChanged(bool isGrounded)
        {
            if (isGrounded) StopWallRun();
        }

        void IGravityController.OnGravityLockChanged(GravityOverride gravityOverride) { }
    }
}