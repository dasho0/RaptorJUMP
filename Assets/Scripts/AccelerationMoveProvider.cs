using System;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Gravity;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Movement;

public class AccelerationMoveProvider : ContinuousMoveProvider
{
    [SerializeField] private float baseAcceleration = 0.2f;
    [SerializeField] private float speedCap = 5f;
    [SerializeField] private GravityProvider gravityProvider;
    [SerializeField] private CharacterController characterController;
    [SerializeField, Range(0.25f, 2f)]
    private float shapeExponent = 1.0f;
    [SerializeField]
    private float normalizeAtTime = 4f;

    private bool hasTouchedWall = false;

    private float currentSpeed = 0f;

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

        if(shouldAccelerate) {
            currentSpeed = AccelerateFromCurrent(currentSpeed);
        }

        var originTransform = xrOrigin.Origin.transform;
        var speedFactor = currentSpeed * deltaTime * originTransform.localScale.x; // Adjust speed with user scale

        // If flying, just compute move directly from input and forward source
        if(enableFly) {
            var inputRightInWorldSpace = forwardSourceTransform.right;
            var combinedMove = inputMove.x * inputRightInWorldSpace + inputMove.z * inputForwardInWorldSpace;
            return combinedMove * speedFactor;
        }

        var originUp = originTransform.up;

        if(Mathf.Approximately(Mathf.Abs(Vector3.Dot(inputForwardInWorldSpace, originUp)), 1f)) {
            // When the input forward direction is parallel with the rig normal,
            // it will probably feel better for the player to move along the same direction
            // as if they tilted forward or up some rather than moving in the rig forward direction.
            // It also will probably be a better experience to at least move in a direction
            // rather than stopping if the head/controller is oriented such that it is perpendicular with the rig.
            inputForwardInWorldSpace = -forwardSourceTransform.up;
        }

        var inputForwardProjectedInWorldSpace = Vector3.ProjectOnPlane(inputForwardInWorldSpace, originUp);
        var forwardRotation = Quaternion.FromToRotation(originTransform.forward, inputForwardProjectedInWorldSpace);

        var rigSpaceDir = new Vector3(inputMove.x, 0f, inputMove.z).normalized;
        var translationInRigSpace = forwardRotation * rigSpaceDir * speedFactor;
        var translationInWorldSpace = originTransform.TransformDirection(translationInRigSpace);

        if(translationInWorldSpace.sqrMagnitude > 0f) {
            var rayOrigin = characterController.transform.position;
            var rayDir = translationInWorldSpace.normalized;
            var distance = translationInWorldSpace.magnitude + characterController.radius;

            Debug.DrawRay(rayOrigin, rayDir * (distance + 0.1f), Color.red, 1f);

            if(Physics.Raycast(new Ray(rayOrigin, rayDir), out var hitInfo, distance)) {
                Debug.Log($"Coliding with wall with speed: {currentSpeed}");
                Debug.DrawLine(rayOrigin, hitInfo.point, Color.green, 1f);
                if(!hasTouchedWall) {
                    Debug.Log($"Entering wallhug with speed: {currentSpeed}");
                }

                hasTouchedWall = true;

                var wallNormal = hitInfo.normal.normalized;

                var desiredMove = translationInWorldSpace;
                var componentIntoWall = Vector3.Dot(desiredMove, wallNormal) * wallNormal;
                var componentAlongWall = desiredMove - componentIntoWall;

                if(componentAlongWall.sqrMagnitude > distance * distance) {
                    componentAlongWall = componentAlongWall.normalized * distance;
                }

                currentSpeed *= componentAlongWall.magnitude / componentIntoWall.magnitude;
                // currentSpeed = 0;

                // translationInWorldSpace = componentAlongWall;
            } else {
                if(hasTouchedWall) {
                    Debug.Log($"Stopped touching wall, after hugging it, with speed: {currentSpeed}");
                }

                hasTouchedWall = false;
            }
        }

        return translationInWorldSpace;
    }
}
