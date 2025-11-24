using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class AccelerationMoveProvider : ContinuousMoveProvider
{
    [SerializeField] private float baseAcceleration = 0.222f;
    [SerializeField] private float speedCap = 6.9f;
    [SerializeField] private GravityProvider gravityProvider;
    [SerializeField] private CharacterController characterController;
    [SerializeField, Range(0.25f, 2f)]
    private float shapeExponent = 1.0f;
    [SerializeField]
    private float normalizeAtTime = 4f;

    private float currentSpeed = 0f;

    #if UNITY_EDITOR
    private float debug_angleBetweenMovementAndForward = 0f;

    public void OnGUI() {
            GUILayout.Label($"Angle between forward and movement: {debug_angleBetweenMovementAndForward}");
    }
#endif

    private float AccelerateFromCurrent(float sCurrent)
    {
        var k = baseAcceleration;
        var cap = speedCap;
        var alpha = shapeExponent;
        var T = normalizeAtTime;
        var dt = Time.deltaTime;

        var denom = 1f - Mathf.Exp(-k * Mathf.Pow(T, alpha));

        // Guard against invalid state
        if(cap <= 0f || denom <= 0f || sCurrent <= 0f) {
            return Mathf.Min(sCurrent + k * dt * cap, cap);
        }

        // s(t) = cap * (1 - exp(-k * t^alpha)) / denom
        var A = Mathf.Clamp01((sCurrent / cap) * denom);
        if (A >= 1f)
            return cap;

        var tPowA = -Mathf.Log(1f - A) / k;
        var t = Mathf.Pow(tPowA, 1f / alpha);

        // ds/dt = cap/denom * k * alpha * t^(alpha-1) * exp(-k * t^alpha)
        var tAlpha = Mathf.Pow(t, alpha);
        var expTerm = Mathf.Exp(-k * tAlpha);
        var dsdt = cap / denom * k * alpha * Mathf.Pow(t, alpha - 1f) * expTerm;

        // if (!float.IsFinite(dsdt) || dsdt <= 0f) {
        //     dsdt = k * cap;
        // }

        var sNext = sCurrent + dsdt * dt;
        return Mathf.Min(sNext, cap);
    }

    private float GetDynamicSpeedCap(Vector3 movementForward, Vector3 movementInWorldSpace)
    {
        if(movementInWorldSpace == Vector3.zero)
            return speedCap;

        Vector3 forwardDir2D = new Vector3(movementForward.x, 0, movementForward.z).normalized;
        Vector3 movement2D = new Vector3(movementInWorldSpace.x, 0f, movementInWorldSpace.z).normalized;

        // Debug.DrawRay(characterController.transform.position, forwardDir2D, Color.yellow);
        // Debug.DrawRay(characterController.transform.position, movement2D, Color.blue);

        float angle = Vector3.Angle(forwardDir2D, movement2D);
        float absAngle = Mathf.Abs(angle);

        #if UNITY_EDITOR
            debug_angleBetweenMovementAndForward = angle;
        #endif

        if(absAngle <= 90f) {
            float t = absAngle / 90f;
            return Mathf.Lerp(speedCap, speedCap * 0.5f, t);
        }

        return speedCap * 0.5f;
    }

    protected override Vector3 ComputeDesiredMove(Vector2 input) {
        var shouldAccelerate = gravityProvider.isGrounded;
        // TODO: this should be handled properly probably

        if(input == Vector2.zero) {
            currentSpeed = 0f;
            return Vector3.zero;
        }

        var xrOrigin = mediator.xrOrigin;
        if(xrOrigin == null)
            return Vector3.zero;

        // Assumes that the input axes are in the range [-1, 1].
        // Clamps the magnitude of the input direction to prevent faster speed when moving diagonally,
        // while still allowing for analog input to move slower (which would be lost if simply normalizing).
        var inputMove = Vector3.ClampMagnitude(new Vector3(base.enableStrafe ? input.x : 0f, 0f, input.y), 1f);

        var deltaTime = Time.deltaTime;

        // Determine frame of reference for what the input direction is relative to
        var forwardSourceTransform = forwardSource == null ? xrOrigin.Camera.transform : forwardSource;
        var inputForwardInWorldSpace = forwardSourceTransform.forward;
        var originTransform = xrOrigin.Origin.transform;
        var originUp = originTransform.up;
        var inputForwardProjectedInWorldSpace = Vector3.ProjectOnPlane(inputForwardInWorldSpace, originUp);

        var forwardRotation = Quaternion.FromToRotation(originTransform.forward, inputForwardProjectedInWorldSpace);
        var rigSpaceDir = new Vector3(inputMove.x, 0f, inputMove.z).normalized;
        var translationInRigSpaceNormalized = forwardRotation * rigSpaceDir;
        var translationInWorldSpaceNormalized  = originTransform.TransformDirection(translationInRigSpaceNormalized);

        var cap = GetDynamicSpeedCap(inputForwardProjectedInWorldSpace, translationInWorldSpaceNormalized);

        if(shouldAccelerate) {
            currentSpeed = Math.Min(cap, AccelerateFromCurrent(currentSpeed));
        }

        var speedFactor = currentSpeed * deltaTime * originTransform.localScale.x; // Adjust speed with user scale

        // If flying, just compute move directly from input and forward source
        if(enableFly) {
            var inputRightInWorldSpace = forwardSourceTransform.right;
            var combinedMove = inputMove.x * inputRightInWorldSpace + inputMove.z * inputForwardInWorldSpace;
            return combinedMove * speedFactor;
        }

        if(Mathf.Approximately(Mathf.Abs(Vector3.Dot(inputForwardInWorldSpace, originUp)), 1f)) {
            // When the input forward direction is parallel with the rig normal,
            // it will probably feel better for the player to move along the same direction
            // as if they tilted forward or up some rather than moving in the rig forward direction.
            // It also will probably be a better experience to at least move in a direction
            // rather than stopping if the head/controller is oriented such that it is perpendicular with the rig.
            inputForwardInWorldSpace = -forwardSourceTransform.up;
        }

        var translationInWorldSpace = translationInWorldSpaceNormalized * speedFactor;
        return translationInWorldSpace;
    }

    public void ProcessWallCollision(ControllerColliderHit hit) {
        Debug.Log($"hit wall: {hit}");
        if (Vector3.Dot(hit.normal, Vector3.up) > 0.7f || Vector3.Dot(hit.normal, Vector3.down) > 0.7f)
            return;

        var wallNormal = hit.normal.normalized;
        var desiredMove = hit.controller.velocity * Time.deltaTime;

        var componentIntoWall = Vector3.Dot(desiredMove, wallNormal) * wallNormal;
        var componentAlongWall = desiredMove - componentIntoWall;

        if (desiredMove.magnitude > 0f)
        {
            float speedReduction = componentAlongWall.magnitude / desiredMove.magnitude;
            currentSpeed *= speedReduction;
        }
    }
}
